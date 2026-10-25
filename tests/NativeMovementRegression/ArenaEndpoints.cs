using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void ArenaEndpoints()
    {
        var map = Grid(11, 11);
        var baseline = Context(map, 55, speed: 6);
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(5.5f) };
        var interior = NativePresentationCapture.CaptureArenaInterior(map, hints, true)!;
        MovementPlanningContext Input(bool enabled = true, int origin = 55, bool[]? allowed = null,
            PlanningHealingCoverage? healing = null, WPos? required = null)
            => new(baseline.Identity with { Origin = Point(map, origin), CapturedTimestamp = Stopwatch.GetTimestamp() }, map, 0, 0,
                allowed ?? Enumerable.Repeat(true, 121).ToArray(), new float[121], [], [],
                range: new(10, default, 20, RecoverExecution: true), requiredDestination: required, healingCoverage: healing,
                arenaInterior: enabled ? interior : null, movementValue: PlanningMovementValue.Capture(Class.SGE, 0, []));
        var input = Input();
        var plan = JointMovementSearch.Search(input, Exhaustive, stopAtCurrentOpportunity: true);
        Check(plan.HasVerifiedRoute && input.PreferredArenaEndpoint(plan.UsefulDestination) && plan.Stages.Any(s => s.Kind == MovementStageKind.Walk)
            && MovementPlanValidation.Validate(plan, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "A healer prefers usable arena interior over conserving a short walk while standing at the edge.");
        var reference = JointMovementSearch.Search(input, Exhaustive, proveCompleteOptimum: false,
            pruneExecutionInferiorStates: false, pruneResourceInferiorMobility: false, partitionFrontier: false);
        Check(reference.UsefulDestination == plan.UsefulDestination,
            "First-pass stopping and pruning agree with the reference interior endpoint.");
        var edgeWalk = NativeMovementIncumbent.CaptureRoute(input, [input.Origin, Point(map, 56)])!;
        Check(NativeMovementHost.MobilityImprovesWalking(edgeWalk, 0, plan, input, input.Identity.CapturedTimestamp)
            && !NativeMovementHost.MobilityImprovesWalking(plan, 0, edgeWalk, input, input.Identity.CapturedTimestamp),
            "A freshly proved interior destination can replace an edge-directed walk, without reverting to its edge endpoint.");
        var centered = Input(origin: 60);
        var held = JointMovementSearch.Search(centered, Exhaustive);
        Check(held.UsefulDestination == centered.Origin && held.Stages.IsEmpty,
            "Adequate arena clearance has no continuing inward gradient or center-seeking walk.");
        var covered = new MovementPlanningContext(input.Identity, map, 0, 0,
            Enumerable.Repeat(true, 121).ToArray(), new float[121], [], [], arenaInterior: interior,
            enemyFootprints: [new(10, default, 50)], movementValue: PlanningMovementValue.Capture(Class.SGE, 0, []));
        Check(covered.PreferredArenaEndpoint(JointMovementSearch.Search(covered, Exhaustive).UsefulDestination),
            "A boss covering the entire arena does not make the current edge pose preferable to usable interior space.");
        var disabled = Input(enabled: false);
        Check(JointMovementSearch.Search(disabled, Exhaustive).Stages.IsEmpty
            && NativePresentationCapture.CaptureArenaInterior(map, hints, false) == null,
            "The saved arena-edge opt-out retains stationary execution and disables the frozen preference.");
        Check(NativePresentationCapture.CaptureArenaInterior(map, new AIHints(), true) == null,
            "The default player-centered navigation window is not an authored arena edge.");
        var assigned = Input(allowed: interior.Cells.Select(c => !c).ToArray());
        var requiredEdge = JointMovementSearch.Search(assigned, Exhaustive);
        Check(requiredEdge.HasVerifiedRoute && !assigned.PreferredArenaEndpoint(requiredEdge.UsefulDestination),
            "An edge-only mechanic assignment retains a usable edge fallback.");
        var explicitEdge = Input(required: baseline.Origin);
        Check(JointMovementSearch.Search(explicitEdge, Exhaustive).UsefulDestination == baseline.Origin,
            "An explicit native edge destination remains authoritative.");
        var healing = Input(healing: new([new(20, baseline.Origin, 0, .2f, [new(100, .4f)])]));
        Check(JointMovementSearch.Search(healing, Exhaustive).UsefulDestination == baseline.Origin,
            "Interior comfort cannot drop an independently required healing recipient.");
        map.PixelPriority[55] = 1;
        var priority = Input();
        Check(JointMovementSearch.Search(priority, Exhaustive).UsefulDestination == priority.Origin,
            "Native assignment priority can require the arena edge even when ordinary interior space exists.");
        map.PixelPriority[55] = 0;
        for (var row = 0; row < 11; ++row) map.PixelMaxG[row * 11 + 1] = -1;
        var disconnected = Input();
        var fallback = JointMovementSearch.Search(disconnected, Exhaustive);
        Check(fallback.HasVerifiedRoute && !disconnected.PreferredArenaEndpoint(fallback.UsefulDestination)
            && !MovementPlanValidation.Validate(plan, disconnected, 0, true, disconnected.Identity.CapturedTimestamp).Allowed,
            "A blocked inward route cannot be crossed merely because its endpoint is comfortably inside.");
        for (var row = 0; row < 11; ++row) map.PixelMaxG[row * 11 + 1] = float.MaxValue;
        for (var i = 0; i < 121; ++i)
            if (interior.Cells[i]) map.PixelMaxG[i] = 0;
        var dangerous = Input();
        var dangerousFallback = JointMovementSearch.Search(dangerous, Exhaustive);
        Check(dangerousFallback.HasVerifiedRoute && !dangerous.PreferredArenaEndpoint(dangerousFallback.UsefulDestination),
            "Hazards covering all interior positions leave safe edge pockets available.");

        var large = Grid(21, 21);
        hints.PathfindMapBounds = new ArenaBoundsCustom([new Circle(default, 10)], [new Circle(default, 3)]);
        var annulus = NativePresentationCapture.CaptureArenaInterior(large, hints, true)!;
        Check(!annulus.Cells[large.GridToIndex(10, 10)] && !annulus.Cells[large.GridToIndex(14, 10)]
            && annulus.Cells[large.GridToIndex(16, 10)] && !annulus.Cells[large.GridToIndex(19, 19)],
            "Interior capture respects real arena holes, their edges and the outer contour.");
        hints.PathfindMapBounds = new ArenaBoundsCustom([new Circle(default, 10)], WorldProjectionLayers:
            [new(new ArenaBoundsSquare(5.5f).Shape, 0), new(new ArenaBoundsSquare(2).Shape, 10)]);
        hints.PathfindMapArenaProjectionLayer = 0;
        var wide = NativePresentationCapture.CaptureArenaInterior(large, hints, true)!;
        hints.PathfindMapArenaProjectionLayer = 1;
        var narrow = NativePresentationCapture.CaptureArenaInterior(large, hints, true)!;
        Check(wide.Cells[large.GridToIndex(10, 10)] && !narrow.Cells[large.GridToIndex(10, 10)],
            "A narrow selected floor cannot borrow the other floor's interior space.");
        hints.PathfindMapArenaProjectionLayer = null;
        Check(NativePresentationCapture.CaptureArenaInterior(large, hints, true) == null,
            "Unknown layered geometry cannot invent an authoritative interior preference.");
        var changed = new MovementPlanningContext(centered.Identity, centered.CopyMap(), 0, 0,
            Enumerable.Repeat(true, 121).ToArray(), new float[121], [], [], arenaInterior: null,
            range: centered.Range, movementValue: centered.MovementValue);
        Check(!centered.SameProblem(changed), "Changing edge preference is a new optimization input.");
    }
}
