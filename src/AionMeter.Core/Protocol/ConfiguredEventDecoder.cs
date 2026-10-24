using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using AionMeter.Core.Events;

namespace AionMeter.Core.Protocol;

public sealed record ConfiguredGameEvent(long TimeMs, string EventName, int? MapId, string? Zone,
    IReadOnlyDictionary<string, JsonElement> Fields, bool Notify = false, bool Sound = false) : GameEvent(TimeMs);
public sealed record EventFieldDefinition(string Name, int Offset, string Type, int Length = 0, JsonElement? Match = null);
public sealed record EventDefinition(string Name, bool Enabled = false, string? Opcode = null,
    EventFieldDefinition[]? Fields = null, bool Notify = false, bool Sound = false);
public sealed record EventConfiguration(EventDefinition[] Events);

/// <summary>Validated immutable decoder rules. Unknown layouts are disabled templates, never guessed protocols.</summary>
public sealed class ConfiguredEventDecoder
{
    private sealed record Rule(string Name, EventFieldDefinition[] Fields, bool Notify, bool Sound);
    private readonly Dictionary<ushort, List<Rule>> _rules = [];
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public ConfiguredEventDecoder(EventConfiguration configuration)
    {
        if (configuration.Events is null || configuration.Events.Length > 256) throw new InvalidDataException("Maximal 256 Ereignistypen sind erlaubt.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in configuration.Events)
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 96 || !names.Add(definition.Name))
                throw new InvalidDataException("Ereignisnamen müssen eindeutig und 1–96 Zeichen lang sein.");
            if (!definition.Enabled) continue;
            var text = definition.Opcode?.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (text?.Length != 4 || !ushort.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var opcode))
                throw new InvalidDataException($"{definition.Name}: Ein echter vierstelliger Hex-Opcode fehlt.");
            var fields = definition.Fields ?? [];
            if (fields.Length > 64 || fields.Any(f => f is null) || fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length)
                throw new InvalidDataException($"{definition.Name}: Maximal 64 eindeutige Felder sind erlaubt.");
            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 64 || field.Offset < 0 ||
                    field.Type is not ("u8" or "bool" or "u16le" or "u16be" or "u32le" or "u32be" or "i32le" or "u64le" or "i64le" or "f32le" or "varuint" or "utf8" or "bytes") ||
                    (field.Type is "utf8" or "bytes" && field.Length is < 1 or > 4096) ||
                    field.Match is { ValueKind: JsonValueKind.Object or JsonValueKind.Array })
                    throw new InvalidDataException($"{definition.Name}: Ungültiges Feld {field.Name}.");
            }
            if (!_rules.TryGetValue(opcode, out var list)) _rules[opcode] = list = [];
            list.Add(new(definition.Name, fields.Select(f => f with { Match = f.Match?.Clone() }).ToArray(), definition.Notify, definition.Sound));
        }
    }

    public static ConfiguredEventDecoder Load(string path)
    {
        if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException("events.json darf maximal 1 MiB groß sein.");
        return new(JsonSerializer.Deserialize<EventConfiguration>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("events.json ist leer."));
    }

    public IReadOnlyList<ConfiguredGameEvent> Decode(ushort opcode, ReadOnlySpan<byte> body, long timeMs, int? mapId, string? zone)
    {
        if (!_rules.TryGetValue(opcode, out var rules)) return [];
        var events = new List<ConfiguredGameEvent>();
        foreach (var rule in rules)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var valid = true;
            foreach (var field in rule.Fields)
            {
                if (!TryRead(field, body, out var value) || (field.Match is { } expected && !Matches(value, expected))) { valid = false; break; }
                values.Add(field.Name, value);
            }
            if (valid) events.Add(new(timeMs, rule.Name, mapId, zone, new ReadOnlyDictionary<string, JsonElement>(values), rule.Notify, rule.Sound));
        }
        return events;
    }

    private static bool Matches(JsonElement value, JsonElement expected) => value.ValueKind == expected.ValueKind && (value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() == expected.GetString(),
        JsonValueKind.Number => value.TryGetDecimal(out var a) && expected.TryGetDecimal(out var b)
            ? a == b : value.GetDouble() == expected.GetDouble(),
        _ => value.GetRawText() == expected.GetRawText(),
    });

    private static bool TryRead(EventFieldDefinition field, ReadOnlySpan<byte> body, out JsonElement value)
    {
        value = default;
        if (field.Offset > body.Length) return false;
        var span = body[field.Offset..];
        var size = field.Type switch { "u8" or "bool" => 1, "u16le" or "u16be" => 2, "u32le" or "u32be" or "i32le" or "f32le" => 4,
            "u64le" or "i64le" => 8, "bytes" or "utf8" => field.Length, _ => 0 };
        if (span.Length < size) return false;
        object result;
        switch (field.Type)
        {
            case "u8": result = span[0]; break;
            case "bool": if (span[0] > 1) return false; result = span[0] == 1; break;
            case "u16le": result = BinaryPrimitives.ReadUInt16LittleEndian(span); break;
            case "u16be": result = BinaryPrimitives.ReadUInt16BigEndian(span); break;
            case "u32le": result = BinaryPrimitives.ReadUInt32LittleEndian(span); break;
            case "u32be": result = BinaryPrimitives.ReadUInt32BigEndian(span); break;
            case "i32le": result = BinaryPrimitives.ReadInt32LittleEndian(span); break;
            case "u64le": result = BinaryPrimitives.ReadUInt64LittleEndian(span); break;
            case "i64le": result = BinaryPrimitives.ReadInt64LittleEndian(span); break;
            case "f32le": var number = BinaryPrimitives.ReadSingleLittleEndian(span); if (!float.IsFinite(number)) return false; result = number; break;
            case "varuint": if (!Wire.TryVarint(span, 0, out var integer, out _)) return false; result = integer; break;
            case "bytes": result = Convert.ToHexString(span[..size]); break;
            case "utf8":
                try { result = new UTF8Encoding(false, true).GetString(span[..size]).TrimEnd('\0'); }
                catch (DecoderFallbackException) { return false; }
                break;
            default: return false;
        }
        value = JsonSerializer.SerializeToElement(result, result.GetType());
        return true;
    }
}
