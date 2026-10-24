namespace BossMod.CombatAssistance.Planning;

internal sealed record RejectedMobility(uint ActionId, ulong AnchorId, WPos From, WPos To, Angle? Facing, string Reason)
{
    internal bool Matches(MovementPlanningContext context, PlanningMobility candidate)
        => ActionId == candidate.ActionId && AnchorId == candidate.AnchorId && Facing == candidate.Facing
            && From.AlmostEqual(context.MobilityOrigin(candidate), 0.1f) && To.AlmostEqual(context.MobilityLanding(candidate), 0.1f);
}
