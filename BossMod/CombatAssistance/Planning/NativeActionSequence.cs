namespace BossMod.CombatAssistance.Planning;

internal static class NativeActionSequence
{
    // The migrated direct party-heal catalogue identifies effect semantics only;
    // current RSR/native evidence still supplies radius, recipients and timing.
    // A self-only field or buff with a radius is not a party healing action.
    internal static bool IsImmediatePartyHeal(uint id) => id is 3583 or 16553 or 7445
        or 24288 or 24298 or 24299 or 24302 or 24310 or 24311;

    // RSR selected the immediate ability ahead of its GCD. Preserve that current
    // opportunity and each of its recipients before forecasting the GCD. Native
    // rotation delivery still executes both; the planner never queues duplicates.
    internal static ImmutableArray<PlanningAction> Compose(PlanningAction? immediate, PlanningAction? gcd)
    {
        if (immediate == null) return gcd == null ? [] : [gcd];
        if (gcd == null || immediate.Id == gcd.Id && immediate.TargetId == gcd.TargetId) return [immediate];
        return [immediate, gcd with { RequiredEarlierActions = 1 }];
    }

    internal static byte CompleteMask(int count) => (byte)((1 << count) - 1);
}
