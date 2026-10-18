using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AionMeter.App.Windows;

public partial class BossAlertToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly Action? _onClick;

    public BossAlertToastWindow(string bossName, string countdown, Action? onClick = null)
    {
        InitializeComponent();
        BossName.Text = bossName;
        Countdown.Text = countdown;
        _onClick = onClick;
        _closeTimer.Tick += (_, _) => Close();
        _closeTimer.Start();
        MouseLeftButtonUp += Toast_Click;
        Loaded += (_, _) => PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 22;
        Top = workArea.Bottom - ActualHeight - 22;
    }

    private void Toast_Click(object sender, MouseButtonEventArgs e)
    {
        _closeTimer.Stop();
        _onClick?.Invoke();
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _closeTimer.Stop();
        Close();
    }
}
