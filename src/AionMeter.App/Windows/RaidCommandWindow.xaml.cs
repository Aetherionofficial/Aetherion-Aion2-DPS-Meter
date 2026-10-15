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

    public ObservableCollection<RaidPlayerViewModel> Players { get; } = [];

    public RaidCommandWindow(MeterService meter)
    {
        _meter = meter;
        InitializeComponent();
        PlayersList.ItemsSource = Players;
        Loaded += (_, _) => Refresh();
        SizeChanged += (_, _) => DrawChart();
        Closing += (_, e) =>
        {
            // The meter lives in the notification area; closing this dashboard hides it instead of killing capture.
            e.Cancel = true;
            Hide();
        };
    }

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
        var snap = selected is null ? null : _meter.Tracker.Snapshot(selected.Id, now);
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
        var healing = snap.Combatants.Sum(c => c.Healing);
        PartyDpsText.Text = Format.Compact(snap.PartyDps) + "/s";
        PartyHpsText.Text = Format.Compact(healing / activeSeconds) + "/s";
        PeakDpsText.Text = Format.Compact(snap.PeakPartyDps) + "/s";
        TotalDamageText.Text = $"Gesamtschaden {Format.Compact(snap.TotalDamage)}";
        DamageTakenText.Text = Format.Compact(snap.Combatants.Sum(c => c.DamageTaken));
        PlayerCountText.Text = $"{snap.PlayerCount} Spieler · {(snap.IsActive ? "Kampf aktiv" : "Kampf beendet")}";
        ChartRangeText.Text = snap.IsActive ? "LIVE · letzte 18 Sek." : "KAMPF BEENDET";

        var total = _healingMode ? healing : snap.TotalDamage;
        var ordered = snap.Combatants
            .Where(c => !c.IsUnknownSummons && (_healingMode ? c.Healing > 0 : c.Damage > 0))
            .OrderByDescending(c => _healingMode ? c.Healing : c.Damage)
            .ToList();
        UpdatePlayers(ordered, total, activeSeconds);

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
        if (Players.Count == 0) PlayerCountText.Text = "Noch keine Spieler in diesem Kampf";
    }

    private void RefreshTeamDetails(EncounterSnapshot snap)
    {
        var deaths = snap.Combatants.SelectMany(c => _meter.Tracker.Detail(snap.Id, c.ActorId)?.Deaths ?? [])
            .OrderByDescending(d => d.TimeMs).ToList();
        DeathCountText.Text = "Todesfälle " + deaths.Count;
        DeathStatusText.Text = deaths.Count.ToString();
        LastDeathText.Text = deaths.Count == 0 ? "Noch keine Todesfälle" : $"Zuletzt: {deaths[0].Name}";

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
    private void History_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowHistory();
    private void Timers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowBossTimers();
    private void EventTimers_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowEventTimers();
    private void Map_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowAionMap();
    private void Guide_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowWakayashi();
    private void Settings_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowSettings();
    private void Overlay_Click(object sender, RoutedEventArgs e) => AppHost.Current.SetOverlayVisible(!AppHost.Current.OverlayVisible);
    private void Details_Click(object sender, RoutedEventArgs e) => AppHost.Current.ShowCurrentBreakdown();
    private void DamageMode_Click(object sender, RoutedEventArgs e) { _healingMode = false; Refresh(); }
    private void HealingMode_Click(object sender, RoutedEventArgs e) { _healingMode = true; Refresh(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private static Brush ClassColor(GameClass gameClass) => gameClass switch
    {
        GameClass.Gladiator => new SolidColorBrush(Color.FromRgb(0xF0, 0x9A, 0x55)),
        GameClass.Templar => new SolidColorBrush(Color.FromRgb(0xEA, 0xC4, 0x69)),
        GameClass.Assassin => new SolidColorBrush(Color.FromRgb(0xB5, 0x8B, 0xFF)),
        GameClass.Ranger => new SolidColorBrush(Color.FromRgb(0x66, 0xBD, 0xF1)),
        GameClass.Sorcerer => new SolidColorBrush(Color.FromRgb(0x72, 0xBA, 0xFF)),
        GameClass.Elementalist => new SolidColorBrush(Color.FromRgb(0xE0, 0x86, 0xD7)),
        GameClass.Cleric => new SolidColorBrush(Color.FromRgb(0x73, 0xD8, 0xA0)),
        GameClass.Chanter => new SolidColorBrush(Color.FromRgb(0x56, 0xD6, 0xC8)),
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
