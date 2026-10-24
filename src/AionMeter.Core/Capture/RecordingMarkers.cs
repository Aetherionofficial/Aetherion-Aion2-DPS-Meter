using System.Text.Json;

namespace AionMeter.Core.Capture;

public sealed record RecordingMarker(long TimeMs, string Label, int? MapId = null, string? Zone = null);

public sealed class RecordingMarkers
{
    private readonly object _gate = new();
    public static string SidecarPath(string pcapPath) => Path.GetFullPath(pcapPath) + ".markers.jsonl";
    public void Append(string pcapPath, RecordingMarker marker)
    {
        Validate(marker);
        lock (_gate) File.AppendAllText(SidecarPath(pcapPath), JsonSerializer.Serialize(marker) + Environment.NewLine);
    }
    public static IReadOnlyList<RecordingMarker> Load(string path)
    {
        if (new FileInfo(path).Length > 4_194_304) throw new InvalidDataException("Die Markerdatei ist zu groß.");
        var result = new List<RecordingMarker>();
        foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var marker = JsonSerializer.Deserialize<RecordingMarker>(line) ?? throw new InvalidDataException("Ungültiger Marker.");
            Validate(marker); result.Add(marker);
            if (result.Count > 10000) throw new InvalidDataException("Maximal 10000 Marker sind erlaubt.");
        }
        return result;
    }
    private static void Validate(RecordingMarker marker)
    {
        if (string.IsNullOrWhiteSpace(marker.Label) || marker.Label.Length > 120 || marker.Label.Any(char.IsControl) ||
            marker.TimeMs < 0 || marker.TimeMs > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            throw new InvalidDataException("Marker benötigen ein Label mit 1–120 Zeichen und einen gültigen Zeitstempel.");
    }
}
