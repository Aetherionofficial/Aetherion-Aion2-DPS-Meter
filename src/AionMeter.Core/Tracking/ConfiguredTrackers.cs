using System.Collections.ObjectModel;
using System.Text.Json;
using AionMeter.Core.Events;
using AionMeter.Core.Protocol;

namespace AionMeter.Core.Tracking;

/// <summary>Observed death, never proof of personal kill credit or a drop.</summary>
public sealed record MonsterDeathObservedEvent(long TimeMs, uint ActorId, int NpcCode, int? MapId, string? Zone) : GameEvent(TimeMs);
public sealed record TrackedEvent(long Sequence, long TimeMs, string Type, string? Zone, string Details, bool Notify, bool Sound);
public sealed record EventLogSnapshot(long Version, IReadOnlyList<TrackedEvent> Events) : ITrackerSnapshot;
public sealed record CounterSnapshot(long Version, IReadOnlyDictionary<string, long> Counts) : ITrackerSnapshot;

public sealed class EventLogTracker : ITracker
{
    private readonly object _gate = new();
    private readonly List<TrackedEvent> _events = [];
    private readonly int _limit;
    private long _version;
    public EventLogTracker(int limit = 2000) => _limit = Math.Clamp(limit, 1, 100_000);
    public void OnEvent(GameEvent gameEvent)
    {
        var entry = Describe(gameEvent);
        if (entry is null) return;
        lock (_gate)
        {
            _events.Add(entry with { Sequence = ++_version });
            if (_events.Count > _limit) _events.RemoveRange(0, _events.Count - _limit);
        }
    }
    internal static TrackedEvent? Describe(GameEvent gameEvent) => gameEvent switch
    {
        ConfiguredGameEvent e => new(0, e.TimeMs, e.EventName, e.Zone, Limit(JsonSerializer.Serialize(e.Fields)), e.Notify, e.Sound),
        MonsterDeathObservedEvent e => new(0, e.TimeMs, "MonsterTodBeobachtet", e.Zone, $"ActorId={e.ActorId}; NpcCode={e.NpcCode}; kein Nachweis für eigenen Kill/Drop", false, false),
        ZoneChangedEvent e => new(0, e.TimeMs, "ZoneChanged", e.ZoneName, $"MapId={e.MapId}; Teleport={e.IsTeleport}", false, false),
        _ => null,
    };
    private static string Limit(string text) => text.Length <= 8192 ? text : text[..8192] + " … (gekürzt)";
    public EventLogSnapshot GetSnapshot() { lock (_gate) return new(_version, Array.AsReadOnly(_events.ToArray())); }
    ITrackerSnapshot ITracker.GetSnapshot() => GetSnapshot();
    public void Reset() { lock (_gate) { _events.Clear(); _version++; } }
    public void Restore(EventLogSnapshot snapshot)
    {
        if (snapshot.Version < 0 || snapshot.Events is null || snapshot.Events.Any(e => e is null || e.Sequence < 0 || e.TimeMs < 0 ||
            e.TimeMs > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() || e.Details is null || string.IsNullOrWhiteSpace(e.Type)))
            throw new InvalidDataException("Ungültiger Ereignisverlauf.");
        lock (_gate)
        {
            _events.Clear(); _events.AddRange(snapshot.Events.TakeLast(_limit));
            _version = Math.Max(snapshot.Version, _events.Select(e => e.Sequence).DefaultIfEmpty().Max());
        }
    }
}

public sealed class CounterTracker : ITracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);
    private long _version;
    public void OnEvent(GameEvent gameEvent)
    {
        var name = gameEvent switch { ConfiguredGameEvent e => e.EventName, MonsterDeathObservedEvent => "MonsterTodBeobachtet", _ => null };
        if (name is null) return;
        lock (_gate) { _counts[name] = checked(_counts.GetValueOrDefault(name) + 1); _version++; }
    }
    public CounterSnapshot GetSnapshot() { lock (_gate) return new(_version, new ReadOnlyDictionary<string, long>(new Dictionary<string, long>(_counts))); }
    ITrackerSnapshot ITracker.GetSnapshot() => GetSnapshot();
    public void Reset() { lock (_gate) { _counts.Clear(); _version++; } }
    public void Restore(CounterSnapshot snapshot)
    {
        if (snapshot.Version < 0 || snapshot.Counts is null || snapshot.Counts.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)) throw new InvalidDataException("Ungültige Ereigniszähler.");
        lock (_gate) { _counts.Clear(); foreach (var pair in snapshot.Counts) _counts.Add(pair.Key, pair.Value); _version = snapshot.Version; }
    }
}

/// <summary>Snapshot persistence on a worker thread; no GUI or capture-thread file I/O.</summary>
public sealed class TrackerStateFile<T> : IDisposable where T : class
{
    private readonly Func<T> _snapshot;
    private readonly Func<T, long> _version;
    private readonly string _path;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private long _saved = -1;
    private Exception? _error;
    private int _disposed;
    public Exception? Error => Volatile.Read(ref _error);
    public TrackerStateFile(string path, Func<T> snapshot, Func<T, long> version)
    {
        _path = path; _snapshot = snapshot; _version = version;
        _worker = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            try { while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false)) Save(); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }
    private void Save()
    {
        var state = _snapshot(); var version = _version(state);
        if (_saved == version) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(state));
            File.Move(_path + ".tmp", _path, true); _saved = version; Volatile.Write(ref _error, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Volatile.Write(ref _error, error); }
    }
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; _stop.Cancel(); _worker.GetAwaiter().GetResult(); Save(); _stop.Dispose(); }
}
