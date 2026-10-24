using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

internal enum MovementSearchStatus { Searching, Complete, Bounded, NativeRecovery, Invalidated, NoVerifiedRoute }
internal enum MovementSearchBound { None, InputFreshness, RequiredDeparture, StateMemory, ExpansionLimit, ElapsedLimit, SequenceLimit, InputChanged, FriendlyEffectGeometry, CooldownEvidence, HeadingSamples, SearchInProgress, ActionTimingEvidence, TargetSetupEvidence, NativeDisplacement }
internal enum MovementStageKind { Walk, Wait, Cast, TargetDash, GroundMovement, DirectionalMovement, Return, UtilityAction, Teleport, ForcedMovement, Target, FieldPlacement, Knockback }

internal sealed record MovementStage(MovementStageKind Kind, WPos From, WPos To, float StartSeconds, float EndSeconds,
    uint ActionId = 0, ulong AnchorId = 0, Angle? Facing = null, float PreparationSeconds = 0, PlanningTeleport? Transit = null, PlanningForcedMovement? Forced = null,
    int SourceActionIndex = -1, PlanningPersonalField? Field = null, float PlannedRecastSeconds = float.NaN, NativeKnockbackPhase? Knockback = null,
    float ActionLockUntil = float.NaN, uint PreventedByActionId = 0, PlanningUtility? Utility = null);

// A target choice retains the original action, resource costs and independently
// required recipients. Setup is a captured estimate; execution must confirm the
// selected target through the native rotation before allowing the walking leg.
internal sealed record PlanningTargetChoice(ExecutionOpportunity Opportunity, float SetupSeconds, float VerticalOffset = 0)
{
    // Plugin.SetTarget uses a strict 50-yalm three-dimensional acquisition
    // envelope. The caller captures height for the supported projection layer;
    // actual native eligibility is rechecked by the target owner at execution.
    internal bool CanAcquire(WPos position) => float.IsFinite(VerticalOffset)
        && (Opportunity.Anchor - position).LengthSq() + VerticalOffset * VerticalOffset < 2500;

    internal bool Preserves(ExecutionOpportunity original)
    {
        var oldPrimary = original.Recipients.FirstOrDefault(recipient => recipient.Id == original.TargetId);
        var primary = Opportunity.Recipients.FirstOrDefault(recipient => recipient.Id == Opportunity.TargetId);
        if (oldPrimary == null || primary == null || Opportunity.Anchor != primary.Position
            || Opportunity.Shape != original.Shape || Opportunity.EffectRadius != original.EffectRadius
            || Opportunity.HalfWidth != original.HalfWidth || Opportunity.HalfAngle != original.HalfAngle
            || Opportunity.Positional != original.Positional || Opportunity.TankFacing != original.TankFacing
            || Math.Abs((Opportunity.CastReach - primary.Radius) - (original.CastReach - oldPrimary.Radius)) > 0.0001f
            || Opportunity.Recipients.Length != original.Recipients.Length
            || Opportunity.ForbiddenRecipients.Length != original.ForbiddenRecipients.Length) return false;
        // Relative height is anchored at the selected target for target circles.
        // Compare the same actor across anchors rather than different primaries.
        var matchingOldPrimary = Opportunity.Recipients.FirstOrDefault(recipient => recipient.Id == oldPrimary.Id);
        if (matchingOldPrimary == null) return false;
        var heightShift = matchingOldPrimary.HeightOffset - oldPrimary.HeightOffset;
        return original.Recipients.All(before => Opportunity.Recipients.Any(after => SameActor(before, after)
                && before.Value == after.Value && (!before.Required || after.Required)
                && Math.Abs(after.HeightOffset - before.HeightOffset - heightShift) < 0.0001f))
            && original.ForbiddenRecipients.All(before => Opportunity.ForbiddenRecipients.Any(after => SameActor(before, after)
                && Math.Abs(after.HeightOffset - before.HeightOffset - heightShift) < 0.0001f));
    }

    private static bool SameActor(ExecutionRecipient first, ExecutionRecipient second)
        => first.Id == second.Id && first.Position == second.Position && first.Radius == second.Radius;
}

// Only observed actions belong here. Coverage is per recipient, not a weighted healing score.
// All arrays are copied at the context boundary; no callback can reach live actors from the worker.
internal sealed record PlanningAction(uint Id, ulong TargetId, float Earliest, float Latest, float CastSeconds, float LockSeconds,
    ulong RequiredRecipients, ulong[] RecipientsByCell, float[] ValueByCell, byte RequiredEarlierActions = 0,
    ImmutableArray<ulong> RecipientIds = default, ExecutionOpportunity? Opportunity = null, bool HorizonLimited = false, PlanningCastTiming? Timing = null,
    ImmutableArray<PlanningTargetChoice> TargetChoices = default, bool UsesHardTarget = false);

internal sealed record FrozenPlanningAction(uint Id, ulong TargetId, float Earliest, float Latest, float CastSeconds, float LockSeconds,
    ulong RequiredRecipients, ImmutableArray<ulong> RecipientsByCell, ImmutableArray<float> ValueByCell, byte RequiredEarlierActions,
    ImmutableArray<ulong> RecipientIds, ExecutionOpportunity? Opportunity, bool HorizonLimited, PlanningCastTiming? Timing,
    ImmutableArray<PlanningTargetChoice> TargetChoices = default, bool UsesHardTarget = false)
{
    internal float CastAt(WPos position, float start, bool enteredFieldByAbility = false, PlanningPersonalField? field = null, bool fieldBenefitAllowed = true)
        => Timing?.Seconds(CastSeconds, position, start, enteredFieldByAbility, field, fieldBenefitAllowed) ?? CastSeconds;
}

internal sealed record PlannedActionEvidence(uint Id, ulong TargetId, ImmutableArray<ulong> RequiredRecipientIds, float Value,
    int SourceActionIndex = -1, float CastSeconds = float.NaN, PlanningCastTiming? Timing = null, float RecastSeconds = float.NaN);

internal sealed record PlanningMobility(int SourceCell, int DestinationCell, MovementStageKind Kind, uint ActionId,
    ulong AnchorId, Angle? Facing, float SetupSeconds, float TravelSeconds, float LockSeconds, int Resource, int Charges = 1,
    WPos? SourcePosition = null, WPos? LandingPosition = null, float ReadyIn = 0, ulong AdditionalResourceCost = 0)
{
    internal ulong ResourceCost => ((ulong)Charges << (4 * Resource)) | AdditionalResourceCost;
}

internal sealed class MovementPlanningContext
{
    internal const float MaximumHorizonSeconds = 12;
    internal const int MaximumActions = 8;
    internal const int MaximumPhases = 8;
    private readonly Map _map = new();
    private readonly bool[] _allowedDestinations;
    private readonly float[] _presentation;
    private readonly ImmutableArray<FrozenPlanningAction> _actions;
    private readonly ImmutableArray<PlanningMobility> _mobility;
    internal NavigationIdentity Identity { get; }
    internal NativeNavigationSnapshot? Native { get; }
    internal ulong TargetId { get; }
    internal bool Optimize { get; }
    internal bool StationaryOnly { get; }
    internal PlanningRange? Range { get; }
    internal PlanningRange? PartyContact { get; }
    internal PlanningComboRecovery? ComboRecovery { get; }
    internal PlanningExecutionRules ExecutionRules { get; }
    internal ImmutableArray<PlanningUtility> Utilities { get; }
    internal PlanningPersonalField? PersonalField { get; }
    internal PlanningFieldPlacement? FieldPlacement { get; }
    internal ImmutableArray<PlanningRecharge> Recharges { get; }
    internal bool CooldownEvidenceLimited { get; }
    internal ImmutableArray<PlanningTeleport> Teleports { get; }
    internal ForcedMovementChoices? ForcedMovement { get; }
    internal NativeKnockbackPhases? KnockbackPhases { get; }
    internal Knockback.KnockbackCompletionEvidence? KnockbackCompletion { get; }
    internal ImmutableArray<PlanningZone> Zones { get; }
    internal float PositionalBypassUntil { get; }
    internal PlanningTravel Travel { get; }
    internal Angle? DowntimeFacing { get; }
    internal WPos? RequiredDestination { get; }
    internal float ArrivalDeadline { get; }
    internal float? LateDepartureMargin { get; }
    internal WPos Origin => Identity.Origin;
    internal WPos? CommittedDestination { get; }
    internal float Radius { get; }
    internal float UnlockSeconds { get; }
    internal ulong Resources { get; }
    internal int Cells => _map.Width * _map.Height;
    internal int Width => _map.Width;
    internal int Height => _map.Height;
    internal ImmutableArray<FrozenPlanningAction> Actions => _actions;
    internal bool HasTargetChoices { get; }
    internal FrozenPlanningAction? ResolveTarget(FrozenPlanningAction action, ulong target)
    {
        if (action.TargetId == target) return action;
        var choice = action.TargetChoices.IsDefaultOrEmpty ? null : action.TargetChoices.FirstOrDefault(c => c.Opportunity.TargetId == target);
        return choice == null ? null : action with { TargetId = target, Opportunity = choice.Opportunity, TargetChoices = [] };
    }
    internal float? TargetSetup(MovementStage stage)
    {
        if (stage.SourceActionIndex < 0 || stage.SourceActionIndex >= _actions.Length) return null;
        var action = _actions[stage.SourceActionIndex];
        if (action.Id != stage.ActionId || action.TargetChoices.IsDefaultOrEmpty) return null;
        return action.TargetChoices.FirstOrDefault(c => c.Opportunity.TargetId == stage.AnchorId && c.CanAcquire(stage.From))?.SetupSeconds;
    }
    internal bool TargetConfirmed(MovementStage stage, ulong observed)
        => observed == stage.AnchorId && stage.SourceActionIndex >= 0 && stage.SourceActionIndex < _actions.Length
            && _actions[stage.SourceActionIndex].Id == stage.ActionId && _actions[stage.SourceActionIndex].TargetId == observed;

    internal FrozenPlanningAction? ActionFor(MovementStage stage)
    {
        var action = stage.SourceActionIndex >= 0
            ? stage.SourceActionIndex < _actions.Length ? _actions[stage.SourceActionIndex] : null
            : _actions.FirstOrDefault(a => a.Id == stage.ActionId && a.TargetId == stage.AnchorId);
        return action?.Id == stage.ActionId ? ResolveTarget(action, stage.AnchorId) : null;
    }
    internal ImmutableArray<PlanningMobility> Mobility => _mobility;
    internal ImmutableArray<MobilityMechanics> MobilityMechanics { get; }
    internal ImmutableArray<RejectedMobility> RejectedMobility { get; private init; } = [];

    private MovementPlanningContext(MovementPlanningContext source, ImmutableArray<RejectedMobility> rejected, Map? phaseMap = null)
    {
        _map = phaseMap ?? source._map;
        _allowedDestinations = source._allowedDestinations;
        _presentation = source._presentation;
        _actions = source._actions;
        HasTargetChoices = source.HasTargetChoices;
        _mobility = source._mobility;
        Identity = source.Identity;
        Native = source.Native;
        TargetId = source.TargetId;
        Optimize = source.Optimize;
        StationaryOnly = source.StationaryOnly;
        Range = source.Range;
        PartyContact = source.PartyContact;
        ComboRecovery = source.ComboRecovery;
        ExecutionRules = source.ExecutionRules;
        Utilities = source.Utilities;
        PersonalField = source.PersonalField; FieldPlacement = source.FieldPlacement;
        Recharges = source.Recharges;
        CooldownEvidenceLimited = source.CooldownEvidenceLimited;
        Teleports = source.Teleports;
        ForcedMovement = source.ForcedMovement;
        KnockbackPhases = source.KnockbackPhases;
        KnockbackCompletion = source.KnockbackCompletion;
        Zones = source.Zones;
        PositionalBypassUntil = source.PositionalBypassUntil;
        Travel = source.Travel;
        DowntimeFacing = source.DowntimeFacing;
        RequiredDestination = source.RequiredDestination;
        ArrivalDeadline = source.ArrivalDeadline;
        LateDepartureMargin = source.LateDepartureMargin;
        CommittedDestination = source.CommittedDestination;
        Radius = source.Radius;
        UnlockSeconds = source.UnlockSeconds;
        Resources = source.Resources;
        MobilityMechanics = source.MobilityMechanics;
        RejectedMobility = rejected;
    }

    internal MovementPlanningContext Excluding(ImmutableArray<RejectedMobility> rejected) => new(this, rejected);

    internal MovementPlanningContext WithDisplacementMap(NativeNavigationSnapshot phase)
    {
        var map = phase.CopyStrictMap();
        if (phase.Identity != Identity || map.Width != _map.Width || map.Height != _map.Height
            || map.Resolution != _map.Resolution || map.Center != _map.Center || map.Rotation != _map.Rotation)
            throw new ArgumentException("Displacement phases must share one captured native map and clock.");
        return new(this, RejectedMobility, map);
    }

    internal MovementPlanningContext(NavigationIdentity identity, Map unrelaxedMap, float radius, float unlockSeconds,
        bool[] allowedDestinations, float[] presentation, IEnumerable<PlanningAction> actions,
        IEnumerable<PlanningMobility> mobility, ulong resources = 0, WPos? committedDestination = null,
        NativeNavigationSnapshot? native = null, ulong targetId = 0, bool optimize = true, PlanningRange? range = null,
        IEnumerable<MobilityMechanics>? mobilityMechanics = null, PlanningExecutionRules? executionRules = null,
        IEnumerable<PlanningUtility>? utilities = null, float positionalBypassUntil = 0, PlanningTravel? travel = null, Angle? downtimeFacing = null,
        WPos? requiredDestination = null, float arrivalDeadline = float.MaxValue, float? lateDepartureMargin = null,
        IEnumerable<PlanningZone>? zones = null, IEnumerable<PlanningRecharge>? recharges = null, bool cooldownEvidenceLimited = false,
        IEnumerable<PlanningTeleport>? teleports = null, ForcedMovementChoices? forcedMovement = null, PlanningRange? partyContact = null,
        PlanningComboRecovery? comboRecovery = null, PlanningPersonalField? personalField = null, PlanningFieldPlacement? fieldPlacement = null, NativeKnockbackPhases? knockbackPhases = null, bool stationaryOnly = false,
        Knockback.KnockbackCompletionEvidence? knockbackCompletion = null)
    {
        StationaryOnly = stationaryOnly;
        ForcedMovement = forcedMovement;
        KnockbackPhases = knockbackPhases;
        KnockbackCompletion = knockbackCompletion;
        Teleports = teleports?.ToImmutableArray() ?? native?.Teleports ?? [];
        if (unrelaxedMap.HasTeleporters && Teleports.IsEmpty || Teleports.Any(t => !t.Valid))
            throw new NotSupportedException("Authored teleporter transitions need complete typed entrance, exit and timing evidence.");
        if (unrelaxedMap.Width <= 0 || unrelaxedMap.Height <= 0 || (long)unrelaxedMap.Width * unrelaxedMap.Height > Array.MaxLength
            || !float.IsFinite(identity.Speed) || identity.Speed <= 0 || !float.IsFinite(radius) || radius < 0
            || !float.IsFinite(unlockSeconds) || unlockSeconds < 0 || unlockSeconds > MaximumHorizonSeconds)
            throw new ArgumentException("Unsupported planning geometry or timing.");
        _map.Init(unrelaxedMap, unrelaxedMap.Center);
        Identity = identity;
        if (native != null && native.Identity != identity)
            throw new ArgumentException("Native recovery and planning must belong to the same capture.");
        Native = native;
        TargetId = targetId;
        Optimize = optimize;
        Range = range;
        PartyContact = partyContact;
        ComboRecovery = comboRecovery;
        ExecutionRules = executionRules ?? PlanningExecutionRules.None;
        Utilities = utilities?.ToImmutableArray() ?? [];
        PersonalField = personalField;
        FieldPlacement = fieldPlacement;
        if (FieldPlacement?.Valid == false || FieldPlacement != null && PersonalField != null)
            throw new ArgumentException("Incomplete or already active field placement evidence.");
        if (PersonalField != null && (!PersonalField.Valid || PersonalField.Owner != identity.ActorId)
            || Utilities.Any(u => u.Field != null && u.Field != PersonalField))
            throw new ArgumentException("Invalid owned personal field evidence.");
        CooldownEvidenceLimited = cooldownEvidenceLimited;
        Recharges = recharges is ImmutableArray<PlanningRecharge> { IsDefault: true } ? [] : recharges?.ToImmutableArray() ?? [];
        if (Recharges.Any(r => !r.Valid || ChargeCount(resources, r.Resource) > r.Capacity
                || ChargeCount(resources, r.Resource) < r.Capacity && r.NextIn == float.MaxValue)
            || Recharges.Select(r => r.Resource).Distinct().Count() != Recharges.Length)
            throw new ArgumentException("Invalid shared cooldown evidence.");
        Zones = zones?.ToImmutableArray() ?? [];
        if (Zones.Any(z => !z.Valid)) throw new ArgumentException("Invalid friendly zone evidence.");
        PositionalBypassUntil = positionalBypassUntil;
        Travel = travel ?? new(identity.Speed);
        DowntimeFacing = downtimeFacing;
        RequiredDestination = requiredDestination;
        ArrivalDeadline = arrivalDeadline;
        LateDepartureMargin = lateDepartureMargin;
        if (lateDepartureMargin is { } margin && (!float.IsFinite(margin) || margin <= 0))
            throw new ArgumentException("Invalid late departure margin.");
        if (!float.IsFinite(arrivalDeadline) || arrivalDeadline < 0
            || requiredDestination is { } required && (!float.IsFinite(required.X) || !float.IsFinite(required.Z)))
            throw new ArgumentException("Invalid explicit destination or arrival deadline.");
        if (downtimeFacing is { } facing && !float.IsFinite(facing.Rad)) throw new ArgumentException("Invalid downtime facing.");
        if (!Travel.Valid || !float.IsFinite(positionalBypassUntil) || positionalBypassUntil < 0 || Utilities.Any(u => !u.Valid))
            throw new ArgumentException("Invalid utility action evidence.");
        MobilityMechanics = mobilityMechanics?.ToImmutableArray() ?? [];
        Radius = radius;
        UnlockSeconds = unlockSeconds;
        Resources = resources;
        CommittedDestination = committedDestination;
        _allowedDestinations = (bool[])allowedDestinations.Clone();
        _presentation = (float[])presentation.Clone();
        _actions = actions.Select(a => new FrozenPlanningAction(a.Id, a.TargetId, a.Earliest, a.Latest, a.CastSeconds, a.LockSeconds,
            a.RequiredRecipients, a.RecipientsByCell.ToImmutableArray(), a.ValueByCell.ToImmutableArray(), a.RequiredEarlierActions,
            a.RecipientIds.IsDefault ? [] : a.RecipientIds, a.Opportunity, a.HorizonLimited, a.Timing,
            a.TargetChoices.IsDefault ? [] : a.TargetChoices.ToImmutableArray(), a.UsesHardTarget)).ToImmutableArray();
        HasTargetChoices = _actions.Any(a => !a.TargetChoices.IsDefaultOrEmpty);
        if (PersonalField != null && _actions.Any(a => a.Timing is { ObservedFieldCast: not null } timing
            && (timing.HasteCenter != PersonalField.Center || timing.HasteUntil > PersonalField.EndsIn + 0.01f)))
            throw new ArgumentException("Cast timing and personal field must belong to one coherent capture.");
        _mobility = mobility.ToImmutableArray();
        if (_allowedDestinations.Length != Cells || _presentation.Length != Cells || _actions.Length > MaximumActions
            || range is { Valid: false } || partyContact is { Valid: false } || comboRecovery is { Valid: false }
            || _presentation.Any(v => !float.IsFinite(v)) || _map.PixelPriority.Take(Cells).Any(v => !float.IsFinite(v))
            || _actions.Any(a => a.Id == 0 || a.Opportunity == null && (a.RecipientsByCell.Length != Cells || a.ValueByCell.Length != Cells)
                || a.Opportunity != null && (a.Opportunity.TargetId != a.TargetId || a.RequiredRecipients != 0
                    || !a.RecipientsByCell.IsEmpty || !a.ValueByCell.IsEmpty || !a.RecipientIds.IsEmpty)
                || a.RecipientIds.Length > 64 || a.RecipientIds.Any(id => id == 0) || a.RecipientIds.Distinct().Count() != a.RecipientIds.Length
                || a.RecipientIds.Length < 64 && ((a.RequiredRecipients >> a.RecipientIds.Length) != 0 || a.RecipientsByCell.Any(mask => (mask >> a.RecipientIds.Length) != 0))
                || !float.IsFinite(a.Earliest) || !float.IsFinite(a.Latest) || a.Earliest < 0 || a.Latest < a.Earliest || a.Latest > MaximumHorizonSeconds
                || !float.IsFinite(a.CastSeconds) || a.CastSeconds < 0 || !float.IsFinite(a.LockSeconds) || a.LockSeconds < 0
                || a.Timing is { Valid: false }
                || a.ValueByCell.Any(v => !float.IsFinite(v) || v < 0))
            || _mobility.Any(m => m.SourceCell < 0 || m.SourceCell >= Cells || m.DestinationCell < 0 || m.DestinationCell >= Cells
                || m.ActionId == 0 || m.Kind == MovementStageKind.TargetDash && m.AnchorId == 0
                || m.Kind == MovementStageKind.DirectionalMovement && m.Facing == null
                || m.Kind is MovementStageKind.Walk or MovementStageKind.Wait or MovementStageKind.Cast or MovementStageKind.Target
                || m.Resource is < 0 or > 15 || m.Charges is < 1 or > 15
                || (m.AdditionalResourceCost & (15ul << (4 * m.Resource))) != 0
                || !float.IsFinite(m.SetupSeconds) || m.SetupSeconds < 0 || !float.IsFinite(m.TravelSeconds) || m.TravelSeconds <= 0
                || !float.IsFinite(m.LockSeconds) || m.LockSeconds < 0 || !float.IsFinite(m.ReadyIn) || m.ReadyIn < 0))
            throw new ArgumentException("Incomplete planning evidence.");
        foreach (var action in _actions.Where(a => !a.TargetChoices.IsDefaultOrEmpty))
        {
            var original = action.Opportunity ?? throw new ArgumentException("Target choices require typed current-action geometry.");
            var requiredRecipientIds = original.Recipients.Where(recipient => recipient.Required).Select(recipient => recipient.Id).ToHashSet();
            if (!action.UsesHardTarget || action.TargetChoices.Any(choice => choice.Opportunity.TargetId == 0
                    || choice.Opportunity.TargetId == action.TargetId || !float.IsFinite(choice.SetupSeconds)
                    || choice.SetupSeconds <= 0 || choice.SetupSeconds > MaximumHorizonSeconds || !float.IsFinite(choice.VerticalOffset)
                    || !choice.Preserves(original)
                    || requiredRecipientIds.Any(id => !choice.Opportunity.Recipients.Any(recipient => recipient.Id == id && recipient.Required)))
                || action.TargetChoices.Select(choice => choice.Opportunity.TargetId).Distinct().Count() != action.TargetChoices.Length)
                throw new ArgumentException("Target setup must preserve the current action and every independently required recipient.");
        }
        for (var i = 0; i < _actions.Length; ++i)
            if ((_actions[i].RequiredEarlierActions & ~((1 << i) - 1)) != 0)
                throw new ArgumentException("Action dependencies must refer to earlier evidenced actions.");
        foreach (var movement in _mobility)
            if (Cell(MobilityOrigin(movement)) != movement.SourceCell || Cell(MobilityLanding(movement)) != movement.DestinationCell)
                throw new ArgumentException("Mobility cell does not contain its executable position.");
    }

    internal int Cell(WPos point)
    {
        var grid = _map.WorldToGrid(point);
        return _map.InBounds(grid.x, grid.y) ? _map.GridToIndex(grid) : -1;
    }
    internal WPos Point(int cell) => _map.GridToWorld(cell % Width, cell / Width, 0.5f, 0.5f);
    internal WPos MobilityOrigin(PlanningMobility mobility) => mobility.SourcePosition ?? Point(mobility.SourceCell);
    internal WPos MobilityLanding(PlanningMobility mobility) => mobility.LandingPosition ?? Point(mobility.DestinationCell);
    internal IEnumerable<PlanningMobility> MobilityFrom(WPos position, Func<ulong, bool>? canSpend = null, PlanningPersonalField? relocatedField = null)
        => _mobility.Where(m => MobilityOrigin(m).AlmostEqual(position, 0.001f) && (canSpend?.Invoke(m.ResourceCost) ?? true)
                && !(m.ActionId == 7419 && relocatedField != null))
            .Concat(MobilityMechanics.Where(m => canSpend?.Invoke(m.ResourceCost) ?? true)
                .SelectMany(m => m.From(this, position, m.ActionId == 7419 ? relocatedField?.Center : null)))
            .Where(m => !RejectedMobility.Any(rejected => rejected.Matches(this, m)));
    internal PlanningMobility? ResolveMobility(WPos position, MovementStage stage, PlanningPersonalField? relocatedField = null)
        => _mobility.Where(m => m.ActionId != 7419 || relocatedField == null)
            .Concat(MobilityMechanics.Select(m => m.Resolve(this, position, stage, m.ActionId == 7419 ? relocatedField?.Center : null)).OfType<PlanningMobility>())
            .FirstOrDefault(m => m.Kind == stage.Kind && m.ActionId == stage.ActionId && m.AnchorId == stage.AnchorId
                && m.Facing == stage.Facing && MobilityOrigin(m).AlmostEqual(position, 0.01f)
                && MobilityLanding(m).AlmostEqual(stage.To, 0.01f) && !RejectedMobility.Any(r => r.Matches(this, m)));
    internal bool DestinationAllowed(int cell) => _allowedDestinations[cell];
    internal float NativePriority(int cell) => _map.PixelPriority[cell];
    internal float Presentation(int cell) => _presentation[cell];
    internal bool CanRemain(WPos point) => TimedTraversal.CanRemain(_map, point, Radius)
        && !Teleports.Any(t => point.InCircle(t.Entrance, t.Radius));
    internal bool CanChooseStationaryAction(WPos point) => !Teleports.Any(t => point.InCircle(t.Entrance, t.Radius));
    internal bool DestinationSatisfied(WPos point, float time)
        => time <= ArrivalDeadline && (RequiredDestination is not { } required || point.AlmostEqual(required, 0.001f));
    internal bool Traverse(WPos from, WPos to, float elapsed, float duration, out float slack)
        => TimedTraversal.Check(_map, from, to, elapsed, duration, Radius, out slack);

    internal bool TraverseOrdinary(WPos from, WPos to, float elapsed, float duration, out float slack)
    {
        slack = float.MaxValue;
        return Teleports.All(t => t.PermitsOrdinaryTravel(from, to)) && Traverse(from, to, elapsed, duration, out slack);
    }

    internal IEnumerable<PlanningTeleport> TeleportsFrom(WPos point)
        => Teleports.Where(t => (t.Entrance - point).Length() <= t.Radius + _map.Resolution * 1.5f);

    internal ExecutionCoverage Evaluate(FrozenPlanningAction action, WPos position, bool ignorePositional = false)
    {
        if (action.Opportunity is { } opportunity)
            return opportunity.Evaluate(position, ignorePositional);
        var cell = Cell(position);
        return cell >= 0 && (action.RecipientsByCell[cell] & action.RequiredRecipients) == action.RequiredRecipients
            ? new(true, action.ValueByCell[cell]) : default;
    }

    internal bool Covers(FrozenPlanningAction action, WPos position, ulong recipientId, bool ignorePositional = false)
    {
        if (action.Opportunity is { } opportunity)
            return opportunity.Covers(position, recipientId, ignorePositional);
        var cell = Cell(position);
        var index = action.RecipientIds.IndexOf(recipientId);
        return cell >= 0 && index >= 0 && (action.RecipientsByCell[cell] & (1ul << index)) != 0;
    }

    // Compare actual captured values, never delegate/object identity or a hash used as proof.
    // Time limits are relative to capture: equivalent countdown observations do not restart work.
    internal bool SameProblem(MovementPlanningContext previous, bool continuingVerifiedRoute = false, bool ignoreRejections = false)
        => ProblemChange(previous, continuingVerifiedRoute, ignoreRejections) == null;

    internal string? ProblemChange(MovementPlanningContext previous, bool continuingVerifiedRoute = false, bool ignoreRejections = false)
    {
        var age = (float)(Identity.WorldTime - previous.Identity.WorldTime).TotalSeconds;
        if (age < 0) return "age";
        if (Identity.ActorId != previous.Identity.ActorId) return "Identity.ActorId";
        if (Identity.ZoneId != previous.Identity.ZoneId) return "Identity.ZoneId";
        if (Identity.ContentId != previous.Identity.ContentId) return "Identity.ContentId";
        if (Identity.ProjectionLayer != previous.Identity.ProjectionLayer) return "Identity.ProjectionLayer";
        if (TargetId != previous.TargetId) return "TargetId";
        if (PartyContact != previous.PartyContact) return "party contact region";
        if ((ComboRecovery == null) != (previous.ComboRecovery == null)
            || ComboRecovery != null && !ComboRecovery.Same(previous.ComboRecovery!, age)) return "observed combo recovery";
        if (FieldPlacement == null ? previous.FieldPlacement != null : previous.FieldPlacement == null || !FieldPlacement.Same(previous.FieldPlacement, age))
            return "field placement opportunity";
        if (Resources != previous.Resources) return "Resources";
        if (PersonalField == null ? previous.PersonalField != null : previous.PersonalField == null || !PersonalField.Same(previous.PersonalField, age))
            return "personal field";
        if (Identity.Speed != previous.Identity.Speed) return "Identity.Speed";
        if (!ignoreRejections && !RejectedMobility.SequenceEqual(previous.RejectedMobility)) return "rejected mobility";
        if (Identity.Casting != previous.Identity.Casting) return "Identity.Casting";
        if (Optimize != previous.Optimize) return "Optimize";
        if (StationaryOnly != previous.StationaryOnly) return "stationary target permission";
        if ((ForcedMovement == null) != (previous.ForcedMovement == null)) return "ForcedMovement";
        if (ForcedMovement != null && !ForcedMovement.Same(previous.ForcedMovement!, age)) return "ForcedMovement";
        if (KnockbackPhases == null ? previous.KnockbackPhases != null : previous.KnockbackPhases == null || !KnockbackPhases.Same(previous.KnockbackPhases))
            return "native displacement sources or phase constraints";
        if (CooldownEvidenceLimited != previous.CooldownEvidenceLimited) return "CooldownEvidenceLimited";
        if (Teleports.Length != previous.Teleports.Length) return "Teleports.Length";
        if (Teleports.Where((t, i) => !t.Same(previous.Teleports[i], age)).Any()) return "teleporter timing";
        if (Recharges.Length != previous.Recharges.Length) return "Recharges.Length";
        if (Recharges.Where((r, i) => !r.Same(previous.Recharges[i], age)).Any()) return "resource recharge";
        if (Utilities.Length != previous.Utilities.Length) return "Utilities.Length";
        for (var i = 0; i < Utilities.Length; ++i)
            if (!Utilities[i].Same(previous.Utilities[i], age))
            {
                var current = Utilities[i];
                var prior = previous.Utilities[i];
                return FormattableString.Invariant($"utility {current.ActionId}/{prior.ActionId}: ready {current.ReadyIn:R}/{prior.ReadyIn:R}, age {age:R}, lock {current.LockSeconds:R}/{prior.LockSeconds:R}, resource {current.Resource}/{prior.Resource}, extra {current.AdditionalResourceCost}/{prior.AdditionalResourceCost}");
            }
        if (Zones.Length != previous.Zones.Length) return "Zones.Length";
        if (Zones.Where((z, i) => !z.Same(previous.Zones[i], age)).Any()) return "friendly zone";
        if (Math.Abs(PositionalBypassUntil - Math.Max(0, previous.PositionalBypassUntil - age)) > 0.01f) return "positional status deadline";
        if (!Travel.Same(previous.Travel, age)) return "travel speed";
        if (DowntimeFacing != previous.DowntimeFacing) return "DowntimeFacing";
        if (RequiredDestination != previous.RequiredDestination) return "RequiredDestination";
        if (LateDepartureMargin != previous.LateDepartureMargin) return "LateDepartureMargin";
        if ((ArrivalDeadline == float.MaxValue || previous.ArrivalDeadline == float.MaxValue
                ? ArrivalDeadline != previous.ArrivalDeadline : Math.Abs(ArrivalDeadline - Math.Max(0, previous.ArrivalDeadline - age)) > 0.002f)) return "ArrivalDeadline";
        if (Range != previous.Range) return "Range";
        if (!ExecutionRules.SameRules(previous.ExecutionRules, age)) return "execution constraint";
        if (Radius != previous.Radius) return "Radius";
        if (!continuingVerifiedRoute && !Origin.AlmostEqual(previous.Origin, 0.25f)) return "origin";
        if (Math.Abs(UnlockSeconds - Math.Max(0, previous.UnlockSeconds - age)) > 0.01f) return "cast lock";
        if (_map.Width != previous._map.Width) return "_map.Width";
        if (_map.Height != previous._map.Height) return "_map.Height";
        if (_map.Center != previous._map.Center) return "_map.Center";
        if (_map.Rotation != previous._map.Rotation) return "_map.Rotation";
        if (_map.Resolution != previous._map.Resolution) return "_map.Resolution";
        if (!_allowedDestinations.AsSpan().SequenceEqual(previous._allowedDestinations)) return "allowed destinations";
        if (!_map.PixelPriority.AsSpan(0, Cells).SequenceEqual(previous._map.PixelPriority.AsSpan(0, Cells))) return "native goal field";
        if (!_mobility.SequenceEqual(previous._mobility)) return "mobility edges";
        if (_actions.Length != previous._actions.Length) return "_actions.Length";
        if (MobilityMechanics.Length != previous.MobilityMechanics.Length) return "MobilityMechanics.Length";
        if (MobilityMechanics.Where((m, i) => !m.SameMechanics(previous.MobilityMechanics[i], age)).Any()) return "mobility mechanics";
        if ((Native == null) != (previous.Native == null)) return "Native";
        if (Native != null && !Native.SameTransitions(previous.Native!)) return "Native";

        for (var i = 0; i < Cells; ++i)
        {
            var now = _map.PixelMaxG[i];
            var before = previous._map.PixelMaxG[i];
            if (now == float.MaxValue || before == float.MaxValue || now < 0 || before < 0)
            {
                if (now != before)
                    return "native warning deadline";
            }
            else if (Math.Abs(now - Math.Max(0, before - age)) > 0.002f)
                return "native warning countdown";
        }
        for (var i = 0; i < _actions.Length; ++i)
        {
            var now = _actions[i];
            var before = previous._actions[i];
            if (now.UsesHardTarget != before.UsesHardTarget || now.TargetChoices.Length != before.TargetChoices.Length
                || now.TargetChoices.Where((choice, index) => choice.SetupSeconds != before.TargetChoices[index].SetupSeconds
                    || choice.VerticalOffset != before.TargetChoices[index].VerticalOffset
                    || !choice.Opportunity.SameGeometry(before.TargetChoices[index].Opportunity)).Any()) return "action target choices";
            if (now.Id != before.Id || now.TargetId != before.TargetId || now.CastSeconds != before.CastSeconds
                || (now.Timing == null) != (before.Timing == null) || now.Timing != null && !now.Timing.Same(before.Timing!, age)
                || now.LockSeconds != before.LockSeconds || now.RequiredRecipients != before.RequiredRecipients
                || now.RequiredEarlierActions != before.RequiredEarlierActions || !now.RecipientIds.SequenceEqual(before.RecipientIds)
                || (now.Opportunity == null) != (before.Opportunity == null)
                || now.Opportunity != null && !now.Opportunity.SameGeometry(before.Opportunity!)
                || !now.RecipientsByCell.SequenceEqual(before.RecipientsByCell) || !now.ValueByCell.SequenceEqual(before.ValueByCell)
                || now.HorizonLimited != before.HorizonLimited
                || Math.Abs(now.Earliest - Math.Max(0, before.Earliest - age)) > 0.01f
                || Math.Abs(now.Latest - (before.HorizonLimited ? before.Latest : before.Latest - age)) > 0.01f)
                return FormattableString.Invariant($"action {now.Id}/{before.Id}: ready {now.Earliest:R}/{before.Earliest:R}, age {age:R}, latest {now.Latest:R}/{before.Latest:R}, cast {now.CastSeconds:R}/{before.CastSeconds:R}, geometry {now.Opportunity != null && before.Opportunity != null && now.Opportunity.SameGeometry(before.Opportunity)}");
        }
        return null;
    }
    internal static int ChargeCount(ulong resources, int resource) => (int)((resources >> (4 * resource)) & 15);
    internal static bool NoFewerResources(ulong a, ulong b)
    {
        for (var i = 0; i < 16; ++i)
            if (ChargeCount(a, i) < ChargeCount(b, i))
                return false;
        return true;
    }
}

internal sealed record MovementPlan(NavigationIdentity Input, MovementSearchStatus Status, ImmutableArray<MovementStage> Stages,
    WPos UsefulDestination, WPos? ImmediateWaypoint, float PlanSlackSeconds, float DurationSeconds,
    ulong RemainingResources, byte DeliveredActions, ImmutableArray<float> ExecutionValues, float NativePriority,
    int ExpandedStates, double SearchMilliseconds, string Evidence)
{
    internal ulong TargetId { get; init; }
    internal PlanningFieldPlacement? FieldPlacement { get; init; }
    internal float FieldPlacementGain { get; init; }
    internal Angle? DowntimeFacing { get; init; }
    internal ImmutableArray<PlannedActionEvidence> ActionEvidence { get; init; } = [];
    internal ImmutableArray<float> RecoveryTimes { get; init; } = [];
    internal ImmutableArray<PlanningZone> UsefulZones { get; init; } = [];
    internal ImmutableArray<float> DeliveryTimes { get; init; } = [];
    internal MovementSearchBound Bound { get; init; }
    // Immutable label state permits continuing a verified incumbent in the same
    // capture without losing spent resources, recharge clocks or utility expiry.
    internal PlanningResourceState? EndInventory { get; init; }
    internal float EndPositionalBypassUntil { get; init; }
    internal PlanningTravel? EndTravel { get; init; }
    internal float OriginNativePriority { get; init; }
    internal float DestinationPresentation { get; init; }
    internal float OriginPresentation { get; init; }
    internal WPos? PriorDestination { get; init; }
    internal PlanningRange? PartyContact { get; init; }
    internal PlanningComboRecovery? ComboRecovery { get; init; }
    internal float ComboReadyAt { get; init; }
    internal float ComboExecutionValue { get; init; }
    internal int ResourcePrunedActions { get; init; }
    internal NavigationDecision? NativeRecovery { get; init; }
    // Only the provisional recovery publication carries this flag. A finished
    // search with no strict route must execute recovery, even if a new job starts.
    internal bool OptimizationPending { get; init; }
    internal bool HasVerifiedRoute => NativePriority != float.MinValue && Status is MovementSearchStatus.Searching or MovementSearchStatus.Complete or MovementSearchStatus.Bounded;
    // Slack measures delaying the entire sequence. Cast/wait stages before the
    // first movement are already planned time, not extra delay. Native rotation
    // consumers need the actual departure deadline to retain that cast window.
    internal float? FirstDepartureDeadline { get; init; }
    internal float DepartureDeadlineSeconds => FirstDepartureDeadline ?? (PlanSlackSeconds == float.MaxValue ? float.MaxValue
        : PlanSlackSeconds + (Stages.FirstOrDefault(s => s.From != s.To)?.StartSeconds ?? 0));
    internal float RemainingDepartureSeconds(long timestamp)
        => Math.Max(0, DepartureDeadlineSeconds - (float)((timestamp - Input.CapturedTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency));
}
