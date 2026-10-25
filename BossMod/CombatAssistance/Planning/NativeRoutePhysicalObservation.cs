using BossMod.Pathfinding;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace BossMod.CombatAssistance.Planning;

// Observed physical surfaces augment the SAME raster used by native recovery
// and the joint search. All collision calls stay on the framework thread.
internal sealed class NativeRoutePhysicalObservation
{
    internal sealed record Block(WPos Position, Vector3 From, Vector3 Step, float Radius, string Reason);
    private sealed record Observation(Block Block, NativePhysicalObstacle Surface, Vector3 ProbeFrom, Vector3 ProbeDirection, float ProbeDistance);
    private readonly Dictionary<NativePhysicalObstacle, Observation> _blocks = [];
    private readonly List<NativePhysicalObstacle> _removed = [];
    private (ulong Actor, ushort Zone, int? Layer) _identity;
    private float _height;
    private NativeMovementCollision.Hit? _lastHit;
    private Vector3 _lastFrom, _lastDirection;
    private float _lastDistance;
    internal IEnumerable<NativePhysicalObstacle> BlockedObstacles => _blocks.Keys;
    internal string? LastReason { get; private set; }
    internal int LastQueries { get; private set; }
    internal int RefreshQueries { get; private set; }
    internal bool QueryAvailable { get; private set; }
    internal int KnownSurfaces => _blocks.Count;

    internal void Refresh(Actor actor, ushort zone, int? layer, NavigationMapSource source)
    {
        RefreshQueries = 0;
        var position = actor.PosRot.XYZ();
        if (_identity != (actor.InstanceID, zone, layer) || Math.Abs(position.Y - _height) > 0.75f)
        {
            _identity = (actor.InstanceID, zone, layer);
            _height = position.Y;
            _blocks.Clear();
        }
        QueryAvailable = NativeMovementCollision.Available();
        if (!QueryAvailable) return;
        _removed.Clear();
        foreach (var (surface, observation) in _blocks)
        {
            var block = observation.Block;
            if (!source.Bounds.Contains(block.Position - source.Center))
            { _removed.Add(surface); continue; }
            // Previously each remembered wall repeated a complete 16-ray floor
            // and clearance probe every frame. Recheck its exact swept-body
            // witness instead. Streaming misses cannot retire known obstacles.
            if (!GroundCoverage(block.From, block.From + block.Step, block.Radius)) continue;
            try
            {
                if (block.Reason == "observed-movement-collision")
                {
                    ++RefreshQueries;
                    var hit = NativeMovementCollision.Query(observation.ProbeFrom, observation.ProbeDirection,
                        observation.ProbeDistance, block.Radius);
                    if (hit == null || NativePhysicalObstacle.FromHit(hit.Value, Math.Max(0.1f, block.Radius)) != surface)
                        _removed.Add(surface);
                }
                else if (NativePhysicalProbe.Evaluate(block.From, block.Step, block.Radius, RefreshFloorRay, RefreshSweep).Status == "clear")
                    _removed.Add(surface);
            }
            catch (InvalidOperationException) { /* An updating scene is not evidence of removal. */ }
        }
        foreach (var surface in _removed) _blocks.Remove(surface);
    }

    internal bool Check(Actor actor, IEnumerable<MovementStage> stages)
    {
        LastReason = null;
        LastQueries = 0;
        _lastHit = null;
        QueryAvailable = NativeMovementCollision.Available();
        if (!QueryAvailable) { LastReason = "native-physical-query-unavailable"; return true; }
        var block = Inspect(actor.PosRot.XYZ(), actor.HitboxRadius, stages, FloorRay, Sweep, GroundCoverage);
        if (block == null) return true;
        var surface = block.Reason == "observed-movement-collision" && _lastHit is { } hit
            ? NativePhysicalObstacle.FromHit(hit, Math.Max(0.1f, block.Radius))
            : new NativePhysicalObstacle(block.Position, block.Position, 0);
        _blocks[surface] = new(block, surface, _lastFrom, _lastDirection, _lastDistance);
        LastReason = block.Reason;
        return false;
    }

    internal static Block? Inspect(Vector3 origin, float radius, IEnumerable<MovementStage> stages,
        Func<Vector3, Vector3, float, Vector3?> raycast,
        Func<Vector3, Vector3, float, float, Vector3?>? sweep = null,
        Func<Vector3, Vector3, float, bool>? groundCoverage = null)
    {
        var from = origin;
        var supportKnown = false;
        foreach (var stage in stages)
        {
            if (stage.Kind != MovementStageKind.Walk)
            {
                if (stage.To != stage.From) { from = stage.To.ToVec3(from.Y); supportKnown = false; }
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
                var probe = NativePhysicalProbe.Evaluate(from, movement, radius, raycast, sweep, supportKnown,
                    groundCoverage?.Invoke(from, from + movement, radius) != false);
                if (probe.Status == "blocked")
                    return new(probe.ContactPoint is { } hit ? new(hit) : next, from, movement, radius, probe.Reason);
                var followGround = probe.GroundHeight is { } ground && Math.Abs(ground - from.Y) <= 2;
                from = next.ToVec3(followGround ? probe.GroundHeight!.Value : from.Y);
                supportKnown = followGround && probe.NextSupportVerified;
            }
        }
        return null;
    }

    private Vector3? RefreshFloorRay(Vector3 from, Vector3 direction, float distance)
    {
        ++RefreshQueries;
        return NativeMovementCollision.FloorRay(from, direction, distance);
    }
    private Vector3? RefreshSweep(Vector3 from, Vector3 direction, float distance, float radius)
    {
        ++RefreshQueries;
        return NativeMovementCollision.Query(from, direction, distance, radius)?.Point;
    }

    private Vector3? FloorRay(Vector3 from, Vector3 direction, float distance)
    {
        ++LastQueries;
        return NativeMovementCollision.FloorRay(from, direction, distance);
    }
    private Vector3? Sweep(Vector3 from, Vector3 direction, float distance, float radius)
    {
        ++LastQueries;
        _lastHit = NativeMovementCollision.Query(from, direction, distance, radius);
        _lastFrom = from; _lastDirection = direction; _lastDistance = distance;
        return _lastHit?.Point;
    }

    private static unsafe bool GroundCoverage(Vector3 from, Vector3 to, float radius)
    {
        var framework = Framework.Instance();
        var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null || collision->LoadInProgressCounter != 0) return false;
        var sphere = collision->SceneManager->StreamingSphere;
        var centre = new Vector3(sphere.X, sphere.Y, sphere.Z);
        return float.IsFinite(sphere.W) && sphere.W > radius
            && Vector3.Distance(from, centre) + radius < sphere.W && Vector3.Distance(to, centre) + radius < sphere.W;
    }
}
