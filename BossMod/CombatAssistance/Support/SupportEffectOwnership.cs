// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System.Collections.Generic;

namespace BossMod.CombatAssistance.Support;

internal static class SupportEffectOwnership
{
    // RSR's current eligible set already applies its configured refresh window
    // to the local caster's card. Foreign cards remain observed overlap.
    public static bool BlocksCardRecipient(uint status, uint expected, bool own, bool eligible)
        => expected != 0 && status == expected && (!own || !eligible);

    // Kardion is applied by the Sage directly. A surviving status with an
    // unresolved or unavailable source does not establish active peer coverage.
    public static bool IsOtherKardion(uint status, ulong source, ulong player, IReadOnlySet<ulong> otherSages)
        => status == 2605 && player != 0 && source != 0 && source != player && otherSages.Contains(source);

    // SCH SID 1223 is the recipient's observed Fey Union effect. Require a
    // visible living party Scholar as source or pet owner; unknown provenance
    // is not evidence that another healer is covering this recipient.
    public static bool IsOtherFeyUnion(uint status, ulong source, ulong owner, ulong player, IReadOnlySet<ulong> otherScholars)
        => status == 1223 && player != 0 && source != player && owner != player &&
           (source != 0 && otherScholars.Contains(source) || owner != 0 && otherScholars.Contains(owner));
}
