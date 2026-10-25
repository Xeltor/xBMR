using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;

public static partial class Checks
{
    private static void SagePackApproach()
    {
        var map = Grid(30, 2);
        var initial = Context(map, 0, speed: 6);
        var player = new ExecutionActor(1, initial.Origin, 0, .5f, default);
        ExecutionActor[] pack = [new(10, new(8, -.5f), 0, .5f, default), new(11, new(9, .5f), 0, .5f, default), new(12, new(10, -.5f), 0, .5f, default)];
        var range = NativeRadialCoverageRegion.Capture(player, 10, 5, 3, pack, [], 1)!;
        Check(range != null && range.NeedsRecovery(player.Position) && range.Radius < 5.5f,
            "A ranged Sage receives a real three-recipient Dyskrasia entry region rather than the ordinary 25-yalm goal.");
        var filler = Action(map, 12, 1, Enumerable.Repeat(1ul, 60).ToArray(), Enumerable.Repeat(1f, 60).ToArray(), cast: 1.5f)
            with { Id = (uint)BossMod.SGE.AID.DosisII };
        Check(NativeSagePackApproach.ForPlanning(Class.SGE, filler, range, player.Position) == null
            && NativeSagePackApproach.ForPlanning(Class.SGE, filler with { CastSeconds = 0 }, range, player.Position) != null
            && NativeSagePackApproach.ForPlanning(Class.SGE, filler with { Id = (uint)BossMod.SGE.AID.Diagnosis }, range, player.Position) != null
            && NativeSagePackApproach.ForPlanning(Class.SGE, filler, null, player.Position) != null,
            "Unstarted stationary Dosis yields during real pack recovery, while instants, heals and ordinary ranged play remain intact.");
        for (var i = 0; i < 32; ++i)
        {
            var point = range!.Center + (i * MathF.Tau / 32).Radians().ToDirection() * (range.Radius + MovementPlanValidation.ArrivalTolerance);
            Check(pack.Count(a => (point - a.Position).LengthSq() <= (5 + a.Radius) * (5 + a.Radius) + .0001f) >= 3,
                "Every boundary arrival, including native arrival tolerance, covers RSR's configured minimum.");
        }
        Check(NativeRadialCoverageRegion.Capture(player, 10, 5, 4, pack, [], 0) == null
            && NativeRadialCoverageRegion.Capture(player, 10, 5, 0, pack, [], 0) == null,
            "Missing or unmet RSR AoE counts cannot invent a pack opportunity.");
        Check(NativeRadialCoverageRegion.Capture(player, 99, 5, 3, pack, [], 0) == null,
            "A missing or excluded current target cannot send the healer toward another pack.");
        Check(NativeRadialCoverageRegion.Capture(player, 10, 5, 3, pack.Select(a => a with { Height = 30 }).ToArray(), [], 0) == null,
            "An enemy on another floor cannot count toward local Dyskrasia reach.");
        Check(NativeRadialCoverageRegion.Capture(player, 10, 5, 3, pack, [pack[1] with { Id = 99 }], 0) == null,
            "Nearby excluded or idle collateral prevents a supposedly useful AoE landing.");
        var sparse = pack.Select((a, i) => a with { Position = new(i * 20, 0) }).ToArray();
        Check(NativeRadialCoverageRegion.Capture(player, 10, 5, 3, sparse, [], 0) == null,
            "Separate enemies do not count as a compact trash pack.");
        var action = Action(map, 12, 1, Enumerable.Repeat(1ul, 60).ToArray(), Enumerable.Repeat(1f, 60).ToArray(), cast: 1.5f);
        MovementPlanningContext Input(PlanningRange? objective, PlanningAction[]? actions = null, bool[]? allowed = null)
            => new(initial.Identity, map, 0, 0, allowed ?? Enumerable.Repeat(true, 60).ToArray(), new float[60], actions ?? [], [], range: objective);
        var plan = JointMovementSearch.Search(Input(range, [action]), Exhaustive);
        Check(plan.HasVerifiedRoute && plan.DeliveryTimes[0] == 1.5f && range!.Loss(plan.UsefulDestination) <= MovementPlanValidation.ArrivalTolerance
            && plan.Stages.Any(s => s.Kind == MovementStageKind.Walk),
            "The selected ranged cast still lands on time, followed by a safe walk into Dyskrasia reach in the same plan.");
        Check(MovementPlanValidation.Validate(plan, Input(range, [action]), 0, true, initial.Identity.CapturedTimestamp).Allowed,
            "Sage pack approach uses ordinary complete-route execution validation.");
        var reference = JointMovementSearch.Search(Input(range, [action]), Exhaustive, pruneExecutionInferiorStates: false);
        Check(range!.Loss(reference.UsefulDestination) <= MovementPlanValidation.ArrivalTolerance,
            "Execution pruning and the exhaustive reference both restore pack access.");
        for (var i = 0; i < 60; ++i)
            if (Point(map, i).X >= range.Center.X - range.Radius - map.Resolution) map.PixelMaxG[i] = 0;
        var escape = JointMovementSearch.Search(Input(range), Exhaustive);
        Check(escape.HasVerifiedRoute && range.Loss(escape.UsefulDestination) > MovementPlanValidation.ArrivalTolerance,
            "An imminently hazardous pack region cannot override a complete safe escape elsewhere.");
        var denied = JointMovementSearch.Search(Input(range, allowed: Enumerable.Repeat(false, 60).ToArray()), Exhaustive);
        Check(!denied.HasVerifiedRoute, "Pack approach cannot bypass independent destination assignments.");
        Check(NativeMovementHost.ResolveRange(new(Range: new(10, default, 25)), new MovementCombatIntent([], 0, true, RangeRecovery: range), true) is { Radius: 25 },
            "An explicit native range instruction retains its radius instead of silently becoming a pack order.");
    }
}
