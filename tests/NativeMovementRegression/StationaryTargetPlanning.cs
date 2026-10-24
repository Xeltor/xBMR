using BossMod;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void StationaryTargetPlanning()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 100;
        var identity = Context(map, 0).Identity;
        MovementPlanningContext Scope(bool stationary) => new(identity, map, 0, 0, [true, true, true],
            [0, 0, 0], [], [], stationaryOnly: stationary);
        var movement = Scope(false);
        var stationary = Scope(true);
        var normal = JointMovementSearch.Search(movement, Exhaustive);
        var targetOnly = JointMovementSearch.Search(stationary, Exhaustive);
        Check(normal.UsefulDestination == Point(map, 2) && targetOnly.UsefulDestination == identity.Origin
            && !targetOnly.Stages.Any(stage => stage.From != stage.To),
            "Stationary target permission cannot turn a native score gain into elective walking.");
        Check(!MovementPlanValidation.Validate(normal, stationary, 0, true, identity.CapturedTimestamp).Allowed,
            "Disabling movement invalidates an old executable walking commitment before it can be applied.");
        Check(stationary.Excluding([]).StationaryOnly && !stationary.SameProblem(movement, false),
            "Target-only permission survives immutable rejection copies and invalidates the previous worker input.");

        // Exercise the production native-recovery/joint-search boundary, not a
        // second synthetic safety implementation. Required native movement wins.
        identity = identity with { Origin = new(-2.5f, -2.5f), CapturedTimestamp = Stopwatch.GetTimestamp() };
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(4, mapResolution: 1), default, null, default, false);
        var input = new NavigationDecision.CapturedInput(source, [], [],
            new([point => point.X > 1 && point.Z > 1 ? 10 : 0]), true, [], identity, false);
        MovementPlanningContext NativeScope(NavigationDecision.CapturedInput captured)
        {
            var native = NativeNavigationSnapshot.Capture(captured);
            var raster = native.CopyStrictMap();
            var cells = raster.Width * raster.Height;
            return new(captured.Identity, raster, 0, 0, Enumerable.Repeat(true, cells).ToArray(),
                new float[cells], [], [], native: native, stationaryOnly: true);
        }
        foreach (var captured in new[] { input, input with
        {
            Goals = new([]),
            Warnings = [(new SDCircle(identity.Origin, 1), identity.WorldTime.AddSeconds(1.8), 0)]
        } })
        {
            var context = NativeScope(captured);
            var result = NativeMovementSearch.Run(context, default, _ => { });
            Check(result.Status == MovementSearchStatus.NativeRecovery && result.NativeRecovery?.Destination != null
                && result.DeliveredActions == 0,
                "Stationary target optimization cannot suppress native goal movement or mandatory warning recovery.");
        }
        var idle = NativeMovementSearch.Run(NativeScope(input with { Goals = new([]) }), default, _ => { });
        Check(idle.HasVerifiedRoute && idle.UsefulDestination == identity.Origin
            && idle.Stages.All(stage => stage.From == stage.To),
            "The shared production search accepts a stationary opportunity when native movement is not required.");
    }
}
