using BossMod.Components;
using BossMod.Pathfinding;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record SafetyCapture(IntegrationSession.Context Context, string OwnerToken, string[] OmitComponentIds);
    internal sealed record SafetyPoll(IntegrationSession.Context Context, string OwnerToken, string SnapshotId);
    internal sealed record SafetyWindow(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string Kind,
        int Left, int Top, int Width, int Height);
    private sealed record PhysicalRow(int Offset, byte[] Data);
    private sealed class SafetyJob
    {
        internal required string Id, Owner, Scope;
        internal required IntegrationSession.Context SourceContext;
        internal required Actor Actor;
        internal float ActorHeight, ActorRadius;
        internal BossModule? Module;
        internal required DateTime WorldTime;
        internal DateTime? NativeDeadline;
        internal required string[] SelectedNativeComponentIds;
        internal bool SelectedNativeSourcesPositive;
        internal required Func<bool> Live;
        internal required IntegrationShapeSnapshot Copier;
        internal required NavigationMapSource Map;
        internal required (ShapeDistance shapeDistance, DateTime activation, ulong Source)[] Originals;
        internal required IntegrationSafetyRaster.Region[] Preparation, Landing;
        internal required ShapeDistance[] OriginalObstacles, Obstacles;
        internal required Bitmap.Region OriginalPhysical;
        internal required PhysicalRow[] PhysicalRows;
        internal required bool DisabledObstacles;
        internal required object[] Omitted;
        internal required IntegrationDisplacementObservation.Result Displacement;
        internal required string DisplacementStamp;
        internal required Task<IntegrationSafetyRaster.Pair?> Work;
        internal required Action Cancel;
        internal int Retired;
        internal void Retire()
        {
            if (System.Threading.Interlocked.Exchange(ref Retired, 1) != 0) return;
            Cancel();
            _ = Work.ContinueWith(t => { _ = t.Exception; Copier.Dispose(); }, TaskScheduler.Default);
        }
    }
    private readonly Dictionary<string, SafetyJob> _safetyJobs = [];
    private readonly object _safetyGate = new();
    private Task<IntegrationSafetyRaster.Pair?>? _safetyWorker;
    private long _safetyFrame;
    private int _safetyAttempts;
    private static ShapeDistance? SeparatePreparation(ShapeDistance original, ShapeDistance frozen, List<string> paths,
        IntegrationRoutes.Budget budget, string path = "root", int depth = 0)
    {
        if (!budget.Step() || depth > 8) throw new InvalidOperationException("native-projection-tree-limit");
        if (original is IKnockbackPreparationConstraint) { paths.Add(path); return IntegrationUnprojectedConstraint.Create(frozen); }
        if (original is not SDUnion union || frozen is not SDUnion copied) return frozen;
        if (union.Operands.Length != copied.Operands.Length || union.Operands.Length > 128) throw new InvalidOperationException("native-projection-tree-mismatch");
        var remaining = new List<ShapeDistance>();
        for (var i = 0; i < union.Operands.Length; ++i)
        {
            var child = SeparatePreparation(union.Operands[i], copied.Operands[i], paths, budget, path + "/union[" + i + "]", depth + 1);
            if (child != null) remaining.Add(child);
        }
        return remaining.Count switch { 0 => null, 1 => remaining[0], _ => new SDUnion([.. remaining]) };
    }
    private void DisposeSafetySnapshots()
    {
        lock (_safetyGate) { foreach (var job in _safetyJobs.Values) job.Retire(); _safetyJobs.Clear();
            if (_safetyRoutes != null) { foreach (var route in _safetyRoutes.Values) _ = route.Work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); _safetyRoutes.Clear(); } }
    }
    private void PruneSafetySnapshots()
    {
        foreach (var job in _safetyJobs.Values.ToArray())
            if (!job.Live() || !Session.ProviderAlive || job.Scope != ControlScope() || !ReferenceEquals(job.Actor, World.Party.Player())
                || !ReferenceEquals(job.Module, bossmod.ActiveModule)) { _safetyJobs.Remove(job.Id); job.Retire(); }
    }
    private SafetyJob? SafetySource(IntegrationSession.Context context, string owner, string id)
    {
        if (!Session.Matches(context) || !ControlsAllowed()) return null;
        PruneSafetySnapshots();
        if (!_safetyJobs.TryGetValue(id, out var job) || job.Owner != owner || !job.Live()) return null;
        var budget = new IntegrationRoutes.Budget(evaluations: 65536);
        var valid = job.Actor.PosRot.Y == job.ActorHeight && job.Actor.HitboxRadius == job.ActorRadius
            && ReferenceEquals(hints.PathfindMapBounds, job.Map.Bounds) && hints.PathfindMapCenter == job.Map.Center
            && hints.PathfindMapArenaProjectionLayer == job.Map.ProjectionLayer && hints.ObstacleMapsDisabled == job.DisabledObstacles
            && ReferenceEquals(hints.PathfindMapObstacles.Bitmap, job.OriginalPhysical.Bitmap) && hints.PathfindMapObstacles.Rect.Equals(job.OriginalPhysical.Rect)
            && hints.ForbiddenZones.Count == job.Originals.Length && hints.TemporaryObstacles.Count == job.Obstacles.Length;
        for (var i = 0; valid && i < job.Preparation.Length; ++i)
        {
            var current = hints.ForbiddenZones[i]; var frozen = job.Preparation[i];
            valid = current.activation == frozen.Activation && current.Source == frozen.Source && job.Copier.Matches(current.shapeDistance, frozen.Shape, budget);
        }
        for (var i = 0; valid && i < job.Obstacles.Length; ++i) valid = job.Copier.Matches(hints.TemporaryObstacles[i], job.Obstacles[i], budget);
        if (valid && job.OriginalPhysical.Bitmap != null)
            foreach (var row in job.PhysicalRows)
                if (!budget.Step() || !job.OriginalPhysical.Bitmap.Pixels.AsSpan(row.Offset, row.Data.Length).SequenceEqual(row.Data)) { valid = false; break; }
        if (valid) valid = IntegrationSession.Serialize(IntegrationDisplacementObservation.Capture(job.Module, job.Actor, hints, budget)) == job.DisplacementStamp;
        if (valid && budget.Step()) return job;
        _safetyJobs.Remove(job.Id); job.Retire(); return null;
    }
    internal string StartSafetySnapshot(string json)
    {
        var request = IntegrationSession.Parse<SafetyCapture>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready" || !Session.CanContribute
            || !ControlsAllowed() || !Session.IsOwnerActive(request.OwnerToken) || request.OmitComponentIds is not { Length: <= 8 }
            || request.OmitComponentIds.Any(string.IsNullOrWhiteSpace) || request.OmitComponentIds.Distinct().Count() != request.OmitComponentIds.Length
            || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor || hints.ForbiddenZones.Count + hints.TemporaryObstacles.Count > 128)
            return Unavailable("invalid-native-safety-snapshot-context");
        lock (_safetyGate)
        {
            PruneSafetySnapshots();
            if (_safetyFrame != Session.FrameId) { _safetyFrame = Session.FrameId; _safetyAttempts = 0; }
            if (++_safetyAttempts > 4 || _safetyWorker is { IsCompleted: false } || _safetyRouteWorker is { IsCompleted: false } || _safetyJobs.Count >= 4
                || _safetyJobs.Values.Count(j => j.Owner == request.OwnerToken) >= 2) return Unavailable("native-safety-snapshot-busy");
            var sourceLive = Session.CaptureObservationLiveness(request.OwnerToken);
            if (sourceLive == null) return Unavailable("native-safety-owner-retired");
            var retired = 0;
            bool Live() => System.Threading.Volatile.Read(ref retired) == 0 && sourceLive();
            Action cancel = () => System.Threading.Interlocked.Exchange(ref retired, 1);
            Func<bool> live = Live;
            var budget = new IntegrationRoutes.Budget(); var copier = new IntegrationShapeSnapshot(budget);
            var transferred = false;
            try
            {
                var bounds = hints.PathfindMapBounds;
                if (!float.IsFinite(bounds.MapResolution) || bounds.MapResolution <= 0 || !float.IsFinite(bounds.Radius)
                    || bounds.Radius <= 0 || bounds.Radius / bounds.MapResolution > 64
                    || bounds is ABRect rect && (rect.HalfWidth / bounds.MapResolution > 64 || rect.HalfHeight / bounds.MapResolution > 64)
                    || bounds is ArenaBoundsCustom custom && (custom.HalfWidth / bounds.MapResolution > 64 || custom.HalfHeight / bounds.MapResolution > 64)) return Unavailable("native-safety-map-limit");
                var region = hints.PathfindMapObstacles;
                if (region.Bitmap != null && (long)region.Rect.Width * region.Rect.Height > IntegrationSearch.MaxCells) return Unavailable("native-safety-physical-limit");
                var map = hints.CapturePathfindMapSource();
                if (bounds.AllowObstacleMap && map.Obstacles.Bitmap == null) return Unavailable("native-safety-physical-coverage-missing");
                var module = bossmod.ActiveModule;
                var displacement = IntegrationDisplacementObservation.Capture(module, actor, hints, budget);
                var selected = new HashSet<BossComponent>();
                if (request.OmitComponentIds.Length != 0)
                {
                    if (module == null || module.Components.Count > 64 || hints.ProjectionSourcesContext is not { } captured
                        || captured.FrameId != request.Context.FrameId || captured.InstanceGeneration != request.Context.InstanceGeneration
                        || !hints.ProjectionSourcesValid || !hints.ProjectionSourcesMatch(module, actor)) return Unavailable("native-safety-projection-attribution-missing");
                    foreach (var id in request.OmitComponentIds)
                    {
                        var component = module.Components.OfType<GenericKnockback>().FirstOrDefault(c => IntegrationMechanics.ComponentId(c) == id);
                        if (component == null) return Unavailable("native-safety-projection-component-missing"); selected.Add(component);
                    }
                }
                DateTime? nativeDeadline = null;
                var selectedPositive = selected.Count != 0;
                foreach (var selectedComponent in selected.OfType<GenericKnockback>())
                {
                    var sources = selectedComponent.ActiveKnockbacks(module!.Raid.FindSlot(actor.InstanceID), actor);
                    selectedPositive &= sources.Length != 0;
                    if (sources.Length > 32 || !budget.Step()) return Unavailable("native-safety-displacement-source-limit");
                    foreach (ref readonly var source in sources)
                    {
                        if (!budget.Step()) return Unavailable("native-safety-displacement-source-budget");
                        if (source.Activation != default && (nativeDeadline == null || source.Activation < nativeDeadline)) nativeDeadline = source.Activation;
                    }
                }
                var originals = hints.ForbiddenZones.ToArray(); var all = new List<IntegrationSafetyRaster.Region>();
                var landing = new List<IntegrationSafetyRaster.Region>(); var omitted = new List<object>();
                for (var i = 0; i < originals.Length; ++i)
                {
                    var original = originals[i]; var shape = copier.Freeze(original.shapeDistance);
                    if (shape == null || !budget.Step()) return Unavailable("native-safety-geometry-copy-incomplete");
                    var copied = new IntegrationSafetyRaster.Region(shape, original.activation, original.Source); all.Add(copied);
                    var producer = hints.ProjectionProducer(original.shapeDistance, original.activation, original.Source);
                    if (producer == null && hints.ProjectionSourceWasRecorded(original.shapeDistance)) return Unavailable("native-safety-projection-attribution-changed");
                    var paths = new List<string>();
                    var independent = producer != null && selected.Contains(producer)
                        ? SeparatePreparation(original.shapeDistance, shape, paths, budget) : shape;
                    foreach (var path in paths)
                        omitted.Add(new { nativeRegionIndex = i, treePath = path, componentId = IntegrationMechanics.ComponentId(producer!), activationTicks = T(original.activation),
                            sourceActorId = original.Source == 0 ? null : original.Source.ToString("X"), meaning = "native producer-attributed displacement suppressed on frozen landing view; native embedded hazards, envelopes, direct avoid rules and independent siblings retained" });
                    if (independent != null) landing.Add(copied with { Shape = independent });
                }
                var originalObstacles = hints.TemporaryObstacles.ToArray(); var obstacles = new List<ShapeDistance>();
                foreach (var original in originalObstacles)
                {
                    var shape = copier.Freeze(original); if (shape == null || !budget.Step()) return Unavailable("native-safety-obstacle-copy-incomplete"); obstacles.Add(shape);
                }
                var physicalRows = new List<PhysicalRow>();
                if (region.Bitmap != null)
                {
                    var clipped = region.Rect.Clamped(region.Bitmap.FullRect); var left = clipped.Left >> 3; var length = ((clipped.Right + 7) >> 3) - left;
                    for (var y = clipped.Top; y < clipped.Bottom; ++y)
                    {
                        if (!budget.Step()) return Unavailable("native-safety-physical-copy-budget");
                        var offset = y * region.Bitmap.BytesPerRow + left;
                        physicalRows.Add(new(offset, region.Bitmap.Pixels.AsSpan(offset, length).ToArray()));
                    }
                }
                if (!live() || !budget.Step()) return Unavailable("native-safety-capture-retired");
                var prep = all.ToArray(); var land = landing.ToArray(); var temporary = obstacles.ToArray(); var time = World.CurrentTime;
                var work = Task.Run(() => IntegrationSafetyRaster.Build(map, prep, land, temporary, time, live));
                var job = new SafetyJob { Id = Guid.NewGuid().ToString("N"), Owner = request.OwnerToken, Scope = ControlScope(), SourceContext = request.Context,
                    Actor = actor, ActorHeight = actor.PosRot.Y, ActorRadius = actor.HitboxRadius, Module = module, WorldTime = time, NativeDeadline = nativeDeadline, SelectedNativeComponentIds = request.OmitComponentIds.ToArray(), SelectedNativeSourcesPositive = selectedPositive, Live = live, Copier = copier, Map = map, Originals = originals,
                    Preparation = prep, Landing = land, OriginalObstacles = originalObstacles, Obstacles = temporary, OriginalPhysical = region,
                    Cancel = cancel, PhysicalRows = [.. physicalRows], DisabledObstacles = hints.ObstacleMapsDisabled, Omitted = [.. omitted], Displacement = displacement, DisplacementStamp = IntegrationSession.Serialize(displacement), Work = work };
                _safetyWorker = work; _safetyJobs.Add(job.Id, job); transferred = true;
                return IntegrationSession.Serialize(new { accepted = true, context = request.Context, snapshotId = job.Id, status = "pending",
                    sourceContext = job.SourceContext, worldTimeTicks = T(time), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs });
            }
            catch (Exception error) { Service.LogVerbose($"[Integration] Native safety snapshot failed: {error.GetType().Name}"); return Unavailable("native-safety-capture-failed"); }
            finally { if (!transferred) copier.Dispose(); }
        }
    }
    internal string PollSafetySnapshot(string json)
    {
        var request = IntegrationSession.Parse<SafetyPoll>(json);
        lock (_safetyGate)
        {
            var job = request == null ? null : SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            if (job == null) return Unavailable("native-safety-source-retired-or-changed");
            if (!job.Work.IsCompleted) return IntegrationSession.Serialize(new { available = true, context = request!.Context, snapshotId = job.Id, status = "pending" });
            if (!job.Work.IsCompletedSuccessfully || job.Work.Result is not { } result)
            { _safetyJobs.Remove(job.Id); job.Retire(); return Unavailable("native-safety-worker-incomplete"); }
            object Describe(NavigationRaster raster) => new { raster.Width, raster.Height, raster.Resolution, center = P(raster.Center), rotation = raster.Rotation.Rad };
            return IntegrationSession.Serialize(new { available = true, context = request!.Context, snapshotId = job.Id, status = "ready", sourceContext = job.SourceContext,
                actorId = job.Actor.InstanceID.ToString("X"), worldTimeTicks = T(job.WorldTime), worldTimeKind = job.WorldTime.Kind.ToString(),
                sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs, projectionLayer = job.Map.ProjectionLayer,
                selectedNativeComponentIds = job.SelectedNativeComponentIds, selectedNativeSourcesPositive = job.SelectedNativeSourcesPositive,
                nativeDisplacementDeadlineTicks = job.NativeDeadline is { } deadline ? T(deadline) : null,
                preparation = Describe(result.Preparation), landing = Describe(result.Landing), timedPreparation = Describe(result.TimedPreparation), preparationWarningTimeLimitsRetained = true, omittedProjectionRegions = job.Omitted, displacement = job.Displacement,
                nativePreparationRelaxationPrevented = result.PreparationRelaxed, nativeLandingRelaxationPrevented = result.LandingRelaxed,
                complete = true, strictUnion = true, nativeAuthoredConstraintsOnly = true, ownedProjectedConstraintsIncluded = false, embeddedLandingConstraintsRetained = true, meaning = "copied exact native raster/corner/center constraints with original pre-relaxation limits; preparation retains all native regions, landing uses native unprojected bodies only for attributed preparation constraints and retains embedded hazards; no projection, route or action approval" });
        }
    }
    internal string GetSafetyWindow(string json)
    {
        var request = IntegrationSession.Parse<SafetyWindow>(json);
        lock (_safetyGate)
        {
            var job = request == null ? null : SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            if (job?.Work.IsCompletedSuccessfully != true || job.Work.Result is not { } result || request!.Kind is not ("preparation" or "landing" or "timed-preparation"))
                return Unavailable("native-safety-window-source-unavailable");
            var raster = request.Kind == "preparation" ? result.Preparation : request.Kind == "timed-preparation" ? result.TimedPreparation : result.Landing;
            var window = raster.CopyWindow(request.Left, request.Top, request.Width, request.Height);
            return window == null ? Unavailable("native-safety-window-bounds") : IntegrationSession.Serialize(new { available = true, context = request.Context,
                snapshotId = job.Id, request.Kind, sourceContext = job.SourceContext, worldTimeTicks = T(job.WorldTime), strictUnion = true,
                blockCodes = new { clear = 0, arena = 1, physical = 2, temporary = 3, warningOrUnproven = 4 }, window });
        }
    }
}
