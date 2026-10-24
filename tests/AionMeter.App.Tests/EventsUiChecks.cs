using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AionMeter.App.Controls;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Protocol;

namespace AionMeter.App.Tests;

internal static class EventsUiChecks
{
    public static void Run(MeterService meter)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var decoder = new ConfiguredEventDecoder(new([new("HiddenCubeOpened", true, "ABCD", [new("id", 0, "u8")])]));
        meter.Trackers.Publish(Assert.Single(decoder.Decode(0xABCD, [42], now, 1110, "Testgebiet")));
        var window = new SettingsWindow(meter) { ShowActivated = false, Opacity = 0 };
        try
        {
            window.Show();
            ((Button)window.FindName("NavEventsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("EventsSection")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("OverlaySection")).Visibility);
            var page = Assert.IsType<EventsPage>(((ContentControl)window.FindName("EventsHost")).Content);
            var grid = Assert.Single(Visuals<DataGrid>(page));
            Assert.Equal(new[] { "Zeit", "Typ", "Gebiet", "Details" }, grid.Columns.Select(c => c.Header));
            Assert.Contains(grid.Items.Cast<EventsPage.Row>(), r => r.Type == "HiddenCubeOpened" && r.Zone == "Testgebiet" && r.Details.Contains("42"));
            var search = Assert.Single(Visuals<TextBox>(page), t => t.ToolTip is string);
            search.Text = "NICHT_VORHANDEN"; Assert.Empty(grid.Items.Cast<object>());
            search.Text = "HiddenCubeOpened"; Assert.Single(grid.Items.Cast<object>());
            var typeFilter = Assert.Single(Visuals<ComboBox>(page)); typeFilter.SelectedItem = "HiddenCubeOpened";
            Assert.Single(grid.Items.Cast<object>());
            Assert.Equal(meter.Settings.HotkeyRecordingMarker, ((TextBox)window.FindName("HkMarker")).Text);
            // Navigation must unload the page's own timer without touching the overlay timer.
            ((Button)window.FindName("NavOverlayButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout(); Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("EventsSection")).Visibility);
        }
        finally { window.Close(); }
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
