using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace BossMod.Pathfinding;

// Exact ground placement is a framework-thread observation. Native arena raster
// safety and loaded collision coverage are independent requirements.
internal static class NativeGroundObservation
{
    internal static unsafe bool TryLanding(Actor actor, WPos landing, out Vector3 position)
    {
        position = default;
        if (!Service.Framework.IsInFrameworkUpdateThread)
            return false;
        var framework = Framework.Instance();
        var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null || collision->SceneManager->NumScenes < 1
            || collision->LoadInProgressCounter != 0)
            return false;
        var sphere = collision->SceneManager->StreamingSphere;
        var point = new Vector3(landing.X, actor.PosRot.Y, landing.Z);
        if (!float.IsFinite(sphere.W) || sphere.W <= actor.HitboxRadius
            || Vector3.Distance(point, new(sphere.X, sphere.Y, sphere.Z)) + actor.HitboxRadius >= sphere.W)
            return false;
        var radius = Math.Max(0.1f, actor.HitboxRadius);
        foreach (var offset in new[] { Vector3.Zero, Vector3.UnitX * radius, -Vector3.UnitX * radius, Vector3.UnitZ * radius, -Vector3.UnitZ * radius })
        {
            if (!BGCollisionModule.RaycastMaterialFilter(point + offset + Vector3.UnitY * 2, -Vector3.UnitY, out var hit, 12)
                || !float.IsFinite(hit.Point.Y) || Math.Abs(hit.Point.Y - point.Y) > 2)
                return false;
            if (offset == default)
                position = hit.Point;
        }
        return float.IsFinite(position.X) && float.IsFinite(position.Z) && new WPos(position).AlmostEqual(landing, 0.01f);
    }
}
