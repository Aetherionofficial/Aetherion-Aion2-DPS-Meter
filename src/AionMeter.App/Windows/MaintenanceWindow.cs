using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

public sealed class MaintenanceWindow : Window
{
    private readonly AppMaintenanceService _service;
    private readonly TextBlock _heading = new() { FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 16) };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0, 0, 0, 22) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), Foreground = Brushes.LightGray };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _checking;
    private bool _updateOpen;
    private readonly bool _duringSession;

    public MaintenanceWindow(AppMaintenanceService? service = null, bool duringSession = false)
    {
        _service = service ?? AppMaintenanceService.Shared;
        _duringSession = duringSession;
        Topmost = true;
        ShowActivated = true;
        Title = "Aetherion · Wartungsarbeiten";
        Width = 530; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(12, 15, 20)); Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "AETHERION", Foreground = Brushes.DarkOrange, FontWeight = FontWeights.Bold });
        panel.Children.Add(_heading); panel.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var retry = new Button { Content = "Erneut prüfen", Style = (Style)Application.Current.FindResource("PrimaryButton"), Padding = new Thickness(16, 9, 16, 9) };
        retry.Click += async (_, _) => await CheckAsync();
        var exit = new Button { Content = "App beenden", Style = (Style)Application.Current.FindResource("GhostButton"), Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(12, 0, 0, 0) };
        exit.Click += (_, _) => { DialogResult = false; };
        var update = new Button { Content = "Nach Updates suchen", Style = (Style)Application.Current.FindResource("GhostButton"), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            _updateOpen = true;
            _timer.Stop();
            try { await StartupUpdatePrompt.ShowAsync(this, manual: true); }
            finally { _updateOpen = false; if (IsVisible) { update.IsEnabled = true; _timer.Start(); } }
        };
        buttons.Children.Add(retry); buttons.Children.Add(exit); panel.Children.Add(buttons); panel.Children.Add(_status); panel.Children.Add(update);
        Content = panel;
        Render();
        _timer.Tick += async (_, _) => await CheckAsync();
        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void Render()
    {
        var maintenance = _service.Status is { Enabled: true };
        _heading.Text = maintenance ? "Wartungsarbeiten" : "Verbindung prüfen";
        _message.Text = maintenance ? _service.Status!.Message : "Der App-Status kann gerade nicht bestätigt werden. Bitte prüfe deine Internetverbindung.";
        _status.Text = maintenance && _duringSession
            ? "Die Wartungsarbeiten haben begonnen. Die App kann vorübergehend nicht genutzt werden. Sobald sie wieder freigegeben ist, startet sie automatisch neu. Prüfung alle 15 Sekunden."
            : "Die App kann während der Sperre nicht genutzt werden. Automatische Prüfung alle 15 Sekunden.";
    }

    private async Task CheckAsync()
    {
        if (_checking || _updateOpen) return;
        _checking = true;
        try
        {
            var success = await _service.RefreshAsync();
            if (!IsVisible || _updateOpen) return;
            if (success && _service.CanUseApp) { DialogResult = true; return; }
            Render();
            if (!success) _status.Text = "Statusprüfung fehlgeschlagen. Die Sperre bleibt bestehen; nächster Versuch in 15 Sekunden.";
        }
        finally { _checking = false; }
    }
}
