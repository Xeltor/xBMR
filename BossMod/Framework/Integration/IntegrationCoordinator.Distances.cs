using System.Collections;
using System.Reflection;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private sealed class DistanceSurface(ShapeDistance shape)
    {
        internal ShapeDistance? Shape = shape;
    }
    private sealed class DistanceLifetime
    {
        internal IDisposable[] Owners = [];
        internal readonly List<DistanceSurface> Surfaces = [];
        private int _revoked, _readers, _disposed;
        internal bool Enter(Func<bool> live)
        {
            if (!live()) { Retire(); return false; }
            System.Threading.Interlocked.Increment(ref _readers);
            if (System.Threading.Volatile.Read(ref _revoked) == 0 && live()) return true;
            Exit(); Retire(); return false;
        }
        internal void Exit()
        { if (System.Threading.Interlocked.Decrement(ref _readers) == 0 && System.Threading.Volatile.Read(ref _revoked) != 0) DisposeOwners(); }
        internal void Retire()
        { System.Threading.Interlocked.Exchange(ref _revoked, 1); if (System.Threading.Volatile.Read(ref _readers) == 0) DisposeOwners(); }
        private void DisposeOwners()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var owner in Owners) owner.Dispose();
            Owners = [];
            foreach (var surface in Surfaces) System.Threading.Volatile.Write(ref surface.Shape, null);
            Surfaces.Clear();
        }
        internal bool Live => System.Threading.Volatile.Read(ref _revoked) == 0;
    }
    private List<(DistanceLifetime Lifetime, Func<bool> Live)>? _distanceSources;
    private int _distanceShutdown;
    private void RetireDistanceSources(bool all = false)
    {
        if (all) System.Threading.Interlocked.Exchange(ref _distanceShutdown, 1);
        if (_distanceSources is not { } sources) return;
        lock (sources)
            foreach (var source in sources.ToArray())
                if (all || !source.Live() || !source.Lifetime.Live) { source.Lifetime.Retire(); sources.Remove(source); }
    }
    private bool DistanceCapacityAvailable()
    {
        RetireDistanceSources();
        return (_distanceSources?.Count ?? 0) < 64;
    }
    private bool TrackDistanceSource(DistanceLifetime lifetime, Func<bool> live)
    {
        if (System.Threading.Volatile.Read(ref _distanceShutdown) != 0 || !live() || !DistanceCapacityAvailable()) { lifetime.Retire(); return false; }
        var sources = _distanceSources ??= [];
        lock (sources)
        {
            if (System.Threading.Volatile.Read(ref _distanceShutdown) != 0 || !live() || sources.Count >= 64) { lifetime.Retire(); return false; }
            sources.Add((lifetime, live));
        }
        return true;
    }
    private static Func<Vector2, float> FrozenDistance(ShapeDistance frozen, Func<bool> live, DistanceLifetime lifetime)
    {
        var surface = new DistanceSurface(frozen); lifetime.Surfaces.Add(surface);
        return point =>
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || Math.Abs(point.X) > 1_000_000 || Math.Abs(point.Y) > 1_000_000) return float.NaN;
            if (!lifetime.Enter(live)) return float.NaN;
            var shape = System.Threading.Volatile.Read(ref surface.Shape);
            try
            {
                var result = shape?.Distance(new(point.X, point.Y)) ?? float.NaN;
                if (!live()) lifetime.Retire();
                return lifetime.Live ? result : float.NaN;
            }
            catch { return float.NaN; }
            finally { GC.KeepAlive(shape); lifetime.Exit(); }
        };
    }

    internal sealed record DistanceRequest(IntegrationSession.Context Context, string OwnerToken);
    private long _distanceFrame;
    private int _distanceAttempts;

    internal string CaptureConstraintDistances(string json, Action<string, Func<Vector2, float>[]> receive)
    {
        var request = IntegrationSession.Parse<DistanceRequest>(json);
        if (request == null || receive == null || !Session.Matches(request.Context) || !Session.IsOwnerActive(request.OwnerToken)
            || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor)
            return Unavailable("invalid-native-distance-context");
        if (_distanceFrame != Session.FrameId) { _distanceFrame = Session.FrameId; _distanceAttempts = 0; }
        if (!DistanceCapacityAvailable() || ++_distanceAttempts > 4 || hints.ForbiddenZones.Count + hints.TemporaryObstacles.Count > 128)
            return Unavailable("native-distance-work-limit");
        var live = Session.CaptureObservationLiveness(request.OwnerToken);
        if (live == null) return Unavailable("expired-native-distance-owner");
        var budget = new IntegrationRoutes.Budget(); using var freezer = new IntegrationShapeSnapshot(budget);
        var functions = new List<Func<Vector2, float>>(); var regions = new List<object>();
        var lifetime = new DistanceLifetime();
        try
        {
            bool Add(ShapeDistance original, DateTime? activation, ulong source, string kind)
            {
                var frozen = freezer.Freeze(original);
                if (frozen == null || !budget.Step()) return false;
                var bound = frozen.ConservativeLipschitz;
                var index = functions.Count;
                functions.Add(FrozenDistance(frozen, live, lifetime));
                regions.Add(new
                {
                    distanceIndex = index, kind, activationTicks = activation is { } at ? T(at) : null,
                    secondsFromWorldTime = activation is { } when ? (double?)(when - World.CurrentTime).TotalSeconds : null,
                    sourceActorId = source == 0 ? null : source.ToString("X"),
                    conservativeLipschitz = float.IsFinite(bound) && bound >= 0 ? (float?)bound : null,
                    meaning = "exact frozen native distance; null bound permits no continuous clearance proof"
                });
                return true;
            }
            foreach (var zone in hints.ForbiddenZones)
                if (!Add(zone.shapeDistance, zone.activation, zone.Source, "forbidden")) return Unavailable("native-distance-freeze-incomplete");
            foreach (var obstacle in hints.TemporaryObstacles)
                if (!Add(obstacle, null, 0, "temporary-obstacle")) return Unavailable("native-distance-freeze-incomplete");
            var carrier = false;
            var module = bossmod.ActiveModule;
            if (module != null)
            {
                if (module.Components.Count > 128) return Unavailable("native-carrier-component-limit");
                object? Member(object value, string name)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var type = value.GetType();
                    return type.GetField(name, flags)?.GetValue(value) ?? type.GetProperty(name, flags)?.GetValue(value);
                }
                foreach (var component in module.Components)
                {
                    if (!budget.Step()) return Unavailable("native-carrier-budget");
                    carrier |= Member(component, "Target") is Actor target && target.InstanceID == actor.InstanceID;
                    foreach (var name in new[] { "Stacks", "Spreads" })
                        if (Member(component, name) is IList entries)
                        {
                            if (entries.Count > 64) return Unavailable("native-carrier-entry-limit");
                            foreach (var entry in entries)
                            {
                                if (!budget.Step()) return Unavailable("native-carrier-budget");
                                carrier |= entry != null && Member(entry, "Target") is Actor t && t.InstanceID == actor.InstanceID;
                            }
                        }
                }
            }
            if (!live() || !Session.Matches(request.Context) || !budget.Step()) return Unavailable("native-distance-capture-retired");
            lifetime.Owners = freezer.TakeOwned();
            if (!TrackDistanceSource(lifetime, live)) return Unavailable("native-distance-live-source-limit");
            var snapshotId = Guid.NewGuid().ToString("N");
            var metadata = IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, snapshotId, actorId = actor.InstanceID.ToString("X"),
                worldTimeTicks = T(World.CurrentTime), worldTimeKind = World.CurrentTime.Kind.ToString(), regions, regionsComplete = true, nativeAuthoredConstraintsOnly = true, ownedProjectedConstraintsIncluded = false, carrier,
                carrierMeaning = "same-native-publication direct target and stack/spread target facts",
                modulePresent = module != null, encounterForcedDirection = V(_encounterMovement),
                sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                meaning = "immutable native geometry only; delegates return NaN on source/owner/clear/scope expiry; no route or gameplay approval"
            });
            receive(metadata, [.. functions]);
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, snapshotId, regionCount = functions.Count });
        }
        catch (Exception error)
        {
            lifetime.Retire();
            Service.LogVerbose($"[Integration] Native distance capture failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-distance-capture-failed");
        }
    }
}
