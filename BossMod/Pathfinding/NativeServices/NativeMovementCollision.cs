using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace BossMod.Pathfinding;

// The game's observed walking/ground policy, not the default targeting raycast
// or the camera policy. Rendered decoration is never an input to this service.
internal static unsafe partial class NativeMovementCollision
{
    internal const ulong Layer = 1;
    internal const ulong MaterialMask = 0x2800;
    internal const ulong MaterialValue = 0x2000;
    internal const float BodyCenterHeight = 1.01f;
    internal readonly record struct Hit(Vector3 Point, Vector3 A, Vector3 B, Vector3 C, Vector3 Normal, ulong Material, float Distance = 0);
    internal static bool Includes(ulong material) => (material & MaterialMask) == MaterialValue;

    internal static bool Available()
    {
        if (!Service.Framework.IsInFrameworkUpdateThread) return false;
        var framework = Framework.Instance();
        return framework != null && framework->BGCollisionModule != null && framework->BGCollisionModule->SceneManager != null
            && framework->BGCollisionModule->SceneManager->NumScenes > 0;
    }

    [LibraryImport("kernel32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool TryAcquireSRWLockShared(nint* address);
    [LibraryImport("kernel32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void ReleaseSRWLockShared(nint* address);

    internal static Hit? Query(Vector3 from, Vector3 direction, float distance, float radius = 0)
    {
        if (!Available()) return null;
        var module = Framework.Instance()->BGCollisionModule;
        // Scene queries share the module's update lock, just like its native
        // entry points. Never stall a combat frame behind collision streaming.
        if (!TryAcquireSRWLockShared(&module->UpdateTaskLock))
            throw new InvalidOperationException("Native collision update in progress");
        try
        {
            var origin = new Vector4(from, radius);
            var filter = new RaycastMaterialFilter { Mask = MaterialMask, Value = MaterialValue };
            var maximum = distance;
            var parameters = new RaycastParams { Algorithm = radius > 0 ? 5 : 4, Origin = &origin, Direction = &direction,
                MaxDistance = &maximum, MaterialFilter = &filter };
            Hit? closest = null;
            foreach (var scene in module->SceneManager->Scenes)
            {
                var hit = new RaycastHit();
                if (!scene->Raycast(&hit, Layer, &parameters)) continue;
                if (!float.IsFinite(hit.Distance) || hit.Distance < 0 || hit.Distance > maximum) continue;
                maximum = hit.Distance;
                closest = new(hit.Point, hit.V1, hit.V2, hit.V3, hit.Normal, hit.Material, hit.Distance);
            }
            return closest;
        }
        finally { ReleaseSRWLockShared(&module->UpdateTaskLock); }
    }

    internal static Vector3? FloorRay(Vector3 from, Vector3 direction, float distance) => Query(from, direction, distance)?.Point;
}
