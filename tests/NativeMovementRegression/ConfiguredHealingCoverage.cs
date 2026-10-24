using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void ConfiguredHealingCoverage()
    {
        Check(NativeHealingActions.Supported((uint)BossMod.SGE.AID.Diagnosis) && NativeHealingActions.Supported((uint)BossMod.SGE.AID.Ixochole)
            && NativeHealingActions.Supported((uint)BossMod.AST.AID.Benefic) && NativeHealingActions.Supported((uint)BossMod.WHM.AID.MedicaIII)
            && !NativeHealingActions.Supported((uint)BossMod.SGE.AID.Kardia) && !NativeHealingActions.Supported((uint)BossMod.SGE.AID.Krasis)
            && !NativeHealingActions.Supported((uint)BossMod.SCH.AID.DeploymentTactics),
            "Actual healing identities include self/BeAttacked selectors and exclude friendly support buffs.");
        var map = Grid(5, 1);
        var identity = Context(map, 0).Identity;
        var demand = new PlanningHealingCoverage([
            new(20, Point(map, 4), 0, 0.2f, [new(120, 1.05f, HealingDemandKind.SingleSpell)]),
            new(21, Point(map, 2), 0, 0.55f, [new(121, 1.05f, HealingDemandKind.AreaAbility)])]);
        MovementPlanningContext Input(PlanningHealingCoverage? coverage)
            => new(identity, map, 0, 0, [true, true, true, true, true], [100, 0, 0, 0, 0], [], [], healingCoverage: coverage);
        var context = Input(demand);
        var alternativeCategories = new PlanningHealingCoverage([
            new(20, Point(map, 4), 0, 0.2f, [new(120, 1.05f, HealingDemandKind.SingleSpell), new(121, 5, HealingDemandKind.AreaAbility)])]);
        Check(alternativeCategories.Satisfied(identity.Origin),
            "An eligible heal already covering the recipient does not force movement into every other healing category's smaller radius.");
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.UsefulDestination == Point(map, 3) && demand.Satisfied(plan.UsefulDestination) && plan.DeliveredActions == 0,
            "Configured healing need restores access to both out-of-range recipients without inventing a selected heal or claiming its delivery.");
        Check(plan.HealingCoverage == demand && plan.Stages.All(s => s.Kind == MovementStageKind.Walk),
            "Each healing requirement remains identifiable in one native movement plan.");
        Check(MovementPlanValidation.Validate(plan, context, 0, true, identity.CapturedTimestamp).Allowed,
            "Configured healing recovery uses the same complete native route validation.");
        var noDemand = JointMovementSearch.Search(Input(null), Exhaustive);
        Check(noDemand.UsefulDestination == identity.Origin,
            "No configured demand means no fabricated low-health movement objective.");
        map.PixelMaxG[2] = -1;
        var disconnected = JointMovementSearch.Search(Input(demand), Exhaustive);
        Check(disconnected.UsefulDestination != plan.UsefulDestination && !demand.Satisfied(disconnected.UsefulDestination),
            "Healing need cannot outweigh an impassable route or conceal unreachable recipients.");
        var urgent = new PlanningHealingCoverage([
            new(20, Point(map, 4), 0, 0.2f, [new(120, 0.1f, HealingDemandKind.SingleAbility)]),
            new(21, Point(map, 0), 0, 0.8f, [new(121, 0.1f, HealingDemandKind.AreaSpell)]),
            new(22, Point(map, 0), 0, 0.8f, [new(121, 0.1f, HealingDemandKind.AreaSpell)])]);
        Check(urgent.Compare(Point(map, 4), Point(map, 0)) > 0,
            "Additional less-urgent recipients cannot purchase the loss of the most urgent configured healing requirement.");
        var movedRecipient = new PlanningHealingCoverage([
            demand.Requirements[0] with { Position = Point(map, 0) }, demand.Requirements[1]]);
        Check(!movedRecipient.Preserves(demand, plan.UsefulDestination, plan.UsefulDestination),
            "A recipient leaving the promised coverage invalidates that requirement rather than hiding it in a total.");
    }
}
