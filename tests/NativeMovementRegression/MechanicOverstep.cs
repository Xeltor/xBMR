using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;
using System.Diagnostics;

public static partial class Checks
{
    private static void MechanicOverstep()
    {
        var map = Grid(11, 3);
        for (var row = 0; row < 3; ++row)
            for (var x = 0; x < 4; ++x) map.PixelMaxG[row * 11 + x] = 2;
        var baseline = Context(map, 11, speed: 6);
        var edge = Point(map, 15);
        var preference = new PlanningMechanicClearance(2,
            Enumerable.Range(0, 33).Select(i => Math.Clamp(i % 11 - 3.5f, 0, 2)).ToImmutableArray());
        MovementPlanningContext Input(PlanningAction[]? actions = null, PlanningRange? range = null,
            PlanningHealingCoverage? healing = null, bool explicitDestination = false, PlanningMechanicClearance? clearance = null)
            => new(baseline.Identity with { CapturedTimestamp = Stopwatch.GetTimestamp() }, map, 0, 0,
                Enumerable.Repeat(true, 33).ToArray(), new float[33], actions ?? [], [], range: range,
                requiredDestination: explicitDestination ? edge : null, healingCoverage: healing,
                movementValue: PlanningMovementValue.Capture(Class.SGE, 0, (actions ?? []).ToImmutableArray()),
                mechanicClearance: clearance ?? preference);
        MovementPlan Refine(MovementPlanningContext input, MovementPlan original)
            => NativeMechanicClearance.Refine(input, original, respectFreshness: false);
        var input = Input();
        var minimal = NativeMovementIncumbent.CaptureRoute(input, [input.Origin, edge])!;
        var padded = Refine(input, minimal);
        Check(padded.UsefulDestination == Point(map, 17) && padded.Status == MovementSearchStatus.Bounded
            && padded.Stages[0] == minimal.Stages[0] && padded.Stages[^1].From == edge
            && (padded.UsefulDestination - edge).Length() <= preference.Distance,
            "A healer's proved escape continues into nearby safe space within the configured extra-travel budget.");
        Check(MovementPlanValidation.Validate(padded, input, 0, true, input.Identity.CapturedTimestamp).Allowed
            && padded.RemainingResources == minimal.RemainingResources
            && padded.DepartureDeadlineSeconds <= minimal.DepartureDeadlineSeconds,
            "Mechanic overstep retains original escape deadlines, complete route proof and resources.");
        var constrained = Input(range: new(10, edge, .1f), healing: new([new(20, edge, 0, .2f, [new(100, .1f)])]));
        Check(Refine(constrained, minimal).UsefulDestination == edge,
            "Extra clearance cannot leave useful attack/healing range.");
        var assigned = Input(explicitDestination: true);
        Check(ReferenceEquals(Refine(assigned, minimal), minimal), "An explicit native destination is not overstepped.");
        map.PixelPriority[15] = 1;
        assigned = Input();
        var assignedBase = NativeMovementIncumbent.CaptureRoute(assigned, [assigned.Origin, edge])!;
        Check(Refine(assigned, assignedBase).UsefulDestination == edge, "A higher-priority native assignment wins over clearance.");
        map.PixelPriority[15] = 0;

        PlanningAction Attack(float earliest, bool narrow = false) => Action(map, 6, 0, new ulong[33],
            Enumerable.Range(0, 33).Select(i => i % 11 >= 4 && (!narrow || i % 11 == 4) ? 1f : 0).ToArray(),
            cast: 1, earliest: earliest);
        var waiting = Input([Attack(2)]);
        var waitingBase = JointMovementSearch.Search(waiting, Exhaustive);
        var waitingPadded = Refine(waiting, waitingBase);
        Check(waitingPadded.UsefulDestination.X > waitingBase.UsefulDestination.X
            && waitingPadded.DeliveryTimes.SequenceEqual(waitingBase.DeliveryTimes)
            && MovementPlanValidation.Validate(waitingPadded, waiting, 0, true, waiting.Identity.CapturedTimestamp).Allowed,
            "An observed free action window can absorb the overstep without delaying the selected cast.");
        var immediate = Input([Attack(0)]);
        var immediateBase = JointMovementSearch.Search(immediate, Exhaustive);
        Check(Refine(immediate, immediateBase).UsefulDestination == immediateBase.UsefulDestination,
            "A selected attack that would be delayed keeps its tighter arrival.");
        var narrow = Input([Attack(2, true)]);
        var narrowBase = JointMovementSearch.Search(narrow, Exhaustive);
        Check(Refine(narrow, narrowBase).UsefulDestination == narrowBase.UsefulDestination,
            "Clearance cannot lose current action coverage or its positional value.");

        for (var row = 0; row < 3; ++row) map.PixelMaxG[row * 11 + 5] = .7f;
        var blocked = Input();
        Check(Refine(blocked, minimal).UsefulDestination == edge,
            "A future hazard on the extra leg prevents overstepping even when the endpoint itself is safe.");
        for (var row = 0; row < 3; ++row)
            for (var x = 5; x < 11; ++x) map.PixelMaxG[row * 11 + x] = -1;
        var pocket = Input();
        Check(Refine(pocket, minimal).UsefulDestination == edge, "A narrow safe pocket remains usable without the preferred margin.");
        var safeMap = Grid(11, 3);
        var alreadySafe = new MovementPlanningContext(baseline.Identity, safeMap, 0, 0,
            Enumerable.Repeat(true, 33).ToArray(), new float[33], [], [], mechanicClearance: preference);
        Check(ReferenceEquals(Refine(alreadySafe, minimal), minimal), "Clearance does not initiate a new correction from an already-safe pose.");
        var hints = new AIHints();
        Check(PlanningMechanicClearance.Capture(safeMap, hints, 2) == null, "No authored warning means no fabricated clearance field.");
        hints.ForbiddenZones.Add((new SDCircle(baseline.Origin, 2), DateTime.UtcNow.AddSeconds(5), 99));
        var captured = PlanningMechanicClearance.Capture(safeMap, hints, 2)!;
        Check(captured.Values[11] == 0 && captured.Values[17] == 2
            && PlanningMechanicClearance.Capture(safeMap, hints, 0) == null,
            "Framework capture freezes bounded authored-shape distances and honors zero extra space.");
        var changed = Input(clearance: preference with { Distance = 3 });
        Check(!changed.SameProblem(input), "A changed clearance preference invalidates the pending optimization.");
        for (var row = 0; row < 3; ++row)
            for (var x = 4; x < 11; ++x) map.PixelMaxG[row * 11 + x] = float.MaxValue;
        var groupPreference = preference with { Values = Enumerable.Repeat(2f, 33).ToImmutableArray(),
            PartyRegion = new(20, Point(map, 17), 4) };
        MovementPlanningContext Group(float ready) => new(baseline.Identity, map, 0, 0,
            Enumerable.Repeat(true, 33).ToArray(), new float[33], [], [], mechanicClearance: groupPreference,
            movementValue: PlanningMovementValue.Capture(Class.SGE, ready, []));
        var freeWindow = Group(2);
        var groupBase = NativeMovementIncumbent.CaptureRoute(freeWindow, [freeWindow.Origin, edge])!;
        var groupArrival = Refine(freeWindow, groupBase);
        Check(groupArrival.UsefulDestination == Point(map, 17) && groupArrival.DurationSeconds <= 2
            && groupArrival.Evidence.Contains("healing posture"),
            "A required mechanic escape uses its observed free GCD window to arrive nearer useful party healing coverage.");
        Check(Refine(Group(0), groupBase).UsefulDestination == edge,
            "Preparatory party posture alone cannot extend a tight required movement window.");
        var stableSoft = new MovementPlanningContext(freeWindow.Identity, map, 0, 0,
            Enumerable.Repeat(true, 33).ToArray(), new float[33], [], [],
            mechanicClearance: groupPreference with { PartyRegion = new(20, Point(map, 18), 4) },
            movementValue: PlanningMovementValue.Capture(Class.SGE, 2, []));
        Check(stableSoft.SameProblem(freeWindow), "A moving party's optional escape posture does not cancel a valid worker every frame.");
    }
}
