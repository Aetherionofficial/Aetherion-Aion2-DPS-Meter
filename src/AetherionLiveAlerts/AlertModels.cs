using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherionLiveAlerts;

public enum AlertEventType { Gift, Follow, Like, Share, Subscribe, Chat }

public sealed record LiveEvent(AlertEventType Type, string User, string Message = "", string Gift = "", int Quantity = 1, string Id = "", int Value = 0);

public sealed class AlertRule
{
    public string Name { get; set; } = "Neue Regel";
    public bool Enabled { get; set; } = true;
    public AlertEventType EventType { get; set; } = AlertEventType.Gift;
    public string MatchText { get; set; } = "";
    public int MinimumQuantity { get; set; } = 1;
    public string Template { get; set; } = "{user} hat {quantity}× {gift} gesendet!";
}

public sealed class AppConfig
{
    public bool AlertsEnabled { get; set; }
    public bool SpeechEnabled { get; set; }
    public string Username { get; set; } = "";
    public string WebhookPrefix { get; set; } = "http://localhost:39171/aetherion/";
    public string AlertSound { get; set; } = "Aetherion";
    public string CustomSoundPath { get; set; } = "";
    public int CooldownSeconds { get; set; } = 2;
    public bool OverlayEnabled { get; set; } = true;
    public int OverlayPort { get; set; } = 39172;
    public string GiftGoalName { get; set; } = "Geschenkziel";
    public int GiftGoalTarget { get; set; } = 100;
    public int GiftGoalCurrent { get; set; }
    public List<AlertRule> Rules { get; set; } = [];
    [JsonIgnore] public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AetherionLiveAlerts");
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static AppConfig Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (JsonException) { return new(); }
    }
    public void Save() { Directory.CreateDirectory(DirectoryPath); File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
}

public sealed class AlertEngine(IEnumerable<AlertRule> rules, int cooldownSeconds = 0)
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<AlertRule> _rules = rules.Where(r => r is not null).ToArray();
    private readonly int _cooldownSeconds = Math.Clamp(cooldownSeconds, 0, 300);
    public IEnumerable<(AlertRule Rule, string Message)> Match(LiveEvent e)
    {
        if (!string.IsNullOrWhiteSpace(e.Id) && !_seen.Add(e.Id)) yield break;
        foreach (var rule in _rules)
        {
            if (!rule.Enabled || rule.EventType != e.Type || e.Quantity < Math.Max(1, rule.MinimumQuantity)) continue;
            var haystack = $"{e.Gift} {e.User} {e.Message}";
            if (!string.IsNullOrWhiteSpace(rule.MatchText) && !haystack.Contains(rule.MatchText, StringComparison.OrdinalIgnoreCase)) continue;
            var key = $"{rule.Name}|{e.Type}|{e.User}|{e.Gift}";
            if (_cooldownSeconds > 0 && _cooldowns.TryGetValue(key, out var last) && DateTimeOffset.UtcNow - last < TimeSpan.FromSeconds(_cooldownSeconds)) continue;
            _cooldowns[key] = DateTimeOffset.UtcNow;
            yield return (rule, Render(rule.Template, e));
        }
    }
    private static string Render(string template, LiveEvent e) => (string.IsNullOrWhiteSpace(template) ? "{user} hat eine Aktion ausgelöst." : template)
        .Replace("{user}", e.User).Replace("{message}", e.Message).Replace("{gift}", e.Gift).Replace("{quantity}", e.Quantity.ToString());
}
