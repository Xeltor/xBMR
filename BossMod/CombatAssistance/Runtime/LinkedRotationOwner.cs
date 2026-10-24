using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Runtime;

// Scoped ownership: a user/other plugin selecting a different mode wins. There
// is no saved-setting mutation and no ownership across plugin instances.
internal sealed class LinkedRotationOwner(Func<RsrMode?> read, Func<RsrMode, bool> write)
{
    private bool _owned;
    internal bool Owned => _owned;

    internal void Update(bool assistance, bool linked, bool nativeActive, bool handoff)
    {
        if (!_owned && (!assistance || !linked || !nativeActive || handoff)) return;
        var current = read();
        if (_owned && current != null && current != RsrMode.Henched) _owned = false;
        if (!assistance || !linked || !nativeActive)
        {
            if (_owned && current == RsrMode.Henched && write(RsrMode.Off)) _owned = false;
            return;
        }
        if (LinkedRotationPolicy.ShouldStart(assistance, linked, nativeActive, handoff, current))
            _owned = write(RsrMode.Henched);
    }

    internal void Release() => Update(false, false, false, true);
}
