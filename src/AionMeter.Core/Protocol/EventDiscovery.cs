using System.Buffers.Binary;
using System.Text;
using AionMeter.Core.Capture;

namespace AionMeter.Core.Protocol;

public sealed record DiscoveryCandidate(string Label, string Opcode, int MarkerHits, int MarkerCount, long Inside, long Outside,
    bool StrongCandidate, IReadOnlyList<UndecodedPacket> Samples, IReadOnlyList<string> FieldPreview);

/// <summary>Temporal correlation, NOT evidence that an opcode actually represents a cube/soul/loot acquisition.</summary>
public sealed class EventDiscovery
{
    private sealed class State
    {
        public HashSet<int> Markers { get; } = [];
        public List<UndecodedPacket> Samples { get; } = [];
        public long Inside;
    }
    private readonly RecordingMarker[] _markers;
    private readonly long _windowMs;
    private readonly Dictionary<(string Label, string Opcode), State> _states = [];
    private readonly Dictionary<string, long> _totals = [];
    public EventDiscovery(IEnumerable<RecordingMarker> markers, long windowMs = 3000)
    {
        _markers = markers.ToArray();
        if (_markers.Length == 0 || _markers.Length > 10000 || _markers.Select(m => m.Label).Distinct().Count() > 64 || windowMs is < 1 or > 30000)
            throw new InvalidDataException("discover benötigt Marker und ein Zeitfenster von 1–30000 ms.");
        _windowMs = windowMs;
    }

    public IReadOnlyList<RecordingMarker> Observe(UndecodedPacket packet)
    {
        _totals[packet.Opcode] = _totals.GetValueOrDefault(packet.Opcode) + 1;
        var matches = _markers.Select((marker, index) => (marker, index))
            .Where(p => Math.Abs((double)p.marker.TimeMs - packet.TimeMs) <= _windowMs).ToArray();
        foreach (var group in matches.GroupBy(p => p.marker.Label, StringComparer.Ordinal))
        {
            var key = (group.Key, packet.Opcode);
            if (!_states.TryGetValue(key, out var state))
            {
                if (_states.Count >= 4096) throw new InvalidDataException("Zu viele Kandidaten; Aufnahme oder Markerdatei bitte aufteilen.");
                _states[key] = state = new State();
            }
            foreach (var hit in group) state.Markers.Add(hit.index);
            state.Inside++;
            if (state.Samples.Count < 8) state.Samples.Add(packet with { Hex = packet.Hex[..Math.Min(1024, packet.Hex.Length)] });
        }
        return matches.Select(p => p.marker).ToArray();
    }

    public IReadOnlyList<DiscoveryCandidate> GetCandidates() => _states.Select(pair =>
    {
        var (label, opcode) = pair.Key; var state = pair.Value;
        var count = _markers.Count(m => m.Label == label);
        var outside = _totals[opcode] - state.Inside;
        return new DiscoveryCandidate(label, opcode, state.Markers.Count, count, state.Inside, outside,
            count >= 2 && state.Markers.Count == count && outside <= 2, state.Samples.AsReadOnly(), Preview(state.Samples));
    }).OrderByDescending(c => c.StrongCandidate).ThenByDescending(c => (double)c.MarkerHits / c.MarkerCount)
      .ThenBy(c => c.Outside).ThenBy(c => c.Opcode, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string> Preview(IReadOnlyList<UndecodedPacket> samples)
    {
        var bytes = samples.Select(s => Convert.FromHexString(s.Hex)).ToArray();
        var min = bytes.Min(b => b.Length);
        var result = new List<string>();
        for (var offset = 0; offset < Math.Min(min, 24); offset++)
        {
            Add("u8", bytes.Select(b => (ulong)b[offset]));
            if (offset + 2 <= min) Add("u16le", bytes.Select(b => (ulong)BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(offset))));
            if (offset + 8 <= min) Add("u64le", bytes.Select(b => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(offset))));
            if (offset + 4 <= min)
            {
                Add("u32le", bytes.Select(b => (ulong)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset))));
                Add("u32be", bytes.Select(b => (ulong)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset))));
            }
            void Add(string type, IEnumerable<ulong> numbers)
            {
                var values = numbers.ToArray();
                result.Add($"Offset {offset}, {type}: {(values.Distinct().Count() == 1 ? "gleich" : "veränderlich")} [{string.Join(", ", values)}] (Zahl, möglicherweise ID; unbestätigt)");
            }
        }
        foreach (var sample in bytes.Take(3))
        {
            // Printable ASCII runs are UTF-8-compatible string hints, never inferred field definitions.
            var text = Encoding.ASCII.GetString(sample.Take(256).ToArray());
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"[\x20-\x7E]{4,64}"))
                result.Add($"Möglicher String ab Offset {match.Index}: {match.Value}");
        }
        return result.AsReadOnly();
    }
}
