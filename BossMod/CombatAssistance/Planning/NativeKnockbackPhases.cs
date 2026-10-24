using BossMod.Components;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

internal sealed record NativeDisplacementStatus(int Owners, int PublishedSources, int InterruptingSources,
    bool InventoryComplete, bool AuthoredAbsenceVerified, bool MarchPublished, bool Pending, int PhaseMaps, string Detail, int PreparationConstraints = 0);

internal sealed record NativeKnockbackPhase(AuthoredKnockbackSource Source, NativeNavigationSnapshot After,
    ImmutableArray<NativeLandingConstraints.Envelope> LandingEnvelopes, int SeparatedLeaves, int IndependentLeaves)
{
    // Native publishers do not imply a flight duration. The framework owner
    // must bind a corroborated absolute window before search may use this edge.
    internal Knockback.KnockbackActivationWindow? Timing { get; init; }
    internal ulong CastGeneration { get; init; }
    internal long TimingEvidenceRevision { get; init; } = -1;
    internal float LandingRadius { get; init; }

    internal bool Preventable(MovementPlanningContext context, float appliedBy, float expiresAfter, uint immunityAction = 0)
    {
        if (Timing is not { } timing || Source.IgnoresImmunity || !Source.Enabled || !Source.Participant
            || Source.Activation == default || timing.LatestUtc < Source.Activation
            || timing.ExpectedUtc < timing.EarliestUtc || timing.ExpectedUtc > timing.LatestUtc) return false;
        var earliest = (float)((timing.EarliestUtc < Source.Activation ? timing.EarliestUtc : Source.Activation)
            - context.Identity.WorldTime).TotalSeconds;
        var latest = (float)(timing.LatestUtc - context.Identity.WorldTime).TotalSeconds;
        return float.IsFinite(appliedBy) && float.IsFinite(expiresAfter) && appliedBy >= 0
            && appliedBy < earliest && expiresAfter > latest && latest >= earliest;
    }

    internal bool TryPrevent(MovementPlanningContext before, MovementPlanningContext after, WPos position,
        float time, float appliedBy, float expiresAfter, out float slack, uint immunityAction = 0)
    {
        slack = float.MaxValue;
        // The native preparation leaf is unnecessary only for this source.
        // Independent damage/assignments and later source constraints remain in After.
        return time >= appliedBy && Preventable(before, appliedBy, expiresAfter, immunityAction)
            && time <= (Timing!.EarliestUtc - before.Identity.WorldTime).TotalSeconds
            && after.Traverse(position, position, time, 0, out slack);
    }

    internal bool MayBePrevented(MovementPlanningContext context, float applicationEarliest, float expiryLatest)
    {
        if (Source.IgnoresImmunity || Timing is not { } timing) return false;
        var earliest = (float)((timing.EarliestUtc < Source.Activation ? timing.EarliestUtc : Source.Activation)
            - context.Identity.WorldTime).TotalSeconds;
        var latest = (float)(timing.LatestUtc - context.Identity.WorldTime).TotalSeconds;
        return applicationEarliest <= latest && expiryLatest > earliest;
    }

    internal bool TryTransition(MovementPlanningContext before, MovementPlanningContext after,
        WPos launch, float time, out WPos landing, out float earliest, out float completed, out float slack)
    {
        landing = default;
        earliest = completed = 0;
        slack = float.MaxValue;
        if (Timing is not { } timing || !Source.GeometryKnown || !Source.Participant
            || !Source.Enabled || Source.ObservedImmune && !Source.IgnoresImmunity
            || !Source.ShapeUnrestricted || Source.Kind != GenericKnockback.Kind.AwayFromOrigin
            || Source.MinimumDistance != 0 || Source.StopAtWall || Source.StopAfterWall
            || !Source.Walls.IsEmpty || Source.ProjectionLayer != null) return false;
        if (Source.Activation == default || timing.LatestUtc < Source.Activation) return false;
        earliest = (float)((timing.EarliestUtc < Source.Activation ? timing.EarliestUtc : Source.Activation)
            - before.Identity.WorldTime).TotalSeconds;
        var latest = (float)(timing.LatestUtc - before.Identity.WorldTime).TotalSeconds;
        completed = (float)(timing.LandingLatestUtc - before.Identity.WorldTime).TotalSeconds;
        if (!float.IsFinite(time) || time < 0 || time > earliest || earliest < 0
            || latest < earliest || timing.ExpectedUtc < timing.EarliestUtc || timing.ExpectedUtc > timing.LatestUtc
            || completed <= latest || completed > MovementPlanningContext.MaximumHorizonSeconds
            || !TryProject(launch, out landing)) return false;
        if (!LandingAllowed(landing, before.Radius) || after.Cell(landing) < 0
            || !before.Traverse(launch, launch, time, earliest - time, out var waiting)) return false;
        // Onset may occur anywhere in its evidenced window. Do not pretend a
        // uniform-speed traversal: every swept cell must remain clear through
        // the latest landing. This is conservative without inventing expiry.
        if (!after.Traverse(launch, landing, completed, 0, out var corridor)) return false;
        slack = Math.Min(earliest - time, Math.Min(waiting, corridor));
        return true;
    }

    internal bool TryProject(WPos launch, out WPos landing)
    {
        landing = default;
        if (!GenericKnockback.TryUnclippedMovement(launch, Source.Origin, Source.Distance, Source.Direction,
            Source.Kind, Source.MinimumDistance, out var direction, out var distance)) return false;
        landing = launch + distance * direction;
        return true;
    }

    internal bool SameBinding(NativeKnockbackPhase other) => CastGeneration != 0 && CastGeneration == other.CastGeneration
        && Source.SourceActor != 0 && Source.CastActionId != 0 && Source.SourceDataId != 0
        && Source.Same(other.Source) && Timing != null && Timing == other.Timing
        && TimingEvidenceRevision == other.TimingEvidenceRevision && LandingRadius == other.LandingRadius && LandingEnvelopes.SequenceEqual(other.LandingEnvelopes);

    internal bool Completed(MovementPlanningContext current, MovementStage stage)
        => Completed(current.Identity, current.Origin, current.KnockbackPhases?.Inventory,
            current.KnockbackCompletion, stage.From, stage.To);

    internal bool Completed(NavigationIdentity identity, WPos position, NativeDisplacementInventory? inventory,
        Knockback.KnockbackCompletionEvidence? receipt, WPos launch, WPos landing)
        => receipt is { } observed && ObservedCompletion(identity, position, inventory, observed)
            && new WPos(observed.Launch.X, observed.Launch.Y).AlmostEqual(launch, MovementPlanValidation.ArrivalTolerance)
            && new WPos(observed.Landing.X, observed.Landing.Y).AlmostEqual(landing, MovementPlanValidation.ArrivalTolerance)
            && position.AlmostEqual(landing, MovementPlanValidation.ArrivalTolerance);

    // An invalidated forecast may resolve at a different landing. A matching
    // native effect and stable actual motion can retire that failed event; they
    // cannot turn its old predicted route into a successfully executed plan.
    internal bool ObservedCompletion(NavigationIdentity identity, WPos position, NativeDisplacementInventory? inventory,
        Knockback.KnockbackCompletionEvidence? receipt)
    {
        if (receipt is not { } observed || Timing is not { } timing
            || inventory?.Pending == true || Source.CastActionId == 0 || Source.SourceDataId == 0
            || observed.Actor != identity.ActorId || observed.Territory != identity.ZoneId
            || observed.SourceActor != Source.SourceActor || observed.SourceDataId != Source.SourceDataId
            || observed.CastActionId != Source.CastActionId || observed.EffectCounter == 0
            || CastGeneration == 0 || observed.CastGeneration != CastGeneration
            || observed.StartedUtc < (timing.EarliestUtc < Source.Activation ? timing.EarliestUtc : Source.Activation)
            || observed.StartedUtc > timing.LatestUtc || observed.EndedUtc < observed.StartedUtc
            || observed.EndedUtc > timing.LandingLatestUtc || observed.ObservedUtc < observed.EndedUtc
            || identity.WorldTime < observed.ObservedUtc
            || identity.WorldTime - observed.ObservedUtc >= TimeSpan.FromMilliseconds(250)
            || !position.AlmostEqual(new(observed.Landing.X, observed.Landing.Y), MovementPlanValidation.ArrivalTolerance)) return false;
        return inventory?.Knockbacks.Any(source => source.Interrupts(identity.WorldTime)
            && source.SameGeometry(Source)) != true;
    }

    // The envelope constrains the forced LANDING, not every later walking
    // position. Keeping it as a permanent warning would invent a lasting
    // assignment and could suppress a valid second push/recovery route.
    internal bool LandingAllowed(WPos landing, float radius)
        => LandingEnvelopes.All(e => NativeLandingConstraints.EnvelopeClear(e, landing, Math.Max(radius, LandingRadius)));
}

internal sealed record NativeKnockbackPhases(NativeDisplacementInventory Inventory,
    ImmutableArray<NativeKnockbackPhase> Phases, string Detail)
{
    // A saved immunity preference must not silently enable landing preparation.
    internal bool PlanLandings { get; init; } = true;
    internal bool MatchesInventory(NavigationIdentity identity, ForcedMovementChoices? forced = null)
    {
        if (Inventory is not { Complete: true, Pending: false, MarchPublished: false }
            || Inventory.Actor != identity.ActorId || Inventory.At != identity.WorldTime
            || Inventory.Zone != identity.ZoneId || Inventory.Content != identity.ContentId) return false;
        var published = Inventory.Knockbacks.Where(source => source.Interrupts(identity.WorldTime)
            && forced?.Routes.Any(route => route.Knockback?.SameGeometry(source) == true) != true).OrderBy(source => source.Activation).ToArray();
        return published.Length == Phases.Length && published.Where((source, index) => !source.Same(Phases[index].Source)).Any() == false;
    }

    internal static bool SafePhysicalFallback(MovementPlanningContext frame, int first, WPos position, float time)
    {
        var phases = frame.KnockbackPhases?.Phases ?? [];
        if (first < 0 || first >= phases.Length) return false;
        for (var index = first; index < phases.Length; ++index)
        {
            var after = frame.WithDisplacementMap(phases[index].After);
            if (!phases[index].TryTransition(frame, after, position, time, out var landing,
                out _, out var completed, out _)) return false;
            frame = after;
            position = landing;
            time = completed;
        }
        var cell = frame.Cell(position);
        return cell >= 0 && frame.DestinationAllowed(cell) && frame.CanRemain(position)
            && frame.DestinationSatisfied(position, time);
    }

    internal bool Same(NativeKnockbackPhases other)
        => PlanLandings == other.PlanLandings && Inventory.Actor == other.Inventory.Actor && Inventory.Zone == other.Inventory.Zone && Inventory.Content == other.Inventory.Content
            && Inventory.Complete == other.Inventory.Complete && Inventory.Pending == other.Inventory.Pending
            && Inventory.HintsInspected == other.Inventory.HintsInspected && Inventory.HintInspectionComplete == other.Inventory.HintInspectionComplete
            && Inventory.PreparationConstraints == other.Inventory.PreparationConstraints
            && Inventory.PreparationInterruptsForecast == other.Inventory.PreparationInterruptsForecast
            && Inventory.MarchPublished == other.Inventory.MarchPublished && Inventory.MarchParticipates == other.Inventory.MarchParticipates
            && Inventory.Knockbacks.Length == other.Inventory.Knockbacks.Length
            && Inventory.Knockbacks.Zip(other.Inventory.Knockbacks).All(p => p.First == (p.Second with { Walls = p.First.Walls })
                && p.First.Walls.SequenceEqual(p.Second.Walls))
            && Phases.Length == other.Phases.Length && Detail == other.Detail
            && Phases.Zip(other.Phases).All(p => p.First.Source.Same(p.Second.Source) && p.First.TimingEvidenceRevision == p.Second.TimingEvidenceRevision && p.First.LandingRadius == p.Second.LandingRadius && p.First.CastGeneration == p.Second.CastGeneration && p.First.Timing == p.Second.Timing && p.First.LandingEnvelopes.SequenceEqual(p.Second.LandingEnvelopes)
                && p.First.After.SameFrozenMap(p.Second.After));
}

internal static class NativeKnockbackPhaseCapture
{
    private sealed record Region(ShapeDistance Shape, DateTime Activation, ulong Actor, BossComponent? Producer);
    // Capture on the framework owner, then discard all native producer/shape
    // references. Workers receive the same native raster/evaluators as recovery.
    internal static NativeKnockbackPhases Freeze(NativeDisplacementCapture capture, AIHints hints,
        BossModule? module, Actor actor, NavigationDecision.CapturedInput input, IEnumerable<WPos>? observedBlocks = null, AuthoredKnockbackSource? active = null)
    {
        var inventory = capture.Snapshot;
        NativeKnockbackPhases Unavailable(string reason) => new(inventory, [], reason);
        if (inventory.Knockbacks.IsEmpty) return Unavailable("no published knockback phase");
        if (!inventory.Complete || inventory.Pending || inventory.MarchPublished || module == null
            || input.Identity.ActorId != inventory.Actor || input.Identity.WorldTime != inventory.At
            || input.Identity.ZoneId != inventory.Zone || input.Identity.ContentId != inventory.Content
            || input.Identity.ProjectionLayer != null || input.Teleporters.Length != 0)
            return Unavailable("native displacement inventory, identity or projection is incomplete for phase separation");
        if (!hints.ProjectionSourcesValid || !hints.ProjectionSourcesMatch(module, actor))
            return Unavailable("native preparation provenance unavailable");
        var sources = inventory.Knockbacks.Where(s => s.Interrupts(inventory.At)).OrderBy(s => s.Activation).ToArray();
        if (sources.Any(s => !s.GeometryKnown || s.Activation <= inventory.At && (active == null || !s.SameGeometry(active))
            || !s.ShapeUnrestricted || s.Kind != GenericKnockback.Kind.AwayFromOrigin || s.MinimumDistance != 0
            || s.StopAtWall || s.StopAfterWall || !s.Walls.IsEmpty || s.ProjectionLayer != null))
            return Unavailable("native knockback source needs additional projection/timing support");
        if (sources.GroupBy(s => s.Owner).Any(group => group.Count() > group.First().MaximumAppliedSources))
            return Unavailable("native source-count limit requires applicability-aware sequence composition");
        if (sources.GroupBy(s => s.Owner).Any(group => group.OrderBy(s => s.Index).Zip(group.OrderBy(s => s.Index).Skip(1))
            .Any(pair => pair.First.Activation > pair.Second.Activation)))
            return Unavailable("native source publication is not in its authored sequential order");
        if (sources.GroupBy(s => s.Activation).Any(group => group.Select(s => s.Owner).Distinct().Count() > 1))
            return Unavailable("simultaneous independent native displacement owners need authored composition");
        var regions = new List<Region>();
        foreach (var zone in input.Warnings)
        {
            var producer = hints.ProjectionProducer(zone.shapeDistance, zone.activation, zone.Source);
            if (producer == null && hints.ProjectionSourceWasRecorded(zone.shapeDistance))
                return Unavailable("native preparation graph changed after attribution");
            if (NativeLandingConstraints.Graph(zone.shapeDistance) == null)
                return Unavailable("native constraint graph is cyclic or unsupported");
            regions.Add(new(zone.shapeDistance, zone.activation, zone.Source, producer));
        }
        var current = regions;
        var result = ImmutableArray.CreateBuilder<NativeKnockbackPhase>();
        foreach (var source in sources)
        {
            var remaining = new List<Region>();
            var envelopes = ImmutableArray.CreateBuilder<NativeLandingConstraints.Envelope>();
            var separated = 0;
            var independent = 0;
            var valid = true;
            foreach (var zone in current)
            {
                if (!ReferenceEquals(zone.Producer, capture.Owners[source.Owner]) || zone.Activation != source.Activation)
                { remaining.Add(zone); continue; }
                var transformed = Separate(zone.Shape);
                if (!valid) return Unavailable("native preparation graph contains an unseparated projection rule");
                if (transformed != null) remaining.Add(zone with { Shape = transformed });

                ShapeDistance? Separate(ShapeDistance shape)
                {
                    if (shape is SDUnion union)
                    {
                        var children = new List<ShapeDistance>();
                        foreach (var child in union.Operands)
                            if (Separate(child) is { } kept) children.Add(kept);
                        return children.Count switch { 0 => null, 1 => children[0], _ => new SDUnion([.. children]) };
                    }
                    if (NativeLandingConstraints.ProjectionEnvelope(shape) is { } envelope)
                    {
                        if (!NativeLandingConstraints.EnvelopeKnown(envelope)) { valid = false; return shape; }
                        var matches = inventory.Knockbacks.Where(candidate => candidate.Owner == source.Owner
                            && candidate.Activation == zone.Activation && candidate.Kind == GenericKnockback.Kind.AwayFromOrigin
                            && candidate.Origin == envelope.Origin && candidate.Distance == envelope.PushDistance
                            && (zone.Actor == 0 || zone.Actor == candidate.SourceActor)).ToArray();
                        if (matches.Length != 1) { valid = false; return shape; }
                        if (matches[0].Index != source.Index) return shape;
                        ++separated;
                        envelopes.Add(envelope);
                        return null;
                    }
                    if (shape is IKnockbackPreparationConstraint) valid = false;
                    ++independent;
                    return shape;
                }
            }
            if (separated == 0) return Unavailable("no exactly attributed pure preparation requirement for this source");
            current = remaining;
            // Do not invent expiry for independent damage/assignment warnings.
            // The original native recovery input remains unchanged.
            var after = NativeNavigationSnapshot.Capture(input with { Warnings = current.Select(z => (z.Shape, z.Activation, z.Actor)).ToArray() }, observedBlocks);
            result.Add(new(source, after, envelopes.ToImmutable(), separated, independent));
        }
        return new(inventory, result.ToImmutable(), result.Count == 0 ? "no interrupting knockback phase" : "native phase rasters captured; transition timing/execution still required");
    }
}
