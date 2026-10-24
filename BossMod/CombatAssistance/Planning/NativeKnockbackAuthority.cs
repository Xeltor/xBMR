using BossMod.Components;

namespace BossMod.CombatAssistance.Planning;

internal sealed record NativeKnockbackAuthority(string Reason, int SourceCount, float? FirstActivationIn,
    bool PendingEffect, bool IncompleteEvidence);

// Capture native mechanic authority before an ordinary walking/casting forecast.
// Until joint forced-motion timing is represented, do not publish a false proof
// that a cast or walking route passes straight through an authored knockback.
// A null result is NOT proof that no source was published: a known source may
// be covered by observed immunity or lie outside the current planning horizon.
// Admission of learned fallback requires a separate complete source inventory.
internal static class NativeKnockbackAuthorityCapture
{
    internal static NativeKnockbackAuthority? Capture(WorldState world, BossModule? module, Actor actor)
        => Capture(NativeDisplacementCapture.Capture(world, module, actor).Snapshot);

    internal static NativeKnockbackAuthority? Capture(NativeDisplacementInventory inventory)
    {
        if (inventory.Pending)
            return new("native knockback effect pending; awaiting native displacement authority", 0, null, true, false);
        if (inventory.FailedActiveOwners > 0)
            return new("native knockback participant or source capture is unavailable; retaining native movement", 0, null, false, true);
        if (inventory.Knockbacks.IsEmpty && inventory.PreparationInterruptsForecast)
            return new("native knockback preparation has no modeled source; retaining native movement", 0, null, false, true);
        var count = 0;
        float? earliest = null;
        var unknown = false;
        foreach (var source in inventory.Knockbacks)
        {
            if (!source.Interrupts(inventory.At)) continue;
            var activation = source.Activation == default ? (float?)null : (float)(source.Activation - inventory.At).TotalSeconds;
            ++count;
            unknown |= activation == null || !source.GeometryKnown;
            if (activation != null) earliest = earliest == null ? activation : Math.Min(earliest.Value, activation.Value);
        }
        return count == 0 ? null : new(unknown
            ? "native knockback source has incomplete evidence; retaining native movement"
            : "native knockback projection requires forced-motion planning; retaining native movement",
            count, earliest, false, unknown);
    }
}
