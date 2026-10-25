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
    private readonly FrozenGoalField? _stackGathering;
    private readonly NativeRecoveryHazards? _recoveryHazards;
    private readonly float[] _presentation;
    // Optional native physical-classification evidence for direct geometry queries.
    private readonly NavigationRaster? _physicalRaster;
    private readonly NavigationMapSource? _mapSource;
    internal NavigationIdentity Identity { get; }
    internal int Cells => checked(_map.Width * _map.Height);
    internal bool HasTeleporters => !_teleporters.IsEmpty;
    internal bool HasStackGathering => _stackGathering != null;
    internal ImmutableArray<PlanningTeleport> Teleports { get; }
    internal bool SameTransitions(NativeNavigationSnapshot previous) => Teleports.Length == previous.Teleports.Length
        && Teleports.Select((t, i) => t.Same(previous.Teleports[i], (float)(Identity.WorldTime - previous.Identity.WorldTime).TotalSeconds)).All(same => same);

    private NativeNavigationSnapshot(NavigationDecision.CapturedInput input, IEnumerable<BossMod.Pathfinding.NativePhysicalObstacle>? observedBlocks)
    {
        if (!float.IsFinite(input.Identity.Speed) || input.Identity.Speed <= 0)
            throw new ArgumentException("Navigation speed must be positive and finite.", nameof(input));
        var start = Stopwatch.GetTimestamp();
        Identity = input.Identity;
        // Freeze goals once below; recovery applies that same field through its
        // original cast/urgent-warning gate instead of invoking live goals twice.
        var raster = NavigationDecision.RasterizeInput(_map, input with { GoalsEnabled = false }, true, freezeCallbacks: true);
        foreach (var obstacle in observedBlocks ?? [])
            obstacle.Apply(_map, raster.Unrelaxed, raster.Blocks, input.Identity.Origin);
        _strictLimits = ImmutableArray.Create<float>((raster.Unrelaxed ?? _map.PixelMaxG).AsSpan(0, Cells));
        _teleporters = input.Teleporters.ToImmutableArray();
        Teleports = PlanningTeleport.Capture(_teleporters).ToImmutableArray();
        _relaxed = raster.Relaxed;
        // Native recovery keeps its original casting/urgent-warning goal gate.
        // The joint search must evaluate the useful endpoint before that first
        // recovery move, including when casting finishes later in the route.
        if (input.GoalsEnabled)
        {
            _goals = FrozenGoalField.Capture(_map, input.Goals.Native, captureLeaves: false);
            var gathering = input.Goals.Native.Where(goal => goal.Target is NativeStackGathering.Goal).ToArray();
            if (gathering.Length != 0)
                _stackGathering = FrozenGoalField.Capture(_map, gathering, captureLeaves: false);
        }
        if (input.GoalsEnabled && !input.Identity.Casting)
        {
            var origin = _map.WorldToGrid(input.Identity.Origin);
            if (!_map.InBounds(origin.x, origin.y) || _map.PixelMaxG[_map.GridToIndex(origin)] >= 1f)
            {
                if (input.Goals.Native.Length != 0) _goals!.ApplyNativePriorities(_map, preserveUnscored: true);
                if (input.Identity.AvoidanceBuffer > 0) NavigationDecision.AvoidForbiddenZone(_map, input.Identity.AvoidanceBuffer);
            }
        }
        _presentation = new float[Cells];
        if (input.GoalsEnabled && input.Identity.AvoidanceBuffer > 0 && HasForbiddenCells(_map))
        {
            var strict = CopyStrictMap();
            var nativePriorities = strict.PixelPriority.AsSpan(0, Cells).ToArray();
            NavigationDecision.AvoidForbiddenZone(strict, input.Identity.AvoidanceBuffer);
            for (var i = 0; i < Cells; ++i)
                _presentation[i] = strict.PixelPriority[i] - nativePriorities[i];
        }
        if (input.PublishRaster && raster.Blocks != null)
        {
            _physicalRaster = new(_map, raster.Unrelaxed, raster.Blocks);
            _mapSource = input.MapSource;
        }
        var recoveryOrigin = _map.WorldToGrid(input.Identity.Origin);
        if (input.Warnings.Length >= 2 && _map.InBounds(recoveryOrigin.x, recoveryOrigin.y)
            && _strictLimits[_map.GridToIndex(recoveryOrigin)] < float.MaxValue)
            _recoveryHazards = NativeRecoveryHazards.Capture(_map, input);
        _rasterTicks = Stopwatch.GetTimestamp() - start;
    }

    internal static NativeNavigationSnapshot Capture(NavigationDecision.CapturedInput input, IEnumerable<BossMod.Pathfinding.NativePhysicalObstacle>? observedBlocks = null) => new(input, observedBlocks);

    internal Map CopyStrictMap()
    {
        var map = new Map();
        map.Init(_map, _map.Center);
        _strictLimits.CopyTo(map.PixelMaxG);
        _goals?.ApplyNativePriorities(map);
        return map;
    }

    internal float[] CopyPresentation() => _presentation.ToArray();

    internal bool ImprovesStackGathering(WPos origin, WPos destination)
    {
        if (_stackGathering == null) return false;
        var from = _map.WorldToGrid(origin);
        var to = _map.WorldToGrid(destination);
        return _map.InBounds(from.x, from.y) && _map.InBounds(to.x, to.y)
            && _stackGathering.Aggregate(_map.GridToIndex(to)) > _stackGathering.Aggregate(_map.GridToIndex(from));
    }

    // AvoidForbiddenZone cannot alter priorities when every cell is unrestricted.
    // This checks the freshly captured map, including physical blocked cells.
    internal static bool HasForbiddenCells(Map map)
    {
        var cells = checked(map.Width * map.Height);
        for (var i = 0; i < cells; ++i)
            if (map.PixelMaxG[i] != float.MaxValue) return true;
        return false;
    }

    internal bool SameFrozenMap(NativeNavigationSnapshot other)
        => SameSafetyMap(other) && _map.PixelPriority.AsSpan(0, Cells).SequenceEqual(other._map.PixelPriority.AsSpan(0, Cells));

    // Rotation goals may change during a committed escape. Only changed native
    // geometry, deadlines or transitions can retire that mechanic commitment.
    internal bool SameSafetyMap(NativeNavigationSnapshot other)
    {
        var age = (float)(Identity.WorldTime - other.Identity.WorldTime).TotalSeconds;
        if (age < 0 || _map.Width != other._map.Width || _map.Height != other._map.Height || _map.Center != other._map.Center
            || _map.Rotation != other._map.Rotation || _map.Resolution != other._map.Resolution
            || !SameTransitions(other) || (_recoveryHazards == null) != (other._recoveryHazards == null)
            || _recoveryHazards != null && !_recoveryHazards.Same(other._recoveryHazards!, age)) return false;
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
        // Recover already searched THIS frozen map from THIS exact position.
        // Native ThetaStar deliberately permits egress from an occupied blocked
        // raster cell (including obstacle inflation or an out-of-bounds start).
        // Applying elective traversal to that freshly computed recovery would
        // reject the starting cell and permanently suppress native escape.
        // The caller still checks real collision and execution authority.
        if (publication.Source == Identity && current == Identity.Origin) return true;
        var duration = (destination - current).Length() / Identity.Speed;
        // Emergency egress may intentionally be the least harmful route from an
        // already unsafe position. Physical obstructions still remain binding.
        var strict = Enum.TryParse<ThetaStar.Score>(publication.NativeScore, out var score) && score >= ThetaStar.Score.Safe;
        return TimedTraversal.Check(_map, current, destination, 0, duration, 0, out _, physicalOnly: !strict);
    }

    internal Map CopyRecoveryMap(Map? reuse = null)
    {
        var map = reuse ?? new Map();
        map.Init(_map, _map.Center);
        return map;
    }

    // A mobility landing inside a warning is useful only with a complete escape.
    // Reuse native ThetaStar to supply an early witness; the joint search must
    // re-prove it with the route's actual travel, action and assignment state.
    internal WPos[] ContinuationWitness(WPos origin, float elapsed, System.Threading.CancellationToken cancellation, float? speed = null)
    {
        var movementSpeed = speed ?? Identity.Speed;
        if (HasTeleporters || !float.IsFinite(elapsed) || elapsed < 0 || !float.IsFinite(movementSpeed) || movementSpeed <= 0)
            return [];
        var map = CopyStrictMap();
        for (var cell = 0; cell < Cells; ++cell)
            if (map.PixelMaxG[cell] is >= 0 and < float.MaxValue)
                map.PixelMaxG[cell] = Math.Max(0, map.PixelMaxG[cell] - elapsed);
        map.MaxG = Math.Max(0, map.MaxG - elapsed);
        var search = new ThetaStar();
        search.Start(map, origin, 1f / movementSpeed);
        var index = search.ExecuteCancellable(cancellation);
        return NavigationDecision.CopyWitness(search, index, origin, out var complete) is { } witness && complete ? witness : [];
    }

    internal NavigationDecision Recover(NavigationDecision.Context context, System.Threading.CancellationToken cancellation = default)
    {
        var original = RecoverMap(_map);
        return _recoveryHazards?.Improve(_map, Identity, original, RecoverMap, cancellation) ?? original;

        NavigationDecision RecoverMap(Map source)
        {
            cancellation.ThrowIfCancellationRequested();
            context.Map.Init(source, source.Center);
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
}
