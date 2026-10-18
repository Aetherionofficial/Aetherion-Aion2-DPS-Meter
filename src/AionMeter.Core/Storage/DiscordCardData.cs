using AionMeter.Core.Combat;

namespace AionMeter.Core.Storage;

public sealed record DiscordCardRow(int Rank, string Name, string Class, long Damage, double Dps, double Share, bool IsSelf);
public sealed record DiscordCardPage(string Boss, long DurationMs, string Result, string Quality, string QualityExplanation,
    IReadOnlyList<DiscordCardRow> Rows, int Page, int Pages, int Players, bool Anonymous);

public static class DiscordCardData
{
    public const int RowsPerPage = 15;
    public static IReadOnlyList<DiscordCardPage> Create(FightRecord record, bool anonymize, string? bossName = null)
    {
        var players = record.Summary.Combatants.Where(c => !c.IsUnknownSummons)
            .OrderByDescending(c => c.Damage).ThenBy(c => c.ActorId).ToArray();
        var rows = players.Select((p, index) => new DiscordCardRow(index + 1, anonymize ? $"Spieler {index + 1}" : Safe(p.Name, 48),
            p.Class.ToString(), Math.Max(0, p.Damage), double.IsFinite(p.Dps) ? Math.Max(0, p.Dps) : 0,
            record.Summary.TotalDamage > 0 ? Math.Clamp(p.Damage / (double)record.Summary.TotalDamage, 0, 1) : 0, p.IsSelf)).ToArray();
        var quality = JournalInsights.Quality(record);
        var pages = Math.Max(1, (int)Math.Ceiling(rows.Length / (double)RowsPerPage));
        var result = record.Summary.IsActive ? "LIVE · vorläufig" : record.Summary.Reason switch
        { EncounterEndReason.Kill => "KILL", EncounterEndReason.Wipe => "VERSUCH · WIPE", _ => "BEENDET" };
        var title = Safe(bossName ?? record.Summary.Boss?.Name ?? "Kampfergebnis", 64);
        return Enumerable.Range(0, pages).Select(i => new DiscordCardPage(title, Math.Max(0, record.Summary.CombatMs), result,
            quality.Label, quality.Explanation, rows.Skip(i * RowsPerPage).Take(RowsPerPage).ToArray(), i + 1, pages, rows.Length, anonymize)).ToArray();
    }
    private static string Safe(string value, int length) => new string(value.Where(c => !char.IsControl(c)).Take(length).ToArray());
}
