using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void WalkingApproach()
    {
        var map = Grid(40, 5);
        var baseline = Context(map, 80, speed: 6);
        var range = new PlanningRange(10, Point(map, 110), 2, RecoverExecution: true, RecoveryReadyIn: 2.5f);
        MovementPlanningContext Input(PlanningAction[]? actions = null, float unlock = 0, PlanningRange? combat = null, PlanningRange? contact = null)
            => new(baseline.Identity with { CapturedTimestamp = Stopwatch.GetTimestamp() }, map, 0, unlock,
                Enumerable.Repeat(true, 200).ToArray(), new float[200], actions ?? [], [], range: combat ?? range, partyContact: contact);
        var input = Input();
        var clock = Stopwatch.StartNew();
        MovementPlan? first = null;
        var plan = NativeWalkingApproach.Search(input, publish: p => first ??= p);
        Console.WriteLine($"Native 200-cell walking proposal: first/complete {clock.Elapsed.TotalMilliseconds:F3}ms; no mobility enumeration or live timing claim.");
        Check(first is { HasVerifiedRoute: true } && plan is { HasVerifiedRoute: true, Status: MovementSearchStatus.Bounded }
            && plan.Stages[0].Kind == MovementStageKind.Walk && plan.Stages.All(s => s.Kind == MovementStageKind.Walk)
            && range.Loss(plan.UsefulDestination) < range.Loss(input.Origin),
            "A complete safe walking proposal is published before the full mobility search, with honest bounded status.");
        Check(MovementPlanValidation.Validate(plan!, input, 0, true, input.Identity.CapturedTimestamp).Allowed,
            "The proposed walking corridor reuses full original-map route validation.");
        var cast = Action(map, 12, 1, Enumerable.Repeat(1ul, 200).ToArray(), Enumerable.Repeat(1f, 200).ToArray(), cast: 1.5f, earliest: 2.5f);
        var casting = Input([cast]);
        var movingCast = NativeWalkingApproach.Search(casting);
        Check(movingCast is { HasVerifiedRoute: true } && movingCast.DeliveredActions == 1
            && movingCast.DeliveryTimes[0] == 4 && movingCast.Stages[0].Kind == MovementStageKind.Walk,
            "An evidenced future cast keeps its exact delivery while readiness time is used for walking rather than standing still.");
        var locked = NativeWalkingApproach.Search(Input(unlock: .6f));
        Check(locked is { HasVerifiedRoute: true } && locked.Stages[0] is { Kind: MovementStageKind.Wait, EndSeconds: .6f },
            "An actually active cast or explicit movement delay remains a native unlock before approach.");
        var contact = new PlanningRange(20, Point(map, 110), 3);
        var healingInput = Input(combat: new(10, baseline.Origin, 25), contact: contact);
        Check(NativeWalkingApproach.Search(healingInput) is { HasVerifiedRoute: true } healing
            && contact.Loss(healing.UsefulDestination) < contact.Loss(healingInput.Origin),
            "Useful party/healing contact gets the same walking-first proposal even while offensive range is already satisfied.");
        for (var y = 0; y < 5; ++y) map.PixelMaxG[y * 40 + 15] = -1;
        var blocked = Input();
        var blockedPlan = NativeWalkingApproach.Search(blocked);
        Check(blockedPlan == null || blockedPlan.UsefulDestination.X < Point(map, 15).X,
            "Native route proposals cannot cross an impassable wall to chase range.");
        Check(!MovementPlanValidation.Validate(plan!, blocked, 0, true, blocked.Identity.CapturedTimestamp).Allowed,
            "A newly blocked route immediately invalidates a previously verified walking proposal.");

        var player = new ExecutionActor(1, new(0, 25), 0, .5f, default);
        ExecutionActor[] party = [new(2, new(-3, 0), 0, .5f, default), new(3, new(3, 0), 0, .5f, default), new(4, default, 0, .5f, default)];
        var coverage = NativeRadialCoverageRegion.Capture(player, 2, 15, party.Length, party, [], 0)!;
        Check(coverage.Loss(player.Position) > 0
            && party.All(a => (coverage.Center - a.Position).Length() + coverage.Radius < 15 + a.Radius),
            "An evidenced group-effect region retains every required recipient's footprint without imposing an artificial half-radius posture.");
    }
}
