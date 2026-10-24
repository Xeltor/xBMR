namespace BossMod.CombatAssistance.Planning;

// Owner instructions are captured before search. They never rewrite a selected
// waypoint. Explicit destinations continue to use their existing native authority.
internal sealed record MovementIntent(float MoveDelay = 0, float DodgeDelay = 0, bool SeparateDodgeDelay = false,
    bool InterruptCast = false, PlanningRange? Range = null, WPos? ExplicitDestination = null, float ArrivalDeadline = float.MaxValue);

internal sealed record PlanningRange(ulong TargetId, WPos Center, float Radius)
{
    internal bool Valid => TargetId != 0 && float.IsFinite(Center.X) && float.IsFinite(Center.Z)
        && float.IsFinite(Radius) && Radius >= 0;
    internal float Loss(WPos position) => Math.Max(0, (position - Center).Length() - Radius);
}
