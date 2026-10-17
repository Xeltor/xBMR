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
        inputs.AssignNative("instance", 7, Vector3.UnitX, "native-hints", "decision", "bmr-follow", 12);
        var captured = inputs.CurrentAssignment!;
        inputs.Observe("walk-input-result", Vector3.UnitX, Vector2.Zero, Vector2.UnitX, Vector2.UnitX, false, false,
            automatic: true, capturedAssignment: captured, requireCapturedAssignment: true);
        var attributed = inputs.Latest!;
        Check(attributed.Automatic && attributed.MatchingAssignment?.ControllerDecisionId == "decision"
            && attributed.MatchingAssignment.ControllerNavigationGeneration == 12, "input carries exact native controller and source identity");
        inputs.AssignNative("instance", 7, Vector3.UnitX, "native-interaction-suppression");
        Check(inputs.CurrentAssignment!.Generation > captured.Generation && inputs.CurrentAssignment.ControllerDecisionId == null,
            "same-value native reassignments have distinct identities and cannot inherit an old controller source");
        inputs.Observe("walk-input-result", Vector3.UnitX, Vector2.Zero, Vector2.UnitX, Vector2.UnitX, false, false,
            automatic: true, capturedAssignment: captured, requireCapturedAssignment: true);
        Check(inputs.Latest!.MatchingAssignment == null, "assignment changed during input read cannot be relabeled from its new same-value assignment");
        inputs.Observe("walk-input-result", Vector3.UnitX, Vector2.Zero, Vector2.UnitX, Vector2.UnitX, false, false,
            automatic: true, requireCapturedAssignment: true);
        Check(inputs.Latest!.MatchingAssignment == null, "an input read begun without an assignment cannot borrow a later one");
        Check(attributed.MatchingAssignment == captured && attributed.MatchingAssignment.Reason == "native-hints",
            "retained input observations keep their original immutable assignment identity");
        var decision = new IntegrationControllerDecision("decision", "instance", 7, "bmr-follow", 42, DateTime.UtcNow, 1,
            Vector3.Zero, Vector3.UnitX, Vector3.UnitX, true, "native-follow-destination", 12, 6, "instance", Vector2.UnitY);
        Check(decision.Matches("instance", 7, 42, Vector3.UnitX), "native corrected outcome matches its actual output even if old search destination differs");
        Check(!decision.Matches("other-instance", 7, 42, Vector3.UnitX) && !decision.Matches("instance", 8, 42, Vector3.UnitX)
            && !decision.Matches("instance", 7, 43, Vector3.UnitX) && !decision.Matches("instance", 7, 42, Vector3.UnitZ),
            "controller correlation requires exact provider/frame/actor/output");
        Check(!(decision with { WroteMovement = false }).Matches("instance", 7, 42, Vector3.UnitX),
            "a suppressed controller cannot claim another writer's identical direction");
    }
}
