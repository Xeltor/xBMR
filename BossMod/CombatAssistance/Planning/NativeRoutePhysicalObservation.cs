using BossMod.Pathfinding;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace BossMod.CombatAssistance.Planning;

// Additional current collision evidence augments the native obstacle raster.
// This is not a second hazard model: warning timing and arena geometry continue
// to use TimedTraversal. No live collision query runs on the search worker.
internal sealed class NativeRoutePhysicalObservation
{
    internal sealed record Block(WPos Position, Vector3 From, Vector3 Step, float Radius, string Reason);
    private readonly Dictionary<WPos, Block> _blocks = [];
    private (ulong Actor, ushort Zone, int? Layer) _identity;
    internal IEnumerable<WPos> BlockedPoints => _blocks.Keys;
    internal string? LastReason { get; private set; }

    internal void Refresh(Actor actor, ushort zone, int? layer)
    {
        if (_identity != (actor.InstanceID, zone, layer))
        {
            _identity = (actor.InstanceID, zone, layer);
            _blocks.Clear();
        }
        if (!Available(actor.PosRot.XYZ(), actor.HitboxRadius)) return;
        foreach (var (point, block) in _blocks.ToArray())
            if (Available(block.From, block.Radius)
                && NativePhysicalProbe.Evaluate(block.From, block.Step, block.Radius, Raycast).Status == "clear")
                _blocks.Remove(point);
    }

    internal bool Check(Actor actor, IEnumerable<MovementStage> stages)
    {
        LastReason = null;
        if (!Available(actor.PosRot.XYZ(), actor.HitboxRadius)) return true;
        var block = Inspect(actor.PosRot.XYZ(), actor.HitboxRadius, stages, Raycast);
        if (block == null) return true;
        _blocks[block.Position] = block;
        LastReason = block.Reason;
        return false;
    }

    internal static Block? Inspect(Vector3 origin, float radius, IEnumerable<MovementStage> stages,
        Func<Vector3, Vector3, float, Vector3?> raycast)
    {
        var from = origin;
        foreach (var stage in stages)
        {
            // Authored transports and displacement use their own native
            // transitions. They must not become an ordinary walking chord.
            if (stage.Kind != MovementStageKind.Walk)
            {
                if (stage.To != stage.From) from = stage.To.ToVec3(from.Y);
                continue;
            }
            var delta = stage.To - new WPos(from);
            var distance = delta.Length();
            if (distance < 0.01f) continue;
            var steps = (int)MathF.Ceiling(distance);
            var start = new WPos(from);
            for (var i = 1; i <= steps; ++i)
            {
                var next = start + delta * (i / (float)steps);
                var movement = next.ToVec3(from.Y) - from;
                var probe = NativePhysicalProbe.Evaluate(from, movement, radius, raycast);
                if (probe.Status == "blocked")
                {
                    var point = probe.MovementCollision && probe.BlockedDistance is { } hit
                        ? new WPos(from) + new WDir(movement.X, movement.Z).Normalized() * hit : next;
                    return new(point, from, movement, radius, probe.Reason);
                }
                // Unknown collision data neither proves an obstruction nor
                // overrides existing native geometry. Follow changing floor
                // height on stairs instead of projecting the entire path flat.
                // Reuse the verified centre observation. An extra unchecked
                // native ray here could fail after the probe had safely returned
                // unknown, or observe a different floor in the same corridor step.
                from = next.ToVec3(probe.GroundHeight is { } ground && Math.Abs(ground - from.Y) <= 2 ? ground : from.Y);
            }
        }
        return null;
    }

    private static Vector3? Raycast(Vector3 from, Vector3 direction, float distance)
        => BGCollisionModule.RaycastMaterialFilter(from, direction, out var hit, distance) ? hit.Point : null;

    private static unsafe bool Available(Vector3 point, float radius)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread) return false;
        var framework = Framework.Instance();
        var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null || collision->SceneManager->NumScenes < 1
            || collision->LoadInProgressCounter != 0) return false;
        var sphere = collision->SceneManager->StreamingSphere;
        return float.IsFinite(sphere.W) && sphere.W > radius
            && Vector3.Distance(point, new(sphere.X, sphere.Y, sphere.Z)) + radius < sphere.W;
    }
}
