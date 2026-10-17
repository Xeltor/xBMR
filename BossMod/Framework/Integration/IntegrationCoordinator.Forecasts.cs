using System.Reflection;
using System.Collections;
using BossMod.Components;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private static readonly MethodInfo StandardAoeAI = typeof(GenericAOEs).GetMethod(nameof(GenericAOEs.AddAIHints))!;
    private static readonly MethodInfo StandardRotatingPublication = typeof(GenericRotatingAOE).GetMethod(nameof(GenericAOEs.ActiveAOEs))!;

    // This queue can hide a separate imminent axis behind its published prefix.
    // Expose that factual limitation without changing the native publication or hints.
    private static bool ForecastQueueComplete(BossComponent component, ReadOnlySpan<GenericAOEs.AOEInstance> active)
    {
        if (component.GetType().FullName != "BossMod.Dawntrail.Foray.FATE.WavedAway.StormWave") return true;
        IList? Queue(string name) => component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(component) as IList;
        var cardinal = Queue("_cardinal"); var diagonal = Queue("_intercardinal");
        static bool Parallel(IList? queue)
        {
            if (queue == null || queue.Count > 9) return false;
            float? axis = null;
            foreach (var item in queue)
            {
                if (item is not GenericAOEs.AOEInstance a || !float.IsFinite(a.Rotation.Rad)) return false;
                if (axis is { } r && MathF.Abs(MathF.IEEERemainder(a.Rotation.Rad - r, MathF.PI)) > 0.002f) return false;
                axis ??= a.Rotation.Rad;
            }
            return true;
        }
        static bool Contains(IList queue, GenericAOEs.AOEInstance a)
        {
            foreach (var item in queue)
                if (item is GenericAOEs.AOEInstance q && q.Activation == a.Activation && q.Origin == a.Origin
                    && q.Rotation == a.Rotation && ReferenceEquals(q.Shape, a.Shape)) return true;
            return false;
        }
        if (!Parallel(cardinal) || !Parallel(diagonal)) return false;
        foreach (ref readonly var a in active)
            if (!Contains(cardinal!, a) && !Contains(diagonal!, a)) return false;
        return true;
    }

    private long _forecastFrame;
    private int _forecastAttempts;

    internal string CaptureForecastDistances(string json, Action<string, Func<Vector2, float>[]> receive)
    {
        var request = IntegrationSession.Parse<DistanceRequest>(json);
        if (request == null || receive == null || !Session.Matches(request.Context) || !Session.IsOwnerActive(request.OwnerToken)
            || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor)
            return Unavailable("invalid-native-forecast-context");
        if (_forecastFrame != Session.FrameId) { _forecastFrame = Session.FrameId; _forecastAttempts = 0; }
        if (++_forecastAttempts > 4 || hints.ForbiddenZones.Count > 128) return Unavailable("native-forecast-work-limit");
        var live = Session.CaptureObservationLiveness(request.OwnerToken);
        if (live == null) return Unavailable("expired-native-forecast-owner");
        var module = bossmod.ActiveModule;
        if (module?.Components.Count > IntegrationMechanics.MaxComponents) return Unavailable("native-forecast-component-limit");
        var budget = new IntegrationRoutes.Budget();
        using var freezer = new IntegrationShapeSnapshot(budget);
        var functions = new List<Func<Vector2, float>>();
        var publications = new List<object>(); var components = new List<object>();
        var lifetime = new DistanceLifetime();
        try
        {
            if (module != null)
            {
                var slot = module.Raid.FindSlot(actor.InstanceID);
                if (slot < 0) return Unavailable("actor-not-in-native-party");
                foreach (var component in module.Components)
                {
                    if (!budget.Step()) return Unavailable("native-forecast-budget");
                    if (component is not GenericAOEs aoes) continue;
                    var id = IntegrationMechanics.ComponentId(component);
                    var active = aoes.ActiveAOEs(slot, actor);
                    if (active.Length > IntegrationMechanics.MaxEntries - publications.Count) return Unavailable("native-forecast-entry-limit");
                    var queueComplete = ForecastQueueComplete(component, active);
                    for (var i = 0; i < active.Length; ++i)
                    {
                        ref readonly var a = ref active[i];
                        if (!budget.Step()) return Unavailable("native-forecast-budget");
                        var original = a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation);
                        var matches = new List<int>();
                        for (var zoneIndex = 0; zoneIndex < hints.ForbiddenZones.Count; ++zoneIndex)
                        {
                            var zone = hints.ForbiddenZones[zoneIndex];
                            if (!budget.Step()) return Unavailable("native-forecast-attribution-budget");
                            if (zone.activation == a.Activation && IntegrationShapeSnapshot.SameNativeGeometry(original, zone.shapeDistance, budget)) matches.Add(zoneIndex);
                        }
                        var frozen = freezer.Freeze(original);
                        if (frozen == null) return Unavailable("native-forecast-freeze-incomplete");
                        var bound = frozen.ConservativeLipschitz;
                        functions.Add(FrozenDistance(frozen, live, lifetime));
                        publications.Add(new
                        {
                            distanceIndex = functions.Count - 1, componentId = id, pulseIndex = i, publishedPulseCount = active.Length,
                            activationTicks = T(a.Activation), secondsFromWorldTime = (a.Activation - World.CurrentTime).TotalSeconds,
                            risky = a.Risky, color = a.Color, nativeHintRegionIndices = matches,
                            matchesCurrentNativeConstraint = matches.Count != 0,
                            attributionMeaning = "exact native geometry and activation equality against current forbidden region order; not timer-only or component producer attribution",
                            sourceActorId = a.ActorID == 0 ? null : a.ActorID.ToString("X"),
                            distanceOverride = a.ShapeDistance != null,
                            conservativeLipschitz = float.IsFinite(bound) && bound >= 0 ? (float?)bound : null,
                            arenaProjectionLayer = a.ArenaProjectionLayer, restrictToArenaProjectionLayer = a.RestrictToArenaProjectionLayer,
                            appliesToActor = module.MechanicAppliesToArenaProjectionLayer(actor, a.ArenaProjectionLayer, a.RestrictToArenaProjectionLayer),
                            componentKind = component is GenericRotatingAOE ? "rotating-aoe" : "generic-aoe"
                        });
                    }
                    components.Add(new { componentId = id, component = component.GetType().FullName,
                        publishedPulseCount = active.Length, publishedComplete = true,
                        standardAIHints = component.GetType().GetMethod(nameof(GenericAOEs.AddAIHints))?.DeclaringType == StandardAoeAI.DeclaringType,
                        standardRotatingPublication = component.GetType().GetMethod(nameof(GenericAOEs.ActiveAOEs))?.DeclaringType == StandardRotatingPublication.DeclaringType,
                        publicationComplete = queueComplete,
                        publicationCompleteReason = queueComplete ? "native published prefix" : "StormWave overlapping axes share an incomplete native queue" });
                }
            }
            if (!live() || !Session.Matches(request.Context) || !budget.Step()) return Unavailable("native-forecast-capture-retired");
            lifetime.Owners = freezer.TakeOwned();
            if (!TrackDistanceSource(lifetime, live)) return Unavailable("native-forecast-live-source-limit");
            var snapshotId = Guid.NewGuid().ToString("N");
            receive(IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, snapshotId, actorId = actor.InstanceID.ToString("X"),
                worldTimeTicks = T(World.CurrentTime), worldTimeKind = World.CurrentTime.Kind.ToString(), publications, components,
                publishedEntriesComplete = true, encounterForecastComplete = false, futurePulsesComplete = false,
                sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                meaning = "all actual generic AOE publications, including non-risky pulses, in native publication order; frozen exact shape override or native shape distance before floor clipping; no invented later pulses, expiry or safety proof; delegates expire to NaN"
            }), [.. functions]);
            return IntegrationSession.Serialize(new { accepted = true, context = request.Context, snapshotId, publicationCount = functions.Count });
        }
        catch (Exception error)
        {
            lifetime.Retire();
            Service.LogVerbose($"[Integration] Native forecast capture failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-forecast-capture-failed");
        }
    }
}
