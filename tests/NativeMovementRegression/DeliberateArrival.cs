using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void DeliberateArrival()
    {
        // A native witness goes to the center, but equally timely clear arrivals
        // already exist. The first published walking pass must compare them.
        var map = Grid(9, 1);
        var identity = Context(map, 0, speed: 6).Identity;
        var target = Point(map, 6);
        var presentation = Enumerable.Range(0, 9).Select(i => i is >= 4 and <= 7 ? -1f : 0).ToArray();
        for (var i = 2; i < 9; ++i) map.PixelPriority[i] = 1;
        MovementPlanningContext Input(PlanningAction[] actions, float ready = 2)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), presentation, actions, [],
                targetId: 10, range: new(10, target, 4, true, ready));
        var context = Input([]);
        var centerSeed = NativeMovementIncumbent.CaptureRoute(context, [context.Origin, target]);
        var first = JointMovementSearch.Search(context, Exhaustive, nativeIncumbent: centerSeed, stopAtCurrentOpportunity: true);
        Check(first.UsefulDestination == Point(map, 2) && first.RangeAccess is { Loss: 0, At: 2 },
            "First range-recovery publication selects clear near-side space instead of the native boss-center seed.");
        Check(first.Stages.Where(s => s.Kind == MovementStageKind.Walk).All(s => s.To.X <= Point(map, 2).X),
            "The initial route never walks under the boss before correcting back to the useful endpoint.");
        var fallbackDecision = new BossMod.Pathfinding.NavigationDecision { Destination = target, LeewaySeconds = float.MaxValue };
        fallbackDecision.Publication = new(identity, null, [context.Origin, target], true, false,
            BossMod.Pathfinding.ThetaStar.Score.SafeMaxPrio, fallbackDecision, false);
        var fallback = centerSeed! with { Status = MovementSearchStatus.NativeRecovery, NativeRecovery = fallbackDecision };
        Check(!NativeMovementHost.CanRecoverCombatRange(context, fallback),
            "Elective native fallback cannot start a worse center arrival before the worker compares useful destinations.");

        var positional = Action(map, 4, 0, new ulong[9], [0, 0, 0, 0, 0, 1, 1, 1, 1], earliest: 2);
        context = Input([positional]);
        centerSeed = NativeMovementIncumbent.CaptureRoute(context, [context.Origin, target]);
        var positioned = JointMovementSearch.Search(context, Exhaustive, nativeIncumbent: centerSeed, stopAtCurrentOpportunity: true);
        Check(positioned.UsefulDestination == Point(map, 8) && positioned.ExecutionValues[0] == 1 && positioned.DeliveryTimes[0] == 2,
            "The first positional arrival clears the center while preserving the same attack window.");
        Check(MovementPlanValidation.Validate(positioned, context, 0, true, identity.CapturedTimestamp).Allowed,
            "The selected arrival retains exact native route and action validation.");

        // Comfort must yield when the only executable/safe position is inside.
        map.PixelMaxG[8] = -1;
        context = Input([positional]);
        var confined = JointMovementSearch.Search(context, Exhaustive, stopAtCurrentOpportunity: true);
        Check(confined.HasVerifiedRoute && confined.ExecutionValues[0] == 1 && confined.DeliveryTimes[0] == 2
            && confined.DestinationPresentation < 0,
            "A blocked clear endpoint never excludes the only safe positional pocket under the boss.");
        map.PixelMaxG[8] = float.MaxValue;
        context = Input([positional with { Earliest = 0 }], 0);
        var urgent = JointMovementSearch.Search(context, Exhaustive, stopAtCurrentOpportunity: true);
        Check(urgent.DeliveryTimes[0] < 1 && urgent.UsefulDestination != Point(map, 8),
            "Center clearance cannot delay an immediately available positional attack.");

        Check(NativePresentationCapture.EnemyCenterRadius(20) == 4.35f
            && NativePresentationCapture.EnemyCenterRadius(4) == 4.35f
            && NativePresentationCapture.EnemyCenterRadius(2) == 2.35f
            && NativePresentationCapture.EnemyCenterRadius(0) == 0,
            "Large bosses permit close interior positions; ordinary bosses retain hitbox clearance and absent radii create no preference.");
        PositionalArrivals();
    }

    private static void PositionalArrivals()
    {
        var map = Grid(11, 11);
        var boss = Point(map, map.GridToIndex(5, 5));
        foreach (var positional in new[] { Positional.Any, Positional.Rear, Positional.Flank })
        {
            var identity = Context(map, map.GridToIndex(8, 9), speed: 6).Identity;
            var opportunity = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, boss, 8, 0,
                [new(10, boss, 5, 1, true)], positional: positional);
            var action = new PlanningAction(1, 10, 2, 4, 0, 0, 0, [], [], Opportunity: opportunity);
            var presentation = Enumerable.Range(0, 121).Select(cell =>
                -Math.Max(0, 4.35f - (Point(map, cell) - boss).Length()) / 4.35f).ToArray();
            var context = new MovementPlanningContext(identity, map, 0, 0,
                Enumerable.Repeat(true, 121).ToArray(), presentation, [action], [],
                targetId: 10, range: new(10, boss, 8, true, 2));
            var seed = NativeMovementIncumbent.CaptureRoute(context, [context.Origin, boss]);
            var plan = JointMovementSearch.Search(context, Exhaustive, nativeIncumbent: seed, stopAtCurrentOpportunity: true);
            Check(plan.HasVerifiedRoute && plan.DeliveryTimes[0] == 2 && plan.DestinationPresentation == 0
                && opportunity.Evaluate(plan.UsefulDestination).Value == 1,
                $"First {positional} arrival uses clear space in the actual selected positional with no attack delay.");
            Check(MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
                $"First {positional} arrival and all preceding stages retain their independent safety and action proof.");
        }
    }
}
