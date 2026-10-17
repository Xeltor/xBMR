using BossMod.Components;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

// A bounded view of native publications. Missing components/future pulses are never negative evidence.
internal static class IntegrationMechanics
{
    internal const int MaxComponents = 64;
    internal const int MaxEntries = 128;
    private sealed class Identity { internal readonly string Id = Guid.NewGuid().ToString("N"); }
    private static readonly ConditionalWeakTable<BossComponent, Identity> Identities = new();
    private static string Id(BossComponent component) => Identities.GetOrCreateValue(component).Id;
    internal static string ComponentId(BossComponent component) => Id(component);
    private static string T(DateTime time) => time.Ticks.ToString(CultureInfo.InvariantCulture);
    private static float[] P(WPos pos) => [pos.X, pos.Z];

    internal sealed record Shape(string Status, string Kind, float[] Parameters, float DirectionOffset, bool Inverted);
    internal static Shape Describe(AOEShape shape)
    {
        var result = shape switch
        {
            AOEShapeCircle s => new Shape("known", "circle", [s.Radius], 0, s.InvertForbiddenZone),
            AOEShapeDonut s => new("known", "donut", [s.InnerRadius, s.OuterRadius], 0, s.InvertForbiddenZone),
            AOEShapeCone s => new("known", "cone", [s.Radius, s.HalfAngle.Rad], s.DirectionOffset.Rad, s.InvertForbiddenZone),
            AOEShapeDonutSector s => new("known", "donut-sector", [s.InnerRadius, s.OuterRadius, s.HalfAngle.Rad], s.DirectionOffset.Rad, s.InvertForbiddenZone),
            AOEShapeRect s => new("known", "rectangle", [s.LengthFront, s.HalfWidth, s.LengthBack], s.DirectionOffset.Rad, s.InvertForbiddenZone),
            AOEShapeCross s => new("known", "cross", [s.Length, s.HalfWidth], s.DirectionOffset.Rad, s.InvertForbiddenZone),
            AOEShapeCapsule s => new("known", "capsule", [s.Radius, s.Length], s.DirectionOffset.Rad, s.InvertForbiddenZone),
            _ => new("unknown", "unsupported", [], 0, shape.InvertForbiddenZone)
        };
        if (!float.IsFinite(result.DirectionOffset) || result.Parameters.Any(v => !float.IsFinite(v) || v < 0))
            return new("unknown", "invalid-parameters", [], 0, shape.InvertForbiddenZone);
        return result;
    }

    internal sealed record Movement(float[] From, float[] To, float? Facing, bool NativeDestinationUnsafe,
        string? ActivationTicks, string? EndTicks, int? SourceIndex, int? ArenaProjectionLayer, bool? RestrictToArenaProjectionLayer);
    internal static Movement[]? ObserveCurrentProjection(BossComponent component, Actor actor, int slot)
    {
        if (slot < 0 || slot >= PartyState.MaxAllies)
            return null;
        if (component is GenericKnockback knockback && knockback.ActiveKnockbacks(slot, actor).Length <= 32)
        {
            var movements = knockback.CalculateMovementProjections(slot, actor);
            return movements.Count <= 32 ? movements.Select(m => new Movement(P(m.From), P(m.To), null,
                knockback.DestinationUnsafe(slot, actor, m.To), m.Activation == default ? null : T(m.Activation), null, m.SourceIndex,
                m.ArenaProjectionLayer, m.RestrictToArenaProjectionLayer)).ToArray() : null;
        }
        if (component is GenericForcedMarch march && (march.State.GetValueOrDefault(actor.InstanceID)?.PendingMoves.Count ?? 0) <= 32)
        {
            var movements = march.CalculateMovementProjections(actor);
            return movements.Count <= 33 ? movements.Select(m => new Movement(P(m.From), P(m.To), m.Direction.Rad,
                march.DestinationUnsafe(slot, actor, m.To), m.Activation == default ? null : T(m.Activation), m.End == default ? null : T(m.End), m.PendingIndex,
                march.ArenaProjectionLayer, march.RestrictToArenaProjectionLayer)).ToArray() : null;
        }
        return null;
    }

    internal static object Capture(BossModule module, Actor actor, IntegrationSession.Context context, IntegrationRoutes.Budget? budget = null)
    {
        budget ??= new();
        var slot = module.Raid.FindSlot(actor.InstanceID);
        if (slot < 0)
            return new { available = false, context, reason = "actor-not-in-native-party" };
        var party = module.Raid.WithSlot(includeDead: true);
        var entries = new List<object>();
        var coverage = new List<object>();
        var omitted = module.Components.Count > MaxComponents;
        string[] Recipients(BitMask mask) => party.Where(p => mask[p.Item1]).Select(p => p.Item2.InstanceID.ToString("X")).ToArray();
        void Add(object entry)
        {
            if (entries.Count < MaxEntries) entries.Add(entry);
            else omitted = true;
        }
        foreach (var component in module.Components.Take(MaxComponents))
        {
            if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
            var id = Id(component);
            var supported = false;
            var before = entries.Count;
            if (component is GenericAOEs aoes)
            {
                supported = true;
                var active = aoes.ActiveAOEs(slot, actor);
                for (var i = 0; i < active.Length; ++i)
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    ref readonly var a = ref active[i];
                    Add(new { kind = "aoe", componentId = id, pulseIndex = i, publishedPulseCount = active.Length,
                        futurePulseCount = (int?)null, shape = Describe(a.Shape), origin = P(a.Origin), rotation = a.Rotation.Rad,
                        activationTicks = T(a.Activation), a.Risky, sourceActorId = a.ActorID == 0 ? null : a.ActorID.ToString("X"),
                        a.ArenaProjectionLayer, a.RestrictToArenaProjectionLayer,
                        distanceOverride = a.ShapeDistance != null, geometryMeaning = a.ShapeDistance == null ? "native-display-shape" : "display-shape-differs-from-native-distance-constraint" });
                }
            }
            if (component is GenericStackSpread stacks)
            {
                supported = true;
                foreach (var s in stacks.Stacks.Take(MaxEntries + 1))
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    Add(new { kind = "stack", componentId = id, targetActorId = s.Target.InstanceID.ToString("X"), position = P(s.Target.Position),
                        s.Radius, s.MinSize, s.MaxSize, activationTicks = T(s.Activation), excludedActorIds = Recipients(s.ForbiddenPlayers),
                        stacks.IncludeDeadTargets, targetDead = s.Target.IsDead, stacks.EnableHints,
                        positioningPublished = stacks.EnableHints && (stacks.IncludeDeadTargets || !s.Target.IsDead),
                        arenaProjectionLayer = s.ResolveArenaProjectionLayer(module), s.RestrictToArenaProjectionLayer });
                }
                foreach (var s in stacks.Spreads.Take(MaxEntries + 1))
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    Add(new { kind = "spread", componentId = id, targetActorId = s.Target.InstanceID.ToString("X"), position = P(s.Target.Position),
                        s.Radius, activationTicks = T(s.Activation), stacks.IncludeDeadTargets, targetDead = s.Target.IsDead, stacks.EnableHints,
                        positioningPublished = stacks.EnableHints && (stacks.IncludeDeadTargets || !s.Target.IsDead),
                        arenaProjectionLayer = s.ResolveArenaProjectionLayer(module), s.RestrictToArenaProjectionLayer });
                }
            }
            if (component is GenericBaitAway baits)
            {
                supported = true;
                foreach (var b in baits.CurrentBaits.Take(MaxEntries + 1))
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    Add(new { kind = "bait", componentId = id, sourceActorId = b.Source.InstanceID.ToString("X"), targetActorId = b.Target.InstanceID.ToString("X"),
                        sourcePosition = P(b.Source.Position), targetPosition = P(b.Target.Position), shape = Describe(b.Shape), rotation = b.Rotation.Rad,
                        offset = new[] { b.Offset.X, b.Offset.Z }, b.MaxCasts, activationTicks = T(b.Activation),
                        excludedActorIds = Recipients(b.Forbidden | baits.ForbiddenPlayers), baits.AllowDeadTargets, baits.EnableHints,
                        baits.CenterAtTarget, baits.IgnoreOtherBaits, targetDead = b.Target.IsDead, sourceDead = b.Source.IsDead,
                        arenaProjectionLayer = b.ResolveArenaProjectionLayer(module), b.RestrictToArenaProjectionLayer });
                }
            }
            if (component is GenericChasingAOEs chasing)
            {
                supported = true;
                foreach (var c in chasing.Chasers.Take(MaxEntries + 1))
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    Add(new { kind = "chasing", componentId = id, targetActorId = c.Target.InstanceID.ToString("X"),
                        shape = Describe(c.Shape), previousPosition = P(c.PrevPos), nextPosition = P(c.PredictedPosition()),
                        c.MoveDist, c.NumRemaining, nextActivationTicks = T(c.NextActivation), c.SecondsBetweenActivations,
                        arenaProjectionLayer = c.ResolveArenaProjectionLayer(module), c.RestrictToArenaProjectionLayer,
                        laterPositionsKnown = false });
                }
            }
            if (component is GenericKnockback knockback)
            {
                supported = true;
                var sources = knockback.ActiveKnockbacks(slot, actor);
                for (var i = 0; i < sources.Length; ++i)
                {
                    if (entries.Count >= MaxEntries || !budget.Step()) { omitted = true; break; }
                    ref readonly var s = ref sources[i];
                    Add(new { kind = "knockback", componentId = id, sequenceIndex = i, publishedSequenceLength = sources.Length,
                        origin = P(s.Origin), s.Distance, direction = s.Direction.Rad, movementKind = s.Kind.ToString(), s.MinDistance,
                        activationTicks = T(s.Activation), shape = s.Shape == null ? null : Describe(s.Shape),
                        s.IgnoreImmunes, immuneAtActivation = knockback.IsImmune(slot, s.Activation),
                        knockback.StopAtWall, knockback.StopAfterWall, knockback.MaxCasts,
                        safeWalls = s.SafeWalls.Take(32).Select(w => new[] { P(w.Vertex1), P(w.Vertex2) }).ToArray(),
                        safeWallsComplete = s.SafeWalls.Length <= 32, sourceActorId = s.ActorID == 0 ? null : s.ActorID.ToString("X"),
                        s.ArenaProjectionLayer, s.RestrictToArenaProjectionLayer });
                }
            }
            if (component is GenericForcedMarch march)
            {
                supported = true;
                if (march.State.TryGetValue(actor.InstanceID, out var state))
                    Add(new { kind = "forced-march", componentId = id, actorId = actor.InstanceID.ToString("X"),
                        forcedEndTicks = T(state.ForcedEnd), march.MovementSpeed, march.StopAtWall, march.StopAfterWall, march.OverrideDirection,
                        march.ArenaProjectionLayer, march.RestrictToArenaProjectionLayer,
                        activeForActor = march.HasForcedMovements(actor),
                        pending = state.PendingMoves.Take(32).Select(m => new { direction = m.dir.Rad, m.duration, activationTicks = T(m.activation) }).ToArray(),
                        pendingComplete = state.PendingMoves.Count <= 32 });
            }
            coverage.Add(new { componentId = id, component = component.GetType().FullName, genericPublicationSupported = supported,
                counterfactualGeometrySupported = component is GenericForcedMarch || component is GenericKnockback { SupportsCounterfactualProjection: true },
                publishedEntries = entries.Count - before });
        }
        return new { available = true, context, actorId = actor.InstanceID.ToString("X"), entries, components = coverage,
            publishedEntriesComplete = !omitted, encounterForecastComplete = false, futurePulsesComplete = false,
            validThroughFrame = context.FrameId, validForMs = IntegrationSession.MaxFrameAgeMs,
            coverage = "known generic component publications only; unknown components and later pulses are not safe evidence" };
    }
}
