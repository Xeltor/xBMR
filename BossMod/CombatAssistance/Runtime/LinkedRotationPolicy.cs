using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Runtime;

internal static class LinkedRotationPolicy
{
    // Native movement must already be active. Importing Enabled/Link preferences
    // alone cannot start either AI or rotation. Existing foreign modes are retained.
    internal static bool ShouldStart(bool assistance, bool linked, bool nativeActive, bool handoff, RsrMode? mode)
        => assistance && linked && nativeActive && !handoff && mode == RsrMode.Off;
}
