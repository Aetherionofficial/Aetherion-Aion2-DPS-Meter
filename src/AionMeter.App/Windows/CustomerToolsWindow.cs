using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AionMeter.App.Services;
using AionMeter.Core;
using AionMeter.Core.Capture;
using AionMeter.Core.Combat;
using AionMeter.Core.Storage;
using Microsoft.Win32;

namespace AionMeter.App.Windows;

/// <summary>Customer-facing tools. Changes are applied explicitly; file exports show their content first.</summary>
public sealed class CustomerToolsWindow : Window
{
    private readonly MeterService _meter;
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 8, 16, 12) };
    private readonly TabControl _tabs = new();
    private readonly StackPanel _analysis = new();
    private readonly StackPanel _help = new();
    private readonly ComboBox _fights = new() { MinWidth = 360, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly CheckBox _auto;
    private readonly CheckBox _privacy;
    private readonly CheckBox _saving;
    private readonly CheckBox _contrast;
    private readonly CheckBox _colors;
    private readonly CheckBox _records;
    private readonly Slider _font;
    private readonly List<HistoryEntry> _entries;
    private readonly System.Windows.Threading.DispatcherTimer _diagnostics = new() { Interval = TimeSpan.FromSeconds(2) };

    public CustomerToolsWindow(MeterService meter, bool firstRun = false)
    {
        _meter = meter;
        Title = "Aetherion · Komfort & Analyse";
        Width = 880; Height = 730; MinWidth = 620; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(18, 18, 23)); Foreground = Brushes.WhiteSmoke;
        FontFamily = (FontFamily)Application.Current.FindResource("UiFont"); FontSize = 13;
        _tabs.Background = Background; _tabs.Foreground = Foreground;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        var root = new DockPanel();
        var header = new DockPanel { Margin = new Thickness(20, 16, 16, 12), Background = Brushes.Transparent };
        var close = new Button { Content = "✕", Padding = new Thickness(10, 5, 10, 5), Style = (Style)Application.Current.FindResource("GhostButton"), ToolTip = "Schließen" };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = "AETHERION  /  Komfort & Analyse", Foreground = Brushes.WhiteSmoke, FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock || ReferenceEquals(e.OriginalSource, header)) DragMove(); };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        _feedback.Foreground = Brushes.LightGray;
        _tabs.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 49, 48));
        _tabs.ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem">
              <Setter Property="Foreground" Value="#BFC3CB" />
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
                <Border Name="Surface" Background="#19191F" BorderBrush="#343039" BorderThickness="0,0,0,2" Padding="14,12" Margin="0,0,3,0">
                  <ContentPresenter ContentSource="Header" />
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsSelected" Value="True"><Setter TargetName="Surface" Property="Background" Value="#302018"/><Setter TargetName="Surface" Property="BorderBrush" Value="#F28A45"/><Setter Property="Foreground" Value="White"/></Trigger>
                  <Trigger Property="IsMouseOver" Value="True"><Setter Property="Foreground" Value="White"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        DockPanel.SetDock(_feedback, Dock.Bottom); root.Children.Add(_feedback); root.Children.Add(_tabs); Content = root;
        var display = Page("Anzeige & Profile");
        Heading(display, "Dein Overlay, passend zu deinem Spiel");
        Note(display, "Profile ändern nur die Darstellung. Speichere deine bevorzugte Größe als Solo, Gruppe oder Raid. Änderungen werden mit den Schaltflächen unten angewendet.");
        _auto = Check(display, "Fensterhöhe automatisch an Spielerzahl anpassen", meter.Settings.AutoOverlayHeight);
        _privacy = Check(display, "Streamer-Modus: Namen und Server in Spielerauswertungen verbergen", meter.Settings.StreamerMode);
        _saving = Check(display, "Sparmodus: Overlay mit 2 statt 5 Aktualisierungen pro Sekunde", meter.Settings.PowerSaving);
        _contrast = Check(display, "Hoher Kontrast: dunkle Flächen und weiße Schrift", meter.Settings.HighContrast);
        _colors = Check(display, "Farbsehfreundliche Balken (Klassen bleiben über Symbol und Text erkennbar)", meter.Settings.AccessibleColors);
        _records = Check(display, "Persönliche Boss-Rekorde nach vollständigen Kills anzeigen", meter.Settings.ShowPersonalRecords);
        Note(display, "Schriftgröße im Overlay (80–150 %)");
        _font = new Slider { Minimum = .8, Maximum = 1.5, TickFrequency = .1, IsSnapToTickEnabled = true,
            Value = meter.Settings.OverlayTextScale, Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 10) };
        _font.Style = (Style)Application.Current.FindResource("GoldSlider");
        display.Children.Add(_font);
        var fontValue = new TextBlock { Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 8) };
        fontValue.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = _font, StringFormat = "{0:P0}" });
        display.Children.Add(fontValue);
        Action(display, "Anzeige übernehmen", Apply);
        var profiles = new ComboBox { ItemsSource = CustomerTools.ProfileNames, SelectedIndex = 1, Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        display.Children.Add(profiles);
        var profileActions = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
        display.Children.Add(profileActions);
        Action(profileActions, "Als gewähltes Profil speichern", () =>
        {
            Apply(); var name = (string)profiles.SelectedItem;
            meter.Settings.LayoutProfiles[name] = LayoutProfile.From(meter.Settings); meter.Settings.Save();
            Say($"Profil „{name}“ gespeichert.");
        });
        Action(profileActions, "Gewähltes Profil laden", () =>
        {
            var name = (string)profiles.SelectedItem;
            (meter.Settings.LayoutProfiles.GetValueOrDefault(name) ?? CustomerTools.Preset(name)).Apply(meter.Settings);
            Commit(); Sync(); Say($"Profil „{name}“ geladen.");
        });
        Action(profileActions, "Layout exportieren…", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Aetherion Layout (*.json)|*.json", FileName = "Aetherion-Layout.json" };
            if (dialog.ShowDialog(this) == true) { CustomerTools.ExportLayout(dialog.FileName, meter.Settings); Say("Layout exportiert. Keine Konto- oder Lizenzdaten enthalten."); }
        });
        Action(profileActions, "Layout importieren…", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Aetherion Layout (*.json)|*.json" };
            if (dialog.ShowDialog(this) == true) { CustomerTools.ImportLayout(dialog.FileName).Apply(meter.Settings); Commit(); Sync(); Say("Layout importiert."); }
        });
        Action(profileActions, "Nur Overlay-Darstellung zurücksetzen", () => { new LayoutProfile().Apply(meter.Settings); Commit(); Sync(); Say("Overlay-Darstellung zurückgesetzt. Profile bleiben erhalten."); });

        var setup = Page("Einrichtung");
        Heading(setup, "In drei Schritten startklar");
        Note(setup, "1 · Sprache wählen. 2 · Beispielvorschau ansehen und ein Layout wählen. 3 · AION2 starten und die Verbindung unter Hilfe prüfen. Die Vorschau verwendet ausdrücklich Beispieldaten.");
        var language = new ComboBox { ItemsSource = new[] { "Deutsch", "English", "Русский" }, SelectedIndex = UiText.Normalize(meter.Settings.Language) switch { "en" => 1, "ru" => 2, _ => 0 }, Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        setup.Children.Add(language);
        var preview = new Image { Stretch = Stretch.Uniform, MaxHeight = 330, Margin = new Thickness(0, 10, 0, 10) };
        Action(setup, "Beispielvorschau aus der App erzeugen", () =>
        {
            var path = Path.Combine(AppSettings.AppDataDir, "preview", "setup.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var p = LayoutProfile.From(meter.Settings);
            SampleRenderer.Render(path, meter.Settings.Language, new SampleRenderer.Options(Width: (int)p.Width, Height: (int)p.Height,
                RowSize: p.RowSize, Scale: 1, BackgroundOpacity: p.Opacity, Backdrop: "dark"));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
            preview.Source = bitmap; Say("Beispieldaten · keine Live-Messung und kein gespeicherter Kampf.");
        });
        setup.Children.Add(preview);
        foreach (var name in CustomerTools.ProfileNames)
            Action(setup, $"{name}-Layout verwenden", () => { CustomerTools.Preset(name).Apply(meter.Settings); Commit(); Sync(); Say($"{name}-Layout aktiv. Das Overlay lässt sich entsperrt an die gewünschte Position ziehen."); });
        Action(setup, "Einrichtung abschließen", () =>
        {
            var lang = new[] { "de", "en", "ru" }[Math.Max(0, language.SelectedIndex)];
            meter.Settings.SetupCompleted = true; Commit(); AppHost.Current.SetLanguage(lang); Say("Einrichtung gespeichert. Die Verbindung kannst du unter Hilfe prüfen."); _tabs.SelectedIndex = 3;
        });

        var analysis = Page("Kampfanalyse");
        Action(analysis, "Kampfbuch: Boss-Tagebuch, Training, Heiler & Discord-Karte", () => AppHost.Current.ShowJournal());
        Heading(analysis, "Deine letzten Kämpfe verstehen");
        Note(analysis, "Die Auswertung beruht auf empfangenen Ereignissen. Sekunden mit Schaden sind keine Messung deiner Eingaben. Fehlende Treffer, Bossphasen und außer Reichweite befindliche Spieler können das Bild beeinflussen.");
        _entries = meter.History.List(200).ToList();
        _fights.ItemsSource = _entries.Select(e => $"{e.StartedAt:dd.MM HH:mm} · {meter.DisplayTitle(e)}").ToArray();
        _fights.SelectionChanged += (_, _) => Guard(ShowAnalysis);
        analysis.Children.Add(_fights); analysis.Children.Add(_analysis);
        Action(analysis, "Kampfliste aktualisieren", () =>
        {
            _entries.Clear(); _entries.AddRange(meter.History.List(200));
            _fights.ItemsSource = _entries.Select(e => $"{e.StartedAt:dd.MM HH:mm} · {meter.DisplayTitle(e)}").ToArray();
            _fights.SelectedIndex = _entries.Count > 0 ? 0 : -1; ShowAnalysis();
        });
        _fights.SelectedIndex = _entries.Count > 0 ? 0 : -1;
        if (_entries.Count == 0) Note(_analysis, "Noch keine gespeicherten Kämpfe. Nach dem ersten gespeicherten Kampf erscheinen hier Aktivität, Todesanalyse und Rekorde.");
        Heading(analysis, "Buffs & Debuffs · Datenstatus");
        Note(analysis, "Nicht verfügbar: Der aktuelle AION2-Decoder liefert keine verlässlichen Buff-Beginn- und Ende-Ereignisse. Deshalb werden keine Laufzeiten oder fehlenden Buffs geschätzt. Schadensereignisse und Skill-Namen beweisen keine Buff-Wirkungsdauer.");

        var help = Page("Hilfe & Wiederherstellung"); help.Children.Add(_help);
        Action(help, "Verbindung erneut prüfen", Diagnostics);
        Action(help, "Erfassung neu starten", () => { meter.RestartCapture(); Diagnostics(); Say("Erfassung neu gestartet."); });
        var report = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 170, Background = Background, Foreground = Foreground, Margin = new Thickness(0, 12, 0, 6) };
        Action(help, "Support-Paket vorbereiten (Vorschau)", () => { report.Text = CustomerTools.SupportReport(meter); Say("Die Vorschau zeigt den vollständigen Diagnoseinhalt. Dazu kommt nur layout.json. Exportiert wird lokal; nichts wird versendet."); });
        help.Children.Add(report);
        Action(help, "Vorschau als Support-ZIP speichern…", () =>
        {
            if (string.IsNullOrWhiteSpace(report.Text)) { report.Text = CustomerTools.SupportReport(meter); Say("Bericht vorbereitet. Prüfe den Inhalt und klicke erneut zum Exportieren."); return; }
            var dialog = new SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = $"Aetherion-Support-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
            if (dialog.ShowDialog(this) == true)
            {
                // SaveFileDialog already confirms an overwrite. Build aside to avoid leaving an incomplete archive.
                var temp = dialog.FileName + ".tmp-" + Guid.NewGuid().ToString("N");
                try { CustomerTools.ExportSupport(temp, report.Text, meter.Settings); File.Move(temp, dialog.FileName, true); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                Say("Support-Paket lokal gespeichert.");
            }
        });
        Heading(help, "Updates mit Rückweg");
        Note(help, "Vor künftigen Updates werden die aktuelle App und die Einstellungen lokal gesichert. Die Wiederherstellung beendet Aetherion und ersetzt die App durch diesen Checkpoint. Der Rückweg ist erst nach dem ersten Update mit dieser Funktion verfügbar.");
        var recovery = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
        Guard(() => recovery.Text = UpdateRecovery.Description()); help.Children.Add(recovery);
        Action(help, "Vorherige App-Version wiederherstellen…", () =>
        {
            if (!UpdateRecovery.Available) { Say("Noch kein Update-Checkpoint vorhanden."); return; }
            if (MessageBox.Show(this, "Aetherion wird geschlossen. Die vorherige Version und ihre Einstellungen werden wiederhergestellt. Fortfahren?", "Aetherion", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            UpdateRecovery.Restore(); Application.Current.Shutdown();
        });
        Diagnostics();
        _diagnostics.Tick += (_, _) => { if (_tabs.SelectedIndex == 3) Guard(Diagnostics); };
        Loaded += (_, _) => _diagnostics.Start(); Closed += (_, _) => _diagnostics.Stop();
        if (firstRun) _tabs.SelectedIndex = 1;
    }

    internal void ShowPageForPreview(string? page) => _tabs.SelectedIndex = page switch { "setup" => 1, "analysis" => 2, "help" => 3, _ => 0 };

    private void ShowAnalysis()
    {
        _analysis.Children.Clear();
        if (_fights.SelectedIndex < 0 || _fights.SelectedIndex >= _entries.Count) return;
        var entry = _entries[_fights.SelectedIndex];
        var record = _meter.History.Load(entry.Path) ?? throw new InvalidDataException("Kampfdatei kann nicht gelesen werden.");
        var self = record.Details.FirstOrDefault(d => d.IsSelf);
        if (self is null) { Note(_analysis, "Der eigene Charakter wurde in diesem Kampf nicht identifiziert."); return; }
        var activity = FightInsights.Activity(self.PerSecond, record.Summary.CombatMs);
        Heading(_analysis, $"{Format.Compact(self.Dps)}/s · {Format.Clock(record.Summary.CombatMs)} · {(self.Quality.CritRate?.ToString("P1", UiText.Current.Culture) ?? "—")} kritische Treffer");
        Note(_analysis, $"Schaden in {activity.DamageSeconds} von {activity.ObservedSeconds} Sekunden ({activity.DamageUptime:P0}). Längste Folge ohne empfangenen eigenen Schaden: {activity.LongestQuietSeconds} s. Todesmeldungen: {self.Deaths?.Count ?? 0}.");
        if (record.Summary.MostDamageUnsent || record.Summary.Boss?.CountedFrom is not null)
            Note(_analysis, "Unvollständige Erfassung: Gruppenanteile und Vergleiche können eingeschränkt sein. Dieser Kampf zählt nicht als persönlicher Rekord.");
        Heading(_analysis, "Persönlicher Boss-Rekord");
        var best = _entries.Where(e => e.BossCode == entry.BossCode && e.SelfName == self.Name && e.SelfClass == self.Class)
            .Select(e => _meter.History.Load(e.Path)).OfType<FightRecord>()
            .Where(old => JournalInsights.EligibleBest(old) && FightInsights.ComparableKills(old, record))
            .Select(old => old.Details.First(d => d.IsSelf).Dps).DefaultIfEmpty(0).Max();
        Note(_analysis, best > 0 ? $"Bester vergleichbarer vollständiger Kill in den letzten 200 gespeicherten Kämpfen: {Format.Compact(best)}/s. Eigener Charakter, Klasse, Server und Boss müssen übereinstimmen. Ausrüstung und Bossvarianten können Unterschiede erklären."
            : "Noch kein vergleichbarer vollständiger Boss-Kill. Rekorde berücksichtigen keine Demo-Kämpfe oder teilweise erfassten Kämpfe.");
        Action(_analysis, "Mit vorherigem Versuch desselben Bosses vergleichen", () =>
        {
            var previous = _entries.FirstOrDefault(e => e.StartedAt < entry.StartedAt && e.BossCode != 0 && e.BossCode == entry.BossCode && e.SelfName == entry.SelfName && e.SelfClass == entry.SelfClass);
            if (previous is null || _meter.History.Load(previous.Path) is not { } old) { Say("Kein vorheriger passender Versuch vorhanden."); return; }
            new FightComparisonWindow(_meter, previous, old, entry, record) { Owner = this }.Show();
        });
        Heading(_analysis, "Todesanalyse · letzte 10 Sekunden");
        if (self.Deaths is not { Count: > 0 }) Note(_analysis, "Keine Todesmeldung empfangen. Das beweist nicht, dass kein Tod stattgefunden hat.");
        foreach (var death in self.Deaths ?? [])
        {
            Note(_analysis, $"Tod bei {Format.Clock(Math.Max(0, death.TimeMs - record.Summary.StartedAt.ToUnixTimeMilliseconds()))} · empfangener Schaden davor: {Format.Compact(death.RecentDamageTaken)}");
            if (death.Timeline is null) Note(_analysis, "Ältere Kampfdatei: keine Treffer-Zeitleiste gespeichert.");
            else foreach (var hit in death.Timeline)
                Note(_analysis, $"{(hit.TimeMs - death.TimeMs) / 1000.0:0.0} s · {(hit.Healing ? "+ Heilung" : "− Schaden")} {Format.Compact(hit.Amount)} · {_meter.Data.SkillName(hit.SkillCode)}");
        }
    }

    private void Diagnostics()
    {
        _help.Children.Clear(); Heading(_help, "Verbindung & nächste Schritte");
        var status = _meter.CaptureStatus;
        var game = GameProcessLocator.FindGameProcessIds().Length > 0;
        Note(_help, $"Spiel: {(game ? "erkannt" : "nicht erkannt")} · Erfassung: {status.State}\nSpielpakete: {status.Packets:N0} · Ereignisse: {status.Events:N0}");
        Note(_help, !game ? "AION2 starten und den Charakter einloggen. Danach erneut prüfen."
            : status.State == CaptureState.Error ? "In den Einstellungen den passenden Netzwerkadapter wählen. Prüfe, ob Npcap installiert ist; starte danach die Erfassung neu."
            : status.Packets == 0 ? "Noch keine Spielpakete: Adapter-Auswahl prüfen. Bei VPN oder mehreren Adaptern den tatsächlich verwendeten Adapter wählen."
            : status.Events == 0 ? "Pakete kommen an. Führe im Spiel einen Angriff aus. Bleiben Ereignisse aus, kann ein Spiel-Update eine Decoder-Anpassung erfordern."
            : string.IsNullOrWhiteSpace(_meter.Tracker.SelfName) ? "Kampfdaten kommen an. Für die Charakter-Erkennung einmal eine Ladezone wechseln."
            : "Spiel- und Kampfdaten kommen an. Das Meter ist bereit.");
    }

    private StackPanel Page(string title)
    {
        var stack = new StackPanel { Margin = new Thickness(20) };
        _tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        return stack;
    }
    private static void Heading(Panel p, string text) => p.Children.Add(new TextBlock { Text = text, Foreground = Brushes.WhiteSmoke, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) });
    private static void Note(Panel p, string text) => p.Children.Add(new TextBlock { Text = text, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 8) });
    private static CheckBox Check(Panel p, string text, bool value)
    {
        var check = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = value, Margin = new Thickness(0, 5, 0, 5), Foreground = Brushes.WhiteSmoke };
        p.Children.Add(check); return check;
    }
    private void Action(Panel p, string title, Action action)
    {
        var button = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 8, 5), Padding = new Thickness(12, 7, 12, 7), Style = (Style)Application.Current.FindResource("GhostButton") };
        button.Click += (_, _) => Guard(action); p.Children.Add(button);
    }
    private void Say(string text) => _feedback.Text = text;
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { Say("Aktion konnte nicht abgeschlossen werden: " + ex.Message); }
    }
    private void Apply()
    {
        _meter.Settings.AutoOverlayHeight = _auto.IsChecked == true;
        _meter.Settings.StreamerMode = _privacy.IsChecked == true;
        _meter.Settings.PowerSaving = _saving.IsChecked == true;
        _meter.Settings.HighContrast = _contrast.IsChecked == true;
        _meter.Settings.AccessibleColors = _colors.IsChecked == true;
        _meter.Settings.ShowPersonalRecords = _records.IsChecked == true;
        _meter.Settings.OverlayTextScale = _font.Value;
        Commit(); Say("Anzeige gespeichert und angewendet.");
    }
    private void Commit() { _meter.ApplySettings(); AppHost.Current.OnSettingsChanged(); }
    private void Sync()
    {
        var s = _meter.Settings; _auto.IsChecked = s.AutoOverlayHeight; _privacy.IsChecked = s.StreamerMode;
        _saving.IsChecked = s.PowerSaving; _contrast.IsChecked = s.HighContrast;
        _colors.IsChecked = s.AccessibleColors; _records.IsChecked = s.ShowPersonalRecords; _font.Value = s.OverlayTextScale;
    }
}
