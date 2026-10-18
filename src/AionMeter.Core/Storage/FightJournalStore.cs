using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;

namespace AionMeter.Core.Storage;

public sealed record FightAnnotation(bool Favorite = false, string Note = "");
public sealed record TrainingScope(int BossCode, string Zone, string Character, GameClass Class, int ServerId)
{
    public string Key() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { BossCode, Zone, Character, Class = (int)Class, ServerId }))));

    public static TrainingScope? For(FightRecord record)
    {
        var self = record.Details.FirstOrDefault(d => d.IsSelf);
        var healer = record.Healers?.FirstOrDefault(h => h.IsSelf);
        var name = self?.Name ?? healer?.Name;
        var playerClass = self?.Class ?? healer?.Class ?? GameClass.Unknown;
        var server = self?.ServerId ?? healer?.ServerId ?? 0;
        return record.Summary.Boss is { NpcCode: > 0 } boss && !string.IsNullOrWhiteSpace(record.Summary.Zone)
            && playerClass != GameClass.Unknown && server > 0 && !string.IsNullOrWhiteSpace(name) && !name.StartsWith('#')
            ? new(boss.NpcCode, record.Summary.Zone, name, playerClass, server) : null;
    }
}

/// <summary>Separate atomic metadata file: never rewrites original fight captures or the history index.</summary>
public sealed class FightJournalStore
{
    private sealed record Document(int Version, Dictionary<string, FightAnnotation> Fights, Dictionary<string, double> Goals);
    private readonly object _gate = new();
    private readonly string? _path;
    private Document _data = new(1, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
    public string? LoadError { get; private set; }

    public FightJournalStore(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (data is null || data.Version != 1 || data.Fights is null || data.Goals is null
                || data.Fights.Any(p => !ValidFile(p.Key) || p.Value is null || p.Value.Note is null || p.Value.Note.Length > 2000)
                || data.Goals.Any(p => p.Key.Length != 64 || !ValidTarget(p.Value)))
                throw new JsonException("Ungültige Tagebuchdatei.");
            _data = data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { LoadError = "Tagebuch-Metadaten konnten nicht geladen werden. Die Originaldatei bleibt geschützt; Änderungen sind deaktiviert."; }
    }

    public FightAnnotation Annotation(string fileName) { lock (_gate) return _data.Fights.GetValueOrDefault(fileName) ?? new(); }
    public double? Target(TrainingScope? scope)
    {
        if (scope is null) return null;
        lock (_gate) return _data.Goals.TryGetValue(scope.Key(), out var value) ? value : null;
    }

    public void Annotate(string fileName, bool favorite, string note)
    {
        if (!ValidFile(fileName) || note is null || note.Length > 2000) throw new ArgumentException("Notiz: maximal 2.000 Zeichen.");
        lock (_gate)
        {
            var fights = new Dictionary<string, FightAnnotation>(_data.Fights, StringComparer.Ordinal) { [fileName] = new(favorite, note) };
            Commit(_data with { Fights = fights });
        }
    }

    public void SetTarget(TrainingScope scope, double? target)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (target is { } number && !ValidTarget(number)) throw new ArgumentOutOfRangeException(nameof(target), "DPS-Ziel: 1 bis 1.000.000.000.");
        lock (_gate)
        {
            var goals = new Dictionary<string, double>(_data.Goals, StringComparer.Ordinal);
            if (target is null) goals.Remove(scope.Key()); else goals[scope.Key()] = target.Value;
            Commit(_data with { Goals = goals });
        }
    }

    public void Edit(string fileName, bool favorite, string note, TrainingScope? scope, double? target)
    {
        if (!ValidFile(fileName) || note is null || note.Length > 2000) throw new ArgumentException("Notiz: maximal 2.000 Zeichen.");
        if (target is { } number && !ValidTarget(number)) throw new ArgumentOutOfRangeException(nameof(target));
        lock (_gate)
        {
            var fights = new Dictionary<string, FightAnnotation>(_data.Fights, StringComparer.Ordinal) { [fileName] = new(favorite, note) };
            var goals = new Dictionary<string, double>(_data.Goals, StringComparer.Ordinal);
            if (scope is not null) { if (target is null) goals.Remove(scope.Key()); else goals[scope.Key()] = target.Value; }
            Commit(_data with { Fights = fights, Goals = goals });
        }
    }

    private void Commit(Document next)
    {
        if (LoadError is not null) throw new IOException(LoadError);
        if (_path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temp = _path + ".tmp-" + Guid.NewGuid().ToString("N");
            try { File.WriteAllText(temp, JsonSerializer.Serialize(next)); File.Move(temp, _path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        _data = next;
    }

    private static bool ValidTarget(double value) => double.IsFinite(value) && value >= 1 && value <= 1_000_000_000;
    private static bool ValidFile(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 255
        && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(['/', '\\']) < 0;
}
