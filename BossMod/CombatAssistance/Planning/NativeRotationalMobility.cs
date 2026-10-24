namespace BossMod.CombatAssistance.Planning;

// Action mechanics migrated from XCAI's rotational movement catalogue. Jump,
// High Jump, Fleeting Raiju and Primal Ruination return/stay at the origin and
// are not substitutes for these actual displacement actions.
internal static class NativeRotationalMobility
{
    internal static readonly ImmutableArray<NativeMobilityCapture.Definition> Definitions =
    [
        new(25777, MobilityMechanic.Target, Rotational: true), // Forked Raiju
        new(25753, MobilityMechanic.Target, Rotational: true), // Primal Rend
        new(25835, MobilityMechanic.Target, Rotational: true), // Crimson Cyclone
        new(16480, MobilityMechanic.Target, Rotational: true), // Stardiver
        new(96, MobilityMechanic.Target, Rotational: true), // Dragonfire Dive
        new(18325, MobilityMechanic.Target, Rotational: true), // J Kick
        new(11389, MobilityMechanic.Target, Rotational: true), // Flying Frenzy
        new(11401, MobilityMechanic.Ground, Rotational: true) // Loom
    ];
    internal static bool Contains(uint id) => Definitions.Any(d => d.Id == id);

    internal static PlanningAction? WithReadyAlternatives(PlanningAction? action, AvailableMobility available)
    {
        if (action is not { Id: 25778, IsGCD: true, CastSeconds: 0, ResourceCost: 0,
                Opportunity: { Shape: ExecutionShape.SingleTarget } opportunity, Timing: { NativeRecast: > 0 } timing }) return action;
        var forked = available.Actions.FirstOrDefault(m => m.ActionId == 25777 && m.IsGCD && m.RequiredEarlierActions == 0);
        var anchor = forked?.Anchors.FirstOrDefault(a => a.Id == action.TargetId && a.Position == opportunity.Anchor);
        if (forked == null || anchor == null || forked.RecastSeconds > timing.NativeRecast) return action;
        var alternative = new PlanningMovingAlternative(forked.ActionId,
            opportunity.WithCastReach(forked.Range + forked.PlayerRadius + anchor.Radius), forked.TravelSeconds,
            timing with { NativeRecast = forked.RecastSeconds });
        return action with { MovingAlternatives = [alternative], ResourceCost = forked.ResourceCost };
    }
}
