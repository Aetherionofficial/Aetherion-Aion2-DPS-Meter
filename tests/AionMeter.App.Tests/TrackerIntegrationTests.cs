using System.IO;
using AionMeter.App.Services;
using AionMeter.Core.Capture;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;
using AionMeter.Core.Tracking;

namespace AionMeter.App.Tests;

public sealed class TrackerIntegrationTests
{
    [Fact]
    public async Task Invalid_rules_are_reported_and_can_be_repaired_without_changing_combat()
    {
        using var meter = new MeterService(new AppSettings { Transient = true, DownloadIcons = false }, null);
        var original = File.ReadAllBytes(meter.EventConfigPath);
        try
        {
            var last = meter.Tracker.LastEventMs;
            File.WriteAllText(meter.EventConfigPath, "{\"events\":[null]}");
            Assert.False(meter.ReloadEventConfiguration()); Assert.NotNull(meter.EventConfigError);
            Assert.Equal(last, meter.Tracker.LastEventMs);
            File.WriteAllBytes(meter.EventConfigPath, original);
            Assert.True(meter.ReloadEventConfiguration()); Assert.Null(meter.EventConfigError);
            Assert.False(HotkeyManager.TryParse("+++", out _, out _));
            Assert.True(HotkeyManager.TryParse("Ctrl+Shift+M", out _, out _));
            Assert.Null(await meter.AddRecordingMarkerAsync("cube_geöffnet"));
        }
        finally { File.WriteAllBytes(meter.EventConfigPath, original); }
    }

    [Fact]
    public void Embedded_data_updates_preserve_the_user_owned_id_map()
    {
        var directory = MeterService.DataDirectory; // TestEnvironment redirects this to the isolated test directory.
        var path = Path.Combine(directory, "ids.json");
        var marker = Path.Combine(directory, ".embedded-data-version");
        var config = Path.Combine(directory, "events.json");
        var originalConfig = File.ReadAllBytes(config);
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var originalMarker = File.ReadAllBytes(marker);
        try
        {
            const string ownMapping = "{\"123\":\"Mein Monster\"}";
            File.WriteAllText(path, ownMapping);
            const string ownRules = "{\"events\":[]}";
            File.WriteAllText(config, ownRules);
            File.WriteAllText(marker, "Version-für-Test");
            var install = typeof(MeterService).Assembly.GetType("AionMeter.App.Services.EmbeddedData")!.GetMethod("Install")!;
            install.Invoke(null, null);
            Assert.Equal(ownMapping, File.ReadAllText(path));
            Assert.Equal(ownRules, File.ReadAllText(config));
            File.Delete(config);
            File.Delete(path);
            install.Invoke(null, null); // Same-version marker must still create a missing empty ID map.
            Assert.Equal("{}", File.ReadAllText(path));
            Assert.Contains("HiddenCubeOpened", File.ReadAllText(config));
        }
        finally
        {
            if (original is null) File.Delete(path); else File.WriteAllBytes(path, original);
            File.WriteAllBytes(marker, originalMarker);
            File.WriteAllBytes(config, originalConfig);
        }
    }

    private sealed class Source : IEventSource
    {
        public event Action<GameEvent>? EventDecoded;
        public event Action<CaptureStatus>? StatusChanged { add { } remove { } }
        public CaptureStatus Status => new(CaptureState.Capturing, "Testquelle");
        public void Emit(GameEvent gameEvent) => EventDecoded?.Invoke(gameEvent);
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class Observer(MeterService meter) : ITracker
    {
        public List<GameEvent> Events { get; } = [];
        public void OnEvent(GameEvent gameEvent)
        {
            Assert.True(meter.Tracker.LastEventMs >= gameEvent.TimeMs);
            Events.Add(gameEvent);
        }
        public ITrackerSnapshot GetSnapshot() => new KillCounterSnapshot(new Dictionary<int, long>());
        public void Reset() => Events.Clear();
    }

    [Fact]
    public void Capture_keeps_original_combat_results_and_delivers_the_same_events_after_combat_processing()
    {
        var source = new Source();
        using var meter = new MeterService(new AppSettings
            { Transient = true, SaveHistory = false, DownloadIcons = false, TargetMode = TargetMode.All }, _ => source);
        var baseline = new CombatTracker(meter.Data, new MeterOptions { TargetMode = TargetMode.All });
        var observer = new Observer(meter);
        using var subscription = meter.Trackers.Register(observer);
        meter.StartCapture(); meter.StartCapture(); // Must not add duplicate subscriptions.
        GameEvent[] events =
        [
            new ZoneChangedEvent(0, 1110, "Testgebiet", false),
            new SelfIdentifiedEvent(0, 100, "Testspieler", 1304, GameClass.Cleric),
            new NpcSeenEvent(0, 500, 991122, 1000),
            new DamageEvent(1000, 100, 500, 17010000, 50, HitFlags.None),
            new DamageEvent(2000, 100, 500, 17010000, 70, HitFlags.Critical),
            new DeathEvent(2500, 500), new DeathEvent(2501, 500),
        ];
        foreach (var gameEvent in events) { baseline.Process(gameEvent); source.Emit(gameEvent); }
        Assert.Equal(events, observer.Events.Where(e => e is not MonsterDeathObservedEvent));
        var expected = baseline.Snapshot(null, 3000)!;
        var actual = meter.Tracker.Snapshot(null, 3000)!;
        Assert.Equal(expected.TotalDamage, actual.TotalDamage);
        Assert.Equal(expected.PartyDps, actual.PartyDps);
        Assert.Equal(expected.CombatMs, actual.CombatMs);
        Assert.Equal(expected.Combatants, actual.Combatants);
        Assert.Equal(1, meter.KillCounter.GetSnapshot().Counts[991122]);
        Assert.Equal(1, meter.EventCounters.GetSnapshot().Counts["MonsterTodBeobachtet"]);
        var lastCombatEvent = meter.Tracker.LastEventMs;
        var configured = new AionMeter.Core.Protocol.ConfiguredEventDecoder(new([
            new("SoulGained", true, "ABCD", [new("id", 0, "u8")])]));
        subscription.Dispose();
        meter.Trackers.Publish(Assert.Single(configured.Decode(0xABCD, [42], lastCombatEvent + 60000, 1110, "Testgebiet")));
        Assert.Equal(lastCombatEvent, meter.Tracker.LastEventMs);
        Assert.Equal(1, meter.EventCounters.GetSnapshot().Counts["SoulGained"]);
        Assert.Contains(meter.EventLog.GetSnapshot().Events, e => e.Type == "SoulGained");
        meter.Tracker.Reset(); // Resetting the DPS view is not a reset of the independent persistent tracker.
        Assert.Equal(1, meter.KillCounter.GetSnapshot().Counts[991122]);
    }
}
