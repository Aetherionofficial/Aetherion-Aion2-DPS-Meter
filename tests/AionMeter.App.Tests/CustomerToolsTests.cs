using System.IO;
using AionMeter.App.Services;
using AionMeter.App.ViewModels;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;
using AionMeter.Core.Game;

namespace AionMeter.App.Tests;

public sealed class CustomerToolsTests
{
    [Fact]
    public void Layout_import_cannot_modify_capture_or_account_settings()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"Width\":700,\"Height\":340,\"CaptureDevice\":\"malicious\",\"AutoInstallUpdates\":true}");
            var settings = new AppSettings { CaptureDevice = "selected", AutoInstallUpdates = false };
            CustomerTools.ImportLayout(path).Apply(settings);
            Assert.Equal(700, settings.OverlayWidth);
            Assert.Equal("selected", settings.CaptureDevice);
            Assert.False(settings.AutoInstallUpdates);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Layout_export_round_trips_and_excludes_private_settings()
    {
        var path = Path.GetTempFileName();
        try
        {
            var settings = new AppSettings { OverlayWidth = 750, OverlayHeight = 400, RowSize = 65,
                OverlayTextScale = 1.3, CaptureDevice = "private-adapter", StreamerMode = true };
            CustomerTools.ExportLayout(path, settings);
            Assert.DoesNotContain("private-adapter", File.ReadAllText(path));
            var imported = CustomerTools.ImportLayout(path);
            Assert.Equal(750, imported.Width); Assert.Equal(400, imported.Height);
            Assert.Equal(65, imported.RowSize); Assert.Equal(1.3, imported.TextScale);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Layout_rejects_non_finite_values_and_clamps_external_sizes()
    {
        Assert.Throws<InvalidDataException>(() => new LayoutProfile(Width: double.NaN).Validate());
        Assert.Throws<InvalidDataException>(() => new LayoutProfile(Left: double.PositiveInfinity).Validate());
        var layout = new LayoutProfile(-100, 100_000, 999, -1, TextScale: 10).Validate();
        Assert.Equal(340, layout.Width); Assert.Equal(1200, layout.Height);
        Assert.Equal(100, layout.RowSize); Assert.Equal(0, layout.Opacity); Assert.Equal(1.5, layout.TextScale);
    }

    [Fact]
    public void Oversized_layout_is_rejected_before_parsing()
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, new string(' ', 16_385)); Assert.Throws<InvalidDataException>(() => CustomerTools.ImportLayout(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Streamer_pseudonyms_are_consistent_without_exposing_actor_ids()
    {
        PrivacyNames.Enabled = true;
        try
        {
            var first = PrivacyNames.Display(123456789, "SecretCharacter");
            Assert.Equal(first, PrivacyNames.Display(123456789, "SecretCharacter"));
            Assert.NotEqual(first, PrivacyNames.Display(987654321, "OtherCharacter"));
            Assert.DoesNotContain("SecretCharacter", first); Assert.DoesNotContain("123456789", first);
        }
        finally { PrivacyNames.Enabled = false; }
    }

    [Fact]
    public void Streamer_view_and_tooltip_hide_names_without_changing_recorded_data()
    {
        var tracker = new CombatTracker(GameData.Empty, new MeterOptions());
        tracker.Process(new SelfIdentifiedEvent(0, 100, "SecretCharacter", 1304, GameClass.Cleric));
        tracker.Process(new NpcSeenEvent(0, 500, 2300243, 100_000));
        tracker.Process(new DamageEvent(1_000, 100, 500, 17010000, 1_000, HitFlags.None));
        var original = tracker.Snapshot(null, 2_000)!;
        PrivacyNames.Enabled = true;
        try
        {
            var vm = new OverlayViewModel { HighContrast = true, AccessibleColors = true };
            vm.Apply(original, 8, true);
            var row = Assert.Single(vm.Rows);
            Assert.DoesNotContain("SecretCharacter", row.Name);
            Assert.DoesNotContain("SecretCharacter", row.Tooltip);
            Assert.Equal("SecretCharacter", Assert.Single(original.Combatants).Name);
            Assert.Equal(0, Assert.Single(PrivacyNames.Apply(original)!.Combatants).ServerId);
            Assert.Equal(1304, Assert.Single(original.Combatants).ServerId);
            var color = Assert.IsType<System.Windows.Media.SolidColorBrush>(row.AccentBrush).Color;
            Assert.Equal(System.Windows.Media.Color.FromRgb(230, 159, 0), color);
        }
        finally { PrivacyNames.Enabled = false; }
    }

    [Fact]
    public async Task First_record_notification_is_suppressed_when_maximum_boss_hp_is_unknown()
    {
        var settings = new AppSettings { Transient = true, SaveHistory = true, DownloadIcons = false };
        using var meter = new MeterService(settings, null);
        meter.Options.MinSavedFightMs = 0;
        meter.Data.Npcs[990002] = new NpcDef(990002, "Unknown HP boss", IsBoss: true, IsDummy: false);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        meter.FightSaved += _ => saved.TrySetResult();
        meter.Tracker.Process(new SelfIdentifiedEvent(0, 100, "RecordTest", 1304, GameClass.Cleric));
        meter.Tracker.Process(new NpcSeenEvent(0, 500, 990002));
        meter.Tracker.Process(new NpcHpEvent(500, 500, 5_000, 0));
        meter.Tracker.Process(new DamageEvent(1_000, 100, 500, 17010000, 1_000, HitFlags.None));
        Assert.False(meter.Tracker.Snapshot(null, 1_500)!.Boss!.MaxHpKnown);
        meter.Tracker.Process(new DeathEvent(2_000, 500));
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(meter.PersonalRecordMessage);
    }
}
