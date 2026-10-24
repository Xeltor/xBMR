using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Targeting;
using BossMod.Pathfinding;
using System.Collections.Immutable;
using System.Diagnostics;

public static partial class Checks
{
    private static void NativeTargetOptionInteractions()
    {
        var map = Grid(3, 1);
        var origin = Point(map, 0);
        var player = new ExecutionActor(1, origin, 0, .5f, default);
        ExecutionActor[] enemies = [new(2, origin + new WDir(5, 0), 0, .5f, default),
            new(3, origin + new WDir(8, 0), 0, .5f, default),
            new(4, origin + new WDir(11, 0), 0, .5f, default)];
        var selected = new RsrActionEvidence(100, 2, default, [2, 3], false, false,
            2, 25, 3, 0, 1, 2.5f, 0, 0, 0);
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            origin, false, 6, 0, null, 7, 1, 1);
        foreach (var move in new[] { false, true })
        foreach (var pick in new[] { false, true })
        foreach (var ready in new[] { 0f, 1f })
        foreach (var stationary in new[] { false, true })
        {
            var action = RsrExecutionCapture.Create(selected with { ReadyIn = ready }, player, enemies, [], 4, Positional.Any, null, out _,
                improveCoverage: move && !stationary, improveTargetChoice: pick)!;
            var original = action.Opportunity!;
            var choice = NativeOffensiveTargeting.Reanchor(original, 3, enemies[1].Position, .5f, 0, .05f, 0)!;
            if (pick && NativeOffensiveTargeting.ChoiceRespectsMovementPreference(original, choice, origin, move && !stationary))
                action = action with { TargetChoices = [choice] };
            var context = new MovementPlanningContext(identity, map, 0, 0, [true, true, true], [0, 0, 0], [action], [], targetId: 2, stationaryOnly: stationary);
            var plan = JointMovementSearch.Search(context, Exhaustive);
            var improveNow = pick && ready > .05f;
            Check(plan.HasVerifiedRoute && plan.ActionEvidence.Single().TargetId == (improveNow ? 3ul : 2ul)
                && plan.ExecutionValues.Single() == (improveNow ? 3 : 2),
                $"Better-target and movement-coverage choices act independently: move={move}, pick={pick}, ready={ready}, stationary={stationary}, status={plan.Status}, target={plan.ActionEvidence.FirstOrDefault()?.TargetId}, value={plan.ExecutionValues.FirstOrDefault()}, reason={plan.Evidence}.");
            Check(plan.UsefulDestination == origin && !plan.Stages.Any(stage => stage.From != stage.To)
                && MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
                "A stationary target gain is committed without adding a movement leg or weakening native validation.");
            Check(plan.ActionEvidence.Single().RequiredRecipientIds.Contains(2)
                && plan.ActionEvidence.Single().RequiredRecipientIds.Contains(3),
                "Either coverage opt-out still preserves the selected primary and each evidenced recipient.");
            Check(plan.DeliveryTimes.Single() == ready,
                "Extra recipients cannot delay an already available current action, even when better targeting is enabled.");
            if (improveNow)
                Check(plan.Status == MovementSearchStatus.Bounded && plan.Bound == MovementSearchBound.TargetSetupEvidence,
                    "Independent target-only optimization keeps its unconfirmed handoff explicitly bounded.");
        }

        var tied = RsrExecutionCapture.Create(selected, player, enemies[..2], [], 4, Positional.Any, null, out _,
            improveCoverage: false, improveTargetChoice: true)!;
        var equivalent = NativeOffensiveTargeting.Reanchor(tied.Opportunity!, 3, enemies[1].Position, .5f, 0, .05f, 0)!;
        Check(!NativeOffensiveTargeting.ChoiceRespectsMovementPreference(tied.Opportunity!, equivalent, origin, false),
            "Equivalent circular coverage retains the original target rather than creating an unnecessary handoff.");

        // The target option is stationary-only when movement coverage is off.
        // A farther cluster cannot manufacture a walk, even though its anchor
        // would become executable at another otherwise safe map position.
        ExecutionRecipient[] recipients = [new(2, new(3, 0), .5f, 1, true), new(3, new(6, 0), .5f, 1, true),
            new(4, new(9, 0), .5f, 1)];
        var shortRange = new ExecutionOpportunity(ExecutionShape.TargetCircle, 2, new(3, 0), 4, 3, recipients);
        var farther = NativeOffensiveTargeting.Reanchor(shortRange, 3, new(6, 0), .5f, 0, .05f, 0)!;
        Check(shortRange.Evaluate(default).Allowed && !farther.Opportunity.Evaluate(default).Allowed
            && farther.Opportunity.Evaluate(new(2, 0)).Allowed,
            "The far-cluster fixture distinguishes a legal stationary opportunity from a walk-dependent improvement.");
        Check(!NativeOffensiveTargeting.ChoiceRespectsMovementPreference(shortRange, farther, default, false)
            && NativeOffensiveTargeting.ChoiceRespectsMovementPreference(shortRange, farther, default, true),
            "A target-only preference rejects a newly required walk while the movement preference may consider it jointly.");
        Check(!NativeOffensiveTargeting.ChoiceRespectsMovementPreference(shortRange, farther, new(10, 0), false),
            "An available alternative cannot hide an unavailable original action when evaluating a stationary replacement.");

        foreach (var geometry in new[] { selected with { TargetId = 1, Range = 0 },
            selected with { Friendly = true }, selected with { GroundTargeted = true } })
        {
            var baseline = RsrExecutionCapture.Create(geometry, player, enemies, [], 4, Positional.Any, null, out _, improveCoverage: false)!;
            var targetOnly = RsrExecutionCapture.Create(geometry, player, enemies, [], 4, Positional.Any, null, out _,
                improveCoverage: false, improveTargetChoice: true)!;
            Check(baseline.Opportunity!.Recipients.SequenceEqual(targetOnly.Opportunity!.Recipients),
                "A target-choice option cannot silently enable self, ground or healing movement coverage.");
        }
        foreach (var cone in new[] { true, false })
        {
            ExecutionActor[] directionalEnemies = [new(2, origin + new WDir(5, cone ? 6 : 2), 0, .1f, default),
                new(3, origin + new WDir(5, 0), 0, .1f, default),
                new(4, origin + new WDir(5, cone ? -5 : -2), 0, .1f, default)];
            var directional = selected with { CastType = cone ? (byte)3 : (byte)4, EffectRange = 10,
                HalfWidth = cone ? 0 : 3, ReadyIn = 1 };
            var action = RsrExecutionCapture.Create(directional, player, directionalEnemies, [], 4, Positional.Any, null, out _,
                improveCoverage: false, improveTargetChoice: true)!;
            var original = action.Opportunity!;
            var choice = NativeOffensiveTargeting.Reanchor(original, 3, directionalEnemies[1].Position, .1f, 0, .05f, 0)!;
            Check(choice.Preserves(original) && NativeOffensiveTargeting.ChoiceRespectsMovementPreference(original, choice, origin, false),
                "Directional target choice improves aim from the current pose while preserving every original recipient.");
            var context = new MovementPlanningContext(identity, map, 0, 0, [true, true, true], [0, 0, 0],
                [action with { TargetChoices = [choice] }], [], targetId: 2, stationaryOnly: true);
            var plan = JointMovementSearch.Search(context, Exhaustive);
            Check(plan.HasVerifiedRoute && plan.ActionEvidence.Single().TargetId == 3 && plan.ExecutionValues.Single() == 3
                && plan.UsefulDestination == origin && !plan.Stages.Any(stage => stage.From != stage.To),
                "One native plan selects the useful directional anchor without creating a second positioning move.");
            Check(MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed
                && plan.Stages.Single(stage => stage.Kind == MovementStageKind.Cast).Facing == Angle.FromDirection(new WDir(5, 0)),
                "Directional target setup retains exact executable facing and native route validation.");
            var protectedTarget = new ExecutionOpportunity(original.Shape, original.TargetId, original.Anchor, original.CastReach,
                original.EffectRadius, original.Recipients, original.HalfWidth, original.HalfAngle, tankFacing: new(default, 1f.Degrees()));
            Check(!NativeOffensiveTargeting.SupportsTargetChoice(protectedTarget),
                "Target improvement cannot transfer an independent tank-facing assignment to another enemy.");
            var guardedDirection = new ExecutionOpportunity(original.Shape, original.TargetId, original.Anchor, original.CastReach,
                original.EffectRadius, original.Recipients, original.HalfWidth, original.HalfAngle,
                forbiddenRecipients: [new(99, directionalEnemies[2].Position, .1f, 0)]);
            var unsafeChoice = NativeOffensiveTargeting.Reanchor(guardedDirection, 3, directionalEnemies[1].Position, .1f, 0, .05f, 0)!;
            Check(!NativeOffensiveTargeting.ChoiceRespectsMovementPreference(guardedDirection, unsafeChoice, origin, false),
                "A directional area gain cannot conceal forbidden collateral.");
        }
        var forbidden = enemies[2] with { Id = 99 };
        var guarded = RsrExecutionCapture.Create(selected, player, enemies, [forbidden], 4, Positional.Any, null, out _,
            improveCoverage: false, improveTargetChoice: true)!;
        var rejected = NativeOffensiveTargeting.Reanchor(guarded.Opportunity!, 3, enemies[1].Position, .5f, 0, .05f, 0)!;
        Check(!NativeOffensiveTargeting.ChoiceRespectsMovementPreference(guarded.Opportunity!, rejected, origin, false),
            "Target-only improvement still rejects forbidden collateral before promising a different anchor.");
    }
}
