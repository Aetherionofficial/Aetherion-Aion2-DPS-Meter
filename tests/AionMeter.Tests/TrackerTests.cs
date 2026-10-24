using AionMeter.Core.Events;
using AionMeter.Core.Tracking;

namespace AionMeter.Tests;

public sealed class TrackerTests
{
    private sealed class Observer(Action<GameEvent>? action = null) : ITracker
    {
        public List<GameEvent> Received { get; } = [];
        public void OnEvent(GameEvent gameEvent) { Received.Add(gameEvent); action?.Invoke(gameEvent); }
        public ITrackerSnapshot GetSnapshot() => new KillCounterSnapshot(new Dictionary<int, long>());
        public void Reset() => Received.Clear();
    }

    [Fact]
    public void Dispatcher_keeps_instances_and_order_and_unregisters()
    {
        var dispatcher = new TrackerDispatcher();
        var a = new Observer(); var b = new Observer();
        using var first = dispatcher.Register(a);
        var second = dispatcher.Register(b);
        GameEvent[] events = [new NpcSeenEvent(3, 500, 123), new DeathEvent(1, 500)];
        foreach (var gameEvent in events) dispatcher.Publish(gameEvent);
        Assert.Equal(events, a.Received); Assert.Equal(events, b.Received);
        Assert.Same(events[0], a.Received[0]);
        second.Dispose(); second.Dispose();
        dispatcher.Publish(new DeathEvent(4, 501));
        Assert.Equal(3, a.Received.Count); Assert.Equal(2, b.Received.Count);
    }

    [Fact]
    public void Concurrent_publishers_deliver_exactly_once_in_the_same_order_to_every_tracker()
    {
        var dispatcher = new TrackerDispatcher();
        var a = new Observer(); var b = new Observer();
        using var first = dispatcher.Register(a); using var second = dispatcher.Register(b);
        Parallel.For(0, 4000, i => dispatcher.Publish(new DeathEvent(i, (uint)i)));
        Assert.Equal(4000, a.Received.Count);
        Assert.Equal(a.Received, b.Received);
        Assert.Equal(4000, a.Received.Select(e => e.TimeMs).Distinct().Count());
    }

    [Fact]
    public void Reentrant_publication_waits_for_the_current_event_and_failures_are_isolated()
    {
        var errors = new List<Exception>();
        var dispatcher = new TrackerDispatcher(errors.Add);
        var a = new Observer(e => { if (e.TimeMs == 1) dispatcher.Publish(new DeathEvent(2, 500)); });
        var broken = new Observer(_ => throw new InvalidOperationException("Testfehler"));
        var b = new Observer();
        using var x = dispatcher.Register(a); using var y = dispatcher.Register(broken); using var z = dispatcher.Register(b);
        dispatcher.Publish(new DeathEvent(1, 500));
        Assert.Equal(new long[] { 1, 2 }, a.Received.Select(e => e.TimeMs));
        Assert.Equal(a.Received, b.Received); Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Kill_counter_deduplicates_deaths_and_accepts_a_new_spawn()
    {
        using var tracker = new KillCounter();
        tracker.OnEvent(new NpcSeenEvent(0, 500, 123));
        tracker.OnEvent(new DeathEvent(1, 500));
        tracker.OnEvent(new DeathEvent(2, 500)); // e.g. Death plus KillCredit, even with different timestamps.
        Assert.Equal(1, tracker.GetSnapshot().Counts[123]);
        var snapshot = tracker.GetSnapshot();
        tracker.OnEvent(new NpcSeenEvent(3, 500, 123));
        tracker.OnEvent(new DeathEvent(4, 500));
        Assert.Equal(2, tracker.GetSnapshot().Counts[123]);
        Assert.Equal(1, snapshot.Counts[123]); // An older snapshot never changes under the GUI.
    }

    [Fact]
    public void Already_dead_unknown_players_and_summons_are_not_counted()
    {
        using var tracker = new KillCounter();
        tracker.OnEvent(new NpcSeenEvent(0, 500, 123));
        tracker.OnEvent(new DeathEvent(1, 500, AlreadyDead: true));
        tracker.OnEvent(new DeathEvent(2, 500));
        tracker.OnEvent(new DeathEvent(3, 600));
        tracker.OnEvent(new NpcSeenEvent(4, 600, 123)); // Late identity cannot prove an observed monster death.
        tracker.OnEvent(new DeathEvent(5, 600));
        tracker.OnEvent(new PlayerSeenEvent(6, 700, "Spieler", 1304, GameClass.Cleric));
        tracker.OnEvent(new NpcSeenEvent(7, 700, 123));
        tracker.OnEvent(new DeathEvent(8, 700));
        tracker.OnEvent(new SummonSeenEvent(9, 800, 700, null, 123));
        tracker.OnEvent(new NpcSeenEvent(10, 800, 123));
        tracker.OnEvent(new DeathEvent(11, 800));
        Assert.Empty(tracker.GetSnapshot().Counts);
    }

    [Fact]
    public void Zone_change_discards_actor_ids_but_keeps_totals_and_teleport_keeps_deduplication()
    {
        using var tracker = new KillCounter();
        tracker.OnEvent(new ZoneChangedEvent(0, 1110, "Gebiet A", false));
        tracker.OnEvent(new NpcSeenEvent(1, 500, 123)); tracker.OnEvent(new DeathEvent(2, 500));
        tracker.OnEvent(new ZoneChangedEvent(3, 1110, "Gebiet A", false, IsTeleport: true));
        tracker.OnEvent(new DeathEvent(4, 500));
        Assert.Equal(1, tracker.GetSnapshot().Counts[123]);
        tracker.OnEvent(new ZoneChangedEvent(5, 1120, "Gebiet B", false));
        tracker.OnEvent(new DeathEvent(6, 500));
        Assert.Equal(1, tracker.GetSnapshot().Counts[123]);
    }

    [Fact]
    public void Counter_is_thread_safe_while_snapshots_are_read()
    {
        using var tracker = new KillCounter();
        Parallel.For(1, 2001, id =>
        {
            tracker.OnEvent(new NpcSeenEvent(id, (uint)id, 123));
            tracker.OnEvent(new DeathEvent(id, (uint)id));
            tracker.OnEvent(new DeathEvent(id, (uint)id));
            _ = tracker.GetSnapshot().Counts.Values.Sum();
        });
        Assert.Equal(2000, tracker.GetSnapshot().Counts[123]);
    }

    [Fact]
    public async Task Changed_counts_are_automatically_saved_by_the_background_worker()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Aion-autosave-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "counts.json");
        try
        {
            using (var tracker = new KillCounter(path))
            {
                tracker.OnEvent(new NpcSeenEvent(0, 500, 123)); tracker.OnEvent(new DeathEvent(1, 500));
                var until = DateTime.UtcNow.AddSeconds(10);
                while (!File.Exists(path) && DateTime.UtcNow < until) await Task.Delay(50);
                Assert.True(File.Exists(path));
                using var loaded = new KillCounter(path, autoSave: false);
                Assert.Equal(1, loaded.GetSnapshot().Counts[123]);
            }
        }
        finally
        {
            File.Delete(path); File.Delete(path + ".tmp");
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
    }

    [Fact]
    public void Counts_are_saved_reloaded_reset_and_corrupt_files_are_preserved()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Aion-tracker-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "counts.json");
        Directory.CreateDirectory(folder);
        try
        {
            using (var tracker = new KillCounter(path, autoSave: false))
            {
                tracker.OnEvent(new NpcSeenEvent(0, 500, 123)); tracker.OnEvent(new DeathEvent(1, 500));
                tracker.Save();
            }
            using (var reloaded = new KillCounter(path, autoSave: false))
            {
                Assert.Equal(1, reloaded.GetSnapshot().Counts[123]);
                reloaded.Reset(); reloaded.Save();
            }
            using (var empty = new KillCounter(path, autoSave: false)) Assert.Empty(empty.GetSnapshot().Counts);
            File.WriteAllText(path, "{beschädigt");
            using (var corrupt = new KillCounter(path, autoSave: false))
            {
                Assert.NotNull(corrupt.PersistenceError);
                corrupt.Reset(); corrupt.Save();
            }
            Assert.Equal("{beschädigt", File.ReadAllText(path));
        }
        finally
        {
            // All paths are explicit generated test files inside our unique temporary folder.
            File.Delete(path); File.Delete(path + ".tmp"); Directory.Delete(folder);
        }
    }
}
