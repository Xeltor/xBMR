using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void HealingDelivery()
    {
        var map = Grid(5, 1);
        var input = Context(map, 1);
        var origin = input.Origin;
        var shield = new PlanningAction((uint)BossMod.SGE.AID.EukrasianPrognosisII, 1, 1, 12, 0, .6f, 0, [], [],
            Opportunity: new(ExecutionShape.CasterCircle, 1, origin, 0, 1.1f,
                [new(2, Point(map, 2), 0, 1, true)]));
        var hold = new NativeHealingDelivery();
        var now = input.Identity.WorldTime;
        HealingDeliveryRequest? Update(float elapsed, PlanningAction? action = null, uint casting = 0, float remaining = 0)
            => hold.Update(now.AddSeconds(elapsed), 1, 1, Class.SGE, origin, action ?? shield, 2, casting, remaining);
        var pending = Update(0)!;
        Check(pending != null && NativeHealingDelivery.CanHold(input, pending),
            "A selected shield retains its usable safe pose through setup rather than returning before its native effect.");
        Check(Update(1)!.CompletesIn == 1 && Update(2.1f) == null && Update(2.2f) == null,
            "A stalled heal has one original completion deadline, never a timeout renewed by every frame.");
        hold.Reset();
        _ = Update(0);
        hold.Observe(7, shield.Id);
        Check(Update(.1f) != null, "Another healer's same spell does not complete this healer's delivery.");
        hold.Observe(1, (uint)BossMod.SGE.AID.Eukrasia);
        Check(Update(.2f) != null, "Eukrasia setup is not shield completion.");
        hold.Observe(1, shield.Id);
        Check(Update(.3f) == null && Update(.4f) == null,
            "An observed shield effect releases the pose without rearming on a lingering RSR selection.");
        hold.Reset();
        var castHeal = shield with { Id = (uint)BossMod.SGE.AID.Prognosis, CastSeconds = 2 };
        _ = Update(0, castHeal);
        Check(hold.Update(now.AddSeconds(2.1), 1, 1, Class.SGE, origin, null, 2, castHeal.Id, .3f)?.CompletesIn == .3f,
            "An in-progress heal remains protected through its full remaining cast, including the usual slidecast window.");
        Check(hold.Update(now.AddSeconds(2.2), 1, 1, Class.SGE, origin, null, 2) == null,
            "A cancelled cast with no current selection releases its delivery hold.");
        var outOfRange = shield with { Opportunity = new(ExecutionShape.CasterCircle, 1, origin, 0, .1f,
            [new(2, Point(map, 4), 0, 1, true)]) };
        Check(Update(0, outOfRange) == null, "An out-of-range shield does not stop its own approach.");
        Check(!NativeHealingDelivery.CanHold(input, pending with { Action = outOfRange }),
            "Fresh recipient geometry can reject a formerly usable healing pose.");
        map.PixelMaxG[1] = 10;
        Check(!NativeHealingDelivery.CanHold(Context(map, 1), pending),
            "A future hazard retains the complete native escape rather than granting a heal wait.");
        map.PixelMaxG[1] = float.MaxValue;
        MovementPlanningContext Constrained(PlanningExecutionRules? rules = null, WPos? required = null, PlanningRange? contact = null)
            => new(input.Identity, map, 0, 0, [true, true, true, true, true], [0, 0, 0, 0, 0], [], [],
                executionRules: rules, requiredDestination: required, partyContact: contact);
        Check(!NativeHealingDelivery.CanHold(Constrained(new(float.MaxValue, 1, [])), pending)
            && !NativeHealingDelivery.CanHold(Constrained(required: Point(map, 4)), pending)
            && !NativeHealingDelivery.CanHold(Constrained(contact: new(2, Point(map, 4), .1f)), pending),
            "Action prohibitions, explicit destinations and independent party contact still override the elective healing hold.");
        Check(RsrActionEvidenceSource.IsSageShield(shield.Id)
            && !RsrActionEvidenceSource.IsSageShield((uint)BossMod.SGE.AID.EukrasianDyskrasia)
            && !RsrActionEvidenceSource.IsSageShield((uint)BossMod.SGE.AID.EukrasianDosisIII),
            "Only RSR's selected Eukrasian shield aim retains healing position; offensive setup does not.");
    }
}
