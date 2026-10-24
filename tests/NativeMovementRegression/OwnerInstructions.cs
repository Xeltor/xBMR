using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void OwnerInstructions()
    {
        var map = Grid(5, 3);
        map.PixelMaxG[7] = -1;
        map.PixelPriority[0] = 100000;
        var input = Context(map, 5);
        var target = Point(map, 9) + new WDir(0.2f, 0.1f);
        var context = new MovementPlanningContext(input.Identity, map, 0, 0,
            Enumerable.Repeat(true, 15).ToArray(), new float[15], [], [], requiredDestination: target, arrivalDeadline: 10);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.UsefulDestination == target && plan.Stages.Length > 1,
            "explicit instructions retain their exact endpoint through a verified detour");
        Check(plan.Stages.All(s => context.Traverse(s.From, s.To, s.StartSeconds, s.EndSeconds - s.StartSeconds, out _)),
            "an explicit destination does not grant permission to cross physical obstructions");
        var deadline = new MovementPlanningContext(input.Identity, map, 0, 0,
            Enumerable.Repeat(true, 15).ToArray(), new float[15], [], [], requiredDestination: target, arrivalDeadline: 0.1f);
        Check(!JointMovementSearch.Search(deadline, Exhaustive).HasVerifiedRoute,
            "an impossible explicit arrival deadline is reported without inventing a substitute endpoint");
        Check(!MovementPlanValidation.Validate(plan, deadline, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "explicit execution rechecks the original instruction deadline");
        var changed = new MovementPlanningContext(input.Identity, map, 0, 0,
            Enumerable.Repeat(true, 15).ToArray(), new float[15], [], [], requiredDestination: Point(map, 0));
        Check(!changed.SameProblem(context) && !MovementPlanValidation.Validate(plan, changed, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "a new explicit instruction invalidates the previous endpoint");

        var decision = new NavigationDecision { Destination = Point(map, 6), LeewaySeconds = 0.2f, TimeToGoal = 1 };
        decision.Publication = new(input.Identity, null, [input.Origin, Point(map, 6)], true, true, ThetaStar.Score.UnsafeImprove, decision, false);
        var recovery = plan with { Status = MovementSearchStatus.NativeRecovery, NativeRecovery = decision };
        var executor = new MovementPlanExecutor();
        var now = input.Identity.CapturedTimestamp;
        Check(executor.AdvanceRecovery(recovery, input.Identity, true, true, now).Kind == MovementExecutionKind.Walk,
            "native unsafe-start egress uses the shared executor without being relabeled safe");
        var later = now + (long)(0.1 * Stopwatch.Frequency);
        Check(executor.AdvanceRecovery(recovery, input.Identity, true, true, later).DepartureSeconds < 0.11f,
            "repeated native recovery observations cannot refresh their departure deadline");
        Check(executor.AdvanceRecovery(recovery, input.Identity, false, true, now).Kind == MovementExecutionKind.Invalidated,
            "manual input stops native recovery through the shared executor");
        Check(executor.AdvanceRecovery(recovery, input.Identity, true, false, now).Kind == MovementExecutionKind.Invalidated,
            "changed executable geometry rejects a native recovery route");
        Check(executor.AdvanceRecovery(recovery, input.Identity, true, true, now + Stopwatch.Frequency).Kind == MovementExecutionKind.Invalidated,
            "stale native recovery cannot execute");
        Check(executor.AdvanceRecovery(recovery, input.Identity with { ProjectionLayer = 1 }, true, true, now).Kind == MovementExecutionKind.Invalidated,
            "native recovery cannot cross an unverified projection layer");
    }
}
