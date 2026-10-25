using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;

public static partial class Checks
{
    private static void UntimedMechanicUrgency()
    {
        var map = Grid(8, 4);
        var origin = Point(map, 9);
        var tower = Point(map, 13);
        var identity = Context(map, 9).Identity;
        var now = identity.WorldTime;
        var source = NavigationMapSource.Capture(new ArenaBoundsRect(4, 2, mapResolution: 1), default, null, default, false);
        var requirement = new SDInvertedCircle(tower, 1.4f);
        var input = new NavigationDecision.CapturedInput(source, [], [(requirement, DateTime.MaxValue, 99ul)],
            new([]), false, [], identity, true);

        foreach (var activation in new[] { DateTime.MaxValue, default(DateTime) })
        {
            var unknown = input with { Warnings = [(requirement, activation, 99ul)] };
            var frozen = NativeNavigationSnapshot.Capture(unknown);
            var strict = frozen.CopyStrictMap();
            var cell = strict.GridToIndex(strict.WorldToGrid(origin));
            Check(strict.PixelMaxG[cell] == 0,
                "An unknown tower activation is due NOW, never a synthesized two-minute greed window.");
            Check(unknown.Warnings[0].activation == activation && unknown.Warnings[0].Source == 99ul,
                "Urgency changes raster timing without rewriting authored activation or source provenance.");
            var context = new MovementPlanningContext(identity, strict, 0, 0,
                Enumerable.Repeat(true, frozen.Cells).ToArray(), new float[frozen.Cells], [], [], native: frozen);
            var recovery = NativeMovementSearch.Recover(context, new());
            Check(recovery.NativeRecovery is { Destination: not null, LeewaySeconds: <= 0 }
                && context.CanRemain(recovery.UsefulDestination) && !NativeMovementHost.CanDeferRecovery(context, recovery),
                $"Unknown activation immediately executes native recovery instead of waiting for optimization: {recovery.NativeRecovery?.Destination}, leeway={recovery.NativeRecovery?.LeewaySeconds}, score={recovery.NativeRecovery?.Publication?.NativeScore}.");
            var execution = new MovementPlanExecutor().AdvanceRecovery(recovery, identity, true,
                frozen.CanExecuteRecovery(recovery.NativeRecovery!.Value.Publication!, origin), identity.CapturedTimestamp);
            Check(execution is { Kind: MovementExecutionKind.Walk, DepartureSeconds: 0 },
                "Immediate native recovery publishes walking with zero cast leeway even when the starting position is already overdue.");
            var ordinary = NavigationDecision.BuildCaptured(new(), unknown);
            Check(ordinary.Destination == recovery.NativeRecovery!.Value.Destination && ordinary.LeewaySeconds <= 0,
                "Ordinary BMR and combat assistance agree on immediate untimed mechanic movement.");
            var cells = frozen.Cells;
            var cast = Action(strict, 12, 1, Enumerable.Repeat(1ul, cells).ToArray(), Enumerable.Repeat(1f, cells).ToArray(), cast: 2);
            var casting = new MovementPlanningContext(identity, strict, 0, 2,
                Enumerable.Repeat(true, cells).ToArray(), new float[cells], [cast], [], native: frozen, lateDepartureMargin: .5f);
            Check(!JointMovementSearch.Search(casting, Exhaustive).HasVerifiedRoute,
                "A cast or greedy departure wait cannot certify staying outside an untimed required position.");
        }

        // The original list places MaxValue last. Both corner and center
        // sampling must still select the untimed requirement before a real timer.
        var timed = new SDCircle(origin, 1.1f);
        var overlap = NativeNavigationSnapshot.Capture(input with
        {
            Warnings = [(timed, now.AddSeconds(8), 7ul), (requirement, DateTime.MaxValue, 99ul)]
        }).CopyStrictMap();
        Check(overlap.PixelMaxG[overlap.GridToIndex(overlap.WorldToGrid(origin))] == 0,
            "An overlapping timed hazard cannot conceal an untimed requirement sorted last by authored activation.");

        var knownInput = input with { Warnings = [(timed, now.AddSeconds(8), 7ul)] };
        var known = NativeNavigationSnapshot.Capture(knownInput);
        var knownMap = known.CopyStrictMap();
        Check(knownMap.PixelMaxG[knownMap.GridToIndex(knownMap.WorldToGrid(origin))] == 7,
            "A known eight-second activation retains the existing one-second movement margin.");
        var count = known.Cells;
        var attack = Action(knownMap, 12, 1, Enumerable.Repeat(1ul, count).ToArray(), Enumerable.Repeat(1f, count).ToArray(), cast: 1);
        var safeGreed = new MovementPlanningContext(identity, knownMap, 0, 0,
            Enumerable.Repeat(true, count).ToArray(), new float[count], [attack], [], native: known);
        var plan = JointMovementSearch.Search(safeGreed, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.DeliveredActions == 1 && plan.DeliveryTimes[0] == 1
            && MovementPlanValidation.Validate(plan, safeGreed, 0, true, identity.CapturedTimestamp).Allowed,
            "Known timing still permits a safe cast with a complete verified onward escape.");

        var cleared = NativeNavigationSnapshot.Capture(input with { Warnings = [] }).CopyStrictMap();
        Check(cleared.PixelMaxG[cleared.GridToIndex(cleared.WorldToGrid(origin))] == float.MaxValue,
            "Removing or cancelling the mechanic removes its urgent constraint on the next capture.");
    }
}
