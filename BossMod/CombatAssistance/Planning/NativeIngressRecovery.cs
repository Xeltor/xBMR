using BossMod.Pathfinding;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace BossMod.CombatAssistance.Planning;

// Entry from outside the authored map is a recovery operation, not an expanded
// arena or a completed joint search. The complete corridor must have observed
// floor, collision clearance and unchanged native warning deadlines.
internal static class NativeIngressRecovery
{
    internal static bool Validate(Vector3 origin, WPos destination, float radius, float speed, AIHints hints, DateTime now,
        Func<Vector3, Vector3, float, Vector3?> raycast, Func<Vector3, bool> loaded, out string reason)
    {
        reason = "entry geometry or movement timing unavailable";
        var from = new WPos(origin);
        var distance = (destination - from).Length();
        if (!float.IsFinite(speed) || speed <= 0 || !float.IsFinite(distance) || distance <= 0
            || distance >= int.MaxValue
            || distance / speed > MovementPlanningContext.MaximumHorizonSeconds
            || !float.IsFinite(radius) || radius < 0 || hints.Teleporters.Count != 0 || hints.PathfindMapArenaProjectionLayer != null)
            return false;
        var duration = distance / speed;
        var rules = PlanningExecutionRules.Capture(now, hints);
        if (!rules.PermitsMovement(0, duration)) { reason = "authored movement lock prevents entry"; return false; }
        var budget = new NativeRouteValidation.Budget();
        var clearance = radius + NativeRouteValidation.SpatialMargin;
        foreach (var obstacle in hints.TemporaryObstacles)
        {
            if (!float.IsFinite(obstacle.ConservativeLipschitz) || obstacle.ConservativeLipschitz < 0
                || NativeRouteValidation.CheckShape(obstacle, from, destination, clearance, 0, duration,
                    float.NegativeInfinity, 0, null, null, budget) != null)
            { reason = "entry obstacle clearance unavailable"; return false; }
        }
        foreach (var warning in hints.ForbiddenZones)
        {
            if (!float.IsFinite(warning.shapeDistance.ConservativeLipschitz) || warning.shapeDistance.ConservativeLipschitz < 0
                || NativeRouteValidation.CheckShape(warning.shapeDistance, from, destination, clearance, 0, duration,
                    (float)(warning.activation - now).TotalSeconds - NavigationDecision.ActivationTimeCushion, 0, null, 0, budget) != null
                || warning.shapeDistance.Distance(destination) <= warning.shapeDistance.ConservativeLipschitz * clearance)
            { reason = "entry route needs a different warning escape"; return false; }
        }
        // Check all of the corridor, including floor between its endpoints. A
        // ray with no wall hit does not prove there is ground beneath that ray.
        var steps = Math.Max(1, (int)MathF.Ceiling(distance));
        var destination3 = destination.ToVec3(origin.Y);
        for (var i = 0; i < steps; ++i)
        {
            var a = Vector3.Lerp(origin, destination3, i / (float)steps);
            var b = Vector3.Lerp(origin, destination3, (i + 1) / (float)steps);
            if (!loaded(a) || !loaded(b)) { reason = "entry corridor is outside loaded collision"; return false; }
            var physical = NativePhysicalProbe.Evaluate(a, b - a, radius, raycast);
            if (physical.Status != "clear") { reason = physical.Reason; return false; }
        }
        reason = "native entry corridor revalidated; joint search resumes inside its arena";
        return true;
    }

    internal static unsafe WPos? Find(WorldState world, AIHints hints, Actor player, BossModule module, float speed, out string reason)
    {
        reason = "entry waits for native authority and physical evidence";
        if (!Service.Framework.IsInFrameworkUpdateThread || player.CastInfo != null || player.PendingKnockbacks.Count != 0
            || player.FindStatus(418) != null || hints.ForcedMovement != null || hints.ForbiddenDirections.Count != 0)
            return null;
        var framework = Framework.Instance();
        var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null || collision->SceneManager->NumScenes < 1
            || collision->LoadInProgressCounter != 0) return null;
        var sphere = collision->SceneManager->StreamingSphere;
        if (!float.IsFinite(sphere.W) || sphere.W <= player.HitboxRadius) return null;
        var point = module.Arena.Center;
        var arena = NativeArenaGeometry.Query(module.Arena.Bounds, module.Arena.Center, hints.PathfindMapArenaProjectionLayer, point, 0);
        if (!arena.Available || arena.SignedClearance is not { } margin || margin <= player.HitboxRadius) return null;
        return Validate(player.PosRot.XYZ(), point, player.HitboxRadius, speed, hints, world.CurrentTime,
            static (from, direction, length) => BGCollisionModule.RaycastMaterialFilter(from, direction, out var hit, length) ? hit.Point : null,
            p => Vector3.Distance(p, new(sphere.X, sphere.Y, sphere.Z)) + player.HitboxRadius < sphere.W, out reason) ? point : null;
    }
}
