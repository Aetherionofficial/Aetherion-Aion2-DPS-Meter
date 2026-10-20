using System.Collections.ObjectModel;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace AetherionLiveAlerts;

public partial class MainWindow : Window
{
    private readonly AppConfig _config = AppConfig.Load(); private readonly List<AlertRule> _rules; private readonly ObservableCollection<string> _history = []; private AlertEngine _engine; private WebhookReceiver? _receiver; private OverlayServer? _overlay;
    public MainWindow()
    {
        InitializeComponent(); _rules = _config.Rules; _engine = new AlertEngine(_rules, _config.CooldownSeconds); Enabled.IsChecked = _config.AlertsEnabled; Speech.IsChecked = _config.SpeechEnabled; Username.Text = _config.Username; Webhook.Text = _config.WebhookPrefix; CooldownBox.Text = _config.CooldownSeconds.ToString(); GoalName.Text = _config.GiftGoalName; GoalTarget.Text = _config.GiftGoalTarget.ToString(); GoalCurrent.Text = _config.GiftGoalCurrent.ToString(); OverlayEnabled.IsChecked = _config.OverlayEnabled; SoundBox.SelectedIndex = Math.Max(0, _config.AlertSound switch { "System" => 1, "Stumm" => 2, _ => 0 }); OverlayUrl.Text = $"http://localhost:{_config.OverlayPort}/overlay"; TypeBox.ItemsSource = Enum.GetValues<AlertEventType>(); Rules.ItemsSource = _rules; History.ItemsSource = _history; Rules.SelectionChanged += Rules_SelectionChanged; Footer.Text = $"{_rules.Count} Regel(n) · Eigene App · Daten lokal gespeichert"; Loaded += (_, _) => { StartReceiver(); StartOverlay(); }; Closed += (_, _) => StopServices();
    }
    private void Add_Click(object sender, RoutedEventArgs e) { _rules.Add(new AlertRule { Name = "Neue Alert-Regel" }); Rules.Items.Refresh(); Rules.SelectedIndex = _rules.Count - 1; Footer.Text = $"{_rules.Count} Regel(n) · Ungespeicherte Änderungen"; }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (Rules.SelectedItem is AlertRule rule) { _rules.Remove(rule); Rules.Items.Refresh(); Footer.Text = $"{_rules.Count} Regel(n) · Ungespeicherte Änderungen"; } }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.AlertsEnabled = Enabled.IsChecked == true; _config.SpeechEnabled = Speech.IsChecked == true; _config.Username = Username.Text.Trim(); _config.WebhookPrefix = Webhook.Text.Trim(); _config.AlertSound = (SoundBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Aetherion"; _config.CooldownSeconds = Int(CooldownBox.Text, 0, 300); _config.GiftGoalName = GoalName.Text.Trim(); _config.GiftGoalTarget = Int(GoalTarget.Text, 1, 1_000_000); _config.GiftGoalCurrent = Int(GoalCurrent.Text, 0, 1_000_000); _config.OverlayEnabled = OverlayEnabled.IsChecked == true; _config.Save(); _engine = new AlertEngine(_rules, _config.CooldownSeconds); Status.Text = "Gespeichert. Alerts, Geschenkziel und Overlay sind bereit."; StartReceiver(); StartOverlay();
    }
    private void Rules_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (Rules.SelectedItem is not AlertRule r) return; TypeBox.SelectedItem = r.EventType; QuantityBox.Text = r.MinimumQuantity.ToString(); MatchBox.Text = r.MatchText; TemplateBox.Text = r.Template; RuleEnabled.IsChecked = r.Enabled; }
    private void SaveRule_Click(object sender, RoutedEventArgs e) { if (Rules.SelectedItem is not AlertRule r) { Status.Text = "Bitte zuerst eine Regel auswählen."; return; } r.EventType = TypeBox.SelectedItem is AlertEventType t ? t : AlertEventType.Gift; r.MinimumQuantity = Int(QuantityBox.Text, 1, 1_000_000); r.MatchText = MatchBox.Text.Trim(); r.Template = string.IsNullOrWhiteSpace(TemplateBox.Text) ? "{user} hat eine Aktion ausgelöst." : TemplateBox.Text; r.Enabled = RuleEnabled.IsChecked == true; if (string.IsNullOrWhiteSpace(r.Name)) r.Name = $"{r.EventType}-Alert"; Rules.Items.Refresh(); Status.Text = "Regel aktualisiert. Jetzt Einstellungen speichern."; }
    private void RuleField_Changed(object sender, RoutedEventArgs e) { }
    private void RuleField_Changed(object sender, TextChangedEventArgs e) { }
    private void Test_Click(object sender, RoutedEventArgs e) { var rule = Rules.SelectedItem as AlertRule ?? _rules.FirstOrDefault(); if (rule is null) { Status.Text = "Bitte zuerst eine Regel hinzufügen."; return; } HandleEvent(new LiveEvent(rule.EventType, "Demo-Zuschauer", "Hallo Aetherion", string.IsNullOrWhiteSpace(rule.MatchText) ? "Rose" : rule.MatchText, Math.Max(1, rule.MinimumQuantity), Guid.NewGuid().ToString("N"))); }
    private void StartReceiver()
    {
        if (_receiver is not null || !_config.AlertsEnabled || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AETHERION_TIKTOK_CLIENT_SECRET")) || string.IsNullOrWhiteSpace(_config.WebhookPrefix)) return;
        try { _receiver = new WebhookReceiver(Environment.GetEnvironmentVariable("AETHERION_TIKTOK_CLIENT_SECRET")!); _receiver.EventReceived += (_, e) => Dispatcher.BeginInvoke(() => HandleEvent(e)); _receiver.Start(_config.WebhookPrefix); Status.Text = "Live-Alerts aktiv · Webhook wartet auf Events"; } catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or UnauthorizedAccessException) { Status.Text = "Webhook konnte nicht gestartet werden: " + ex.Message; _receiver = null; }
    }
    private void StartOverlay()
    {
        if (_overlay is not null || !_config.OverlayEnabled) return;
        try { _overlay = new OverlayServer(BuildOverlay); _overlay.Start(_config.OverlayPort); OverlayUrl.Text = $"http://localhost:{_config.OverlayPort}/overlay"; } catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException or UnauthorizedAccessException) { Status.Text = "OBS-Overlay konnte nicht gestartet werden: " + ex.Message; _overlay = null; }
    }
    private void HandleEvent(LiveEvent e)
    {
        if (e.Type == AlertEventType.Gift) { _config.GiftGoalCurrent = Math.Min(1_000_000, _config.GiftGoalCurrent + e.Quantity); GoalCurrent.Text = _config.GiftGoalCurrent.ToString(); if (_config.GiftGoalTarget > 0 && _config.GiftGoalCurrent >= _config.GiftGoalTarget) _history.Insert(0, "🎉 Geschenkziel erreicht: " + _config.GiftGoalName); }
        _history.Insert(0, $"{DateTime.Now:HH:mm:ss} · {e.Type} · {e.User}{(string.IsNullOrWhiteSpace(e.Gift) ? "" : " · " + e.Gift)} ×{e.Quantity}"); while (_history.Count > 80) _history.RemoveAt(_history.Count - 1);
        foreach (var match in _engine.Match(e)) { PlaySound(); if (Speech.IsChecked == true) Speak(match.Message); new AlertToastWindow(e.Type.ToString(), match.Message).Show(); Status.Text = "Alert: " + match.Message; }
    }
    private void PlaySound() { if (_config.AlertSound == "Stumm") return; if (_config.AlertSound == "System") { SystemSounds.Asterisk.Play(); return; } Console.Beep(660, 100); Console.Beep(880, 180); }
    private static void Speak(string text) { try { var type = Type.GetTypeFromProgID("SAPI.SpVoice"); if (type is null) return; dynamic voice = Activator.CreateInstance(type)!; voice.Speak(text, 1); } catch (COMException) { } }
    private string BuildOverlay() { var sb = new StringBuilder("<!doctype html><meta charset='utf-8'><style>body{margin:0;background:transparent;color:#fff;font:16px Segoe UI}.card{width:420px;background:rgba(17,16,19,.9);border:2px solid #f06a22;border-radius:12px;padding:16px}.title{color:#f06a22;font-weight:700;font-size:18px}.item{padding:7px 0;border-bottom:1px solid #4a3430}</style><div class='card'><div class='title'>AETHERION LIVE</div>"); foreach (var h in _history.Take(5)) sb.Append($"<div class='item'>{System.Net.WebUtility.HtmlEncode(h)}</div>"); return sb.Append("</div>").ToString(); }
    private static int Int(string value, int min, int max) => int.TryParse(value, out var n) ? Math.Clamp(n, min, max) : min;
    private void StopServices() { _receiver?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _overlay?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
}
