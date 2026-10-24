using BossMod.Components;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Knockback;

namespace BossMod.CombatAssistance.Planning;

internal sealed record KnockbackTransactionFrame(NativeKnockbackPhase Phase, WPos Launch, WPos Landing,
    WPos UsefulDestination, bool Preparing, string? Failure = null);

// One movement owner's currently armed native event. This owns no movement,
// history or timer renewal: every continuation keeps the original absolute
// source/window, and physical completion comes from the native observer.
internal sealed class NativeKnockbackTransaction
{
    private string? _owner;
    private object? _module;
    private NavigationIdentity? _identity;
    private KnockbackTransactionFrame? _frame;
    private DateTime _lastAt;
    internal bool Active => _frame != null;
    internal NativeKnockbackPhase? Phase => _frame?.Phase;
    internal float CastWindow(DateTime now) => _frame?.Failure != null ? 0 : _frame?.Phase.Timing is not { } timing || now >= timing.LandingLatestUtc
        ? float.MaxValue : Math.Max(0, (float)(Earliest(_frame.Phase) - now).TotalSeconds);

    internal void ObservePreparation(string owner, object? module, NavigationIdentity identity,
        NativeKnockbackPhase phase, WPos position)
    {
        if (phase.Timing == null || phase.CastGeneration == 0 || phase.Source.CastActionId == 0
            || phase.Source.SourceDataId == 0 || identity.WorldTime >= Earliest(phase)) return;
        if (_frame != null && (!ReferenceEquals(module, _module) || _owner != owner
            || _identity?.ActorId != identity.ActorId || !_frame.Phase.SameBinding(phase))) Release();
        if (!phase.TryProject(position, out var landing)) return;
        _frame = new(phase, position, landing, _frame?.UsefulDestination ?? landing, true);
        _owner = owner; _module = module; _identity = identity; _lastAt = identity.WorldTime;
    }

    internal void ObservePlan(MovementPlan plan)
    {
        if (_frame != null && plan.Stages.Any(stage => stage.Knockback?.SameBinding(_frame.Phase) == true))
            _frame = _frame with { UsefulDestination = plan.UsefulDestination };
    }

    internal KnockbackTransactionFrame? Capture(string owner, object? module, NavigationIdentity identity,
        NativeDisplacementInventory inventory, KnockbackCompletionEvidence? completion, bool authority)
    {
        if (_frame == null) return null;
        if (!authority || _owner != owner || !ReferenceEquals(module, _module)
            || _identity?.ActorId != identity.ActorId || _identity.ZoneId != identity.ZoneId
            || _identity.ContentId != identity.ContentId || identity.WorldTime < _lastAt)
        { Release(); return null; }
        _lastAt = identity.WorldTime;
        var phase = _frame.Phase;
        if (phase.Completed(identity, identity.Origin, inventory, completion, _frame.Launch, _frame.Landing)
            || _frame.Failure != null && phase.ObservedCompletion(identity, identity.Origin, inventory, completion))
        { Release(); return null; }
        if (phase.Timing == null || identity.WorldTime >= phase.Timing.LandingLatestUtc)
        {
            var failed = _frame with { Preparing = false, Failure = "native knockback window ended without physical completion" };
            Release();
            return failed;
        }
        var published = inventory.Knockbacks.Any(source => source.SameGeometry(phase.Source));
        if (published && identity.WorldTime < Earliest(phase)) return _frame;
        var segment = _frame.Landing - _frame.Launch;
        var length = segment.LengthSq();
        var progress = length <= 1e-8f ? 0 : (identity.Origin - _frame.Launch).Dot(segment) / length;
        var projected = _frame.Launch + Math.Clamp(progress, 0, 1) * segment;
        if (progress < -.01f || progress > 1.01f || !identity.Origin.AlmostEqual(projected, MovementPlanValidation.ArrivalTolerance))
            _frame = _frame with { Failure = "observed displacement left its verified native corridor" };
        _frame = _frame with { Preparing = false };
        return _frame;
    }

    internal void Invalidate(string reason)
    {
        if (_frame != null) _frame = _frame with { Failure = reason };
    }

    internal void Release()
    { _frame = null; _owner = null; _module = null; _identity = null; _lastAt = default; }

    private static DateTime Earliest(NativeKnockbackPhase phase) => phase.Timing is { } timing
        && timing.EarliestUtc < phase.Source.Activation ? timing.EarliestUtc : phase.Source.Activation;
}
