using BossMod.Integration;

static partial class Checks
{
    private static void InputObservations()
    {
        var inputs = new IntegrationInputObservation();
        Check(inputs.Latest == null, "input observation is unavailable before a real read");
        inputs.Assign("instance", 5, new(1, 0, 0));
        inputs.Observe("walk-input-result", new(1, 0, 0), Vector2.Zero, Vector2.UnitX, Vector2.UnitX, false, false);
        var first = inputs.Latest!;
        Check(first.Generation == 1 && first.MatchingAssignment?.FrameId == 5 && first.UserDirection == Vector2.Zero,
            "real input sample correlates the preceding matching native assignment without inventing manual input");
        inputs.Assign("instance", 6, new(0, 0, 1));
        Check(ReferenceEquals(inputs.Latest, first) && first.MatchingAssignment?.FrameId == 5, "new assignment cannot relabel an old input read as current");
        inputs.Observe("walk-input-result", new(9, 0, 0), Vector2.UnitY, Vector2.UnitY, Vector2.UnitY, true, false);
        Check(inputs.Latest!.Generation == 2 && inputs.Latest.MatchingAssignment == null && inputs.Latest.FollowPath,
            "changed direction/manual-follow evidence does not retain a mismatched native assignment");
        Check(first.Generation == 1 && first.ActualDirection == Vector2.UnitX, "published input samples remain immutable");
        inputs.Observe("walk-input-result", new(0, 0, 1), Vector2.Zero, Vector2.Zero, Vector2.Zero, false, true);
        Check(inputs.Latest!.Misdirection && inputs.Latest.OutputDirection == Vector2.Zero && inputs.Latest.MatchingAssignment?.FrameId == 6,
            "misdirection suppression is an observed result rather than movement inferred from the assigned direction");
    }
}
