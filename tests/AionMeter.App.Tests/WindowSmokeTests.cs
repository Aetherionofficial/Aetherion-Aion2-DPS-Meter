using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Events;

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
                var app = new global::AionMeter.App.App();
                app.InitializeComponent();
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
                    SampleRenderer.Render(output.Path, "de", new SampleRenderer.Options(Window: "customer-tools", Backdrop: "dark", Scale: 1));
                    Assert.True(new FileInfo(output.Path).Length > 1_000);
                }
                finally { overlay.Close(); app.Shutdown(); }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF test did not finish.");
        Assert.Null(failure);
    }

    private sealed class TemporaryOutput : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetherion-smoke-" + Guid.NewGuid().ToString("N") + ".png");
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }
}
