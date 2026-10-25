using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;
using System.Threading;

public static partial class Checks
{
    private static void PlannerContinuity()
    {
        var map = Grid(31, 3);
        var goal = Point(map, 59);
        var current = Context(map, 31, committed: goal, speed: 6);
        var direct = JointMovementSearch.Search(current, Exhaustive with { Expansions = 1 });
        Check(direct.UsefulDestination == goal && direct.Stages.Length == 1
            && MovementPlanValidation.Validate(direct, current, 0, true, current.Identity.CapturedTimestamp).Allowed,
            "An open committed walk produces a complete timed continuation in its first expansion, without exploring every intervening cell.");
        var executor = new MovementPlanExecutor();
        Check(executor.Advance(direct, current, new(default), true, current.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "The direct committed route executes as one decisive walking directive.");
        var blockedMap = Grid(31, 3);
        blockedMap.PixelMaxG[45] = -1;
        var blocked = Context(blockedMap, 31, committed: goal, speed: 6);
        Check(JointMovementSearch.Search(blocked, Exhaustive with { Expansions = 1 }).UsefulDestination != goal,
            "The continuation shortcut cannot cross a blocked corner or obstacle.");
        var detour = JointMovementSearch.Search(blocked, Exhaustive);
        Check(detour.UsefulDestination == goal && detour.Stages.Length > 1
            && MovementPlanValidation.Validate(detour, blocked, 0, true, blocked.Identity.CapturedTimestamp).Allowed,
            "Ordinary cell search still supplies a complete safe detour when the committed chord is blocked.");
        Check(NativeMovementHost.CommitCandidate(direct, blocked, blocked.Identity.CapturedTimestamp) == null,
            "A newly obstructed worker replacement is rejected before it can evict the ongoing route.");
        Check(NativeMovementHost.CommitCandidate(direct, current, current.Identity.CapturedTimestamp + Stopwatch.Frequency) == null,
            "A stale completed replacement cannot evict a freshly checked ongoing route.");
        Check(executor.Advance(direct, current, new(default), true, current.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "Rejecting a replacement leaves the original execution stage and commitment usable.");
        Check(executor.Advance(direct, blocked, new(default), true, blocked.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "The retained route itself still yields immediately to new hazards.");
        CompletedSearchCoalescing();
        MobilityReadinessChurn();
        UsedMobilityReadiness();
        CommittedWalkingIntent();
    }

    private static void CommittedWalkingIntent()
    {
        var map = Grid(9, 3);
        var baseline = Context(map, 9, speed: 6);
        var goal = Point(map, 17);
        var range = new PlanningRange(10, goal, .1f, RecoverExecution: true);
        var action = Action(map, 4, 1, Enumerable.Range(0, 27).Select(i => i == 17 ? 1ul : 0).ToArray(),
            Enumerable.Range(0, 27).Select(i => i == 17 ? 1f : 0).ToArray());
        MovementPlanningContext Input(float age = 0, PlanningAction[]? actions = null, ulong target = 10)
            => new(baseline.Identity with { Origin = age > 0 ? Point(map, 10) : baseline.Origin,
                WorldTime = baseline.Identity.WorldTime.AddSeconds(age),
                CapturedTimestamp = baseline.Identity.CapturedTimestamp + (long)(age * Stopwatch.Frequency) },
                map, 0, 0, Enumerable.Repeat(true, 27).ToArray(), new float[27], actions ?? [], [],
                committedDestination: goal, targetId: target, range: range);
        var original = Input(actions: [action]);
        var plan = JointMovementSearch.Search(original, Exhaustive);
        var executor = new MovementPlanExecutor();
        Check(plan.Stages[0].Kind == MovementStageKind.Walk && plan.DeliveredActions == 1
            && executor.Advance(plan, original, new(default), true, original.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "The commitment fixture starts an actual approach with an evidenced action at its destination.");
        Check(executor.CanContinueWalking(plan, original, original.Identity.CapturedTimestamp),
            "A freshly valid active approach keeps execution priority over replacement optimization results.");
        var next = Input(.3f);
        Check(!MovementPlanValidation.Validate(plan, next, 0, true, next.Identity.CapturedTimestamp,
            continuingCommittedRoute: true).Allowed, "A vanished rotation action really invalidates the old combined plan.");
        var continued = NativeMovementHost.ContinueWalkingWithoutAction(plan, next, 0, next.Identity.CapturedTimestamp);
        Check(continued is { HasVerifiedRoute: true, DeliveredActions: 0 } && continued.ActionEvidence.IsEmpty
            && continued.UsefulDestination == plan.UsefulDestination && continued.Stages.All(s => s.ActionId == 0)
            && executor.Advance(continued, next, new(default), true, next.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Walk,
            "An empty rotation-selection frame keeps the original safe approach, without claiming an obsolete action or renewing its deadline.");
        Check(!executor.CanContinueWalking(continued!, Input(.3f, [action]), next.Identity.CapturedTimestamp),
            "A newly selected current action must be included and revalidated before a walking-only continuation may retain priority.");
        Check(NativeMovementHost.ContinueWalkingWithoutAction(plan, Input(.3f, target: 11), 0) == null,
            "A different combat target cannot inherit the old approach.");
        map.PixelPriority[26] = 1;
        var assigned = Input(.3f);
        Check(!executor.CanContinueWalking(continued!, assigned, assigned.Identity.CapturedTimestamp)
            && NativeMovementHost.ContinueWalkingWithoutAction(plan, assigned, 0) == null,
            "A new higher native assignment takes precedence over an otherwise safe ongoing approach.");
        map.PixelPriority[26] = 0;
        map.PixelMaxG[13] = -1;
        var blocked = Input(.3f);
        Check(!executor.CanContinueWalking(continued!, blocked, blocked.Identity.CapturedTimestamp)
            && NativeMovementHost.ContinueWalkingWithoutAction(plan, blocked, 0) == null,
            "An obstruction in the actual remaining corridor immediately cancels both forms of walking commitment.");
        Check(executor.Advance(continued!, next, new(default), false, next.Identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "Committed walking still yields immediately to manual or external control.");
    }

    private static void CompletedSearchCoalescing()
    {
        var map = Grid(3, 1);
        var identity = Context(map, 0).Identity;
        var now = identity.CapturedTimestamp;
        var searches = 0;
        MovementPlanningContext Input(double age) => new(identity with
        {
            CapturedTimestamp = identity.CapturedTimestamp + (long)(age * Stopwatch.Frequency),
            WorldTime = identity.WorldTime.AddSeconds(age)
        }, map, 0, 0, [true, true, true], [0, 0, 0], [], []);
        using var service = new MovementPlanningService((context, cancel) =>
        {
            Interlocked.Increment(ref searches);
            return JointMovementSearch.Search(context, Exhaustive, cancel);
        }, () => now);
        service.Submit("normal", 1, Input(0));
        Check(SpinWait.SpinUntil(() => service.Poll("normal", 1, identity.WorldTime) is { Status: MovementSearchStatus.Complete }, 1000),
            "The equivalent-input fixture has a completed result.");
        for (var frame = 1; frame <= 100; ++frame)
        {
            var age = frame * .002;
            now = identity.CapturedTimestamp + (long)(age * Stopwatch.Frequency);
            var context = Input(age);
            service.Submit("normal", 1, context);
            Check(service.Poll("normal", 1, context.Identity.WorldTime)?.Input == identity,
                "Equivalent frames consume the original completed result without renewing its proof age.");
        }
        Check(Volatile.Read(ref searches) == 1, "One hundred fresh equivalent observations do not launch one hundred repeat searches.");
        now = identity.CapturedTimestamp + Stopwatch.Frequency / 4;
        var next = Input(.25);
        service.Submit("normal", 1, next);
        var atExpiry = service.Poll("normal", 1, next.Identity.WorldTime);
        Check(atExpiry == null || atExpiry.Input == next.Identity,
            "Expiry can return a newly finished search, but cannot extend the original proof's freshness clock.");
        Check(SpinWait.SpinUntil(() => service.Poll("normal", 1, next.Identity.WorldTime)?.Input == next.Identity, 1000)
            && Volatile.Read(ref searches) == 2, "Expired equivalent input starts a fresh search.");
        service.Submit("normal", 2, Input(.25));
        Check(SpinWait.SpinUntil(() => Volatile.Read(ref searches) == 3, 1000),
            "A changed revision bypasses completed-search coalescing immediately.");
    }

    private static void MobilityReadinessChurn()
    {
        var map = Grid(3, 1);
        map.PixelPriority[2] = 1;
        var identity = Context(map, 0).Identity;
        MovementPlanningContext Input(float ready, bool obstructed = false)
        {
            map.PixelMaxG[1] = obstructed ? -1 : float.MaxValue;
            return new(identity, map, 0, 0, [true, true, true], [0, 0, 0], [], [], 1,
                mobilityMechanics: [new(24295, MobilityMechanic.Target, 20, 0, .5f, 0, .6f, 0,
                    [new(10, Point(map, 2), .5f)], readyIn: ready)]);
        }
        var original = Input(.1f);
        var plan = JointMovementSearch.Search(original, Exhaustive);
        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        CancellationToken observed = default;
        using var service = new MovementPlanningService((context, cancel, publish) =>
        {
            observed = cancel;
            started.Set();
            finish.Wait();
            return plan;
        }, () => identity.CapturedTimestamp);
        service.Submit("normal", 1, original);
        Check(started.Wait(1000), "Mobility readiness fixture starts its worker.");
        try
        {
            var next = Input(.3f);
            Check(!next.SameProblem(original) && next.SameProblem(original, true, forRevalidation: true),
                "Changed readiness remains a new optimization problem, while allowing a result to undergo exact current revalidation.");
            service.Submit("normal", 2, next);
            Check(!observed.IsCancellationRequested, "An unused mobility lock estimate does not starve a useful walking search.");
            finish.Set();
            Check(SpinWait.SpinUntil(() => service.Poll("normal", 2, identity.WorldTime)?.HasVerifiedRoute == true, 1000),
                "A completed alternative survives readiness churn only after current full-route validation.");
            service.Submit("normal", 3, Input(.4f, true));
            Check(service.Poll("normal", 3, identity.WorldTime) == null,
                "Readiness coalescing never carries a route through a new obstruction.");
        }
        finally { finish.Set(); }
    }

    private static void UsedMobilityReadiness()
    {
        var map = Grid(7, 1);
        var identity = Context(map, 0).Identity;
        var attack = Action(map, .8f, 1, [0, 0, 0, 0, 0, 0, 1], [0, 0, 0, 0, 0, 0, 1]);
        MovementPlanningContext Input(float ready) => new(identity, map, 0, 0,
            Enumerable.Repeat(true, 7).ToArray(), new float[7], [attack], [], 1,
            mobilityMechanics: [new(24295, MobilityMechanic.Target, 20, 0, 0, 0, .1f, 0,
                [new(10, Point(map, 6), 0)], readyIn: ready)]);
        var original = Input(0);
        var dash = JointMovementSearch.Search(original, Exhaustive);
        Check(dash.DeliveredActions == 1 && dash.Stages.Any(s => s.Kind == MovementStageKind.TargetDash),
            "The readiness safety fixture requires a real dash to preserve its current attack.");
        var delayed = Input(1);
        Check(delayed.SameProblem(original, true, forRevalidation: true)
            && !MovementPlanValidation.Validate(dash, delayed, 0, true, identity.CapturedTimestamp).Allowed
            && NativeMovementHost.CommitCandidate(dash, delayed, identity.CapturedTimestamp) == null,
            "Letting a worker finish across readiness changes cannot execute a dash that now misses the original attack deadline.");
    }
}
