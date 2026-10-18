using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AionMeter.App.Services;
using AionMeter.Core;
using AionMeter.Core.Combat;
using AionMeter.Core.Storage;

namespace AionMeter.App.Windows;

/// <summary>Local saved-fight workspace. No automatic uploads; notes/targets are committed explicitly.</summary>
public sealed class FightJournalWindow : Window
{
    private readonly MeterService _meter;
    private readonly IReadOnlyList<(HistoryEntry Entry, FightRecord Record)>? _preview;
    private readonly TextBox _search = Input(230), _character = Input(145), _note = Input(500), _target = Input(180);
    private readonly DatePicker _from = new() { Width = 135 }, _through = new() { Width = 135 };
    private readonly CheckBox _favorites = new() { Content = "Nur Favoriten", Foreground = Brushes.WhiteSmoke, Margin = new Thickness(10, 7, 10, 7) };
    private readonly ComboBox _result = new() { Width = 135 };
    private readonly ListBox _list = new() { MinWidth = 260, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _title = Label("Kampf auswählen", 23), _meta = Label(""), _feedback = Label(""), _bossStats = Label(""), _quality = Label("");
    private readonly CheckBox _favorite = new() { Content = "Als Favorit behalten", Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _goalProgress = Label(""), _healingNote = Label("");
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Foreground = Brushes.DarkOrange, Background = B("#273144"), Height = 12, Width = 350, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 12) };
    private readonly DataGrid _development = Table(), _skills = Table(), _recipients = Table();
    private readonly ComboBox _healers = new() { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TabControl _tabs = new();
    private readonly Button _save, _export;
    private HistoryEntry? _entry;
    private FightRecord? _record;
    private FightAnnotation _annotation = new();
    private string _savedTarget = "";
    private string? _privateNoteDraft;
    private bool _selectionGuard;
    private int _selectionVersion;
    private CancellationTokenSource? _load;
    private IReadOnlyList<HistoryEntry> _entries = [];
    private sealed class Row(HistoryEntry entry, string display) : System.ComponentModel.INotifyPropertyChanged
    {
        public HistoryEntry Entry { get; } = entry;
        public string Display { get; private set; } = display;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void Rename(string text) { Display = text; PropertyChanged?.Invoke(this, new(nameof(Display))); }
    }
    private sealed record HealerChoice(HealerRecord Healer, string Display);

    public FightJournalWindow(MeterService meter, HistoryEntry? selected = null,
        IReadOnlyList<(HistoryEntry Entry, FightRecord Record)>? preview = null)
    {
        _meter = meter; _preview = preview;
        Title = "Aetherion · Kampfbuch & Training"; Width = 1220; Height = 870; MinWidth = 900; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = B("#0D1017"); Foreground = Brushes.WhiteSmoke; FontFamily = new FontFamily("Segoe UI");
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 15) };
        var refresh = Button("Aktualisieren", () => { if (LeaveEdits()) Reload(_entry?.FileName); });
        DockPanel.SetDock(refresh, Dock.Right); heading.Children.Add(refresh);
        heading.Children.Add(Label("KAMPFBUCH  /  Training & Discord", 24)); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        Field(filters, "Boss, Notiz oder Suche", _search); Field(filters, "Charakter", _character);
        Field(filters, "Von", _from); Field(filters, "Bis einschließlich", _through);
        _result.ItemsSource = new[] { "Alle Ergebnisse", "Kills", "Wipes", "Andere Enden" }; _result.SelectedIndex = 0;
        Field(filters, "Ergebnis", _result); filters.Children.Add(_favorites);
        DockPanel.SetDock(filters, Dock.Top); root.Children.Add(filters);
        _feedback.Margin = new Thickness(0, 10, 0, 0); _feedback.Foreground = B("#FFB780");
        DockPanel.SetDock(_feedback, Dock.Bottom); root.Children.Add(_feedback);
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); root.Children.Add(body);
        _list.Background = B("#151B26"); _list.Foreground = Foreground; _list.DisplayMemberPath = nameof(Row.Display);
        _list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        _list.SelectionChanged += async (_, _) => await SelectAsync(); body.Children.Add(_list);
        var detail = new DockPanel { Margin = new Thickness(18, 0, 0, 0) }; Grid.SetColumn(detail, 1); body.Children.Add(detail);
        var header = new StackPanel(); header.Children.Add(_title); header.Children.Add(_meta);
        _export = Button("Ergebniskarte für Discord…", () => { if (_record is not null) new DiscordCardWindow(_record, _meter.DisplayTitle(_entry!)) { Owner = this }.ShowDialog(); });
        _export.IsEnabled = false; header.Children.Add(_export); DockPanel.SetDock(header, Dock.Top); detail.Children.Add(header);
        _tabs.Background = B("#121822"); _tabs.Foreground = Brushes.WhiteSmoke;
        _tabs.ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem">
              <Setter Property="Foreground" Value="#BBC7D8" />
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
                <Border Name="Surface" Background="#1B2330" BorderBrush="#3A4658" BorderThickness="0,0,0,2" Padding="10,12" Margin="0,0,2,0"><ContentPresenter ContentSource="Header" /></Border>
                <ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Surface" Property="Background" Value="#403025"/><Setter TargetName="Surface" Property="BorderBrush" Value="#FF964E"/><Setter Property="Foreground" Value="White"/></Trigger></ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        detail.Children.Add(_tabs);
        var diary = Page("Boss-Tagebuch"); diary.Children.Add(_bossStats);
        Note(diary, "Alle gespeicherten Versuche dieses Bosses und Charakters (Klasse, Server und Zone getrennt). Die Listenfilter ändern die Auswahl links. Bestwerte und Entwicklung zählen nur vergleichbare vollständige Kills.");
        Columns(_development, ("Datum", "When"), ("Eigene DPS", "Dps"), ("Ergebnis", "Result"), ("Bestwert-fähig", "Eligible")); diary.Children.Add(_development);
        var notes = Page("Notizen & Ziele"); notes.Children.Add(_favorite);
        Note(notes, "Eigene Notiz zum Versuch (maximal 2.000 Zeichen; wird nicht auf der Discord-Karte geteilt)");
        _note.AcceptsReturn = true; _note.TextWrapping = TextWrapping.Wrap; _note.Height = 125; _note.MaxLength = 2000; _note.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; notes.Children.Add(_note);
        Note(notes, "DPS-Ziel als ganze Zahl ohne Trennzeichen, z.B. 50000 (1–1.000.000.000; leer = entfernen)"); notes.Children.Add(_target);
        _save = Button("Notiz, Favorit & Ziel speichern", SaveEdits); notes.Children.Add(_save);
        notes.Children.Add(_progress); notes.Children.Add(_goalProgress);
        Note(notes, "Nach jedem gespeicherten Versuch steht der Fortschritt hier. Nach Kampfende erscheint er zusätzlich kurz im Overlay. Eingeschränkte Daten oder fehlende Kills bleiben vorläufig und bestätigen keine Zielerreichung.");
        var healing = Page("Heiler-Auswertung"); healing.Children.Add(_healingNote); healing.Children.Add(_healers);
        _healers.DisplayMemberPath = nameof(HealerChoice.Display); _healers.SelectionChanged += (_, _) => ShowHealer();
        Note(healing, "Heilung nach Fähigkeit"); Columns(_skills, ("Fähigkeit", "Name"), ("Erfasste Heilung", "Amount"), ("Ereignisse", "Events"), ("Anteil", "Share")); healing.Children.Add(_skills);
        Note(healing, "Heilung nach Empfänger"); Columns(_recipients, ("Spieler", "Name"), ("Erfasste Heilung", "Amount"), ("Ereignisse", "Events"), ("Anteil", "Share")); healing.Children.Add(_recipients);
        var data = Page("Datenqualität"); data.Children.Add(_quality);
        Note(data, "Effektive Heilung und Überheilung sind mit den aktuellen Ereignissen nicht zuverlässig unterscheidbar. HPS nutzt den erfassten Kampf-/Heilungsabschnitt; außerhalb des Kampfabschnitts oder nicht empfangene Heilungen fehlen.");
        _search.TextChanged += (_, _) => Filter(); _character.TextChanged += (_, _) => Filter();
        _from.SelectedDateChanged += (_, _) => Filter(); _through.SelectedDateChanged += (_, _) => Filter();
        _result.SelectionChanged += (_, _) => Filter(); _favorites.Checked += (_, _) => Filter(); _favorites.Unchecked += (_, _) => Filter();
        if (preview is null) _meter.History.Changed += HistoryChanged;
        PrivacyNames.Changed += PrivacyChanged;
        Closed += (_, _) => { _selectionVersion++; _load?.Cancel(); _load?.Dispose(); _meter.History.Changed -= HistoryChanged; PrivacyNames.Changed -= PrivacyChanged; };
        Closing += (_, e) => { if (IsVisible && IsEnabled && !LeaveEdits()) e.Cancel = true; };
        Reload(selected?.FileName);
    }

    public void SelectEntry(HistoryEntry entry)
    {
        if (!LeaveEdits()) return;
        _selectionGuard = true;
        _search.Text = _character.Text = ""; _from.SelectedDate = _through.SelectedDate = null;
        _favorites.IsChecked = false; _result.SelectedIndex = 0;
        _selectionGuard = false;
        Reload(entry.FileName);
    }

    public void ShowPageForPreview(string? page) => _tabs.SelectedIndex = page switch
        { "notes" => 1, "healing" => 2, "quality" => 3, _ => 0 };

    private void HistoryChanged() => Dispatcher.BeginInvoke(() => { if (IsLoaded) _feedback.Text = "Neuer Kampf gespeichert. Mit „Aktualisieren“ in die Liste übernehmen."; });
    private void PrivacyChanged()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(PrivacyChanged); return; }
        _selectionGuard = true;
        if (PrivacyNames.Enabled) { _privateNoteDraft ??= _note.Text; _note.Text = ""; }
        else { _note.Text = _privateNoteDraft ?? _annotation.Note; _privateNoteDraft = null; }
        _note.IsEnabled = !PrivacyNames.Enabled;
        _search.Text = "";
        _character.Text = ""; _character.IsEnabled = !PrivacyNames.Enabled;
        _selectionGuard = false;
        Filter(checkEdits: false, preserveDetails: true); ShowHealing();
    }
    private void Reload(string? selected)
    {
        _entries = _preview?.Select(p => p.Entry).ToArray() ?? _meter.History.List();
        _feedback.Text = _preview is not null ? "VORSCHAU · Beispieldaten" : _meter.Journal.LoadError ?? (_meter.Settings.SaveHistory ? $"{_entries.Count} gespeicherte Kämpfe · nur lokal" : "Kampfspeicherung ist deaktiviert. Für neue Tagebucheinträge in den Einstellungen die Verlaufsspeicherung einschalten.");
        _character.IsEnabled = !PrivacyNames.Enabled;
        Filter(false, selected);
    }
    private void Filter() => Filter(true);
    private void Filter(bool checkEdits, string? selected = null, bool preserveDetails = false)
    {
        if (_selectionGuard || checkEdits && !LeaveEdits()) return;
        var query = new FightHistoryQuery(_search.Text, _from.SelectedDate, _through.SelectedDate, _character.Text,
            _result.SelectedIndex == 1 ? EncounterEndReason.Kill : _result.SelectedIndex == 2 ? EncounterEndReason.Wipe : null, _favorites.IsChecked == true);
        var rows = _entries.Where(e => query.Matches(e, _meter.Journal.Annotation(e.FileName), _meter.DisplayTitle(e)))
            .Where(e => _result.SelectedIndex != 3 || e.Reason is not (EncounterEndReason.Kill or EncounterEndReason.Wipe))
            .Select(e => new Row(e, $"{(_meter.Journal.Annotation(e.FileName).Favorite ? "★ " : "")}{_meter.DisplayTitle(e)}\n{e.StartedAt.LocalDateTime:dd.MM.yy HH:mm} · {Result(e.Reason)} · {Format.Compact(e.SelfDps)}/s\n{(PrivacyNames.Enabled ? "Charakter verborgen" : e.SelfName ?? "Charakter unbekannt")}" )).ToArray();
        selected ??= _entry?.FileName;
        _selectionGuard = true; _list.ItemsSource = rows;
        if (preserveDetails && rows.FirstOrDefault(r => r.Entry.FileName == selected) is { } kept)
        { _list.SelectedItem = kept; _selectionGuard = false; return; }
        _selectionGuard = false;
        _list.SelectedItem = rows.FirstOrDefault(r => r.Entry.FileName == selected) ?? rows.FirstOrDefault();
        if (rows.Length == 0) Clear();
    }
    private async Task SelectAsync()
    {
        if (_selectionGuard || _list.SelectedItem is not Row row) return;
        if (!LeaveEdits())
        {
            _selectionGuard = true;
            _list.SelectedItem = ((IEnumerable<Row>)_list.ItemsSource).FirstOrDefault(r => r.Entry.FileName == _entry?.FileName);
            _selectionGuard = false; return;
        }
        _load?.Cancel(); _load?.Dispose(); _load = new CancellationTokenSource(); var token = _load.Token;
        var version = ++_selectionVersion; _entry = row.Entry; _record = null; _export.IsEnabled = _save.IsEnabled = false;
        _title.Text = _meter.DisplayTitle(row.Entry); _meta.Text = "Auswertung wird geladen …";
        _development.ItemsSource = null; _tabs.IsEnabled = false;
        var record = _preview?.FirstOrDefault(p => p.Entry.FileName == row.Entry.FileName).Record;
        record ??= await Task.Run(() => _meter.History.Load(row.Entry.Path));
        if (version != _selectionVersion) return;
        if (record is null) { Clear(); _feedback.Text = "Die Kampfdatei konnte nicht gelesen werden. Das Original wurde nicht verändert."; return; }
        Populate(row.Entry, record);
        var entries = _entries.Where(e => e.BossCode == row.Entry.BossCode && e.SelfName == row.Entry.SelfName && e.Zone == row.Entry.Zone && e.SelfClass == row.Entry.SelfClass).ToArray();
        BossJournalSummary summary;
        try
        {
            summary = _preview is not null ? JournalInsights.Boss(record, _preview.Select(p => p.Record)) : await Task.Run(() =>
                JournalInsights.Boss(record, entries.Select(e => { token.ThrowIfCancellationRequested(); return _meter.History.Load(e.Path); }).OfType<FightRecord>()), token);
        }
        catch (OperationCanceledException) { return; }
        if (version != _selectionVersion) return;
        _bossStats.Text = TrainingScope.For(record) is null ? "Für persönliche Bosswerte müssen Boss, Zone, Charakter, Klasse und Server bekannt sein."
            : $"{summary.Attempts} Versuche · {summary.Kills} Kills · Bestwert: {(summary.BestDps is { } best ? Format.Compact(best) + "/s" : "noch kein vergleichbarer Kill")}"
                + (summary.FastestKillMs is { } fastest ? $" · Schnellster Kill: {Format.Clock(fastest)}" : "");
        var first = summary.Development.FirstOrDefault(p => p.ComparableKill)?.Dps;
        _development.ItemsSource = summary.Development.Reverse().Select(p => new
        { When = p.At.LocalDateTime.ToString("dd.MM.yy HH:mm"), Dps = Format.Compact(p.Dps) + "/s" + (p.ComparableKill && first is > 0 ? $" ({p.Dps / first.Value - 1:+0.0%;-0.0%;0.0%})" : ""), Result = Result(p.Result), Eligible = p.ComparableKill ? "Ja" : "Nein / vorläufig" }).ToArray();
    }
    private void Populate(HistoryEntry entry, FightRecord record)
    {
        _tabs.IsEnabled = true;
        _privateNoteDraft = null;
        _entry = entry; _record = record; _annotation = _meter.Journal.Annotation(entry.FileName);
        _selectionGuard = true;
        _favorite.IsChecked = _annotation.Favorite; _note.Text = PrivacyNames.Enabled ? "" : _annotation.Note; _note.IsEnabled = !PrivacyNames.Enabled;
        _savedTarget = _meter.Journal.Target(TrainingScope.For(record))?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        _target.Text = _savedTarget; _target.IsEnabled = TrainingScope.For(record) is not null;
        _selectionGuard = false; _save.IsEnabled = _meter.Journal.LoadError is null; _export.IsEnabled = true;
        var quality = JournalInsights.Quality(record);
        _meta.Text = $"{entry.StartedAt.LocalDateTime:dd.MM.yyyy HH:mm} · {Format.Clock(record.Summary.CombatMs)} · {Result(record.Summary.Reason)} · {quality.Label}";
        _quality.Text = quality.Label + "\n\n" + quality.Explanation + "\n\n" + (JournalInsights.EligibleBest(record) ? "Dieser Kill kann als persönlicher Bestwert zählen." : "Dieser Versuch zählt nicht als bestätigter persönlicher Bestwert.");
        RefreshGoal(); ShowHealing();
    }
    private void ShowHealing()
    {
        if (_record is null) return;
        var old = (_healers.SelectedItem as HealerChoice)?.Healer.ActorId;
        _healingNote.Text = "Erfasste Heilung · HPS über den erfassten Kampf-/Heilungsabschnitt. Effektive Heilung / Überheilung: nicht verfügbar."
            + (_record.Healers is null ? " Ältere Datei: reine Heiler und Empfänger wurden noch nicht vollständig gespeichert." : " Nur empfangene Ereignisse im aktiven Kampf werden gezählt.")
            + (_record.HealingTruncated ? " Achtung: Detailtabellen sind durch die Speichergrenze eingeschränkt." : "");
        var choices = JournalInsights.Healing(_record).OrderByDescending(h => h.Total).Select(h => new HealerChoice(h,
            $"{PrivacyNames.Display(h.ActorId, h.Name)} · {Format.Compact(h.Total)} · {Format.Compact(h.Total / Math.Max(1, (_record.HealingDurationMs ?? _record.Summary.CombatMs) / 1000.0))} HPS")).ToArray();
        _healers.ItemsSource = choices; _healers.SelectedItem = choices.FirstOrDefault(h => h.Healer.ActorId == old) ?? choices.FirstOrDefault();
        if (choices.Length == 0) { _skills.ItemsSource = null; _recipients.ItemsSource = null; _healingNote.Text += " Keine Heilereignisse erfasst; das beweist nicht, dass keine Heilung stattfand."; }
    }
    private void ShowHealer()
    {
        if (_healers.SelectedItem is not HealerChoice choice) return;
        var healer = choice.Healer;
        _skills.ItemsSource = healer.Skills.OrderByDescending(s => s.Amount).Select(s => new
        { s.Name, Amount = Format.Compact(s.Amount), s.Events, Share = healer.Total > 0 ? (s.Amount / (double)healer.Total).ToString("P1") : "—" }).ToArray();
        _recipients.ItemsSource = healer.Recipients?.OrderByDescending(s => s.Amount).Select(s => new
        { Name = PrivacyNames.Display(s.ActorId, s.Name), Amount = Format.Compact(s.Amount), s.Events, Share = healer.Total > 0 ? (s.Amount / (double)healer.Total).ToString("P1") : "—" }).ToArray();
        if (healer.Recipients is null) _healingNote.Text += " Empfängerdaten fehlen in dieser älteren Datei.";
    }
    private bool Dirty => _record is not null && ((_favorite.IsChecked == true) != _annotation.Favorite
        || (PrivacyNames.Enabled ? _privateNoteDraft ?? _annotation.Note : _note.Text) != _annotation.Note || _target.Text != _savedTarget);
    private bool LeaveEdits()
    {
        if (!Dirty) return true;
        var answer = MessageBox.Show(this, "Ungespeicherte Notiz, Favorit oder Ziel speichern?", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) { SaveEdits(); return !Dirty; }
        _selectionGuard = true; _privateNoteDraft = null; _note.Text = PrivacyNames.Enabled ? "" : _annotation.Note; _favorite.IsChecked = _annotation.Favorite; _target.Text = _savedTarget; _selectionGuard = false;
        return true;
    }
    private void SaveEdits()
    {
        if (_entry is null || _record is null) return;
        try
        {
            double? target = null;
            if (_target.IsEnabled && !string.IsNullOrWhiteSpace(_target.Text))
            {
                if (!long.TryParse(_target.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    || parsed < 1 || parsed > 1_000_000_000) throw new ArgumentException("DPS-Ziel als ganze Zahl von 1 bis 1.000.000.000 ohne Trennzeichen eingeben, z.B. 50000.");
                target = parsed;
            }
            // Commit the note, favorite and goal together so a write failure cannot leave a partial edit.
            if (_note.Text.Length > 2000) throw new ArgumentException("Notiz: maximal 2.000 Zeichen.");
            _meter.Journal.Edit(_entry.FileName, _favorite.IsChecked == true, PrivacyNames.Enabled ? _privateNoteDraft ?? _annotation.Note : _note.Text,
                TrainingScope.For(_record), target);
            _annotation = _meter.Journal.Annotation(_entry.FileName); _savedTarget = target?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""; _target.Text = _savedTarget;
            if (_list.Items.Cast<Row>().FirstOrDefault(r => r.Entry.FileName == _entry.FileName) is { } current)
                current.Rename($"{(_annotation.Favorite ? "★ " : "")}{_meter.DisplayTitle(_entry)}\n{_entry.StartedAt.LocalDateTime:dd.MM.yy HH:mm} · {Result(_entry.Reason)} · {Format.Compact(_entry.SelfDps)}/s\n{(PrivacyNames.Enabled ? "Charakter verborgen" : _entry.SelfName ?? "Charakter unbekannt")}");
            _feedback.Text = "Notiz, Favorit und Trainingsziel lokal gespeichert."; RefreshGoal();
            var saved = _entry.FileName;
            Dispatcher.BeginInvoke(() => { if (IsLoaded && _entry?.FileName == saved && !Dirty) Filter(false, saved); });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _feedback.Text = ex.Message; }
    }
    private void RefreshGoal()
    {
        if (_record is null) return;
        var progress = JournalInsights.Progress(_record, _meter.Journal.Target(TrainingScope.For(_record)));
        _progress.Value = progress?.Percent ?? 0;
        _goalProgress.Text = progress is null ? "Noch kein Ziel gesetzt." : $"{progress.Percent:0.0}% · {Format.Compact(progress.ObservedDps)} / {Format.Compact(progress.Target)} DPS\n"
            + (progress.Reached ? "Ziel in einem vergleichbaren vollständigen Kill erreicht." : progress.Confirmed ? "Noch unter deinem Ziel." : "Vorläufig: Dieser Versuch bestätigt keine Zielerreichung. Siehe Datenqualität und Ergebnis.");
    }
    private void Clear()
    {
        _selectionVersion++; _load?.Cancel(); _privateNoteDraft = null; _record = null; _entry = null; _annotation = new();
        _title.Text = "Keine passenden Kämpfe"; _meta.Text = "Filter anpassen oder einen Kampf speichern lassen.";
        _bossStats.Text = _quality.Text = _goalProgress.Text = _note.Text = _target.Text = "";
        _savedTarget = ""; _progress.Value = 0; _favorite.IsChecked = false;
        _development.ItemsSource = _skills.ItemsSource = _recipients.ItemsSource = null; _healers.ItemsSource = null;
        _tabs.IsEnabled = false;
        _save.IsEnabled = _export.IsEnabled = false;
    }
    private StackPanel Page(string title)
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        _tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        return panel;
    }
    private static TextBox Input(double width) => new() { Width = width, HorizontalAlignment = HorizontalAlignment.Left, Background = B("#1E2634"), Foreground = Brushes.WhiteSmoke, BorderBrush = B("#465063"), Padding = new Thickness(8, 6, 8, 6), MaxLength = 2000 };
    private static TextBlock Label(string text, double size = 13) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
    private static void Note(Panel panel, string text) { var label = Label(text); label.Foreground = B("#ADB8C9"); panel.Children.Add(label); }
    private static void Field(Panel panel, string label, Control control)
    { var stack = new StackPanel { Margin = new Thickness(0, 0, 10, 5) }; stack.Children.Add(Label(label, 11)); stack.Children.Add(control); panel.Children.Add(stack); }
    private static Button Button(string text, Action action)
    { var button = new Button { Content = text, Style = (Style)Application.Current.FindResource("GhostButton"), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 8, 10), HorizontalAlignment = HorizontalAlignment.Left }; button.Click += (_, _) => action(); return button; }
    private static DataGrid Table() => new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
        Background = B("#151B26"), Foreground = Brushes.WhiteSmoke, RowBackground = B("#151B26"), AlternatingRowBackground = B("#1D2532"), BorderBrush = B("#465063"), GridLinesVisibility = DataGridGridLinesVisibility.None,
        MaxHeight = 330, MinHeight = 110, Margin = new Thickness(0, 4, 0, 12) };
    private static void Columns(DataGrid grid, params (string Header, string Property)[] columns)
    {
        grid.ColumnHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader))
        { Setters = { new Setter(Control.BackgroundProperty, B("#253145")), new Setter(Control.ForegroundProperty, Brushes.WhiteSmoke), new Setter(Control.PaddingProperty, new Thickness(8)) } };
        foreach (var (header, property) in columns) grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property), Width = new DataGridLength(grid.Columns.Count == 0 ? 240 : 170), MinWidth = 120 }); }
    private static string Result(EncounterEndReason reason) => reason switch { EncounterEndReason.Kill => "Kill", EncounterEndReason.Wipe => "Wipe", EncounterEndReason.None => "Laufend", _ => "Beendet" };
    private static SolidColorBrush B(string value) => new((Color)ColorConverter.ConvertFromString(value));
}
