using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void EscapeCommitment()
    {
        var identity = Context(Grid(9, 1), 4, speed: 6).Identity with { Origin = new(1.5f, .5f) };
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(6, mapResolution: 1), default, null, default, false);
        var warning = new SDCircle(default, 4);
        MovementPlanningContext Capture(float elapsed, WPos position, bool pending = true, bool newGoal = false, bool obstruction = false, ulong target = 10)
        {
            var pose = identity with { Origin = position, WorldTime = identity.WorldTime.AddSeconds(elapsed), CapturedTimestamp = Stopwatch.GetTimestamp() };
            var input = new NavigationDecision.CapturedInput(source, obstruction ? [new SDCircle(new(3.5f, .5f), .5f)] : [],
                pending ? [(warning, identity.WorldTime.AddSeconds(4), 0)] : [],
                new([point => newGoal && point.X < 0 ? 10 : 0]), true, [], pose, true);
            var native = NativeNavigationSnapshot.Capture(input);
            return new(pose, native.CopyStrictMap(), 0, 0, Enumerable.Repeat(true, native.Cells).ToArray(), new float[native.Cells],
                [], [], 1, native: native, targetId: target, range: new(target, new(-2.5f, .5f), .1f, true));
        }
        var initial = Capture(0, identity.Origin);
        var route = NativeMovementIncumbent.CaptureRoute(initial, [initial.Origin, new(5.5f, .5f)])!;
        Check(route != null, "The cover fixture supplies a complete native escape witness.");
        var commitment = NativeEscapeCommitment.Begin(initial, route!, 0);
        Check(commitment != null, "Walking out of a pending native hazard establishes the selected escape commitment.");
        var moved = Capture(.2f, new(2.5f, .5f), newGoal: true);
        var continued = commitment!.Continue(moved);
        Check(continued is { HasVerifiedRoute: true } && continued.UsefulDestination == route.UsefulDestination
            && continued.Stages.All(s => s.ActionId == 0)
            && MovementPlanValidation.Validate(continued, moved, 0, true, moved.Identity.CapturedTimestamp).Allowed,
            "Rotation goal/readiness changes cannot reverse a started escape; its full remainder uses fresh native deadlines.");
        var covered = Capture(.6f, new(5.5f, .5f), newGoal: true);
        Check(commitment.Continue(covered) is { Stages.Length: 0 },
            "Reaching cover retains the same safe destination while the original mechanic remains pending.");
        Check(commitment.Continue(Capture(1, new(5.5f, .5f), pending: false)) == null,
            "An authored hazard clearing immediately releases cover so normal aggressive recovery can resume.");
        Check(commitment.Continue(Capture(.2f, new(2.5f, .5f), obstruction: true)) == null
            && commitment.Continue(Capture(.2f, new(2.5f, .5f), target: 11)) == null,
            "New physical geometry or target identity retires the old escape rather than pinning an invalid route.");
        Check(commitment.Continue(Capture(4, identity.Origin)) == null,
            "Elapsed deadlines cannot be refreshed by re-reading an old escape commitment.");
        Check(NativeEscapeCommitment.Begin(initial, route with
            { Stages = [new(MovementStageKind.Wait, initial.Origin, initial.Origin, 0, 1), .. route.Stages] }, 0) == null,
            "A safe decision to remain fighting before departure does not establish an escape before walking actually begins.");
    }
}
