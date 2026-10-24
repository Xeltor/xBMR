using BossMod;
using BossMod.Components;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void ForcedMovementPlanning()
    {
        var map = Grid(6, 1);
        map.PixelPriority[5] = 10;
        var identity = Context(map, 0).Identity;
        var origin = Point(map, 0);
        var heading = 90f.Degrees();
        var route = new PlanningForcedMovement("authored march", heading, true,
            [new(origin, origin, 0, 0.5f), new(origin, Point(map, 2), 0.5f, 1.5f)]);
        MovementPlanningContext Capture(PlanningForcedMovement[] routes, bool sampled = true, WPos? from = null)
            => new(identity with { Origin = from ?? origin }, map, 0, 0, Enumerable.Repeat(true, 6).ToArray(),
                new float[6], [], [], forcedMovement: new([.. routes], sampled));
        var context = Capture([route]);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.UsefulDestination == Point(map, 5)
            && plan.Stages[0].Kind == MovementStageKind.ForcedMovement,
            "mandatory march and subsequent useful destination form one verified plan");
        Check(plan.Status == MovementSearchStatus.Bounded && plan.Bound == MovementSearchBound.HeadingSamples,
            "sampled headings never claim an exhaustive continuous search");
        Check(plan.ImmediateWaypoint == null && MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
            "the route is validated without turning forced movement into a walk directive");
        var executor = new MovementPlanExecutor();
        var execution = executor.Advance(plan, context, new(default), true, identity.CapturedTimestamp);
        Check(execution.Kind == MovementExecutionKind.Face && execution.ForcedMovementFacing && execution.Facing == heading,
            "exact verified forced-march heading reaches the shared executor");
        Check(executor.Advance(plan, context, new(heading), true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "observed preparation leaves physical displacement to the mechanic");
        Check(executor.Advance(plan, context, new(heading), false, identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "manual authority cancels compulsory route preparation");
        var changed = route with { Legs = [new(origin, Point(map, 1), 0.5f, 1.5f)] };
        Check(!MovementPlanValidation.Validate(plan, Capture([changed]), 0, true, identity.CapturedTimestamp).Allowed,
            "changed authored landing cannot retain an old heading");
        Check(!JointMovementSearch.Search(Capture([]), Exhaustive).HasVerifiedRoute,
            "missing compulsory projection cannot silently become ordinary walking");
        var ordinary = JointMovementSearch.Search(Context(map, 0), Exhaustive);
        Check(!MovementPlanValidation.Validate(ordinary, context, 0, true, identity.CapturedTimestamp).Allowed,
            "a new march invalidates an otherwise valid prior walking plan");
        map.PixelMaxG[1] = -1;
        Check(!JointMovementSearch.Search(Capture([route]), Exhaustive).HasVerifiedRoute,
            "forced route checks intervening physical cells");
        map.PixelMaxG[1] = 0.7f;
        Check(!JointMovementSearch.Search(Capture([route]), Exhaustive).HasVerifiedRoute,
            "a successive warning must remain clear until the forced leg exits it");
        map.PixelMaxG[1] = float.MaxValue;
        map.PixelMaxG[2] = 2.5f;
        Check(JointMovementSearch.Search(Capture([route]), Exhaustive).HasVerifiedRoute,
            "temporary forced landing includes a verified onward escape");
        map.PixelMaxG[3] = -1;
        map.PixelMaxG[0] = map.PixelMaxG[1] = 2;
        Check(!JointMovementSearch.Search(Capture([route]), Exhaustive).HasVerifiedRoute,
            "temporary forced landing without onward escape is rejected");
        Array.Fill(map.PixelMaxG, float.MaxValue);
        var active = route with { CanTurn = false, Active = true, Legs = [new(origin, Point(map, 2), 0, 1)] };
        var activeContext = Capture([active], false);
        var activePlan = JointMovementSearch.Search(activeContext, Exhaustive);
        Check(activePlan.Status == MovementSearchStatus.Complete
            && executor.Advance(activePlan, activeContext, new(default), true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "an already active march has one physical trajectory and cannot be steered");

        // Independent enumeration: every solution consists of one compulsory
        // prefix and the straight unobstructed remainder on this one-row map.
        for (var landing = 1; landing < 5; ++landing)
            for (var alternateLanding = 1; alternateLanding < 5; ++alternateLanding)
            {
                var first = route with { Legs = [new(origin, Point(map, landing), 0.5f, 1.5f)] };
                var second = route with { Heading = default, Legs = [new(origin, Point(map, alternateLanding), 0.5f, 2)] };
                var result = JointMovementSearch.Search(Capture([first, second]), Exhaustive);
                var expected = Math.Min(1.5f + (Point(map, 5) - first.Landing).Length() / identity.Speed,
                    2 + (Point(map, 5) - second.Landing).Length() / identity.Speed);
                Check(result.UsefulDestination == Point(map, 5) && Math.Abs(result.DurationSeconds - expected) < 0.001f,
                    "joint compulsory route matches exhaustive prefix and remainder enumeration");
            }
        var gazeContext = new MovementPlanningContext(identity, map, 0, 0, Enumerable.Repeat(true, 6).ToArray(),
            new float[6], [], [], forcedMovement: new([route], true),
            executionRules: new(float.MaxValue, float.MaxValue, [new(heading, 10f.Degrees(), 0.8f)]));
        Check(!JointMovementSearch.Search(gazeContext, Exhaustive).HasVerifiedRoute,
            "authored facing constraints remain binding during the forced leg");
        var (world, actor, module) = ProjectionFixture();
        actor.PosRot = default;
        var authored = new FacingMarch(module);
        module.Components.Add(authored);
        authored.AddForcedMovement(actor, 90f.Degrees(), 0.5f, world.FutureTime(8));
        Check(!NativeForcedMovementCapture.NeedsExclusivePlanning(world, module, actor),
            "a distant march countdown does not take away the existing early attack window");
        authored.State[actor.InstanceID].PendingMoves.Clear();
        authored.AddForcedMovement(actor, default, 0.5f, world.FutureTime(-0.2));
        Check(NativeForcedMovementCapture.NeedsExclusivePlanning(world, module, actor),
            "the existing native status-transition margin retains compulsory authority");
        authored.State[actor.InstanceID].PendingMoves.Clear();
        authored.AddForcedMovement(actor, default, 0.5f, world.FutureTime(-0.5));
        Check(!NativeForcedMovementCapture.NeedsExclusivePlanning(world, module, actor),
            "an expired pending movement does not acquire an indefinite compulsory forecast gate");
        authored.State[actor.InstanceID].PendingMoves.Clear();
        authored.AddForcedMovement(actor, 90f.Degrees(), 0.5f, world.FutureTime(0.6));
        Check(NativeForcedMovementCapture.NeedsExclusivePlanning(world, module, actor),
            "imminent native compulsory movement remains authoritative independently of correction preferences");
        var uncorrected = NativeForcedMovementCapture.Capture(world, module, actor, false)!;
        Check(!uncorrected.SampledHeadings && uncorrected.Routes.Length == 1
            && uncorrected.Routes.All(route => !route.CanTurn && route.Heading == actor.Rotation),
            "disabling heading correction preserves the exact observed heading without permitting ordinary walking forecasts");
        var captured = NativeForcedMovementCapture.Capture(world, module, actor, true)!;
        Check(captured.SampledHeadings && captured.Routes.Length > 0
            && captured.Routes.All(r => r.Landing.X <= 4) && actor.Rotation.Rad == 0,
            "native capture uses the component's facing-aware destination check without changing the live actor");
        var frozenEnd = captured.Routes[0].End;
        authored.State[actor.InstanceID].PendingMoves.Clear();
        Check(captured.Routes[0].End == frozenEnd, "worker projection remains immutable after the native component changes");
        authored.ActivateForcedMovement(actor, world.FutureTime(0.5));
        Check(NativeForcedMovementCapture.NeedsExclusivePlanning(world, module, actor),
            "active forced movement retains native authority with no pending countdown");
        var currentMarch = NativeForcedMovementCapture.Capture(world, module, actor, true)!;
        Check(!currentMarch.SampledHeadings && currentMarch.Routes.Length == 1
            && currentMarch.Routes[0].Active && !currentMarch.Routes[0].CanTurn,
            "active native march captures only the already committed physical heading");
        module.Components.Add(new FacingMarch(module));
        ((FacingMarch)module.Components[^1]).AddForcedMovement(actor, default, 1, world.FutureTime(0.5));
        Check(NativeForcedMovementCapture.Capture(world, module, actor, true) is { Routes.IsEmpty: true },
            "simultaneous native projection owners require authored composition");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        module.Components.RemoveAt(module.Components.Count - 1);
        authored.DeactivateForcedMovement(actor);
        authored.AddForcedMovement(actor, default, 0.5f, world.FutureTime(0.6));
        for (var i = 0; i < 100; ++i) NativeForcedMovementCapture.Capture(world, module, actor, true);
        Console.WriteLine($"Synthetic native forced-march capture: {watch.Elapsed.TotalMilliseconds / 100:f3}ms average for {NativeForcedMovementCapture.HeadingSamples} heading samples; no live frame-time claim.");
        var writes = 0;
        var pause = new ForcedMarchRotationOwner(() => (RsrMode.Off, typeof(Checks)), _ => { ++writes; return true; });
        pause.Tick(true, 1);
        Check(pause.Paused && !pause.Owned && writes == 0, "already disabled RSR needs no mode mutation or restore lease");
        var unconfirmed = new ForcedMarchRotationOwner(() => (RsrMode.Auto, typeof(Checks)), _ => true);
        unconfirmed.Tick(true, 1);
        Check(!unconfirmed.Paused && unconfirmed.Owned,
            "successful transport return without Off readback cannot authorize heading preparation");
        pause.Tick(false, 0);
        Check(!pause.Paused && writes == 0, "already disabled RSR stays disabled after the mechanic");
    }
}
