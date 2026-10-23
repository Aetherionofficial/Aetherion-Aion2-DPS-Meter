using AionMeter.App.ViewModels;
using AionMeter.Core.Combat;
using AionMeter.Core.Events;

namespace AionMeter.App.Tests;

public sealed class OverlayPartyTests
{
    [Fact]
    public void Empty_overlay_has_six_places_and_updates_reuse_real_and_empty_rows()
    {
        var vm = new OverlayViewModel();
        vm.Apply(null, 6, true, compactParty: true);
        Assert.Equal(6, vm.Rows.Count);
        Assert.All(vm.Rows, row => Assert.True(row.IsPlaceholder));
        Assert.False(vm.HasData);
        var emptyRows = vm.Rows.ToArray();
        var collectionChanges = 0;
        vm.Rows.CollectionChanged += (_, _) => collectionChanges++;
        vm.Apply(null, 6, true, compactParty: true);
        Assert.Equal(emptyRows, vm.Rows.ToArray());
        Assert.Equal(0, collectionChanges);
        var snapshot = Snapshot(1);
        vm.Apply(snapshot, 6, true, compactParty: true);
        var references = vm.Rows.ToArray();
        vm.Apply(snapshot, 6, true, compactParty: true);
        Assert.Equal(references, vm.Rows.ToArray());
        Assert.Equal(5, vm.Rows.Count(r => r.IsPlaceholder));
        vm.Apply(null, 6, true, compactParty: true);
        Assert.Equal(references, vm.Rows.ToArray());
        Assert.All(vm.Rows, row => Assert.True(row.IsPlaceholder));
    }

    [Fact]
    public void Party_ranks_by_dps_limits_to_six_and_keeps_the_local_player()
    {
        var vm = new OverlayViewModel();
        vm.Apply(Snapshot(8), 6, true, compactParty: true);
        Assert.Equal(6, vm.Rows.Count);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 8 }, vm.Rows.Select(r => r.ActorId));
        Assert.True(vm.Rows.Last().IsSelf);
        Assert.Equal(8, vm.Rows.Last().Rank);
        Assert.DoesNotContain(vm.Rows, r => r.IsPlaceholder);
    }

    private static EncounterSnapshot Snapshot(int count)
    {
        var actors = Enumerable.Range(1, count).Select(i => new CombatantSnapshot((uint)i, $"Spieler {i}",
            GameClass.Cleric, i == count, i * 1000, 10000 - i * 100, 1, 1, 0, 0, 0)).ToArray();
        return new(Guid.NewGuid(), "Test", null, DateTimeOffset.UnixEpoch, 1000, 1000, true,
            EncounterEndReason.None, actors.Sum(a => a.Damage), 10000, null, actors);
    }
}
