using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using AionMeter.App.Services;
using AionMeter.Core.Updates;

namespace AionMeter.App.Windows;

/// <summary>
/// "A new version is out": what changed, then Update / Later / Skip this version. Installed and portable copies
/// download their matching package and restart themselves after applying it.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly Updater _updater;
    private bool _canInstall;
    private bool _useInstaller;
    private CancellationTokenSource? _download;

    public UpdateWindow(Updater updater, ReleaseInfo release, bool installed)
    {
        _updater = updater;
        Release = release;
        InitializeComponent();
        Controls.DragAnywhere.Attach(this);

        var t = UiText.Current;
        var current = updater.Current.ToString(3);
        Headline.Text = string.Format(t.UpdateHeadline, release.Version.ToString(3));
        SubLine.Text = release.PublishedAt is { } at
            ? string.Format(t.UpdateReleased, current, FormatDate(at))
            : string.Format(t.UpdateYouHave, current);
        ShowNotes(release.Notes);
        _useInstaller = installed && release.Installer is not null;
        _canInstall = _useInstaller || release.Portable is not null;
        UpdateButton.Content = _canInstall ? t.UpdateNow : t.UpdateOpenPage;
        Hint.Text = _useInstaller ? t.UpdateInstalledHint : _canInstall ? t.UpdatePortableHint : t.UpdatePortableManualHint;
    }

    public ReleaseInfo Release { get; }

    private static string FormatDate(DateTimeOffset at) =>
        at.LocalDateTime.ToString(UiText.Current.Code == "ru" ? "d MMMM yyyy" : "MMMM d, yyyy", UiText.Current.Culture);

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (!_canInstall)
        {
            Updater.OpenReleasePage(Release);
            Close();
            return;
        }

        var t = UiText.Current;
        UpdateButton.IsEnabled = SkipButton.IsEnabled = false;
        DownloadBar.Visibility = State.Visibility = Visibility.Visible;
        State.Text = string.Format(t.UpdateDownloading, 0);
        _download = new CancellationTokenSource();
        var progress = new Progress<double>(f =>
        {
            DownloadBar.Value = f;
            State.Text = string.Format(t.UpdateDownloading, f * 100);
        });
        try
        {
            if (_useInstaller)
            {
                var installer = await _updater.DownloadInstallerAsync(Release, progress, _download.Token);
                State.Text = t.UpdateStarting;
                Log.Info($"Updating to {Release.Tag} with {installer}");
                Updater.LaunchInstaller(installer);
            }
            else
            {
                var archive = await _updater.DownloadPortableAsync(Release, progress, _download.Token);
                State.Text = t.UpdateStartingPortable;
                Log.Info($"Updating portable copy to {Release.Tag} with {archive}");
                Updater.LaunchPortableUpdate(archive);
            }
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) when (_download.IsCancellationRequested)
        {
            // the window was closed while downloading
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or InvalidOperationException
                                       or OperationCanceledException or Win32Exception)
        {
            Log.Error("Update failed", ex);
            State.Text = string.Format(t.UpdateFailed, ex is OperationCanceledException ? "timeout" : ex.Message);
            DownloadBar.Visibility = Visibility.Collapsed;
            _canInstall = false;
            _useInstaller = false;
            UpdateButton.Content = t.UpdateOpenPage;
            UpdateButton.IsEnabled = SkipButton.IsEnabled = true;
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _updater.Skip(Release);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _download?.Cancel();
        base.OnClosed(e);
    }

    private void ShowNotes(string markdown)
    {
        Notes.Inlines.Clear();
        var gold = (Brush)FindResource("Gold");
        foreach (var (text, heading) in ReleaseNotes.Lines(markdown, UiText.Current.Code))
        {
            if (Notes.Inlines.Count > 0) Notes.Inlines.Add(new LineBreak());
            Notes.Inlines.Add(heading ? new Run(text) { FontWeight = FontWeights.SemiBold, Foreground = gold } : new Run(text));
        }
    }
}
