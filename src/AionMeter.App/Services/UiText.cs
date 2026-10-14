namespace AionMeter.App.Services;

/// <summary>
/// Interface wording and language selection. Plain properties so XAML binds to them:
/// <c>{Binding TabDps, Source={x:Static s:UiText.Current}}</c> in windows, <c>{Binding T.ColDps}</c> in the overlay.
/// Switching language swaps <see cref="Current"/>; the overlay re-binds, other windows are reopened.
/// </summary>
public sealed class UiText
{
    // Only offer languages with a complete interface translation. Name tables may support more languages,
    // but showing those here made the UI silently fall back to English after the user selected them.
    public static readonly string[] Languages = ["en", "de", "ru"];
    public static readonly (string Code, string Name)[] LanguageOptions =
    [
        ("en", "English"), ("de", "Deutsch"), ("ru", "Русский"),
    ];

    public static UiText Current { get; private set; } = new();

    public static string Normalize(string? language) => Languages.Contains(language) ? language! : "en";

    public static void Use(string? language) => Current = Normalize(language) switch
    {
        "ru" => Ru,
        "de" => De,
        var code => new UiText { Code = code, LanguageName = LanguageOptions.First(x => x.Code == code).Name },
    };

    public string Code { get; init; } = "en";
    public string LanguageName { get; init; } = "English";
    public string MapLabel { get; init; } = "Map";

    /// <summary>Dates in the interface language ("06 Oct"), not in the Windows one.</summary>
    public System.Globalization.CultureInfo Culture => System.Globalization.CultureInfo.GetCultureInfo(Code switch
    {
        "ru" => "ru-RU",
        "de" => "de-DE",
        "tr" => "tr-TR",
        "pl" => "pl-PL",
        "bs" => "bs-Latn-BA",
        "fr" => "fr-FR",
        "ko" => "ko-KR",
        "zh-Hant" => "zh-TW",
        "ar" => "ar-SA",
        "zh-Hans" => "zh-CN",
        "it" => "it-IT",
        _ => "en-US",
    });

    public IReadOnlyDictionary<Core.Events.GameClass, string> ClassNames { get; init; } = new Dictionary<Core.Events.GameClass, string>
    {
        [Core.Events.GameClass.Gladiator] = "Gladiator",
        [Core.Events.GameClass.Templar] = "Templar",
        [Core.Events.GameClass.Ranger] = "Ranger",
        [Core.Events.GameClass.Assassin] = "Assassin",
        [Core.Events.GameClass.Sorcerer] = "Sorcerer",
        [Core.Events.GameClass.Elementalist] = "Elementalist",
        [Core.Events.GameClass.Cleric] = "Cleric",
        [Core.Events.GameClass.Chanter] = "Chanter",
        [Core.Events.GameClass.Brawler] = "Brawler",
    };

    public string ClassName(Core.Events.GameClass c) => ClassNames.TryGetValue(c, out var n) ? n : "";

    public IReadOnlyDictionary<Core.Events.HitFlags, string> FlagNames { get; init; } = new Dictionary<Core.Events.HitFlags, string>
    {
        [Core.Events.HitFlags.Critical] = "Critical",
        [Core.Events.HitFlags.Back] = "Back",
        [Core.Events.HitFlags.Front] = "Front",
        [Core.Events.HitFlags.Perfect] = "Perfect",
        [Core.Events.HitFlags.Heavy] = "Smite",
        [Core.Events.HitFlags.Multi] = "Multi-hit",
        [Core.Events.HitFlags.Double] = "Double",
        [Core.Events.HitFlags.Block] = "Block",
        [Core.Events.HitFlags.Parry] = "Parry",
        [Core.Events.HitFlags.Endure] = "Endure",
        [Core.Events.HitFlags.Evade] = "Evade",
        [Core.Events.HitFlags.Dot] = "DoT",
    };

    /// <summary>"Critical · Back" in the current language.</summary>
    public string FlagsText(Core.Events.HitFlags flags) =>
        string.Join(" · ", FlagNames.Where(kv => (flags & kv.Key) != 0).Select(kv => kv.Value));

    // Skill row tooltip in the breakdown
    public string TipSkillDamage { get; init; } = "Damage";
    public string TipSkillHits { get; init; } = "Hits";
    public string TipSkillDotTicks { get; init; } = "DoT ticks";
    public string TipSkillMin { get; init; } = "Min";
    public string TipSkillAvg { get; init; } = "Avg";
    public string TipSkillMax { get; init; } = "Max";

    /// <summary>
    /// Players whose name the server has not sent yet are "#id": show them as "Templar #10388" so the class is
    /// readable at a glance.
    /// </summary>
    public string PlayerLabel(string name, Core.Events.GameClass cls) =>
        name.StartsWith('#') && ClassName(cls) is { Length: > 0 } c ? $"{c} {name}" : name;

    /// <summary>A row of the ranking: a player, or the pseudo-row of pets whose owner is unknown.</summary>
    public string CombatantLabel(uint actorId, string name, Core.Events.GameClass cls) =>
        actorId == Core.Combat.Combatant.UnknownSummonsId ? UnknownSummons : PlayerLabel(name, cls);

    /// <summary>A fight's title as shown: a boss the meter never saw appear has nothing but its id.</summary>
    public string FightTitle(Core.Combat.EncounterSnapshot s) =>
        s.Boss is { NpcCode: 0 } b && s.Title == $"#{b.ActorId}" ? UnknownBoss : s.Title;

    public string UnknownSummons { get; init; } = "Summons (owner unknown)";
    public string UnknownSummonsTip { get; init; } =
        "Pets, spirits and skill effects (Bittercold Wind, Fire Wall …) whose caster could not be identified. Not a player: not counted in places or the player total.";

    // ---------------------------------------------------------------- boss timers
    public string TimersTitle { get; init; } = "Boss timers";
    public string TimersButton { get; init; } = "Boss timers";
    public string HkTimers { get; init; } = "Boss timers";
    public string TipTimers { get; init; } = "Boss respawn timers";
    public string TrayTimers { get; init; } = "Boss timers…";
    public string TimersHint { get; init; } =
        "Open a map's field boss list in the game (map → Exploration → field monsters) and its timers come in straight from the server. Field bosses you see die restart their countdown. Bosses of quest and sealed-area instances have no world timer and are not listed. 🔔 = notify before respawn.";
    public string FilterWatched { get; init; } = "🔔 Watched";
    public string TipWatch { get; init; } = "Notify before this boss respawns";
    public string UnknownSlotBoss { get; init; } = "Field boss #{0}";
    public string SourceGame { get; init; } = "From the in-game boss list (read at {0})";
    public string SourceMeter { get; init; } = "Measured by the meter (kills it saw)";
    public string RespawnFromGame { get; init; } = "from game";
    public string TimerAliveSince { get; init; } = "up since {0}";
    public string TimerNoTime { get; init; } = "no timer in the game";
    public string TipServer { get; init; } = "Every server runs its own bosses: these are the timers of this server (your character's, unless picked)";
    public string ServerUnknown { get; init; } = "server ?";
    public string ServerYours { get; init; } = "your character";
    public string MapNumber { get; init; } = "Map {0}";
    public string TimersEmpty { get; init; } = "No timers yet. Kill a world boss (or watch one die) and its countdown starts here.";
    public string TimerAlive { get; init; } = "ALIVE";
    public string TimerDue { get; init; } = "SHOULD BE UP";
    public string TimerSeen { get; init; } = "seen {0}";
    public string TimerSince { get; init; } = "since {0}";
    public string TimerAt { get; init; } = "at {0}";
    public string TimerKilled { get; init; } = "killed {0}";
    public string TimerFoundDead { get; init; } = "found dead {0}";
    public string TimerNeedsInterval { get; init; } = "set respawn time";
    public string TimerLearned { get; init; } = "Measured by the meter: the shortest time from a kill to the boss showing up again";
    public string TipRespawn { get; init; } = "Respawn time after a kill";
    public string RespawnUnknown { get; init; } = "respawn ?";
    public string BtnKilled { get; init; } = "Killed";
    public string TipKilled { get; init; } = "Killed just now — start the countdown";
    public string TipRemoveTimer { get; init; } = "Remove from the list";
    public string AlertLabel { get; init; } = "Notify before respawn";
    public string AlertOff { get; init; } = "off";
    public string AlertSoon { get; init; } = "{0} respawns in about {1} min ({2})";
    public string AlertNow { get; init; } = "{0} should be up now ({1})";
    public string HoursShort { get; init; } = "h";
    public string MinutesShort { get; init; } = "min";
    public string EventTimersTitle { get; init; } = "Event schedule";
    public string EventTimersHint { get; init; } = "Live countdowns use the local clock. Recurring schedules are community-reported; confirm notices in game. Field-boss timers come directly from the game via Boss timers.";
    public string EventRift { get; init; } = "Spacetime Rift";
    public string EventShugo { get; init; } = "Shugo Festival";
    public string EventInvasion { get; init; } = "Dimensional Invasion";
    public string EventWatcher { get; init; } = "Watcher Kaira (world boss)";
    public string EventSiege { get; init; } = "Artifact Siege";
    public string EventExecutors { get; init; } = "Abyss Siege bosses";
    public string EventNahma { get; init; } = "Guardian Lord Nahma (world boss)";
    public string EventArena { get; init; } = "Arena of Tactics";
    public string EventNext { get; init; } = "NEXT START";
    public string EventAt { get; init; } = "Local time: {0}";
    public string EventSource { get; init; } = "Schedule: community observations · Field bosses: in-game timer";
    public string EventOpacity { get; init; } = "Opacity";
    public string EventCompact { get; init; } = "Collapse timer list";
    public string EventExpand { get; init; } = "Expand timer list";

    /// <summary>"1 h 30 min", "45 min".</summary>
    public string Duration(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        if (h == 0) return $"{m} {MinutesShort}";
        return m == 0 ? $"{h} {HoursShort}" : $"{h} {HoursShort} {m} {MinutesShort}";
    }

    // ---------------------------------------------------------------- automatic show / hide
    public string AutoShow { get; init; } = "Show by itself when a fight starts";
    public string AutoShowHint { get; init; } =
        "When a boss is fought nearby or you hit any monster. Hide it during a fight and it waits for the next one.";
    public string AutoHide { get; init; } = "Hide again after combat (s, 0 = never)";
    public string ShowOnStart { get; init; } = "Show the overlay when the meter or the game starts";

    // ---------------------------------------------------------------- overlay: columns
    public string ColPlayer { get; init; } = "PLAYER";
    public string ColDps { get; init; } = "DPS";
    public string ColDamage { get; init; } = "DAMAGE";
    public string ColShare { get; init; } = "SHARE";
    public string HealShort { get; init; } = "Heal";
    public string TipPlace { get; init; } = "Place in this fight by damage dealt";
    public string TipDps { get; init; } = "Fight-average DPS: total damage divided by time from the first to the latest hit";
    public string TipDamage { get; init; } = "Total damage dealt in this fight";
    public string TipShare { get; init; } = "Share of the whole group's damage";
    public string TipGs { get; init; } = "Gear score (equipment item level), from the party roster";
    public string TipCp { get; init; } = "Combat power, from the party roster";

    // ---------------------------------------------------------------- overlay: card
    public string LiveFight { get; init; } = "live fight";
    public string LastFight { get; init; } = "last fight";
    public string SavedFight { get; init; } = "saved fight";
    public string ThisSession { get; init; } = "this session";
    public string WaitingLabel { get; init; } = "waiting for combat";
    public string WaitingTitle { get; init; } = "Waiting for combat";
    public string WaitingDetail { get; init; } = "Hit something — the meter starts on its own";
    public string EmptyParty { get; init; } = "Your party appears here as soon as someone lands a hit.";
    public string TopHit { get; init; } = "TOP HIT";
    public string TopHitBy { get; init; } = "Biggest single hit — {0}";
    public string TipScrollFights { get; init; } = "Mouse wheel here: step through fights";
    public string Defeated { get; init; } = "Defeated";
    public string UnknownBoss { get; init; } = "Unknown boss";
    /// <summary>"counted from 82% HP": the meter started (or restarted) after the boss had already lost the rest.</summary>
    public string CountedFrom { get; init; } = "counted from {0} HP";
    public string HpUnknown { get; init; } = "max HP unknown";
    /// <summary>Tooltip of the instance countdown in the overlay's top line; {0} = when it runs out (local time).</summary>
    public string TimeLimitTip { get; init; } = "Time limit (from the game): runs out at {0}";
    public string BossLimitTip { get; init; } = "Boss time limit (from the game): kill it before {0}";
    /// <summary>A world boss with a crowd: "444 attacking · damage of 53 sent" (see EncounterSnapshot.MostDamageUnsent).</summary>
    public string FightingCount { get; init; } = "{0} attacking";
    public string DamageSentBy { get; init; } = "damage of {0} sent";
    public string DamageUnsentTip { get; init; } =
        "On a world boss with a crowd the game sends your client the damage of only a few players, you among them. For everyone else it sends that a hit landed, without the number, or their damage over time. So their numbers and every share here are far below the real ones; a meter on this PC cannot see more.";
    public string DamageUnsentLine(Core.Combat.EncounterSnapshot s) =>
        $"{string.Format(FightingCount, s.Fighting)} · {string.Format(DamageSentBy, s.DamageSenders)}";
    public string TipRestart { get; init; } = "Restart the meter ({0}): the fight so far is saved, counting starts over";
    public string InProgress { get; init; } = "In progress";
    public string Kill { get; init; } = "Kill";
    public string Wipe { get; init; } = "Wipe";
    public string Ended { get; init; } = "Ended";
    public string OpenWorld { get; init; } = "Open world";
    public string OpenFight { get; init; } = "Open the fight";
    public string Fights { get; init; } = "Fights";
    public string NamesLater { get; init; } = "names after next loading screen";
    public string YourPlace { get; init; } = "Your place in this fight";
    public string Party { get; init; } = "party";
    public string Seconds { get; init; } = "s";
    public string SwitchLanguage { get; init; } = "Language: English → Русский";

    // ---------------------------------------------------------------- tray
    public string TrayHide { get; init; } = "Hide overlay";
    public string TrayShow { get; init; } = "Show overlay";
    public string TrayHistory { get; init; } = "Fight history…";
    public string TraySettings { get; init; } = "Settings…";
    public string TrayDemo { get; init; } = "Run demo fight";
    public string TrayReplay { get; init; } = "Replay capture file…";
    public string TrayExit { get; init; } = "Exit";
    public string ClickThroughOn { get; init; } = "Click-through ON — the overlay ignores the mouse. {0} to turn it off.";
    public string ClickThroughOff { get; init; } = "Click-through OFF";

    // ---------------------------------------------------------------- overlay: menus
    public string Live { get; init; } = "Live";
    public string LiveHint { get; init; } = "follows the current fight";
    public string SessionSection { get; init; } = "THIS SESSION";
    public string SavedToday { get; init; } = "SAVED · TODAY";
    public string SavedYesterday { get; init; } = "SAVED · YESTERDAY";
    public string SavedOn { get; init; } = "SAVED · {0}";
    public string AllSaved { get; init; } = "All saved fights ({0})…";
    public string NoFights { get; init; } = "No fights yet — they are saved automatically";
    public string OpenBreakdown { get; init; } = "Open breakdown";
    public string CopyPlayer { get; init; } = "Copy {0}'s result to chat";
    public string CopyParty { get; init; } = "Copy party ranking to chat";
    public string CopyTable { get; init; } = "Copy party table for Discord";
    public string Copied { get; init; } = "Copied — paste it into the chat with Ctrl+V";
    public string CopyFailed { get; init; } = "The clipboard is busy — try again";
    public string TipChat { get; init; } = "Result to chat or Discord";
    /// <summary>What a pasted result says, in this language (numbers keep their units in every language).</summary>
    public Core.ChatWords Chat { get; init; } = Core.ChatWords.English;
    public string RowDamage { get; init; } = "Damage";
    public string RowHits { get; init; } = "Hits";
    public string RowCrit { get; init; } = "Crit";
    public string RowMax { get; init; } = "Max";
    public Func<int, string> Players { get; init; } = n => n == 1 ? "1 player" : $"{n} players";

    // ---------------------------------------------------------------- breakdown window
    public string TabDps { get; init; } = "DPS";
    public string TabAccuracy { get; init; } = "Accuracy";
    public string TabRotation { get; init; } = "Rotation";
    public string TabDefense { get; init; } = "Defense";
    public string TabHealing { get; init; } = "Healing";
    public string HealingTotal { get; init; } = "TOTAL HEALING";
    public string HealingSkill { get; init; } = "SKILL";
    public string HealingEvents { get; init; } = "CASTS";
    public string HealingAmount { get; init; } = "HEALING";
    public string TilePlace { get; init; } = "PLACE";
    public string TileDamage { get; init; } = "DMG";
    public string TileDps { get; init; } = "DPS";
    public string TileContrib { get; init; } = "CONTRIB";
    public string TileTime { get; init; } = "TIME";
    public string DpsTimeline { get; init; } = "DPS TIMELINE";
    public string ThSkill { get; init; } = "SKILL";
    public string ThHits { get; init; } = "HITS";
    public string ThDamage { get; init; } = "DAMAGE";
    public string ThDps { get; init; } = "DPS";
    public string ThAvg { get; init; } = "AVG";
    public string ThMax { get; init; } = "MAX";
    public string ThCrit { get; init; } = "CRIT";
    public string ThShare { get; init; } = "SHARE";
    public string ThBack { get; init; } = "BACK";
    public string ThFront { get; init; } = "FRONT";
    public string ThPerfect { get; init; } = "PERFECT";
    public string ThSmite { get; init; } = "SMITE";
    public string ThMulti { get; init; } = "MULTI";
    public string SortTip { get; init; } = "Sort by this column · click again to reverse";
    public string QHitsMeasured { get; init; } = "{0} hits measured";
    public string QPositional { get; init; } = "positional";
    public string QExtraHits { get; init; } = "extra hits";
    public string QBiggestHit { get; init; } = "BIGGEST HIT";
    public string DefTaken { get; init; } = "DAMAGE TAKEN";
    public string DefTakenPerSec { get; init; } = "TAKEN / S";
    public string DefDotTicks { get; init; } = "DOT TICKS";
    public string DefHits { get; init; } = "{0} hits";
    public string DefDot { get; init; } = "damage over time";
    public string DefNote { get; init; } = "Damage taken counts hits from NPCs while this fight was running. Block / parry / evade details depend on what the server reports.";
    public string RotationHint { get; init; } = "Every cast in order. Gold ring = critical hit · tile size follows damage · DoT ticks hidden.";
    public string You { get; init; } = "you";
    public string PartyDpsSuffix { get; init; } = "party";
    public string TipCopyImage { get; init; } = "Copy as image";
    public string TipMinimize { get; init; } = "Minimize";
    public string TipClose { get; init; } = "Close";

    // ---------------------------------------------------------------- history window
    public string YourFights { get; init; } = "Your fights";
    public string SavedCount { get; init; } = "{0} saved";
    public string ShownOf { get; init; } = "{0} of {1}";
    public string FilterAll { get; init; } = "All";
    public string FilterBosses { get; init; } = "Bosses";
    public string FilterKills { get; init; } = "Kills";
    public string ShowInOverlay { get; init; } = "Show in overlay";
    public string TipShowInOverlay { get; init; } = "Show this fight in the overlay (live returns with the next pull)";
    public string HistoryEmpty { get; init; } = "Kill a boss and it lands here automatically.";
    public string PlayersShort { get; init; } = "pl.";
    public string JustNow { get; init; } = "just now";
    public string MinutesAgo { get; init; } = "{0}m ago";
    public string HoursAgo { get; init; } = "{0}h ago";
    public string DeleteConfirm { get; init; } = "Delete \"{0}\" ({1})?";
    public string TipRefresh { get; init; } = "Refresh";
    public string TipOpenFolder { get; init; } = "Open folder";
    public string TipDelete { get; init; } = "Delete selected fight";
    public string TipOpenBreakdown { get; init; } = "Open the full breakdown";
    public string DamageWord { get; init; } = "damage";

    // ---------------------------------------------------------------- settings window
    public string SettingsTitle { get; init; } = "Settings";
    public string LanguageLabel { get; init; } = "Language";
    public string BreakdownTitle { get; init; } = "Breakdown — AION2 DPS Meter";
    public string HistoryTitle { get; init; } = "Fight history — AION2 DPS Meter";
    public string TipTargetMode { get; init; } = "Boss only / everything you hit";
    public string TipLock { get; init; } = "Lock position";
    public string TipSettings { get; init; } = "Settings";
    public string TipOpacity { get; init; } = "Background opacity";
    public string TipHide { get; init; } = "Hide overlay (Ctrl+Shift+D)";
    public string ModeBossOnly { get; init; } = "BOSS";
    public string ModeAll { get; init; } = "ALL";
    public string SecLanguage { get; init; } = "LANGUAGE";
    public string LanguageHint { get; init; } = "Interface, skill and monster names. Applied immediately.";
    public string SecOverlay { get; init; } = "OVERLAY";
    public string BarsRelative { get; init; } = "Bars relative to the top player";
    public string BarsRelativeHint { get; init; } = "The leader's bar is full and everyone else is sized against it. Off: bars show share of party damage.";
    public string ShowBossPanel { get; init; } = "Show boss HP bar";
    public string ShowGear { get; init; } = "Show GS / CP after names";
    public string ShowGearHint { get; init; } =
        "Gear score and combat power come with the party roster: your party members and you. Other players have none to show. " +
        "On a narrow overlay the columns step aside so the fight's numbers fit: widen it or make the rows smaller.";
    public string RowsShown { get; init; } = "Rows shown";
    public string BackgroundOpacity { get; init; } = "Background opacity";
    public string RowSize { get; init; } = "Row size";
    public string RowSizeHint { get; init; } = "Also right on the overlay: Ctrl + mouse wheel over the rows.";
    public string SecMeter { get; init; } = "METER";
    public string Measure { get; init; } = "Measure";
    public string MeasureBoss { get; init; } = "Boss only (falls back to all)";
    public string MeasureAll { get; init; } = "Everything you hit";
    public string IdleEnd { get; init; } = "End fight after idle (s)";
    public string IdleBoss { get; init; } = "… while boss alive (s)";
    public string SecHistory { get; init; } = "FIGHT HISTORY";
    public string SaveAuto { get; init; } = "Save fights automatically";
    public string WhichFights { get; init; } = "Which fights";
    public string HistBosses { get; init; } = "Bosses only";
    public string HistBossesLong { get; init; } = "Bosses + longer fights";
    public string HistAll { get; init; } = "Everything (5 s and longer)";
    public string LongerThan { get; init; } = "Longer than (s)";
    public string HistoryHint { get; init; } = "Saved fights stay pickable in the overlay (Fights menu or mouse wheel over the title) and in Fight history.";
    public string HistoryInfo { get; init; } = "{0} fights saved in {1}";
    public string SecCapture { get; init; } = "CAPTURE";
    public string NetworkAdapter { get; init; } = "Network adapter";
    public string AdapterAuto { get; init; } = "Automatic (recommended)";
    public string AdapterHint { get; init; } = "Automatic finds the adapter that carries the AION 2 connection (loopback for ping accelerators). Pick one only if your VPN hides it.";
    public string RecordPackets { get; init; } = "Record game packets to .pcap (for debugging the parser)";
    public string RecordHint { get; init; } = "Recordings contain your own game traffic only, including character names. Keep them private.";
    public string StatusPrefix { get; init; } = "Status: ";
    public string NpcapMissing { get; init; } = "Npcap not installed";
    public string NpcapNeeded { get; init; } =
        "AION2 DPS Meter reads the game's own network traffic through Npcap, a free capture driver — and it is not installed yet.\n\n" +
        "Install it from npcap.com with the default options (ExitLag and other ping boosters work with them too), then start the meter again.\n\n" +
        "Open the download page now?";
    public string SecHotkeys { get; init; } = "HOTKEYS";
    public string HkToggle { get; init; } = "Show / hide overlay";
    public string HkReset { get; init; } = "Restart meter";
    public string HkClick { get; init; } = "Click-through";
    public string HotkeysHint { get; init; } = "Format: Ctrl+Shift+D, Alt+F9 … Applied after saving.";
    public string SecData { get; init; } = "DATA";
    public string DownloadIcons { get; init; } = "Download skill icons and boss portraits";
    public string IconsHint { get; init; } = "Skill icons: official AION 2 CDN (assets.playnccdn.com). Boss portraits: MetaBot.GG (metabot.gg). Each picture is fetched once, then cached.";
    public string DataInfo { get; init; } = "Skill names: {0:#,0} · NPCs: {1:#,0} ({2:#,0} bosses) · DoT list: {3:#,0} · Maps: {4:#,0}\nTables come from GPL-3.0 projects (see data/NOTICE.md). skills.json / npcs.json in the data folder override them.";
    public string OpenDataFolder { get; init; } = "Open data folder";
    public string OpenAppFolder { get; init; } = "Open app folder";
    public string Logs { get; init; } = "Logs";
    public string Disclaimer { get; init; } = "AION2 DPS Meter only listens to your own network connection. It never reads game memory, injects anything or sends a byte to the game. Third-party tools may still be against the game's terms of service — use at your own risk.";
    public string Cancel { get; init; } = "Cancel";
    public string Save { get; init; } = "Save";

    // Updates
    public string UpdateTitle { get; init; } = "Update available";
    public string UpdateHeadline { get; init; } = "AION2 DPS Meter {0} is out";
    public string UpdateYouHave { get; init; } = "You have {0}.";
    public string UpdateReleased { get; init; } = "You have {0}. The new one was released {1}.";
    public string UpdateWhatsNew { get; init; } = "WHAT'S NEW";
    public string UpdateInstalledHint { get; init; } = "The meter closes, the installer updates it and starts it again. Settings, fight history and timers stay.";
    public string UpdatePortableHint { get; init; } = "This copy was unzipped by hand, so it cannot replace itself: download the new zip and unzip it over this folder, or switch to the installer.";
    public string UpdatePortableManualHint { get; init; } = "This release has no update archive. Open the release page to download the latest version.";
    public string UpdateStartingPortable { get; init; } = "Closing and replacing the portable copy…";
    public string UpdateNow { get; init; } = "Update";
    public string UpdateOpenPage { get; init; } = "Open download page";
    public string UpdateLater { get; init; } = "Later";
    public string UpdateSkip { get; init; } = "Skip this version";
    public string UpdateDownloading { get; init; } = "Downloading the installer… {0:0}%";
    public string UpdateStarting { get; init; } = "Starting the installer…";
    public string UpdateFailed { get; init; } = "Could not update: {0}. The new version can be downloaded from the release page.";
    public string UpdateBanner { get; init; } = "Version {0} is out — click to update";
    public string TipUpdate { get; init; } = "What's new, and update in one click";
    public string UpdateBalloon { get; init; } = "Version {0} is out. Click here to update.";
    public string UpdatedBalloon { get; init; } = "Updated to version {0}.";
    public string TrayUpdate { get; init; } = "Update to {0}…";
    public string TrayCheckUpdates { get; init; } = "Check for updates";
    public string SecUpdates { get; init; } = "UPDATES";
    public string CheckUpdatesAuto { get; init; } = "Check GitHub for new versions";
    public string UpdatesHint { get; init; } = "Shortly after start and every 2 hours. The meter only asks GitHub which version is the newest; nothing about you or your fights is sent.";
    public string AutoInstallUpdates { get; init; } = "Install updates by itself";
    public string AutoInstallHint { get; init; } = "A new version is downloaded in the background and installed when you are not playing (or after 10 minutes without a fight): the meter restarts by itself, without any window. Installed copies only.";
    public string UpdateBannerReady { get; init; } = "Update {0} is ready — it installs itself when you are not playing";
    public string UpdateReadyBalloon { get; init; } = "Version {0} is downloaded and installs itself when you are not playing. Click here to install it now.";
    public string VersionInstalled { get; init; } = "Version {0} · installed";
    public string VersionPortable { get; init; } = "Version {0} · portable (zip)";
    public string CheckNow { get; init; } = "Check now";
    public string UpdateChecking { get; init; } = "Checking…";
    public string UpToDate { get; init; } = "You have the newest version.";
    public string UpdateFound { get; init; } = "Version {0} is out.";
    public string UpdateCheckFailed { get; init; } = "Could not reach GitHub: {0}";

    private static readonly UiText De = new()
    {
        Code = "de", LanguageName = "Deutsch",
        MapLabel = "Karte",
        ClassNames = new Dictionary<Core.Events.GameClass, string>
        {
            [Core.Events.GameClass.Gladiator] = "Gladiator", [Core.Events.GameClass.Templar] = "Templer",
            [Core.Events.GameClass.Ranger] = "Waldläufer", [Core.Events.GameClass.Assassin] = "Assassine",
            [Core.Events.GameClass.Sorcerer] = "Zauberer", [Core.Events.GameClass.Elementalist] = "Beschwörer",
            [Core.Events.GameClass.Cleric] = "Kleriker", [Core.Events.GameClass.Chanter] = "Kantor",
            [Core.Events.GameClass.Brawler] = "Faustkämpfer",
        },
        FlagNames = new Dictionary<Core.Events.HitFlags, string>
        {
            [Core.Events.HitFlags.Critical] = "Kritisch", [Core.Events.HitFlags.Back] = "Rücken",
            [Core.Events.HitFlags.Front] = "Frontal", [Core.Events.HitFlags.Perfect] = "Perfekt",
            [Core.Events.HitFlags.Heavy] = "Vernichtend", [Core.Events.HitFlags.Multi] = "Mehrfachtreffer",
            [Core.Events.HitFlags.Double] = "Doppeltreffer", [Core.Events.HitFlags.Block] = "Blocken",
            [Core.Events.HitFlags.Parry] = "Parieren", [Core.Events.HitFlags.Endure] = "Widerstehen",
            [Core.Events.HitFlags.Evade] = "Ausweichen", [Core.Events.HitFlags.Dot] = "Schaden über Zeit",
        },
        Chat = new Core.ChatWords
        {
            Kill = "SIEG", Wipe = "NIEDERLAGE", BossLeft = "Boss {0}", Fighting = "{0} greifen an",
            DamageSentBy = "Schaden von {0} übertragen", Party = "Gruppe {0}", Place = "#{0} von {1}",
            Damage = "{0} Schaden", Crit = "Kritisch {0}", TopHit = "Höchster Treffer {0}",
            More = "+{0} weitere", ColPlayer = "Spieler", ColDps = "DPS", ColDamage = "Schaden",
            ColShare = "Anteil", GearScore = "GS", CombatPower = "KK",
        },
        TipSkillDamage = "Schaden", TipSkillHits = "Treffer", TipSkillDotTicks = "DoT-Ticks", TipSkillMin = "Min.", TipSkillAvg = "Ø", TipSkillMax = "Max.",
        UnknownSummons = "Beschwörungen (Besitzer unbekannt)",
        UnknownSummonsTip = "Begleiter, Geister und Fertigkeitseffekte, deren Urheber nicht ermittelt werden konnte. Keine Spieler; sie zählen nicht für Rang oder Spielersumme.",
        TimersTitle = "Boss-Timer", TimersButton = "Boss-Timer", HkTimers = "Boss-Timer", TipTimers = "Boss-Wiederbelebungszeiten", TrayTimers = "Boss-Timer…",
        TimersHint = "Öffne im Spiel auf der Karte die Liste der Feldbosse (Karte → Erkundung → Feldmonster). Die Zeiten kommen direkt vom Server. Beobachtete Boss-Tode starten den Countdown neu. Quest- und Instanzbosse haben keinen Welt-Timer.",
        FilterWatched = "🔔 Beobachtet", TipWatch = "Vor dem Wiedererscheinen dieses Bosses benachrichtigen", UnknownSlotBoss = "Feldboss #{0}",
        SourceGame = "Aus der Bossliste im Spiel (gelesen um {0})", SourceMeter = "Vom Meter erfasst (beobachtete Tode)", RespawnFromGame = "aus dem Spiel",
        TimerAliveSince = "seit {0} aktiv", TimerNoTime = "kein Timer im Spiel", TipServer = "Jeder Server hat eigene Bosse. Dies sind die Zeiten deines Servers.",
        ServerUnknown = "Server ?", ServerYours = "dein Charakter", MapNumber = "Karte {0}",
        TimersEmpty = "Noch keine Timer. Besiege einen Weltboss oder beobachte seinen Tod, damit der Countdown startet.",
        TimerAlive = "AKTIV", TimerDue = "SOLLTE AKTIV SEIN", TimerSeen = "gesehen {0}", TimerSince = "seit {0}", TimerAt = "um {0}",
        TimerKilled = "besiegt {0}", TimerFoundDead = "tot gefunden {0}", TimerNeedsInterval = "Wiederkehrzeit festlegen",
        TimerLearned = "Vom Meter ermittelt: kürzeste Zeit zwischen dem Tod und dem erneuten Erscheinen des Bosses",
        TipRespawn = "Wiederkehrzeit nach dem Besiegen", RespawnUnknown = "Wiederkehr ?", BtnKilled = "Besiegt",
        TipKilled = "Gerade besiegt — Countdown starten", TipRemoveTimer = "Aus der Liste entfernen", AlertLabel = "Vor dem Wiedererscheinen warnen",
        AlertOff = "aus", AlertSoon = "{0} erscheint voraussichtlich in {1} Min. ({2})", AlertNow = "{0} sollte jetzt erscheinen ({1})",
        HoursShort = "Std.", MinutesShort = "Min.",
        EventTimersTitle = "Event- und Weltboss-Timer",
        EventTimersHint = "Countdowns nach deiner PC-Uhr. Wiederkehrende Zeiten beruhen auf Community-Beobachtungen; beachte Spielmeldungen. Feldboss-Timer kommen direkt aus dem Spiel und stehen unter Boss-Timer.",
        EventRift = "Raumzeit-Riss", EventShugo = "Shugo-Festival", EventInvasion = "Dimensionale Invasion",
        EventWatcher = "Wächter Kaira (Weltboss)", EventSiege = "Artefaktbelagerung", EventExecutors = "Abyss-Belagerungsbosse",
        EventNahma = "Wächtergeneral Nahma (Weltboss)", EventArena = "Arena der Taktik", EventNext = "NÄCHSTER START",
        EventAt = "Ortszeit: {0}", EventSource = "Zeitplan: Community-Beobachtungen · Feldbosse: Timer im Spiel",
        EventOpacity = "Deckkraft", EventCompact = "Timer einklappen", EventExpand = "Timer ausklappen",
        AutoShow = "Bei Kampfbeginn automatisch anzeigen", AutoShowHint = "Wenn in der Nähe ein Boss bekämpft wird oder du ein Monster triffst. Während des Kampfes bleibt das Meter sichtbar.",
        AutoHide = "Nach dem Kampf ausblenden (Sek., 0 = nie)", ShowOnStart = "Overlay beim Start des Meters oder Spiels anzeigen",
        ColPlayer = "SPIELER", ColDps = "DPS", ColDamage = "SCHADEN", ColShare = "ANTEIL", HealShort = "Heilung", TipPlace = "Rang in diesem Kampf nach verursachtem Schaden",
        TipDps = "Kampfdurchschnitt: Gesamtschaden geteilt durch die Zeit vom ersten bis zum letzten Treffer", TipDamage = "Gesamter verursachter Schaden in diesem Kampf", TipShare = "Anteil am Gruppenschaden",
        TipGs = "Ausrüstungsstufe aus der Gruppenliste", TipCp = "Kampfkraft aus der Gruppenliste",
        LiveFight = "laufender Kampf", LastFight = "letzter Kampf", SavedFight = "gespeicherter Kampf", ThisSession = "diese Sitzung",
        WaitingLabel = "warte auf Kampf", WaitingTitle = "Warte auf einen Kampf", WaitingDetail = "Triff ein Ziel — das Meter startet automatisch",
        EmptyParty = "Deine Gruppe erscheint hier, sobald jemand einen Treffer landet.", TopHit = "STÄRKSTER TREFFER", TopHitBy = "Größter Einzeltreffer — {0}",
        TipScrollFights = "Mausrad: zwischen Kämpfen wechseln", Defeated = "Besiegt", UnknownBoss = "Unbekannter Boss", CountedFrom = "ab {0} LP gezählt",
        HpUnknown = "Max. LP unbekannt", TimeLimitTip = "Zeitlimit (laut Spiel): endet um {0}", BossLimitTip = "Boss-Zeitlimit (laut Spiel): besiege ihn vor {0}",
        FightingCount = "{0} greifen an", DamageSentBy = "Schaden von {0} übertragen",
        DamageUnsentTip = "Bei Weltbossen mit vielen Spielern sendet das Spiel nur für wenige Spieler vollständige Schadenswerte. Für andere fehlen Zahlen oder DoT-Schaden. Daher sind deren Werte und Anteile hier niedriger als tatsächlich; dieses Meter kann die fehlenden Daten nicht sehen.",
        TipRestart = "Meter neu starten ({0}): bisherigen Kampf speichern und Zählung neu beginnen", InProgress = "Läuft", Kill = "Sieg", Wipe = "Niederlage", Ended = "Beendet",
        OpenWorld = "Offene Welt", OpenFight = "Kampf öffnen", Fights = "Kämpfe", NamesLater = "Namen nach dem nächsten Ladebildschirm", YourPlace = "Dein Rang in diesem Kampf", Party = "Gruppe", Seconds = "Sek.",
        SwitchLanguage = "Sprache wechseln", TrayHide = "Overlay ausblenden", TrayShow = "Overlay anzeigen", TrayHistory = "Kampfverlauf…", TraySettings = "Einstellungen…",
        TrayDemo = "Beispielkampf starten", TrayReplay = "Aufzeichnung wiedergeben…", TrayExit = "Beenden",
        ClickThroughOn = "Klickdurchlässig EIN — Overlay ignoriert die Maus. {0} schaltet es aus.", ClickThroughOff = "Klickdurchlässig AUS",
        Live = "Live", LiveHint = "zeigt den aktuellen Kampf", SessionSection = "DIESE SITZUNG", SavedToday = "GESPEICHERT · HEUTE", SavedYesterday = "GESPEICHERT · GESTERN",
        SavedOn = "GESPEICHERT · {0}", AllSaved = "Alle gespeicherten Kämpfe ({0})…", NoFights = "Noch keine Kämpfe — sie werden automatisch gespeichert",
        OpenBreakdown = "Auswertung öffnen", CopyPlayer = "Ergebnis von {0} in den Chat kopieren", CopyParty = "Gruppenrangliste in den Chat kopieren",
        CopyTable = "Gruppentabelle für Discord kopieren", Copied = "Kopiert — mit Strg+V in den Chat einfügen", CopyFailed = "Zwischenablage belegt — bitte erneut versuchen",
        TipChat = "Ergebnis für Chat oder Discord", RowDamage = "Schaden", RowHits = "Treffer", RowCrit = "Kritisch", RowMax = "Max.",
        Players = n => n == 1 ? "1 Spieler" : $"{n} Spieler",
        TabDps = "DPS", TabAccuracy = "Trefferquote", TabRotation = "Rotation", TabDefense = "Verteidigung", TabHealing = "Heilung", HealingTotal = "GESAMTHEILUNG", HealingSkill = "FERTIGKEIT", HealingEvents = "ANWENDUNGEN", HealingAmount = "GEHEILT", TilePlace = "RANG", TileDamage = "SCHADEN", TileDps = "DPS", TileContrib = "ANTEIL", TileTime = "ZEIT",
        DpsTimeline = "DPS-VERLAUF", ThSkill = "FERTIGKEIT", ThHits = "TREFFER", ThDamage = "SCHADEN", ThDps = "DPS", ThAvg = "Ø", ThMax = "MAX.", ThCrit = "KRIT.", ThShare = "ANTEIL", ThBack = "RÜCKEN", ThFront = "FRONTAL", ThPerfect = "PERFEKT", ThSmite = "VERNICHTEND", ThMulti = "MEHRFACH",
        SortTip = "Nach dieser Spalte sortieren · erneut klicken zum Umkehren", QHitsMeasured = "{0} Treffer gemessen", QPositional = "Positionstreffer", QExtraHits = "Zusatztreffer", QBiggestHit = "STÄRKSTER TREFFER",
        DefTaken = "ERLITTENER SCHADEN", DefTakenPerSec = "SCHADEN / SEK.", DefDotTicks = "DOT-TICKS", DefHits = "{0} Treffer", DefDot = "Schaden über Zeit",
        DefNote = "Erlittener Schaden umfasst NPC-Treffer während des Kampfes. Details zu Blocken, Parieren und Ausweichen hängen von den Serverdaten ab.",
        RotationHint = "Jeder Einsatz in Reihenfolge. Goldener Ring = kritischer Treffer · Kachelgröße entspricht Schaden · DoT-Ticks ausgeblendet.", You = "du", PartyDpsSuffix = "Gruppe", TipCopyImage = "Als Bild kopieren", TipMinimize = "Minimieren", TipClose = "Schließen",
        YourFights = "Deine Kämpfe", SavedCount = "{0} gespeichert", ShownOf = "{0} von {1}", FilterAll = "Alle", FilterBosses = "Bosse", FilterKills = "Siege",
        ShowInOverlay = "Im Overlay anzeigen", TipShowInOverlay = "Diesen Kampf im Overlay anzeigen (Live-Modus kehrt beim nächsten Pull zurück)", HistoryEmpty = "Besiege einen Boss — der Kampf erscheint hier automatisch.",
        PlayersShort = "Sp.", JustNow = "gerade eben", MinutesAgo = "vor {0} Min.", HoursAgo = "vor {0} Std.", DeleteConfirm = "„{0}“ ({1}) löschen?", TipRefresh = "Aktualisieren", TipOpenFolder = "Ordner öffnen", TipDelete = "Ausgewählten Kampf löschen", TipOpenBreakdown = "Vollständige Auswertung öffnen", DamageWord = "Schaden",
        SettingsTitle = "Einstellungen", SecLanguage = "SPRACHE", LanguageHint = "Oberfläche, Fertigkeits- und Monsternamen. Wird sofort angewendet.", SecOverlay = "OVERLAY",
        LanguageLabel = "Sprache", BreakdownTitle = "Kampfauswertung — AION2 DPS Meter", HistoryTitle = "Kampfverlauf — AION2 DPS Meter",
        TipTargetMode = "Nur Boss / alle getroffenen Ziele", TipLock = "Position fixieren", TipSettings = "Einstellungen",
        TipOpacity = "Hintergrundtransparenz", TipHide = "Overlay ausblenden (Strg+Umschalt+D)", ModeBossOnly = "BOSS", ModeAll = "ALLE",
        BarsRelative = "Balken relativ zum stärksten Spieler", BarsRelativeHint = "Der Spitzenreiter erhält einen vollen Balken, alle anderen werden daran gemessen. Aus: Balken zeigen den Anteil am Gruppenschaden.",
        ShowBossPanel = "Boss-LP-Balken anzeigen", ShowGear = "GS / KK nach Namen anzeigen", ShowGearHint = "Ausrüstungsstufe und Kampfkraft kommen aus der Gruppenliste. Auf schmalen Overlays weichen die Spalten aus; verbreitere das Fenster oder verkleinere die Zeilen.",
        RowsShown = "Angezeigte Zeilen", BackgroundOpacity = "Hintergrundtransparenz", RowSize = "Zeilengröße", RowSizeHint = "Auch direkt im Overlay: Strg + Mausrad über den Zeilen.", SecMeter = "METER", Measure = "Messung", MeasureBoss = "Nur Boss (sonst alle Ziele)", MeasureAll = "Alle getroffenen Ziele", IdleEnd = "Kampf nach Inaktivität beenden (Sek.)", IdleBoss = "… solange Boss lebt (Sek.)",
        SecHistory = "KAMPFVERLAUF", SaveAuto = "Kämpfe automatisch speichern", WhichFights = "Welche Kämpfe", HistBosses = "Nur Bosse", HistBossesLong = "Bosse und längere Kämpfe", HistAll = "Alle (ab 5 Sek.)", LongerThan = "Länger als (Sek.)",
        HistoryHint = "Gespeicherte Kämpfe bleiben im Overlay auswählbar (Kämpfe-Menü oder Mausrad über dem Titel) und im Kampfverlauf.", HistoryInfo = "{0} Kämpfe gespeichert in {1}",
        SecCapture = "ERFASSUNG", NetworkAdapter = "Netzwerkadapter", AdapterAuto = "Automatisch (empfohlen)", AdapterHint = "Erkennt automatisch den Adapter der AION-2-Verbindung (Loopback für Ping-Booster). Nur ändern, wenn dein VPN ihn verbirgt.",
        RecordPackets = "Spielpakete als .pcap aufzeichnen (Parser-Diagnose)", RecordHint = "Aufzeichnungen enthalten nur deinen eigenen Spielverkehr, einschließlich Charakternamen. Halte sie privat.", StatusPrefix = "Status: ", NpcapMissing = "Npcap nicht installiert",
        NpcapNeeded = "AION2 DPS Meter liest den Netzwerkverkehr des Spiels über Npcap, einen kostenlosen Erfassungstreiber. Er ist noch nicht installiert.\n\nInstalliere Npcap von npcap.com mit den Standardeinstellungen und starte das Meter erneut.\n\nDownloadseite jetzt öffnen?",
        SecHotkeys = "TASTENKÜRZEL", HkToggle = "Overlay anzeigen / ausblenden", HkReset = "Meter neu starten", HkClick = "Klickdurchlässigkeit", HotkeysHint = "Format: Strg+Umschalt+D, Alt+F9 … Wird nach dem Speichern angewendet.",
        SecData = "DATEN", DownloadIcons = "Fertigkeitssymbole und Boss-Porträts herunterladen", IconsHint = "Fertigkeitssymbole: offizieller AION-2-CDN (assets.playnccdn.com). Boss-Porträts: MetaBot.GG. Jedes Bild wird einmal geladen und dann zwischengespeichert.",
        DataInfo = "Fertigkeiten: {0:#,0} · NPCs: {1:#,0} ({2:#,0} Bosse) · DoT-Liste: {3:#,0} · Karten: {4:#,0}\nTabellen aus GPL-3.0-Projekten (siehe data/NOTICE.md). skills.json / npcs.json im Datenordner überschreiben diese.",
        OpenDataFolder = "Datenordner öffnen", OpenAppFolder = "Programmordner öffnen", Logs = "Protokolle", Disclaimer = "AION2 DPS Meter überwacht nur deine eigene Netzwerkverbindung. Es liest keinen Spielspeicher, schleust nichts ein und sendet nichts an das Spiel. Drittanbieter-Tools können dennoch gegen die Spielregeln verstoßen — Nutzung auf eigene Verantwortung.", Cancel = "Abbrechen", Save = "Speichern",
        UpdateTitle = "Update verfügbar", UpdateHeadline = "AION2 DPS Meter {0} ist verfügbar", UpdateYouHave = "Installiert: {0}.", UpdateReleased = "Installiert: {0}. Die neue Version erschien am {1}.", UpdateWhatsNew = "NEU IN DIESER VERSION",
        UpdateInstalledHint = "Das Meter schließt sich, der Installer aktualisiert es und startet es neu. Einstellungen, Kampfverlauf und Timer bleiben erhalten.", UpdatePortableHint = "Das Update wird geladen, die portable Version ersetzt und Aetherion anschließend neu gestartet.", UpdatePortableManualHint = "Für diese Version gibt es kein Update-Archiv. Öffne die Release-Seite, um die aktuelle Version herunterzuladen.", UpdateStartingPortable = "Portable Version wird ersetzt…",
        UpdateNow = "Aktualisieren", UpdateOpenPage = "Downloadseite öffnen", UpdateLater = "Später", UpdateSkip = "Diese Version überspringen", UpdateDownloading = "Update wird heruntergeladen… {0:0}%", UpdateStarting = "Installer wird gestartet…",
        UpdateFailed = "Update fehlgeschlagen: {0}. Die neue Version findest du auf der Release-Seite.", UpdateBanner = "Version {0} verfügbar — zum Aktualisieren klicken", TipUpdate = "Neuigkeiten und Update mit einem Klick", UpdateBalloon = "Version {0} ist verfügbar. Zum Aktualisieren hier klicken.", UpdatedBalloon = "Auf Version {0} aktualisiert.",
        TrayUpdate = "Auf {0} aktualisieren…", TrayCheckUpdates = "Nach Updates suchen", SecUpdates = "UPDATES", CheckUpdatesAuto = "GitHub auf neue Versionen prüfen", UpdatesHint = "Kurz nach dem Start und alle 2 Stunden. Das Meter fragt GitHub nur nach der neuesten Version; keine Daten über dich oder deine Kämpfe werden gesendet.",
        AutoInstallUpdates = "Updates automatisch installieren", AutoInstallHint = "Neue Versionen werden im Hintergrund geladen und installiert, wenn du nicht spielst (oder 10 Minuten lang kein Kampf läuft). Nur bei installierter Version.",
        UpdateBannerReady = "Update {0} bereit — Installation erfolgt automatisch, wenn du nicht spielst", UpdateReadyBalloon = "Version {0} wurde geladen und wird automatisch installiert, wenn du nicht spielst. Hier klicken, um sofort zu installieren.",
        VersionInstalled = "Version {0} · installiert", VersionPortable = "Version {0} · portabel (ZIP)", CheckNow = "Jetzt prüfen", UpdateChecking = "Suche…", UpToDate = "Du hast die neueste Version.", UpdateFound = "Version {0} ist verfügbar.", UpdateCheckFailed = "GitHub nicht erreichbar: {0}",
    };

    private static readonly UiText Ru = new()
    {
        Code = "ru",
        LanguageName = "Русский",
        MapLabel = "Карта",
        ClassNames = new Dictionary<Core.Events.GameClass, string>
        {
            [Core.Events.GameClass.Gladiator] = "Гладиатор",
            [Core.Events.GameClass.Templar] = "Страж",
            [Core.Events.GameClass.Ranger] = "Стрелок",
            [Core.Events.GameClass.Assassin] = "Убийца",
            [Core.Events.GameClass.Sorcerer] = "Волшебник",
            [Core.Events.GameClass.Elementalist] = "Заклинатель",
            [Core.Events.GameClass.Cleric] = "Целитель",
            [Core.Events.GameClass.Chanter] = "Чародей",
            [Core.Events.GameClass.Brawler] = "Боец",
        },
        FlagNames = new Dictionary<Core.Events.HitFlags, string>
        {
            [Core.Events.HitFlags.Critical] = "Крит",
            [Core.Events.HitFlags.Back] = "Со спины",
            [Core.Events.HitFlags.Front] = "Спереди",
            [Core.Events.HitFlags.Perfect] = "Идеальный",
            [Core.Events.HitFlags.Heavy] = "Сокрушение",
            [Core.Events.HitFlags.Multi] = "Доп. удары",
            [Core.Events.HitFlags.Double] = "Двойной",
            [Core.Events.HitFlags.Block] = "Блок",
            [Core.Events.HitFlags.Parry] = "Парирование",
            [Core.Events.HitFlags.Endure] = "Стойкость",
            [Core.Events.HitFlags.Evade] = "Уклонение",
            [Core.Events.HitFlags.Dot] = "DoT",
        },
        UnknownSummons = "Призывы (владелец неизвестен)",
        UnknownSummonsTip = "Питомцы, духи и эффекты умений (Леденящий ветер, Стена огня …), хозяина которых определить не удалось. Это не игрок: в местах и числе игроков не учитывается.",

        TimersTitle = "Таймеры боссов",
        TimersButton = "Таймеры боссов",
        HkTimers = "Таймеры боссов",
        TipTimers = "Таймеры респауна боссов",
        TrayTimers = "Таймеры боссов…",
        TimersHint = "Откройте в игре список полевых боссов карты (карта → Исследование → полевые монстры) — таймеры придут прямо с сервера. Убийство полевого босса рядом с вами перезапускает его отсчёт. У боссов квестовых и запечатанных инстансов нет общего таймера — их здесь нет. 🔔 — оповещать перед респауном.",
        FilterWatched = "🔔 Отслеживаемые",
        TipWatch = "Оповещать перед респауном этого босса",
        UnknownSlotBoss = "Полевой босс №{0}",
        SourceGame = "По списку боссов в игре (обновлено в {0})",
        SourceMeter = "Посчитано метром (по убийствам, которые он видел)",
        RespawnFromGame = "из игры",
        TimerAliveSince = "с {0}",
        TimerNoTime = "в игре нет таймера",
        TipServer = "На каждом сервере свои боссы: здесь таймеры этого сервера (вашего персонажа, если не выбран другой)",
        ServerUnknown = "сервер ?",
        ServerYours = "ваш персонаж",
        MapNumber = "Карта {0}",
        TimersEmpty = "Таймеров пока нет. Убейте полевого босса (или увидьте, как его убили) — здесь начнётся отсчёт.",
        TimerAlive = "ЖИВ",
        TimerDue = "ДОЛЖЕН БЫТЬ",
        TimerSeen = "замечен {0}",
        TimerSince = "с {0}",
        TimerAt = "в {0}",
        TimerKilled = "убит {0}",
        TimerFoundDead = "найден мёртвым {0}",
        TimerNeedsInterval = "задайте респаун",
        TimerLearned = "Измерено метром: самое короткое время от убийства до появления босса",
        TipRespawn = "Время респауна после убийства",
        RespawnUnknown = "респаун ?",
        BtnKilled = "Убит",
        TipKilled = "Убит только что — запустить отсчёт",
        TipRemoveTimer = "Убрать из списка",
        AlertLabel = "Оповещать до респауна",
        AlertOff = "выкл",
        AlertSoon = "{0} появится примерно через {1} мин ({2})",
        AlertNow = "{0} уже должен появиться ({1})",
        HoursShort = "ч",
        MinutesShort = "мин",
        EventTimersTitle = "Расписание событий и мировых боссов",
        EventTimersHint = "Обратный отсчёт по местному времени ПК. Расписание составлено по наблюдениям сообщества; сверяйтесь с игровыми уведомлениями. Таймеры полевых боссов напрямую читаются из игры в разделе «Таймеры боссов». ",
        EventRift = "Разлом пространства-времени", EventShugo = "Фестиваль Шиго", EventInvasion = "Вторжение измерений",
        EventWatcher = "Страж Кайра (мировой босс)", EventSiege = "Осада артефакта", EventExecutors = "Боссы Бездны",
        EventNahma = "Генерал-страж Нахма (мировой босс)", EventArena = "Арена тактики", EventNext = "СЛЕДУЮЩИЙ СТАРТ",
        EventAt = "Местное время: {0}", EventSource = "Расписание: наблюдения сообщества · Полевые боссы: таймер игры",
        EventOpacity = "Прозрачность", EventCompact = "Свернуть таймеры", EventExpand = "Развернуть таймеры",

        AutoShow = "Показывать сам, когда начинается бой",
        AutoShowHint = "Когда рядом бьют босса или вы ударили любого моба. Если скрыть его во время боя — появится в следующем.",
        AutoHide = "Скрывать после боя через (с, 0 — нет)",
        ShowOnStart = "Показывать оверлей при запуске метра и игры",

        TipSkillDamage = "Урон",
        TipSkillHits = "Удары",
        TipSkillDotTicks = "Тики DoT",
        TipSkillMin = "Мин",
        TipSkillAvg = "Сред",
        TipSkillMax = "Макс",

        ColPlayer = "ИГРОК",
        ColDps = "УРОН/С",
        ColDamage = "ВСЕГО",
        ColShare = "ДОЛЯ",
        HealShort = "Леч.",
        TipPlace = "Место в бою по нанесённому урону",
        TipDps = "Средний DPS за бой: весь урон, делённый на время от первого до последнего удара",
        TipDamage = "Весь урон, нанесённый за бой",
        TipShare = "Доля от урона всей группы",
        TipGs = "Уровень экипировки (GS), из состава группы",
        TipCp = "Боевая мощь (CP), из состава группы",

        LiveFight = "идёт бой",
        LastFight = "последний бой",
        SavedFight = "сохранённый бой",
        ThisSession = "этот сеанс",
        WaitingLabel = "ожидание боя",
        WaitingTitle = "Ожидание боя",
        WaitingDetail = "Атакуйте цель — метр запустится сам",
        EmptyParty = "Группа появится здесь после первого удара.",
        TopHit = "МАКС. УДАР",
        TopHitBy = "Самый сильный удар — {0}",
        TipScrollFights = "Колесо мыши здесь: листать бои",
        Defeated = "Повержен",
        UnknownBoss = "Неизвестный босс",
        CountedFrom = "учтено с {0} HP",
        HpUnknown = "макс. HP неизвестен",
        TimeLimitTip = "Лимит времени (из игры): закончится в {0}",
        BossLimitTip = "Лимит времени босса (из игры): убить до {0}",
        FightingCount = "{0} бьют босса",
        DamageSentBy = "урон виден у {0}",
        DamageUnsentTip = "На мировом боссе с толпой игра присылает вашему клиенту урон лишь нескольких игроков, в том числе ваш. Про остальных приходит только «удар попал» — без числа — или их периодический урон. Поэтому их цифры и все доли здесь намного ниже настоящих; метр на этом компьютере больше увидеть не может.",
        TipRestart = "Перезапустить метр ({0}): бой сохранится, подсчёт начнётся заново",
        InProgress = "Идёт бой",
        Kill = "Убит",
        Wipe = "Вайп",
        Ended = "Завершён",
        OpenWorld = "Открытый мир",
        OpenFight = "Разбор боя",
        Fights = "Бои",
        NamesLater = "ники появятся после загрузки локации",
        YourPlace = "Ваше место в этом бою",
        Party = "группа",
        Seconds = "с",
        SwitchLanguage = "Язык: Русский → Deutsch",

        TrayHide = "Скрыть оверлей",
        TrayShow = "Показать оверлей",
        TrayHistory = "История боёв…",
        TraySettings = "Настройки…",
        TrayDemo = "Демо-бой",
        TrayReplay = "Воспроизвести запись…",
        TrayExit = "Выход",
        ClickThroughOn = "Клики сквозь оверлей ВКЛ — он не реагирует на мышь. {0} — выключить.",
        ClickThroughOff = "Клики сквозь оверлей ВЫКЛ",

        Live = "Текущий бой",
        LiveHint = "следит за боем в реальном времени",
        SessionSection = "ЭТОТ СЕАНС",
        SavedToday = "СОХРАНЕНО · СЕГОДНЯ",
        SavedYesterday = "СОХРАНЕНО · ВЧЕРА",
        SavedOn = "СОХРАНЕНО · {0}",
        AllSaved = "Все сохранённые бои ({0})…",
        NoFights = "Боёв пока нет — они сохраняются автоматически",
        OpenBreakdown = "Открыть разбор",
        CopyPlayer = "Скопировать результат {0} в чат",
        CopyParty = "Скопировать рейтинг группы в чат",
        CopyTable = "Скопировать таблицу группы для Discord",
        Copied = "Скопировано — вставьте в чат: Ctrl+V",
        CopyFailed = "Буфер обмена занят — попробуйте ещё раз",
        TipChat = "Результат в чат или Discord",
        Chat = new Core.ChatWords
        {
            Kill = "УБИТ",
            Wipe = "ВАЙП",
            BossLeft = "босс {0}",
            Fighting = "бьют {0}",
            DamageSentBy = "урон виден у {0}",
            Party = "группа {0}",
            Place = "{0}-й из {1}",
            Damage = "урон {0}",
            Crit = "крит {0}",
            TopHit = "макс. удар {0}",
            More = "+ещё {0}",
            ColPlayer = "Игрок",
            ColDps = "DPS",
            ColDamage = "Урон",
            ColShare = "Доля",
        },
        RowDamage = "Урон",
        RowHits = "Удары",
        RowCrit = "Крит",
        RowMax = "Макс",
        Players = n => $"{n} {RuPlural(n, "игрок", "игрока", "игроков")}",

        TabDps = "Урон",
        TabAccuracy = "Точность",
        TabRotation = "Ротация",
        TabDefense = "Защита",
        TabHealing = "Лечение",
        HealingTotal = "ВСЕГО ЛЕЧЕНИЯ",
        HealingSkill = "УМЕНИЕ",
        HealingEvents = "ПРИМЕНЕНИЯ",
        HealingAmount = "ЛЕЧЕНИЕ",
        TilePlace = "МЕСТО",
        TileDamage = "УРОН",
        TileDps = "DPS",
        TileContrib = "ВКЛАД",
        TileTime = "ВРЕМЯ",
        DpsTimeline = "DPS ПО ВРЕМЕНИ",
        ThSkill = "УМЕНИЕ",
        ThHits = "УДАРЫ",
        ThDamage = "УРОН",
        ThDps = "DPS",
        ThAvg = "СРЕДН.",
        ThMax = "МАКС.",
        ThCrit = "КРИТ",
        ThShare = "ДОЛЯ",
        ThBack = "СПИНА",
        ThFront = "ФРОНТ",
        ThPerfect = "ИДЕАЛ",
        ThSmite = "СОКРУШ.",
        ThMulti = "МУЛЬТИ",
        SortTip = "Сортировать по этой колонке · ещё щелчок — в обратном порядке",
        QHitsMeasured = "ударов учтено: {0}",
        QPositional = "по позиции",
        QExtraHits = "доп. удары",
        QBiggestHit = "МАКС. УДАР",
        DefTaken = "ПОЛУЧЕНО УРОНА",
        DefTakenPerSec = "ПОЛУЧЕНО / С",
        DefDotTicks = "ТИКИ DOT",
        DefHits = "ударов: {0}",
        DefDot = "периодический урон",
        DefNote = "Полученный урон — удары от NPC за время этого боя. Детали блока / парирования / уклонения зависят от того, что присылает сервер.",
        RotationHint = "Все применения по порядку. Золотое кольцо — крит · размер плитки зависит от урона · тики DoT скрыты.",
        You = "вы",
        PartyDpsSuffix = "группа",
        TipCopyImage = "Скопировать картинкой",
        TipMinimize = "Свернуть",
        TipClose = "Закрыть",

        YourFights = "Ваши бои",
        SavedCount = "сохранено: {0}",
        ShownOf = "{0} из {1}",
        FilterAll = "Все",
        FilterBosses = "Боссы",
        FilterKills = "Убийства",
        ShowInOverlay = "Показать в оверлее",
        TipShowInOverlay = "Показать этот бой в оверлее (с началом нового боя вернётся живой режим)",
        HistoryEmpty = "Убейте босса — бой сохранится здесь автоматически.",
        PlayersShort = "игр.",
        JustNow = "только что",
        MinutesAgo = "{0} мин назад",
        HoursAgo = "{0} ч назад",
        DeleteConfirm = "Удалить «{0}» ({1})?",
        TipRefresh = "Обновить",
        TipOpenFolder = "Открыть папку",
        TipDelete = "Удалить выбранный бой",
        TipOpenBreakdown = "Открыть полный разбор",
        DamageWord = "урона",

        SettingsTitle = "Настройки",
        LanguageLabel = "Язык",
        BreakdownTitle = "Разбор боя",
        HistoryTitle = "История боёв",
        TipTargetMode = "Только босс / все цели",
        TipLock = "Закрепить положение",
        TipSettings = "Настройки",
        TipOpacity = "Непрозрачность фона",
        TipHide = "Скрыть оверлей (Ctrl+Shift+D)",
        ModeBossOnly = "БОСС",
        ModeAll = "ВСЁ",
        SecLanguage = "ЯЗЫК",
        LanguageHint = "Интерфейс, названия умений и монстров. Применяется сразу.",
        SecOverlay = "ОВЕРЛЕЙ",
        BarsRelative = "Полосы относительно лидера",
        BarsRelativeHint = "Полоса лидера полная, остальные — пропорционально ему. Выключено: полосы показывают долю урона группы.",
        ShowBossPanel = "Показывать полосу HP босса",
        ShowGear = "Показывать GS / CP после ников",
        ShowGearHint = "GS и CP сервер присылает в составе группы: они есть у членов вашей группы и у вас. У остальных игроков их нет. " +
                       "На узком оверлее колонки прячутся, чтобы влезли цифры боя: сделайте оверлей шире или строки меньше.",
        RowsShown = "Строк в списке",
        BackgroundOpacity = "Плотность фона",
        RowSize = "Размер строк",
        RowSizeHint = "Или прямо на оверлее: Ctrl + колесо мыши над строками.",
        SecMeter = "МЕТР",
        Measure = "Считать",
        MeasureBoss = "Только босса (иначе всё)",
        MeasureAll = "Всё, по чему бьёте",
        IdleEnd = "Конец боя после простоя (с)",
        IdleBoss = "… пока босс жив (с)",
        SecHistory = "ИСТОРИЯ БОЁВ",
        SaveAuto = "Сохранять бои автоматически",
        WhichFights = "Какие бои",
        HistBosses = "Только боссы",
        HistBossesLong = "Боссы + длинные бои",
        HistAll = "Все (от 5 с)",
        LongerThan = "Длиннее (с)",
        HistoryHint = "Сохранённые бои можно выбрать в оверлее (меню «Бои» или колесо мыши над названием) и в окне истории.",
        HistoryInfo = "Сохранено боёв: {0} — {1}",
        SecCapture = "ЗАХВАТ",
        NetworkAdapter = "Сетевой адаптер",
        AdapterAuto = "Автоматически (рекомендуется)",
        AdapterHint = "Автоматически выбирается адаптер с соединением AION 2 (loopback для ускорителей пинга). Выбирайте вручную, только если VPN его скрывает.",
        RecordPackets = "Записывать игровые пакеты в .pcap (для отладки)",
        RecordHint = "В записи только ваш игровой трафик, включая ники. Не публикуйте их.",
        StatusPrefix = "Статус: ",
        NpcapMissing = "Npcap не установлен",
        NpcapNeeded =
            "AION2 DPS Meter читает сетевой трафик игры через Npcap — бесплатный драйвер захвата, а он ещё не установлен.\n\n" +
            "Установите его с npcap.com с настройками по умолчанию (с ними работают и ExitLag, и другие ускорители пинга), затем запустите метр снова.\n\n" +
            "Открыть страницу загрузки?",
        SecHotkeys = "ГОРЯЧИЕ КЛАВИШИ",
        HkToggle = "Показать / скрыть оверлей",
        HkReset = "Перезапустить метр",
        HkClick = "Клики сквозь оверлей",
        HotkeysHint = "Формат: Ctrl+Shift+D, Alt+F9 … Применяется после сохранения.",
        SecData = "ДАННЫЕ",
        DownloadIcons = "Скачивать иконки умений и портреты боссов",
        IconsHint = "Иконки умений — официальный CDN AION 2 (assets.playnccdn.com), портреты боссов — MetaBot.GG (metabot.gg). Каждая картинка скачивается один раз, затем берётся из кэша.",
        DataInfo = "Умений: {0:#,0} · NPC: {1:#,0} (боссов: {2:#,0}) · DoT: {3:#,0} · Карт: {4:#,0}\nТаблицы взяты из проектов под GPL-3.0 (см. data/NOTICE.md). skills.json / npcs.json в папке data их переопределяют.",
        OpenDataFolder = "Папка данных",
        OpenAppFolder = "Папка программы",
        Logs = "Логи",
        Disclaimer = "AION2 DPS Meter только слушает ваше собственное сетевое соединение. Он не читает память игры, ничего не внедряет и не отправляет в игру ни байта. Тем не менее сторонние программы могут нарушать правила игры — используйте на свой риск.",
        Cancel = "Отмена",
        Save = "Сохранить",
        UpdateTitle = "Доступно обновление",
        UpdateHeadline = "Вышла версия AION2 DPS Meter {0}",
        UpdateYouHave = "У вас {0}.",
        UpdateReleased = "У вас {0}. Новая выпущена {1}.",
        UpdateWhatsNew = "ЧТО НОВОГО",
        UpdateInstalledHint = "Метр закроется, установщик обновит его и запустит снова. Настройки, история боёв и таймеры сохранятся.",
        UpdatePortableHint = "Эта копия распакована из zip и не может заменить себя сама: скачайте новый архив и распакуйте его поверх этой папки или перейдите на установщик.",
        UpdateNow = "Обновить",
        UpdateOpenPage = "Открыть страницу загрузки",
        UpdateLater = "Позже",
        UpdateSkip = "Пропустить эту версию",
        UpdateDownloading = "Скачивание установщика… {0:0}%",
        UpdateStarting = "Запуск установщика…",
        UpdateFailed = "Не удалось обновить: {0}. Новую версию можно скачать со страницы релиза.",
        UpdateBanner = "Вышла версия {0} — нажмите, чтобы обновить",
        TipUpdate = "Что нового — и обновление в один клик",
        UpdateBalloon = "Вышла версия {0}. Нажмите сюда, чтобы обновить.",
        UpdatedBalloon = "Метр обновлён до версии {0}.",
        TrayUpdate = "Обновить до {0}…",
        TrayCheckUpdates = "Проверить обновления",
        SecUpdates = "ОБНОВЛЕНИЯ",
        CheckUpdatesAuto = "Проверять новые версии на GitHub",
        UpdatesHint = "Вскоре после запуска и раз в 2 часа. Метр только спрашивает у GitHub номер новой версии — ничего о вас и ваших боях не отправляется.",
        AutoInstallUpdates = "Устанавливать обновления самому",
        AutoInstallHint = "Новая версия скачивается в фоне и ставится, когда вы не в игре (или после 10 минут без боя): метр перезапустится сам, без всяких окон. Только для установленной версии.",
        UpdateBannerReady = "Обновление {0} готово — установится само, когда вы не в игре",
        UpdateReadyBalloon = "Версия {0} скачана и установится сама, когда вы не в игре. Нажмите сюда, чтобы установить сейчас.",
        VersionInstalled = "Версия {0} · установлена",
        VersionPortable = "Версия {0} · без установки (zip)",
        CheckNow = "Проверить сейчас",
        UpdateChecking = "Проверка…",
        UpToDate = "У вас последняя версия.",
        UpdateFound = "Вышла версия {0}.",
        UpdateCheckFailed = "Не удалось связаться с GitHub: {0}",
    };

    private static string RuPlural(int n, string one, string few, string many)
    {
        var mod100 = n % 100;
        var mod10 = n % 10;
        if (mod100 is >= 11 and <= 14) return many;
        return mod10 switch { 1 => one, >= 2 and <= 4 => few, _ => many };
    }
}
