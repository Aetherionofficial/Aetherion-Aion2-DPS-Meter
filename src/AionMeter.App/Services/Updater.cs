using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using AionMeter.Core.Updates;
using Microsoft.Win32;

namespace AionMeter.App.Services;

public enum UpdateCheckResult
{
    UpToDate,
    Available,
    Failed,
}

/// <summary>
/// New versions of the meter. Asks GitHub shortly after start and every few hours (Settings → Updates) and raises
/// <see cref="Changed"/> for the tray, the overlay and the update window. A copy installed by the installer updates
/// itself: it downloads the new installer in the background (<see cref="Downloaded"/>), and the app runs it at a quiet
/// moment without any window; the installer replaces this copy and starts the new one. A portable copy gets the
/// download page.
/// </summary>
public sealed class Updater : IDisposable
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(2);
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AE1B2590-0A93-4500-9CB1-0F543771957A}_is1"; // AppId in installer/AION2DpsMeter.iss

    private readonly AppSettings _settings;
    private readonly UpdateFeed _feed;
    private readonly bool _allowAutomaticDownloads;
    private DispatcherTimer? _timer;
    private Task<UpdateCheckResult>? _running;

    public Updater(AppSettings settings, UpdateFeed? feed = null, bool allowAutomaticDownloads = true)
    {
        _settings = settings;
        _allowAutomaticDownloads = allowAutomaticDownloads;
        Current = UpdateFeed.Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0));
        _feed = feed ?? new UpdateFeed($"AION2DpsMeter/{Current.ToString(3)}");
    }

    /// <summary>This copy's version (major.minor.patch).</summary>
    public Version Current { get; }

    /// <summary>The newest release GitHub reported, newer or not; null before the first successful check.</summary>
    public ReleaseInfo? Latest { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>A release newer than this copy, unless the user chose to skip that version.</summary>
    public ReleaseInfo? Available =>
        Latest is { } r && r.Version > Current && r.Version.ToString(3) != _settings.SkippedUpdate ? r : null;

    /// <summary>True when this copy runs from the folder the installer put it in (it can update itself).</summary>
    public static bool IsInstalled { get; } = DetectInstalled();

    /// <summary>This copy fetches and installs new versions by itself (installed copies, unless turned off).</summary>
    public bool AutoInstall => IsInstalled && _settings.AutoInstallUpdates;

    /// <summary>The update whose installer is downloaded and checked, waiting for a quiet moment to be installed.</summary>
    public ReleaseInfo? Downloaded { get; private set; }

    /// <summary>The background download of <see cref="Available"/> failed: fall back to asking the player.</summary>
    public bool DownloadFailed { get; private set; }

    private string? _downloadedPath;
    private bool _downloading;

    /// <summary>Raised on the UI thread after every check and when a version is skipped.</summary>
    public event Action? Changed;

    /// <summary>Automatic checks: the first shortly after start, then every few hours, while the setting is on.</summary>
    public void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = FirstCheckDelay };
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = CheckInterval;
            if (_settings.CheckUpdates) _ = CheckAsync();
        };
        _timer.Start();
    }

    /// <summary>Asks GitHub now. <see cref="UpdateCheckResult.Available"/> also for a skipped version (the user asked).</summary>
    public async Task<UpdateCheckResult> CheckAsync()
    {
        if (_running is { } running) return await running; // a check is already on its way: share its answer
        _running = RunCheckAsync();
        try
        {
            return await _running;
        }
        finally
        {
            _running = null;
        }
    }

    private async Task<UpdateCheckResult> RunCheckAsync()
    {
        try
        {
            Latest = await _feed.GetLatestAsync();
            LastError = null;
            Log.Info($"Update check: newest release {Latest?.Tag ?? "none"}, this copy {Current.ToString(3)}.");
            if (_allowAutomaticDownloads && Available is { Installer: not null } update && AutoInstall) _ = PrepareAsync(update);
            return Latest is { } r && r.Version > Current ? UpdateCheckResult.Available : UpdateCheckResult.UpToDate;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            LastError = ex is TaskCanceledException ? "timeout" : ex.Message;
            Log.Info($"Update check failed: {LastError}");
            return UpdateCheckResult.Failed;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Stop announcing this version; a newer one is announced again.</summary>
    public void Skip(ReleaseInfo release)
    {
        _settings.SkippedUpdate = release.Version.ToString(3);
        _settings.Save();
        Changed?.Invoke();
    }

    /// <summary>Downloads the release's installer to the temp folder, checked against GitHub's size and SHA-256.</summary>
    public Task<string> DownloadInstallerAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken ct) =>
        release.Installer is { } installer
            ? _feed.DownloadAsync(installer, DownloadFolder, progress, ct)
            : throw new InvalidOperationException($"Release {release.Tag} has no installer");

    /// <summary>Downloads and validates the portable update archive for one-click updates of ZIP copies.</summary>
    public Task<string> DownloadPortableAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken ct) =>
        release.Portable is { } portable
            ? _feed.DownloadAsync(portable, DownloadFolder, progress, ct)
            : throw new InvalidOperationException($"Release {release.Tag} has no portable archive");

    /// <summary>Runs a small Windows helper that waits for this process, replaces the portable files, and restarts it.</summary>
    public static void LaunchPortableUpdate(string archivePath)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate the running executable");
        var checkpoint = UpdateRecovery.CreateCheckpoint();
        var payload = JsonSerializer.Serialize(new { Backup = checkpoint, Archive = Path.GetFullPath(archivePath), Target = AppContext.BaseDirectory, Exe = Path.GetFullPath(exe), Pid = Environment.ProcessId });
        var payload64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        var script = "$ErrorActionPreference='Stop'; $j=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + payload64 + "')) | ConvertFrom-Json; " +
                     "while (Get-Process -Id $j.Pid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }; " +
                     "$stage=Join-Path $env:TEMP ('Aetherion-update-' + [guid]::NewGuid().ToString('N')); " +
                     "New-Item -ItemType Directory -Path $stage | Out-Null; " +
                     "$success=$false; try { Expand-Archive -LiteralPath $j.Archive -DestinationPath $stage -Force; " +
                     "$newExe=Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Name -match '^(Aetherion|AION2DpsMeter)\\.exe$' } | Sort-Object FullName | Select-Object -First 1; " +
                     "if (-not $newExe) { throw 'The update archive does not contain Aetherion.exe or AION2DpsMeter.exe' }; " +
                     // Keep the executable name the player launched: new direct downloads use Aetherion.exe,
                     // while existing portable installs continue to run as AION2DpsMeter.exe.
                     // Windows can keep the executable image locked briefly after the process exits, or another
                     // Aetherion instance may still be closing. Retry the replace instead of failing immediately.
                     "for ($i=0; $i -lt 40; $i++) { try { Copy-Item -LiteralPath $newExe.FullName -Destination $j.Exe -Force -ErrorAction Stop; break } catch { if ($i -eq 39) { throw }; Start-Sleep -Milliseconds 750 } }; " +
                     "foreach ($doc in @('LICENSE','INSTALL.txt')) { $source=Join-Path $stage $doc; if (Test-Path $source) { Copy-Item -LiteralPath $source -Destination $j.Target -Force } }; " +
                     "Start-Sleep -Milliseconds 500; $started=$false; for ($i=0; $i -lt 5 -and -not $started; $i++) { try { Start-Process -FilePath $j.Exe -ArgumentList '--updated' -ErrorAction Stop; $started=$true } catch { Start-Sleep -Milliseconds 700 } }; if (-not $started) { throw 'The updated app could not be restarted' }; $success=$true } catch { " +
                     "try { if ($j.Backup -and (Test-Path -LiteralPath $j.Backup)) { Copy-Item -LiteralPath $j.Backup -Destination $j.Exe -Force }; Start-Process -FilePath $j.Exe -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null } catch {}; " +
                     "Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show(('Aetherion konnte das Update nicht einspielen: ' + $_.Exception.Message + [Environment]::NewLine + 'Das Update-Archiv bleibt erhalten: ' + $j.Archive),'Aetherion Update') | Out-Null " +
                     "} finally { " +
                     "Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue; " +
                     "if ($success) { Remove-Item -LiteralPath $j.Archive -Force -ErrorAction SilentlyContinue } }";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(encoded);
        _ = Process.Start(start) ?? throw new InvalidOperationException("Could not start the portable update helper");
    }

    /// <summary>Fetches the update in the background, so it can go in at the next quiet moment.</summary>
    private async Task PrepareAsync(ReleaseInfo release)
    {
        if (_downloading || Downloaded?.Version == release.Version) return;
        _downloading = true;
        DownloadFailed = false;
        try
        {
            _downloadedPath = await DownloadInstallerAsync(release, null, CancellationToken.None);
            Downloaded = release;
            Log.Info($"Update {release.Tag} downloaded: {_downloadedPath}");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or InvalidOperationException
                                       or OperationCanceledException)
        {
            DownloadFailed = true;
            Log.Info($"Update {release.Tag} not downloaded: {ex.Message}");
        }
        finally
        {
            _downloading = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Runs the downloaded installer without any window; the caller exits right after.</summary>
    public void InstallDownloaded()
    {
        if (_downloadedPath is null) throw new InvalidOperationException("No update downloaded");
        LaunchInstaller(_downloadedPath, quiet: true);
    }

    /// <summary>
    /// Starts the downloaded installer: <paramref name="quiet"/> with no window at all (automatic updates), otherwise
    /// with a small progress window (the player clicked Update). It closes this copy if it is still running (Restart
    /// Manager), installs over it and starts the new version (<c>/RELAUNCH=1</c>, see the [Run] section of the
    /// installer). The caller exits right after.
    /// </summary>
    public static void LaunchInstaller(string path, bool quiet = false)
    {
        if (!UpdateFeed.IsProductName(FileVersionInfo.GetVersionInfo(path).ProductName, AppInfo.Name))
            throw new InvalidDataException($"{Path.GetFileName(path)} is not the {AppInfo.Name} installer");
        UpdateRecovery.CreateCheckpoint();
        Process.Start(new ProcessStartInfo(path, (quiet ? "/VERYSILENT" : "/SILENT") + " /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1")
            { UseShellExecute = true });
    }

    public static void OpenReleasePage(ReleaseInfo? release)
    {
        try
        {
            Process.Start(new ProcessStartInfo(release?.PageUrl ?? UpdateFeed.ReleasesPage) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Error("Could not open the release page", ex);
        }
    }

    private static string DownloadFolder => Path.Combine(Path.GetTempPath(), "AION2DpsMeter-update");

    /// <summary>Installers of earlier updates are no longer needed (the one running right now is locked and stays).</summary>
    public static void CleanDownloads()
    {
        try
        {
            if (!Directory.Exists(DownloadFolder)) return;
            foreach (var file in Directory.EnumerateFiles(DownloadFolder))
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromHours(1))
                    File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The render tool shows the update window and the overlay's update button with a made-up release.</summary>
    internal void Preview(ReleaseInfo release) => Latest = release;

    private static bool DetectInstalled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key?.GetValue("InstallLocation") is string location && location.Length > 0 &&
                   string.Equals(Path.GetFullPath(location).TrimEnd('\\'), Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _feed.Dispose();
    }
}
