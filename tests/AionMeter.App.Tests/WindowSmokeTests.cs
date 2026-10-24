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
                    CheckOverlayRendering(overlay);
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

    private static void CheckOverlayRendering(OverlayWindow overlay)
    {
        var source = Assert.IsType<System.Windows.Interop.HwndSource>(PresentationSource.FromVisual(overlay));
        Assert.Equal(System.Windows.Interop.RenderMode.SoftwareOnly, source.CompositionTarget.RenderMode);
        Assert.False(overlay.ShowActivated);
        Assert.True(overlay.Topmost);
        var sizeChanges = 0;
        SizeChangedEventHandler changed = (_, _) => sizeChanges++;
        overlay.SizeChanged += changed;
        try
        {
            var bounds = (overlay.Left, overlay.Top, overlay.ActualWidth, overlay.ActualHeight);
            for (var i = 0; i < 30; i++)
            {
                overlay.Refresh(); overlay.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            Assert.Equal(bounds, (overlay.Left, overlay.Top, overlay.ActualWidth, overlay.ActualHeight));
            Assert.Equal(0, sizeChanges);
            Assert.Equal(System.Windows.Interop.RenderMode.SoftwareOnly, source.CompositionTarget.RenderMode);
        }
        finally { overlay.SizeChanged -= changed; }
    }

    private static void CheckOverlayTypography(OverlayWindow overlay, AppSettings settings)
    {
        var original = (overlay.Width, overlay.Height, settings.OverlayTextScale);
        var vm = (OverlayViewModel)overlay.DataContext;
        Assert.Equal(6, vm.Rows.Count);
        Assert.Equal(5, vm.Rows.Count(r => r.IsPlaceholder));
        var row = vm.Rows.First(r => !r.IsPlaceholder);
        var oldName = row.Name;
        row.Name = "EinExtremLangerSpielernameDerNiemalsInDieDpsSpalteRagenDarf";
        try
        {
            foreach (var scale in new[] { .7, 1d, 1.5 })
            {
                overlay.ApplyScale(scale);
                double? observedFont = null;
                foreach (var width in new[] { overlay.MinWidth, 600d, 900d })
                {
                    overlay.PreviewSize(width, 600); overlay.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var name = Visuals<TextBlock>(overlay).Single(t => t.Name == "NameText" && ReferenceEquals(t.DataContext, row));
                    var dps = Visuals<TextBlock>(overlay).Single(t => t.Name == "DpsText" && ReferenceEquals(t.DataContext, row));
                    var share = Visuals<TextBlock>(overlay).Single(t => t.Name == "ShareText" && ReferenceEquals(t.DataContext, row));
                    Assert.Equal(13 * scale, name.FontSize, 3);
                    if (observedFont is { } font) Assert.Equal(font, name.FontSize);
                    observedFont = name.FontSize;
                    var rowGrid = Visuals<Grid>(overlay).Single(g => ReferenceEquals(g.DataContext, row) && g.Cursor == System.Windows.Input.Cursors.Hand);
                    Assert.InRange(rowGrid.ActualHeight, 24 * scale - 1, 24 * scale + 1);
                    Assert.Equal(2 * scale, rowGrid.Margin.Bottom, 3);
                    Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
                    Assert.Equal(TextWrapping.NoWrap, name.TextWrapping);
                    Assert.True(name.TranslatePoint(new Point(name.ActualWidth, 0), overlay).X <= dps.TranslatePoint(new Point(), overlay).X);
                    foreach (var text in new[] { dps, share }) Assert.True(text.ActualWidth + 1 >= TextWidth(text));
                    var timer = (TextBlock)overlay.FindName("StatisticsClock");
                    var actions = (StackPanel)overlay.FindName("CompactTopActions");
                    Assert.Equal(4, actions.Children.OfType<Button>().Count());
                    Assert.All(actions.Children.OfType<Button>(), b => Assert.NotNull(b.ToolTip));
                    Assert.True(timer.TranslatePoint(new Point(timer.ActualWidth, 0), overlay).X <= actions.TranslatePoint(new Point(), overlay).X, "Header overlap");
                    Assert.True(overlay.Width >= overlay.MinWidth);
                }
            }
            var details = (Button)overlay.FindName("HeaderDetailsButton");
            details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var window = Assert.Single(Application.Current.Windows.OfType<BreakdownWindow>());
            Assert.True(window.IsVisible);
            Assert.True(window.Topmost); // Must not be created underneath a borderless game's topmost surface.
            Assert.True(window.ShowActivated);
            Assert.False(overlay.ShowActivated); // Only the explicit detail window can take keyboard focus.
            Assert.Equal(Visibility.Visible, ((Border)window.FindName("OverlayCritTile")).Visibility);
            window.WindowState = WindowState.Minimized;
            details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(window, Assert.Single(Application.Current.Windows.OfType<BreakdownWindow>()));
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.IsVisible);
            window.Hide();
            details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(window, Assert.Single(Application.Current.Windows.OfType<BreakdownWindow>()));
            Assert.True(window.IsVisible);
            details.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(Application.Current.Windows.OfType<BreakdownWindow>());
            settings.ShowPing = false; overlay.ApplyAppearance();
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)overlay.FindName("PingStatus")).Visibility);
            settings.ShowPing = true; overlay.ApplyAppearance();
            Assert.Equal(Visibility.Visible, ((FrameworkElement)overlay.FindName("PingStatus")).Visibility);
            overlay.SetValue(OverlayWindow.LatencyProperty, new LatencyDisplay("42 ms", "#65D98A", "Testmessung"));
            overlay.UpdateLayout();
            var pingPanel = (StackPanel)overlay.FindName("PingStatus");
            Assert.Equal("42 ms", Assert.Single(pingPanel.Children.OfType<TextBlock>()).Text);
            Assert.Equal(Color.FromRgb(0x65, 0xD9, 0x8A),
                ((SolidColorBrush)Assert.Single(pingPanel.Children.OfType<System.Windows.Shapes.Ellipse>()).Fill).Color);
        }
        finally
        {
            row.Name = oldName; overlay.ApplyScale(original.OverlayTextScale);
            overlay.PreviewSize(original.Width, original.Height);
        }
    }

    private static void CheckSizePresets(MeterService meter, OverlayWindow overlay)
    {
        var window = new SettingsWindow(meter) { Opacity = 0, ShowActivated = false };
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            window.Show(); window.UpdateLayout();
            var handler = typeof(SettingsWindow).GetMethod("SizePreset_Click", flags)!;
            foreach (var (preset, rows) in new[] { ("compact", 6), ("standard", 6), ("large", 6) })
            {
                handler.Invoke(window, new object[] { new Button { Tag = preset }, new RoutedEventArgs() });
                Assert.Equal(rows, ((ComboBox)window.FindName("MaxRows")).SelectedItem);
                Assert.Equal(preset == "compact" ? 85d : preset == "large" ? 125d : 100d, ((Slider)window.FindName("RowSizeValue")).Value);
                Assert.True(overlay.Width >= overlay.MinWidth);
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
            (0L, "44,1k/S"), (500L, "44,1k/S"), (1000L, "44,1k/S"),
            (2000L, "22,05k/S"), (2500L, "17,64k/S")
        })
        {
            var timed = snapshot with { CombatMs = milliseconds };
            Assert.Equal(expected, Display(timed, "rate"));
            Assert.Equal("44,1k", Display(timed, "total"));
        }
        var zero = snapshot with { Combatants = new[] { actor with { Healing = 0 } } };
        Assert.Equal("0/S", Display(zero, "rate")); Assert.Equal("0", Display(zero, "total"));
        var rounded = snapshot with { CombatMs = 2000, Combatants = new[] { actor with { Healing = 3 } } };
        Assert.Equal("1,5/S", Display(rounded, "rate"));
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
