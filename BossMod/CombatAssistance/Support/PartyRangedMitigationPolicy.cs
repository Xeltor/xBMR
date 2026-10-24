// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BossMod.CombatAssistance.Support;

// These three party buffs share non-stacking protection. Other mitigation
// families remain complementary and must not suppress this one.
internal static class PartyRangedMitigationPolicy
{
    public const string Family = "ranged-physical";

    public static bool Unlocked(uint job, byte level) => job switch
    {
        23 => level >= 62,
        31 => level >= 56,
        38 => level >= 56,
        _ => false,
    };

    public static (string Property, uint Action, string Family) ActionForJob(uint job) => job switch
    {
        23 => ("TroubadourPvE", 7405, Family),
        31 => ("TacticianPvE", 16889, Family),
        38 => ("ShieldSambaPvE", 16012, Family),
        _ => ("", 0, ""),
    };

    public static bool Covered(DateTime now, DateTime deadline, IReadOnlyList<ulong> recipients,
        IReadOnlyList<PartyMitigationEffect> effects)
    {
        var remaining = (deadline - now).TotalSeconds;
        if (remaining is <= 0 or > 10 || recipients.Count is 0 or > 8 || recipients.Any(id => id == 0)) return false;
        return recipients.All(id => effects.Any(effect => effect.Actor == id && effect.Source != 0 &&
            effect.Status is 1826 or 1934 or 1951 or 2177 && float.IsFinite(effect.RemainingSeconds) &&
            effect.RemainingSeconds >= remaining + 0.5));
    }
}
