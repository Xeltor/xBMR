using BossMod.Pathfinding;
using System.Diagnostics;

namespace BossMod.CombatAssistance.Planning;

// Capture on the framework thread. Neither native callbacks, mutable shapes, actors nor
// reflection-based geometry copies cross into a worker. Native recovery and optimization
// consume the same rasterization, retaining the strict limits separately from recovery.
internal sealed class NativeNavigationSnapshot
{
    private readonly Map _map = new();
    private readonly ImmutableArray<float> _strictLimits;
    private readonly ImmutableArray<Teleporter> _teleporters;
    private readonly bool _relaxed;
    private readonly long _rasterTicks;
    private readonly FrozenGoalField? _goals;
    private readonly ImmutableArray<float> _presentation;
    // Optional native physical-classification evidence for direct geometry queries.
    private readonly NavigationRaster? _physicalRaster;
    private readonly NavigationMapSource? _mapSource;
    internal NavigationIdentity Identity { get; }
    internal int Cells => checked(_map.Width * _map.Height);
    internal bool HasTeleporters => !_teleporters.IsEmpty;
    internal ImmutableArray<PlanningTeleport> Teleports { get; }
    internal bool SameTransitions(NativeNavigationSnapshot previous) => Teleports.Length == previous.Teleports.Length
        && Teleports.Select((t, i) => t.Same(previous.Teleports[i], (float)(Identity.WorldTime - previous.Identity.WorldTime).TotalSeconds)).All(same => same);

    private NativeNavigationSnapshot(NavigationDecision.CapturedInput input, IEnumerable<WPos>? observedBlocks)
    {
        if (!float.IsFinite(input.Identity.Speed) || input.Identity.Speed <= 0)
            throw new ArgumentException("Navigation speed must be positive and finite.", nameof(input));
        var start = Stopwatch.GetTimestamp();
        Identity = input.Identity;
        var raster = NavigationDecision.RasterizeInput(_map, input, true, freezeCallbacks: true);
        foreach (var point in observedBlocks ?? [])
        {
            var cell = _map.WorldToGrid(point);
            if (!_map.InBounds(cell.x, cell.y)) continue;
            var index = _map.GridToIndex(cell);
            _map.PixelMaxG[index] = -1000;
            _map.PixelPriority[index] = float.MinValue;
            if (raster.Unrelaxed != null) raster.Unrelaxed[index] = -1000;
            if (raster.Blocks != null) raster.Blocks[index] = 2;
        }
        _strictLimits = (raster.Unrelaxed ?? _map.PixelMaxG).AsSpan(0, Cells).ToArray().ToImmutableArray();
        _teleporters = input.Teleporters.ToImmutableArray();
        Teleports = PlanningTeleport.Capture(_teleporters).ToImmutableArray();
        _relaxed = raster.Relaxed;
        // Native recovery keeps its original casting/urgent-warning goal gate.
        // The joint search must evaluate the useful endpoint before that first
        // recovery move, including when casting finishes later in the route.
        if (input.GoalsEnabled)
            _goals = FrozenGoalField.Capture(_map, input.Goals.Native);
        var strict = CopyStrictMap();
        var nativePriorities = strict.PixelPriority.AsSpan(0, Cells).ToArray();
        if (input.GoalsEnabled && input.Identity.AvoidanceBuffer > 0)
            NavigationDecision.AvoidForbiddenZone(strict, input.Identity.AvoidanceBuffer);
        _presentation = Enumerable.Range(0, Cells).Select(i => strict.PixelPriority[i] - nativePriorities[i]).ToImmutableArray();
        if (input.PublishRaster && raster.Blocks != null)
        {
            _physicalRaster = new(_map, raster.Unrelaxed, raster.Blocks);
            _mapSource = input.MapSource;
        }
        _rasterTicks = Stopwatch.GetTimestamp() - start;
    }

    internal static NativeNavigationSnapshot Capture(NavigationDecision.CapturedInput input, IEnumerable<WPos>? observedBlocks = null) => new(input, observedBlocks);

    internal Map CopyStrictMap()
    {
        var map = new Map();
        map.Init(_map, _map.Center);
        _strictLimits.CopyTo(map.PixelMaxG);
        _goals?.ApplyNativePriorities(map);
        return map;
    }

    internal float[] CopyPresentation() => _presentation.ToArray();

    internal bool SameFrozenMap(NativeNavigationSnapshot other)
    {
        var age = (float)(Identity.WorldTime - other.Identity.WorldTime).TotalSeconds;
        if (age < 0 || _map.Width != other._map.Width || _map.Height != other._map.Height || _map.Center != other._map.Center
            || _map.Rotation != other._map.Rotation || _map.Resolution != other._map.Resolution
            || !_map.PixelPriority.AsSpan(0, Cells).SequenceEqual(other._map.PixelPriority.AsSpan(0, Cells))
            || !SameTransitions(other)) return false;
        for (var i = 0; i < Cells; ++i)
        {
            var now = _strictLimits[i]; var before = other._strictLimits[i];
            if (now == float.MaxValue || before == float.MaxValue || now < 0 || before < 0)
            { if (now != before) return false; }
            else if (Math.Abs(now - Math.Max(0, before - age)) > 0.002f) return false;
        }
        return true;
    }

    internal bool CanExecuteRecovery(NavigationPublication publication, WPos current)
    {
        if (publication.Destination is not { } destination) return true;
        var duration = (destination - current).Length() / Identity.Speed;
        // Emergency egress may intentionally be the least harmful route from an
        // already unsafe position. Physical obstructions still remain binding.
        var strict = Enum.TryParse<ThetaStar.Score>(publication.NativeScore, out var score) && score >= ThetaStar.Score.Safe;
        return TimedTraversal.Check(_map, current, destination, 0, duration, 0, out _, physicalOnly: !strict);
    }

    internal Map CopyRecoveryMap()
    {
        var map = new Map();
        map.Init(_map, _map.Center);
        return map;
    }

    internal NavigationDecision Recover(NavigationDecision.Context context, System.Threading.CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        context.Map.Init(_map, _map.Center);
        if (!_teleporters.IsEmpty)
            context.Map.BuildTeleporterEdges(_teleporters.ToArray());
        var start = Stopwatch.GetTimestamp();
        context.ThetaStar.Start(context.Map, Identity.Origin, 1f / Identity.Speed);
        var index = context.ThetaStar.ExecuteCancellable(cancellation);
        ref var node = ref context.ThetaStar.NodeByIndex(index);
        var waypoints = NavigationDecision.GetFirstWaypoints(context.ThetaStar, context.Map, index, Identity.Origin);
        var result = new NavigationDecision
        {
            Destination = waypoints.first,
            NextWaypoint = waypoints.second,
            LeewaySeconds = node.PathLeeway,
            TimeToGoal = node.GScore,
            RasterizeTime = _rasterTicks,
            PathfindTime = Stopwatch.GetTimestamp() - start
        };
        var witness = NavigationDecision.CopyWitness(context.ThetaStar, index, Identity.Origin, out var complete);
        // This publication carries native timing and a complete witness. IPC serialization and
        // raster-window limits do not determine which map cells this native snapshot supports.
        result.Publication = new(Identity, _physicalRaster, witness, complete, _relaxed, node.Score, result, HasTeleporters, _mapSource);
        return result;
    }
}
