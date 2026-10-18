using AionMeter.App.Services;
using AionMeter.Core.Events;
using AionMeter.Core.Game;
using AionMeter.Core.Storage;

namespace AionMeter.App.Tests;

public sealed class TrainingEndToEndTests
{
    [Fact]
    public void Configured_boss_goal_produces_progress_immediately_after_the_fight()
    {
        using var meter = new MeterService(new AppSettings { Transient = true, SaveHistory = false, DownloadIcons = false }, null);
        meter.Options.MinSavedFightMs = 0;
        meter.Data.Npcs[990052] = new NpcDef(990052, "Trainingsboss", IsBoss: true, IsDummy: false);
        meter.Tracker.Process(new ZoneChangedEvent(0, 600012, "Testzone", true));
        meter.Tracker.Process(new SelfIdentifiedEvent(0, 100, "Training", 1304, GameClass.Cleric));
        meter.Tracker.Process(new NpcSeenEvent(0, 500, 990052, 10000));
        meter.Tracker.Process(new NpcHpEvent(500, 500, 10000, 10000));
        meter.Tracker.Process(new DamageEvent(1000, 100, 500, 17010000, 500, HitFlags.None));
        meter.Tracker.Process(new DamageEvent(2000, 100, 500, 17010000, 500, HitFlags.None));
        var scope = TrainingScope.For(meter.Tracker.Record(null)!)!;
        Assert.NotNull(scope); meter.Journal.SetTarget(scope, 500);
        meter.Tracker.Process(new DeathEvent(2500, 500));
        Assert.NotNull(meter.TrainingMessage);
        Assert.Contains("erreicht", meter.TrainingMessage);
        Assert.True(Environment.TickCount64 - meter.TrainingMessageAt < 5000);
    }
}
