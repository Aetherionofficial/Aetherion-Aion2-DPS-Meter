using AionMeter.Core.Capture;
using AionMeter.Core.Protocol;

namespace AionMeter.Tests;

public sealed class DiscoveryTests
{
    [Fact]
    public void Markers_round_trip_with_unicode_label_and_validate_inputs()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Aion-markers-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "test.pcap"); var markers = new RecordingMarkers();
            var marker = new RecordingMarker(1700000000000, "cube_geöffnet", 1110, "Testgebiet");
            markers.Append(path, marker); markers.Append(path, marker with { TimeMs = marker.TimeMs + 1000 });
            var loaded = RecordingMarkers.Load(RecordingMarkers.SidecarPath(path)); Assert.Equal(marker, loaded[0]); Assert.Equal(2, loaded.Count);
            Assert.Throws<InvalidDataException>(() => markers.Append(path, marker with { Label = "\n" }));
            Assert.Throws<InvalidDataException>(() => markers.Append(path, marker with { TimeMs = -1 }));
        }
        finally { Directory.Delete(directory, true); }
    }
    private static UndecodedPacket Packet(long time, string opcode, string hex = "0100000043554245") => new(time, opcode, hex.Length / 2, hex, 1110, "Gebiet", "Unbekannter Opcode");

    [Fact]
    public void Repeated_label_rare_opcodes_are_highlighted_and_fields_show_constant_and_changed_values()
    {
        var discovery = new EventDiscovery([new(10000, "cube"), new(20000, "cube")]);
        Assert.Single(discovery.Observe(Packet(10001, "ABCD")));
        discovery.Observe(Packet(20001, "ABCD", "0200000043554245"));
        discovery.Observe(Packet(10002, "EEEE")); discovery.Observe(Packet(20002, "EEEE"));
        for (var i = 0; i < 5; i++) discovery.Observe(Packet(30000 + i, "EEEE"));
        var result = discovery.GetCandidates(); Assert.Equal("ABCD", result[0].Opcode); Assert.True(result[0].StrongCandidate);
        Assert.Equal(2, result[0].MarkerHits); Assert.Equal(0, result[0].Outside);
        Assert.Contains(result[0].FieldPreview, s => s.Contains("veränderlich")); Assert.Contains(result[0].FieldPreview, s => s.Contains("gleich"));
        Assert.Contains(result[0].FieldPreview, s => s.Contains("CUBE")); Assert.False(result[1].StrongCandidate); Assert.Equal(5, result[1].Outside);
    }

    [Fact]
    public void Single_markers_and_partial_label_coverage_are_not_strong_evidence()
    {
        var discovery = new EventDiscovery([new(10000, "cube"), new(20000, "cube"), new(30000, "soul")]);
        discovery.Observe(Packet(10000, "ABCD")); discovery.Observe(Packet(30000, "DCBA"));
        Assert.All(discovery.GetCandidates(), c => Assert.False(c.StrongCandidate));
        Assert.Empty(discovery.Observe(Packet(7000 - 1, "ABCD")));
        Assert.Single(discovery.Observe(Packet(7000, "ABCD"))); // inclusive ±3 seconds
    }

    [Fact]
    public void Overlapping_markers_do_not_count_one_packet_twice_in_inside_statistics()
    {
        var discovery = new EventDiscovery([new(10000, "cube"), new(11000, "cube")]);
        Assert.Equal(2, discovery.Observe(Packet(10500, "ABCD")).Count);
        var candidate = Assert.Single(discovery.GetCandidates()); Assert.Equal(1, candidate.Inside); Assert.Equal(0, candidate.Outside);
    }
}
