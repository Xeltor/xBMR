using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Collections.Immutable;
using System.Threading;

public static partial class Checks
{
    private static PlanningAction TypedAction(ExecutionOpportunity opportunity, float latest = 5)
        => new(1, opportunity.TargetId, 0, latest, 0, 0, 0, [], [], Opportunity: opportunity);

    private static void ActionOpportunities()
    {
        var map = Grid(3, 1);
        var center = Point(map, 1);
        var invalidLanding = center + new WDir(-0.4f, 0);
        var validLanding = center + new WDir(0.4f, 0);
        var target = new ExecutionRecipient(10, center + new WDir(0.5f, 0), 0, 1, true);
        var opportunity = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, target.Position, 0.6f, 0, [target]);
        Check(opportunity.Evaluate(Point(map, 1)).Allowed && !opportunity.Evaluate(invalidLanding).Allowed,
            "A cell-center attack does not prove range at an exact landing in the same cell.");
        PlanningMobility[] mobility = [new(0, 1, MovementStageKind.TargetDash, 13, 10, null, 0, 0.1f, 0, 0, LandingPosition: invalidLanding)];
        var context = Context(map, 0, [TypedAction(opportunity, 0.2f)], mobility, 1);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.DeliveredActions == 0, "An out-of-range landing cannot receive the cell center's action value.");

        target = target with { Position = center + new WDir(1, 0) };
        opportunity = new(ExecutionShape.SingleTarget, 10, target.Position, 0.65f, 0, [target]);
        Check(!opportunity.Evaluate(Point(map, 1)).Allowed && opportunity.Evaluate(validLanding).Allowed,
            "An exact useful landing remains available when its cell center cannot hit.");
        mobility = [mobility[0] with { LandingPosition = validLanding }];
        context = Context(map, 0, [TypedAction(opportunity, 0.2f)], mobility, 1);
        plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.Stages.Single(s => s.Kind == MovementStageKind.Cast).To == validLanding,
            "Search delivers the action from its executable position.");
        Check(MovementPlanValidation.Validate(plan, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Final validation uses the same exact range and recipient evidence.");

        var many = Enumerable.Range(1, 96).Select(i => new ExecutionRecipient((ulong)i, default, 0, 1, true)).ToArray();
        var circle = new ExecutionOpportunity(ExecutionShape.CasterCircle, 0, default, 0, 2, many);
        many[0] = many[0] with { Position = new(100, 100) };
        Check(circle.Evaluate(default).Value == 96, "Native recipient capture is immutable and has no 64-recipient transport mask limit.");
        context = Context(Grid(1, 1), 0, [TypedAction(circle)]);
        plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.ActionEvidence.Single().RequiredRecipientIds.Length == 96 && plan.ExecutionValues[0] == 96,
            "Every required recipient stays individually identifiable in the native plan.");
        Check(MovementPlanValidation.Validate(plan, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Exact final validation retains more than 64 recipients without truncation.");

        ExecutionRecipient[] party = [new(10, new(-2, 0), 0, 1, true), new(11, new(2, 0), 0, 1000), new(12, new(2, 0), 0, 1000)];
        circle = new(ExecutionShape.CasterCircle, 0, default, 0, 2, party);
        Check(circle.Evaluate(default).Allowed && !circle.Evaluate(new(1, 0)).Allowed,
            "More healing value cannot conceal loss of a required recipient.");
        var single = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, default, 5, 0,
            [new(10, default, 0, 1), new(11, default, 0, 500)]);
        Check(single.Evaluate(default).Value == 1 && !single.Covers(default, 11),
            "An overlapping ally is not a recipient of a single-target heal on someone else.");

        var ally = new ExecutionOpportunity(ExecutionShape.TargetCircle, 10, new(8, 0), 5, 2,
            [new(10, new(8, 0), 0, 1, true), new(11, new(9, 0), 0, 1, true)]);
        Check(!ally.Evaluate(default).Allowed && ally.Evaluate(new(4, 0)).Value == 2,
            "Ally-centered healing preserves the selected anchor and actual cast range.");
        var ground = new ExecutionOpportunity(ExecutionShape.GroundCircle, 0, new(8, 0), 5, 2,
            [new(10, new(8, 0), 0, 1, true)]);
        Check(!ground.Evaluate(default).Allowed && ground.Evaluate(new(4, 0)).Allowed,
            "Ground healing does not relocate the rotation's selected placement.");
        var vertical = new ExecutionOpportunity(ExecutionShape.CasterCircle, 0, default, 0, 2,
            [new(10, default, 0, 1, true, HeightOffset: 3)]);
        Check(!vertical.Evaluate(default).Allowed, "Radial healing cannot credit a recipient outside its vertical reach.");

        var rear = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, default, 3, 0,
            [new(10, default, 0, 1, true)], positional: Positional.Rear, targetFacing: default);
        Check(rear.Evaluate(new(0, -2)).Allowed && !rear.Evaluate(new(0, 2)).Allowed,
            "The current action's rear requirement is separate from attack range.");
        var turned = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, default, 3, 0,
            [new(10, default, 0, 1, true)], targetFacing: 90.Degrees());
        var any = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, default, 3, 0,
            [new(10, default, 0, 1, true)]);
        Check(any.SameGeometry(turned) && !rear.SameGeometry(new(ExecutionShape.SingleTarget, 10, default, 3, 0,
            [new(10, default, 0, 1, true)], positional: Positional.Rear, targetFacing: 90.Degrees())),
            "A target turning cannot cancel an Any-positional opportunity; actual positional geometry still changes.");
        var tank = new ExecutionOpportunity(ExecutionShape.SingleTarget, 10, default, 3, 0,
            [new(10, default, 0, 1, true)], tankFacing: new(default, 20.Degrees()));
        Check(tank.Evaluate(new(0, 2)).Allowed && !tank.Evaluate(new(0, -2)).Allowed,
            "An authored tank-facing requirement cannot be exchanged for other combat value.");

        var enemies = new[] { new ExecutionRecipient(10, new(0, 2), 0.25f, 1, true), new ExecutionRecipient(11, new(0.5f, 3), 0.25f, 1) };
        var cone = new ExecutionOpportunity(ExecutionShape.Cone, 10, enemies[0].Position, 3, 5, enemies, halfAngle: 45.Degrees());
        Check(cone.Evaluate(default) is { Allowed: true, Value: 2, Facing: not null },
            "AoE cone preserves hits and publishes the facing used by its execution proof.");
        var line = new ExecutionOpportunity(ExecutionShape.Rectangle, 10, enemies[0].Position, 3, 5, enemies, halfWidth: 1);
        Check(line.Evaluate(default).Value == 2 && !line.Evaluate(new(4, 0)).Allowed,
            "Line attacks use native circle/rectangle intersections and actual cast range.");

        var nativeIdentity = context.Identity;
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(2, mapResolution: 1), default, null, default, false);
        var native = NativeNavigationSnapshot.Capture(new(source, [], [], new([]), true, [], nativeIdentity, false));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            native.Recover(new(), cancelled.Token);
            Check(false, "Superseded native recovery must stop before searching.");
        }
        catch (OperationCanceledException) { Check(true, "Native recovery honors shared worker cancellation."); }
        var nativeMap = native.CopyStrictMap();
        var nativeContext = new MovementPlanningContext(nativeIdentity, nativeMap, 0, 0,
            Enumerable.Repeat(true, native.Cells).ToArray(), new float[native.Cells], [], [], native: native);
        var publications = new List<MovementPlan>();
        var finished = NativeMovementSearch.Run(nativeContext, default, publications.Add);
        Check(publications.First().Status == MovementSearchStatus.NativeRecovery && publications.First().OptimizationPending,
            "Mandatory native recovery is published before additional combat optimization.");
        Check(!finished.OptimizationPending, "A finished search never delays recovery as though optimization were still pending.");
    }
}
