using BossMod.Components;

namespace BossMod.CombatAssistance.Planning;

internal readonly record struct DisplacementWall(WPos From, WPos To);
internal sealed record AuthoredKnockbackSource(int Owner, int Index, string OwnerType, WPos Origin, float Distance,
    DateTime Activation, GenericKnockback.Kind Kind, Angle Direction, float MinimumDistance, ulong SourceActor,
    bool IgnoresImmunity, bool ObservedImmune, bool Participant, int MaximumAppliedSources, bool ShapeUnrestricted,
    string? ShapeType, bool StopAtWall, bool StopAfterWall, int? ProjectionLayer, bool? RestrictLayer,
    ImmutableArray<DisplacementWall> Walls)
{
    internal uint CastActionId { get; init; }
    internal uint SourceDataId { get; init; }
    internal bool Same(AuthoredKnockbackSource other) => this == (other with { Walls = Walls }) && Walls.SequenceEqual(other.Walls);
    internal bool SameGeometry(AuthoredKnockbackSource other)
        => Same(other with { CastActionId = CastActionId, SourceDataId = SourceDataId, ObservedImmune = ObservedImmune });
    internal bool Enabled => MaximumAppliedSources > 0;
    internal bool GeometryKnown => float.IsFinite(Origin.X) && float.IsFinite(Origin.Z) && float.IsFinite(Distance)
        && Distance >= 0 && float.IsFinite(Direction.Rad) && float.IsFinite(MinimumDistance) && MinimumDistance >= 0
        && Enum.IsDefined(Kind) && Walls.All(w => float.IsFinite(w.From.X) && float.IsFinite(w.From.Z)
            && float.IsFinite(w.To.X) && float.IsFinite(w.To.Z));
    internal bool Interrupts(DateTime now) => Enabled && Participant && (!ObservedImmune || IgnoresImmunity)
        && Kind != GenericKnockback.Kind.None && (!float.IsFinite(Distance) || Distance > 0)
        && (Activation == default || (Activation - now).TotalSeconds <= MovementPlanningContext.MaximumHorizonSeconds);
}

// This is the worker-safe inventory. Presence is deliberately independent of
// immunity, layer, distance and horizon filtering used by the ordinary forecast.
internal sealed record NativeDisplacementInventory(DateTime At, ulong Actor, ushort Zone, ushort Content,
    ImmutableArray<AuthoredKnockbackSource> Knockbacks, int KnockbackOwners, int UnknownOwners,
    int FailedActiveOwners, bool ParticipantKnown, bool MarchPublished, bool MarchParticipates, bool Pending)
{
    internal bool HintsInspected { get; init; }
    internal bool HintInspectionComplete { get; init; }
    internal int PreparationConstraints { get; init; }
    internal bool PreparationInterruptsForecast { get; init; }
    internal bool Complete => ParticipantKnown && UnknownOwners == 0 && FailedActiveOwners == 0 && HintsInspected && HintInspectionComplete;
    internal bool AuthoredAbsenceVerified => Complete && Knockbacks.IsEmpty && PreparationConstraints == 0 && !MarchPublished && !Pending;
}

// Native handles stay on the capture owner. They never enter a planning context,
// result, IO worker or diagnostic document. Custom pose-dependent publishers can
// report nothing at the present pose; that is not proof of authored absence.
internal sealed class NativeDisplacementCapture(NativeDisplacementInventory snapshot,
    ImmutableArray<GenericKnockback> owners)
{
    internal NativeDisplacementInventory Snapshot { get; } = snapshot;
    internal ImmutableArray<GenericKnockback> Owners { get; } = owners;

    internal static NativeDisplacementCapture Capture(WorldState world, BossModule? module, Actor actor, AIHints? hints = null)
    {
        var slot = world.Party.FindSlot(actor.InstanceID);
        var participant = slot >= 0 && slot < PartyState.MaxAllies;
        var owners = module?.Components.OfType<GenericKnockback>().ToImmutableArray() ?? [];
        var rows = ImmutableArray.CreateBuilder<AuthoredKnockbackSource>();
        var unknown = new HashSet<int>();
        var failures = 0;
        for (var index = 0; index < owners.Length; ++index)
        {
            var owner = owners[index];
            if (!participant) { unknown.Add(index); if (owner.MaxCasts > 0) ++failures; continue; }
            // The native API explicitly identifies only pose-independent simple
            // publishers. Retain a custom publisher's reported sources, while
            // keeping absence unproven for hypothetical route poses.
            if (!owner.SupportsCounterfactualProjection) unknown.Add(index);
            try
            {
                var sources = owner.ActiveKnockbacks(slot, actor);
                for (var sourceIndex = 0; sourceIndex < sources.Length; ++sourceIndex)
                {
                    ref readonly var source = ref sources[sourceIndex];
                    var caster = world.Actors?.Find(source.ActorID);
                    rows.Add(new(index, sourceIndex, owner.GetType().FullName ?? owner.GetType().Name,
                        source.Origin, source.Distance, source.Activation, source.Kind, source.Direction,
                        source.MinDistance, source.ActorID, source.IgnoreImmunes,
                        source.Activation != default && owner.IsImmune(slot, source.Activation),
                        owner.ParticipantInProjection(actor, source), owner.MaxCasts,
                        source.Shape == null, source.Shape?.GetType().FullName, owner.StopAtWall, owner.StopAfterWall,
                        source.ArenaProjectionLayer, source.RestrictToArenaProjectionLayer,
                        (source.SafeWalls ?? []).Select(w => new DisplacementWall(w.Vertex1, w.Vertex2)).ToImmutableArray())
                    {
                        CastActionId = caster?.CastInfo is { Action.Type: ActionType.Spell } cast
                            && (owner.WatchedAction == 0 || cast.Action.ID == owner.WatchedAction) ? cast.Action.ID : 0,
                        SourceDataId = caster?.OID ?? 0
                    });
                }
            }
            catch (Exception error)
            {
                unknown.Add(index);
                if (owner.MaxCasts > 0) ++failures;
                // An individual publisher cannot manufacture absence or discard
                // other native sources. The active failure retains recovery.
                Service.LogVerbose($"[Displacement] Native source capture failed: {error.GetType().Name}: {error.Message}");
            }
        }
        var marchPublished = false;
        var marchParticipates = false;
        foreach (var march in module?.Components.OfType<GenericForcedMarch>() ?? [])
        {
            var published = march.State.TryGetValue(actor.InstanceID, out var state) && state.Active(march.Module);
            marchPublished |= published;
            marchParticipates |= published && march.HasForcedMovements(actor);
        }
        var completeHints = hints != null;
        var preparation = 0;
        var preparationInterrupts = false;
        if (hints != null)
            foreach (var region in hints.ForbiddenZones.Select(z => (Shape: z.shapeDistance, At: z.activation))
                .Concat(hints.TemporaryObstacles.Select(shape => (Shape: shape, At: default(DateTime)))))
            {
                var graph = Pathfinding.NativeLandingConstraints.Graph(region.Shape);
                if (graph == null) { completeHints = false; continue; }
                foreach (var node in graph)
                {
                    if (node is SDUnion) continue;
                    if (node is IKnockbackPreparationConstraint)
                    {
                        ++preparation;
                        preparationInterrupts |= region.At == default || (region.At - world.CurrentTime).TotalSeconds <= MovementPlanningContext.MaximumHorizonSeconds;
                    }
                    // An opaque compound/custom evaluator cannot certify that
                    // no hidden projection rule exists. It remains native-owned.
                    else if (!float.IsFinite(node.ConservativeLipschitz)) completeHints = false;
                }
            }
        return new(new NativeDisplacementInventory(world.CurrentTime, actor.InstanceID, world.CurrentZone, world.CurrentCFCID,
            rows.ToImmutable(), owners.Length, unknown.Count, failures, participant || owners.IsEmpty,
            marchPublished, marchParticipates, actor.PendingKnockbacks is { Count: > 0 })
        { HintsInspected = hints != null, HintInspectionComplete = completeHints, PreparationConstraints = preparation, PreparationInterruptsForecast = preparationInterrupts }, owners);
    }
}
