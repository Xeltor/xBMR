using BossMod;
using BossMod.CombatAssistance.Camera;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.UI;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void Presentation()
    {
        uint? mode = 0;
        var option = new LegacyMovementOption(() => mode, value => mode = value);
        var now = DateTime.UtcNow;
        option.Update(true, false, now);
        Check(mode == 1 && option.Active, "camera can borrow legacy movement while authorized");
        option.Update(false, true, now);
        Check(mode == 0 && !option.Active, "manual input restores borrowed camera movement immediately");
        option.Update(true, false, now.AddSeconds(1));
        Check(mode == 0, "manual correction has a stable camera handoff interval");
        option.Update(true, false, now.AddSeconds(4));
        mode = 2;
        option.Update(true, false, now.AddSeconds(5));
        option.Restore();
        Check(mode == 2 && !option.Active, "user movement-mode edit survives camera ownership release");
        mode = 1;
        option.Update(true, false, now.AddSeconds(9));
        option.Restore();
        Check(mode == 1, "camera preserves an already selected legacy movement mode");

        var context = Context(Grid(3, 1), 0);
        var plan = JointMovementSearch.Search(context, Exhaustive) with
        {
            Status = MovementSearchStatus.Bounded, UsefulDestination = context.Point(2),
            Stages = [new(MovementStageKind.Walk, context.Point(0), context.Point(1), 0, 1),
                new(MovementStageKind.TargetDash, context.Point(1), context.Point(2), 1, 2, 99, 10)]
        };
        var overlay = NativePlanOverlay.Capture(plan, new(MovementExecutionKind.Action, "ready"), 1,
            [new(100, 20, context.Point(1), context.Point(0), null, "anchor unavailable")], 7);
        Check(overlay.Lines.Length == 1 && overlay.Lines[0].From.X == context.Point(1).X
            && overlay.Destination.X == context.Point(2).X && overlay.Destination.Y == 7,
            "overlay depicts only the remaining actual route and final useful destination");
        Check(overlay.Status.Contains("bounded") && overlay.DestinationLabel == "Movement action"
            && overlay.Markers.Length == 2 && overlay.Markers[1].State == DecisionOverlayState.Rejected,
            "overlay distinguishes bounded search, committed mobility and local rejection");

        var preview = NativePlanOverlay.Capture(plan with { Status = MovementSearchStatus.Searching }, null, 0, [], 7);
        Check(preview.Preview && preview.Lines.All(line => line.State == DecisionOverlayState.Candidate),
            "uncommitted search draws preview primitives instead of active movement");
        var recovery = new BossMod.Pathfinding.NavigationDecision { Destination = context.Point(1), LeewaySeconds = 1 };
        recovery.Publication = new(context.Identity, null, [context.Point(0), context.Point(1)], true, false,
            BossMod.Pathfinding.ThetaStar.Score.Safe, recovery, false);
        var recoveryPlan = plan with { Status = MovementSearchStatus.NativeRecovery, NativeRecovery = recovery, Stages = [] };
        Check(NativePlanOverlay.CanDisplay(recoveryPlan)
            && NativePlanOverlay.Capture(recoveryPlan, null, 0, [], 7).Lines.Length == 1,
            "native recovery remains visible through its actual complete witness");
        var failures = 0;
        var failedOption = new LegacyMovementOption(() => throw new InvalidOperationException("unavailable"), _ => { }, _ => ++failures);
        failedOption.Update(true, false, now);
        failedOption.Update(true, false, now.AddSeconds(1));
        Check(failures == 1, "camera option failures are contained and retried at a bounded rate");

        var grid = Grid(3, 1);
        var original = Context(grid, 0, [Action(grid, 4, 0, new ulong[3], [1, 0, 0])]);
        var cosmetic = new MovementPlanningContext(original.Identity, grid, 0, 0, [true, true, true], [0, 1e9f, 0],
            [Action(grid, 4, 0, new ulong[3], [1, 0, 0])], []);
        var selected = JointMovementSearch.Search(cosmetic, Exhaustive);
        Check(selected.ExecutionValues[0] == 1 && selected.DeliveryTimes[0] == 0,
            "arbitrary presentation gain cannot outweigh current action delivery");
    }
}
