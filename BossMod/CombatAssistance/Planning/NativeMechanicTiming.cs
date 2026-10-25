using BossMod.CombatAssistance.Config;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

internal static class NativeMechanicTiming
{
    // Maintainer-requested clearance preference: these jobs should visibly clear
    // mechanics (including tank busters) before the final moment. This is a
    // planning margin, not a claim about an encounter's server snapshot timing.
    internal const float EarlyClearanceSeconds = 2.5f;

    internal static bool PrefersEarlyClearance(Class job)
        => JobRoles.GetRangeRole((uint)job) is RangeRole.Healer or RangeRole.PhysicalRanged;

    internal static NavigationDecision.CapturedInput Apply(NavigationDecision.CapturedInput input, Class job)
        => PrefersEarlyClearance(job) ? input with { WarningTimeCushion = EarlyClearanceSeconds } : input;
}
