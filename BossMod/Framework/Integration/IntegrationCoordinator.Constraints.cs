using BossMod.Pathfinding;
using System.Diagnostics;
using System.Threading;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record ConstraintRequest(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string SourceKind, float ConservativeLipschitz, string ActivationTicks, string? LandingUntilTicks = null);
    internal sealed record ConstraintCellsRequest(IntegrationSession.Context Context, string OwnerToken, string SnapshotId, string SourceKind, int[] AllowedCellIndices, string ActivationTicks, string? LandingUntilTicks = null);
    internal sealed record ConstraintPoll(IntegrationSession.Context Context, string OwnerToken, string ConstraintId);
    private sealed record ConstraintBuild(byte[] Blocks, WPos Witness);
    private sealed class ConstraintJob
    {
        internal required string Id, Owner, Parent, Kind;
        internal required Func<bool> Live;
        internal required IntegrationSession.ContributionGuard Guard;
        internal required Task<ConstraintBuild?> Work;
        internal required NavigationRaster Raster;
        internal required ShapeDistance[] Existing;
        internal required Vector4 Pose;
        internal required float Radius;
        internal required IntegrationSession.Context SourceContext;
        internal DateTime SourceWorldTime;
        internal DateTime Activation;
        internal DateTime? LandingUntil;
        internal int Retired;
        internal long AdmittedFrame;
        internal bool Current => Volatile.Read(ref Retired) == 0 && Live();
        internal void Retire() { if (Interlocked.Exchange(ref Retired, 1) == 0) Guard.Retire(); }
    }
    private readonly Dictionary<string, ConstraintJob> _constraintJobs = [];
    private Task<ConstraintBuild?>? _constraintWorker;
    private long _constraintFrame;
    private int _constraintAttempts;

    internal string SubmitConstraint(string json, Func<Vector2, float> distance)
    {
        return SubmitConstraintCore(IntegrationSession.Parse<ConstraintRequest>(json), distance, null);
    }
    internal string SubmitConstraintCells(string json)
    {
        var request = IntegrationSession.Parse<ConstraintCellsRequest>(json);
        if (request?.AllowedCellIndices is not { Length: >= 1 and <= 1024 } || request.AllowedCellIndices.Distinct().Count() != request.AllowedCellIndices.Length)
            return Unavailable("invalid-owned-constraint-cells");
        return SubmitConstraintCore(new(request.Context, request.OwnerToken, request.SnapshotId, request.SourceKind, 0, request.ActivationTicks, request.LandingUntilTicks), null, request.AllowedCellIndices.ToArray());
    }
    private string SubmitConstraintCore(ConstraintRequest? request, Func<Vector2, float>? distance, int[]? cells)
    {
        if (request == null || distance == null && cells == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready"
            || !Session.CanContribute || !ControlsAllowed() || !Session.IsOwnerActive(request.OwnerToken)
            || request.SourceKind is not ("native" or "learned") || !float.IsFinite(request.ConservativeLipschitz)
            || request.ConservativeLipschitz is < 0 or > 16) return Unavailable("invalid-owned-constraint-context");
        lock (_safetyGate)
        {
            foreach (var old in _constraintJobs.Values.ToArray())
                if (!old.Current) { old.Retire(); _constraintJobs.Remove(old.Id); }
            string Reject(string reason) => Unavailable(reason);
            var parent = SafetySource(request.Context, request.OwnerToken, request.SnapshotId);
            if (parent?.Work.IsCompletedSuccessfully != true || parent.Work.Result is not { } maps || World.Party.Player() is not { } actor)
                return Reject("owned-constraint-source-unavailable");
            if (request.SourceKind == "learned" && !parent.Displacement.NoPublishedNativeDisplacementVerified)
                return Reject("learned-constraint-native-displacement-not-excluded");
            if (request.SourceKind == "native" && (!parent.Displacement.Complete || !parent.SelectedNativeSourcesPositive || parent.NativeDeadline == null)) return Reject("native-constraint-selected-source-missing");
            if (_constraintFrame != Session.FrameId) { _constraintFrame = Session.FrameId; _constraintAttempts = 0; }
            if (++_constraintAttempts > 4 || _constraintWorker is { IsCompleted: false } || _constraintJobs.Count >= 16
                || _constraintJobs.Values.Any(j => j.Owner == request.OwnerToken && j.AdmittedFrame == 0)) return Reject("owned-constraint-work-limit");
            if (!long.TryParse(request.ActivationTicks, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var activationTicks)
                || activationTicks < World.FutureTime(request.SourceKind == "learned" ? -2 : 0).Ticks || activationTicks > World.FutureTime(30).Ticks) return Reject("owned-constraint-activation-invalid");
            var activation = new DateTime(activationTicks, World.CurrentTime.Kind);
            if (request.SourceKind == "native" && parent.NativeDeadline is { } nativeDeadline && activation > nativeDeadline) activation = nativeDeadline;
            DateTime? landingUntil = null;
            if (request.LandingUntilTicks != null)
            {
                if (request.SourceKind != "learned" || !long.TryParse(request.LandingUntilTicks, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var endTicks) || endTicks <= World.CurrentTime.Ticks
                    || endTicks <= activation.Ticks || endTicks > activation.AddSeconds(2).Ticks)
                    return Reject("learned-constraint-landing-window-invalid");
                landingUntil = new(endTicks, World.CurrentTime.Kind);
            }
            if (activation <= World.CurrentTime && (request.SourceKind != "learned" || landingUntil == null))
                return Reject("owned-constraint-activation-expired");
            var live = Session.CaptureObservationLiveness(request.OwnerToken);
            if (live == null || !float.IsFinite(actor.HitboxRadius) || actor.HitboxRadius is < 0 or > 5) return Reject("owned-constraint-owner-or-footprint-invalid");
            var existing = hints.IntegrationConstraints?.CaptureExcept(request.OwnerToken) ?? [];
            var radius = actor.HitboxRadius; var raster = maps.Preparation;
            var expires = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4;
            if (landingUntil is { } end) expires = Math.Min(expires, Stopwatch.GetTimestamp() + (long)((end - World.CurrentTime).TotalSeconds * Stopwatch.Frequency));
            var parentLive = parent.Live;
            bool Current() => live() && parentLive() && Stopwatch.GetTimestamp() < expires;
            if (cells != null && cells.Any(i => i < 0 || i >= raster.Width * raster.Height)) return Reject("owned-constraint-cell-outside-native-grid");
            var work = Task.Run(() => cells == null ? BuildConstraint(raster, existing, distance!, request.ConservativeLipschitz, radius, Current)
                : BuildConstraintCells(raster, existing, cells, radius, Current));
            var guard = new IntegrationSession.ContributionGuard(Current);
            if (Session.PairConstraintGoals(request.OwnerToken, guard) == null) { guard.Retire(); return Reject("owned-constraint-pairing-unavailable"); }
            var job = new ConstraintJob { Id = Guid.NewGuid().ToString("N"), Owner = request.OwnerToken, Parent = parent.Id, Kind = request.SourceKind,
                Live = Current, Guard = guard, Work = work, Raster = raster, Existing = existing, Pose = actor.PosRot, Radius = radius, SourceContext = parent.SourceContext, SourceWorldTime = parent.WorldTime, Activation = activation, LandingUntil = landingUntil };
            _constraintWorker = work; _constraintJobs.Add(job.Id, job);
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, constraintId = job.Id, snapshotId = parent.Id,
                status = "pending", sourceContext = parent.SourceContext, sourceWorldTimeTicks = T(parent.WorldTime), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                meaning = "detached consumer policy; pending worker result is not active or native projection approval" });
        }
    }

    private static ConstraintBuild? BuildConstraint(NavigationRaster raster, ShapeDistance[] existing, Func<Vector2, float> distance,
        float lipschitz, float radius, Func<bool> live)
    {
        var map = raster.CopyForSearch(0, IntegrationSearch.MaxCells); if (map == null) return null;
        var blocks = new byte[map.Width * map.Height]; var combined = new byte[blocks.Length];
        try
        {
            for (var y = 0; y < map.Height; ++y) for (var x = 0; x < map.Width; ++x)
            {
                if (!live()) return null;
                var index = map.GridToIndex(x, y); var clear = true; var othersClear = true;
                // Lipschitz bound proves the entire cell from its center. Corners additionally
                // detect malformed callbacks; no interpolation can turn an unknown cell clear.
                for (var point = 0; point < 5; ++point)
                {
                    var p = point == 4 ? map.GridToWorld(x, y, 0.5f, 0.5f) : map.GridToWorld(x, y, point & 1, point >> 1);
                    var started = Stopwatch.GetTimestamp(); var value = distance(new(p.X, p.Z));
                    if (!float.IsFinite(value) || Math.Abs(value) > 1_000_000 || Stopwatch.GetElapsedTime(started).TotalMilliseconds > 2 || !live()) return null;
                    clear &= value > (point == 4 ? lipschitz * map.Resolution * 0.707107f : 0);
                    foreach (var shape in existing) othersClear &= !shape.Contains(p);
                }
                blocks[index] = clear ? (byte)0 : (byte)1;
                combined[index] = !clear || !othersClear || raster.BlockAt(x, y) != 0 ? (byte)1 : (byte)0;
                map.PixelMaxG[index] = combined[index] == 0 ? float.MaxValue : -1;
            }
            NavigationDecision.RasterizeVoidzones(map, [new IntegrationConstraintMask(raster, blocks, live)]);
            for (var i = 0; i < combined.Length; ++i) if (map.PixelMaxG[i] < 0) combined[i] = 1;
            var strict = new NavigationRaster(map, null, combined);
            // Require a full footprint witness in the combined strict native/owned raster.
            // Optional advice may never rely on native least-danger reopening.
            for (var y = 0; y < map.Height; ++y) for (var x = 0; x < map.Width; ++x)
            {
                if (!live()) return null;
                if (combined[map.GridToIndex(x, y)] != 0) continue;
                var p = map.GridToWorld(x, y, 0.5f, 0.5f);
                var result = IntegrationRoutes.Evaluate(new(strict, default, [], [], default), [p], new(radius, 6, 0, 0.25f), new());
                if (result.Status == "clear") return new(blocks, p);
            }
        }
        catch { return null; }
        return null;
    }

    private static ConstraintBuild? BuildConstraintCells(NavigationRaster raster, ShapeDistance[] existing, int[] cells,
        float radius, Func<bool> live)
    {
        var map = raster.CopyForSearch(0, IntegrationSearch.MaxCells); if (map == null) return null;
        var blocks = Enumerable.Repeat((byte)1, map.Width * map.Height).ToArray();
        foreach (var cell in cells) blocks[cell] = 0;
        var combined = blocks.ToArray();
        for (var y = 0; y < map.Height; ++y) for (var x = 0; x < map.Width; ++x)
        {
            if (!live()) return null;
            var index = map.GridToIndex(x, y); var clear = blocks[index] == 0 && raster.BlockAt(x, y) == 0;
            for (var point = 0; clear && point < 5; ++point)
            {
                var p = point == 4 ? map.GridToWorld(x, y, 0.5f, 0.5f) : map.GridToWorld(x, y, point & 1, point >> 1);
                foreach (var shape in existing) if (shape.Contains(p)) { clear = false; break; }
            }
            combined[index] = clear ? (byte)0 : (byte)1;
            map.PixelMaxG[index] = clear ? float.MaxValue : -1;
        }
        NavigationDecision.RasterizeVoidzones(map, [new IntegrationConstraintMask(raster, blocks, live)]);
        for (var i = 0; i < combined.Length; ++i) if (map.PixelMaxG[i] < 0) combined[i] = 1;
        var strict = new NavigationRaster(map, null, combined);
        foreach (var cell in cells)
        {
            if (!live()) return null;
            if (combined[cell] != 0) continue;
            var p = map.GridToWorld(cell % map.Width, cell / map.Width, 0.5f, 0.5f);
            if (IntegrationRoutes.Evaluate(new(strict, default, [], [], default), [p], new(radius, 6, 0, 0.25f), new()).Status == "clear") return new(blocks, p);
        }
        return null;
    }

    private bool ConstraintCurrent(ConstraintJob job)
    {
        try
        {
            if (!job.Current || job.LandingUntil is { } end && World.CurrentTime >= end || !Session.CanContribute || !ControlsAllowed() || World.Party.Player() is not { } actor
                || actor.PosRot.Y != job.Pose.Y || job.Kind == "native" && actor.PosRot.W != job.Pose.W || actor.HitboxRadius != job.Radius) return false;
            lock (_safetyGate)
            {
                var parent = SafetySource(Session.CurrentContext, job.Owner, job.Parent);
                return parent != null && (job.Kind != "learned" || parent.Displacement.NoPublishedNativeDisplacementVerified);
            }
        }
        catch { return false; }
    }
    internal string PollConstraint(string json)
    {
        var request = IntegrationSession.Parse<ConstraintPoll>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready") return Unavailable("invalid-owned-constraint-poll");
        lock (_safetyGate)
        {
            if (!_constraintJobs.TryGetValue(request.ConstraintId, out var job) || job.Owner != request.OwnerToken) return Unavailable("owned-constraint-not-found");
            if (!ConstraintCurrent(job)) { job.Retire(); return Unavailable("owned-constraint-source-retired"); }
            if (Session.PairConstraintGoals(job.Owner, job.Guard) == null) { job.Retire(); return Unavailable("owned-constraint-owner-unavailable"); }
            if (!job.Work.IsCompleted) return IntegrationSession.Serialize(new { available = true, context = request.Context, constraintId = job.Id, status = "pending" });
            if (!job.Work.IsCompletedSuccessfully || job.Work.Result is not { } result)
            { _ = job.Work.Exception; job.Retire(); return Unavailable("owned-constraint-no-complete-clear-footprint"); }
            if (job.AdmittedFrame != Session.FrameId)
            {
                if (!(hints.IntegrationConstraints?.CaptureExcept(job.Owner) ?? []).SequenceEqual(job.Existing))
                { job.Retire(); return Unavailable("owned-constraint-combined-source-changed"); }
                hints.IntegrationConstraints ??= new();
                var shape = new IntegrationConstraintMask(job.Raster, result.Blocks, () => job.Current);
                if (!hints.IntegrationConstraints.Add(new(job.Owner, shape, job.Activation, () => ConstraintCurrent(job), job.Retire)))
                { job.Retire(); return Unavailable("owned-constraint-admission-conflict"); }
                foreach (var previous in _constraintJobs.Values.Where(j => j.Owner == job.Owner && j.Id != job.Id).ToArray()) { previous.Retire(); _constraintJobs.Remove(previous.Id); }
                job.Guard.Activate();
                job.AdmittedFrame = Session.FrameId;
            }
            return IntegrationSession.Serialize(new { accepted = true, available = true, context = request.Context, constraintId = job.Id,
                snapshotId = job.Parent, status = "admitted", timedConstraint = true, leastDangerRelaxationAllowed = false, activationTicks = T(job.Activation), landingUntilTicks = job.LandingUntil is { } until ? T(until) : null, nativeConstraintsPreserved = true,
                sourceContext = job.SourceContext, sourceWorldTimeTicks = T(job.SourceWorldTime), clearFootprintWitness = P(result.Witness), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                sourceKind = job.Kind, nativeProjectionApproved = false, expiresWithOriginalSource = true,
                meaning = "additional immutable timed cell mask; native and consumer-owned constraints remain binding; final native capture revalidates source; paired goal guard retired on failure; observer registration remains alive" });
        }
    }
}
