using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AionMeter.Core.Combat;

namespace AionMeter.App.Services;

public enum HistoryMode
{
    BossesOnly,
    BossesAndLong,
    All,
}

public enum OverlayRanking
{
    Damage,
    Healing,
}

public sealed class AppSettings
{
    private static string _appDataDir = ResolveAppDataDir();
    /// <summary>Tests may redirect storage to a temporary directory and restore it afterwards.</summary>
    public static string AppDataDir
    {
        get => _appDataDir;
        set => _appDataDir = Path.GetFullPath(value);
    }

    private static string ResolveAppDataDir()
    {
        var configured = Environment.GetEnvironmentVariable("AIONMETER_APPDATA");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AionMeter")
            : Path.GetFullPath(configured);
    }

    private static string FilePath => Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Overlay window
    public double OverlayLeft { get; set; } = 125;
    public double OverlayTop { get; set; } = 129;
    public double OverlayWidth { get; set; } = 500;
    public double OverlayHeight { get; set; } = 300;
    public double BackgroundOpacity { get; set; } = 0;
    public bool Locked { get; set; }
    public bool ClickThrough { get; set; }
    public bool OverlayVisible { get; set; } = true;
    /// <summary>Bring the overlay up by itself when a boss is fought nearby or you hit anything.</summary>
    public bool AutoShow { get; set; } = true;
    /// <summary>An overlay that came up by itself hides again after this long out of combat; 0 = stays up.</summary>
    public int AutoHideSeconds { get; set; }
    /// <summary>Bring the overlay up when the meter starts and when it finds the game, so a player sees it is running.</summary>
    public bool ShowOnStart { get; set; } = true;

    public bool AutoOverlayHeight { get; set; } = true;
    public double OverlayTextScale { get; set; } = 1;
    public bool HighContrast { get; set; }
    public bool AccessibleColors { get; set; }
    public bool StreamerMode { get; set; }
    public bool PowerSaving { get; set; }
    public bool SetupCompleted { get; set; }
    public bool ShowPersonalRecords { get; set; } = true;
    public Dictionary<string, LayoutProfile> LayoutProfiles { get; set; } = new();
    /// <summary>Pet collection entries the player marked as owned in the local tracker.</summary>
    public HashSet<string> OwnedPetIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Boss respawn timers
    /// <summary>Tray notification this many minutes before a tracked boss respawns; 0 = off.</summary>
    public int BossAlertMinutes { get; set; } = 2;
    public string BossAlertStages { get; set; } = "10,5,2";
    public bool BossAlertSpeech { get; set; }
    public bool BossAlertQuietDuringFight { get; set; }
    public int BossAlertToastSeconds { get; set; } = 8;
    public int BossAlertVolume { get; set; } = 70;
    public string BossAlertSound { get; set; } = "aetherion";
    public double BossAlertToastScale { get; set; } = 1;
    public double BossTimersWidth { get; set; } = 620;
    public double BossTimersHeight { get; set; } = 500;

    // Event timer overlay
    public double EventTimersLeft { get; set; } = 616;
    public double EventTimersTop { get; set; } = 225;
    public double EventTimersWidth { get; set; } = 330;
    public double EventTimersHeight { get; set; } = 440;
    public double EventTimersOpacity { get; set; } = 0.94;

    // Meter
    public TargetMode TargetMode { get; set; } = TargetMode.BossOnly;
    /// <summary>True: the leader's bar is full and the rest are sized against it. False: bars show share of party damage.</summary>
    public bool BarsRelativeToTop { get; set; } = true;
    public int MaxRows { get; set; } = 6;
    public const int MinRowSize = 50, MaxRowSize = 100;
    /// <summary>Overlay row size in percent of the original (Settings → Overlay, or Ctrl + wheel over the rows).</summary>
    public int RowSize { get; set; } = 60;
    /// <summary>Gear score and combat power after the names in the overlay (when the party roster gave them).</summary>
    public bool ShowGear { get; set; } = false;
    public bool ShowHealingPanel { get; set; } = true;
    public bool ShowPeakDps { get; set; } = true;
    public bool CompactDpsOnly { get; set; }
    /// <summary>Overlay ranking: damage (DPS) or healing (HPS).</summary>
    public OverlayRanking OverlayRanking { get; set; } = OverlayRanking.Damage;
    public bool ShowBossPanel { get; set; } = true;
    public int IdleTimeoutSec { get; set; } = 10;
    public int BossIdleTimeoutSec { get; set; } = 30;

    /// <summary>Interface and skill / NPC name language: "en" (default) or "ru".</summary>
    public string Language { get; set; } = "de";

    /// <summary>Fetch skill icons from the official AION 2 game-data CDN (cached locally).</summary>
    public bool DownloadIcons { get; set; } = true;

    // Discord Rich Presence (off by default; character data is shared only after explicit opt-in).
    public bool DiscordPresenceEnabled { get; set; }
    /// <summary>Faction is selected by the player because current capture data does not reliably contain it.</summary>
    public string DiscordFaction { get; set; } = "";

    // Updates
    /// <summary>Ask GitHub for a newer version shortly after start and every few hours.</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>Installed copies download a new version by themselves and install it at a quiet moment.</summary>
    public bool AutoInstallUpdates { get; set; } = false;
    /// <summary>A version the user chose to skip ("0.2.0"): not announced again, a newer one is.</summary>
    public string? SkippedUpdate { get; set; }

    // Capture / storage
    public bool SaveHistory { get; set; } = true;
    public HistoryMode HistoryMode { get; set; } = HistoryMode.BossesAndLong;
    /// <summary>For <see cref="HistoryMode.BossesAndLong"/>: non-boss fights shorter than this are not saved.</summary>
    public int HistoryMinSeconds { get; set; } = 20;
    public bool RecordPackets { get; set; }
    public string? CaptureDevice { get; set; }

    // Hotkeys (modifier+key, parsed by HotkeyManager)
    public string HotkeyToggleOverlay { get; set; } = "Ctrl+Shift+D";
    public string HotkeyReset { get; set; } = "Ctrl+Shift+R";
    public string HotkeyClickThrough { get; set; } = "Ctrl+Shift+L";
    public string HotkeyTimers { get; set; } = "Ctrl+Shift+T";

    /// <summary>Bumped when the overlay layout changes enough that saved sizes no longer fit.</summary>
    public int LayoutVersion { get; set; } // 0 when missing from an older settings file; new installs get it in Load()
    private const int CurrentLayout = 15;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
                if (s.LayoutVersion < 3)
                {
                    // v3 = card layout (portrait header, two-line rows): the old compact size is too small for it.
                    s.OverlayWidth = Math.Max(s.OverlayWidth, 470);
                    s.OverlayHeight = Math.Max(s.OverlayHeight, 540);
                }
                if (s.LayoutVersion < 4)
                {
                    // v4 = English / Russian switch. Earlier builds picked the language from Windows on their own;
                    // the default is now English until the user chooses.
                    s.Language = "de";
                }
                if (s.LayoutVersion < 5)
                {
                    // First migration to the owner's release feed: keep automatic installs opt-in.
                    s.AutoInstallUpdates = false;
                }
                if (s.LayoutVersion < 6) s.CheckUpdates = true;
                if (s.LayoutVersion < 7)
                {
                    // v7 = dashboard-style combat card with boss health, party DPS/HPS tiles and roomier player rows.
                    s.OverlayWidth = Math.Max(s.OverlayWidth, 470);
                    s.OverlayHeight = Math.Max(s.OverlayHeight, 620);
                }
                if (s.LayoutVersion < 8)
                {
                    // v8 keeps the overlay readable over bright game scenes and prevents gear details colliding with row bars.
                    // Older default layouts reserved a large empty list area; keep user-sized layouts but compact the old default.
                    if (s.OverlayHeight >= 620) s.OverlayHeight = 420;
                    s.RowSize = Math.Max(s.RowSize, MinRowSize);
                }
                if (s.LayoutVersion < 9)
                {
                    // v9 removes the cramped row-size range while preserving the user's transparency preference.
                    s.RowSize = Math.Max(s.RowSize, MinRowSize);
                }
                if (s.LayoutVersion < 10)
                {
                    // v10 matches the reference composition: taller live card, narrow event schedule, settings on the right.
                    if (s.OverlayHeight <= 540) s.OverlayHeight = 630;
                    if (Math.Abs(s.EventTimersLeft - 26) < 1) s.EventTimersLeft = 616;
                    if (Math.Abs(s.EventTimersTop - 110) < 1) s.EventTimersTop = 225;
                    if (Math.Abs(s.EventTimersWidth - 380) < 1) s.EventTimersWidth = 330;
                    if (Math.Abs(s.EventTimersOpacity - 0.76) < 0.02) s.EventTimersOpacity = 0.94;
                }
                if (s.LayoutVersion < 11)
                {
                    // v11 makes the combat overlay narrower and taller so it covers less of the game view.
                    if (s.OverlayWidth >= 440) s.OverlayWidth = 410;
                    if (s.OverlayHeight <= 630) s.OverlayHeight = 700;
                    if (s.MaxRows == 10) s.MaxRows = 8;
                    if (s.RowSize >= 95) s.RowSize = 80;
                }
                if (s.LayoutVersion < 12)
                {
                    // v12 follows the wide, shallow reference overlay, including existing installations.
                    s.OverlayWidth = 678;
                    s.OverlayHeight = 330;
                    s.RowSize = 80;
                    s.BackgroundOpacity = 0;
                    s.ShowGear = false;
                }
                if (s.LayoutVersion < 13)
                {
                    // v13 keeps the live overlay compact: the previous wide card and 80% rows covered too much of the game.
                    if (Math.Abs(s.OverlayWidth - 678) < 1 && Math.Abs(s.OverlayHeight - 330) < 1)
                    {
                        s.OverlayWidth = 600;
                        s.OverlayHeight = 300;
                    }
                    if (s.RowSize == 80) s.RowSize = 65;
                }
                if (s.LayoutVersion < 14)
                {
                    // v14 uses a smaller default footprint so the DPS meter does not cover the game view.
                    if (Math.Abs(s.OverlayWidth - 600) < 1 && Math.Abs(s.OverlayHeight - 300) < 1)
                    {
                        s.OverlayWidth = 500;
                        s.OverlayHeight = 300;
                    }
                    if (s.MaxRows == 8) s.MaxRows = 6;
                    if (s.RowSize == 65) s.RowSize = 60;
                }
                if (s.LayoutVersion < 15)
                {
                    // The live meter must remain visible during travel and combat pauses. The old default
                    // hid it after 60 seconds, which made the overlay appear to vanish when no DPS was dealt.
                    if (s.AutoHideSeconds == 60) s.AutoHideSeconds = 0;
                }
                s.OverlayTextScale = double.IsFinite(s.OverlayTextScale) ? Math.Clamp(s.OverlayTextScale, 0.8, 1.5) : 1;
                s.LayoutProfiles ??= new();
                s.Language = UiText.Normalize(s.Language);
                s.LayoutVersion = CurrentLayout;
                return s;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }
        return new AppSettings { LayoutVersion = CurrentLayout };
    }

    /// <summary>Throw-away settings (render tool): never written to disk.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Transient { get; init; }

    public void Save()
    {
        if (Transient) return;
        Directory.CreateDirectory(AppDataDir);
        // Written aside, then swapped in: Windows shutting the app down mid-write must not leave an empty settings.json.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public void ApplyTo(MeterOptions options)
    {
        options.TargetMode = TargetMode;
        options.IdleTimeoutMs = Math.Clamp(IdleTimeoutSec, 3, 120) * 1000;
        options.BossIdleTimeoutMs = Math.Clamp(BossIdleTimeoutSec, 5, 300) * 1000;
    }
}
