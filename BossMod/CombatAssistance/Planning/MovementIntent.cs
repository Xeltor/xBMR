namespace BossMod.CombatAssistance.Planning;

// Owner instructions are captured before search. They never rewrite a selected
// waypoint. Explicit destinations continue to use their existing native authority.
internal sealed record MovementIntent(float MoveDelay = 0, float DodgeDelay = 0, bool SeparateDodgeDelay = false,
    bool InterruptCast = false, PlanningRange? Range = null, WPos? ExplicitDestination = null, float ArrivalDeadline = float.MaxValue);

internal sealed record PlanningRange(ulong TargetId, WPos Center, float Radius, bool RecoverExecution = false, float RecoveryReadyIn = 0)
{
    internal bool Valid => TargetId != 0 && float.IsFinite(Center.X) && float.IsFinite(Center.Z)
        && float.IsFinite(Radius) && Radius >= 0 && float.IsFinite(RecoveryReadyIn) && RecoveryReadyIn >= 0;
    internal float Loss(WPos position) => Math.Max(0, (position - Center).Length() - Radius);
    internal bool NeedsRecovery(WPos origin) => RecoverExecution && Loss(origin) > MovementPlanValidation.ArrivalTolerance;
    internal bool Same(PlanningRange other, float age) => TargetId == other.TargetId && Center == other.Center
        && Radius == other.Radius && RecoverExecution == other.RecoverExecution
        && Math.Abs(RecoveryReadyIn - Math.Max(0, other.RecoveryReadyIn - age)) <= .01f;
}
