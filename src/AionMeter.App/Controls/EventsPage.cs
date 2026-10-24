using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using AionMeter.App.Services;
using AionMeter.App.Windows;
using AionMeter.Core.Tracking;

namespace AionMeter.App.Controls;

/// <summary>All UI work stays on the dispatcher; capture workers only supply immutable snapshots.</summary>
public sealed class EventsPage : UserControl
{
    private readonly MeterService _meter;
    private readonly Func<string> _label;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _counts = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly ComboBox _types = new() { MinWidth = 155, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBox _search = new() { MinWidth = 160, ToolTip = "In Typ, Gebiet und Details suchen" };
    private readonly DataGrid _grid = new() { Height = 320, IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false,
        HeadersVisibility = DataGridHeadersVisibility.Column, EnableRowVirtualization = true, RowHeaderWidth = 0 };
    private readonly Button _record = new();
    private long _version = -1;
    private bool _refreshing;
    public sealed record Row(string Time, string Type, string Zone, string Details);

    public EventsPage(MeterService meter, Func<string> label, Action<bool>? recordingChanged = null)
    {
        _meter = meter; _label = label;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/AION2DpsMeter;component/Controls/EventsPageTheme.xaml", UriKind.Relative) });
        _types.Style = (Style)FindResource("EventTypeFilter");
        _search.Width = double.NaN; _search.HorizontalAlignment = HorizontalAlignment.Stretch;
        var panel = new StackPanel(); Content = panel;
        _grid.Background = (Brush)FindResource("Bg0"); _grid.Foreground = (Brush)FindResource("Text");
        _grid.RowBackground = (Brush)FindResource("Bg1"); _grid.AlternatingRowBackground = (Brush)FindResource("Bg2");
        _grid.BorderBrush = (Brush)FindResource("Line"); _grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        _grid.HorizontalGridLinesBrush = (Brush)FindResource("Line");
        var headers = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        headers.Setters.Add(new Setter(BackgroundProperty, FindResource("Bg2")));
        headers.Setters.Add(new Setter(ForegroundProperty, FindResource("Text")));
        headers.Setters.Add(new Setter(PaddingProperty, new Thickness(8)));
        _grid.ColumnHeaderStyle = headers;
        var actions = new WrapPanel(); panel.Children.Add(actions);
        AddButton(actions, _record, "Aufnahme starten", async () =>
        {
            _record.IsEnabled = false;
            try
            {
                _meter.Settings.RecordPackets = !_meter.Settings.RecordPackets;
                _meter.Settings.Save();
                recordingChanged?.Invoke(_meter.Settings.RecordPackets);
                await Task.Run(_meter.RestartCapture);
            }
            finally { _record.IsEnabled = true; RefreshStatus(); }
        });
        AddButton(actions, new Button(), "Marker setzen", async () =>
        {
            var marker = await _meter.AddRecordingMarkerAsync(_label());
            EventToastWindow.ShowMessage(marker is null ? "Keine aktive Aufnahme" : "Marker gesetzt",
                marker is null ? "Starte eine Aufnahme und warte auf Spielverkehr." : $"{marker.Label} · {DateTimeOffset.FromUnixTimeMilliseconds(marker.TimeMs).ToLocalTime():HH:mm:ss.fff}");
        });
        AddButton(actions, new Button(), "events.json öffnen", () =>
        {
            Process.Start(new ProcessStartInfo(_meter.EventConfigPath) { UseShellExecute = true }); return Task.CompletedTask;
        });
        AddButton(actions, new Button(), "Regeln neu laden", () => { _meter.ReloadEventConfiguration(); RefreshStatus(); return Task.CompletedTask; });
        panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "Nur bestätigte Felder aktivieren. Ein beobachteter Monster-Tod belegt weder einen eigenen Kill noch Beute.",
            TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextDim"), Margin = new Thickness(0, 0, 0, 12) });
        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        filters.Children.Add(_types);
        filters.Children.Add(new TextBlock { Text = "Suche:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) });
        filters.Children.Add(_search); panel.Children.Add(filters);
        foreach (var column in new[] { ("Zeit", "Time", 84d), ("Typ", "Type", 155d), ("Gebiet", "Zone", 130d), ("Details", "Details", 350d) })
            _grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = column.Item3 });
        panel.Children.Add(_grid); panel.Children.Add(_counts);
        _types.SelectionChanged += (_, _) => { if (!_refreshing) Refresh(true); };
        _search.TextChanged += (_, _) => Refresh(true);
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(true); if (IsVisible) _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => { if (IsVisible && IsLoaded) { Refresh(true); _timer.Start(); } else _timer.Stop(); };
        Refresh(true);
    }
    private void AddButton(Panel parent, Button button, string text, Func<Task> action)
    {
        button.Content = text; button.Style = (Style)FindResource("GhostButton"); button.Margin = new Thickness(0, 0, 8, 8);
        button.Click += async (_, _) =>
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
            { EventToastWindow.ShowMessage("Ereignisse", error.Message); }
        };
        parent.Children.Add(button);
    }
    private void RefreshStatus()
    {
        _record.Content = _meter.Settings.RecordPackets ? "Aufnahme stoppen" : "Aufnahme starten";
        _status.Text = (_meter.ActiveRecordingPath is { } path ? $"Aufnahme: {path}" : _meter.Settings.RecordPackets ? "Aufnahme bereit – warte auf Spielverkehr." : "Keine Aufnahme aktiv.")
            + $"\nRegeln: {_meter.EventConfigPath}" + (_meter.EventConfigError is { } error ? $"\n{error}" : "")
            + (_meter.EventStorageError is { } storageError ? $"\n{storageError}" : "");
    }
    private void Refresh(bool force = false)
    {
        RefreshStatus();
        var snapshot = _meter.EventLog.GetSnapshot();
        _counts.Text = "Zähler: " + string.Join(" · ", _meter.EventCounters.GetSnapshot().Counts.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value:N0}"));
        if (!force && snapshot.Version == _version) return;
        _version = snapshot.Version;
        var selected = _types.SelectedItem as string ?? "Alle Typen";
        _refreshing = true;
        try
        {
            _types.ItemsSource = new[] { "Alle Typen" }.Concat(snapshot.Events.Select(e => e.Type).Distinct().Order()).ToArray();
            _types.SelectedItem = _types.Items.Contains(selected) ? selected : "Alle Typen";
        }
        finally { _refreshing = false; }
        selected = _types.SelectedItem as string ?? "Alle Typen";
        var search = _search.Text.Trim();
        _grid.ItemsSource = snapshot.Events.Reverse().Where(e => (selected == "Alle Typen" || e.Type == selected) &&
            (search.Length == 0 || $"{e.Type} {e.Zone} {e.Details}".Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Select(e => new Row(DateTimeOffset.FromUnixTimeMilliseconds(e.TimeMs).ToLocalTime().ToString("HH:mm:ss"), e.Type, e.Zone ?? "Unbekannt", e.Details)).ToArray();
    }
}
