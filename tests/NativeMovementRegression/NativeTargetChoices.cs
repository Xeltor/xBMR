using BossMod;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;
using System.Diagnostics;

public static partial class Checks
{
    private static void NativeTargetChoices()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 1;
        var origin = Point(map, 0);
        var first = origin + new WDir(5, 0);
        var middle = origin + new WDir(8, 0);
        var last = origin + new WDir(11, 0);
        ExecutionRecipient[] recipients = [new(2, first, .5f, 1, true), new(3, middle, .5f, 1, true), new(4, last, .5f, 1)];
        ExecutionOpportunity Area(ulong target, WPos anchor, IEnumerable<ExecutionRecipient> values, float radius = 3)
            => new(ExecutionShape.TargetCircle, target, anchor, 26, radius, values);
        var original = Area(2, first, recipients);
        var alternative = Area(3, middle, recipients.Select(recipient => recipient with { Required = true }));
        var action = new PlanningAction(100, 2, 1, 4, 0, 0, 0, [], [], Opportunity: original,
            TargetChoices: [new(alternative, .05f)], UsesHardTarget: true);
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            origin, false, 6, 0, null, 7, 1, 1);
        MovementPlanningContext Capture(PlanningAction selected, ulong target = 2)
            => new(identity, map, 0, 0, [true, true, true], [0, 0, 0], [selected], [], targetId: target);
        var context = Capture(action);
        var result = JointMovementSearch.Search(context, Exhaustive);
        Check(result.HasVerifiedRoute && result.Stages[0].Kind == MovementStageKind.Target && result.Stages[0].AnchorId == 3
            && result.ActionEvidence.Single().TargetId == 3 && result.ExecutionValues[0] == 3
            && result.UsefulDestination == Point(map, 2),
            "One native search chooses the useful destination and better target together, before any walking leg.");
        Check(result.Status == MovementSearchStatus.Bounded && result.Bound == MovementSearchBound.TargetSetupEvidence,
            "An estimated target handoff is explicit bounded evidence until the original rotation confirms it.");
        Check(result.ActionEvidence.Single().RequiredRecipientIds.Order().SequenceEqual(new ulong[] { 2, 3, 4 }),
            "The chosen action witness retains each original and newly required recipient identity.");
        Check(MovementPlanValidation.Validate(result, context, 0, true, identity.CapturedTimestamp).Allowed,
            "The complete route validates target setup and the chosen action anchor against one frozen input.");
        var executor = new MovementPlanExecutor();
        var directive = executor.Advance(result, context, new(default, CurrentTarget: 2), true, identity.CapturedTimestamp);
        Check(directive.Kind == MovementExecutionKind.Target && directive.TargetId == 3 && directive.Waypoint == null,
            "The single executor requests target setup before it can publish the walking leg.");
        Check(executor.Advance(result, context, new(default, CurrentTarget: 3), true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Wait,
            "A changed hard target alone does not prove that RSR selected the promised action on it.");
        var confirmed = Capture(action with { TargetId = 3, Opportunity = alternative, TargetChoices = [] }, 3);
        var observed = executor.Advance(result, confirmed, new(default, CurrentTarget: 3), true, identity.CapturedTimestamp);
        Check(observed.Kind == MovementExecutionKind.Walk,
            "Walking begins only after fresh native action evidence and the actual target confirm the same anchor.");
        executor.Release();
        Check(executor.Advance(result, context, new(default, CurrentTarget: 2), true,
            identity.CapturedTimestamp + (long)(Stopwatch.Frequency * .06)).Kind == MovementExecutionKind.Invalidated,
            "A failed target setup cannot refresh its deadline or hold past its captured setup window.");
        Check(executor.Advance(result, context, default, false, identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "Manual authority invalidates target setup as well as walking and actions.");
        var noImprovement = action with { TargetChoices = [new(Area(3, middle, recipients, radius: 1), .05f)] };
        // This narrow shape changes mechanics and is rejected at the context boundary.
        var rejected = false;
        try { _ = Capture(noImprovement); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A target option cannot silently change the action's effect geometry.");
        var lostRecipient = action with { TargetChoices = [new(Area(3, middle,
            recipients.Select(recipient => recipient.Id == 2 ? recipient with { Required = false } : recipient)), .05f)] };
        rejected = false;
        try { _ = Capture(lostRecipient); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "No summed area gain can erase an independently required recipient in a target option.");
        var restrictedOriginal = new ExecutionOpportunity(ExecutionShape.TargetCircle, 2, first, 26, 3, recipients,
            forbiddenRecipients: [new(9, origin + new WDir(20, 0), .5f, 0)]);
        rejected = false;
        try { _ = Capture(action with { Opportunity = restrictedOriginal }); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A target option cannot erase a forbidden recipient from the original native snapshot.");
        var longerReach = new ExecutionOpportunity(ExecutionShape.TargetCircle, 3, middle, 30, 3, alternative.Recipients);
        rejected = false;
        try { _ = Capture(action with { TargetChoices = [new(longerReach, .05f)] }); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Changing the primary target cannot manufacture additional native action range.");
        var changed = Capture(action with { TargetChoices = [new(alternative, .1f)] });
        Check(!changed.SameProblem(context, false), "Changed target setup timing materially invalidates the planning input.");
        var overlay = BossMod.CombatAssistance.UI.NativePlanOverlay.Capture(result, directive, 0, [], 0);
        Check(overlay.Markers.Any(marker => marker.Label == "Target setup"),
            "The existing world-marker primitives explain a target-setup wait without a separate status panel.");
        // Existing native exit timing takes precedence over additional coverage.
        foreach (var warning in new[] { float.MaxValue, .01f, .09f, .12f, .14f, .3f })
        foreach (var ready in new[] { 0f, .1f, 1f })
        foreach (var setup in new[] { .01f, .05f, .2f })
        {
            map.PixelMaxG[0] = warning;
            var input = Capture(action with { Earliest = ready, TargetChoices = [new(alternative, setup)] });
            // Independent one-dimensional oracle. Enumerate each cast cell and
            // then walk to the highest native-goal cell. Effects have zero lock;
            // the only timed obstacle is departure from the original half-cell.
            float EarliestDelivery(float delay)
            {
                var best = float.PositiveInfinity;
                for (var cell = 0; cell < 3; ++cell)
                for (var setupCell = 0; setupCell <= cell; ++setupCell)
                {
                    var delivered = Math.Max(ready, delay + cell / 6f);
                    var exit = cell == 0 ? delivered + .5f / 6
                        : (setupCell == 0 ? delay : 0) + .5f / 6;
                    if (warning != float.MaxValue && exit > warning) continue;
                    best = Math.Min(best, delivered);
                }
                return best;
            }
            var oldTime = EarliestDelivery(0);
            var newTime = EarliestDelivery(setup);
            var actual = JointMovementSearch.Search(input, Exhaustive);
            if (!float.IsFinite(oldTime) && !float.IsFinite(newTime))
                Check(!actual.HasVerifiedRoute, "The target/cell oracle and native search both reject an impossible warning exit.");
            else
            {
                var expected = newTime <= oldTime + .00001f ? 3ul : 2ul;
                Check(actual.HasVerifiedRoute && actual.ActionEvidence.Single().TargetId == expected
                    && Math.Abs(actual.DeliveryTimes[0] - Math.Min(oldTime, newTime)) < .0001f,
                    "Native target/cell/time labels match an independent exhaustive warning/setup oracle.");
            }
        }
        map.PixelMaxG[0] = .12f;
        var urgent = Capture(action);
        var escape = JointMovementSearch.Search(urgent, Exhaustive);
        var setupIndex = Array.FindIndex(escape.Stages.ToArray(), stage => stage.Kind == MovementStageKind.Target);
        Check(escape.HasVerifiedRoute && escape.ActionEvidence.Single().TargetId == 3
            && escape.Stages[0].Kind == MovementStageKind.Walk && setupIndex > 0,
            "The joint plan clears an urgent warning first, then prepares its better anchor without a second correction destination.");
        Check(!new PlanningTargetChoice(alternative, .05f, 50).CanAcquire(origin)
            && new PlanningTargetChoice(alternative, .05f).CanAcquire(origin),
            "Target setup obeys the native three-dimensional acquisition envelope, independently of attack range.");
    }
}
