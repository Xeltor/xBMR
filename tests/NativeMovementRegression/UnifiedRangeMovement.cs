using BossMod;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private static void UnifiedRangeMovement()
    {
        var map = Grid(17, 3);
        var baseline = Context(map, 17, speed: 6);
        var target = Point(map, 32);
        MovementPlanningContext Input(ulong resources, bool healing = false, bool blocked = false)
        {
            for (var row = 0; row < 3; ++row) map.PixelMaxG[row * 17 + 8] = blocked ? -1 : float.MaxValue;
            return new(baseline.Identity, map, 0, 0, Enumerable.Repeat(true, 51).ToArray(), new float[51], [], [], resources,
                range: healing ? new(10, baseline.Origin, 25) : new(10, target, 2, RecoverExecution: true),
                partyContact: null,
                healingCoverage: healing ? new([new(20, target, 0, .2f, [new(100, 2)])]) : null,
                mobilityMechanics: [new(155, MobilityMechanic.Target, 25, 0, 0, 0, .4f, 0, [new(20, target, 0)]),
                    new(94, MobilityMechanic.Directional, 0, -10, 0, .1f, .2f, 1)],
                utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, 0, 2, 9)]);
        }
        MovementPlan Search(MovementPlanningContext context, bool spatial = false)
            => JointMovementSearch.Search(context, Exhaustive, includeSpatialMobilityActions: spatial,
                stopAtCurrentOpportunity: true);
        foreach (var healing in new[] { false, true })
        {
            var walk = Search(Input(0, healing));
            var sprint = Search(Input(0x100, healing));
            var reverse = Search(Input(0x10, healing));
            var dash = Search(Input(1, healing));
            var allInput = Input(0x111, healing);
            var all = Search(allInput);
            Check(walk.HasVerifiedRoute && walk.Stages.All(s => s.Kind == MovementStageKind.Walk)
                && sprint.Stages.Any(s => s.ActionId == 3) && sprint.DurationSeconds < walk.DurationSeconds
                && reverse.Stages.Any(s => s.ActionId == 94) && reverse.DurationSeconds < sprint.DurationSeconds
                && dash.Stages.Any(s => s.ActionId == 155) && dash.DurationSeconds < reverse.DurationSeconds,
                $"{(healing ? "Healing" : "Combat")} reach compares walking, Sprint, reverse-jump-forward and targeted gap closing in the first opportunity pass.");
            Check(all.Stages.Any(s => s.ActionId == 155)
                && MovementPlanValidation.Validate(all, allInput, 0, true, allInput.Identity.CapturedTimestamp).Allowed,
                "All enabled movement options compete on complete safe arrival rather than preserving the walking mode.");
            var reverseStage = reverse.Stages.First(s => s.ActionId == 94);
            Check(reverseStage.To.X > reverseStage.From.X && reverseStage.Facing!.Value.ToDirection().X < 0,
                "Backward displacement faces away and travels forward, including toward a healing recipient.");
            var exhaustive = Search(allInput, true);
            Check(PlanningMovementValue.Compare(all.DurationSeconds, exhaustive.DurationSeconds) == 0,
                "Finite approach choices and full spatial enumeration agree on the useful arrival.");
            Check(NativeMovementHost.MobilityImprovesWalking(walk, 0, all, allInput, allInput.Identity.CapturedTimestamp),
                "A better fresh mobility alternative can replace a still-valid active walking approach.");
            Check(!NativeMovementHost.MobilityImprovesWalking(all, 0, walk, allInput, allInput.Identity.CapturedTimestamp)
                && !NativeMovementHost.MobilityImprovesWalking(walk, 0, all, allInput,
                    allInput.Identity.CapturedTimestamp + Stopwatch.Frequency),
                "A slower walk or stale mobility alternative cannot evict the current route.");
            var blocked = Input(0x111, healing, true);
            var rejected = Search(blocked);
            Check(rejected.UsefulDestination.X < Point(map, 8).X
                && !MovementPlanValidation.Validate(all, blocked, 0, true, blocked.Identity.CapturedTimestamp).Allowed,
                "A fast option cannot buy passage through a newly obstructed route.");
        }
        var contact = new MovementPlanningContext(baseline.Identity, map, 0, 0,
            Enumerable.Repeat(true, 51).ToArray(), new float[51], [], [], 1,
            partyContact: new(20, target, 2),
            mobilityMechanics: [new(94, MobilityMechanic.Directional, 0, -10, 0, .1f, .2f, 0)]);
        // Input() left a blocking wall: contact must retain the same native safety.
        Check(Search(contact).Stages.All(s => s.ActionId != 94), "Party-contact reverse jumps also respect blocked geometry.");
        for (var row = 0; row < 3; ++row) map.PixelMaxG[row * 17 + 8] = float.MaxValue;
        contact = new(contact.Identity, map, 0, 0, Enumerable.Repeat(true, 51).ToArray(), new float[51], [], [], 1,
            partyContact: contact.PartyContact, mobilityMechanics: contact.MobilityMechanics);
        Check(Search(contact).Stages.Any(s => s.ActionId == 94),
            "Party contact gets a deliberate reverse-jump-forward alternative before full spatial enumeration.");
        for (var row = 0; row < 3; ++row)
            for (var x = 0; x < 8; ++x) map.PixelMaxG[row * 17 + x] = .5f;
        var escape = Input(0x111);
        var safe = Search(escape);
        Check(safe.HasVerifiedRoute && safe.Stages.Any(s => s.ActionId is 94 or 155)
            && MovementPlanValidation.Validate(safe, escape, 0, true, escape.Identity.CapturedTimestamp).Allowed,
            "The same movement collection uses mobility to clear an activation deadline that walking cannot meet.");
        Check(!Search(Input(0)).HasVerifiedRoute,
            "An insufficient walking route is never accepted just because it eventually reaches combat range.");
        var shortMap = Grid(3, 1);
        var shortBase = Context(shortMap, 0, speed: 6);
        var shortHeal = new MovementPlanningContext(shortBase.Identity, shortMap, 0, 0,
            [true, true, true], new float[3], [], [], 1,
            healingCoverage: new([new(20, Point(shortMap, 2), 0, .2f, [new(100, 1)])]),
            utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, 0, 0, 9)]);
        Check(Search(shortHeal).Stages.All(s => s.ActionId != 3),
            "Healing access includes Sprint's remaining action lock; a short walk can deliver the heal sooner.");
        var combatClock = new MovementPlanningContext(shortHeal.Identity, shortMap, 0, 0,
            [true, true, true], new float[3], [], [], 1,
            range: new(10, shortBase.Origin, 25, RecoverExecution: true),
            healingCoverage: shortHeal.HealingCoverage,
            mobilityMechanics: [new(155, MobilityMechanic.Target, 25, 0, 0, 0, .01f, 0, [new(20, Point(shortMap, 2), 0)])]);
        Check(Search(combatClock).Stages.Any(s => s.ActionId == 155),
            "Already-satisfied offensive readiness cannot prune a faster arrival for an injured healing recipient.");
    }
}
