using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AionMeter.App.Services;

namespace AionMeter.App.Windows;

/// <summary>Local countdowns for recurring events; actual field-boss timers are read from the game separately.</summary>
public sealed class EventTimersWindow : Window
{
    private sealed record EventSpec(Func<UiText, string> Name, Func<DateTimeOffset, DateTimeOffset> Next);
    private sealed record EventRow(Border Card, TextBlock Name, TextBlock Countdown, TextBlock At);

    private static readonly EventSpec[] Schedule =
    [
        new(t => t.EventRift, n => NextUtcDaily(n, [0, 3, 6, 9, 12, 15, 18, 21], 0)),
        new(t => t.EventShugo, n => NextHourly(n, 0)),
        new(t => t.EventInvasion, n => NextHourly(n, 30)),
        new(t => t.EventWatcher, n => NextDaily(n, [1, 4, 7, 10, 13, 16, 19, 22], 0)),
        new(t => t.EventArena, n => NextDaily(n, [11, 19], 0)),
        new(t => t.EventSiege, n => NextWeekly(n, [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday], 21, 0)),
        new(t => t.EventExecutors, n => NextWeekly(n, [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday], 21, 30)),
        new(t => t.EventNahma, n => NextWeekly(n, [DayOfWeek.Friday, DayOfWeek.Sunday], 21, 0)),
    ];

    private readonly StackPanel _list = new();
    private readonly TextBlock _hint = new();
    private readonly TextBlock _title = new();
    private readonly List<EventRow> _rows = new();
    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings;
    private readonly Grid _details = new();
    private readonly Slider _opacitySlider = new();
    private bool _collapsed;

    public EventTimersWindow(AppSettings settings)
    {
        _settings = settings;
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Math.Clamp(settings.EventTimersWidth, 300, 650), workArea.Width - 16);
        Height = Math.Min(Math.Clamp(settings.EventTimersHeight, 280, 760), workArea.Height - 16);
        Left = Math.Clamp(settings.EventTimersLeft, workArea.Left + 8, workArea.Right - Width - 8);
        Top = Math.Clamp(settings.EventTimersTop, workArea.Top + 8, workArea.Bottom - Height - 8);
        MinWidth = 300;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Opacity = Math.Clamp(settings.EventTimersOpacity, 0.35, 1.0);
        DataContext = UiText.Current;

        var collapse = new Button
        {
            Content = "−", Width = 28, Height = 28, FontSize = 18,
            Background = Brushes.Transparent, Foreground = Brush("#B7B1AA"), BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = UiText.Current.EventCompact,
        };
        collapse.Click += (_, _) =>
        {
            _collapsed = !_collapsed;
            _details.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            collapse.Content = _collapsed ? "+" : "−";
            collapse.ToolTip = _collapsed ? UiText.Current.EventExpand : UiText.Current.EventCompact;
            MinHeight = _collapsed ? 58 : 280;
            Height = _collapsed ? 64 : Math.Clamp(_settings.EventTimersHeight, 280, 760);
        };
        var close = new Button
        {
            Content = "×", Width = 30, Height = 30, FontSize = 20,
            Background = Brushes.Transparent, Foreground = Brush("#B7B1AA"), BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
        };
        close.Click += (_, _) => Close();
        _title.Text = UiText.Current.EventOverlayTitle;
        _title.FontSize = 13.5;
        _title.FontWeight = FontWeights.Bold;
        _title.Foreground = Brush("#F4F0EB");
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        var kicker = new TextBlock { Text = UiText.Current.EventOverlayKicker, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brush("#807971"), Margin = new Thickness(0, 0, 0, 3) };
        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(kicker);
        titlePanel.Children.Add(_title);
        var opacityLabel = new TextBlock { Text = UiText.Current.EventOpacity, Foreground = Brush("#B7B1AA"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        _opacitySlider.Minimum = 0;
        _opacitySlider.Maximum = 1;
        _opacitySlider.Value = Opacity;
        _opacitySlider.Width = 64;
        _opacitySlider.VerticalAlignment = VerticalAlignment.Center;
        _opacitySlider.ToolTip = UiText.Current.EventOpacity;
        _opacitySlider.ValueChanged += (_, _) => Opacity = _opacitySlider.Value;
        var opacityBar = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 2, 0) };
        opacityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        opacityLabel.Margin = new Thickness(0, 0, 0, -2);
        opacityBar.Children.Add(opacityLabel);
        opacityBar.Children.Add(_opacitySlider);
        var header = new Grid { Margin = new Thickness(16, 9, 8, 8), MinHeight = 42 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titlePanel);
        Grid.SetColumn(opacityBar, 1);
        header.Children.Add(opacityBar);
        Grid.SetColumn(collapse, 2);
        header.Children.Add(collapse);
        Grid.SetColumn(close, 3);
        header.Children.Add(close);

        _hint.Text = UiText.Current.EventTimersHint;
        _hint.Text = UiText.Current.EventOverlayHint;
        _hint.FontSize = 10;
        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.Foreground = Brush("#B7B1AA");
        _hint.Margin = new Thickness(16, 7, 16, 8);

        foreach (var spec in Schedule)
        {
            var name = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brush("#F4F0EB"), TextTrimming = TextTrimming.CharacterEllipsis };
            var countdown = new TextBlock { FontSize = 15, FontFamily = new FontFamily("Consolas"), FontWeight = FontWeights.Bold, Foreground = Brush("#FF9B4A"), HorizontalAlignment = HorizontalAlignment.Right };
            var at = new TextBlock { FontSize = 9, Foreground = Brush("#807971") };
            var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            label.Children.Add(name);
            label.Children.Add(at);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new Ellipse { Width = 6, Height = 6, Fill = Brush("#FF741F"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            row.Children.Add(dot);
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            Grid.SetColumn(countdown, 2);
            countdown.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(countdown);
            var card = new Border
            {
                Background = Brush("#141618"), BorderBrush = Brush("#30271F"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 0, 5), Child = row,
            };
            _rows.Add(new EventRow(card, name, countdown, at));
            _list.Children.Add(card);
        }

        var source = new TextBlock
        {
            Text = UiText.Current.EventSource, FontSize = 9, TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#807971"), Margin = new Thickness(14, 2, 14, 8),
        };
        _details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _details.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_hint, 0);
        _details.Children.Add(_hint);
        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12, 0, 12, 0) };
        Grid.SetRow(scroll, 1);
        _details.Children.Add(scroll);
        Grid.SetRow(source, 2);
        _details.Children.Add(source);
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(new Border { BorderBrush = Brush("#30271F"), BorderThickness = new Thickness(0, 0, 0, 1), Child = header });
        Grid.SetRow(_details, 1);
        body.Children.Add(_details);
        Content = new Border
        {
            Background = Brush("#F20A0B0D"), BorderBrush = Brush("#30271F"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Child = body,
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshRows();
        Loaded += (_, _) => { RefreshRows(); _timer.Start(); };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _settings.EventTimersLeft = Left;
            _settings.EventTimersTop = Top;
            _settings.EventTimersWidth = Width;
            if (!_collapsed) _settings.EventTimersHeight = Height;
            _settings.EventTimersOpacity = Opacity;
            _settings.Save();
        };
    }

    private void RefreshRows()
    {
        var now = DateTimeOffset.Now;
        var t = UiText.Current;
        _title.Text = t.EventOverlayTitle;
        _hint.Text = t.EventOverlayHint;
        foreach (var row in _rows)
        {
            var spec = Schedule[_rows.IndexOf(row)];
            row.Name.Text = spec.Name(t);
            var next = spec.Next(now);
            var left = next - now;
            row.Countdown.Text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left:hh\\:mm}" : left.ToString(@"hh\:mm");
            row.At.Text = string.Format(t.EventAt, next.ToLocalTime().ToString("ddd HH:mm", t.Culture));
        }
        foreach (var row in _rows.OrderBy(r => Schedule[_rows.IndexOf(r)].Next(now))) _list.Children.Remove(row.Card);
        foreach (var row in _rows.OrderBy(r => Schedule[_rows.IndexOf(r)].Next(now))) _list.Children.Add(row.Card);
    }

    internal void ReloadNow() => RefreshRows();

    private static DateTimeOffset NextHourly(DateTimeOffset now, int minute)
    {
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, minute, 0, now.Offset);
        if (candidate <= now) candidate = candidate.AddHours(1);
        return candidate;
    }

    private static DateTimeOffset NextDaily(DateTimeOffset now, int[] hours, int minute)
    {
        for (var day = 0; day <= 1; day++)
            foreach (var hour in hours)
            {
                var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset).AddDays(day);
                if (candidate > now) return candidate;
            }
        return now.AddDays(1);
    }

    private static DateTimeOffset NextUtcDaily(DateTimeOffset now, int[] hours, int minute)
    {
        var utc = now.ToUniversalTime();
        for (var day = 0; day <= 1; day++)
            foreach (var hour in hours)
            {
                var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, hour, minute, 0, TimeSpan.Zero).AddDays(day);
                if (candidate > utc) return candidate;
            }
        return utc.AddDays(1);
    }

    private static DateTimeOffset NextWeekly(DateTimeOffset now, DayOfWeek[] days, int hour, int minute)
    {
        for (var offset = 0; offset < 8; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (!days.Contains(date.DayOfWeek)) continue;
            var candidate = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, now.Offset);
            if (candidate > now) return candidate;
        }
        return now.AddDays(7);
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}
