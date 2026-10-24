using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void RedMageComboRecovery()
    {
        var map = Grid(7, 1);
        var identity = Context(map, 0).Identity;
        var filler = Action(map, 12, 0, new ulong[7], [1, 1, 1, 1, 1, 1, 1]);
        var recovery = new PlanningComboRecovery(10, Point(map, 6), 1, 6);
        MovementPlanningContext Capture(PlanningComboRecovery? requirement, PlanningAction? action = null, bool mobility = false)
            => new(identity, map, 0, 0, Enumerable.Repeat(true, 7).ToArray(), new float[7], [action ?? filler], [],
                mobility ? 1ul : 0, comboRecovery: requirement,
                mobilityMechanics: mobility ? [new(7, MobilityMechanic.Ground, 10, 0, 0, 0, 0.1f, 0)] : []);
        var context = Capture(recovery);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.DeliveredActions == 1 && plan.DeliveryTimes[0] == 0 && recovery.Useful(plan.UsefulDestination, plan.DurationSeconds),
            "A current ranged filler does not cancel an evidenced melee-combo recovery, and is delivered before recovery movement.");
        Check(!MovementPlanValidation.Validate(plan, Capture(null), 0, true, identity.CapturedTimestamp).Allowed,
            "Loss of the observed combo immediately invalidates a route committed for its recovery.");
        Check(!MovementPlanValidation.Validate(plan, Capture(recovery with { EndsIn = 0.2f }), 0, true, identity.CapturedTimestamp).Allowed,
            "A shorter actual combo window cannot preserve an old recovery promise.");
        Check(JointMovementSearch.Search(Capture(null), Exhaustive).UsefulDestination == identity.Origin,
            "Without the saved combo-recovery preference, no extra preparation walk is requested.");
        var expiring = recovery with { EndsIn = 2 };
        var tooLate = JointMovementSearch.Search(Capture(expiring), Exhaustive);
        Check(tooLate.UsefulDestination == identity.Origin,
            "An approach that cannot regain range before native combo expiry has no invented execution benefit.");
        var rescue = JointMovementSearch.Search(Capture(expiring, mobility: true), Exhaustive);
        Check(expiring.Useful(rescue.UsefulDestination, rescue.DurationSeconds)
            && rescue.Stages.Any(s => s.Kind == MovementStageKind.GroundMovement),
            "A legal movement action may recover an observed combo that walking would lose.");
        var melee = filler with { ValueByCell = [0, 0, 0, 0, 0, 1, 1] };
        Check(JointMovementSearch.Search(Capture(null, melee), Exhaustive).UsefulDestination == Point(map, 5),
            "The optional anticipation switch does not disable native range execution for an already selected melee action.");
        var later = recovery with { EndsIn = 5.9f };
        Check(later.Same(recovery, 0.1f) && !context.SameProblem(Capture(null)),
            "Combo timing ages without renewal and disappearing combo evidence materially changes planning.");
        map.PixelPriority[2] = 2;
        Check(JointMovementSearch.Search(Capture(recovery), Exhaustive).UsefulDestination == Point(map, 2),
            "An optional combo recovery cannot reinterpret or overpower native goal priorities.");
        map.PixelPriority[2] = 0;
        map.PixelMaxG[4] = 0;
        var obstructed = JointMovementSearch.Search(Capture(recovery), Exhaustive);
        Check(obstructed.UsefulDestination == identity.Origin,
            "An unreachable combo range never authorizes crossing a native obstruction.");
    }
}
