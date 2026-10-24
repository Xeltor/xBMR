using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.UI;

public static partial class Checks
{
    private static void SelectedPartySequence()
    {
        Check(NativeActionSequence.IsImmediatePartyHeal(3583) && NativeActionSequence.IsImmediatePartyHeal(24299)
            && !NativeActionSequence.IsImmediatePartyHeal(3573) && !NativeActionSequence.IsImmediatePartyHeal(34675)
            && !NativeActionSequence.IsImmediatePartyHeal(3571),
            "Known direct party-heal semantics exclude self-only fields and mixed damage/healing effects without explicit recipient evidence.");
        var map = Grid(4, 1);
        var heal = Action(map, 2, 1, [1, 1, 0, 0], [1, 2, 0, 0]);
        var gcd = Action(map, 4, 2, [0, 0, 0, 2], [0, 0, 0, 1], earliest: 3) with { Id = 2, TargetId = 20 };
        var sequence = NativeActionSequence.Compose(heal, gcd);
        Check(sequence.Length == 2 && sequence[0] == heal && sequence[1].RequiredEarlierActions == 1,
            "The actually selected immediate party ability retains priority before the evidenced GCD.");
        var input = Context(map, 0, sequence.ToArray());
        var plan = JointMovementSearch.Search(input, Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(plan.DeliveredActions == 3 && plan.DeliveryTimes[0] == 0 && plan.DeliveryTimes[1] == 3
            && plan.UsefulDestination == Point(map, 3),
            "The preliminary walking plan preserves the current heal, then reaches the useful GCD destination in the same publication.");
        Check(plan.ExecutionValues[0] == 1,
            "An extra healing recipient cannot purchase a delay of the currently selected immediate heal.");
        Check(NativePlanOverlay.Capture(plan, null, 0, [], 0).Markers.Any(m => m.Label == "Action stop"),
            "An intermediate action position is visible with the existing marker primitive instead of duplicating the final destination.");
        var partial = plan with { DeliveredActions = 1, Status = MovementSearchStatus.Searching };
        Check(NativeMovementHost.CommitCandidate(partial, input) == null,
            "A publication cannot commit after evaluating only the first action in the evidenced sequence.");
        Check(NativeMovementHost.CommitCandidate(plan, input)?.DeliveredActions == 3,
            "One complete native publication includes both current opportunities.");
        Check(NativeActionSequence.Compose(null, gcd).SequenceEqual([gcd])
            && NativeActionSequence.Compose(heal, null).SequenceEqual([heal])
            && NativeActionSequence.Compose(heal, heal).SequenceEqual([heal]),
            "Missing or repeated action evidence never invents a second rotation delivery.");
        Check(NativeActionSequence.CompleteMask(0) == 0 && NativeActionSequence.CompleteMask(8) == 255,
            "The complete publication mask supports the full existing evidenced-action model.");
    }
}
