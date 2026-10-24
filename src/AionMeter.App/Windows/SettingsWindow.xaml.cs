using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AionMeter.App.Services;
using AionMeter.Core.Capture;
using AionMeter.Core.Combat;

namespace AionMeter.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly MeterService _meter;
    private readonly List<(string? Name, string Label)> _devices = new();
    private readonly double _originalOverlayWidth;
    private readonly double _originalOverlayHeight;
    private readonly double _originalOverlayScale;
    private Controls.EventsPage? _eventsPage;
    public string CurrentMarkerLabel => MarkerLabel.Text;
    private Button? _activeSettingsNav;
    private double _previewWidth;
    private double _previewHeight;
    private bool _settingsSaved;

    public SettingsWindow(MeterService meter)
    {
        _meter = meter;
        _originalOverlayWidth = meter.Settings.OverlayWidth;
        _originalOverlayHeight = meter.Settings.OverlayHeight;
        _originalOverlayScale = meter.Settings.OverlayTextScale;
        InitializeComponent();
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, workArea.Right - Width - workArea.Width * 0.085);
        Top = Math.Max(workArea.Top + 8, Math.Min(workArea.Top + workArea.Height * 0.09, workArea.Bottom - Height - 8));
        Controls.DragAnywhere.Attach(this);
        Load();
        _ = RefreshLicenseStatusAsync();
        // The row size slider previews on the overlay; closing without saving puts the saved size back.
        Closed += (_, _) =>
        {
            AppHost.Current.PreviewOverlayScale(_settingsSaved ? _meter.Settings.OverlayTextScale : _originalOverlayScale);
            if (!_settingsSaved) AppHost.Current.PreviewOverlaySize(_originalOverlayWidth, _originalOverlayHeight);
        };
    }

    private void Load()
    {
        var s = _meter.Settings;
        _previewWidth = s.OverlayWidth;
        _previewHeight = s.OverlayHeight;
        BarsRelative.IsChecked = s.BarsRelativeToTop;
        ShowBoss.IsChecked = s.ShowBossPanel;
        ShowGear.IsChecked = s.ShowGear;
        ShowHealingPanel.IsChecked = s.ShowHealingPanel;
        ShowPeakDps.IsChecked = s.ShowPeakDps;
        ShowPing.IsChecked = s.ShowPing;
        CombinedView.IsChecked = !s.CompactDpsOnly;
        CompactView.IsChecked = s.CompactDpsOnly;
        AutoShow.IsChecked = s.AutoShow;
        AutoHideSeconds.Text = s.AutoHideSeconds.ToString();
        ShowOnStart.IsChecked = s.ShowOnStart;
        BossAlertStagesBox.Text = s.BossAlertStages;
        BossAlertSpeech.IsChecked = s.BossAlertSpeech;
        BossAlertQuiet.IsChecked = s.BossAlertQuietDuringFight;
        BossAlertToastSeconds.Text = s.BossAlertToastSeconds.ToString();
        BossAlertVolume.Value = s.BossAlertVolume;
        BossAlertVolumeText.Text = $"{s.BossAlertVolume}%";
        BossAlertScale.Value = s.BossAlertToastScale;
        BossAlertScaleText.Text = $"{s.BossAlertToastScale:0.0}×";
        BossAlertSound.SelectedItem = BossAlertSound.Items.Cast<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, s.BossAlertSound)) ?? BossAlertSound.Items[0];
        BossAlertVolume.ValueChanged += (_, e) => BossAlertVolumeText.Text = $"{(int)e.NewValue}%";
        BossAlertScale.ValueChanged += (_, e) => BossAlertScaleText.Text = $"{e.NewValue:0.0}×";
        EventTimerOpacity.Value = s.EventTimersOpacity;
        DiscordPresence.IsChecked = s.DiscordPresenceEnabled;
        foreach (var n in new[] { 6 }) MaxRows.Items.Add(n);
        MaxRows.SelectedItem = MaxRows.Items.Cast<int>().OrderBy(n => Math.Abs(n - s.MaxRows)).First();
        OpacityValue.Minimum = 0.20;
        OpacityValue.Value = OverlayWindow.NormalizeBackgroundOpacity(s.BackgroundOpacity);
        RowSizeValue.Value = Math.Clamp(s.OverlayTextScale, .7, 1.5) * 100;
        Mode.SelectedIndex = s.TargetMode == TargetMode.BossOnly ? 0 : 1;
        Idle.Text = s.IdleTimeoutSec.ToString();
        BossIdle.Text = s.BossIdleTimeoutSec.ToString();
        SaveHistory.IsChecked = s.SaveHistory;
        HistoryModeBox.SelectedIndex = (int)s.HistoryMode;
        HistoryMin.Text = s.HistoryMinSeconds.ToString();
        HistoryInfo.Text = string.Format(T.HistoryInfo, _meter.History.List().Count, _meter.History.Directory);
        Record.IsChecked = s.RecordPackets;
        DebugUnknownPackets.IsChecked = s.DebugUnknownPackets;
        HkToggle.Text = s.HotkeyToggleOverlay;
        HkReset.Text = s.HotkeyReset;
        HkClick.Text = s.HotkeyClickThrough;
        HkTimers.Text = s.HotkeyTimers;
        HkMarker.Text = s.HotkeyRecordingMarker;
        MarkerLabel.Text = s.RecordingMarkerLabel;

        _devices.Add((null, T.AdapterAuto));
        try
        {
            foreach (var (name, description) in LiveCapture.ListDevices()) _devices.Add((name, description));
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            _devices.Add((null, T.NpcapMissing));
        }
        foreach (var d in _devices) Device.Items.Add(d.Label);
        var idx = _devices.FindIndex(d => d.Name == s.CaptureDevice);
        Device.SelectedIndex = Math.Max(0, idx);

        foreach (var (code, name) in UiText.LanguageOptions)
            LanguageBox.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        LanguageBox.SelectedIndex = Array.IndexOf(UiText.Languages, UiText.Normalize(s.Language));
        Icons.IsChecked = s.DownloadIcons;
        CheckUpdates.IsChecked = s.CheckUpdates;
        AutoInstallUpdates.IsChecked = s.AutoInstallUpdates;
        CheckNowButton.IsEnabled = true;
        VersionText.Text = string.Format(Updater.IsInstalled ? T.VersionInstalled : T.VersionPortable, _meter.Updates.Current.ToString(3));

        CaptureState.Text = T.StatusPrefix + _meter.CaptureStatus.Message;
        RefreshDiagnostics();
        var data = _meter.Data;
        DataInfo.Text = string.Format(T.DataInfo, data.Skills.Count, data.Npcs.Count, data.Npcs.Values.Count(n => n.IsBoss),
            data.DotSkills.Count, data.Maps.Count);
        SelectSettingsPage("overlay", NavOverlayButton);
    }

    private static UiText T => UiText.Current;

    private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticFeedback.Text = "";
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        var status = _meter.CaptureStatus;
        var gameDetected = IsGameDetected();
        var npcapInstalled = IsNpcapInstalled();

        SetDiagnostic(GameCheckValue, gameDetected ? true : null, gameDetected ? T.CheckDetected : T.CheckNotDetected);
        SetDiagnostic(NpcapCheckValue, npcapInstalled, npcapInstalled ? T.CheckInstalled : T.CheckMissing);

        var captureResult = status.State switch
        {
            AionMeter.Core.Capture.CaptureState.Capturing => (Passed: (bool?)true, Text: T.CheckCapturing),
            AionMeter.Core.Capture.CaptureState.Error => (Passed: (bool?)false, Text: T.CheckCaptureError),
            AionMeter.Core.Capture.CaptureState.Starting => (Passed: (bool?)null, Text: T.CheckStarting),
            AionMeter.Core.Capture.CaptureState.WaitingForGame => (Passed: (bool?)null, Text: T.CheckWaiting),
            _ => (Passed: (bool?)null, Text: T.CheckStopped),
        };
        SetDiagnostic(CaptureCheckValue, captureResult.Passed, captureResult.Text);

        SetDiagnostic(PacketsCheckValue, status.Packets > 0 ? true : null,
            status.Packets > 0 ? string.Format(T.CheckPacketCount, status.Packets) : T.CheckWaitingPackets);

        var nameKnown = !string.IsNullOrWhiteSpace(_meter.Tracker.SelfName);
        var eventText = status.Events > 0
            ? string.Format(T.CheckEventCount, status.Events) + " · " + (nameKnown ? T.CheckNameKnown : T.CheckNameUnknown)
            : T.CheckWaitingCombat + " · " + T.CheckNameUnknown;
        SetDiagnostic(DecodedCheckValue, status.Events > 0 ? true : null, eventText);

        var overlayVisible = _meter.Settings.OverlayVisible;
        SetDiagnostic(OverlayCheckValue, overlayVisible ? true : null,
            overlayVisible ? T.CheckOverlayVisible : T.CheckOverlayHidden);
    }

    private void SetDiagnostic(TextBlock target, bool? passed, string text)
    {
        target.Text = text;
        var key = passed switch { true => "Gold", false => "Red", _ => "TextDim" };
        target.Foreground = (System.Windows.Media.Brush)FindResource(key);
    }

    private static bool IsGameDetected()
    {
        try { return GameProcessLocator.FindGameProcessIds().Length > 0; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    private static bool IsNpcapInstalled()
    {
        try { return LiveCapture.IsNpcapInstalled(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private void CopyDiagnosticReport_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnostics();
        var status = _meter.CaptureStatus;
        var gameDetected = IsGameDetected();
        var npcapInstalled = IsNpcapInstalled();
        var nameKnown = !string.IsNullOrWhiteSpace(_meter.Tracker.SelfName);
        var version = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        var report = string.Join(Environment.NewLine,
            "Aetherion connection check",
            "Version: " + version,
            "AION2 process detected: " + gameDetected,
            "Npcap installed: " + npcapInstalled,
            "Capture state: " + status.State,
            "Game packets: " + status.Packets,
            "Decoded events: " + status.Events,
            "Character identified: " + nameKnown,
            "Overlay visible: " + _meter.Settings.OverlayVisible);
        try
        {
            Clipboard.SetText(report);
            DiagnosticFeedback.Text = T.SupportReportCopied;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            DiagnosticFeedback.Text = T.ClipboardUnavailable;
        }
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } button) return;
        SelectSettingsPage(section, button);
    }

    private void PetTracker_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowPetTracker();

    private void SelectSettingsPage(string page, Button? button = null)
    {
        if (_activeSettingsNav is not null)
        {
            _activeSettingsNav.Background = System.Windows.Media.Brushes.Transparent;
            _activeSettingsNav.Foreground = (System.Windows.Media.Brush)FindResource("TextDim");
            if (_activeSettingsNav.Template.FindName("Marker", _activeSettingsNav) is FrameworkElement oldMarker)
                oldMarker.Visibility = Visibility.Collapsed;
        }
        if (button is not null)
        {
            button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, 0xF0, 0x6A, 0x22));
            button.Foreground = (System.Windows.Media.Brush)FindResource("GoldBright");
            if (button.Template.FindName("Marker", button) is FrameworkElement marker)
                marker.Visibility = Visibility.Visible;
            _activeSettingsNav = button;
        }

        if (page == "events") EventsHost.Content = _eventsPage ??= new Controls.EventsPage(_meter, () => MarkerLabel.Text, recording => Record.IsChecked = recording);

        FrameworkElement[] visible = page switch
        {
            "timer" => new[] { TimerSection },
            "fights" => new[] { HistorySection, CaptureSection },
            "journal" => new[] { JournalSection },
            "events" => new[] { EventsSection },
            "appearance" => new[] { AppearanceSection, LanguageSection, DataFilesSection },
            "general" => new[] { OverlayBehaviorSection, MeterSection, HotkeysSection, LicenseSection, DataSection },
            "account" => new[] { PremiumSection, UpdatesSection },
            "pvp" => new[] { PvpSection },
            "map" => new[] { MapSection },
            "guide" => new[] { GuideSection },
            _ => new[] { OverlaySection },
        };
        FrameworkElement[] allSections = new FrameworkElement[] { PremiumSection, LanguageSection, OverlaySection, MapSection, GuideSection, AppearanceSection, TimerSection, MeterSection,
            HistorySection, CaptureSection, OverlayBehaviorSection, HotkeysSection, LicenseSection, UpdatesSection, DataSection, DataFilesSection, PvpSection, JournalSection, EventsSection };
        foreach (var section in allSections)
            section.Visibility = visible.Contains(section) ? Visibility.Visible : Visibility.Collapsed;
        DisclaimerText.Visibility = page == "general" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "timer" => T.NavTimer,
            "fights" => T.NavFights,
            "journal" => "Kampfbuch & Training",
            "events" => "Ereignisse",
            "appearance" => T.NavAppearance,
            "general" => T.NavGeneral,
            "account" => T.NavAccount,
            "pvp" => T.PvpPageTitle,
            "map" => T.MapPageTitle,
            "guide" => T.GuidePageTitle,
            _ => T.SettingsOverlayTitle,
        };
        PageSubtitle.Text = page switch
        {
            "events" => "Aufnehmen, markieren und eigene Ereignisse verfolgen – ausschließlich lokal.",
            "timer" => T.TimerPageHint,
            "journal" => "Boss-Tagebuch, Heilung, Trainingsziele und Discord – an einem Ort.",
            "pvp" => T.PvpPageHint,
            "map" => T.MapPageHint,
            "guide" => T.GuidePageHint,
            _ => T.SettingsPageHint,
        };
        SettingsScroll.ScrollToTop();
    }

    internal void ShowSettingsPageForPreview(string page) => SelectSettingsPage(page, page == "journal" ? NavJournalButton : page == "events" ? NavEventsButton : null);

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset }) return;
        MaxRows.SelectedItem = 6;
        RowSizeValue.Value = preset switch { "compact" => 85, "large" => 125, _ => 100 };
        // Keep the live preview, but a typography preset does not resize the window.
        var overlay = Application.Current.Windows.OfType<OverlayWindow>().FirstOrDefault();
        AppHost.Current.PreviewOverlaySize(overlay?.Width ?? _previewWidth, overlay?.Height ?? _previewHeight);
    }

    private void OverlayView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string mode }) return;
        CompactView.IsChecked = mode == "compact";
        CombinedView.IsChecked = mode != "compact";
    }

    private void FarmGuide_Click(object sender, RoutedEventArgs e) => new FarmGuideWindow { Owner = this }.ShowDialog();

    private void OpenMap_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowAionMap();

    private void WorldBossTimers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowBossTimers();

    private void EventTimers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowEventTimers();

    private void OpenGuide_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowWakayashi();

    private void DiscordSupport_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.gg/mostwantedde") { UseShellExecute = true });

    private async Task RefreshLicenseStatusAsync()
    {
        try
        {
            var status = await SupabaseAccessService.CheckSavedSessionAsync();
            LicenseStatusText.Text = SupabaseAccessService.FormatAccountLicense(status);
            RedeemLicenseButton.IsEnabled = status?.HasActiveLicense != true || (status.IsPartner == false && status.ExpiresAt is not null);
        }
        catch (Exception ex)
        {
            LicenseStatusText.Text = "Lizenzstatus konnte nicht geladen werden: " + ex.Message;
        }
    }

    private async void RedeemLicense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LicenseStatusText.Text = "Key wird geprüft …";
            var status = await SupabaseAccessService.RedeemLicenseKeyAsync(LicenseKeyBox.Text);
            LicenseKeyBox.Clear();
            LicenseStatusText.Text = SupabaseAccessService.FormatAccountLicense(status);
            RedeemLicenseButton.IsEnabled = status.HasActiveLicense && !status.IsPartner && status.ExpiresAt is not null;
        }
        catch (Exception ex)
        {
            LicenseStatusText.Text = ex.Message;
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Möchtest du dich abmelden? Aetherion wird geschlossen. Beim nächsten Start kannst du dich erneut mit Discord anmelden.",
            "Von Aetherion abmelden", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        SupabaseAccessService.SignOut();
        Application.Current.Shutdown();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _meter.Settings;
        if (!HotkeyManager.TryParse(HkMarker.Text, out _, out _) || string.IsNullOrWhiteSpace(MarkerLabel.Text) || MarkerLabel.Text.Any(char.IsControl))
        {
            MessageBox.Show(this, "Bitte einen gültigen Marker-Hotkey und ein Label mit 1–120 Zeichen eingeben.", "Ereignisse");
            return;
        }
        var captureChanged = false;

        s.BarsRelativeToTop = BarsRelative.IsChecked == true;
        s.ShowBossPanel = ShowBoss.IsChecked == true;
        s.ShowGear = ShowGear.IsChecked == true;
        s.ShowHealingPanel = ShowHealingPanel.IsChecked == true;
        s.ShowPeakDps = ShowPeakDps.IsChecked == true;
        s.ShowPing = ShowPing.IsChecked == true;
        s.CompactDpsOnly = CompactView.IsChecked == true;
        s.AutoShow = AutoShow.IsChecked == true;
        if (int.TryParse(AutoHideSeconds.Text, out var autoHide)) s.AutoHideSeconds = Math.Clamp(autoHide, 0, 3600);
        s.ShowOnStart = ShowOnStart.IsChecked == true;
        s.BossAlertStages = string.Join(',', BossAlertStagesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim(), out var n) ? Math.Clamp(n, 1, 60) : 0)
            .Where(n => n > 0).Distinct().OrderByDescending(n => n));
        if (s.BossAlertStages.Length == 0) s.BossAlertStages = "2";
        s.BossAlertMinutes = s.BossAlertStages.Split(',').Select(int.Parse).Last();
        s.BossAlertSpeech = BossAlertSpeech.IsChecked == true;
        s.BossAlertQuietDuringFight = BossAlertQuiet.IsChecked == true;
        if (int.TryParse(BossAlertToastSeconds.Text, out var toastSeconds)) s.BossAlertToastSeconds = Math.Clamp(toastSeconds, 3, 30);
        s.BossAlertVolume = (int)Math.Round(BossAlertVolume.Value);
        s.BossAlertSound = (BossAlertSound.SelectedItem as ComboBoxItem)?.Tag as string ?? "aetherion";
        s.BossAlertToastScale = Math.Clamp(BossAlertScale.Value, 0.8, 1.5);
        s.EventTimersOpacity = Math.Clamp(EventTimerOpacity.Value, 0.2, 1);
        s.DiscordPresenceEnabled = DiscordPresence.IsChecked == true;
        s.MaxRows = MaxRows.SelectedItem is int rows ? rows : 10;
        s.OverlayWidth = _previewWidth;
        s.OverlayHeight = _previewHeight;
        s.BackgroundOpacity = Math.Round(OverlayWindow.NormalizeBackgroundOpacity(OpacityValue.Value), 2);
        s.OverlayTextScale = Math.Round(Math.Clamp(RowSizeValue.Value / 100, .7, 1.5), 2);
        s.TargetMode = Mode.SelectedIndex == 1 ? TargetMode.All : TargetMode.BossOnly;
        if (int.TryParse(Idle.Text, out var idle)) s.IdleTimeoutSec = Math.Clamp(idle, 3, 120);
        if (int.TryParse(BossIdle.Text, out var bossIdle)) s.BossIdleTimeoutSec = Math.Clamp(bossIdle, 5, 300);
        s.SaveHistory = SaveHistory.IsChecked == true;
        s.HistoryMode = (HistoryMode)Math.Max(0, HistoryModeBox.SelectedIndex);
        if (int.TryParse(HistoryMin.Text, out var minSec)) s.HistoryMinSeconds = Math.Clamp(minSec, 5, 600);

        var device = _devices[Math.Max(0, Device.SelectedIndex)].Name;
        if (device != s.CaptureDevice || (Record.IsChecked == true) != s.RecordPackets) captureChanged = true;
        s.CaptureDevice = device;
        s.RecordPackets = Record.IsChecked == true;
        s.DebugUnknownPackets = DebugUnknownPackets.IsChecked == true;

        var language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";
        s.DownloadIcons = Icons.IsChecked == true;
        s.CheckUpdates = CheckUpdates.IsChecked == true;
        s.AutoInstallUpdates = AutoInstallUpdates.IsChecked == true;
        s.HotkeyToggleOverlay = HkToggle.Text.Trim();
        s.HotkeyReset = HkReset.Text.Trim();
        s.HotkeyClickThrough = HkClick.Text.Trim();
        s.HotkeyTimers = HkTimers.Text.Trim();
        s.HotkeyRecordingMarker = HkMarker.Text.Trim();
        s.RecordingMarkerLabel = MarkerLabel.Text.Trim();

        _meter.ApplySettings();
        if (captureChanged) _meter.RestartCapture();
        _settingsSaved = true;
        Close();
        AppHost.Current.OnSettingsChanged();
        if (language != UiText.Normalize(s.Language)) AppHost.Current.SetLanguage(language);
    }

    private void CustomerTools_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        new CustomerToolsWindow(_meter).Show();
    }

    private void Journal_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        AppHost.Current.ShowJournal(page: (sender as Button)?.Tag as string ?? "diary");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void TestBossAlert_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        AppHost.Current.ShowBossAlertTest();
    }

    /// <summary>The overlay follows the slider at once, so the size can be judged on the real rows.</summary>
    private void RowSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RowSizeText is null) return; // the slider sets its first value before the rest of the window exists
        var percent = (int)Math.Round(e.NewValue);
        RowSizeText.Text = $"{percent}%";
        AppHost.Current.PreviewOverlayScale(percent / 100.0);
    }

    /// <summary>Asks GitHub right away; a newer version also opens the update window.</summary>
    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        UpdateState.Visibility = Visibility.Visible;
        UpdateState.Text = T.UpdateChecking;
        var result = await AppHost.Current.CheckUpdatesNowAsync();
        UpdateState.Text = result switch
        {
            UpdateCheckResult.Available => string.Format(T.UpdateFound, _meter.Updates.Latest!.Version.ToString(3)),
            UpdateCheckResult.UpToDate => T.UpToDate,
            _ => string.Format(T.UpdateCheckFailed, _meter.Updates.LastError),
        };
        CheckNowButton.IsEnabled = true;
    }

    private static void Open(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => Open(Path.Combine(AppContext.BaseDirectory, "data"));

    private void OpenAppData_Click(object sender, RoutedEventArgs e) => Open(AppSettings.AppDataDir);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => Open(Log.Directory);
}
