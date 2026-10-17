using System.Text.Json;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;
using AionMeter.Core.Game;

namespace AionMeter.Tests;

public class FightInsightsTests
{
    [Fact]
    public void Activity_includes_trailing_quiet_seconds_and_partial_last_second()
    {
        var result = FightInsights.Activity([100, 0, 200], 5_500);
        Assert.Equal(6, result.ObservedSeconds);
        Assert.Equal(2, result.DamageSeconds);
        Assert.Equal(3, result.LongestQuietSeconds);
        Assert.Equal(1.0 / 3, result.DamageUptime, 6);
        Assert.Equal(0, FightInsights.Activity([], 0).DamageUptime);
    }

    [Fact]
    public void Death_timeline_records_incoming_heals_without_counting_them_as_damage()
    {
        var tracker = new CombatTracker(GameData.Empty, new MeterOptions { MinSavedFightMs = 0, IdleTimeoutMs = 120_000, BossIdleTimeoutMs = 120_000 });
        tracker.Process(new SelfIdentifiedEvent(0, 100, "Player", 1304, GameClass.Cleric));
        tracker.Process(new NpcSeenEvent(0, 500, 2300243, 1_000_000));
        tracker.Process(new DamageEvent(1_000, 100, 500, 17010000, 10, HitFlags.None));
        tracker.Process(new DamageEvent(2_000, 500, 100, 101, 100, HitFlags.None));
        tracker.Process(new DamageEvent(12_000, 500, 100, 102, 200, HitFlags.None));
        tracker.Process(new HealEvent(13_000, 100, 100, 17020000, 75, HitFlags.Restore));
        tracker.Process(new DamageEvent(14_000, 500, 100, 103, 300, HitFlags.None));
        tracker.Process(new DeathEvent(15_000, 100));
        var summary = tracker.Snapshot(null, 15_000)!;
        var death = Assert.Single(tracker.Detail(summary.Id, 100)!.Deaths!);
        Assert.Equal(500, death.RecentDamageTaken);
        Assert.Equal(3, death.Timeline!.Count);
        Assert.Single(death.Timeline, hit => hit.Healing && hit.Amount == 75);
        Assert.DoesNotContain(death.Timeline, hit => hit.TimeMs == 2_000);
    }

    [Fact]
    public void Incoming_window_is_bounded_and_filters_future_events()
    {
        var player = new Combatant { ActorId = 1 };
        for (var i = 0; i < 1_000; i++) player.RecordIncoming(new IncomingEvent(i, 1, 1, false));
        Assert.Equal(512, player.BeforeDeath(1_000).Count);
        Assert.All(player.BeforeDeath(700), hit => Assert.True(hit.TimeMs <= 700));
    }

    [Fact]
    public void Legacy_death_records_remain_readable_without_timeline()
    {
        var record = JsonSerializer.Deserialize<DeathRecord>("{\"TimeMs\":100,\"ActorId\":1,\"Name\":\"Player\",\"SkillCode\":2,\"DamageTaken\":10,\"RecentDamageTaken\":10}")!;
        Assert.Null(record.Timeline);
        Assert.Equal(10, record.RecentDamageTaken);
    }

    [Fact]
    public void Boss_records_do_not_mix_servers_characters_or_partially_observed_kills()
    {
        var data = GameData.Empty;
        data.Npcs[990001] = new NpcDef(990001, "Test boss", IsBoss: true, IsDummy: false);
        var tracker = new CombatTracker(data, new MeterOptions { MinSavedFightMs = 0 });
        FightRecord? record = null;
        tracker.EncounterFinished += r => record = r;
        tracker.Process(new SelfIdentifiedEvent(0, 100, "Player", 1304, GameClass.Cleric));
        tracker.Process(new NpcSeenEvent(0, 500, 990001, 5_000));
        tracker.Process(new DamageEvent(1_000, 100, 500, 17010000, 1_000, HitFlags.None));
        tracker.Process(new DeathEvent(2_000, 500));
        Assert.NotNull(record);
        Assert.True(FightInsights.ComparableKills(record, record));
        var otherServer = record with { Details = record.Details.Select(d => d with { ServerId = 1305 }).ToArray() };
        Assert.False(FightInsights.ComparableKills(record, otherServer));
        Assert.False(FightInsights.ComparableKills(record, record with { Summary = record.Summary with { Boss = record.Summary.Boss! with { Uncounted = 1 } } }));
        Assert.False(FightInsights.ComparableKills(record, record with { Summary = record.Summary with { Reason = EncounterEndReason.Wipe } }));
    }
}
