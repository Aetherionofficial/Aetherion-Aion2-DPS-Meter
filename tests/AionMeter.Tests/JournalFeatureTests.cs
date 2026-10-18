using System.Text.Json;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Storage;

namespace AionMeter.Tests;

public sealed class JournalFeatureTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "Aetherion-journal-" + Guid.NewGuid().ToString("N"));
    private string Metadata => Path.Combine(_dir, "journal", "metadata.json");
    private static FightRecord Fight(double dps = 50_000, EncounterEndReason reason = EncounterEndReason.Kill,
        int server = 1304, string character = "Test-Heiler", string zone = "Bosszone", int bossCode = 2300243)
    {
        var player = new CombatantSnapshot(100, character, GameClass.Cleric, true, (long)(dps * 30), dps, 1, 20, .5, 10, 0, server);
        var summary = new EncounterSnapshot(Guid.NewGuid(), "Testboss", zone, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), 30_000, 30_000,
            false, reason, player.Damage, dps, new BossSnapshot(500, bossCode, "Testboss", 0, 1_000_000), [player], Attackers: 1);
        var detail = new CombatantDetail(100, character, GameClass.Cleric, true, player.Damage, dps, 1, 30_000, 0, 0,
            new HitQuality(20, 0, .5, null, null, null, null, null, 10, null), [], [], [], server);
        return new(3, summary, [detail], Healers: [], BossStartObserved: true);
    }

    [Fact]
    public void Notes_favorites_and_goals_survive_restart_without_changing_original_fights()
    {
        var history = new HistoryStore(Path.Combine(_dir, "fights"));
        var record = Fight(); var fightPath = history.Save(record); var original = File.ReadAllBytes(fightPath);
        var journal = new FightJournalStore(Metadata); var scope = TrainingScope.For(record)!;
        journal.Edit(Path.GetFileName(fightPath), true, "Neue Ausrüstung getestet\n+ Heilung", scope, 60_000);
        var reopened = new FightJournalStore(Metadata);
        Assert.True(reopened.Annotation(Path.GetFileName(fightPath)).Favorite);
        Assert.Contains("Ausrüstung", reopened.Annotation(Path.GetFileName(fightPath)).Note);
        Assert.Equal(60_000, reopened.Target(scope));
        Assert.Equal(original, File.ReadAllBytes(fightPath));
        reopened.Edit(Path.GetFileName(fightPath), false, "", scope, null);
        Assert.Null(new FightJournalStore(Metadata).Target(scope));
    }

    [Fact]
    public void Goals_are_isolated_by_boss_zone_character_class_and_server()
    {
        var scope = TrainingScope.For(Fight())!; var journal = new FightJournalStore(null); journal.SetTarget(scope, 50_000);
        foreach (var other in new[] { scope with { BossCode = 99 }, scope with { Zone = "Andere Zone" }, scope with { Character = "Alt" },
            scope with { Class = GameClass.Sorcerer }, scope with { ServerId = 2305 } }) Assert.Null(journal.Target(other));
        Assert.Null(TrainingScope.For(Fight(server: 0)));
        Assert.Null(TrainingScope.For(Fight(character: "#100")));
        Assert.Null(TrainingScope.For(Fight(zone: "")));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)] [InlineData(0)] [InlineData(1_000_000_001)]
    public void Invalid_goal_does_not_partially_commit_a_favorite_or_note(double value)
    {
        var journal = new FightJournalStore(null);
        Assert.Throws<ArgumentOutOfRangeException>(() => journal.Edit("fight.json", true, "note", TrainingScope.For(Fight()), value));
        Assert.False(journal.Annotation("fight.json").Favorite);
    }

    [Fact]
    public void Invalid_or_future_metadata_is_preserved_instead_of_silently_overwritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Metadata)!);
        foreach (var content in new[] { "{broken", "{\"Version\":99,\"Fights\":{},\"Goals\":{}}" })
        {
            File.WriteAllText(Metadata, content); var journal = new FightJournalStore(Metadata);
            Assert.NotNull(journal.LoadError);
            Assert.Throws<IOException>(() => journal.Annotate("fight.json", true, "new"));
            Assert.Equal(content, File.ReadAllText(Metadata));
        }
    }

    [Fact]
    public void Failed_atomic_file_write_keeps_in_memory_state_unchanged()
    {
        Directory.CreateDirectory(Metadata); var journal = new FightJournalStore(Metadata);
        var error = Record.Exception(() => journal.Edit("fight.json", true, "Notiz", TrainingScope.For(Fight()), 60_000));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.False(journal.Annotation("fight.json").Favorite);
        Assert.Null(journal.Target(TrainingScope.For(Fight())));
    }

    [Fact]
    public void Metadata_rejects_traversal_and_oversized_notes_and_serializes_concurrent_edits()
    {
        var journal = new FightJournalStore(Metadata);
        Assert.Throws<ArgumentException>(() => journal.Annotate("../fight.json", true, ""));
        Assert.Throws<ArgumentException>(() => journal.Annotate("fight.json", true, new string('a', 2001)));
        Parallel.For(0, 25, i => journal.Annotate($"{i}.json", true, $"Notiz {i}"));
        var reopened = new FightJournalStore(Metadata);
        for (var i = 0; i < 25; i++) Assert.Equal($"Notiz {i}", reopened.Annotation($"{i}.json").Note);
    }

    [Fact]
    public void Boss_diary_excludes_incomplete_kills_and_other_characters_from_best_values()
    {
        var first = Fight(40_000); var last = Fight(50_000) with { Summary = Fight().Summary with { StartedAt = first.Summary.StartedAt.AddHours(1) } };
        var partial = Fight(900_000); partial = partial with { Summary = partial.Summary with { Boss = partial.Summary.Boss! with { Uncounted = 1000 } } };
        var wipe = Fight(80_000, EncounterEndReason.Wipe);
        var summary = JournalInsights.Boss(first, [first, last, partial, wipe, Fight(1_000_000, server: 2305), Fight(2_000_000, zone: "Other")]);
        Assert.Equal(4, summary.Attempts); Assert.Equal(3, summary.Kills); Assert.Equal(50_000, summary.BestDps);
        Assert.Equal(2, summary.Development.Count(p => p.ComparableKill)); Assert.Equal(30_000, summary.FastestKillMs);
        Assert.False(JournalInsights.Progress(partial, 50_000)!.Reached);
        Assert.Equal(100, JournalInsights.Progress(partial, 50_000)!.Percent);
        Assert.True(JournalInsights.Progress(last, 50_000)!.Reached);
    }

    [Fact]
    public void Quality_explains_late_entry_unknown_hp_and_missing_group_values()
    {
        var record = Fight(); record = record with { Summary = record.Summary with { Attackers = 30, Boss = record.Summary.Boss! with { MaxHpKnown = false, Uncounted = 50_000 } } };
        var quality = JournalInsights.Quality(record);
        Assert.Contains(quality.Warnings, s => s.Contains("Spät"));
        Assert.Contains(quality.Warnings, s => s.Contains("Gruppenschaden"));
        Assert.Contains(quality.Warnings, s => s.Contains("Maximal"));
        Assert.False(JournalInsights.EligibleBest(record));
        Assert.Empty(JournalInsights.Quality(Fight()).Warnings);
    }

    [Fact]
    public void History_search_matches_notes_localized_boss_dates_character_result_and_favorites()
    {
        var history = new HistoryStore(Path.Combine(_dir, "fights")); history.Save(Fight()); var entry = Assert.Single(history.List());
        var note = new FightAnnotation(true, "Neue Ausrüstung getestet");
        Assert.True(new FightHistoryQuery("AUSRÜSTUNG", entry.StartedAt.LocalDateTime.Date, entry.StartedAt.LocalDateTime.Date, "test", EncounterEndReason.Kill, true).Matches(entry, note));
        Assert.True(new FightHistoryQuery("Lokaler Boss").Matches(entry, note, "Lokaler Boss"));
        Assert.False(new FightHistoryQuery(Through: entry.StartedAt.LocalDateTime.Date.AddDays(-1)).Matches(entry, note));
        Assert.False(new FightHistoryQuery(FavoritesOnly: true).Matches(entry, new()));
        Assert.False(new FightHistoryQuery(Result: EncounterEndReason.Wipe).Matches(entry, note));
    }

    [Fact]
    public void Same_second_attempts_do_not_overwrite_each_other_and_malformed_fights_are_skipped()
    {
        var store = new HistoryStore(Path.Combine(_dir, "fights")); var a = Fight(); var b = Fight(60_000);
        var first = store.Save(a); var second = store.Save(b);
        Assert.NotEqual(first, second); Assert.Equal(2, new HistoryStore(store.Directory).List().Count);
        Assert.Equal(50_000, store.Load(first)!.Details[0].Dps);
        var bad = Path.Combine(store.Directory, "bad.json"); File.WriteAllText(bad, "{}");
        Assert.Null(store.Load(bad)); Assert.Equal(2, new HistoryStore(store.Directory).List().Count);
    }

    [Fact]
    public void Discord_pages_include_every_player_but_no_names_when_anonymous()
    {
        var record = Fight(); var players = Enumerable.Range(1, 31).Select(i => record.Summary.Combatants[0] with
        { ActorId = (uint)i, Name = "SecretName" + i, Damage = i * 1000, Dps = i * 100, IsSelf = i == 1 }).ToArray();
        record = record with { Summary = record.Summary with { Combatants = players, TotalDamage = players.Sum(p => p.Damage) } };
        var pages = DiscordCardData.Create(record, true);
        Assert.Equal(3, pages.Count); Assert.All(pages, p => Assert.InRange(p.Rows.Count, 1, 15));
        Assert.Equal(Enumerable.Range(1, 31), pages.SelectMany(p => p.Rows).Select(p => p.Rank));
        Assert.DoesNotContain("SecretName", JsonSerializer.Serialize(pages));
        Assert.Contains("SecretName", JsonSerializer.Serialize(DiscordCardData.Create(record, false)));
    }

    [Fact]
    public void Pure_healers_and_recipients_are_saved_without_fabricating_dps_rows()
    {
        var tracker = Tracker();
        tracker.Process(new PlayerSeenEvent(0, 101, "PureHealer", 1304, GameClass.Cleric));
        tracker.Process(new HealEvent(1100, 101, 100, 17020000, 300, HitFlags.Restore));
        tracker.Process(new HealEvent(1200, 101, 100, 17020000, 200, HitFlags.Restore));
        tracker.Process(new HealEvent(1300, 101, 100, 17030000, 100, HitFlags.Restore));
        tracker.Process(new HealEvent(1400, 9999, 100, 17030000, 9999, HitFlags.Restore)); // unknown source is not credited
        var record = tracker.Record(null)!; var healer = Assert.Single(record.Healers!);
        Assert.Equal("PureHealer", healer.Name); Assert.Equal(600, healer.Total);
        Assert.Equal(500, healer.Skills.Single(s => s.SkillCode == 17020000).Amount);
        Assert.Equal(3, Assert.Single(healer.Recipients!).Events);
        Assert.Equal("Test", Assert.Single(healer.Recipients!).Name);
        Assert.Single(record.Summary.Combatants); Assert.DoesNotContain(record.Summary.Combatants, p => p.ActorId == 101);
        var store = new HistoryStore(Path.Combine(_dir, "fights")); var loaded = store.Load(store.Save(record))!;
        Assert.Equal(600, Assert.Single(loaded.Healers!).Total);
        var legacy = record with { Version = 2, Healers = null };
        Assert.Empty(JournalInsights.Healing(legacy));
    }

    [Fact]
    public void Healing_skill_and_target_accumulators_are_bounded_and_limit_is_visible()
    {
        var tracker = Tracker();
        for (var i = 0; i < 515; i++) tracker.Process(new HealEvent(1100 + i, 100, (uint)(10_000 + i), 20000 + i, 1, HitFlags.Restore));
        var record = tracker.Record(null)!; var healer = Assert.Single(record.Healers!);
        Assert.True(record.HealingTruncated); Assert.Equal(512, healer.Skills.Count); Assert.Equal(512, healer.Recipients!.Count); Assert.Equal(515, healer.Total);
        Assert.Equal(512, Assert.Single(record.Details).HealingSkills!.Count);
        Assert.Contains(JournalInsights.Quality(record).Warnings, s => s.Contains("Speichergrenze"));
    }

    [Fact]
    public void Healing_duration_includes_received_heals_without_inflating_damage_duration()
    {
        var tracker = Tracker();
        tracker.Process(new HealEvent(10_000, 100, 100, 17020000, 900, HitFlags.Restore));
        var record = tracker.Record(null)!;
        Assert.Equal(1000, record.Summary.CombatMs);
        Assert.Equal(9000, record.HealingDurationMs);
        Assert.Equal(900, Assert.Single(record.Healers!).Total);
    }

    [Fact]
    public void Pure_healer_identity_remains_searchable_in_history_and_boss_attempts()
    {
        var tracker = new CombatTracker(GameData.Empty, new MeterOptions { MinSavedFightMs = 0 });
        tracker.Process(new ZoneChangedEvent(0, 600012, "Testzone", true));
        tracker.Process(new SelfIdentifiedEvent(0, 100, "PureSelf", 1304, GameClass.Cleric));
        tracker.Process(new PlayerSeenEvent(0, 101, "Tank", 1304, GameClass.Templar));
        tracker.Process(new NpcSeenEvent(0, 500, 2300243, 10_000_000));
        tracker.Process(new NpcHpEvent(500, 500, 10_000_000, 10_000_000));
        tracker.Process(new DamageEvent(1000, 101, 500, 12010000, 100, HitFlags.None));
        tracker.Process(new HealEvent(1100, 100, 101, 17020000, 1000, HitFlags.Restore));
        var record = tracker.Record(null)!;
        Assert.Null(tracker.Snapshot(null, 1200)!.Boss); // the local healer never targeted it
        Assert.Equal("PureSelf", TrainingScope.For(record)!.Character);
        var store = new HistoryStore(Path.Combine(_dir, "fights")); store.Save(record);
        Assert.Equal("PureSelf", Assert.Single(store.List()).SelfName);
        Assert.Equal(0, Assert.Single(store.List()).SelfPlace);
        var completed = record with { Summary = record.Summary with { IsActive = false, Reason = EncounterEndReason.Kill } };
        var diary = JournalInsights.Boss(completed, [completed]);
        Assert.Equal(1, diary.Attempts); Assert.Equal(1, diary.Kills); Assert.Null(diary.BestDps);
    }

    [Fact]
    public void Missing_saved_start_reference_never_claims_a_complete_kill_or_achievement()
    {
        var record = Fight() with { BossStartObserved = null };
        Assert.False(JournalInsights.EligibleBest(record));
        Assert.Contains(JournalInsights.Quality(record).Warnings, text => text.Contains("Kampfstart nicht bestätigt"));
        Assert.False(JournalInsights.Progress(record, 1000)!.Reached);
    }

    [Fact]
    public void Structurally_broken_combatant_or_detail_arrays_do_not_crash_history_loading()
    {
        var store = new HistoryStore(Path.Combine(_dir, "fights"));
        var path = store.Save(Fight()); var original = File.ReadAllText(path);
        var json = System.Text.Json.Nodes.JsonNode.Parse(original)!;
        json["Details"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        File.WriteAllText(path, json.ToJsonString()); Assert.Null(store.Load(path));
        json = System.Text.Json.Nodes.JsonNode.Parse(original)!;
        json["Summary"]!["Combatants"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        File.WriteAllText(path, json.ToJsonString()); Assert.Null(store.Load(path));
    }

    [Fact]
    public void Healing_source_limit_is_bounded_and_does_not_create_extra_dps_participants()
    {
        var tracker = Tracker();
        for (uint i = 0; i < 260; i++)
        {
            tracker.Process(new PlayerSeenEvent(0, 10000 + i, "Heiler " + i, 1304, GameClass.Cleric));
            tracker.Process(new HealEvent(1100 + i, 10000 + i, 100, 17020000, 1, HitFlags.Restore));
        }
        var record = tracker.Record(null)!;
        Assert.Equal(256, record.Healers!.Count); Assert.True(record.HealingTruncated);
        Assert.Single(record.Summary.Combatants);
    }

    private static CombatTracker Tracker()
    {
        var tracker = new CombatTracker(GameData.Empty, new MeterOptions { MinSavedFightMs = 0 });
        tracker.Process(new SelfIdentifiedEvent(0, 100, "Test", 1304, GameClass.Gladiator));
        tracker.Process(new NpcSeenEvent(0, 500, 2300243, 1_000_000));
        tracker.Process(new DamageEvent(1000, 100, 500, 11010000, 100, HitFlags.None));
        return tracker;
    }
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
