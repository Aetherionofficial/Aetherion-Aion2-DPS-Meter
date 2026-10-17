namespace AionMeter.Core.Combat;

/// <summary>Derived only from received damage events. A quiet second is not proof of inactivity or a failed mechanic.</summary>
public sealed record ActivitySummary(int ObservedSeconds, int DamageSeconds, int LongestQuietSeconds)
{
    public double DamageUptime => ObservedSeconds == 0 ? 0 : (double)DamageSeconds / ObservedSeconds;
}

public static class FightInsights
{
    public static ActivitySummary Activity(IReadOnlyList<long> perSecond, long combatMs)
    {
        var seconds = (int)Math.Clamp(Math.Ceiling(Math.Max(0, combatMs) / 1000.0), 0, 86_400);
        var active = 0;
        var quiet = 0;
        var longest = 0;
        for (var i = 0; i < seconds; i++)
        {
            if (i < perSecond.Count && perSecond[i] > 0) { active++; quiet = 0; }
            else longest = Math.Max(longest, ++quiet);
        }
        return new(seconds, active, longest);
    }

    public static bool ComparableKills(FightRecord a, FightRecord b)
    {
        var x = a.Details.FirstOrDefault(d => d.IsSelf);
        var y = b.Details.FirstOrDefault(d => d.IsSelf);
        return a.Summary.Boss is { NpcCode: > 0 } boss && b.Summary.Boss?.NpcCode == boss.NpcCode
            && a.Summary.Reason == EncounterEndReason.Kill && b.Summary.Reason == EncounterEndReason.Kill
            && boss.MaxHpKnown && boss.Uncounted == 0 && b.Summary.Boss is { MaxHpKnown: true, Uncounted: 0 }
            && string.Equals(a.Summary.Zone, b.Summary.Zone, StringComparison.Ordinal)
            && !a.Summary.MostDamageUnsent && !b.Summary.MostDamageUnsent
            && x is { Dps: > 0 } && y is { Dps: > 0 } && !string.IsNullOrWhiteSpace(x.Name) && x.Class != AionMeter.Core.Events.GameClass.Unknown && x.Class == y.Class
            && x.ServerId == y.ServerId && string.Equals(x.Name, y.Name, StringComparison.Ordinal);
    }
}
