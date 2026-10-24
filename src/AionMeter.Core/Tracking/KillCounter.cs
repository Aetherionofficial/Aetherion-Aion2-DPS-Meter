using System.Collections.ObjectModel;
using System.Text.Json;
using AionMeter.Core.Events;

namespace AionMeter.Core.Tracking;

public sealed record KillCounterSnapshot(IReadOnlyDictionary<int, long> Counts) : ITrackerSnapshot;

/// <summary>Counts observed NPC deaths, NOT personal kills, kill credit, loot or soul acquisition.
/// Only previously announced NPC template IDs are counted. Missing spawn packets cannot be reconstructed safely.</summary>
public sealed class KillCounter : ITracker, IDisposable
{
    private sealed record Actor(int NpcCode, bool Dead);
    private readonly object _gate = new();
    private readonly object _saveGate = new();
    private readonly Dictionary<uint, Actor> _actors = [];
    private readonly HashSet<uint> _nonMonsters = [];
    private readonly Dictionary<int, long> _counts = [];
    private readonly string? _path;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task? _saveLoop;
    private int? _map;
    private string? _zone;
    private readonly Action<MonsterDeathObservedEvent>? _observedDeath;
    private long _version;
    private long _savedVersion;
    private bool _loadFailed;
    private string? _persistenceError;
    private int _disposed;

    public string? PersistenceError { get { lock (_gate) return _persistenceError; } }

    public KillCounter(string? path = null, bool autoSave = true, Action<MonsterDeathObservedEvent>? observedDeath = null)
    {
        _observedDeath = observedDeath;
        _path = path is null ? null : Path.GetFullPath(path);
        if (_path is not null)
        {
            try
            {
                if (File.Exists(_path))
                {
                    var counts = JsonSerializer.Deserialize<Dictionary<int, long>>(File.ReadAllText(_path))
                        ?? throw new JsonException("Leerer Zählerstand.");
                    if (counts.Any(p => p.Key <= 0 || p.Value < 0)) throw new JsonException("Ungültiger Zählerstand.");
                    foreach (var pair in counts) _counts.Add(pair.Key, pair.Value);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                _loadFailed = true; // Preserve the original file for repair; never overwrite it with empty state.
                _persistenceError = $"KillCounter konnte nicht geladen werden: {error.Message}";
            }
            if (autoSave && !_loadFailed) _saveLoop = Task.Run(SaveLoop);
        }
    }

    public void OnEvent(GameEvent gameEvent)
    {
        MonsterDeathObservedEvent? observed = null;
        lock (_gate)
        {
            switch (gameEvent)
            {
                case ZoneChangedEvent zone when !zone.IsTeleport && zone.MapId != _map:
                    _map = zone.MapId; _zone = zone.ZoneName; _actors.Clear(); _nonMonsters.Clear();
                    break;
                case SelfIdentifiedEvent self: Exclude(self.ActorId); break;
                case PlayerSeenEvent player: Exclude(player.ActorId); break;
                case SummonSeenEvent summon: Exclude(summon.ActorId); break;
                case NpcSeenEvent npc when npc.ActorId != 0 && npc.NpcCode > 0 && !_nonMonsters.Contains(npc.ActorId):
                    // Identity after an unclassified death stays dead: do not turn its repeated death into a kill.
                    var lateIdentity = _actors.TryGetValue(npc.ActorId, out var previous) && previous.NpcCode == 0 && previous.Dead;
                    _actors[npc.ActorId] = new(npc.NpcCode, lateIdentity);
                    break;
                case DeathEvent death when death.ActorId != 0 && !_nonMonsters.Contains(death.ActorId):
                    var actor = _actors.GetValueOrDefault(death.ActorId) ?? new Actor(0, false);
                    _actors[death.ActorId] = actor with { Dead = true };
                    if (death.AlreadyDead || actor.Dead || actor.NpcCode <= 0) break;
                    _counts[actor.NpcCode] = checked(_counts.GetValueOrDefault(actor.NpcCode) + 1);
                    _version++;
                    observed = new(death.TimeMs, death.ActorId, actor.NpcCode, _map, _zone);
                    break;
            }
        }
        if (observed is not null) _observedDeath?.Invoke(observed);
    }

    private void Exclude(uint actorId) { _nonMonsters.Add(actorId); _actors.Remove(actorId); }

    public KillCounterSnapshot GetSnapshot()
    {
        lock (_gate) return new(new ReadOnlyDictionary<int, long>(new Dictionary<int, long>(_counts)));
    }
    ITrackerSnapshot ITracker.GetSnapshot() => GetSnapshot();

    public void Reset()
    {
        lock (_gate) { _counts.Clear(); _actors.Clear(); _nonMonsters.Clear(); _map = null; _zone = null; _version++; }
    }

    /// <summary>Disk I/O never runs in OnEvent. Auto-save runs on a worker; tests/tools can explicitly flush.</summary>
    public void Save()
    {
        if (_path is null || _loadFailed) return;
        lock (_saveGate)
        {
            Dictionary<int, long> copy;
            long version;
            lock (_gate)
            {
                if (_version == _savedVersion) return;
                copy = new(_counts); version = _version;
            }
            var temporary = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(temporary, JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, _path, overwrite: true);
                lock (_gate) { _savedVersion = version; _persistenceError = null; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                lock (_gate) _persistenceError = $"KillCounter konnte nicht gespeichert werden: {error.Message}";
            }
        }
    }

    private async Task SaveLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try { while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false)) Save(); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel(); _saveLoop?.GetAwaiter().GetResult(); Save(); _stop.Dispose();
    }
}
