using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

/// <summary>Short, opaque, non-activating hint; never takes focus from the game.</summary>
public sealed class EventToastWindow : Window
{
    private static EventToastWindow? _current;
    private readonly DispatcherTimer _expiry = new() { Interval = TimeSpan.FromSeconds(4) };
    private EventToastWindow(string title, string message)
    {
        Width = 370; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize; ShowActivated = false; ShowInTaskbar = false; Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(20, 23, 30));
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.Turquoise, FontSize = 15, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = message, Foreground = Brushes.WhiteSmoke, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        Content = panel;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetExStyle(handle, NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
            if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
                target.RenderMode = RenderMode.SoftwareOnly;
        };
        Loaded += (_, _) => { Left = SystemParameters.WorkArea.Right - ActualWidth - 18; Top = SystemParameters.WorkArea.Bottom - ActualHeight - 18; _expiry.Start(); };
        _expiry.Tick += (_, _) => Close();
        Closed += (_, _) => { _expiry.Stop(); if (_current == this) _current = null; };
    }
    public static void ShowMessage(string title, string message)
    {
        _current?.Close(); _current = new(title, message); _current.Show();
    }
    public static void Dismiss() => _current?.Close();
}
