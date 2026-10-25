namespace BossMod.CombatAssistance.Planning;

internal sealed record HealingDeliveryRequest(PlanningAction Action, float CompletesIn);

// Framework-thread delivery handoff, not another movement owner or rotation.
// A planned cast is not a delivered heal. Keep the usable pose until its native
// effect, cancellation or original observation deadline, including Eukrasia setup.
internal sealed class NativeHealingDelivery
{
    private (ulong Actor, ushort Zone, Class Job, uint Action, ulong Target)? _binding;
    private PlanningAction? _action;
    private DateTime? _expires;
    private bool _finished;

    internal HealingDeliveryRequest? Update(DateTime now, ulong actor, ushort zone, Class job, WPos origin,
        PlanningAction? selected, float completionIn, uint castingAction = 0, float castRemaining = 0)
    {
        if (selected == null && _action?.Id == castingAction && castRemaining > 0)
            selected = _action;
        if (selected == null) { Reset(); return null; }
        var binding = (actor, zone, job, selected.Id, selected.TargetId);
        if (_binding != binding)
        {
            Reset();
            _binding = binding;
        }
        _action = selected;
        if (_finished || selected.Opportunity?.Evaluate(origin).Allowed != true)
            return null;
        // Start the bound only once we can actually deliver. Do not refresh it
        // each frame when an unavailable/stalled RSR selection remains visible.
        if (!float.IsFinite(completionIn) || completionIn <= 0) return null;
        _expires ??= now.AddSeconds(completionIn);
        if (now >= _expires && castingAction != selected.Id)
        {
            _finished = true;
            return null;
        }
        var remaining = castingAction == selected.Id && castRemaining > 0 ? castRemaining
            : Math.Max(0, (float)(_expires.Value - now).TotalSeconds);
        return remaining > 0 ? new(selected, remaining) : null;
    }

    internal void Observe(ulong actor, uint action)
    {
        if (_binding is { } binding && binding.Actor == actor && binding.Action == action)
            _finished = true;
    }

    internal static bool CanHold(MovementPlanningContext context, HealingDeliveryRequest request)
        // Only an indefinitely safe native footprint qualifies. Any current or
        // future hazard, displacement, gathering assignment or explicit movement
        // keeps the normal complete route; no greed window is inferred here.
        => request.CompletesIn > 0 && float.IsFinite(request.CompletesIn)
            && context.RequiredDestination == null && !context.StationaryOnly
            && context.ForcedMovement == null && context.KnockbackPhases?.Phases.IsEmpty != false
            && context.Native?.HasStackGathering != true
            && context.PartyContact?.Loss(context.Origin) is not > 0
            && context.CanRemain(context.Origin)
            && context.Traverse(context.Origin, context.Origin, 0, request.CompletesIn, out _)
            && request.Action.Opportunity?.Evaluate(context.Origin) is { Allowed: true } coverage
            && context.ExecutionRules.PermitsAction(coverage.Facing, 0, request.CompletesIn);

    internal void Reset()
    {
        _binding = null;
        _action = null;
        _expires = null;
        _finished = false;
    }
}
