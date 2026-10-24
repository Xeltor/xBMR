using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void MobilityRebinding()
    {
        foreach (var directional in new[] { false, true })
        {
            var map = Grid(31, 3);
            for (var row = 0; row < 3; ++row)
                for (var x = 8; x <= 18; ++x) map.PixelMaxG[row * 31 + x] = 2;
            var initial = Context(map, 32, speed: 6);
            var boss = Point(map, 54);
            var actionId = directional ? 94u : 36951u;
            MovementPlanningContext Capture(NavigationIdentity identity, WPos target)
                => new(identity, map, 0, 0, Enumerable.Repeat(true, 93).ToArray(), new float[93], [], [], 1,
                    targetId: 10, range: new(10, target, directional ? 8 : 5.5f, RecoverExecution: true),
                    mobilityMechanics: [new(actionId, directional ? MobilityMechanic.Directional : MobilityMechanic.Target,
                        directional ? 0 : 20, directional ? -15 : 0, .5f, 0, .6f, 0,
                        directional ? [] : [new(10, target, 2)])]);
            var before = Capture(initial.Identity, boss);
            var plan = JointMovementSearch.Search(before, Exhaustive, stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
            Check(plan.HasVerifiedRoute && plan.Stages[0].ActionId == actionId,
                "Rebinding fixture starts with a complete verified warning-crossing mobility route.");
            var now = before.Identity.CapturedTimestamp + (long)(.03 * Stopwatch.Frequency);
            var current = Capture(before.Identity with { CapturedTimestamp = now, WorldTime = before.Identity.WorldTime.AddSeconds(.03),
                Origin = before.Origin + new WDir(.15f, .2f) }, boss + new WDir(.3f, 0));
            Check(!MovementPlanValidation.Validate(plan, current, 0, true, now).Allowed,
                "The captured displacement cannot be executed unchanged after native recovery walks or its anchor moves.");
            var executor = new MovementPlanExecutor();
            var rebound = executor.RebindInitialMobility(plan, current);
            Check(rebound.Input == plan.Input && rebound.Stages[0].StartSeconds == plan.Stages[0].StartSeconds
                && rebound.Stages[0].ActionId == actionId && rebound.Stages[0].AnchorId == plan.Stages[0].AnchorId
                && rebound.Stages[0].From == current.Origin && MovementPlanValidation.Validate(rebound, current, 0, true, now).Allowed,
                "An unsubmitted control resolves its exact current landing and complete escape without renewing input or action times.");
            var observed = new MovementExecutionObservation(rebound.Stages[0].Facing ?? default);
            Check(executor.Advance(rebound, current, observed, true, now).Kind == MovementExecutionKind.Action,
                "The rebound route can actually reach action submission while native recovery has been moving.");
            executor.ConfirmActionSubmission(rebound);
            Check(ReferenceEquals(executor.RebindInitialMobility(rebound, before), rebound),
                "An in-flight mobility action keeps its submitted landing and effect identity.");
            var denied = new MovementPlanExecutor();
            Check(denied.Advance(denied.RebindInitialMobility(plan, current), current, observed, false, now).Kind == MovementExecutionKind.Invalidated,
                "Rebinding never grants movement authority over manual control.");
            for (var row = 0; row < 3; ++row) map.PixelMaxG[row * 31 + 10] = -1;
            var blocked = Capture(current.Identity, boss + new WDir(.3f, 0));
            var blockedExecutor = new MovementPlanExecutor();
            var blockedPlan = blockedExecutor.RebindInitialMobility(plan, blocked);
            Check(!MovementPlanValidation.Validate(blockedPlan, blocked, 0, true, now).Allowed,
                "A rebound control and its entire onward route still reject a new physical obstruction.");
        }
    }
}
