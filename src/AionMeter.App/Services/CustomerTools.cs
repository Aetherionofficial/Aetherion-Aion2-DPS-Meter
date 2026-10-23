using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using AionMeter.Core.Combat;

namespace AionMeter.App.Services;

/// <summary>A deliberately small export: never includes adapters, hotkeys, accounts, licenses or paths.</summary>
public sealed record LayoutProfile(double Width = 678, double Height = 330, int RowSize = 80,
    double Opacity = 0, bool AutoHeight = true, double TextScale = 1, bool HighContrast = false,
    bool AccessibleColors = false, double? Left = null, double? Top = null)
{
    public LayoutProfile Validate()
    {
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || !double.IsFinite(Opacity) || !double.IsFinite(TextScale) || Left is { } left && !double.IsFinite(left) || Top is { } top && !double.IsFinite(top))
            throw new InvalidDataException("Das Layout enthält ungültige Zahlen.");
        return this with { Width = Math.Clamp(Width, 340, 1600), Height = Math.Clamp(Height, 220, 1200),
            RowSize = Math.Clamp(RowSize, 50, 100), Opacity = Math.Clamp(Opacity, 0, 1), TextScale = Math.Clamp(TextScale, 0.7, 1.5), Left = Left is { } x ? Math.Clamp(x, -20_000, 20_000) : null, Top = Top is { } y ? Math.Clamp(y, -20_000, 20_000) : null };
    }

    public static LayoutProfile From(AppSettings s) => new(s.OverlayWidth, s.OverlayHeight, s.RowSize,
        s.BackgroundOpacity, s.AutoOverlayHeight, s.OverlayTextScale, s.HighContrast, s.AccessibleColors, s.OverlayLeft, s.OverlayTop);

    public void Apply(AppSettings s)
    {
        var p = Validate();
        if (p.Left is { } left) s.OverlayLeft = left; if (p.Top is { } top) s.OverlayTop = top;
        s.OverlayWidth = p.Width; s.OverlayHeight = p.Height; s.RowSize = p.RowSize;
        s.BackgroundOpacity = p.Opacity; s.AutoOverlayHeight = p.AutoHeight;
        s.OverlayTextScale = p.TextScale; s.HighContrast = p.HighContrast; s.AccessibleColors = p.AccessibleColors;
    }
}

public static class CustomerTools
{
    public static readonly string[] ProfileNames = ["Solo", "Gruppe", "Raid"];
    public static LayoutProfile Preset(string name) => name switch
    {
        "Solo" => new(540, 280),
        "Raid" => new(760, 600, 65),
        _ => new(),
    };

    public static void ExportLayout(string path, AppSettings settings) => File.WriteAllText(path,
        JsonSerializer.Serialize(LayoutProfile.From(settings), new JsonSerializerOptions { WriteIndented = true }));

    public static LayoutProfile ImportLayout(string path)
    {
        if (new FileInfo(path).Length > 16_384) throw new InvalidDataException("Layout-Datei ist zu groß (max. 16 KB).");
        return (JsonSerializer.Deserialize<LayoutProfile>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Leere Layout-Datei.")).Validate();
    }

    // A support report uses an allowlist rather than trying to redact arbitrary logs containing secrets.
    public static string SupportReport(MeterService meter)
    {
        var status = meter.CaptureStatus;
        return $"Aetherion {meter.Updates.Current.ToString(3)}\n" +
            $"Windows: {Environment.OSVersion.Version}\nCapture state: {status.State}\n" +
            $"Packets: {status.Packets}\nDecoded events: {status.Events}\n" +
            $"Character identified: {!string.IsNullOrWhiteSpace(meter.Tracker.SelfName)}\n" +
            $"Layout: {meter.Settings.OverlayWidth:0} x {meter.Settings.OverlayHeight:0}\n" +
            $"Auto height: {meter.Settings.AutoOverlayHeight}\nPower saving: {meter.Settings.PowerSaving}\n" +
            $"Streamer mode: {meter.Settings.StreamerMode}\n" +
            "No names, IP addresses, account data, license keys, captures, fight files or raw logs included.\n";
    }

    public static void ExportSupport(string path, string preview, AppSettings settings)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry("diagnostics.txt").Open())) writer.Write(preview);
        using (var writer = new StreamWriter(zip.CreateEntry("layout.json").Open()))
            writer.Write(JsonSerializer.Serialize(LayoutProfile.From(settings)));
    }
}

public static class PrivacyNames
{
    private static bool _enabled;
    public static event Action? Changed;
    public static bool Enabled
    {
        get => _enabled;
        set { if (_enabled == value) return; _enabled = value; Changed?.Invoke(); }
    }
    // Actor ids are not displayed: pseudonyms are stable only in this running process.
    private static readonly Dictionary<uint, int> Names = new();
    public static string Display(uint id, string name)
    {
        if (!Enabled) return name;
        if (!Names.TryGetValue(id, out var index)) Names[id] = index = Names.Count + 1;
        return $"Spieler {index}";
    }
    public static EncounterSnapshot? Apply(EncounterSnapshot? s) => !Enabled || s is null ? s : s with
    { Combatants = s.Combatants.Select(c => c with { Name = Display(c.ActorId, c.Name), ServerId = 0 }).ToArray() };
    public static CombatantDetail? Apply(CombatantDetail? d) => !Enabled || d is null ? d : d with
    { Name = Display(d.ActorId, d.Name), ServerId = 0,
        Deaths = d.Deaths?.Select(x => x with { Name = Display(x.ActorId, x.Name) }).ToArray() };
}
