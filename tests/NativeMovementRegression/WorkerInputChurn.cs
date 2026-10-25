using BossMod.CombatAssistance.Planning;
using System.Threading;
using System.Diagnostics;

public static partial class Checks
{
    private static void WorkerInputChurn()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 1;
        var first = Context(map, 0);
        var plan = JointMovementSearch.Search(first, Exhaustive);
        var searching = plan with { Status = MovementSearchStatus.Searching };
        var commit = NativeMovementHost.CommitCandidate(searching, first);
        Check(commit is { Status: MovementSearchStatus.Bounded, Bound: MovementSearchBound.SearchInProgress }
            && commit.Input == searching.Input && commit.Stages == searching.Stages,
            "A complete worker incumbent is executable while optimization continues without renewing its input.");
        var action = Action(map, 10, 0, new ulong[3], [1, 1, 1]);
        var withAction = Context(map, 0, [action]);
        Check(NativeMovementHost.CommitCandidate(searching, withAction) == null,
            "An interim safety-only incumbent cannot commit before the current action opportunity is evaluated.");
        var delivered = JointMovementSearch.Search(withAction, Exhaustive) with { Status = MovementSearchStatus.Searching };
        Check(NativeMovementHost.CommitCandidate(delivered, withAction)?.DeliveredActions == 1,
            "A verified current action and complete onward route can commit during continued optimization.");
        Check(NativeMovementHost.CommitCandidate(plan with { Status = MovementSearchStatus.NativeRecovery }, first) == null,
            "Native recovery is never relabeled as a verified optimization result.");
        var positionalMap = Grid(5, 1);
        var positionalAction = Action(positionalMap, 10, 0, new ulong[5], [0, 0, 1, 1, 1]);
        var positional = Context(positionalMap, 0, [positionalAction]);
        var walking = JointMovementSearch.Search(positional, Exhaustive, includeMobilityActions: false,
            stopAtCurrentOpportunity: true);
        Check(walking.HasVerifiedRoute && walking.DeliveredActions == 1
            && walking.Status == MovementSearchStatus.Bounded && walking.Bound == MovementSearchBound.SearchInProgress,
            "Walking prefix finds a complete current positional opportunity without claiming exhaustive optimization.");
        var continued = JointMovementSearch.Search(positional, Exhaustive with { Expansions = 0 }, nativeIncumbent: walking);
        Check(continued.DeliveredActions == 1 && continued.HasVerifiedRoute && continued.Input == walking.Input
            && continued.DeliveryTimes.SequenceEqual(walking.DeliveryTimes),
            "A fresh walking/action incumbent survives a bounded full mobility search without renewing action timing.");
        var spacingMap = Grid(5, 1);
        var spacingAction = Action(spacingMap, 10, 0, new ulong[5], [0, 1, 1, 1, 1], earliest: 3);
        var spacingIdentity = Context(spacingMap, 0).Identity;
        var spacing = new MovementPlanningContext(spacingIdentity, spacingMap, 0, 0,
            [true, true, true, true, true], [0, -1, 0, 0, 0], [spacingAction], []);
        var spaced = JointMovementSearch.Search(spacing, Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(spaced.UsefulDestination == Point(spacingMap, 2) && spaced.DeliveryTimes[0] == 3,
            "The preliminary pass compares equally timely walking positions before selecting an inside-hitbox first encounter.");
        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        var now = first.Identity.CapturedTimestamp;
        using var service = new MovementPlanningService((context, cancel, publish) =>
        {
            publish(plan with { Status = MovementSearchStatus.Searching });
            started.Set();
            finish.Wait();
            completed.Set();
            return plan;
        }, () => now);
        service.Submit("normal", 1, first);
        Check(started.Wait(1000), "Worker has a complete route before the next target observation.");
        try
        {
            var current = Context(map, 0);
            now = current.Identity.CapturedTimestamp;
            service.Submit("normal", 2, current);
            var carried = service.Poll("normal", 2, current.Identity.WorldTime);
            Check(carried is { Status: MovementSearchStatus.Bounded, Bound: MovementSearchBound.InputChanged }
                && carried.UsefulDestination == plan.UsefulDestination && carried.Input == plan.Input,
                "Input churn preserves an exactly revalidated alternative without claiming a completed replacement search or refreshing its age.");
            now += Stopwatch.Frequency;
            Check(service.Poll("normal", 2, current.Identity.WorldTime) == null, "A carried route retains the original freshness ceiling.");
            now = current.Identity.CapturedTimestamp;
            map.PixelMaxG[1] = 0;
            var blocked = Context(map, 0);
            service.Submit("normal", 3, blocked);
            Check(service.Poll("normal", 3, blocked.Identity.WorldTime) == null,
                "A newly blocked route invalidates the carried alternative immediately.");
            service.Release("normal");
        }
        finally
        {
            finish.Set();
            Check(completed.Wait(1000), "Cancelled test worker exits before its fixtures are disposed.");
        }
        UtilityTimingChurn();
        PlannerContinuity();
    }

    private static void UtilityTimingChurn()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 1;
        var identity = Context(map, 0).Identity;
        MovementPlanningContext Input(float actionLock, bool blocked = false)
        {
            if (blocked) map.PixelMaxG[1] = -1;
            return new(identity, map, 0, 0, [true, true, true], [0, 0, 0], [], [], 1,
                utilities: [new(7546, PlanningUtilityEffect.IgnorePositional, 10, actionLock, 0, 0)]);
        }
        var original = Input(0.6f);
        var result = JointMovementSearch.Search(original, Exhaustive);
        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        CancellationToken observed = default;
        using var service = new MovementPlanningService((context, cancel, publish) =>
        {
            if (!ReferenceEquals(context, original)) return JointMovementSearch.Search(context, Exhaustive);
            observed = cancel;
            started.Set();
            finish.Wait();
            exited.Set();
            return result;
        }, () => identity.CapturedTimestamp);
        service.Submit("normal", 1, original);
        Check(started.Wait(1000), "Timing-churn worker started.");
        try
        {
            var refreshed = Input(0.601f);
            Check(!refreshed.SameProblem(original), "The changed lock estimate remains a changed planning input.");
            service.Submit("normal", 2, refreshed);
            Check(!observed.IsCancellationRequested,
                "A utility lock estimate does not cancel a worker before it can produce a fully revalidated route.");
            finish.Set();
            Check(exited.Wait(1000), "Timing-churn worker completed.");
            MovementPlan? carried = null;
            Check(SpinWait.SpinUntil(() => (carried = service.Poll("normal", 2, identity.WorldTime)) != null, 1000)
                && carried!.Input == result.Input && carried.Status == MovementSearchStatus.Bounded,
                "Completed work survives timing-only revisions with original timestamps and honest bounded status.");
            service.Submit("normal", 3, Input(0.602f, blocked: true));
            Check(service.Poll("normal", 3, identity.WorldTime)?.UsefulDestination != result.UsefulDestination,
                "A real new hazard still invalidates a route under timing churn.");
            service.Release("normal");
        }
        finally { finish.Set(); }
    }
}
