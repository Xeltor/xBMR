using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// An authored environmental transition, not a movement action. The approach is
// walked, the entrance may require a wait, and only the authored exit is a legal
// landing. The space between the pads is never treated as a walking corridor.
internal sealed record PlanningTeleport(WPos Entrance, WPos Exit, float Radius, float UseTime, float NotBefore, float? ElapsedUse = null)
{
    internal bool Valid => float.IsFinite(Entrance.X) && float.IsFinite(Entrance.Z)
        && float.IsFinite(Exit.X) && float.IsFinite(Exit.Z) && float.IsFinite(Radius) && Radius > 0
        && float.IsFinite(UseTime) && UseTime >= 0 && float.IsFinite(NotBefore) && NotBefore >= 0
        && (ElapsedUse == null || float.IsFinite(ElapsedUse.Value) && ElapsedUse >= 0 && ElapsedUse <= UseTime)
        && (Entrance - Exit).LengthSq() > Radius * Radius;

    internal static IEnumerable<PlanningTeleport> Capture(IEnumerable<Teleporter> native)
    {
        foreach (var edge in native)
        {
            yield return new(edge.Entrance, edge.Exit, edge.Radius, Math.Max(0, edge.UseTime), Math.Max(0, edge.NotBeforeG));
            if (edge.Bidirectional)
                yield return new(edge.Exit, edge.Entrance, edge.Radius, Math.Max(0, edge.UseTime), Math.Max(0, edge.NotBeforeG));
        }
    }

    internal bool SameGeometry(PlanningTeleport other)
        => Entrance == other.Entrance && Exit == other.Exit && Radius == other.Radius && UseTime == other.UseTime;

    internal bool Same(PlanningTeleport other, float age)
        => SameGeometry(other) && Math.Abs(NotBefore - Math.Max(0, other.NotBefore - age)) < 0.01f
            && (ElapsedUse == null ? other.ElapsedUse == null : other.ElapsedUse is { } previous
                && Math.Abs(ElapsedUse.Value - Math.Min(UseTime, previous + Math.Max(0, age - other.NotBefore))) < 0.01f);

    internal bool Intersects(WPos from, WPos to)
    {
        var delta = to - from;
        var length = delta.LengthSq();
        var t = length > 0 ? Math.Clamp((Entrance - from).Dot(delta) / length, 0, 1) : 0;
        return (Entrance - (from + t * delta)).LengthSq() <= Radius * Radius;
    }

    internal bool PermitsOrdinaryTravel(WPos from, WPos to)
    {
        // Native arrivals and a player already standing on a pad may leave it.
        // Entering or cutting through another pad must use its compound edge.
        return !Intersects(from, to) || from.InCircle(Entrance, Radius)
            && !to.InCircle(Entrance, Radius) && (to - from).Dot(from - Entrance) >= 0;
    }

    internal bool Evaluate(MovementPlanningContext context, WPos from, PlanningTravel travel, float time,
        out float end, out float slack)
    {
        end = time;
        slack = float.MaxValue;
        if (!Valid || context.Cell(Entrance) < 0 || context.Cell(Exit) < 0
            || context.Teleports.Any(t => t.Exit != Exit && t.Entrance.InCircle(Entrance, t.Radius + Radius))
            || context.Teleports.Any(t => !ReferenceEquals(t, this) && t.Entrance != Entrance && t.Intersects(from, Entrance)))
            return false;
        var approach = travel.Seconds((Entrance - from).Length(), time);
        var entered = time + approach;
        var begin = Math.Max(entered, NotBefore);
        var remaining = from.InCircle(Entrance, Radius) && ElapsedUse is { } elapsed ? Math.Max(0, UseTime - elapsed) : UseTime;
        end = begin + remaining;
        // Conservatively retain entrance safety throughout the authored transit
        // time. Unknown hazard expiry cannot make either pad safe again.
        if (end > MovementPlanningContext.MaximumHorizonSeconds
            || !context.ExecutionRules.PermitsMovement(time, end)
            || !context.Traverse(from, Entrance, time, approach, out var approachSlack)
            || !context.Traverse(Entrance, Entrance, entered, end - entered, out var waitSlack)
            || !context.Traverse(Exit, Exit, end, 0, out var landingSlack))
            return false;
        slack = Math.Min(approachSlack, Math.Min(waitSlack, landingSlack));
        return true;
    }
}
