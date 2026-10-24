using System.Numerics;
using BossMod.Pathfinding;
using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void NativePhysicalProbes()
    {
        Vector3? Flat(Vector3 p, Vector3 d, float length) => d.Y < -0.9f ? new(p.X, 0, p.Z) : null;
        var from = Vector3.Zero; var step = new Vector3(2, 0, 0);
        var clear = NativePhysicalProbe.Evaluate(from, step, 0.5f, Flat);
        Check(clear.Status == "clear" && clear.CurrentSupportVerified && clear.AheadSupportVerified
            && clear.ProbePosition![0] == 1.25f, "native physical probe preserves supported floor and clamps lookahead to 1.25y");
        var unknown = NativePhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => null);
        Check(unknown.Status == "unknown" && !unknown.CurrentSupportVerified, "missing current floor is unknown, never a proven drop");
        var ledge = NativePhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f && p.X <= 0.7f ? new(p.X, 0, p.Z) : null);
        Check(ledge.Status == "blocked" && ledge.CurrentSupportVerified && ledge.Reason == "observed-no-ground-support-ahead",
            "actual supported origin followed by a complete negative floor observation identifies a proposed unsupported step");
        var drop = NativePhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? new(p.X, p.X > 0.7f ? -3 : 0, p.Z) : null);
        Check(drop.Status == "blocked" && drop.Drop == 3, "native observed drop over two yalms blocks the proposed step");
        var wall = NativePhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? new(p.X, 0, p.Z) : p + d * 0.2f);
        Check(wall.Status == "blocked" && wall.MovementCollision && wall.CurrentSupportVerified,
            "native foot/torso collision remains distinct from missing floor support");
        var obscuredFloor = NativePhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? null : p + d * 0.2f);
        Check(obscuredFloor.Status == "blocked" && obscuredFloor.MovementCollision && !obscuredFloor.CurrentSupportVerified,
            "an obstructed floor probe cannot conceal an independently observed wall");
        var sideWall = NativePhysicalProbe.Evaluate(from, step, 0.5f,
            (p, d, n) => d.Y < -0.9f ? new(p.X, 0, p.Z) : p.Z > 0.4f ? p + d * 0.2f : null);
        Check(sideWall.Status == "blocked" && sideWall.MovementCollision,
            "body-width collision is checked even when the centre ray clears the wall");
        Check(NativePhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => throw new InvalidOperationException()).Status == "unknown",
            "native query failure cannot authorize a physical brake");
        Check(NativePhysicalProbe.Evaluate(from, step, 0.5f, (_, _, _) => new(float.NaN, 0, 0)).Status == "unknown",
            "nonfinite native floor observations never become physical evidence");
        var route = new[] { new MovementStage(MovementStageKind.Walk, default, new(6, 0), 0, 1) };
        Check(NativeRoutePhysicalObservation.Inspect(from, 0.5f, route, Flat) == null,
            "complete ordinary walking corridor accepts consistently observed floor and clearance");
        Check(NativeRoutePhysicalObservation.Inspect(from, 0.5f, route, (_, _, _) => throw new InvalidOperationException()) == null,
            "a failed physical query remains unknown for the whole corridor without a second unchecked native read");
        Check(clear.GroundHeight == 0, "the shared probe returns its measured centre-floor height for continuation");
        Vector3? MidWall(Vector3 p, Vector3 d, float length)
            => d.Y < -0.9f ? new(p.X, 0, p.Z)
                : d.X > 0 && p.X < 3.25f && p.X + length * d.X >= 3.25f ? p + d * ((3.25f - p.X) / d.X) : null;
        var obstruction = NativeRoutePhysicalObservation.Inspect(from, 0.5f, route, MidWall);
        Check(obstruction != null && obstruction.Position.AlmostEqual(new(3.25f, 0), 0.001f),
            "a wall in the middle of a longer route is rejected before any first-step movement and located in the shared map");
        Vector3? Stairs(Vector3 p, Vector3 d, float length)
            => d.Y < -0.9f ? new(p.X, MathF.Floor(p.X * 2) * 0.1f, p.Z) : null;
        Check(NativeRoutePhysicalObservation.Inspect(from, 0.5f, route, Stairs) == null,
            "corridor observations follow changing stair height instead of projecting all later feet into the first floor");
        Check(NativeRoutePhysicalObservation.Inspect(from, 0.5f,
            [new(MovementStageKind.Teleport, default, new(6, 0), 0, 1)], MidWall) == null,
            "authored transports are not incorrectly tested as a walking chord through intervening geometry");
    }
}
