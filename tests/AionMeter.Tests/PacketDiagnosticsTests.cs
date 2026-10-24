using System.Text.Json;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Protocol;

namespace AionMeter.Tests;

public sealed class PacketDiagnosticsTests
{
    [Fact]
    public void Unknown_and_undecoded_known_messages_include_opcode_hex_time_and_zone()
    {
        var packets = new List<UndecodedPacket>();
        var events = new List<GameEvent>();
        var parser = new PacketParser(GameData.Empty, events.Add) { TimeMs = 42, Undecoded = packets.Add };
        parser.Handle(0xABCD, [0, 1, 255]);
        Assert.Equal(new UndecodedPacket(42, "ABCD", 3, "0001FF", null, null, "Unbekannter Opcode"), packets[0]);
        parser.Handle(Opcodes.MapLoad, [0, 0, 0, 0, .. BitConverter.GetBytes(1110)]);
        parser.Handle(Opcodes.Damage, []);
        var known = packets.Last();
        Assert.Equal("0438", known.Opcode); Assert.Equal(1110, known.MapId);
        Assert.Equal("Kein Ereignis dekodiert", known.Reason);
        var before = packets.Count;
        parser.Handle(Opcodes.Heartbeat, new byte[8]);
        Assert.Equal(before, packets.Count);
    }

    [Fact]
    public void Diagnostics_do_not_change_the_combat_events_even_when_the_observer_throws()
    {
        var baseline = new List<GameEvent>(); var observed = new List<GameEvent>();
        var plain = new PacketParser(GameData.Empty, baseline.Add) { TimeMs = 1000 };
        var diagnostic = new PacketParser(GameData.Empty, observed.Add)
            { TimeMs = 1000, Undecoded = _ => throw new IOException("Testfehler") };
        var damage = Wire.FromHex("b1ea011600f30a40c0f40063028000010b199b5f01000000ac52d007");
        foreach (var parser in new[] { plain, diagnostic })
        {
            parser.Handle(Opcodes.Damage, damage);
            parser.Handle(0xABCD, [1, 2]);
            parser.Handle(Opcodes.Cast, []);
        }
        Assert.Single(baseline); Assert.Equal(baseline, observed);
    }

    [Fact]
    public void Offline_logging_drains_every_record_and_contains_full_hex()
    {
        var path = Path.Combine(Path.GetTempPath(), "Aion-packet-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var log = new UndecodedPacketLog(path, waitForCapacity: true);
            using (log)
                for (var i = 0; i < 300; i++) log.Write(new(i, "ABCD", 3, "AABBCC", 1110, "Testgebiet", "Unbekannter Opcode"));
            Assert.Null(log.Error); Assert.Equal(0, log.Dropped);
            var lines = File.ReadAllLines(path);
            Assert.Equal(300, lines.Length);
            Assert.Equal("AABBCC", JsonSerializer.Deserialize<UndecodedPacket>(lines[0])!.Hex);
            Assert.Equal(299, JsonSerializer.Deserialize<UndecodedPacket>(lines[^1])!.TimeMs);
        }
        finally { File.Delete(path); }
    }
}
