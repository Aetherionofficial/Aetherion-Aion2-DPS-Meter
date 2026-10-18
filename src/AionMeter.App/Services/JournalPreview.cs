using AionMeter.Core.Combat;
using AionMeter.Core.Storage;

namespace AionMeter.App.Services;

/// <summary>Explicit sample data for screenshots and WPF smoke tests. Never writes fight files.</summary>
public static class JournalPreview
{
    public static IReadOnlyList<(HistoryEntry Entry, FightRecord Record)> Create(FightRecord original)
    {
        var result = new List<(HistoryEntry, FightRecord)>();
        for (var i = 0; i < 4; i++)
        {
            var factor = 1 - i * .08;
            var boss = original.Summary.Boss ?? new BossSnapshot(500, 2701250, "Balhash", 0, 10_000_000);
            var seconds = Math.Max(1, original.Summary.CombatMs / 1000.0);
            var summary = original.Summary with
            {
                Id = Guid.NewGuid(), IsActive = false, Reason = i == 3 ? EncounterEndReason.Wipe : EncounterEndReason.Kill,
                StartedAt = DateTimeOffset.Now.AddDays(-i), Zone = "Beispielzone", Boss = boss with { MaxHpKnown = true, Uncounted = 0, Hp = 0 },
                Combatants = original.Summary.Combatants.Select(c => c with { Damage = (long)(c.Damage * factor), Dps = c.Damage * factor / seconds }).ToArray(),
                TotalDamage = (long)(original.Summary.TotalDamage * factor), PartyDps = original.Summary.TotalDamage * factor / seconds,
            };
            var details = original.Details.Select(d => d with { Damage = (long)(d.Damage * factor), Dps = d.Dps * factor }).ToArray();
            var record = original with { Version = 3, Summary = summary, Details = details, BossStartObserved = true };
            var self = details.FirstOrDefault(d => d.IsSelf);
            var entry = new HistoryEntry($"preview-{i}.json", summary.Title, summary.StartedAt, summary.CombatMs, summary.Reason,
                summary.PartyDps, summary.TotalDamage, summary.Zone, boss.NpcCode, summary.TargetCode, summary.PlayerCount,
                self?.Name, self?.Class ?? Core.Events.GameClass.Unknown, self?.Dps ?? 0, 2) { Path = $"preview-{i}" };
            result.Add((entry, record));
        }
        return result;
    }
}
