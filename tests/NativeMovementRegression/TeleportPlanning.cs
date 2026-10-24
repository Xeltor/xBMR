using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void TeleportPlanning()
    {
        var map = Grid(7, 1);
        map.PixelMaxG[2] = map.PixelMaxG[3] = map.PixelMaxG[4] = -1;
        map.PixelPriority[6] = 1;
        var identity = Context(map, 0).Identity;
        var teleport = new PlanningTeleport(Point(map, 1), Point(map, 5), 0.3f, 0.2f, 0);
        var action = new PlanningAction(1, 10, 0, 3, 0, 0, 0, new ulong[7], [0, 0, 0, 0, 0, 0, 1]);
        MovementPlanningContext Capture(PlanningTeleport[]? edges = null, WPos? origin = null)
            => new(identity with { Origin = origin ?? identity.Origin }, map, 0, 0, Enumerable.Repeat(true, 7).ToArray(),
                new float[7], [action], [], teleports: edges ?? [teleport]);
        var context = Capture();
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.DeliveredActions == 1 && plan.UsefulDestination == Point(map, 6),
            "authored teleport reaches the first useful combat destination across disconnected terrain");
        Check(plan.Stages.Any(s => s.Kind == MovementStageKind.Teleport) && plan.ImmediateWaypoint == teleport.Entrance,
            "the first executable waypoint is the entrance, never the landing across the gap");
        Check(MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
            "exact validation checks approach and landing without walking through the teleport gap");
        var executor = new MovementPlanExecutor();
        Check(executor.Advance(plan, context, default, true, identity.CapturedTimestamp).Waypoint == teleport.Entrance,
            "the shared executor walks to the native trigger");
        Check(executor.Advance(plan, Capture(origin: teleport.Entrance), default, true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "standing on the trigger waits for native transport instead of walking across the gap");
        Check(executor.Advance(plan, Capture(origin: teleport.Exit), default, true, identity.CapturedTimestamp).Waypoint == Point(map, 6),
            "observed landing advances the same useful route");
        Check(!MovementPlanValidation.Validate(plan, Capture([teleport with { Exit = Point(map, 4) }]), 0, true, identity.CapturedTimestamp).Allowed,
            "changed teleporter destination immediately invalidates the planned transition");
        Check(!MovementPlanValidation.Validate(plan, Capture([]), 0, true, identity.CapturedTimestamp).Allowed,
            "a disappeared native transition cannot be reused");
        map.PixelMaxG[5] = -1;
        Check(JointMovementSearch.Search(Capture(), Exhaustive).DeliveredActions == 0,
            "blocked landing rejects the transition locally");
        map.PixelMaxG[5] = float.MaxValue;
        map.PixelMaxG[1] = 2;
        Check(JointMovementSearch.Search(Capture([teleport with { NotBefore = 3 }]), Exhaustive).DeliveredActions == 0,
            "waiting for an entrance cannot cross its warning deadline");
        map.PixelMaxG[1] = float.MaxValue;
        action = action with { Latest = 6 };
        var delayed = Capture([teleport with { NotBefore = 3 }]);
        var delayedPlan = JointMovementSearch.Search(delayed, Exhaustive);
        Check(delayedPlan.DeliveredActions == 1 && delayedPlan.Stages.First(s => s.Kind == MovementStageKind.Teleport).EndSeconds >= 3.2f,
            "authored not-before timing participates in the same execution window");
        var second = new PlanningTeleport(Point(map, 5), Point(map, 1), 0.3f, 0.2f, 0);
        Check(JointMovementSearch.Search(Capture([teleport, second]), Exhaustive).DeliveredActions == 1,
            "a bidirectional native landing can leave its pad without an accidental forced return");
        Check(!context.TraverseOrdinary(identity.Origin, teleport.Entrance, 0, 1, out _)
            && !context.CanRemain(teleport.Entrance) && !context.CanChooseStationaryAction(teleport.Entrance),
            "trigger entry, action delivery and stopping there cannot masquerade as ordinary movement");

        // Independent shortest-arrival enumeration for two disconnected islands.
        // Every legal solution uses exactly one authored crossing; compare all
        // available exits rather than the production search's labels or queue.
        for (var gate = 0; gate < 4; ++gate)
            for (var duration = 0; duration < 4; ++duration)
            {
                var alternate = teleport with { Exit = Point(map, 6), UseTime = duration * 0.25f, NotBefore = gate * 0.5f };
                var input = Capture([teleport, alternate with { Entrance = Point(map, 0), Radius = 0.2f }]);
                // The second entrance is occupied initially; it is a separate
                // native route, and overlapping ambiguous entrances are rejected.
                var result = JointMovementSearch.Search(input, Exhaustive);
                var expected = Math.Max(0, alternate.NotBefore) + alternate.UseTime;
                Check(result.DeliveredActions == 1 && Math.Abs(result.DeliveryTimes[0] - expected) < 0.001f,
                    "teleport arrival agrees with an independent direct-crossing reference");
            }
        var ambiguous = Capture([teleport, teleport with { Exit = Point(map, 6) }]);
        Check(JointMovementSearch.Search(ambiguous, Exhaustive).DeliveredActions == 0,
            "overlapping triggers with different outcomes do not invent a selectable exit");

        var progress = new NativeTeleportProgress();
        var longTransit = teleport with { UseTime = 1 };
        var onPad = identity with { Origin = teleport.Entrance };
        var initial = progress.Observe(onPad, [longTransit]).Single();
        Check(initial.ElapsedUse == 0, "An entrance observation starts only at the observed pad, without backdating use.");
        var firstDuration = 0f;
        for (var frame = 0; frame < 6; ++frame)
        {
            var age = frame * 0.2f;
            var frameIdentity = onPad with { WorldTime = onPad.WorldTime.AddSeconds(age) };
            var observed = progress.Observe(frameIdentity, [longTransit]);
            var input = new MovementPlanningContext(frameIdentity, map, 0, 0, Enumerable.Repeat(true, 7).ToArray(),
                new float[7], [action], [], teleports: observed);
            var continuation = JointMovementSearch.Search(input, Exhaustive);
            if (frame == 0) firstDuration = continuation.DurationSeconds;
            Check(continuation.HasVerifiedRoute && Math.Abs(continuation.DurationSeconds + age - firstDuration) < 0.001f,
                "Repeated fresh planning observations do not restart an authored use time longer than freshness.");
            if (frame > 0)
                Check(observed.Single().Same(initial, age), "Equivalent entrance progress ages coherently with its original schedule.");
        }
        var offPad = onPad with { Origin = identity.Origin, WorldTime = onPad.WorldTime.AddSeconds(1.1) };
        Check(progress.Observe(offPad, [longTransit]).Single().ElapsedUse == null,
            "Leaving an entrance retires its timing observation.");
        Check(progress.Observe(onPad with { WorldTime = offPad.WorldTime.AddSeconds(0.1) }, [longTransit]).Single().ElapsedUse == 0,
            "Returning to an entrance starts a new observed use interval.");
        progress.Reset();
        Check(progress.Observe(onPad, [longTransit]).Single().ElapsedUse == 0,
            "Manual ownership release clears pending transport progress.");
        var held = progress.Observe(onPad, [longTransit with { NotBefore = 0.5f }]).Single();
        Check(held.ElapsedUse == 0 && progress.Observe(onPad with { WorldTime = onPad.WorldTime.AddSeconds(0.2) },
            [longTransit with { NotBefore = 0.3f }]).Single().ElapsedUse == 0,
            "A native not-before gate is not counted as time already using the transport.");
        var afterGate = progress.Observe(onPad with { WorldTime = onPad.WorldTime.AddSeconds(0.7) }, [longTransit]).Single();
        Check(Math.Abs(afterGate.ElapsedUse!.Value - 0.2f) < 0.001f,
            "Only observed time after the authored gate reduces remaining transport duration.");
    }
}
