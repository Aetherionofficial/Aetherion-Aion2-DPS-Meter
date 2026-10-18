using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AionMeter.App.Services;

/// <summary>Local executable and settings checkpoint; never includes credentials or recorded game traffic.</summary>
public static class UpdateRecovery
{
    private static string Root => Path.Combine(AppSettings.AppDataDir, "recovery");
    private static string Manifest => Path.Combine(Root, "previous.json");
    private sealed record Checkpoint(string Exe, string Target, string Sha256, string Version);

    public static bool Available
    {
        get
        {
            try { return Read() is not null; }
            catch (Exception ex) when (IsCheckpointError(ex)) { return false; }
        }
    }

    private static bool IsCheckpointError(Exception ex) => ex is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException;

    public static string? CreateCheckpoint()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("App-Pfad fehlt.");
        if (!Path.GetFileName(exe).Equals("Aetherion.exe", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(exe).Equals("AION2DpsMeter.exe", StringComparison.OrdinalIgnoreCase)) return null; // developer host
        Directory.CreateDirectory(Root);
        var version = typeof(UpdateRecovery).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        var backup = Path.Combine(Root, $"Aetherion-{version}.exe");
        File.Copy(exe, backup, true);
        var settings = Path.Combine(AppSettings.AppDataDir, "settings.json");
        if (File.Exists(settings)) File.Copy(settings, Path.Combine(Root, "settings-before-update.json"), true);
        else if (File.Exists(Path.Combine(Root, "settings-before-update.json"))) File.Delete(Path.Combine(Root, "settings-before-update.json"));
        var checkpoint = new Checkpoint(backup, Path.GetFullPath(exe), Hash(backup), version);
        File.WriteAllText(Manifest + ".tmp", JsonSerializer.Serialize(checkpoint));
        File.Move(Manifest + ".tmp", Manifest, true);
        return backup;
    }

    public static string Description()
    {
        try
        {
            var point = Read();
            return point is null ? "Noch kein Update-Checkpoint vorhanden." : $"Vorherige Version: {point.Version}";
        }
        catch (Exception ex) when (IsCheckpointError(ex))
        {
            return "Update-Wiederherstellung nicht verfügbar: " + ex.Message;
        }
    }

    private static Checkpoint? Read()
    {
        if (!File.Exists(Manifest)) return null;
        var point = JsonSerializer.Deserialize<Checkpoint>(File.ReadAllText(Manifest));
        if (point is null || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(point.Exe)), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase)
            || !File.Exists(point.Exe) || !string.Equals(Hash(point.Exe), point.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Update-Checkpoint fehlt oder wurde verändert.");
        var current = Environment.ProcessPath;
        if (!string.Equals(Path.GetFullPath(point.Target), current, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checkpoint gehört zu einer anderen Installation.");
        return point;
    }

    public static void Restore()
    {
        var point = Read() ?? throw new InvalidOperationException("Kein Checkpoint vorhanden.");
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { point.Exe, point.Target, Pid = Environment.ProcessId, Settings = Path.Combine(Root, "settings-before-update.json"),
            CurrentSettings = Path.Combine(AppSettings.AppDataDir, "settings.json") })));
        var script = "$ErrorActionPreference='Stop'; $p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + payload + "')) | ConvertFrom-Json; " +
            "while (Get-Process -Id $p.Pid -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 300 }; " +
            "try { $done=$false; for($i=0;$i -lt 40;$i++){try {Copy-Item -LiteralPath $p.Exe -Destination $p.Target -Force; $done=$true; break}catch{Start-Sleep -Milliseconds 500}}; " +
            "if(-not $done){throw 'App-Datei konnte nicht wiederhergestellt werden'}; " +
            "if(Test-Path -LiteralPath $p.Settings){Copy-Item -LiteralPath $p.Settings -Destination $p.CurrentSettings -Force}; " +
            "Start-Process -FilePath $p.Target -WindowStyle Hidden } catch {try {Start-Process -FilePath $p.Target -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null} catch {}; Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show($_.Exception.Message,'Aetherion Wiederherstellung') | Out-Null}";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) start.ArgumentList.Add(arg);
        _ = Process.Start(start) ?? throw new InvalidOperationException("Wiederherstellung konnte nicht gestartet werden.");
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
