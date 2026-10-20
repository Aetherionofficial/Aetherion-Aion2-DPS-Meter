using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AetherionLiveAlerts;

public sealed class AlertToastWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(6) };
    public AlertToastWindow(string title, string message)
    {
        Width = 430; Height = 132; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(25, 18, 17)), BorderBrush = new SolidColorBrush(Color.FromRgb(240, 106, 34)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(12), Padding = new Thickness(18) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "AETHERION LIVE", Foreground = new SolidColorBrush(Color.FromRgb(240, 106, 34)), FontWeight = FontWeights.Bold, FontSize = 13 });
        stack.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 18, Margin = new Thickness(0, 4, 0, 2) });
        stack.Children.Add(new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(226, 216, 211)), FontSize = 14, TextWrapping = TextWrapping.Wrap });
        border.Child = stack; Content = border;
        _timer.Tick += (_, _) => Close();
        Loaded += (_, _) => { Left = SystemParameters.WorkArea.Right - Width - 26; Top = SystemParameters.WorkArea.Bottom - Height - 26; _timer.Start(); Activate(); };
        Closed += (_, _) => _timer.Stop();
    }
}
