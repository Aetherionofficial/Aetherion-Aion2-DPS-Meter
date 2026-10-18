using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Events;

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
                    Assert.InRange(overlay.Height, 220, 499);
                    Assert.Equal(500, settings.OverlayHeight);
                    var footer = Assert.IsType<System.Windows.Controls.Button>(overlay.FindName("FooterDetailsButton"));
                    Assert.True(footer.IsVisible);
                    Assert.True(footer.TranslatePoint(new Point(0, footer.ActualHeight), overlay).Y <= overlay.ActualHeight);
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
