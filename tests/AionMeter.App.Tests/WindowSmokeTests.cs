using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.App.ViewModels;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Events;
using AionMeter.Core.Combat;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AionMeter.App.Tests;

public static class TestEnvironment
{
    [ModuleInitializer]
    public static void Initialize() => Environment.SetEnvironmentVariable("AIONMETER_APPDATA",
        Path.Combine(Path.GetTempPath(), "Aetherion-tests-" + Environment.ProcessId));
}

public sealed class WindowSmokeTests
{
    [Fact]
    public void Automatic_height_keeps_header_rows_and_footer_and_preserves_user_ceiling()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                var app = new SmokeApp();
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/AION2DpsMeter;component/Theme/Theme.xaml") });
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var settings = new AppSettings { Transient = true, SaveHistory = false, DownloadIcons = false,
                    OverlayHeight = 500, AutoOverlayHeight = true };
                using var meter = new MeterService(settings, null);
                meter.Tracker.Process(new SelfIdentifiedEvent(0, 100, "Test", 1304, GameClass.Cleric));
                meter.Tracker.Process(new NpcSeenEvent(0, 500, 2300243, 100_000));
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                meter.Tracker.Process(new DamageEvent(now - 1_000, 100, 500, 17010000, 1_000, HitFlags.None));
                var overlay = new OverlayWindow(meter) { Opacity = 0, ShowActivated = false };
                try
                {
                    overlay.Show(); overlay.Refresh(); overlay.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    overlay.Refresh(); overlay.UpdateLayout();
                    // The compact overlay intentionally fits the footer into a shorter card.
                    Assert.InRange(overlay.Height, 160, 499);
                    Assert.Equal(500, settings.OverlayHeight);
                    var footer = Assert.IsType<System.Windows.Controls.StackPanel>(overlay.FindName("FooterStatus"));
                    Assert.True(footer.IsVisible);
                    Assert.True(footer.TranslatePoint(new Point(0, footer.ActualHeight), overlay).Y <= overlay.ActualHeight);
                    CheckOverlayTypography(overlay, settings);
                    foreach (var language in new[] { "de", "en", "ru" })
                    {
                        UiText.Use(language);
                        var share = Assert.Single(ChatCopy.Items(overlay, () => meter.Tracker.Snapshot(null, now)));
                        Assert.Contains("Discord", share.Header.ToString());
                        Assert.Contains("Discord", UiText.Current.TipChat);
                        Assert.Contains("Discord", UiText.Current.Copied);
                    }
                    using var output = new TemporaryOutput();
                    CheckJournalNavigation(app, meter, overlay);
                    CheckSizePresets(meter, overlay);
                    CheckOverlayAppearancePersistence();
                    CheckUnavailableRecovery(meter);
                    SampleRenderer.Render(output.Path, "de", new SampleRenderer.Options(Window: "customer-tools", Backdrop: "dark", Scale: 1));
                    Assert.True(new FileInfo(output.Path).Length > 1_000);
                    SampleRenderer.Render(output.Path, "de", new SampleRenderer.Options(Window: "maintenance", Scale: 1));
                    Assert.True(new FileInfo(output.Path).Length > 1_000);
                    JournalUiChecks.Run(meter);
                    SampleRenderer.Render(output.Path, "de", new SampleRenderer.Options(Window: "fight-journal", Tab: "healing", Scale: 1));
                    Assert.True(new FileInfo(output.Path).Length > 1_000);
                    SampleRenderer.Render(output.Path, "de", new SampleRenderer.Options(Window: "discord-card", Scale: 1));
                    Assert.True(new FileInfo(output.Path).Length > 1_000);
                }
                finally { overlay.Close(); app.Shutdown(); }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "WPF test did not finish.");
        Assert.Null(failure);
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private static double TextWidth(TextBlock text, string? content = null) => new FormattedText(content ?? text.Text,
        System.Globalization.CultureInfo.GetCultureInfo("en-US"), text.FlowDirection,
        new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
        text.Foreground, null, TextFormattingMode.Display, VisualTreeHelper.GetDpi(text).PixelsPerDip).WidthIncludingTrailingWhitespace;

    private static void CheckOverlayTypography(OverlayWindow overlay, AppSettings settings)
    {
        var original = (overlay.Width, overlay.Height, settings.RowSize);
        var vm = (OverlayViewModel)overlay.DataContext;
        var row = vm.Rows[0];
        var oldValues = (row.Name, row.Damage, row.Share, overlay.DisplayNumbers);
        const string longName = "EinExtremLangerSpielernameDerNiemalsInDieDpsSpalteRagenDarf";
        var snapshot = overlay.DisplayNumbers!.Snapshot!;
        var actor = snapshot.Combatants.First(c => c.ActorId == row.ActorId);
        row.Name = longName; row.Damage = "119.4K"; row.Share = "100.0%";
        var exactSnapshot = snapshot with { Combatants = new[] { actor with { Dps = 9_999_999, Damage = 9_999_999 } } };
        overlay.SetValue(OverlayWindow.DisplayNumbersProperty, new OverlayNumberContext(exactSnapshot, false));
        try
        {
            foreach (var (rowSize, expectedHeight, expectedGap) in new[] { (58, 38d, 2d), (80, 44d, 4d), (90, 52d, 6d) })
            {
                settings.RowSize = rowSize;
                overlay.ApplyRowSize(rowSize);
                double? observedFont = null;
                double? observedNumberWidth = null;
                foreach (var width in new[] { 300, 380, 460, 600 })
                {
                    overlay.PreviewSize(width, 500); overlay.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    overlay.UpdateLayout();
                    var name = Visuals<TextBlock>(overlay).Single(t => t.Name == "NameText");
                    var dps = Visuals<TextBlock>(overlay).Single(t => t.Name == "DpsText");
                    var damage = Visuals<TextBlock>(overlay).Single(t => t.Name == "DamageText");
                    var share = Visuals<TextBlock>(overlay).Single(t => t.Name == "ShareText");
                    Assert.True(name.FontSize >= 13);
                    if (observedFont is { } font) Assert.Equal(font, name.FontSize);
                    observedFont = name.FontSize;
                    var rowGrid = Visuals<Grid>(overlay).Single(g => ReferenceEquals(g.DataContext, row) && g.Cursor == System.Windows.Input.Cursors.Hand);
                    Assert.Equal(expectedHeight, rowGrid.ActualHeight);
                    Assert.Equal(expectedGap, rowGrid.Margin.Bottom);
                    if (observedNumberWidth is { } columnWidth) Assert.Equal(columnWidth, dps.ActualWidth);
                    observedNumberWidth = dps.ActualWidth;
                    Assert.True(((Grid)overlay.FindName("CardBody")).LayoutTransform.Value.IsIdentity);
                    Assert.Equal(300, overlay.MinWidth);
                    Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
                    Assert.Equal(TextWrapping.NoWrap, name.TextWrapping);
                    Assert.Equal("9,999,999/S", dps.Text);
                    Assert.Equal("9,999,999", damage.Text);
                    Assert.Equal(dps.FontSize, share.FontSize);
                    Assert.Equal(Color.FromRgb(0x75, 0xE5, 0xA1), ((SolidColorBrush)share.Foreground).Color);
                    if (width == 300)
                    {
                        Assert.True(TextWidth(name) > name.ActualWidth, "Long name must be constrained and ellipsized.");
                        Assert.True(name.ActualWidth >= TextWidth(name, "…"), "The ellipsis itself must fit.");
                        Assert.True(name.TranslatePoint(new Point(name.ActualWidth, 0), overlay).X <= dps.TranslatePoint(new Point(), overlay).X);
                    }
                    foreach (var text in new[] { dps, damage, share })
                    {
                        Assert.True(text.ActualWidth + 1 >= TextWidth(text), $"Clipped {text.Name}: {text.ActualWidth} < {TextWidth(text)} at {width}, profile {rowSize}");
                        Assert.True(text.TranslatePoint(new Point(text.ActualWidth, 0), overlay).X <= overlay.ActualWidth - 8);
                    }
                    var title = (TextBlock)overlay.FindName("StatisticsTitle");
                    var timer = (TextBlock)overlay.FindName("StatisticsClock");
                    Assert.Equal(title.FontSize, timer.FontSize);
                    var actions = (StackPanel)overlay.FindName("CompactTopActions");
                    Assert.True(timer.TranslatePoint(new Point(timer.ActualWidth, 0), overlay).X <= actions.TranslatePoint(new Point(), overlay).X,
                        $"Header overlap at {width}, profile {rowSize}: timer ends {timer.TranslatePoint(new Point(timer.ActualWidth, 0), overlay).X}, actions start {actions.TranslatePoint(new Point(), overlay).X}");
                    foreach (var button in actions.Children.OfType<Button>())
                    {
                        Assert.True(button.ActualWidth >= 23.99); Assert.True(button.ActualHeight >= 23.99);
                    }
                }
            }
            var converter = new OverlayExactNumberConverter();
            var example = new OverlayNumberContext(snapshot with { Combatants = new[] { actor with { Dps = 22_049, Damage = 1_345_034 } } }, false);
            Assert.Equal("22,049/S", converter.Convert(new object[] { actor.ActorId, example }, typeof(string), "rate", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("1,345,034", converter.Convert(new object[] { actor.ActorId, example }, typeof(string), "total", System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            row.Name = oldValues.Name; row.Damage = oldValues.Damage; row.Share = oldValues.Share;
            overlay.SetValue(OverlayWindow.DisplayNumbersProperty, oldValues.DisplayNumbers);
            settings.RowSize = original.RowSize; overlay.ApplyRowSize(original.RowSize);
            overlay.PreviewSize(original.Width, original.Height);
        }
    }

    private static void CheckSizePresets(MeterService meter, OverlayWindow overlay)
    {
        var window = new SettingsWindow(meter) { Opacity = 0, ShowActivated = false };
        var originalSize = (overlay.Width, overlay.Height);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            window.Show(); window.UpdateLayout();
            var handler = typeof(SettingsWindow).GetMethod("SizePreset_Click", flags)!;
            foreach (var (preset, rows) in new[] { ("compact", 5), ("standard", 8), ("large", 12) })
            {
                handler.Invoke(window, new object[] { new Button { Tag = preset }, new RoutedEventArgs() });
                Assert.Equal(rows, ((ComboBox)window.FindName("MaxRows")).SelectedItem);
                Assert.Equal(originalSize.Width, overlay.Width); Assert.Equal(originalSize.Height, overlay.Height);
            }
        }
        finally { window.Close(); }
    }

    private static void CheckOverlayAppearancePersistence()
    {
        foreach (var existing in Application.Current.Windows.OfType<OverlayWindow>()) existing.FlushPendingAppearanceSave();
        var previousDirectory = AppSettings.AppDataDir;
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var temporaryDirectory = Path.Combine(temporaryRoot, "Aetherion-overlay-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        AppSettings.AppDataDir = temporaryDirectory;
        try
        {
            var file = Path.Combine(temporaryDirectory, "settings.json");
            var seed = new AppSettings { LayoutVersion = AppSettings.Load().LayoutVersion, OverlayWidth = 240, BackgroundOpacity = 0 };
            seed.Save();
            var loaded = AppSettings.Load();
            using var meter = new MeterService(loaded, null);
            var window = new OverlayWindow(meter) { Opacity = 0, ShowActivated = false };
            try
            {
                Assert.Equal(300, window.Width); Assert.Equal(300, loaded.OverlayWidth);
                window.FlushPendingAppearanceSave();
                Assert.Equal(300, AppSettings.Load().OverlayWidth);
                Assert.Equal(0.60, ((SolidColorBrush)window.FindName("FrameBackground")).Opacity);
                var slider = (Slider)window.FindName("OpacitySlider");
                Assert.Equal(0.20, slider.Minimum);
                var beforeDrag = File.ReadAllText(file);
                slider.Value = 0.30; slider.Value = 0.32; slider.Value = 0.35;
                Assert.Equal(0.35, ((SolidColorBrush)window.FindName("FrameBackground")).Opacity);
                Assert.Equal(beforeDrag, File.ReadAllText(file)); // Moving the slider does not write synchronously.
                window.FlushPendingAppearanceSave();
                Assert.Equal(0.35, AppSettings.Load().BackgroundOpacity);

                loaded.HighContrast = true;
                window.ApplyAppearance();
                Assert.Equal(1, ((SolidColorBrush)window.FindName("FrameBackground")).Opacity);
                Assert.Equal(0.35, loaded.BackgroundOpacity); // Contrast does not overwrite the user's slider value.
                loaded.HighContrast = false;
                window.ApplyAppearance();
                Assert.Equal(0.35, ((SolidColorBrush)window.FindName("FrameBackground")).Opacity);

                using var restartedMeter = new MeterService(AppSettings.Load(), null);
                var restarted = new OverlayWindow(restartedMeter) { Opacity = 0, ShowActivated = false };
                try { Assert.Equal(0.35, ((SolidColorBrush)restarted.FindName("FrameBackground")).Opacity); }
                finally { restarted.Close(); }
                slider.Value = 0.01;
                Assert.Equal(0.20, slider.Value);
                window.FlushPendingAppearanceSave();
                Assert.Equal(0.20, AppSettings.Load().BackgroundOpacity);
                Assert.Equal(0, window.Opacity); // Background changes never alter the window/text opacity.
                slider.Value = 0.45; // Closing must also commit a pending value without waiting for the timer.
            }
            finally { window.Close(); }
            Assert.Equal(0.45, AppSettings.Load().BackgroundOpacity);
        }
        finally
        {
            AppSettings.AppDataDir = previousDirectory;
            var allowedRoot = Path.TrimEndingDirectorySeparator(temporaryRoot) + Path.DirectorySeparatorChar;
            Assert.StartsWith(allowedRoot, Path.GetFullPath(temporaryDirectory), StringComparison.OrdinalIgnoreCase);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Healing_numbers_keep_the_original_duration_floor_and_handle_zero_and_missing_data()
    {
        var converter = new OverlayExactNumberConverter();
        var actor = new CombatantSnapshot(100, "Healer", GameClass.Cleric, true,
            1_345_034, 9_999_999, 1, 1, 0, 0, 0, Healing: 44_098);
        var snapshot = new EncounterSnapshot(Guid.NewGuid(), "Test", null, DateTimeOffset.UnixEpoch,
            0, 0, true, EncounterEndReason.None, 1_345_034, 9_999_999, null, new[] { actor });
        string Display(EncounterSnapshot? value, string field) => Assert.IsType<string>(converter.Convert(
            new object[] { actor.ActorId, new OverlayNumberContext(value, true) }, typeof(string), field,
            System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (milliseconds, expected) in new[]
        {
            (0L, "44,098/S"), (500L, "44,098/S"), (1000L, "44,098/S"),
            (2000L, "22,049/S"), (2500L, "17,639/S")
        })
        {
            var timed = snapshot with { CombatMs = milliseconds };
            Assert.Equal(expected, Display(timed, "rate"));
            Assert.Equal("44,098", Display(timed, "total"));
        }
        var zero = snapshot with { Combatants = new[] { actor with { Healing = 0 } } };
        Assert.Equal("0/S", Display(zero, "rate")); Assert.Equal("0", Display(zero, "total"));
        var rounded = snapshot with { CombatMs = 2000, Combatants = new[] { actor with { Healing = 3 } } };
        Assert.Equal("2/S", Display(rounded, "rate")); // Full-integer N0 rounding, intentionally no K/M abbreviations.
        Assert.Equal("—", Display(null, "rate"));
        Assert.Equal("—", Display(snapshot with { Combatants = Array.Empty<CombatantSnapshot>() }, "total"));
    }

    // Pumping the dispatcher must not start real login, maintenance HTTP polling or the single-instance path.
    private static void CheckJournalNavigation(Application app, MeterService meter, OverlayWindow overlay)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var appType = typeof(global::AionMeter.App.App);
        appType.GetField("_meter", flags)!.SetValue(app, meter);
        appType.GetField("_overlay", flags)!.SetValue(app, overlay);
        foreach (var index in Enumerable.Range(0, 6))
        {
        var settings = new SettingsWindow(meter) { ShowActivated = false, Opacity = 0 };
        try
        {
            settings.Show(); settings.UpdateLayout();
            var nav = Assert.IsType<System.Windows.Controls.Button>(settings.FindName("NavJournalButton"));
            nav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var section = Assert.IsType<System.Windows.Controls.Border>(settings.FindName("JournalSection"));
            Assert.Equal(Visibility.Visible, section.Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)settings.FindName("OverlaySection")).Visibility);
            var panel = Assert.IsType<System.Windows.Controls.StackPanel>(section.Child);
            var buttons = panel.Children.OfType<System.Windows.Controls.Button>().ToArray();
            Assert.Equal(6, buttons.Length);
            buttons[index].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var journal = Assert.Single(app.Windows.OfType<FightJournalWindow>());
            try
            {
                Assert.True(journal.IsVisible);
                var tabs = (System.Windows.Controls.TabControl)typeof(FightJournalWindow).GetField("_tabs", flags)!.GetValue(journal)!;
                Assert.Equal(new[] { 0, 2, 1, 0, 0, 3 }[index], tabs.SelectedIndex);
                var back = ((System.Windows.Controls.DockPanel)journal.Content).Children
                    .OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, "← Zurück zu den Einstellungen"));
                System.ComponentModel.CancelEventHandler cancel = (_, e) => e.Cancel = true;
                journal.Closing += cancel;
                back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Assert.True(journal.IsVisible);
                Assert.Empty(app.Windows.OfType<SettingsWindow>());
                journal.Closing -= cancel;
                back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Assert.False(journal.IsVisible);
                var returned = Assert.Single(app.Windows.OfType<SettingsWindow>());
                Assert.True(returned.IsVisible);
                returned.Close();
            }
            finally { journal.Close(); }
        }
        finally { settings.Close(); }
        }
    }

    private static void CheckUnavailableRecovery(MeterService meter)
    {
        var root = Path.Combine(AppSettings.AppDataDir, "recovery");
        Directory.CreateDirectory(root);
        var manifest = Path.Combine(root, "previous.json");
        var backup = Path.Combine(root, "test-backup.exe");
        File.WriteAllText(backup, "test checkpoint");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(backup)));
        try
        {
            foreach (var data in new[]
            {
                "{invalid json",
                System.Text.Json.JsonSerializer.Serialize(new { Exe = backup,
                    Target = Path.Combine(root, "another-installation.exe"), Sha256 = hash, Version = "0.3.51" })
            })
            {
                File.WriteAllText(manifest, data);
                Assert.False(UpdateRecovery.Available);
                Assert.Contains("nicht verfügbar", UpdateRecovery.Description());
                var window = new CustomerToolsWindow(meter) { ShowActivated = false, Opacity = 0 };
                try { window.Show(); window.UpdateLayout(); Assert.True(window.IsVisible); }
                finally { window.Close(); }
                var settings = new SettingsWindow(meter) { ShowActivated = false, Opacity = 0 };
                try
                {
                    settings.Show(); settings.UpdateLayout();
                    var button = Assert.IsType<System.Windows.Controls.Button>(settings.FindName("CustomerToolsButton"));
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    var opened = Assert.Single(Application.Current.Windows.OfType<CustomerToolsWindow>());
                    try
                    {
                        Assert.True(opened.IsVisible);
                        var back = ((System.Windows.Controls.DockPanel)opened.Content).Children
                            .OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, "← Zurück zu den Einstellungen"));
                        back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        Assert.False(opened.IsVisible);
                        var returned = Assert.Single(Application.Current.Windows.OfType<SettingsWindow>());
                        Assert.True(returned.IsVisible); returned.Close();
                    }
                    finally { opened.Close(); }
                }
                finally { settings.Close(); }
                Assert.ThrowsAny<Exception>(() => UpdateRecovery.Restore());
                Assert.Equal(data, File.ReadAllText(manifest));
            }
        }
        finally { File.Delete(manifest); File.Delete(backup); }
    }

    private sealed class SmokeApp : global::AionMeter.App.App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private sealed class TemporaryOutput : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetherion-smoke-" + Guid.NewGuid().ToString("N") + ".png");
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }
}
