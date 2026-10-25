namespace BossMod.Pathfinding;

// Read-only game collision observations. A failed current-floor query is unknown,
// never evidence that the proposed step reaches a ledge.
internal static class NativePhysicalProbe
{
    internal sealed record Result(string Status, string Reason, bool CurrentSupportVerified = false, bool AheadSupportVerified = false,
        bool MovementCollision = false, float? Drop = null, float? BlockedDistance = null, float[]? ProbePosition = null,
        float? GroundHeight = null, Vector3? ContactPoint = null, bool NextSupportVerified = false);

    internal static Result Evaluate(Vector3 from, Vector3 movement, float radius, Func<Vector3, Vector3, float, Vector3?> raycast,
        Func<Vector3, Vector3, float, float, Vector3?>? sweep = null, bool knownCurrentSupport = false, bool groundCoverage = true)
    {
        if (!Finite(from) || !Finite(movement) || !float.IsFinite(radius) || radius < 0)
            return new("unknown", "invalid-native-physical-pose");
        var delta = new Vector3(movement.X, 0, movement.Z); var length = delta.Length();
        if (length < 0.01f || !float.IsFinite(length)) return new("unknown", "no-bounded-horizontal-step");
        var direction = delta / length; var target = from + direction * Math.Min(length, 1.25f);
        var footprint = Math.Max(0.1f, radius);
        ReadOnlySpan<Vector3> offsets = [Vector3.Zero, Vector3.UnitX * footprint, -Vector3.UnitX * footprint,
            Vector3.UnitZ * footprint, -Vector3.UnitZ * footprint];
        try
        {
            var currentSupport = knownCurrentSupport;
            if (!currentSupport)
            {
                currentSupport = true;
                foreach (var offset in offsets)
                {
                    var hit = raycast(from + offset + Vector3.UnitY * 2, -Vector3.UnitY, 12);
                    if (hit is not { } point || !Finite(point) || Math.Abs(from.Y - point.Y) > 0.75f)
                        currentSupport = false;
                }
            }
            float? worstDrop = null;
            float? groundHeight = null;
            var minimum = float.MaxValue; var maximum = float.MinValue;
            string? floorFailure = currentSupport ? null : "current-native-ground-support-unverified";
            if (currentSupport)
                foreach (var offset in offsets)
                {
                    var hit = raycast(target + offset + Vector3.UnitY * 2, -Vector3.UnitY, 12);
                    if (hit == null) { floorFailure = "observed-no-ground-support-ahead"; break; }
                    if (!Finite(hit.Value)) { floorFailure = "invalid-native-floor-hit"; break; }
                    if (offset == Vector3.Zero) groundHeight = hit.Value.Y;
                    minimum = Math.Min(minimum, hit.Value.Y); maximum = Math.Max(maximum, hit.Value.Y);
                    var drop = from.Y - hit.Value.Y; worstDrop = Math.Max(worstDrop ?? float.NegativeInfinity, drop);
                    if (drop > 2) { floorFailure = "observed-excessive-drop-ahead"; break; }
                }
            // A swept body detects thin objects between the old three ray lanes.
            // Follow verified floor height along slopes instead of casting the
            // whole route horizontally through rising ground.
            if (sweep != null)
            {
                var end = target;
                if (floorFailure == null && groundHeight is { } ground && Math.Abs(ground - from.Y) <= 2) end.Y = ground;
                var sweepDelta = end - from; var sweepLength = sweepDelta.Length();
                var origin = from + Vector3.UnitY * NativeMovementCollision.BodyCenterHeight;
                if (sweep(origin, sweepDelta / sweepLength, sweepLength, footprint) is { } hit)
                {
                    if (!Finite(hit)) return new("unknown", "invalid-native-movement-hit");
                    return new("blocked", "observed-movement-collision", currentSupport, false, true,
                        BlockedDistance: Vector3.Distance(origin, hit), ProbePosition: [target.X, target.Y, target.Z], ContactPoint: hit);
                }
            }
            else
            {
                var lateral = new Vector3(-direction.Z, 0, direction.X) * footprint;
                foreach (var height in (ReadOnlySpan<float>)[0.25f, 1.5f])
                    foreach (var offset in (ReadOnlySpan<Vector3>)[Vector3.Zero, lateral, -lateral])
                    {
                        var origin = from + offset + Vector3.UnitY * height;
                        if (raycast(origin, direction, Math.Min(length, 1.25f)) is not { } hit) continue;
                        if (!Finite(hit)) return new("unknown", "invalid-native-movement-hit");
                        return new("blocked", "observed-movement-collision", currentSupport, false, true,
                            BlockedDistance: Vector3.Distance(origin, hit), ProbePosition: [target.X, target.Y, target.Z], ContactPoint: hit);
                    }
            }
            // Positive wall evidence is usable while streaming; missing or distant
            // ground during incomplete loading is not a proven cliff.
            if (floorFailure != null)
            {
                var blocked = groundCoverage && currentSupport && (floorFailure is "observed-no-ground-support-ahead" or "observed-excessive-drop-ahead");
                return new(blocked ? "blocked" : "unknown", floorFailure, currentSupport,
                    floorFailure == "observed-excessive-drop-ahead", Drop: worstDrop, ProbePosition: [target.X, target.Y, target.Z]);
            }
            return new("clear", "observed-ground-and-movement-clearance", true, true, Drop: worstDrop,
                ProbePosition: [target.X, target.Y, target.Z], GroundHeight: groundHeight,
                NextSupportVerified: groundHeight is { } centre && centre - minimum <= 0.75f && maximum - centre <= 0.75f);
        }
        catch { return new("unknown", "native-physical-query-failed"); }
    }
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
