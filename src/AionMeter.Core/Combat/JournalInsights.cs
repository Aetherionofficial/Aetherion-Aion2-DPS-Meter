using AionMeter.Core.Events;
using AionMeter.Core.Storage;

namespace AionMeter.Core.Combat;

public sealed record FightQuality(IReadOnlyList<string> Warnings)
{
    public string Label => Warnings.Count == 0 ? "Keine erkannten Einschränkungen" : "Eingeschränkte Daten";
    public string Explanation => Warnings.Count == 0
        ? "Ausgewertet werden ausschließlich empfangene Ereignisse. Unbemerkte Datenlücken sind nicht ausgeschlossen."
        : string.Join(" · ", Warnings);
}
public sealed record TrainingProgress(double Target, double ObservedDps, bool Confirmed)
{
    public double Percent => Math.Clamp(ObservedDps / Target * 100, 0, 100);
    public bool Reached => Confirmed && ObservedDps >= Target;
}
public sealed record JournalAttempt(DateTimeOffset At, double Dps, bool ComparableKill, EncounterEndReason Result, long DurationMs = 0);
public sealed record BossJournalSummary(int Attempts, int Kills, double? BestDps, IReadOnlyList<JournalAttempt> Development, long? FastestKillMs = null);
public sealed record FightHistoryQuery(string Text = "", DateTime? From = null, DateTime? Through = null,
    string Character = "", EncounterEndReason? Result = null, bool FavoritesOnly = false)
{
    public bool Matches(HistoryEntry entry, FightAnnotation annotation, string? localizedBoss = null)
    {
        if (From is { } from && entry.StartedAt.LocalDateTime.Date < from.Date) return false;
        if (Through is { } through && entry.StartedAt.LocalDateTime.Date > through.Date) return false;
        if (Result is { } result && entry.Reason != result || FavoritesOnly && !annotation.Favorite) return false;
        if (!string.IsNullOrWhiteSpace(Character) && !(entry.SelfName ?? "").Contains(Character.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(Text)) return true;
        var search = Text.Trim();
        return new[] { entry.Title, localizedBoss, entry.Zone, entry.SelfName, annotation.Note, entry.StartedAt.ToString("yyyy-MM-dd") }
            .Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }
}

public static class JournalInsights
{
    public static FightQuality Quality(FightRecord record)
    {
        var warnings = new List<string>();
        var s = record.Summary;
        if (record.Version < 2) warnings.Add("Älteres Kampfdateiformat: Vollständigkeit nicht verlässlich bestätigt");
        if (s.Boss is not null && record.BossStartObserved != true) warnings.Add("Kampfstart nicht bestätigt: Keine gespeicherte HP-Referenz; vollständige Erfassung ist nicht belegt");
        if (s.Boss is { MaxHpKnown: false }) warnings.Add("Boss-Maximal-HP unbekannt: Kampfstart und HP-Anteile sind unsicher");
        if (s.Boss is { Uncounted: > 0 }) warnings.Add("Spät eingestiegen: Ein Teil des Bosskampfs fehlt; DPS ist nur für den erfassten Abschnitt gültig");
        if (s.MostDamageUnsent || s.Fighting > s.DamageSenders && s.Boss is not null)
            warnings.Add("Gruppenschaden teilweise nicht empfangen: Rangliste, Gruppen-DPS und Anteile sind eingeschränkt");
        if (s.Combatants.Any(c => c.IsUnknownSummons && c.Damage > 0)) warnings.Add("Einige Beschwörungen konnten keinem Spieler zugeordnet werden");
        if (record.HealingTruncated) warnings.Add("Heilungsdetails erreichten die Speichergrenze; Detailtabellen können unvollständig sein");
        return new(warnings);
    }

    public static bool EligibleBest(FightRecord record) => record.Version >= 2 && !record.Summary.IsActive
        && TrainingScope.For(record) is not null && Quality(record).Warnings.Count == 0
        && FightInsights.ComparableKills(record, record);

    public static BossJournalSummary Boss(FightRecord selected, IEnumerable<FightRecord> records)
    {
        var scope = TrainingScope.For(selected);
        if (scope is null) return new(0, 0, null, []);
        // Reduce each file to a small point immediately; do not retain all cast histories in memory.
        var points = records.Where(r => TrainingScope.For(r) == scope && !r.Summary.IsActive)
            .Select(r => new JournalAttempt(r.Summary.StartedAt, r.Details.FirstOrDefault(d => d.IsSelf)?.Dps ?? 0,
                EligibleBest(r), r.Summary.Reason, r.Summary.CombatMs)).OrderBy(p => p.At).ToArray();
        var best = points.Where(p => p.ComparableKill && double.IsFinite(p.Dps)).Select(p => (double?)p.Dps).DefaultIfEmpty(null).Max();
        var fastest = points.Where(p => p.ComparableKill).Select(p => (long?)p.DurationMs).DefaultIfEmpty(null).Min();
        return new(points.Length, points.Count(r => r.Result == EncounterEndReason.Kill), best, points, fastest);
    }

    public static TrainingProgress? Progress(FightRecord record, double? target)
    {
        if (target is not > 0 || !double.IsFinite(target.Value)) return null;
        var dps = record.Details.FirstOrDefault(d => d.IsSelf)?.Dps ?? 0;
        return new(target.Value, double.IsFinite(dps) ? Math.Max(0, dps) : 0, EligibleBest(record));
    }

    public static IReadOnlyList<HealerRecord> Healing(FightRecord record) => record.Healers
        ?? record.Details.Where(d => d.HealingTotal > 0).Select(d => new HealerRecord(d.ActorId, d.Name, d.Class, d.IsSelf,
            d.ServerId, d.HealingTotal, d.HealingSkills ?? [], null)).ToArray();
}
