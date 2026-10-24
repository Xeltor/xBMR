namespace BossMod.CombatAssistance.Planning;

internal enum HealingDemandKind { SingleAbility, SingleSpell, AreaAbility, AreaSpell }
internal sealed record HealingReach(uint ActionId, float Radius, HealingDemandKind Kind = HealingDemandKind.SingleAbility);
internal sealed record HealingRequirement(ulong RecipientId, WPos Position, float HeightOffset,
    float HealthRatio, ImmutableArray<HealingReach> Alternatives)
{
    // Alternatives are observed enabled/usable heals whose categories need healing. This
    // requests access for RSR; it never predicts or submits a particular heal.
    internal float Loss(WPos caster) => Math.Max(0,
        MathF.Sqrt((Position - caster).LengthSq() + HeightOffset * HeightOffset) - Alternatives.Max(a => a.Radius));
}

internal sealed class PlanningHealingCoverage
{
    internal ImmutableArray<HealingRequirement> Requirements { get; }
    internal PlanningHealingCoverage(IEnumerable<HealingRequirement> requirements)
    {
        Requirements = requirements.OrderBy(r => r.HealthRatio).ThenBy(r => r.RecipientId).ToImmutableArray();
        if (Requirements.Any(r => r.RecipientId == 0 || !float.IsFinite(r.Position.X) || !float.IsFinite(r.Position.Z)
            || !float.IsFinite(r.HeightOffset) || !float.IsFinite(r.HealthRatio) || r.HealthRatio is < 0 or >= 1
            || r.Alternatives.IsDefaultOrEmpty || r.Alternatives.Any(a => a.ActionId == 0 || !float.IsFinite(a.Radius) || a.Radius <= 0)))
            throw new ArgumentException("Incomplete configured healing-range evidence.");
    }
    internal bool Satisfied(WPos caster) => Requirements.All(r => r.Loss(caster) <= MovementPlanValidation.ArrivalTolerance);
    internal int Compare(WPos a, WPos b)
    {
        // Each recipient and its eligible categories stay explicit. A large sum of extra healing
        // cannot buy the loss of a more urgent recipient's configured need.
        foreach (var r in Requirements)
        {
            var aCovered = r.Loss(a) <= MovementPlanValidation.ArrivalTolerance;
            var bCovered = r.Loss(b) <= MovementPlanValidation.ArrivalTolerance;
            if (aCovered != bCovered) return aCovered ? 1 : -1;
        }
        foreach (var r in Requirements)
        {
            var comparison = r.Loss(b).CompareTo(r.Loss(a));
            if (comparison != 0) return comparison;
        }
        return 0;
    }
    internal bool Same(PlanningHealingCoverage other, bool forRevalidation = false) => Requirements.Length == other.Requirements.Length
        && Requirements.Select((r, i) => r.RecipientId == other.Requirements[i].RecipientId
            && (forRevalidation || r.Position == other.Requirements[i].Position && r.HeightOffset == other.Requirements[i].HeightOffset)
            && r.Alternatives.SequenceEqual(other.Requirements[i].Alternatives)).All(s => s);

    internal bool Preserves(PlanningHealingCoverage previous, WPos previousDestination, WPos destination)
        => Requirements.All(r => !previous.Requirements.Any(p => p.RecipientId == r.RecipientId
                && p.Loss(previousDestination) <= MovementPlanValidation.ArrivalTolerance)
            || r.Loss(destination) <= MovementPlanValidation.ArrivalTolerance);
}
