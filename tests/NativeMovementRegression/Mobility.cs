using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void MobilityMechanics()
    {
        var preferences = new BossMod.CombatAssistance.Config.AssistancePreferences { UseBetweenTheLines = false };
        var lines = NativeMobilityCapture.Definitions.Single(d => d.Id == 7419);
        Check(!NativeMobilityCapture.EnabledBySettings(lines, preferences, Class.BLM),
            "the saved Between the Lines opt-out applies even when general movement abilities are enabled");
        preferences.UseBetweenTheLines = true;
        preferences.ManageLeylines = false;
        Check(!NativeMobilityCapture.EnabledBySettings(lines, preferences, Class.BLM),
            "disabling the Ley Lines feature also disables its native return action");
        var map = Grid(7, 3);
        var start = 7;
        var end = 13;
        var baseline = Context(map, start);
        var anchor = new MobilityAnchor(20, Point(map, end), 0);
        var dash = new MobilityMechanics(10, MobilityMechanic.Target, 4, 0, 0, 0, 0.1f, 0, [anchor]);
        var recipients = new ulong[21]; recipients[end] = 1;
        var values = new float[21]; values[end] = 1;
        var attack = Action(map, 3.2f, 1, recipients, values);
        MovementPlanningContext Input(IEnumerable<MobilityMechanics> mobility, PlanningExecutionRules? rules = null,
            ulong resources = 1, PlanningAction[]? actions = null)
            => new(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 21).ToArray(), new float[21], actions ?? [attack], [],
                resources, mobilityMechanics: mobility, executionRules: rules);
        var context = Input([dash]);
        Check(!context.MobilityFrom(baseline.Origin).Any(), "a target dash cannot be invented outside its legal anchor range");
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.HasVerifiedRoute && plan.DeliveredActions == 1 && plan.Stages[0].Kind == MovementStageKind.Walk
            && plan.Stages.Any(s => s.Kind == MovementStageKind.TargetDash && s.From != baseline.Origin && s.To == anchor.Position),
            "the joint route walks into anchor range before dashing and delivering the evidenced action");
        var locked = Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor], readyIn: 2)]);
        var lockedPlan = JointMovementSearch.Search(locked, Exhaustive);
        Check(lockedPlan.Stages.First(s => s.Kind == MovementStageKind.TargetDash).StartSeconds >= 2,
            "animation locks delay mobility without categorically preventing walking");
        var expired = Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor], readyIn: 4)]);
        Check(JointMovementSearch.Search(expired, Exhaustive).DeliveredActions == 0,
            "an action lock cannot be omitted to credit an impossible attack window");

        var backstep = new MobilityMechanics(11, MobilityMechanic.Directional, 0, -6, 0, 0, 0.1f, 0);
        var back = Input([backstep]);
        var edge = back.MobilityFrom(baseline.Origin).First(m => m.LandingPosition == anchor.Position);
        Check(edge.Facing is { } facing && (baseline.Origin - facing.ToDirection() * 6).AlmostEqual(anchor.Position, 0.001f),
            "deliberate opposite facing lets a real backstep travel toward the useful destination");
        var backPlan = JointMovementSearch.Search(back, Exhaustive);
        Check(backPlan.DeliveredActions == 1 && backPlan.Stages.Any(s => s.Kind == MovementStageKind.DirectionalMovement),
            "a directional backstep competes in the same action window as walking and dashing");
        var ground = Input([new(12, MobilityMechanic.Ground, 10, 0, 0, 0, 0.1f, 0)]);
        Check(ground.MobilityFrom(baseline.Origin).Select(m => m.DestinationCell).Distinct().Count() == 20,
            "ground movement considers every supported map cell rather than the previous endpoint's neighborhood");
        foreach (var candidateContext in new[] { ground, back, alternativesForResolution() })
            foreach (var candidate in candidateContext.MobilityFrom(baseline.Origin))
            {
                var stage = new MovementStage(candidate.Kind, baseline.Origin, candidateContext.MobilityLanding(candidate), 0, 1,
                    candidate.ActionId, candidate.AnchorId, candidate.Facing);
                Check(candidateContext.ResolveMobility(baseline.Origin, stage) != null,
                    "direct final resolution agrees with the full worker candidate generator");
            }
        MovementPlanningContext alternativesForResolution() => Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor])]);
        var missingReturn = Input([new(13, MobilityMechanic.Return, 30, 0, 0, 0, 0.1f, 0)]);
        Check(!missingReturn.MobilityFrom(baseline.Origin).Any(), "return movement requires a captured owned return position");
        var returned = Input([new(13, MobilityMechanic.Return, 30, 0, 0, 0, 0.1f, 0, returnPosition: anchor.Position)]);
        Check(returned.MobilityFrom(baseline.Origin).Single().LandingPosition == anchor.Position,
            "return movement cannot choose an arbitrary ground destination");
        var minimum = Input([new(12, MobilityMechanic.Ground, 10, 0, 0, 0, 0.1f, 0, minimumTravel: 5)]);
        Check(minimum.MobilityFrom(baseline.Origin).All(m => (m.LandingPosition!.Value - baseline.Origin).Length() >= 5),
            "the imported minimum movement distance remains an explicit preference");

        var earlier = Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor], readyIn: 0.2f)]);
        var later = new MovementPlanningContext(earlier.Identity with { WorldTime = earlier.Identity.WorldTime.AddSeconds(0.05) },
            map, 0, 0, Enumerable.Repeat(true, 21).ToArray(), new float[21], [], [], 1,
            mobilityMechanics: [new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor], readyIn: 0.15f)]);
        var noAttack = new MovementPlanningContext(earlier.Identity, map, 0, 0, Enumerable.Repeat(true, 21).ToArray(), new float[21], [], [], 1,
            mobilityMechanics: earlier.MobilityMechanics);
        Check(later.SameProblem(noAttack), "equivalent cooldown countdowns do not restart a planning job");
        var alternatives = Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [anchor]), backstep]);
        var failed = alternatives.Excluding([new(10, anchor.Id, baseline.Origin, anchor.Position,
            Angle.FromDirection(anchor.Position - baseline.Origin), "native target rejected")]);
        Check(!failed.MobilityFrom(baseline.Origin).Any(m => m.ActionId == 10)
            && failed.MobilityFrom(baseline.Origin).Any(m => m.ActionId == 11)
            && JointMovementSearch.Search(failed, Exhaustive).DeliveredActions == 1,
            "failure of one exact candidate leaves other legal movement actions available");
        Check(!failed.SameProblem(alternatives) && failed.SameProblem(alternatives, ignoreRejections: true),
            "a rejected executable candidate restarts search without pretending encounter evidence changed");
        var protectedActor = new ExecutionRecipient(99, anchor.Position, 0, 0);
        var damage = new ExecutionOpportunity(ExecutionShape.TargetCircle, anchor.Id, anchor.Position, 10, 2,
            [new(anchor.Id, anchor.Position, 0, 1, true)], forbiddenRecipients: [protectedActor]);
        var dangerousAnchor = anchor with { Effect = damage };
        var protectedInput = Input([new(10, MobilityMechanic.Target, 10, 0, 0, 0, 0.1f, 0, [dangerousAnchor]), backstep]);
        Check(!protectedInput.MobilityFrom(baseline.Origin).Any(m => m.ActionId == 10)
            && protectedInput.MobilityFrom(baseline.Origin).Any(m => m.ActionId == 11),
            "damaging mobility cannot hit a forbidden recipient, while non-damaging alternatives remain available");
        var shared = Grid(3, 1);
        PlanningMobility[] chain = [new(0, 1, MovementStageKind.TargetDash, 20, 21, null, 0, 0.1f, 0, 0, AdditionalResourceCost: 0x100),
            new(1, 2, MovementStageKind.TargetDash, 22, 23, null, 0, 0.1f, 0, 1, AdditionalResourceCost: 0x100)];
        var chainAction = Action(shared, 0.3f, 1, [0, 0, 1], [0, 0, 1]);
        var scarce = Context(shared, 0, [chainAction], chain, resources: 0x111);
        var enough = Context(shared, 0, [chainAction], chain, resources: 0x211);
        Check(JointMovementSearch.Search(scarce, Exhaustive).DeliveredActions == 0
            && JointMovementSearch.Search(enough, Exhaustive).DeliveredActions == 1,
            "distinct movement cooldowns cannot spend the same shared job gauge twice");

        map.PixelMaxG[10] = -1;
        var blockedContext = Input([dash, backstep]);
        var obstructed = JointMovementSearch.Search(blockedContext, Exhaustive);
        Check(obstructed.Stages.Where(s => s.Kind is MovementStageKind.TargetDash or MovementStageKind.DirectionalMovement)
            .All(s => blockedContext.Traverse(s.From, s.To, s.StartSeconds, s.EndSeconds - s.StartSeconds, out _)),
            "a mobility candidate never bypasses the shared native traversal proof");

        var hold = new PlanningExecutionRules(0, float.MaxValue, [], MovementResumesAt: 1);
        var waiting = Input([], hold, actions: []);
        map.PixelPriority[end] = 1;
        waiting = Input([], hold, actions: []);
        var waitPlan = JointMovementSearch.Search(waiting, Exhaustive);
        Check(waitPlan.HasVerifiedRoute && waitPlan.UsefulDestination == anchor.Position && waitPlan.Stages[0].Kind == MovementStageKind.Wait
            && waitPlan.Stages.First(s => s.Kind == MovementStageKind.Walk).StartSeconds >= 1,
            "an authored movement prohibition can be waited out only when its finish is known");
        var unknown = JointMovementSearch.Search(Input([], hold with { MovementResumesAt = float.MaxValue }, actions: []), Exhaustive);
        Check(unknown.UsefulDestination == baseline.Origin, "unknown movement-prohibition expiry is never invented");
        var pyretic = new PlanningExecutionRules(float.MaxValue, 0, [], ActionsResumeAt: 2);
        var pyreticPlan = JointMovementSearch.Search(Input([dash], pyretic), Exhaustive);
        Check(pyreticPlan.Stages.Where(s => s.ActionId != 0).All(s => s.StartSeconds >= 2),
            "authored pyretic cannot be outweighed by attack or mobility value");
        var forbidden = new PlanningExecutionRules(float.MaxValue, float.MaxValue,
            [new(edge.Facing!.Value, 10.Degrees(), 0)]);
        var rejectedFacing = JointMovementSearch.Search(Input([backstep], forbidden), Exhaustive);
        Check(rejectedFacing.Stages.All(s => s.Facing is not { } f || !f.AlmostEqual(edge.Facing.Value, 10.Degrees().Rad)),
            "gaze and directional assignments remain independent hard action constraints");
    }
}
