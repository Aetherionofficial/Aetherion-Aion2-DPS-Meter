using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AionMeter.App.Services;
using AionMeter.App.ViewModels;
using AionMeter.Core;
using AionMeter.Core.Capture;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;

namespace AionMeter.App.Windows;

public partial class RaidCommandWindow : Window
{
    private readonly MeterService _meter;
    private readonly Queue<double> _dpsSamples = new();
    private DateTime _lastDetailRefresh = DateTime.MinValue;
    private Guid? _fightId;
    private bool _healingMode;
    private IReadOnlyList<CombatantSnapshot> _playerRows = [];
    private long _playerTotal;
    private double _combatSeconds = 1;

    public ObservableCollection<RaidPlayerViewModel> Players { get; } = [];

    public RaidCommandWindow(MeterService meter)
    {
        _meter = meter;
        InitializeComponent();
        PrivacyNames.Changed += OnPrivacyChanged;
        Closed += (_, _) => PrivacyNames.Changed -= OnPrivacyChanged;
        PlayersList.ItemsSource = Players;
        UpdateModeButtons();
        Loaded += (_, _) => Refresh();
        SizeChanged += (_, _) => DrawChart();
        Closing += (_, e) =>
        {
            // The meter lives in the notification area; closing this dashboard hides it instead of killing capture.
            e.Cancel = true;
            Hide();
        };
    }

    private void OnPrivacyChanged() { if (Dispatcher.CheckAccess()) Refresh(); else Dispatcher.BeginInvoke(Refresh); }

    public void Refresh()
    {
        CaptureStateText.Text = _meter.DemoRunning ? "DEMO" : _meter.CaptureStatus.State switch
        {
            CaptureState.Capturing => "LIVE-ERFASSUNG",
            CaptureState.Error => "ERFASSUNGSFEHLER",
            CaptureState.Starting => "STARTET …",
            _ => "BEREIT",
        };

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var segments = _meter.Tracker.Segments();
        var liveId = _meter.Tracker.LiveFight()?.Id;
        var selected = liveId is { } id ? segments.FirstOrDefault(s => s.Id == id) : segments.FirstOrDefault();
        var snap = selected is null ? null : PrivacyNames.Apply(_meter.Tracker.Snapshot(selected.Id, now));
        if (snap is null)
        {
            ClearFight();
            return;
        }

        if (_fightId != snap.Id)
        {
            _fightId = snap.Id;
            _dpsSamples.Clear();
            _lastDetailRefresh = DateTime.MinValue;
        }
        _dpsSamples.Enqueue(Math.Max(0, snap.PartyDps));
        while (_dpsSamples.Count > 90) _dpsSamples.Dequeue();

        LocationText.Text = $"{(string.IsNullOrWhiteSpace(snap.Zone) ? "Zone nicht erkannt" : snap.Zone)} · {snap.PlayerCount} Spieler";
        BossNameText.Text = snap.Boss?.Name ?? snap.Title;
        BossMetaText.Text = snap.Boss is { } boss
            ? $"{(snap.Zone ?? "AION2")}{(boss.MaxHpKnown ? " · Boss-HP erkannt" : " · Maximal-HP noch unbekannt")} · Phase wird vom Spiel derzeit nicht eindeutig übertragen"
            : "Kein Boss-Lebenswert verfügbar · Kampf wird weiterhin lokal ausgewertet";
        BossHpBar.Value = snap.Boss?.HpFraction ?? 0;
        BossPercentText.Text = snap.Boss is { MaxHpKnown: true } b ? Format.Percent(b.HpFraction, 1) : "—";
        EncounterTimeText.Text = Format.ClockShort(snap.IsActive ? snap.ClockMs : snap.CombatMs);
        EncounterStatusText.Text = snap.IsActive ? "Kampf läuft · Live-Daten" : $"Letzter Kampf · {snap.Reason}";
        MechanicTitleText.Text = snap.Boss is null ? "Warte auf Bossdaten" : "Phase nicht verfügbar";
        MechanicDetailText.Text = snap.Boss is null
            ? "Sobald der Meter einen Boss erkennt, erscheint hier dessen Status."
            : "AION2 liefert aktuell keine verlässlichen Phasen- oder Mechanikereignisse für diesen Kampf.";

        var activeSeconds = Math.Max(1, snap.CombatMs / 1000.0);
        RateColumnText.Text = _healingMode ? "HPS" : "DPS";
        var healing = snap.Combatants.Sum(c => c.Healing);
        PartyDpsText.Text = Format.Compact(snap.PartyDps) + "/s";
        PartyHpsText.Text = Format.Compact(healing / activeSeconds) + "/s";
        PeakDpsText.Text = Format.Compact(snap.PeakPartyDps) + "/s";
        TotalDamageText.Text = $"Gesamtschaden {Format.Compact(snap.TotalDamage)}";
        DamageTakenText.Text = Format.Compact(snap.Combatants.Sum(c => c.DamageTaken));
        PlayerCountText.Text = $"{snap.PlayerCount} Spieler · {(snap.IsActive ? "Kampf aktiv" : "Kampf beendet")}";
        ChartRangeText.Text = snap.IsActive ? "LIVE · letzte 18 Sek." : "KAMPF BEENDET";

        _playerRows = snap.Combatants
            .Where(c => !c.IsUnknownSummons && (_healingMode ? c.Healing > 0 : c.Damage > 0))
            .ToList();
        _playerTotal = _healingMode ? healing : snap.TotalDamage;
        _combatSeconds = activeSeconds;
        RefreshPlayerRows();

        CaptureStateText.Text = snap.IsActive ? "KAMPF LÄUFT" : "KAMPF-ZUSAMMENFASSUNG";
        if (!snap.IsActive)
        {
            MechanicTitleText.Text = "Kampf abgeschlossen";
            MechanicDetailText.Text = $"{snap.PlayerCount} Spieler · {Format.Compact(snap.TotalDamage)} Gesamtschaden · {Format.ClockShort(snap.CombatMs)} Kampfzeit";
        }

        if (DateTime.UtcNow - _lastDetailRefresh >= TimeSpan.FromSeconds(1))
        {
            RefreshTeamDetails(snap);
            _lastDetailRefresh = DateTime.UtcNow;
        }
        DrawChart();
    }

    private void UpdatePlayers(IReadOnlyList<CombatantSnapshot> rows, long total, double seconds)
    {
        while (Players.Count > rows.Count) Players.RemoveAt(Players.Count - 1);
        while (Players.Count < rows.Count) Players.Add(new RaidPlayerViewModel());
        var max = Math.Max(1, rows.Count == 0 ? 0 : _healingMode ? rows.Max(c => c.Healing) : rows.Max(c => c.Damage));
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var amount = _healingMode ? row.Healing : row.Damage;
            var rate = _healingMode ? amount / seconds : row.Dps;
            var share = total > 0 ? (double)amount / total : 0;
            Players[i].Update(row, i + 1, rate, share, (double)amount / max, _healingMode);
        }
        EmptyPlayersPanel.Visibility = Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlayersList.Visibility = Players.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (Players.Count == 0)
        {
            PlayerCountText.Text = _playerRows.Count == 0 ? "Warte auf Spielerwerte" : "Keine passenden Spieler";
            EmptyPlayersTitle.Text = _playerRows.Count == 0 ? "Noch keine Kampfdaten" : "Keine Treffer für diesen Filter";
            EmptyPlayersDetail.Text = _playerRows.Count == 0
                ? "Sobald Aetherion Treffer aus einem Kampf erkennt, erscheinen hier Spielernamen und Werte."
                : "Passe die Suche oder den Klassenfilter an, um Spieler einzublenden.";
        }
        else PlayerCountText.Text = $"{Players.Count} Spieler angezeigt";
    }

    private void RefreshPlayerRows()
    {
        var classTag = (ClassFilterBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var query = PlayerSearchBox.Text.Trim();
        var rows = _playerRows.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(classTag) && classTag != "*" && Enum.TryParse<GameClass>(classTag, out var selectedClass))
            rows = rows.Where(c => c.Class == selectedClass);
        if (query.Length > 0)
            rows = rows.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Class.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));

        var sort = (SortBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "value";
        var amount = (CombatantSnapshot c) => _healingMode ? c.Healing : c.Damage;
        var rate = (CombatantSnapshot c) => _healingMode ? c.Healing / _combatSeconds : c.Dps;
        rows = sort switch
        {
            "burst" => rows.OrderByDescending(c => _healingMode ? rate(c) : c.BurstDps).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "name" => rows.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            "class" => rows.OrderBy(c => c.Class.ToString(), StringComparer.OrdinalIgnoreCase).ThenByDescending(c => amount(c)),
            "value" => rows.OrderByDescending(c => amount(c)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderByDescending(c => rate(c)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
        };
        UpdatePlayers(rows.ToList(), _playerTotal, _combatSeconds);
    }

    private void PlayerFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (PlayerSearchPlaceholder is not null)
            PlayerSearchPlaceholder.Visibility = string.IsNullOrEmpty(PlayerSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) RefreshPlayerRows();
    }

    private void ViewMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void PanelToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var visible = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        switch (item.Tag?.ToString())
        {
            case "metrics":
                MetricsPanel.Visibility = visible;
                MetricsRow.Height = item.IsChecked ? new GridLength(116) : new GridLength(0);
                break;
            case "chart":
                FightChartPanel.Visibility = visible;
                break;
            case "team":
                TeamStatusPanel.Visibility = visible;
                break;
        }
        var showChart = FightChartPanel.Visibility == Visibility.Visible;
        var showTeam = TeamStatusPanel.Visibility == Visibility.Visible;
        ChartRow.Height = showChart && showTeam ? new GridLength(1.18, GridUnitType.Star) : showChart ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TeamRow.Height = showChart && showTeam ? new GridLength(.92, GridUnitType.Star) : showTeam ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void RefreshTeamDetails(EncounterSnapshot snap)
    {
        var deaths = snap.Combatants.SelectMany(c => _meter.Tracker.Detail(snap.Id, c.ActorId)?.Deaths ?? [])
            .OrderByDescending(d => d.TimeMs).ToList();
        DeathCountText.Text = "Todesfälle " + deaths.Count;
        DeathStatusText.Text = deaths.Count.ToString();
        LastDeathText.Text = deaths.Count == 0 ? "Noch keine Todesfälle" : $"Zuletzt: {PrivacyNames.Display(deaths[0].ActorId, deaths[0].Name)}";

        var incoming = snap.Combatants.SelectMany(c => _meter.Tracker.Detail(snap.Id, c.ActorId)?.IncomingSkills ?? [])
            .OrderByDescending(s => s.Damage).FirstOrDefault();
        TopIncomingText.Text = incoming is null ? "Noch keine Treffer" : incoming.Name;
        TopIncomingMetaText.Text = incoming is null ? "Kein Schaden empfangen" : $"{Format.Compact(incoming.Damage)} Schaden · {incoming.Hits} Treffer";
    }

    private void ClearFight()
    {
        _fightId = null;
        _dpsSamples.Clear();
        BossNameText.Text = "Warte auf Kampf";
        BossMetaText.Text = "Kampfdaten erscheinen automatisch, sobald AION2 einen Kampf meldet.";
        BossHpBar.Value = 0;
        BossPercentText.Text = "—";
        EncounterTimeText.Text = "00:00";
        EncounterStatusText.Text = "Noch kein Kampf erkannt";
        MechanicTitleText.Text = "Warte auf Kampfdaten";
        MechanicDetailText.Text = "Boss-Phasen und Mechaniken erscheinen nur, falls AION2 diese Daten überträgt.";
        PartyDpsText.Text = PartyHpsText.Text = PeakDpsText.Text = DamageTakenText.Text = "—";
        TotalDamageText.Text = "Gesamtschaden —";
        DeathCountText.Text = DeathStatusText.Text = "—";
        LastDeathText.Text = TopIncomingText.Text = "Noch keine Daten";
        TopIncomingMetaText.Text = "Aktueller Kampf";
        LocationText.Text = "Live-Kampfanalyse · Warte auf AION2";
        PlayerCountText.Text = "Warte auf Spieler";
        Players.Clear();
        _playerRows = [];
        EmptyPlayersPanel.Visibility = Visibility.Visible;
        PlayersList.Visibility = Visibility.Collapsed;
        EmptyPlayersTitle.Text = "Noch keine Kampfdaten";
        EmptyPlayersDetail.Text = "Sobald Aetherion Treffer aus einem Kampf erkennt, erscheinen hier Spielernamen und Werte.";
        CaptureStateText.Text = _meter.CaptureStatus.State == CaptureState.Capturing ? "WARTE AUF KAMPF" : "WARTE AUF AION2";
        DrawChart();
    }

    private void DrawChart()
    {
        if (DpsLine is null || DpsLine.ActualWidth < 2 || DpsLine.ActualHeight < 2) return;
        var values = _dpsSamples.ToArray();
        DpsLine.Points = new PointCollection();
        DpsArea.Points = new PointCollection();
        if (values.Length == 0) return;
        var width = DpsLine.ActualWidth;
        var height = DpsLine.ActualHeight;
        var max = Math.Max(1, values.Max());
        var line = new PointCollection();
        for (var i = 0; i < values.Length; i++)
        {
            var x = values.Length == 1 ? width : width * i / (values.Length - 1);
            var y = height - 8 - Math.Clamp(values[i] / max, 0, 1) * Math.Max(8, height - 16);
            line.Add(new Point(x, y));
        }
        var area = new PointCollection { new(0, height) };
        foreach (var point in line) area.Add(point);
        area.Add(new Point(width, height));
        DpsLine.Points = line;
        DpsArea.Points = area;
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void NoDrag(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void Home_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Normal; base.Activate(); }
    private void ToolMenu_Click(object sender, RoutedEventArgs e) => ToolDrawer.Visibility = ToolDrawer.Visibility == Visibility.Visible
        ? Visibility.Collapsed : Visibility.Visible;
    internal void ShowToolMenuPreview() => ToolDrawer.Visibility = Visibility.Visible;
    private void History_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowHistory();
    private void Timers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowBossTimers();
    private void EventTimers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowEventTimers();
    private void Map_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowAionMap();
    private void Guide_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowWakayashi();
    private void Settings_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowSettings();
    private void Overlay_Click(object sender, RoutedEventArgs e) => AppHost.Current.SetOverlayVisible(!AppHost.Current.OverlayVisible);
    private void Details_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowCurrentBreakdown();
    private void DamageMode_Click(object sender, RoutedEventArgs e) { _healingMode = false; UpdateModeButtons(); Refresh(); }
    private void HealingMode_Click(object sender, RoutedEventArgs e) { _healingMode = true; UpdateModeButtons(); Refresh(); }
    private void UpdateModeButtons()
    {
        var selected = new SolidColorBrush(Color.FromRgb(0xF0, 0x6A, 0x22));
        var selectedText = new SolidColorBrush(Color.FromRgb(0x12, 0x0D, 0x0A));
        var inactive = new SolidColorBrush(Color.FromRgb(0x1B, 0x19, 0x1A));
        var inactiveText = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEA));
        DamageModeButton.Background = _healingMode ? inactive : selected;
        DamageModeButton.Foreground = _healingMode ? inactiveText : selectedText;
        HealingModeButton.Background = _healingMode ? selected : inactive;
        HealingModeButton.Foreground = _healingMode ? selectedText : inactiveText;
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private static Brush ClassColor(GameClass gameClass) => gameClass switch
    {
        GameClass.Gladiator => new SolidColorBrush(Color.FromRgb(0xF2, 0x8A, 0x45)),
        GameClass.Templar => new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x6A)),
        GameClass.Assassin => new SolidColorBrush(Color.FromRgb(0xD6, 0x6A, 0x2A)),
        GameClass.Ranger => new SolidColorBrush(Color.FromRgb(0xFF, 0x8F, 0x4A)),
        GameClass.Sorcerer => new SolidColorBrush(Color.FromRgb(0xC8, 0x54, 0x1E)),
        GameClass.Elementalist => new SolidColorBrush(Color.FromRgb(0xF6, 0xC0, 0x9A)),
        GameClass.Cleric => new SolidColorBrush(Color.FromRgb(0xB8, 0x4B, 0x1B)),
        GameClass.Chanter => new SolidColorBrush(Color.FromRgb(0xE4, 0x7A, 0x35)),
        _ => new SolidColorBrush(Color.FromRgb(0xA2, 0xB1, 0xB9)),
    };

    public sealed class RaidPlayerViewModel : ObservableObject
    {
        private string _name = "";
        private string _classLabel = "";
        private string _rateText = "";
        private string _shareText = "";
        private string _rankText = "";
        private double _barWidth;
        private bool _isSelf;
        private Brush _barBrush = Brushes.White;
        private Brush _classBrush = Brushes.White;
        public string Name { get => _name; private set => Set(ref _name, value); }
        public string ClassLabel { get => _classLabel; private set => Set(ref _classLabel, value); }
        public string RateText { get => _rateText; private set => Set(ref _rateText, value); }
        public string ShareText { get => _shareText; private set => Set(ref _shareText, value); }
        public string RankText { get => _rankText; private set => Set(ref _rankText, value); }
        public double BarWidth { get => _barWidth; private set => Set(ref _barWidth, value); }
        public bool IsSelf { get => _isSelf; private set => Set(ref _isSelf, value); }
        public Brush BarBrush { get => _barBrush; private set => Set(ref _barBrush, value); }
        public Brush ClassBrush { get => _classBrush; private set => Set(ref _classBrush, value); }
        public string ClassGlyph => "◆";

        public void Update(CombatantSnapshot player, int rank, double rate, double share, double fraction, bool healing)
        {
            Name = player.Name;
            ClassLabel = UiText.Current.ClassName(player.Class) + (healing ? " · HEAL" : " · DU");
            RateText = Format.Compact(rate) + (healing ? "/s" : "/s");
            ShareText = Format.Percent(share, 0);
            RankText = rank.ToString("00");
            BarWidth = Math.Clamp(fraction, 0, 1) * 240;
            IsSelf = player.IsSelf;
            ClassBrush = ClassColor(player.Class);
            BarBrush = ClassBrush;
        }
    }
}
