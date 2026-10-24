using BossMod.CombatAssistance.Config;

namespace BossMod.CombatAssistance.Planning;

// Reach is a maximum, not a request to stand at maximum range. A shorter action
// or an independent assignment/recipient requirement is enforced by the planner.
internal static class CombatFollowRange
{
    internal static float Resolve(Role role, float fixedRange) => role switch
    {
        Role.Tank or Role.Melee => AssistancePreferences.InternalMeleeUptimeRange,
        Role.Ranged or Role.Healer => AssistancePreferences.InternalRangedUptimeRange,
        _ => fixedRange
    };
}
