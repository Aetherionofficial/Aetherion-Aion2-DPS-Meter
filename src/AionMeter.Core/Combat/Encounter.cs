using AionMeter.Core.Events;

namespace AionMeter.Core.Combat;

public enum EncounterEndReason
{
    None,
    Kill,
    Wipe,
    Idle,
    Manual,
    ZoneChange,
}

public sealed class HitStats
{
    public int Hits;
    public int Crits;
    public int Back;
    public int Front;
    public int Perfect;
    public int Heavy;
    public int Multi;
    public int DotTicks;
    public long Damage;
    public long DotDamage;
    public long Min = long.MaxValue;
    public long Max;

    public void Add(long damage, HitFlags flags)
    {
        Damage += damage;
        if ((flags & HitFlags.Dot) != 0)
        {
            // DoT ticks never crit / land from behind, so keep them out of the hit-quality denominators.
            DotTicks++;
            DotDamage += damage;
            return;
        }
        Hits++;
        if (damage < Min) Min = damage;
        if (damage > Max) Max = damage;
        if ((flags & HitFlags.Critical) != 0) Crits++;
        if ((flags & HitFlags.Back) != 0) Back++;
        if ((flags & HitFlags.Front) != 0) Front++;
        if ((flags & HitFlags.Perfect) != 0) Perfect++;
        if ((flags & HitFlags.Heavy) != 0) Heavy++;
        if ((flags & HitFlags.Multi) != 0) Multi++;
    }
}

public sealed class SkillStats
{
    public required int SkillCode { get; init; }
    public HitStats Stats { get; } = new();
}

public sealed class HealingSkillStats
{
    public required int SkillCode { get; init; }
    public long Amount { get; set; }
    public int Events { get; set; }
}

public sealed class EncounterHealer
{
    public long Total;
    public Dictionary<int, HealingSkillStats> Skills { get; } = new();
    public Dictionary<uint, (long Amount, int Events)> Recipients { get; } = new();
}

public sealed class IncomingSkillStats
{
    public required int SkillCode { get; init; }
    public long Damage { get; set; }
    public int Hits { get; set; }
}

public sealed record IncomingEvent(long TimeMs, int SkillCode, long Amount, bool Healing);
public sealed record DeathRecord(long TimeMs, uint ActorId, string Name, int SkillCode, long DamageTaken, long RecentDamageTaken,
    IReadOnlyList<IncomingEvent>? Timeline = null);

/// <summary>One cast / hit in order, for the rotation view.</summary>
public readonly record struct CastRecord(int OffsetMs, int SkillCode, long Damage, HitFlags Flags);

public sealed class Combatant
{
    public const int MaxCasts = 20_000;

    /// <summary>
    /// Pseudo-row collecting pets and skill effects whose owner could not be found. Never a player: it is listed last
    /// and left out of player counts and places.
    /// </summary>
    public const uint UnknownSummonsId = 0xFFFF_FFF0;

    public required uint ActorId { get; init; }
    public string Name { get; set; } = "";
    public GameClass Class { get; set; }
    public bool IsSelf { get; set; }
    public int ServerId { get; set; }
    /// <summary>Gear score ("GS") and combat power ("CP") when the party roster or the own record gave them; 0 = not known.</summary>
    public int GearScore { get; set; }
    public long CombatPower { get; set; }

    public HitStats Total { get; } = new();
    public Dictionary<int, SkillStats> Skills { get; } = new();
    public Dictionary<int, HealingSkillStats> HealingSkills { get; } = new();
    public Dictionary<int, IncomingSkillStats> IncomingSkills { get; } = new();
    /// <summary>Highest recent-window DPS observed while the encounter is live; held during pauses between attacks.</summary>
    public double PeakLiveDps { get; set; }
    /// <summary>Damage per whole second since encounter start.</summary>
    public List<long> PerSecond { get; } = new();
    public List<CastRecord> Casts { get; } = new();
    public int MaxHitSkill { get; set; }

    public long DamageTaken { get; set; }
    public int HitsTaken { get; set; }
    public long Healing { get; set; }
    public long LastIncomingMs { get; set; }
    public int LastIncomingSkill { get; set; }
    public long RecentIncomingDamage { get; set; }

    private readonly Queue<IncomingEvent> _recentEvents = new();
    public void RecordIncoming(IncomingEvent item)
    {
        while (_recentEvents.TryPeek(out var first) && first.TimeMs < item.TimeMs - 10_000) _recentEvents.Dequeue();
        // Bounded even in exceptionally busy encounters.
        while (_recentEvents.Count >= 512) _recentEvents.Dequeue();
        _recentEvents.Enqueue(item);
    }
    public IReadOnlyList<IncomingEvent> BeforeDeath(long at) => _recentEvents
        .Where(e => e.TimeMs >= at - 10_000 && e.TimeMs <= at).OrderBy(e => e.TimeMs).ToArray();

    public long FirstHitMs { get; set; }
    public long LastHitMs { get; set; }

    /// <summary>Power scalars seen on this actor's hits; summons share their owner's.</summary>
    public HashSet<int> Scalars { get; } = new();

    /// <summary>Folds a summon that was only identified after it already dealt damage into its owner.</summary>
    public void MergeFrom(Combatant other)
    {
        foreach (var cast in other.Casts)
        {
            Total.Add(cast.Damage, cast.Flags);
            if (!Skills.TryGetValue(cast.SkillCode, out var s))
            {
                s = new SkillStats { SkillCode = cast.SkillCode };
                Skills[cast.SkillCode] = s;
            }
            s.Stats.Add(cast.Damage, cast.Flags);
            if (Casts.Count < MaxCasts) Casts.Add(cast);
        }
        Casts.Sort((a, b) => a.OffsetMs.CompareTo(b.OffsetMs));
        for (var i = 0; i < other.PerSecond.Count; i++)
        {
            while (PerSecond.Count <= i) PerSecond.Add(0);
            PerSecond[i] += other.PerSecond[i];
        }
        if (other.Total.Max > Total.Max) MaxHitSkill = other.MaxHitSkill;
        DamageTaken += other.DamageTaken;
        HitsTaken += other.HitsTaken;
        Healing += other.Healing;
        foreach (var (skillCode, heal) in other.HealingSkills)
        {
            if (!HealingSkills.TryGetValue(skillCode, out var current))
                HealingSkills[skillCode] = current = new HealingSkillStats { SkillCode = skillCode };
            current.Amount += heal.Amount;
            current.Events += heal.Events;
        }
        foreach (var (skillCode, incoming) in other.IncomingSkills)
        {
            if (!IncomingSkills.TryGetValue(skillCode, out var current))
                IncomingSkills[skillCode] = current = new IncomingSkillStats { SkillCode = skillCode };
            current.Damage += incoming.Damage;
            current.Hits += incoming.Hits;
        }
        if (FirstHitMs == 0 || (other.FirstHitMs != 0 && other.FirstHitMs < FirstHitMs)) FirstHitMs = other.FirstHitMs;
        LastHitMs = Math.Max(LastHitMs, other.LastHitMs);
    }

    public void AddDamage(int offsetMs, int skillCode, long damage, HitFlags flags)
    {
        var prevMax = Total.Max;
        Total.Add(damage, flags);
        if (Total.Max > prevMax) MaxHitSkill = skillCode;

        if (!Skills.TryGetValue(skillCode, out var s))
        {
            s = new SkillStats { SkillCode = skillCode };
            Skills[skillCode] = s;
        }
        s.Stats.Add(damage, flags);

        var sec = Math.Max(0, offsetMs / 1000);
        while (PerSecond.Count <= sec) PerSecond.Add(0);
        PerSecond[sec] += damage;

        if (Casts.Count < MaxCasts) Casts.Add(new CastRecord(offsetMs, skillCode, damage, flags));
    }
}

public sealed class Encounter
{
    public Guid Id { get; } = Guid.NewGuid();
    public required long StartMs { get; init; }
    public long LastDamageMs { get; set; }
    public long EndMs { get; set; }
    public bool IsActive => Reason == EncounterEndReason.None;
    public EncounterEndReason Reason { get; set; }
    public string? Zone { get; set; }

    public uint? BossId { get; set; }
    public int BossCode { get; set; }
    public string? BossName { get; set; }
    public long BossMaxHp { get; set; }
    /// <summary>See <see cref="Game.NpcInfo.MaxHpKnown"/>.</summary>
    public bool BossMaxHpKnown { get; set; }
    public long BossHp { get; set; } = -1;
    public long BossLowestHp { get; set; } = long.MaxValue;
    /// <summary>The boss's HP when the meter began watching it (-1 until a fresh HP record), and the damage on the boss
    /// counted by then: the boss lost <c>max − BossRefHp</c> before that, beyond those hits.</summary>
    /// <remarks>
    /// Compared in HP, never against the damage: in the open world an NPC loses 4.5 HP per point of damage the hit
    /// reports (some 23.7, in dungeons 1), so damage cannot tell how much of a boss the meter saw.
    /// </remarks>
    public long BossRefHp { get; set; } = -1;
    public long BossRefCounted { get; set; }

    /// <summary>Damage dealt to each NPC actor, used to pick the most relevant target as the encounter title.</summary>
    public Dictionary<uint, long> DamageByTarget { get; } = new();
    /// <summary>The NPC hit most recently; used for a responsive live target title/HP panel.</summary>
    public uint LastTargetId { get; set; }
    public Dictionary<uint, Combatant> Combatants { get; } = new();
    public Dictionary<uint, EncounterHealer> Healers { get; } = new();
    public bool HealingTruncated { get; set; }
    public long LastHealingMs { get; set; }

    /// <summary>Players seen landing hits on the fight's targets (hit notices), whether or not their damage arrived.</summary>
    public HashSet<uint> Attackers { get; } = new();
    public List<DeathRecord> Deaths { get; } = new();

    public long TotalDamage { get; set; }
    /// <summary>Highest recent-window party DPS observed during this live encounter.</summary>
    public double PeakPartyDps { get; set; }

    /// <summary>Fight length used for DPS: first hit to last hit, so idle tails never dilute the numbers.</summary>
    public long CombatMs => Math.Max(1_000, LastDamageMs - StartMs);

    /// <summary>Timer shown in the overlay header; while live it pauses between damage events.</summary>
    public long ClockMs(long nowMs) =>
        Math.Max(0, (IsActive ? Math.Min(nowMs, LastDamageMs) : EndMs > 0 ? EndMs : LastDamageMs) - StartMs);
}
