using System.Collections.Immutable;

namespace BossMod.CombatAssistance.Targeting;

internal enum TargetSetupProgress { Pending, Confirmed, Failed, Foreign, Released }
internal sealed record NativeTargetSetup(ulong Original, ulong Target, uint Action, ImmutableArray<ulong> Required,
    long Requested, long Deadline);

// One current handoff, no saved lease or history. Exact clocks belong to the
// selected plan; polling cannot extend them. Foreign changes always win.
internal sealed class NativeTargetSetupOwner
{
    internal NativeTargetSetup? Pending { get; private set; }
    internal void Begin(NativeTargetSetup setup)
    {
        if (setup.Original == 0 || setup.Target == 0 || setup.Original == setup.Target || setup.Action == 0
            || setup.Requested >= setup.Deadline || setup.Required.IsDefault) throw new ArgumentException("Incomplete native target handoff.");
        Pending = setup with { Required = setup.Required.ToImmutableArray() };
    }
    internal TargetSetupProgress Observe(ulong hardTarget, uint action, ulong selectedTarget,
        ImmutableArray<ulong> recipients, long selectionObserved, long now, bool authority)
    {
        if (Pending is not { } pending) return TargetSetupProgress.Released;
        if (!authority) { Pending = null; return TargetSetupProgress.Released; }
        if (hardTarget != pending.Original && hardTarget != pending.Target)
        { Pending = null; return TargetSetupProgress.Foreign; }
        if (hardTarget == pending.Target && action == pending.Action && selectedTarget == pending.Target
            && selectionObserved > pending.Requested && !recipients.IsDefault && pending.Required.All(recipients.Contains))
        { Pending = null; return TargetSetupProgress.Confirmed; }
        if (now >= pending.Deadline) { Pending = null; return TargetSetupProgress.Failed; }
        return TargetSetupProgress.Pending;
    }
    internal void Release() => Pending = null;
}
