using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void SequenceLimit()
    {
        var map = Grid(2, 1);
        var actions = Enumerable.Range(0, 8).Select(i => Action(map, 12, 1,
            i % 2 == 0 ? [1, 0] : [0, 1], i % 2 == 0 ? [1, 0] : [0, 1]) with
            { Id = (uint)(i + 1), RequiredEarlierActions = (byte)((1 << i) - 1) }).ToArray();
        var result = JointMovementSearch.Search(Context(map, 0, actions), Exhaustive);
        Check(result.HasVerifiedRoute && result.Status == MovementSearchStatus.Bounded
            && result.Bound == MovementSearchBound.SequenceLimit,
            "A useful sequence beyond the supported phase count must not be reported as an exhaustive search.");
        var ordinary = JointMovementSearch.Search(Context(map, 0), Exhaustive);
        Check(ordinary.Status == MovementSearchStatus.Complete && ordinary.Bound == MovementSearchBound.None,
            "Dominated movement loops do not spuriously exhaust the sequence limit.");
    }
}
