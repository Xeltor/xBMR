namespace BossMod.CombatAssistance.Planning;

// Preserve the current GCD for a proved earlier melee arrival. The bound is the
// observed fallback's own recovery clock, never a renewed transport lease.
internal sealed class NativeMeleeRecoveryWindow
{
    private (ulong Actor, ulong Target, uint Action, DateTime Gcd, long Problem)? _key;
    private DateTime _until;

    internal bool Defer(ulong actor, ulong target, uint action, DateTime gcd, DateTime now,
        float ready, float recovery, float arrival, long problem = 0)
    {
        if (actor == 0 || target == 0 || action == 0 || !float.IsFinite(ready) || ready < 0
            || !float.IsFinite(recovery) || recovery <= 0 || !float.IsFinite(arrival) || arrival < 0
            || ready + recovery > MovementPlanningContext.MaximumHorizonSeconds || arrival >= ready + recovery)
            return false;
        // The host revision changes on material input, not equivalent frame or
        // deadline countdown observations. A newly proved escape/re-entry may
        // replace a retired opportunity without renewing the old result.
        var key = (actor, target, action, gcd, problem);
        var until = now.AddSeconds(ready + recovery);
        if (_key != key) { _key = key; _until = until; }
        else if (until < _until) _until = until;
        return now < _until && now.AddSeconds(arrival) < _until;
    }

    internal static float? VerifiedArrival(MovementPlan plan, MovementPlanningContext context, int stage, ulong target,
        long timestamp, WPos actual)
    {
        if (plan.Status == MovementSearchStatus.NativeRecovery)
        {
            if (plan.NativeRecovery?.Publication is not { } publication
                || NativeMovementIncumbent.Capture(context, publication) is not { } strict)
                return null;
            plan = strict;
            stage = 0;
        }
        if (!plan.HasVerifiedRoute || context.StationaryOnly || context.Range is not { RecoverExecution: true } range
            || range.TargetId != target || context.TargetId != target || !range.NeedsRecovery(actual)
            || range.Loss(plan.UsefulDestination) != 0
            || System.Diagnostics.Stopwatch.GetElapsedTime(context.Identity.CapturedTimestamp, timestamp).TotalSeconds is < 0 or >= .25)
            return null;
        var proof = MovementPlanValidation.Validate(plan, context, stage, true, timestamp, actual, continuingCommittedRoute: true);
        return proof.Allowed && float.IsFinite(proof.CompletionSeconds) && proof.CompletionSeconds >= 0 ? proof.CompletionSeconds : null;
    }

    internal void Reset() { _key = null; _until = default; }
}
