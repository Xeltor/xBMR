namespace BossMod.Integration;

// Read-only game collision observations. A failed current-floor query is unknown,
// never evidence that the proposed step reaches a ledge.
internal static class IntegrationPhysicalProbe
{
    internal sealed record Result(string Status, string Reason, bool CurrentSupportVerified = false, bool AheadSupportVerified = false,
        bool MovementCollision = false, float? Drop = null, float? BlockedDistance = null, float[]? ProbePosition = null);
    internal static Result Evaluate(Vector3 from, Vector3 movement, float radius, Func<Vector3, Vector3, float, Vector3?> raycast)
    {
        if (!float.IsFinite(from.X) || !float.IsFinite(from.Y) || !float.IsFinite(from.Z) || !float.IsFinite(movement.X)
            || !float.IsFinite(movement.Y) || !float.IsFinite(movement.Z) || !float.IsFinite(radius) || radius is < 0 or > 5)
            return new("unknown", "invalid-native-physical-pose");
        var delta = new Vector3(movement.X, 0, movement.Z); var length = delta.Length();
        if (length < 0.01f || !float.IsFinite(length)) return new("unknown", "no-bounded-horizontal-step");
        var direction = delta / length; var target = from + direction * Math.Min(length, 1.25f);
        var footprint = Math.Max(0.1f, radius);
        var offsets = new[] { Vector3.Zero, Vector3.UnitX * footprint, -Vector3.UnitX * footprint, Vector3.UnitZ * footprint, -Vector3.UnitZ * footprint };
        bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
        try
        {
            foreach (var offset in offsets)
            {
                var hit = raycast(from + offset + Vector3.UnitY * 2, -Vector3.UnitY, 12);
                if (hit is not { } point || !Finite(point) || Math.Abs(from.Y - point.Y) > 0.75f)
                    return new("unknown", "current-native-ground-support-unverified");
            }
            float? worstDrop = null;
            foreach (var offset in offsets)
            {
                var hit = raycast(target + offset + Vector3.UnitY * 2, -Vector3.UnitY, 12);
                if (hit == null) return new("blocked", "observed-no-ground-support-ahead", true, ProbePosition: [target.X, target.Y, target.Z]);
                if (!Finite(hit.Value)) return new("unknown", "invalid-native-floor-hit");
                var drop = from.Y - hit.Value.Y; worstDrop = Math.Max(worstDrop ?? float.NegativeInfinity, drop);
                if (drop > 2) return new("blocked", "observed-excessive-drop-ahead", true, true, Drop: drop, ProbePosition: [target.X, target.Y, target.Z]);
            }
            foreach (var height in new[] { 0.25f, 1.5f })
            {
                var origin = from + Vector3.UnitY * height;
                if (raycast(origin, direction, Math.Min(length, 1.25f)) is { } hit)
                {
                    if (!Finite(hit)) return new("unknown", "invalid-native-movement-hit");
                    return new("blocked", "observed-movement-collision", true, true, true, worstDrop, Vector3.Distance(origin, hit), [target.X, target.Y, target.Z]);
                }
            }
            return new("clear", "observed-ground-and-movement-clearance", true, true, Drop: worstDrop, ProbePosition: [target.X, target.Y, target.Z]);
        }
        catch { return new("unknown", "native-physical-query-failed"); }
    }
}
