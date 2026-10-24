using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void LeyLinesPlacement()
    {
        var map = Grid(4, 1);
        map.PixelMaxG[0] = 1.5f;
        var original = Context(map, 0);
        var action = Action(map, 4, 0, new ulong[4], [1, 1, 1, 1], earliest: 2);
        var placement = new PlanningFieldPlacement(1, 0, 1.8f, .2f);
        MovementPlanningContext Capture(PlanningFieldPlacement? field = null, PlanningAction? spell = null,
            PlanningExecutionRules? rules = null)
            => new(original.Identity, map, 0, 0, [true, true, true, true], [0, 0, 0, 0], [spell ?? action], [],
                fieldPlacement: field, executionRules: rules);
        var context = Capture(placement);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        var stage = plan.Stages.Single(s => s.Kind == MovementStageKind.FieldPlacement);
        Check(stage.To == Point(map, 1) && plan.UsefulDestination == Point(map, 1) && plan.FieldPlacementGain > 9
            && plan.DeliveryTimes[0] == 2,
            "The first native plan chooses a safer Ley Lines placement and the selected spell without losing its GCD slot.");
        Check(MovementPlanValidation.Validate(plan, context, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "Placement, action lock, selected attack and onward route share native timed traversal validation.");
        Check(plan.Stages.Where(s => s.Kind == MovementStageKind.Cast).All(s => s.EndSeconds == 2),
            "An unobserved field placement cannot invent haste for its selected spell.");
        var noSlot = JointMovementSearch.Search(Capture(placement with { Latest = .2f }), Exhaustive);
        Check(noSlot.Stages.All(s => s.Kind != MovementStageKind.FieldPlacement),
            "A better field location cannot extend the original approved placement weave.");
        var immediate = JointMovementSearch.Search(Capture(placement, action with { Earliest = 0 }), Exhaustive);
        Check(immediate.DeliveryTimes[0] == 0, "A field preference cannot purchase a delayed available attack.");
        var disabled = JointMovementSearch.Search(Capture(), Exhaustive);
        Check(disabled.Stages.All(s => s.Kind != MovementStageKind.FieldPlacement),
            "No observed or enabled placement intent means no Ley Lines movement stage.");
        var forbidden = JointMovementSearch.Search(Capture(placement, rules: new(float.MaxValue, 0, [])), Exhaustive);
        Check(forbidden.Stages.All(s => s.Kind != MovementStageKind.FieldPlacement),
            "An original rotation opportunity cannot bypass native action prohibitions.");
        Check(!MovementPlanValidation.Validate(plan, Capture(), 0, true, context.Identity.CapturedTimestamp).Allowed
            && !MovementPlanValidation.Validate(plan, context, 0, false, context.Identity.CapturedTimestamp).Allowed,
            "An opt-out or manual handoff invalidates the old placement route.");
        var index = Array.FindIndex(plan.Stages.ToArray(), s => s.Kind == MovementStageKind.FieldPlacement);
        Check(!MovementPlanValidation.Validate(plan, context, index, true, context.Identity.CapturedTimestamp,
            Point(map, 3)).Allowed, "Native release validates the real stopped pose, not the planned placement waypoint.");
        var executor = new MovementPlanExecutor();
        Check(executor.Advance(plan, context, default, true, context.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "The single native executor owns walking toward the placement point.");
        Check(context.Excluding([]).FieldPlacement == placement,
            "Local mobility exclusions retain the immutable field-placement opportunity.");

        var selection = (BossMod.CombatAssistance.Integrations.RsrLeyLinesPlacement)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(BossMod.CombatAssistance.Integrations.RsrLeyLinesPlacement));
        var token = new NativePlacementOpportunity();
        var instant = Stopwatch.GetTimestamp();
        var gcdTime = DateTime.UtcNow;
        var observedPlacement = token.Observe(1, 7, 10, gcdTime, instant, 0, 2, .2f, original.Origin)!;
        var selectionType = selection.GetType();
        selectionType.GetField("_opportunity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(selection, token);
        var freshIdentity = original.Identity with { CapturedTimestamp = instant, WorldTime = gcdTime };
        var activeContext = new MovementPlanningContext(freshIdentity, map, 0, 0, [true, true, true, true], [0, 0, 0, 0],
            [action], [], targetId: 10, fieldPlacement: observedPlacement);
        var activePlan = JointMovementSearch.Search(activeContext, Exhaustive);
        selection.Consider(activePlan, activeContext);
        var prepared = selectionType.GetField("_prepared", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Check(prepared.GetValue(selection) != null && token.Approve(observedPlacement.Opportunity),
            "Original RSR selection can approve the exact fresh placement prepared by the native owner.");
        var authorized = selectionType.GetField("_authorized", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        authorized.SetValue(selection, (activePlan, activeContext, 0));
        selection.Consider(null, activeContext);
        Check(prepared.GetValue(selection) == null && authorized.GetValue(selection) == null && token.Approved,
            "A fresh capture with no executable directive cannot inherit the preceding frame's placement release authority.");
        selection.Consider(activePlan, activeContext);
        var explicitContext = new MovementPlanningContext(freshIdentity, map, 0, 0, [true, true, true, true], [0, 0, 0, 0],
            [action], [], targetId: 10, requiredDestination: Point(map, 1));
        selection.Consider(null, explicitContext);
        Check(prepared.GetValue(selection) == null && token.Active == 0 && !token.Approved
            && token.Observe(1, 7, 10, gcdTime, instant + 1, 0, 2, .2f, original.Origin) == null,
            "An explicit destination or lost placement authority clears selection preparation without renewing that GCD's retired window.");

        foreach (var selfTarget in new[] { context.Identity.ActorId, 0xE0000000ul })
        {
            var selfPlan = new MovementPlan(context.Identity, MovementSearchStatus.Complete,
                [new(MovementStageKind.FieldPlacement, context.Origin, context.Origin, 0, .2f, PlanningFieldPlacement.ActionId),
                    new(MovementStageKind.Walk, context.Origin, Point(map, 1), .2f, 1.2f)],
                Point(map, 1), Point(map, 1), float.MaxValue, 1.2f, 0, 0, [], 0, 0, 0, "native self-field effect fixture")
                { FieldPlacement = placement, TargetId = context.TargetId };
            var selfExecutor = new MovementPlanExecutor();
            selfExecutor.Advance(selfPlan, context, new(default, PlanningFieldPlacement.ActionId, selfTarget,
                context.Identity.WorldTime), true, context.Identity.CapturedTimestamp);
            Check(selfExecutor.StageIndex == 1,
                "Native Ley Lines self effects accept the player's identity or the normal untargeted self-action sentinel.");
        }
        var owner = new NativePlacementOpportunity();
        var now = Stopwatch.GetTimestamp();
        var gcd = DateTime.UtcNow;
        var first = owner.Observe(7, 1, 10, gcd, now, 0, 2, .2f)!;
        Check(!owner.Approved && !owner.Approve(first.Opportunity + 1) && owner.Approve(first.Opportunity),
            "Native availability can prepare a field route but only the matching original RSR approval may authorize commitment.");
        Check(!owner.Approve(first.Opportunity + 1) && owner.Approved,
            "A stale candidate approval cannot revoke a different current approved opportunity.");
        var later = owner.Observe(7, 1, 10, gcd, now + Stopwatch.Frequency, 0, 2, .2f)!;
        Check(first.Opportunity == later.Opportunity && Math.Abs(later.Latest - (first.Latest - 1)) < .001f,
            "Repeated original CanUse success does not renew the captured weave deadline.");
        Check(owner.Capture(now + 2 * Stopwatch.Frequency) == null && !owner.Approved
            && owner.Observe(7, 1, 10, gcd, now + 2 * Stopwatch.Frequency, 0, 2, .2f) == null,
            "An expired opportunity cannot rearm on a cached GCD effect.");
        Check(owner.Observe(7, 1, 10, gcd.AddSeconds(2), now + 2 * Stopwatch.Frequency, 0, 2, .2f) != null,
            "An actually observed later GCD permits a new native-approved placement opportunity.");
        owner.Reset();
        var anchored = owner.Observe(7, 1, 10, gcd, now, 0, 2, .2f, new WPos(1, 2))!;
        var continued = owner.Observe(7, 1, 10, gcd, now + Stopwatch.Frequency / 2, 0, 2, .2f, new WPos(3, 4))!;
        Check(continued.OriginalPosition == anchored.OriginalPosition && continued.OriginalPosition == new WPos(1, 2),
            "Beginning the committed walk cannot replace the original placement comparison point with a new endpoint.");
        Check(owner.Capture(7, 1, 11, now) == null,
            "A changed target cannot inherit the placement opportunity approved for an earlier selected attack.");
        owner.Reset();
        Check(owner.Active == 0 && owner.Capture(now) == null,
            "Logout, job change, unload and authority loss retain no saved placement ownership.");
        LeyLinesPlacementOracle();
        foreach (var earliest in new[] { .01f, .13f, .27f, .51f, .77f })
        {
            var precisionMap = Grid(2, 1);
            precisionMap.PixelMaxG[0] = MathF.BitDecrement(12);
            var precision = Context(precisionMap, 0);
            Check(new PlanningFieldPlacement(1, earliest, 2, .6f).MayImprove(precision),
                "Field pruning uses identical float arithmetic to retain even a one-ULP evidenced window gain.");
        }
        var benchmarkMap = Grid(16, 16);
        benchmarkMap.PixelMaxG[0] = .5f;
        var benchmark = Context(benchmarkMap, 0, speed: 6);
        var benchmarkAction = Action(benchmarkMap, 6, 0, new ulong[256], Enumerable.Repeat(1f, 256).ToArray(), earliest: 2.2f);
        var benchmarkContext = new MovementPlanningContext(benchmark.Identity, benchmarkMap, 0, 0,
            Enumerable.Repeat(true, 256).ToArray(), new float[256], [benchmarkAction], [],
            fieldPlacement: new(1, 0, 1.6f, .6f, benchmark.Origin));
        var clock = Stopwatch.StartNew();
        var measured = JointMovementSearch.Search(benchmarkContext);
        Check(measured.HasVerifiedRoute && measured.DeliveredActions == 1 && measured.FieldPlacementGain > 0,
            "A placement-rich native search retains a complete useful plan under its real freshness boundary.");
        Console.WriteLine($"Synthetic 256-cell placement: {clock.Elapsed.TotalMilliseconds:F3}ms, {measured.ExpandedStates} expansions, {measured.Status}/{measured.Bound}; excludes framework capture and live delivery.");
    }

    private static void LeyLinesPlacementOracle()
    {
        // Independent finite enumeration of placement, cast and final cells.
        // The native traversal oracle is shared; the solver/order are independent.
        foreach (var warning in new[] { .6f, 1.1f, 1.5f, 3f, float.MaxValue })
        foreach (var ready in new[] { 1f, 2f, 3f })
        foreach (var blocked in new[] { false, true })
        {
            var map = Grid(4, 1);
            map.PixelMaxG[0] = warning;
            if (blocked) map.PixelMaxG[2] = -1;
            var original = Context(map, 0);
            var action = Action(map, 5, 0, new ulong[4], [1, 1, 1, 1], earliest: ready);
            var placement = new PlanningFieldPlacement(1, 0, ready - .2f, .2f);
            var context = new MovementPlanningContext(original.Identity, map, 0, 0, [true, true, true, true], [0, 0, 0, 0],
                [action], [], fieldPlacement: placement);
            var best = 0f;
            var baseline = Math.Max(0, Math.Min(12 - .2f, warning - .2f));
            for (var p = 0; p < 4; ++p)
            for (var c = 0; c < 4; ++c)
            for (var f = 0; f < 4; ++f)
            {
                var finish = p + .2f;
                var castArrival = finish + Math.Abs(c - p);
                if (p > placement.Latest || castArrival > ready || !context.CanRemain(Point(map, f))
                    || Math.Abs(f - p) > PlanningFieldPlacement.Radius
                    || !context.Traverse(Point(map, 0), Point(map, p), 0, p, out _)
                    || !context.Traverse(Point(map, p), Point(map, p), p, .2f, out _)
                    || !context.Traverse(Point(map, p), Point(map, c), finish, Math.Abs(c - p), out _)
                    || !context.Traverse(Point(map, c), Point(map, c), castArrival, ready - castArrival, out _)
                    || !context.Traverse(Point(map, c), Point(map, f), ready, Math.Abs(f - c), out _)) continue;
                var window = Math.Max(0, Math.Min(12 - finish, map.PixelMaxG[p] - finish));
                best = Math.Max(best, Math.Max(0, window - baseline));
            }
            var plan = JointMovementSearch.Search(context, Exhaustive);
            var unpruned = JointMovementSearch.Search(context, Exhaustive, pruneExecutionInferiorStates: false);
            Check(plan.FieldPlacementGain == unpruned.FieldPlacementGain && plan.DeliveredActions == unpruned.DeliveredActions
                && plan.DeliveryTimes.SequenceEqual(unpruned.DeliveryTimes),
                "Proved action/placement bounds preserve the independent unpruned result.");
            // If the native graph cannot retain the current attack at its earliest
            // slot, a field cannot improve that missing opportunity's result.
            var baselinePlan = JointMovementSearch.Search(new(original.Identity, map, 0, 0,
                [true, true, true, true], [0, 0, 0, 0], [action], []), Exhaustive);
            if (baselinePlan.DeliveredActions == 0 || baselinePlan.DeliveryTimes[0] != ready) continue;
            Check(Math.Abs(plan.FieldPlacementGain - best) < .001f && plan.DeliveryTimes[0] == ready,
                $"Independent placement/cast/final-cell oracle: warning={warning}, ready={ready}, blocked={blocked}.");
        }
    }
}
