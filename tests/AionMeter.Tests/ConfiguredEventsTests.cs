using System.Text.Json;
using AionMeter.Core.Capture;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;
using AionMeter.Core.Tracking;

namespace AionMeter.Tests;

public sealed class ConfiguredEventsTests
{
    private static ConfiguredEventDecoder Decoder(params EventFieldDefinition[] fields) => new(new([
        new("SoulGained", true, "ABCD", fields, true, false)]));

    [Fact]
    public void Typed_fields_offsets_endianness_strings_and_matches_are_decoded()
    {
        var decoder = Decoder(new EventFieldDefinition("kind", 0, "u8", Match: JsonSerializer.SerializeToElement(7)),
            new("id", 1, "u32le"), new("amount", 5, "u16be"), new("name", 7, "utf8", 3), new("raw", 10, "bytes", 2),
            new("variable", 12, "varuint"));
        byte[] body = [7, 0x78, 0x56, 0x34, 0x12, 0, 9, 65, 66, 67, 0xAA, 0xFF, 0xAC, 2];
        var e = Assert.Single(decoder.Decode(0xABCD, body, 123, 1110, "Testgebiet"));
        Assert.Equal("SoulGained", e.EventName); Assert.Equal(123, e.TimeMs); Assert.Equal(1110, e.MapId);
        Assert.Equal(0x12345678u, e.Fields["id"].GetUInt32()); Assert.Equal(9, e.Fields["amount"].GetInt32());
        Assert.Equal("ABC", e.Fields["name"].GetString()); Assert.Equal("AAFF", e.Fields["raw"].GetString());
        Assert.Equal(300, e.Fields["variable"].GetInt32()); Assert.True(e.Notify); Assert.False(e.Sound);
        body[0] = 8; Assert.Empty(decoder.Decode(0xABCD, body, 0, null, null));
        Assert.Empty(decoder.Decode(0xABCE, body, 0, null, null));
    }

    [Fact]
    public void Truncated_invalid_utf8_boolean_and_nonfinite_numbers_are_rejected()
    {
        Assert.Empty(Decoder(new EventFieldDefinition("id", 1, "u32le")).Decode(0xABCD, [1, 2], 0, null, null));
        Assert.Empty(Decoder(new EventFieldDefinition("text", 0, "utf8", 2)).Decode(0xABCD, [0xC3, 0x28], 0, null, null));
        Assert.Empty(Decoder(new EventFieldDefinition("flag", 0, "bool")).Decode(0xABCD, [2], 0, null, null));
        Assert.Empty(Decoder(new EventFieldDefinition("number", 0, "f32le")).Decode(0xABCD, BitConverter.GetBytes(float.NaN), 0, null, null));
        Assert.Empty(Decoder(new EventFieldDefinition("v", 0, "varuint")).Decode(0xABCD, [0x80], 0, null, null));
    }

    [Fact]
    public void Rules_are_validated_and_disabled_templates_need_no_real_opcode()
    {
        var templates = new ConfiguredEventDecoder(new EventConfiguration([new("HiddenCubeOpened")]));
        Assert.Empty(templates.Decode(0xABCD, [0], 0, null, null));
        Assert.Throws<InvalidDataException>(() => new ConfiguredEventDecoder(new EventConfiguration([null!])));
        Assert.Throws<InvalidDataException>(() => Decoder(new EventFieldDefinition[] { null! }));
        Assert.Throws<InvalidDataException>(() => Decoder(new EventFieldDefinition("id", -1, "u32le")));
        Assert.Throws<InvalidDataException>(() => Decoder(new EventFieldDefinition("id", 0, "guessed")));
        Assert.Throws<InvalidDataException>(() => Decoder(new EventFieldDefinition("id", 0, "u8"), new("id", 1, "u8")));
    }

    [Fact]
    public void Uint64_match_does_not_lose_precision_and_rule_arrays_are_copied()
    {
        var field = new EventFieldDefinition("id", 0, "u64le", Match: JsonSerializer.SerializeToElement(9007199254740993UL));
        EventFieldDefinition[] fields = [field]; var decoder = Decoder(fields); fields[0] = new("wrong", 200, "u8");
        Assert.Single(decoder.Decode(0xABCD, BitConverter.GetBytes(9007199254740993UL), 0, null, null));
        Assert.Empty(decoder.Decode(0xABCD, BitConverter.GetBytes(9007199254740992UL), 0, null, null));
    }

    [Fact]
    public void Generic_events_never_enter_the_original_combat_sink_and_failed_observers_are_isolated()
    {
        var original = new List<GameEvent>(); var auxiliary = new List<ConfiguredGameEvent>(); var unknown = new List<UndecodedPacket>();
        var parser = new PacketParser(GameData.Empty, original.Add) { ConfiguredDecoder = Decoder(new EventFieldDefinition("id", 0, "u8")),
            ConfiguredEventDecoded = auxiliary.Add, Undecoded = unknown.Add, TimeMs = 42 };
        parser.Handle(0xABCD, [9]);
        Assert.Empty(original); Assert.Empty(unknown); Assert.Equal(9, Assert.Single(auxiliary).Fields["id"].GetInt32());
        parser.ConfiguredEventDecoded = _ => throw new InvalidOperationException("Fehler im Zusatzmodul");
        parser.Handle(0xABCD, [10]); Assert.Empty(original);
        parser.Handle(Opcodes.MapLoad, [0, 0, 0, 0, .. BitConverter.GetBytes(1110)]);
        Assert.IsType<ZoneChangedEvent>(Assert.Single(original));
    }

    [Fact]
    public void Event_log_is_bounded_and_snapshots_are_independent_from_resets()
    {
        var log = new EventLogTracker(2); var counter = new CounterTracker(); var bus = new TrackerDispatcher();
        using var a = bus.Register(log); using var b = bus.Register(counter);
        var decoder = Decoder(new EventFieldDefinition("id", 0, "u8"));
        for (byte i = 0; i < 3; i++) bus.Publish(Assert.Single(decoder.Decode(0xABCD, [i], i, 1, "Gebiet")));
        var saved = log.GetSnapshot(); Assert.Equal(2, saved.Events.Count); Assert.Equal(1, saved.Events[0].TimeMs);
        Assert.Equal(3, counter.GetSnapshot().Counts["SoulGained"]);
        log.Reset(); counter.Reset(); Assert.Empty(log.GetSnapshot().Events); Assert.Empty(counter.GetSnapshot().Counts);
        Assert.Equal(2, saved.Events.Count);
        var restored = new EventLogTracker(); restored.Restore(JsonSerializer.Deserialize<EventLogSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(saved.Events, restored.GetSnapshot().Events);
    }

    [Fact]
    public void Counter_is_thread_safe_and_snapshot_persistence_flushes_on_dispose()
    {
        var counter = new CounterTracker(); var e = Assert.Single(Decoder().Decode(0xABCD, [], 1, null, null));
        Parallel.For(0, 4000, _ => counter.OnEvent(e)); Assert.Equal(4000, counter.GetSnapshot().Counts["SoulGained"]);
        var directory = Path.Combine(Path.GetTempPath(), "Aion-events-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "counts.json");
        try
        {
            using (new TrackerStateFile<CounterSnapshot>(path, counter.GetSnapshot, s => s.Version)) { }
            var restored = new CounterTracker(); restored.Restore(JsonSerializer.Deserialize<CounterSnapshot>(File.ReadAllText(path))!);
            Assert.Equal(4000, restored.GetSnapshot().Counts["SoulGained"]);
            Assert.Throws<InvalidDataException>(() => restored.Restore(new(1, null!)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Derived_monster_deaths_are_counted_once_and_loaded_dead_monsters_are_excluded()
    {
        var bus = new TrackerDispatcher(); using var kills = new KillCounter(observedDeath: bus.Publish);
        var counts = new CounterTracker(); using var a = bus.Register(kills); using var b = bus.Register(counts);
        bus.Publish(new NpcSeenEvent(0, 100, 123, 1000)); bus.Publish(new DeathEvent(1, 100)); bus.Publish(new DeathEvent(2, 100));
        bus.Publish(new NpcSeenEvent(3, 200, 123, 1000)); bus.Publish(new DeathEvent(4, 200, AlreadyDead: true));
        Assert.Equal(1, counts.GetSnapshot().Counts["MonsterTodBeobachtet"]);
    }
}
