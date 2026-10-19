using System.Text.Json;
using System.IO;

namespace AionMeter.App.Services;

internal sealed record BossAlertEntry(DateTimeOffset At, string Boss, string Message);

internal static class BossAlertHistory
{
    private static readonly string FilePath = Path.Combine(AppSettings.AppDataDir, "boss-alert-history.json");
    private static readonly object Gate = new();

    public static void Add(string boss, string message)
    {
        lock (Gate)
        {
            var items = Load().Take(19).ToList();
            items.Insert(0, new BossAlertEntry(DateTimeOffset.Now, boss, message));
            try
            {
                Directory.CreateDirectory(AppSettings.AppDataDir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(items));
            }
            catch (IOException) { }
        }
    }

    public static IReadOnlyList<BossAlertEntry> Load()
    {
        lock (Gate)
        {
            try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<List<BossAlertEntry>>(File.ReadAllText(FilePath)) ?? [] : []; }
            catch (Exception ex) when (ex is IOException or JsonException) { return []; }
        }
    }
}
