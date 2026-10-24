namespace BossMod.CombatAssistance.Planning;

internal readonly record struct PreparationCost(int SourceActionIndex, ulong Resources);
internal readonly record struct PreparationGrant(int SourceActionIndex, ulong Resources);

// Known action effects, not a guessed rotation. A selected setup can produce
// one bounded movement opportunity. Its real native proc is still required at
// submission; the worker only reasons about the explicitly conditional route.
internal static class NativeRotationalPreparation
{
    internal static uint MovementFor(Class job, uint setup) => (job, setup) switch
    {
        (Class.NIN, 2267) => 25777,
        (Class.WAR, 7389) => 25753,
        (Class.SMN, 25805 or 25838) => 25835,
        (Class.DRG, 3555) => 16480,
        _ => 0
    };

    internal static bool IsImmediateSetup(uint id) => id is 7389 or 3555;

    // First mudras also consume a shared spell clock. Their primary 20s charge
    // cooldown is not that clock; the native follow-up action supplies it.
    internal static uint SharedClockAction(uint id) => id switch
    { 2259 => 18805, 2261 => 18806, 2263 => 18807, _ => id };

    internal static bool MayFinishMudra(Class job, uint movement, ImmutableArray<PlanningAction> actions, ulong player, ulong target)
        => job == Class.NIN && movement == 25777 && SourceIndex(job, movement, actions, player, target) >= 0;

    internal static int PreparationIndex(Class job, uint movement, ImmutableArray<PlanningAction> actions, ulong player, ulong target,
        bool currentlyUsable, bool activeMudra)
        => currentlyUsable && !activeMudra ? -1 : SourceIndex(job, movement, actions, player, target);

    internal static int SourceIndex(Class job, uint movement, ImmutableArray<PlanningAction> actions, ulong player, ulong target)
    {
        if (actions.IsDefault) return -1;
        for (var i = 0; i < actions.Length; ++i)
            if (MovementFor(job, actions[i].Id) == movement && actions[i].CastSeconds == 0
                && actions[i].TargetId == (actions[i].Id == 7389 ? player : target)) return i;
        return -1;
    }

    internal static ImmutableArray<PlanningAction> Apply(ImmutableArray<PlanningAction> actions, ImmutableArray<PreparationGrant> grants, ImmutableArray<PreparationCost> costs = default)
        => grants.IsDefaultOrEmpty && costs.IsDefaultOrEmpty ? actions : actions.Select((a, i) => a with
        { GrantedResources = grants.IsDefaultOrEmpty ? a.GrantedResources : grants.Where(g => g.SourceActionIndex == i).Aggregate(a.GrantedResources, (mask, grant) => mask | grant.Resources),
            ResourceCost = costs.IsDefaultOrEmpty ? a.ResourceCost : costs.Where(c => c.SourceActionIndex == i).Aggregate(a.ResourceCost, (mask, cost) => mask | cost.Resources) }).ToImmutableArray();
}
