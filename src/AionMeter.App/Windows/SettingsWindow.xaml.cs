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
    private Button? _activeSettingsNav;

    public SettingsWindow(MeterService meter)
    {
        _meter = meter;
        InitializeComponent();
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, workArea.Right - Width - workArea.Width * 0.085);
        Top = Math.Max(workArea.Top + 8, Math.Min(workArea.Top + workArea.Height * 0.09, workArea.Bottom - Height - 8));
        Controls.DragAnywhere.Attach(this);
        Load();
        _ = RefreshLicenseStatusAsync();
        // The row size slider previews on the overlay; closing without saving puts the saved size back.
        Closed += (_, _) => AppHost.Current.PreviewRowSize(_meter.Settings.RowSize);
    }

    private void Load()
    {
        var s = _meter.Settings;
        BarsRelative.IsChecked = s.BarsRelativeToTop;
        ShowBoss.IsChecked = s.ShowBossPanel;
        ShowGear.IsChecked = s.ShowGear;
        ShowHealingPanel.IsChecked = s.ShowHealingPanel;
        ShowPeakDps.IsChecked = s.ShowPeakDps;
        CombinedView.IsChecked = !s.CompactDpsOnly;
        CompactView.IsChecked = s.CompactDpsOnly;
        AutoShow.IsChecked = s.AutoShow;
        AutoHideSeconds.Text = s.AutoHideSeconds.ToString();
        ShowOnStart.IsChecked = s.ShowOnStart;
        BossAlertMinutesBox.Text = s.BossAlertMinutes.ToString();
        EventTimerOpacity.Value = s.EventTimersOpacity;
        DiscordPresence.IsChecked = s.DiscordPresenceEnabled;
        DiscordFactionBox.Items.Add(new ComboBoxItem { Content = T.DiscordFactionUnknown, Tag = "" });
        DiscordFactionBox.Items.Add(new ComboBoxItem { Content = T.DiscordFactionElyos, Tag = "Elyos" });
        DiscordFactionBox.Items.Add(new ComboBoxItem { Content = T.DiscordFactionAsmodian, Tag = "Asmodian" });
        DiscordFactionBox.SelectedIndex = Math.Max(0, DiscordFactionBox.Items.Cast<ComboBoxItem>().ToList()
            .FindIndex(i => string.Equals(i.Tag as string, s.DiscordFaction, StringComparison.OrdinalIgnoreCase)));
        foreach (var n in new[] { 5, 8, 10, 12, 15, 20 }) MaxRows.Items.Add(n);
        MaxRows.SelectedItem = MaxRows.Items.Cast<int>().OrderBy(n => Math.Abs(n - s.MaxRows)).First();
        OpacityValue.Value = s.BackgroundOpacity;
        RowSizeValue.Value = s.RowSize;
        Mode.SelectedIndex = s.TargetMode == TargetMode.BossOnly ? 0 : 1;
        Idle.Text = s.IdleTimeoutSec.ToString();
        BossIdle.Text = s.BossIdleTimeoutSec.ToString();
        SaveHistory.IsChecked = s.SaveHistory;
        HistoryModeBox.SelectedIndex = (int)s.HistoryMode;
        HistoryMin.Text = s.HistoryMinSeconds.ToString();
        HistoryInfo.Text = string.Format(T.HistoryInfo, _meter.History.List().Count, _meter.History.Directory);
        Record.IsChecked = s.RecordPackets;
        HkToggle.Text = s.HotkeyToggleOverlay;
        HkReset.Text = s.HotkeyReset;
        HkClick.Text = s.HotkeyClickThrough;
        HkTimers.Text = s.HotkeyTimers;

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
        var data = _meter.Data;
        DataInfo.Text = string.Format(T.DataInfo, data.Skills.Count, data.Npcs.Count, data.Npcs.Values.Count(n => n.IsBoss),
            data.DotSkills.Count, data.Maps.Count);
        SelectSettingsPage("overlay", NavOverlayButton);
    }

    private static UiText T => UiText.Current;

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } button) return;
        SelectSettingsPage(section, button);
    }

    private void SelectSettingsPage(string page, Button? button = null)
    {
        if (_activeSettingsNav is not null)
        {
            _activeSettingsNav.Background = System.Windows.Media.Brushes.Transparent;
            _activeSettingsNav.Foreground = (System.Windows.Media.Brush)FindResource("TextDim");
        }
        if (button is not null)
        {
            button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, 0xC9, 0xA4, 0x5C));
            button.Foreground = (System.Windows.Media.Brush)FindResource("GoldBright");
            _activeSettingsNav = button;
        }

        FrameworkElement[] visible = page switch
        {
            "timer" => new[] { TimerSection },
            "fights" => new[] { HistorySection, CaptureSection },
            "appearance" => new[] { AppearanceSection, LanguageSection, DataFilesSection },
            "general" => new[] { OverlayBehaviorSection, MeterSection, HotkeysSection, LicenseSection, DataSection },
            "account" => new[] { PremiumSection, UpdatesSection },
            _ => new[] { OverlaySection },
        };
        FrameworkElement[] allSections = new FrameworkElement[] { PremiumSection, LanguageSection, OverlaySection, AppearanceSection, TimerSection, MeterSection,
            HistorySection, CaptureSection, OverlayBehaviorSection, HotkeysSection, LicenseSection, UpdatesSection, DataSection, DataFilesSection };
        foreach (var section in allSections)
            section.Visibility = visible.Contains(section) ? Visibility.Visible : Visibility.Collapsed;
        DisclaimerText.Visibility = page == "general" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page switch
        {
            "timer" => T.NavTimer,
            "fights" => T.NavFights,
            "appearance" => T.NavAppearance,
            "general" => T.NavGeneral,
            "account" => T.NavAccount,
            _ => T.SettingsOverlayTitle,
        };
        PageSubtitle.Text = page == "timer" ? T.TimerPageHint : T.SettingsPageHint;
        SettingsScroll.ScrollToTop();
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset }) return;
        (int rows, int size) = preset switch
        {
            "compact" => (5, 85),
            "large" => (15, 100),
            _ => (10, 90),
        };
        MaxRows.SelectedItem = MaxRows.Items.Cast<int>().OrderBy(value => Math.Abs(value - rows)).First();
        RowSizeValue.Value = size;
    }

    private void OverlayView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string mode }) return;
        CompactView.IsChecked = mode == "compact";
        CombinedView.IsChecked = mode != "compact";
    }

    private void FarmGuide_Click(object sender, RoutedEventArgs e) => new FarmGuideWindow { Owner = this }.ShowDialog();

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
        var captureChanged = false;

        s.BarsRelativeToTop = BarsRelative.IsChecked == true;
        s.ShowBossPanel = ShowBoss.IsChecked == true;
        s.ShowGear = ShowGear.IsChecked == true;
        s.ShowHealingPanel = ShowHealingPanel.IsChecked == true;
        s.ShowPeakDps = ShowPeakDps.IsChecked == true;
        s.CompactDpsOnly = CompactView.IsChecked == true;
        s.AutoShow = AutoShow.IsChecked == true;
        if (int.TryParse(AutoHideSeconds.Text, out var autoHide)) s.AutoHideSeconds = Math.Clamp(autoHide, 0, 3600);
        s.ShowOnStart = ShowOnStart.IsChecked == true;
        if (int.TryParse(BossAlertMinutesBox.Text, out var alertMinutes)) s.BossAlertMinutes = Math.Clamp(alertMinutes, 0, 60);
        s.EventTimersOpacity = Math.Clamp(EventTimerOpacity.Value, 0.2, 1);
        s.DiscordPresenceEnabled = DiscordPresence.IsChecked == true;
        s.DiscordFaction = (DiscordFactionBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        s.MaxRows = MaxRows.SelectedItem is int rows ? rows : 10;
        s.BackgroundOpacity = Math.Round(OpacityValue.Value, 2);
        s.RowSize = (int)Math.Round(RowSizeValue.Value);
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

        var language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";
        s.DownloadIcons = Icons.IsChecked == true;
        s.CheckUpdates = CheckUpdates.IsChecked == true;
        s.AutoInstallUpdates = AutoInstallUpdates.IsChecked == true;
        s.HotkeyToggleOverlay = HkToggle.Text.Trim();
        s.HotkeyReset = HkReset.Text.Trim();
        s.HotkeyClickThrough = HkClick.Text.Trim();
        s.HotkeyTimers = HkTimers.Text.Trim();

        _meter.ApplySettings();
        if (captureChanged) _meter.RestartCapture();
        Close();
        AppHost.Current.OnSettingsChanged();
        if (language != UiText.Normalize(s.Language)) AppHost.Current.SetLanguage(language);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>The overlay follows the slider at once, so the size can be judged on the real rows.</summary>
    private void RowSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RowSizeText is null) return; // the slider sets its first value before the rest of the window exists
        var percent = (int)Math.Round(e.NewValue);
        RowSizeText.Text = $"{percent}%";
        AppHost.Current.PreviewRowSize(percent);
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
