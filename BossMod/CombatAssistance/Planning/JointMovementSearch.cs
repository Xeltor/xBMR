using System.Diagnostics;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

// A label-setting search on the native raster. Labels keep action progress and resource state.
// An earlier arrival dominates only when it can safely wait to reproduce the later arrival.
internal static class JointMovementSearch
{
    // Dominance requires equality in each of these discrete state dimensions.
    // Partition the Pareto frontier before comparing its continuous quantities.
    private readonly record struct FrontierKey(int Cell, byte Actions, int Knockbacks, MovementStageKind? LastKind, ulong Target, byte TargetChoices);
    // Production work ends at the real input/departure deadline, not an arbitrary per-query
    // time or expansion quota. The explicit quotas are useful for deterministic bounded tests.
    // Bound allocations as well: more labels can be meaningful but are not free memory.
    internal sealed record Limits(int Expansions = int.MaxValue, double Milliseconds = double.PositiveInfinity,
        int CreatedStates = 65536, bool RespectFreshness = true);
    private sealed record Label(int Cell, WPos Position, float Time, PlanningResourceState Inventory, byte Actions,
        ImmutableArray<float> Values, ImmutableArray<float> DeliveryTimes, float Distance, float Slack, Label? Parent, ImmutableArray<MovementStage> Stages,
        int Phases = 0, float PositionalBypassUntil = 0, PlanningTravel? Travel = null, bool SpentResources = false, float ComboReadyAt = 0,
        bool FieldEntryByAbility = false, PlanningPersonalField? RelocatedField = null, ImmutableArray<float> RecoveryTimes = default, bool FieldBenefitAllowed = true,
        ulong FocusTarget = 0, byte TargetChoices = 0, WPos? FieldPlacementAt = null, float FieldPlacementGain = 0, int Knockbacks = -1, float? FirstDeparture = null,
        float ActionReadyAt = 0, float GcdReadyAt = 0, float ImmunityAppliedBy = float.MaxValue, float ImmunityUntil = 0, uint ImmunityActionId = 0,
        float ImmunityPossibleFrom = float.MaxValue, float ImmunityPossibleUntil = 0, PlanningRangeAccess? RangeAccess = null)
    {
        // Normalize each new candidate in place BEFORE admission. Admitted labels
        // and their parent chains are never mutated; route publications remain frozen.
        public PlanningResourceState Inventory { get; set; } = Inventory;
        public int Phases { get; set; } = Phases;
        public float PositionalBypassUntil { get; set; } = PositionalBypassUntil;
        public PlanningTravel? Travel { get; set; } = Travel;
        public bool SpentResources { get; set; } = SpentResources;
        public float ComboReadyAt { get; set; } = ComboReadyAt;
        public bool FieldEntryByAbility { get; set; } = FieldEntryByAbility;
        public PlanningPersonalField? RelocatedField { get; set; } = RelocatedField;
        public ImmutableArray<float> RecoveryTimes { get; set; } = RecoveryTimes;
        public bool FieldBenefitAllowed { get; set; } = FieldBenefitAllowed;
        public ulong FocusTarget { get; set; } = FocusTarget;
        public byte TargetChoices { get; set; } = TargetChoices;
        public WPos? FieldPlacementAt { get; set; } = FieldPlacementAt;
        public float FieldPlacementGain { get; set; } = FieldPlacementGain;
        public int Knockbacks { get; set; } = Knockbacks;
        public float? FirstDeparture { get; set; } = FirstDeparture;
        public float ActionReadyAt { get; set; } = ActionReadyAt;
        public float GcdReadyAt { get; set; } = GcdReadyAt;
        public float ImmunityAppliedBy { get; set; } = ImmunityAppliedBy;
        public float ImmunityUntil { get; set; } = ImmunityUntil;
        public uint ImmunityActionId { get; set; } = ImmunityActionId;
        public float ImmunityPossibleFrom { get; set; } = ImmunityPossibleFrom;
        public float ImmunityPossibleUntil { get; set; } = ImmunityPossibleUntil;
        public PlanningRangeAccess? RangeAccess { get; set; } = RangeAccess;
        // Queue membership only; never used as movement or safety evidence.
        public bool Retired { get; set; }
    }

    internal static MovementPlan Search(MovementPlanningContext context, Limits? limits = null, CancellationToken cancellation = default,
        MovementPlan? nativeIncumbent = null, Action<MovementPlan>? publishBest = null,
        bool includeMobilityActions = true, bool stopAtCurrentOpportunity = false, bool pruneResourceInferiorMobility = true,
        bool pruneExecutionInferiorStates = true, bool includeSpatialMobilityActions = true, bool proveCompleteOptimum = true,
        bool partitionFrontier = true)
    {
        limits ??= new();
        var clock = Stopwatch.StartNew();
        var labels = new Dictionary<FrontierKey, List<Label>>();
        var queue = new PriorityQueue<Label, (float Time, long Order)>();
        long serial = 0;
        var expansions = 0;
        var resourcePrunedActions = 0;
        var knockbacks = context.KnockbackPhases?.Phases ?? [];
        var frames = new[] { context }.Concat(knockbacks.Select(phase => context.WithDisplacementMap(phase.After))).ToArray();
        var maximumNativePriority = Enumerable.Range(0, context.Cells)
            .Where(cell => context.DestinationAllowed(cell) && frames[^1].HasPermanentCell(cell))
            .Select(frames[^1].NativePriority).DefaultIfEmpty(float.MinValue).Max();
        var maximumPresentation = Enumerable.Range(0, context.Cells).Select(context.Presentation).Max();
        var maximumComboValue = context.ComboRecovery?.MaximumValue ?? 0;
        var canImprovePlacement = context.FieldPlacement?.MayImprove(context) == true;
        var restrictFieldEntry = context.Actions.Any(a => a.Timing?.FieldEntryByAbility == true);
        var maximumActionValues = context.Actions.Select(action => action.Opportunity is { } opportunity
            // Match the evaluator's ordered float arithmetic. A wider-precision
            // LINQ sum can round below an achievable float accumulation and is
            // therefore not a valid upper bound for pruning.
            ? action.TargetChoices.Select(choice => choice.Opportunity).Prepend(opportunity)
                .Max(option => option.MaximumValue) : action.ValueByCell.Max()).ToArray();
        var recoveryWitness = nativeIncumbent is { HasVerifiedRoute: true } nativeSeed && nativeSeed.Input == context.Identity
            && nativeSeed.Stages.All(s => s.Kind is MovementStageKind.Walk or MovementStageKind.Wait or MovementStageKind.Cast)
                ? new[] { context.Origin }.Concat(nativeSeed.Stages.Where(s => s.Kind == MovementStageKind.Walk).Select(s => s.To)).ToArray() : [];
        var createdStates = 0;
        var memoryExhausted = false;
        var sequenceLimited = false;
        var bound = MovementSearchBound.None;
        Label? best = null;
        var startCell = context.Cell(context.Origin);
        if (knockbacks.IsEmpty && context.KnockbackPhases is { } captured
            && context.ForcedMovement?.Routes.Any(route => route.Knockback != null) != true
            && NativeKnockbackAuthorityCapture.Capture(captured.Inventory) != null)
        {
            bound = MovementSearchBound.NativeDisplacement;
            return Empty(MovementSearchStatus.NoVerifiedRoute, "native displacement has no supported phase projection");
        }
        if (!knockbacks.IsEmpty && (context.StationaryOnly
            || context.KnockbackPhases?.MatchesInventory(context.Identity, context.ForcedMovement) != true
            || knockbacks.Any(phase => phase.Timing == null)))
        {
            bound = MovementSearchBound.NativeDisplacement;
            return Empty(MovementSearchStatus.NoVerifiedRoute, "native displacement needs complete motion timing and execution authority");
        }
        var status = MovementSearchStatus.Complete;
        if (startCell < 0 || !context.Traverse(context.Origin, context.Origin, 0, context.UnlockSeconds, out var initialSlack))
            return Empty(MovementSearchStatus.NoVerifiedRoute, "native recovery required before locked action can finish");
        var initialStages = context.UnlockSeconds > 0
            ? ImmutableArray.Create(new MovementStage(MovementStageKind.Wait, context.Origin, context.Origin, 0, context.UnlockSeconds)) : [];
        if (context.StationaryOnly && context.ForcedMovement != null)
            return Empty(MovementSearchStatus.NoVerifiedRoute, "stationary target permission cannot execute compulsory displacement");
        if (context.ForcedMovement is { } forced)
        {
            foreach (var route in forced.Routes)
                if (route.Evaluate(context, out var routeSlack))
                    Add(new(context.Cell(route.Landing), route.Landing, route.End,
                        PlanningResourceState.Initial(context.Resources, context.Recharges), 0,
                        ImmutableArray.CreateRange(Enumerable.Repeat(0f, context.Actions.Length)),
                        ImmutableArray.CreateRange(Enumerable.Repeat(float.PositiveInfinity, context.Actions.Length)),
                        0, routeSlack, null, [new(MovementStageKind.ForcedMovement, context.Origin, route.Landing,
                            0, route.End, Facing: route.Heading, Forced: route)], PositionalBypassUntil: context.PositionalBypassUntil));
        }
        else
            Add(new(startCell, context.Origin, context.UnlockSeconds, PlanningResourceState.Initial(context.Resources, context.Recharges), 0,
                ImmutableArray.CreateRange(Enumerable.Repeat(0f, context.Actions.Length)),
                ImmutableArray.CreateRange(Enumerable.Repeat(float.PositiveInfinity, context.Actions.Length)), 0, initialSlack, null, initialStages, PositionalBypassUntil: context.PositionalBypassUntil));
        if (context.ForcedMovement == null && knockbacks.IsEmpty && nativeIncumbent is { HasVerifiedRoute: true } seed && seed.Input == context.Identity
            && (seed.EndInventory != null || context.Recharges.IsEmpty || seed.Stages.All(s => s.Kind is MovementStageKind.Walk or MovementStageKind.Wait
                or MovementStageKind.Cast or MovementStageKind.Teleport))
            && seed.ExecutionValues.Length == context.Actions.Length
            && (seed.DeliveredActions == 0 || seed.RecoveryTimes.Length == context.Actions.Length)
            && MovementPlanValidation.Validate(seed, context, 0, true, context.Identity.CapturedTimestamp).Allowed)
            Add(new(context.Cell(seed.UsefulDestination), seed.UsefulDestination, seed.DurationSeconds,
                seed.EndInventory ?? PlanningResourceState.Initial(context.Recharges.IsEmpty ? seed.RemainingResources : context.Resources, context.Recharges),
                seed.DeliveredActions, seed.ExecutionValues,
                seed.DeliveryTimes.Length == context.Actions.Length ? seed.DeliveryTimes : ImmutableArray.CreateRange(Enumerable.Repeat(float.PositiveInfinity, context.Actions.Length)),
                seed.Stages.Sum(s => (s.To - s.From).Length()), seed.PlanSlackSeconds, null, seed.Stages,
                PositionalBypassUntil: Math.Max(context.PositionalBypassUntil, seed.EndPositionalBypassUntil), Travel: seed.EndTravel,
                GcdReadyAt: seed.EndGcdReadyAt,
                ActionReadyAt: seed.Stages.Where(s => float.IsFinite(s.ActionLockUntil)).Select(s => s.ActionLockUntil).DefaultIfEmpty(0).Max(),
                RecoveryTimes: seed.RecoveryTimes.Length == context.Actions.Length ? seed.RecoveryTimes : default,
                FieldPlacementAt: seed.Stages.FirstOrDefault(s => s.Kind == MovementStageKind.FieldPlacement)?.To,
                FieldPlacementGain: seed.FieldPlacementGain,
                FocusTarget: seed.Stages.LastOrDefault(s => s.Kind == MovementStageKind.Target)?.AnchorId ?? context.TargetId,
                TargetChoices: (byte)seed.Stages.Where(s => s.Kind == MovementStageKind.Target && s.SourceActionIndex >= 0)
                    .Aggregate(0, (mask, stage) => mask | (1 << stage.SourceActionIndex))));

        while (queue.Count != 0)
        {
            if (cancellation.IsCancellationRequested)
                return Empty(MovementSearchStatus.Invalidated, "superseded input");
            if (proveCompleteOptimum && ReachedBound() == MovementSearchBound.None && AtAbsoluteOptimum()) break;
            // Finish alternatives that can start the same current opportunity
            // on time before publishing the walking pass. First arrival alone
            // must not commit an inside-hitbox point when an equally timely,
            // resource-equivalent useful position remains in the frontier.
            if (stopAtCurrentOpportunity && knockbacks.IsEmpty && best != null && (context.Actions.Length != 0
                    || context.Range?.RecoverExecution == true || context.HealingCoverage?.Satisfied(context.Origin) == false)
                && context.PreservesPresent(best.Actions)
                && (context.ComboRecovery == null || ComboValue(best) >= maximumComboValue)
                && (context.PartyContact == null || context.PartyContact.Loss(best.Position) == 0)
                && (context.HealingCoverage == null || context.HealingCoverage.Satisfied(best.Position))
                && context.Zones.Where(zone => zone.EndsIn > best.Time).All(zone => context.Zones.Any(covered =>
                    covered.Benefit == zone.Benefit && covered.Contains(best.Position, best.Time, best.RelocatedField)))
                && (context.Range?.RecoverExecution != true || RangeOutcome(best) is { Loss: 0 })
                && queue.Peek().Time > (context.Range?.RecoverExecution == true
                    || context.HealingCoverage?.Satisfied(context.Origin) == false
                        ? Math.Max(best.Time, best.RangeAccess?.ReadyAt ?? 0) : best.DeliveryTimes.Max()))
                return BuildPlan(best, MovementSearchStatus.Bounded, MovementSearchBound.SearchInProgress);
            // A complete no-spend native witness already realizes the absolute
            // current instant opportunity and its next readiness contact. Only
            // skip the remaining arrival comparison if its presentation also
            // reaches the ceiling: a center-directed seed is not a final arrival.
            if (stopAtCurrentOpportunity && best != null && knockbacks.IsEmpty && context.PreparationMask == 0
                && context.PreservesPresent(best.Actions) && !best.SpentResources
                && context.HealingCoverage == null && context.ComboRecovery == null && context.Zones.IsEmpty && !canImprovePlacement
                && frames[best.Knockbacks].NativePriority(best.Cell) == maximumNativePriority
                && context.Presentation(best.Cell) == maximumPresentation
                // Being in attack range does not settle continuity. An ongoing
                // walk can reach its committed destination during the same GCD
                // window with identical delivery, coverage and resources. Let
                // those labels compete before publishing a stationary replacement.
                && (context.CommittedDestination is not { } continuing || best.Position.AlmostEqual(continuing, MovementPlanValidation.ArrivalTolerance))
                && best.RangeAccess is { } contact && RangeOutcome(best) is { Loss: 0 } reached && reached.At == contact.ReadyAt
                && context.Actions.Length <= 1 && (context.Actions.IsEmpty || context.Actions[0] is { CastSeconds: 0, ResourceCost: 0, GrantedResources: 0 } currentAction
                    && currentAction.Timing is not { WithoutProc: > 0 } && currentAction.Timing is not { UnbuffedUpperBound: > 0 }
                    && best.DeliveryTimes[0] == Math.Max(context.UnlockSeconds, currentAction.Earliest)
                    && best.Values[0] == maximumActionValues[0]))
                return BuildPlan(best, MovementSearchStatus.Bounded, MovementSearchBound.SearchInProgress);
            bound = ReachedBound();
            if (bound != MovementSearchBound.None)
            {
                status = MovementSearchStatus.Bounded;
                break;
            }
            var current = queue.Dequeue();
            if (current.Retired || CannotImproveExecution(current))
                continue;
            ++expansions;
            var frame = frames[current.Knockbacks];
            if (current.Knockbacks < knockbacks.Length)
            {
                var phase = knockbacks[current.Knockbacks];
                if (current.ImmunityActionId != 0 && phase.TryPrevent(frame, frames[current.Knockbacks + 1],
                    current.Position, current.Time, current.ImmunityAppliedBy, current.ImmunityUntil, out var immuneSlack, current.ImmunityActionId))
                    Add(new(current.Cell, current.Position, current.Time, current.Inventory, current.Actions, current.Values,
                        current.DeliveryTimes, current.Distance, Math.Min(current.Slack, immuneSlack), current,
                        [new(MovementStageKind.Knockback, current.Position, current.Position, current.Time, current.Time,
                            Knockback: phase, PreventedByActionId: current.ImmunityActionId)], Knockbacks: current.Knockbacks + 1));
                // Immunity is automatic. A covered source cannot be accepted as
                // a useful push, and partial expiry/application uncertainty cannot
                // certify either outcome from this frozen input.
                if (context.KnockbackPhases?.PlanLandings != false && !phase.MayBePrevented(frame, current.ImmunityPossibleFrom, current.ImmunityPossibleUntil)
                    && phase.TryTransition(frame, frames[current.Knockbacks + 1], current.Position, current.Time,
                    out var landing, out var earliest, out var completed, out var slack))
                {
                    var stages = ImmutableArray.CreateBuilder<MovementStage>();
                    if (current.Time < earliest)
                        stages.Add(new(MovementStageKind.Wait, current.Position, current.Position, current.Time, earliest));
                    stages.Add(new(MovementStageKind.Knockback, current.Position, landing, earliest, completed, Knockback: phase));
                    Add(new(context.Cell(landing), landing, completed, current.Inventory, current.Actions, current.Values,
                        current.DeliveryTimes, current.Distance, Math.Min(current.Slack, slack), current, stages.ToImmutable(),
                        Knockbacks: current.Knockbacks + 1, FirstDeparture: InitialDeparture(current, earliest)));
                }
            }
            // A route may wait for its spent charge to return without spending
            // it again. That inventory can make an otherwise equivalent useful
            // destination preferable; readiness checks on a second action alone
            // would miss this alternative.
            foreach (var release in context.ExecutionRules.ReleasesAfter(current.Time).Concat(current.Inventory.Next)
                .Append(current.RangeAccess?.ReadyAt ?? float.MaxValue)
                .Where(t => t > current.Time && t <= MovementPlanningContext.MaximumHorizonSeconds).Distinct())
                if (frame.Traverse(current.Position, current.Position, current.Time, release - current.Time, out var waitSlack))
                    Add(new(current.Cell, current.Position, release, current.Inventory, current.Actions, current.Values,
                        current.DeliveryTimes, current.Distance, Math.Min(current.Slack, waitSlack), current,
                        [new(MovementStageKind.Wait, current.Position, current.Position, current.Time, release)]));
            var x = current.Cell % context.Width;
            var y = current.Cell / context.Width;
            foreach (var zone in context.Zones)
                if (context.Cell(zone.EffectiveCenter(current.RelocatedField)) is var zoneCell && zoneCell >= 0
                    && Math.Abs(zoneCell % context.Width - x) <= 1 && Math.Abs(zoneCell / context.Width - y) <= 1)
                    WalkTo(zoneCell, zone.EffectiveCenter(current.RelocatedField));
            if (context.RequiredDestination is { } required && context.Cell(required) is var requiredCell && requiredCell >= 0
                && Math.Abs(requiredCell % context.Width - x) <= 1 && Math.Abs(requiredCell / context.Width - y) <= 1)
                WalkTo(requiredCell, required);
            if (context.CommittedDestination is { } committed && context.Cell(committed) is var committedCell && committedCell >= 0
                && Math.Abs(committedCell % context.Width - x) <= 1 && Math.Abs(committedCell / context.Width - y) <= 1)
                WalkTo(committedCell, committed);
            for (var dy = -1; dy <= 1; ++dy)
                for (var dx = -1; dx <= 1; ++dx)
                {
                    if (x + dx < 0 || x + dx >= context.Width || y + dy < 0 || y + dy >= context.Height)
                        continue;
                    var cell = (y + dy) * context.Width + x + dx;
                    var destination = context.Point(cell);
                    WalkTo(cell, destination);
                }

            void WalkTo(int cell, WPos destination)
            {
                var length = (destination - current.Position).Length();
                if (context.StationaryOnly || length <= 1e-6f)
                    return;
                var duration = current.Travel!.Seconds(length, current.Time);
                if (current.Time + duration <= MovementPlanningContext.MaximumHorizonSeconds
                    && context.ExecutionRules.PermitsMovement(current.Time, current.Time + duration)
                    && frame.TraverseOrdinary(current.Position, destination, current.Time, duration, out var slack))
                    Add(new(cell, destination, current.Time + duration, current.Inventory, current.Actions, current.Values, current.DeliveryTimes,
                        current.Distance + length, Math.Min(current.Slack, slack), current,
                        [new(MovementStageKind.Walk, current.Position, destination, current.Time, current.Time + duration)]));
            }

            foreach (var teleport in context.StationaryOnly ? [] : frame.TeleportsFrom(current.Position))
                if (teleport.Evaluate(frame, current.Position, current.Travel!, current.Time, out var end, out var slack))
                    Add(new(context.Cell(teleport.Exit), teleport.Exit, end, current.Inventory, current.Actions, current.Values,
                        current.DeliveryTimes, current.Distance + (teleport.Entrance - current.Position).Length(), Math.Min(current.Slack, slack), current,
                        [new(MovementStageKind.Teleport, current.Position, teleport.Exit, current.Time, end, Transit: teleport)]));

            // Target choice is part of the whole route. A plan may leave an
            // urgent warning before preparing its already-chosen action anchor.
            // Every setup interval uses the same native deadlines as walking.
            if (context.HasTargetChoices)
                for (var index = 0; index < context.Actions.Length; ++index)
                {
                    var source = context.Actions[index];
                    var bit = (byte)(1 << index);
                    if (source.TargetChoices.IsDefaultOrEmpty || (current.Actions & bit) != 0
                        || (current.TargetChoices & bit) != 0) continue;
                    foreach (var choice in source.TargetChoices)
                    {
                        if (choice.Opportunity.TargetId == current.FocusTarget || !choice.CanAcquire(current.Position)) continue;
                        var end = current.Time + choice.SetupSeconds;
                        if (end > source.Latest || end > MovementPlanningContext.MaximumHorizonSeconds
                            || !frame.Traverse(current.Position, current.Position, current.Time, choice.SetupSeconds, out var slack)) continue;
                        Add(new(current.Cell, current.Position, end, current.Inventory, current.Actions, current.Values, current.DeliveryTimes,
                            current.Distance, Math.Min(current.Slack, slack), current,
                            [new(MovementStageKind.Target, current.Position, current.Position, current.Time, end,
                                source.Id, choice.Opportunity.TargetId, SourceActionIndex: index)],
                            FocusTarget: choice.Opportunity.TargetId, TargetChoices: (byte)(current.TargetChoices | bit)));
                    }
                }

            for (var i = 0; i < context.Actions.Length; ++i)
            {
                var source = context.Actions[i];
                if (context.PreparationLocksActions(current.Actions) && source.Preparation == PlanningPreparationMode.None
                    || context.RejectedMobility.Any(r => r.ActionId == source.Id && r.From.AlmostEqual(current.Position, MovementPlanValidation.ArrivalTolerance))) continue;
                if (NativeRotationalMobility.Contains(source.Id)) continue; // Delivered by its real displacement transition below.
                var action = context.HasTargetChoices && source.UsesHardTarget ? context.ResolveTarget(source, current.FocusTarget) : source;
                if (action == null) continue;
                var bit = (byte)(1 << i);
                var ready = Math.Max(Math.Max(Math.Max(current.Time, current.ActionReadyAt), action.Earliest), action.IsGCD ? current.GcdReadyAt : 0);
                if (!current.Inventory.Ready(action.ResourceCost, ready, context.Recharges, out var begin)) continue;
                var delivered = begin + action.CastAt(current.Position, begin, current.FieldEntryByAbility, current.RelocatedField, current.FieldBenefitAllowed);
                var coverage = context.Evaluate(action, current.Position, delivered < current.PositionalBypassUntil);
                if ((current.Actions & bit) != 0 || (current.Actions & action.RequiredEarlierActions) != action.RequiredEarlierActions
                    || !coverage.Allowed || coverage.Value <= 0 || !context.CanChooseStationaryAction(current.Position))
                    continue;
                var lockUntil = delivered + action.LockSeconds;
                var onset = current.Knockbacks < knockbacks.Length
                    ? (float)(knockbacks[current.Knockbacks].Timing!.EarliestUtc - context.Identity.WorldTime).TotalSeconds : float.PositiveInfinity;
                // Animation lock delays another action, not ordinary walking.
                // Keep it in ActionReadyAt while releasing movement at delivery.
                var end = delivered;
                if (NativeFieldExecution.LosesCurrent(context, action, current.Position, begin, lockUntil, coverage,
                    current.FieldEntryByAbility, current.RelocatedField, current.FieldBenefitAllowed)) continue;
                if (delivered >= onset || delivered > action.Latest || lockUntil > MovementPlanningContext.MaximumHorizonSeconds
                    || !context.ExecutionRules.PermitsAction(coverage.Facing, begin, lockUntil)
                    || !frame.Traverse(current.Position, current.Position, current.Time, end - current.Time, out var slack))
                    continue;
                var stages = ImmutableArray.CreateBuilder<MovementStage>();
                if (begin > current.Time)
                    stages.Add(new(MovementStageKind.Wait, current.Position, current.Position, current.Time, begin));
                var recast = action.Timing?.RecastAt(current.Position, begin, current.FieldEntryByAbility, current.RelocatedField, current.FieldBenefitAllowed) ?? 0;
                stages.Add(new(MovementStageKind.Cast, current.Position, current.Position, begin, end, action.Id, action.TargetId, coverage.Facing,
                    SourceActionIndex: i, PlannedRecastSeconds: recast, ActionLockUntil: lockUntil));
                var afterAction = action.ResourceCost == 0 ? current.Inventory : current.Inventory.Spend(action.ResourceCost, begin, context.Recharges);
                Add(new(current.Cell, current.Position, end, afterAction.Grant(action.GrantedResources), (byte)(current.Actions | bit),
                    current.Values.SetItem(i, coverage.Value), current.DeliveryTimes.SetItem(i, delivered), current.Distance, Math.Min(current.Slack, slack), current, stages.ToImmutable(),
                    RecoveryTimes: current.RecoveryTimes.SetItem(i, Math.Max(lockUntil, begin + recast)), ActionReadyAt: lockUntil,
                    GcdReadyAt: action.IsGCD ? begin + recast : current.GcdReadyAt));
            }

            if (!context.StationaryOnly && !context.PreparationLocksActions(current.Actions) && canImprovePlacement && context.FieldPlacement is { } placement && current.FieldPlacementAt == null
                && context.CanChooseStationaryAction(current.Position))
            {
                var begin = Math.Max(Math.Max(current.Time, current.ActionReadyAt), placement.Earliest);
                var end = begin + placement.LockSeconds;
                var gain = placement.Gain(context, current.Position, end);
                if (begin <= placement.Latest && end <= MovementPlanningContext.MaximumHorizonSeconds && gain > 0
                    && context.ExecutionRules.PermitsAction(null, begin, end)
                    && frame.Traverse(current.Position, current.Position, current.Time, end - current.Time, out var slack))
                {
                    var stages = ImmutableArray.CreateBuilder<MovementStage>();
                    if (begin > current.Time) stages.Add(new(MovementStageKind.Wait, current.Position, current.Position, current.Time, begin));
                    stages.Add(new(MovementStageKind.FieldPlacement, current.Position, current.Position, begin, end,
                        PlanningFieldPlacement.ActionId));
                    Add(new(current.Cell, current.Position, end, current.Inventory, current.Actions, current.Values, current.DeliveryTimes,
                        current.Distance, Math.Min(current.Slack, slack), current, stages.ToImmutable(),
                        FieldPlacementAt: current.Position, FieldPlacementGain: gain));
                }
            }

            foreach (var utility in context.StationaryOnly || context.PreparationLocksActions(current.Actions) ? [] : context.Utilities)
            {
                if (!context.CanChooseStationaryAction(current.Position)
                    || !current.Inventory.Ready(utility.ResourceCost, Math.Max(Math.Max(current.Time, current.ActionReadyAt), utility.ReadyIn), context.Recharges, out var begin)
                    || context.RejectedMobility.Any(r => r.ActionId == utility.ActionId && r.From.AlmostEqual(current.Position, 0.1f))
                    || utility.Effect == PlanningUtilityEffect.IgnorePositional && !context.Actions.Where((_, i) => (current.Actions & (1 << i)) == 0)
                        .Any(a => a.Opportunity?.Positional is Positional.Rear or Positional.Flank or Positional.Front))
                    continue;
                var lockUntil = begin + utility.LockSeconds;
                var end = utility.Effect == PlanningUtilityEffect.MovementSpeed ? begin : lockUntil;
                var until = begin + utility.Duration;
                var appliedBy = begin + utility.ApplicationSeconds;
                if (utility.Effect == PlanningUtilityEffect.KnockbackImmunity
                    && (!knockbacks.Skip(current.Knockbacks).Any(phase => phase.Preventable(frame, appliedBy, until, utility.ActionId))
                        || current.ImmunityPossibleUntil > begin
                        || !NativeKnockbackPhases.SafePhysicalFallback(frame, current.Knockbacks, current.Position, end)))
                    continue;
                if (utility.Effect == PlanningUtilityEffect.IgnorePositional && until <= current.PositionalBypassUntil
                    || utility.Effect == PlanningUtilityEffect.MovementSpeed && (until <= end || utility.Speed <= current.Travel!.At(begin))
                    || lockUntil > MovementPlanningContext.MaximumHorizonSeconds
                    || !context.ExecutionRules.PermitsAction(null, begin, lockUntil)
                    || !frame.Traverse(current.Position, current.Position, current.Time, end - current.Time, out var slack))
                    continue;
                if (utility.Effect == PlanningUtilityEffect.RelocatePersonalField
                    && (utility.Field == null || !utility.Field.LiveAt(end)
                        || !context.Actions.Where((_, i) => (current.Actions & (1 << i)) == 0)
                            .Any(a => a.Timing is { ObservedFieldCast: not null } timing && end < timing.HasteUntil)))
                    continue;
                var stages = ImmutableArray.CreateBuilder<MovementStage>();
                if (begin > current.Time) stages.Add(new(MovementStageKind.Wait, current.Position, current.Position, current.Time, begin));
                stages.Add(new(MovementStageKind.UtilityAction, current.Position, current.Position, begin, end, utility.ActionId, Field: utility.Field,
                    Utility: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? utility : null,
                    ActionLockUntil: utility.Effect == PlanningUtilityEffect.MovementSpeed ? lockUntil : float.NaN));
                var applied = new Label(current.Cell, current.Position, end, current.Inventory.Spend(utility.ResourceCost, begin, context.Recharges), current.Actions,
                    current.Values, current.DeliveryTimes, current.Distance, Math.Min(current.Slack, slack), current, stages.ToImmutable(),
                    PositionalBypassUntil: utility.Effect == PlanningUtilityEffect.IgnorePositional ? until : current.PositionalBypassUntil,
                    ImmunityAppliedBy: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? appliedBy : current.ImmunityAppliedBy,
                    ImmunityUntil: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? until : current.ImmunityUntil,
                    ImmunityActionId: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? utility.ActionId : current.ImmunityActionId,
                    ImmunityPossibleFrom: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? begin : current.ImmunityPossibleFrom,
                    ImmunityPossibleUntil: utility.Effect == PlanningUtilityEffect.KnockbackImmunity ? appliedBy + utility.Duration : current.ImmunityPossibleUntil,
                    Travel: utility.Effect == PlanningUtilityEffect.MovementSpeed ? new(current.Travel!.Baseline, utility.Speed, until) : current.Travel,
                    ActionReadyAt: utility.Effect == PlanningUtilityEffect.MovementSpeed ? lockUntil : current.ActionReadyAt);
                Add(applied);
                if (utility.Effect == PlanningUtilityEffect.MovementSpeed && !includeSpatialMobilityActions
                    && current.Parent == null && current.Position == context.Origin && current.Time == context.UnlockSeconds
                    && context.Range?.NeedsRecovery(context.Origin) == true && knockbacks.IsEmpty && context.ForcedMovement == null
                    && context.Native is { } frozen) SeedEscape(applied, frozen);
            }

            foreach (var mobility in includeMobilityActions && !context.StationaryOnly && !context.PreparationLocksActions(current.Actions) ? frame.MobilityFrom(current.Position, CanSpend, current.RelocatedField, includeSpatialMobilityActions) : [])
            {
                if ((current.Actions & mobility.RequiredEarlierActions) != mobility.RequiredEarlierActions) continue;
                if (cancellation.IsCancellationRequested)
                    return Empty(MovementSearchStatus.Invalidated, "superseded input");
                bound = ReachedBound();
                if (bound != MovementSearchBound.None)
                {
                    status = MovementSearchStatus.Bounded;
                    break;
                }
                var actionIndex = -1;
                for (var i = 0; i < context.Actions.Length; ++i)
                    if (context.ResolveMovingAction(context.Actions[i], mobility.ActionId, mobility.AnchorId) != null) { actionIndex = i; break; }
                var action = actionIndex < 0 ? null : context.ResolveMovingAction(context.Actions[actionIndex], mobility.ActionId, mobility.AnchorId);
                if (action != null && ((current.Actions & (1 << actionIndex)) != 0
                    || action.UsesHardTarget && current.FocusTarget != mobility.AnchorId
                    || (current.Actions & action.RequiredEarlierActions) != action.RequiredEarlierActions || action.IsGCD != mobility.IsGCD)) continue;
                var readyAt = Math.Max(Math.Max(Math.Max(current.Time, current.ActionReadyAt), mobility.ReadyIn),
                    Math.Max(action?.Earliest ?? 0, mobility.IsGCD ? current.GcdReadyAt : 0));
                if (mobility.SourceCell != current.Cell || !context.MobilityOrigin(mobility).AlmostEqual(current.Position, 0.001f)
                    || !current.Inventory.Ready(mobility.ResourceCost, readyAt, context.Recharges, out var preparation))
                    continue;
                var begin = preparation + mobility.SetupSeconds;
                var arrival = begin + mobility.TravelSeconds;
                var end = arrival + mobility.LockSeconds;
                var destination = context.MobilityLanding(mobility);
                var coverage = action == null ? default : context.Evaluate(action, destination, arrival < current.PositionalBypassUntil);
                if (action != null && (action.CastAt(current.Position, begin) != 0 || !coverage.Allowed || coverage.Value <= 0
                    || arrival > action.Latest)) continue;
                // A failed anchor/setup/path rejects this edge only. Other actions remain available.
                if (begin > mobility.AvailableUntil || end > MovementPlanningContext.MaximumHorizonSeconds
                    || mobility.ActionId == 7419 && (current.RelocatedField ?? context.PersonalField) is { } field && !field.LiveAt(end)
                    || current.Position != destination && !context.ExecutionRules.PermitsMovement(preparation, end) || !context.ExecutionRules.PermitsAction(mobility.Facing, preparation, end)
                    || !frame.Traverse(current.Position, current.Position, current.Time, begin - current.Time, out var setupSlack)
                    || !frame.TraverseOrdinary(current.Position, destination, begin, mobility.TravelSeconds, out var travelSlack)
                    || !frame.Traverse(destination, destination, arrival, mobility.LockSeconds, out var landingSlack))
                    continue;
                var stages = ImmutableArray.CreateBuilder<MovementStage>();
                if (preparation > current.Time)
                    stages.Add(new(MovementStageKind.Wait, current.Position, current.Position, current.Time, preparation));
                // Preparation belongs to this action stage. A separate wait would make final
                // validation count facing/setup twice and incorrectly lose a valid opportunity.
                stages.Add(new(mobility.Kind, current.Position, destination, preparation, end, mobility.ActionId, mobility.AnchorId,
                    mobility.Facing, mobility.SetupSeconds, SourceActionIndex: actionIndex,
                    PlannedRecastSeconds: mobility.RecastSeconds, ActionLockUntil: end));
                var landed = new Label(mobility.DestinationCell, destination, end,
                    current.Inventory.Spend(mobility.ResourceCost, begin, context.Recharges),
                    actionIndex < 0 ? current.Actions : (byte)(current.Actions | (1 << actionIndex)),
                    actionIndex < 0 ? current.Values : current.Values.SetItem(actionIndex, coverage.Value),
                    actionIndex < 0 ? current.DeliveryTimes : current.DeliveryTimes.SetItem(actionIndex, arrival),
                    current.Distance + (destination - current.Position).Length(), Math.Min(current.Slack, Math.Min(setupSlack, Math.Min(travelSlack, landingSlack))),
                    current, stages.ToImmutable(),
                    RecoveryTimes: actionIndex < 0 ? current.RecoveryTimes : current.RecoveryTimes.SetItem(actionIndex, Math.Max(end, begin + mobility.RecastSeconds)),
                    ActionReadyAt: end, GcdReadyAt: mobility.IsGCD ? begin + mobility.RecastSeconds : current.GcdReadyAt);
                Add(landed);
                if (!includeSpatialMobilityActions && current.Parent == null && current.Position == context.Origin && current.Time == context.UnlockSeconds
                    && current.Actions == 0 && knockbacks.IsEmpty && context.ForcedMovement == null
                    && context.Native is { } frozen && !frame.CanRemain(destination))
                    SeedEscape(landed, frozen);
            }
        }
        if (memoryExhausted)
        {
            bound = MovementSearchBound.StateMemory;
            status = MovementSearchStatus.Bounded;
        }
        if (sequenceLimited && status == MovementSearchStatus.Complete)
        {
            bound = MovementSearchBound.SequenceLimit;
            status = MovementSearchStatus.Bounded;
        }
        if (context.Actions.Any(a => a.Timing is { OutsideTimingKnown: false }
            || a.Timing != null && a.Timing.OutsideObservationUntil < a.Latest) && status == MovementSearchStatus.Complete)
        {
            bound = MovementSearchBound.ActionTimingEvidence;
            status = MovementSearchStatus.Bounded;
        }
        if (context.Zones.Any(z => !z.CompleteFootprint) && status == MovementSearchStatus.Complete)
        {
            bound = MovementSearchBound.FriendlyEffectGeometry;
            status = MovementSearchStatus.Bounded;
        }
        if (context.CooldownEvidenceLimited && status == MovementSearchStatus.Complete)
        {
            bound = MovementSearchBound.CooldownEvidence;
            status = MovementSearchStatus.Bounded;
        }
        if (context.ForcedMovement is { SampledHeadings: true } && status == MovementSearchStatus.Complete)
        {
            bound = MovementSearchBound.HeadingSamples;
            status = MovementSearchStatus.Bounded;
        }
        if (best == null)
            return Empty(status == MovementSearchStatus.Bounded ? status : MovementSearchStatus.NoVerifiedRoute, "no complete verified route found");
        return BuildPlan(best, status, bound);

        MovementPlan BuildPlan(Label best, MovementSearchStatus status, MovementSearchBound bound)
        {
            if (best.TargetChoices != 0 && status == MovementSearchStatus.Complete)
            {
                status = MovementSearchStatus.Bounded;
                bound = MovementSearchBound.TargetSetupEvidence;
            }
            var legs = new Stack<ImmutableArray<MovementStage>>();
            for (var label = best; label != null; label = label.Parent)
                legs.Push(label.Stages);
            var route = legs.SelectMany(s => s).ToImmutableArray();
            var nextStage = route.FirstOrDefault(s => s.Kind != MovementStageKind.Wait && s.Kind != MovementStageKind.Cast);
            var next = nextStage?.Kind is MovementStageKind.ForcedMovement or MovementStageKind.Knockback ? (WPos?)null : nextStage?.Transit?.Entrance ?? nextStage?.To;
            var result = new MovementPlan(context.Identity, status, route, best.Position, next, best.Slack, best.Time, best.Inventory.Charges,
                best.Actions, best.Values, frames[best.Knockbacks].NativePriority(best.Cell), expansions, clock.Elapsed.TotalMilliseconds,
                "unrelaxed native geometry; every segment and wait clears exit deadlines; final position requires no invented expiry")
            {
                TargetId = context.TargetId,
                FirstDepartureDeadline = best.FirstDeparture,
                FieldPlacement = context.FieldPlacement,
                FieldPlacementGain = PlacementValue(best),
                EndInventory = best.Inventory,
                EndPositionalBypassUntil = best.PositionalBypassUntil,
                EndTravel = best.Travel,
                OriginNativePriority = context.NativePriority(startCell),
                OriginPresentation = context.Presentation(startCell),
                DestinationPresentation = context.Presentation(best.Cell),
                PriorDestination = context.CommittedDestination,
                PartyContact = context.PartyContact,
                HealingCoverage = context.HealingCoverage,
                EndGcdReadyAt = best.GcdReadyAt,
                RangeAccess = RangeOutcome(best),
                ComboRecovery = context.ComboRecovery,
                ComboReadyAt = best.ComboReadyAt,
                ComboExecutionValue = ComboValue(best),
                ResourcePrunedActions = resourcePrunedActions,
                DowntimeFacing = context.DowntimeFacing,
                UsefulZones = context.Zones.Where(z => z.Contains(best.Position, best.Time, best.RelocatedField))
                    .Select(z => z with { Center = z.EffectiveCenter(best.RelocatedField) }).ToImmutableArray(),
                Bound = bound,
                DeliveryTimes = best.DeliveryTimes,
                RecoveryTimes = best.RecoveryTimes,
                ActionEvidence = context.Actions.Select((action, index) => (action, index))
                    .Where(item => (best.Actions & (1 << item.index)) != 0)
                    .Select(item => (item.index, action: ChosenAction(item.index, item.action)))
                    .Select(item => new PlannedActionEvidence(item.action.Id, item.action.TargetId,
                        item.action.Opportunity is { } opportunity
                            ? opportunity.Recipients.Where(r => r.Required).Select(r => r.Id).ToImmutableArray()
                            : item.action.RecipientIds.Where((_, index) => (item.action.RequiredRecipients & (1ul << index)) != 0).ToImmutableArray(), best.Values[item.index],
                        item.index, route.Where(s => s.SourceActionIndex == item.index && (s.Kind == MovementStageKind.Cast || NativeRotationalMobility.Contains(s.ActionId)))
                            .Select(s => s.Kind != MovementStageKind.Cast ? 0 : Math.Max(0, s.EndSeconds - s.StartSeconds - (float.IsFinite(s.ActionLockUntil) ? 0 : item.action.LockSeconds))).FirstOrDefault(float.NaN), item.action.Timing,
                        route.Where(s => s.SourceActionIndex == item.index && (s.Kind == MovementStageKind.Cast || NativeRotationalMobility.Contains(s.ActionId)))
                            .Select(s => s.PlannedRecastSeconds > 0 ? s.PlannedRecastSeconds : float.NaN).FirstOrDefault(float.NaN), item.action.Preparation))
                    .ToImmutableArray()
            };
            return NativeDeparturePreference.Apply(context, result);

            FrozenPlanningAction ChosenAction(int index, FrozenPlanningAction source)
            {
                var cast = route.FirstOrDefault(stage => (stage.Kind == MovementStageKind.Cast || NativeRotationalMobility.Contains(stage.ActionId))
                    && (stage.SourceActionIndex == index || stage.SourceActionIndex < 0
                        && stage.ActionId == source.Id && stage.AnchorId == source.TargetId));
                return cast == null ? source : context.ActionFor(cast) ?? source;
            }
        }

        MovementPlan Empty(MovementSearchStatus emptyStatus, string evidence)
            => new(context.Identity, emptyStatus, [], context.Origin, null, 0, 0, context.Resources, 0, [],
                float.MinValue, expansions, clock.Elapsed.TotalMilliseconds, evidence)
            { Bound = bound };

        void SeedEscape(Label landing, NativeNavigationSnapshot native)
        {
            var travel = landing.Travel ?? landing.Parent?.Travel ?? context.Travel;
            var witness = native.ContinuationWitness(landing.Position, landing.Time, cancellation, travel.At(landing.Time));
            if (witness.Length < 2) return;
            var stages = landing.Stages.ToBuilder();
            var time = landing.Time;
            var distance = landing.Distance;
            var slack = landing.Slack;
            // This is a proposal from the existing native search, never an
            // elective safety exemption. Recheck every leg on the ORIGINAL
            // strict map with its real elapsed time and travel-speed effects.
            for (var i = 1; i < witness.Length; ++i)
            {
                var length = (witness[i] - witness[i - 1]).Length();
                var duration = travel.Seconds(length, time);
                if (time + duration > MovementPlanningContext.MaximumHorizonSeconds
                    || !context.ExecutionRules.PermitsMovement(time, time + duration)
                    || !context.TraverseOrdinary(witness[i - 1], witness[i], time, duration, out var legSlack)) return;
                stages.Add(new(MovementStageKind.Walk, witness[i - 1], witness[i], time, time + duration));
                time += duration;
                distance += length;
                slack = Math.Min(slack, legSlack);
            }
            var cell = context.Cell(witness[^1]);
            if (cell < 0 || !context.DestinationAllowed(cell) || !context.CanRemain(witness[^1])
                || !context.DestinationSatisfied(witness[^1], time)) return;
            Add(landing with { Cell = cell, Position = witness[^1], Time = time,
                Distance = distance, Slack = slack, Stages = stages.ToImmutable() });
        }

        bool CanSpend(ulong cost)
        {
            if (canImprovePlacement || !pruneResourceInferiorMobility || context.Actions.Any(a => a.GrantedResources != 0)
                || context.Range?.RecoverExecution == true
                || context.ForcedMovement != null || best == null || best.SpentResources
                || frames[best.Knockbacks].NativePriority(best.Cell) != maximumNativePriority
                || context.Range?.Loss(best.Position) > 0 || context.PartyContact?.Loss(best.Position) > 0
                || context.HealingCoverage?.Satisfied(best.Position) == false
                || context.ComboRecovery != null && ComboValue(best) < maximumComboValue)
                return true;
            // The only execution ceiling proved here is an empty action sequence,
            // or one instantaneous opportunity delivered at its absolute earliest
            // time with every possible recipient. Future actions and conditional
            // cast-time gains deliberately disable this proof.
            if (!context.Actions.IsEmpty)
            {
                if (context.Actions.Length != 1) return true;
                var action = context.Actions[0];
                if (action.CastSeconds != 0 || action.Timing is { } timing && (timing.WithoutProc != 0 || timing.UnbuffedUpperBound != 0)
                    || (best.Actions & 1) == 0 || best.DeliveryTimes[0] > Math.Max(context.UnlockSeconds, action.Earliest)
                    || best.Values[0] < maximumActionValues[0])
                    return true;
                var recast = action.Timing == null ? 0 : Math.Min(action.Timing.NativeRecast,
                    action.Timing.ObservedFieldRecast ?? action.Timing.NativeRecast);
                if (best.RecoveryTimes[0] > Math.Max(context.UnlockSeconds, action.Earliest) + Math.Max(action.LockSeconds, recast))
                    return true;
            }
            // With no resource generation actions, an unspent incumbent has at
            // least the inventory of any spending route at the comparison time.
            // One charge that cannot return anywhere in the supported horizon
            // makes that advantage strict. Zones, continuity and presentation
            // rank below it. Apply this BEFORE enumerating all action landings.
            for (var resource = 0; resource < 16; ++resource)
            {
                if (MovementPlanningContext.ChargeCount(cost, resource) == 0) continue;
                var recharge = context.Recharges.FirstOrDefault(r => r.Resource == resource);
                if (recharge == null || recharge.Interval > MovementPlanningContext.MaximumHorizonSeconds
                    && (MovementPlanningContext.ChargeCount(context.Resources, resource) == recharge.Capacity
                        || recharge.NextIn > MovementPlanningContext.MaximumHorizonSeconds))
                {
                    ++resourcePrunedActions;
                    return false;
                }
            }
            return true;
        }

        bool CannotImproveExecution(Label label)
        {
            if (!pruneExecutionInferiorStates || best == null || context.Actions.Any(a => a.Preparation == PlanningPreparationMode.Optional)) return false;
            // This is an objective upper bound against a COMPLETE verified plan,
            // not same-cell dominance. No future transition can undo a delivered
            // action or deliver an undelivered one before this label's time.
            // A strictly worse first differing opportunity cannot be purchased
            // by later resources, goals or presentation anywhere in the map.
            for (var i = 0; i < context.Actions.Length; ++i)
            {
                var bit = 1 << i;
                if ((label.Actions & bit) == 0)
                    return (best.Actions & bit) != 0
                        && Math.Max(label.Time, context.Actions[i].Earliest) > best.DeliveryTimes[i];
                if ((best.Actions & bit) == 0) return false;
                if (label.DeliveryTimes[i] != best.DeliveryTimes[i])
                    return label.DeliveryTimes[i] > best.DeliveryTimes[i];
                if (label.Values[i] != best.Values[i]) return label.Values[i] < best.Values[i];
                if (!label.RecoveryTimes.IsDefault && label.RecoveryTimes[i] != best.RecoveryTimes[i])
                    return label.RecoveryTimes[i] > best.RecoveryTimes[i];
            }
            if (context.Range?.RecoverExecution == true && RangeOutcome(best) is { Loss: 0 } bestContact
                && frames[best.Knockbacks].NativePriority(best.Cell) == maximumNativePriority
                && (context.HealingCoverage == null || context.HealingCoverage.Satisfied(best.Position))
                && label.RangeAccess is { } pendingContact && pendingContact.Loss > 0 && label.Time > bestContact.At)
                return true;
            if (!context.StationaryOnly && canImprovePlacement && context.FieldPlacement is { } placement && PlacementValue(best) > 0
                && frames[best.Knockbacks].NativePriority(best.Cell) == maximumNativePriority
                && context.Range?.RecoverExecution != true
                && (context.PartyContact == null || context.PartyContact.Loss(best.Position) == 0)
                && (context.HealingCoverage == null || context.HealingCoverage.Satisfied(best.Position))
                && (context.ComboRecovery == null || ComboValue(best) >= maximumComboValue))
            {
                // All current executions tie and the incumbent has already
                // reached every higher-ranked native/range/contact ceiling.
                // A later placement cannot recover time already spent. A
                // placed label's exact gain remains an upper bound wherever it
                // walks; an unplaced one cannot begin before its current time.
                var latest = Math.Max(label.Time, placement.Earliest);
                var ceiling = label.FieldPlacementAt != null ? label.FieldPlacementGain
                    : latest > placement.Latest ? 0 : Math.Max(0,
                        MovementPlanningContext.MaximumHorizonSeconds - (latest + placement.LockSeconds)
                        - placement.Window(context, placement.OriginalPosition ?? context.Origin, placement.Earliest + placement.LockSeconds));
                if (ceiling < PlacementValue(best)) return true;
            }
            return false;
        }

        bool AtAbsoluteOptimum()
        {
            // A complete stationary witness can meet every objective ceiling.
            // No walking or spending alternative can improve zero distance,
            // earliest instant delivery/recovery, maximal native/presentation
            // values and an unspent inventory without resource generation.
            if (best == null || best.Distance != 0 || best.Position != context.Origin || best.SpentResources
                || best.FocusTarget != context.TargetId || !knockbacks.IsEmpty || context.ForcedMovement != null
                || context.HealingCoverage != null || context.ComboRecovery != null || !context.Zones.IsEmpty
                || context.FieldPlacement != null || context.PersonalField != null || context.RequiredDestination != null
                || context.PreparationMask != 0 || context.Actions.Length > 1
                || !context.PreservesPresent(best.Actions)
                || frames[best.Knockbacks].NativePriority(best.Cell) != maximumNativePriority
                || context.Presentation(best.Cell) != maximumPresentation
                || context.PartyContact?.Loss(best.Position) > 0
                || context.CommittedDestination is { } committed && !best.Position.AlmostEqual(committed, 0.1f)
                || context.Range is { } range && range.Loss(best.Position) != 0
                || context.Range?.RecoverExecution == true && RangeOutcome(best) is not { Loss: 0 }) return false;
            if (context.Range?.RecoverExecution == true && RangeOutcome(best) is { } access && access.At != access.ReadyAt) return false;
            if (context.Actions.IsEmpty) return best.Time == context.UnlockSeconds;
            var action = context.Actions[0];
            if (action.Preparation != PlanningPreparationMode.None || action.CastSeconds != 0 || action.ResourceCost != 0
                || action.GrantedResources != 0 || action.Timing is { WithoutProc: > 0 } or { UnbuffedUpperBound: > 0 } or { HasteCenter: not null }
                || best.Values[0] != maximumActionValues[0]) return false;
            var earliest = Math.Max(context.UnlockSeconds, action.Earliest);
            var recast = action.Timing?.NativeRecast ?? 0;
            return best.DeliveryTimes[0] == earliest && best.Time == earliest
                && best.RecoveryTimes[0] == earliest + Math.Max(action.LockSeconds, recast);
        }

        MovementSearchBound ReachedBound()
        {
            if (memoryExhausted)
                return MovementSearchBound.StateMemory;
            if (expansions >= limits.Expansions)
                return MovementSearchBound.ExpansionLimit;
            if (clock.Elapsed.TotalMilliseconds >= limits.Milliseconds)
                return MovementSearchBound.ElapsedLimit;
            if (limits.RespectFreshness)
            {
                var age = Stopwatch.GetElapsedTime(context.Identity.CapturedTimestamp).TotalSeconds;
                if (age >= 0.25 || age < 0)
                    return MovementSearchBound.InputFreshness;
                var departure = nativeIncumbent is { HasVerifiedRoute: true } incumbent
                    ? Math.Min(initialSlack, incumbent.DepartureDeadlineSeconds) : initialSlack;
                if (age >= departure)
                    return MovementSearchBound.RequiredDeparture;
            }
            return MovementSearchBound.None;
        }

        void Add(Label label)
        {
            var ownImmunity = label.ImmunityActionId != 0;
            var ownPlacement = label.FieldPlacementAt != null;
            if (label.Knockbacks < 0) label.Knockbacks = label.Parent?.Knockbacks ?? 0;
            if (label.Knockbacks < knockbacks.Length && label.Time >
                (knockbacks[label.Knockbacks].Timing!.EarliestUtc - context.Identity.WorldTime).TotalSeconds) return;
            if (label.RecoveryTimes.IsDefault)
                label.RecoveryTimes = label.Parent?.RecoveryTimes
                    ?? ImmutableArray.CreateRange(Enumerable.Repeat(float.PositiveInfinity, context.Actions.Length));
            if (CannotImproveExecution(label)) return;
            label.Inventory = label.Inventory.At(label.Time, context.Recharges);
            if (context.Range is { RecoverExecution: true } range)
            {
                var access = label.Parent?.RangeAccess ?? new PlanningRangeAccess(range.RecoveryReadyIn);
                var ready = access.ReadyAt;
                foreach (var stage in label.Stages)
                {
                    if (stage.Kind == MovementStageKind.UtilityAction && float.IsFinite(stage.ActionLockUntil)
                        && context.Utilities.Any(u => u.ActionId == stage.ActionId && u.Effect == PlanningUtilityEffect.MovementSpeed))
                        ready = Math.Max(ready, stage.ActionLockUntil);
                    if (stage.Kind != MovementStageKind.Target && context.ActionFor(stage) is { IsGCD: true, Preparation: not PlanningPreparationMode.Optional })
                        ready = Math.Max(ready, Math.Max(stage.StartSeconds + (float.IsFinite(stage.PlannedRecastSeconds) ? stage.PlannedRecastSeconds : 0),
                            float.IsFinite(stage.ActionLockUntil) ? stage.ActionLockUntil : stage.EndSeconds));
                }
                label.RangeAccess = access.Advance(range, ready, label.Stages, label.Position, label.Time);
            }
            var comboReady = Math.Max(label.ComboReadyAt, label.Parent?.ComboReadyAt ?? 0);
            var fieldEntry = label.Parent?.FieldEntryByAbility ?? false;
            var fieldBenefitAllowed = label.Parent?.FieldBenefitAllowed ?? true;
            var relocatedField = label.Parent?.RelocatedField;
            foreach (var stage in label.Stages)
            {
                fieldBenefitAllowed = PlanningCastTiming.BenefitAfter(fieldBenefitAllowed, stage, context.Actions);
                if (stage.Kind == MovementStageKind.UtilityAction && stage.ActionId == 36988 && stage.Field != null)
                    relocatedField = stage.Field with { Center = stage.To };
                if (restrictFieldEntry) fieldEntry = PlanningCastTiming.EntryAfter(fieldEntry, stage, context.Actions, relocatedField);
            }
            if (context.ComboRecovery is { AfterActionId: not 0 } combo)
                foreach (var stage in label.Stages)
                    if (stage.Kind == MovementStageKind.Cast && stage.ActionId == combo.AfterActionId && stage.AnchorId == combo.TargetId)
                        comboReady = Math.Max(comboReady, Math.Max(stage.StartSeconds + combo.RecastSeconds,
                            float.IsFinite(stage.ActionLockUntil) ? stage.ActionLockUntil : stage.EndSeconds));
            // Effects are absolute times in this capture. Preserve them across
            // walking, waiting and other actions; do not restart their duration.
            label.FirstDeparture = label.FirstDeparture ?? label.Parent?.FirstDeparture;
            label.ActionReadyAt = Math.Max(label.ActionReadyAt, label.Parent?.ActionReadyAt ?? 0);
            label.GcdReadyAt = Math.Max(label.GcdReadyAt, label.Parent?.GcdReadyAt ?? 0);
            label.ImmunityAppliedBy = ownImmunity ? label.ImmunityAppliedBy : label.Parent?.ImmunityAppliedBy ?? float.MaxValue;
            label.ImmunityUntil = ownImmunity ? label.ImmunityUntil : label.Parent?.ImmunityUntil ?? 0;
            label.ImmunityActionId = label.ImmunityActionId != 0 ? label.ImmunityActionId : label.Parent?.ImmunityActionId ?? 0;
            label.ImmunityPossibleFrom = ownImmunity ? label.ImmunityPossibleFrom : label.Parent?.ImmunityPossibleFrom ?? float.MaxValue;
            label.ImmunityPossibleUntil = ownImmunity ? label.ImmunityPossibleUntil : label.Parent?.ImmunityPossibleUntil ?? 0;
            label.PositionalBypassUntil = Math.Max(label.PositionalBypassUntil, label.Parent?.PositionalBypassUntil ?? 0);
            label.Travel = label.Travel ?? label.Parent?.Travel ?? context.Travel;
            label.FieldPlacementAt = label.FieldPlacementAt ?? label.Parent?.FieldPlacementAt;
            label.FieldPlacementGain = ownPlacement ? label.FieldPlacementGain : label.Parent?.FieldPlacementGain ?? 0;
            label.FocusTarget = label.FocusTarget != 0 ? label.FocusTarget : label.Parent?.FocusTarget ?? context.TargetId;
            label.TargetChoices = (byte)(label.TargetChoices | (label.Parent?.TargetChoices ?? 0));
            label.SpentResources = label.SpentResources || label.Parent?.SpentResources == true || label.Stages.Any(s =>
                    s.Kind is MovementStageKind.TargetDash or MovementStageKind.GroundMovement or MovementStageKind.DirectionalMovement
                        or MovementStageKind.Return or MovementStageKind.UtilityAction);
            var phases = label.Parent?.Phases ?? 0;
            var lastKind = label.Parent?.Stages.LastOrDefault()?.Kind;
            foreach (var stage in label.Stages)
            {
                if (stage.Kind is not (MovementStageKind.Walk or MovementStageKind.Wait) || lastKind != stage.Kind)
                    ++phases;
                lastKind = stage.Kind;
            }
            label.Phases = phases;
            label.ComboReadyAt = comboReady;
            label.FieldEntryByAbility = fieldEntry;
            label.RelocatedField = relocatedField;
            label.FieldBenefitAllowed = fieldBenefitAllowed;
            var key = partitionFrontier
                ? new FrontierKey(label.Cell, label.Actions, label.Knockbacks, label.Stages.LastOrDefault()?.Kind, label.FocusTarget, label.TargetChoices)
                : new FrontierKey(label.Cell, 0, 0, null, 0, 0);
            if (!labels.TryGetValue(key, out var existing))
                labels[key] = existing = [];
            foreach (var other in existing)
                if (Dominates(other, label))
                    return;
            // A dominated over-limit loop adds no alternative. A genuinely new
            // sequence does: retain the best verified result but report the limit.
            if (phases > MovementPlanningContext.MaximumPhases)
            {
                sequenceLimited = true;
                return;
            }
            // A full memory allowance is a bound only if it actually prevents a relevant
            // alternative from being retained. Do not report an exhausted search as complete.
            if (createdStates >= limits.CreatedStates)
            {
                memoryExhausted = true;
                return;
            }
            for (var i = existing.Count - 1; i >= 0; --i)
                if (Dominates(label, existing[i]))
                {
                    existing[i].Retired = true;
                    existing.RemoveAt(i);
                }
            existing.Add(label);
            ++createdStates;
            queue.Enqueue(label, (label.Time, serial++));
            // Keep a complete incumbent even when the budget expires before this label is expanded.
            if (label.Knockbacks == knockbacks.Length && (label.TargetChoices & ~label.Actions) == 0
                && context.CompletePreparation(label.Actions)
                && context.DestinationAllowed(label.Cell) && frames[label.Knockbacks].CanRemain(label.Position) && context.DestinationSatisfied(label.Position, label.Time)
                && (best == null || Better(label, best)))
            {
                best = label;
                // An immutable, complete route is usable before search exhaustion. This callback
                // is a local mailbox write, never a callback into a live actor or framework service.
                publishBest?.Invoke(BuildPlan(best, MovementSearchStatus.Searching, MovementSearchBound.None));
            }
            if (knockbacks.IsEmpty && context.ForcedMovement == null && recoveryWitness.Length > 1 && label.Position == context.Origin && context.Range is { RecoverExecution: true } rangeWindow
                && rangeWindow.Loss(label.Position) == 0 && context.PreservesPresent(label.Actions) && context.CompletePreparation(label.Actions)
                && label.RangeAccess is { } window && window.ReadyAt > label.Time && label.Knockbacks == knockbacks.Length
                && NativeMovementIncumbent.CaptureRoute(context, recoveryWitness, window.ReadyAt) is { HasVerifiedRoute: true } delayed)
            {
                var continuation = ImmutableArray.CreateBuilder<MovementStage>();
                continuation.Add(new(MovementStageKind.Wait, label.Position, label.Position, label.Time, window.ReadyAt));
                continuation.AddRange(delayed.Stages.Where(s => s.Kind == MovementStageKind.Walk));
                Add(new(context.Cell(delayed.UsefulDestination), delayed.UsefulDestination, delayed.DurationSeconds,
                    label.Inventory, label.Actions, label.Values, label.DeliveryTimes,
                    label.Distance + delayed.Stages.Where(s => s.Kind == MovementStageKind.Walk).Sum(s => (s.To - s.From).Length()),
                    Math.Min(label.Slack, delayed.PlanSlackSeconds), label, continuation.ToImmutable()));
            }
        }

        static bool NoLater(ImmutableArray<float> a, ImmutableArray<float> b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; ++i)
                if (!(a[i] <= b[i])) return false;
            return true;
        }

        bool Dominates(Label a, Label b)
            => a.Knockbacks == b.Knockbacks && a.Position == b.Position && a.Actions == b.Actions && a.Values.AsSpan().SequenceEqual(b.Values.AsSpan())
                && a.FieldPlacementAt == b.FieldPlacementAt && a.FieldPlacementGain >= b.FieldPlacementGain
                && a.FocusTarget == b.FocusTarget && a.TargetChoices == b.TargetChoices
                && NoLater(a.DeliveryTimes, b.DeliveryTimes)
                && NoLater(a.RecoveryTimes, b.RecoveryTimes)
                && a.PositionalBypassUntil >= b.PositionalBypassUntil && a.Phases <= b.Phases && a.Stages.LastOrDefault()?.Kind == b.Stages.LastOrDefault()?.Kind
                && a.ComboReadyAt <= b.ComboReadyAt
                && a.ActionReadyAt <= b.ActionReadyAt
                && a.GcdReadyAt <= b.GcdReadyAt
                && (a.RangeAccess == null && b.RangeAccess == null || a.RangeAccess is { } access && b.RangeAccess is { } otherAccess && access.Dominates(otherAccess))
                // Longer immunity may block a useful later push. Neither lifetime
                // dominates a different one merely because it prevents more motion.
                && a.ImmunityAppliedBy == b.ImmunityAppliedBy && a.ImmunityUntil == b.ImmunityUntil
                && a.ImmunityPossibleFrom == b.ImmunityPossibleFrom && a.ImmunityPossibleUntil == b.ImmunityPossibleUntil
                && a.ImmunityActionId == b.ImmunityActionId
                && a.FieldBenefitAllowed == b.FieldBenefitAllowed
                && a.RelocatedField == b.RelocatedField
                && (a.FieldEntryByAbility || !b.FieldEntryByAbility)
                && a.Travel!.Dominates(b.Travel!, b.Time)
                && a.Time <= b.Time && a.Distance <= b.Distance && a.Slack >= b.Slack
                && (a.FirstDeparture ?? float.MaxValue) >= (b.FirstDeparture ?? float.MaxValue)
                && context.ExecutionRules.SameTemporalPhase(a.Time, b.Time)
                && a.Inventory.Dominates(b.Inventory, b.Time, context.Recharges)
                && frames[a.Knockbacks].Traverse(a.Position, a.Position, a.Time, b.Time - a.Time, out _);

        float ComboValue(Label label) => context.ComboRecovery?.Value(label.Position, label.Time, label.ComboReadyAt, context.ExecutionRules) ?? 0;

        float PlacementValue(Label label) => label.FieldPlacementAt is { } center
            && label.Position.InCircle(center, PlanningFieldPlacement.Radius) ? label.FieldPlacementGain : 0;

        float InitialDeparture(Label current, float earliest)
        {
            if (current.FirstDeparture is { } existing) return existing;
            var first = float.MaxValue;
            for (var label = current; label != null; label = label.Parent)
                foreach (var stage in label.Stages)
                    if (stage.From != stage.To) first = Math.Min(first, stage.StartSeconds);
            return first == float.MaxValue ? earliest : first + Math.Min(current.Slack, earliest - current.Time);
        }

        PlanningRangeAccess? RangeOutcome(Label label)
            => context.Range is { } range && label.RangeAccess is { } access
                ? access.Observe(range, label.Position, Math.Max(label.Time, access.ReadyAt)) : null;

        int CompareActions(Label a, Label b, bool optional)
        {
            for (var i = 0; i < a.Values.Length; ++i)
            {
                if ((context.Actions[i].Preparation == PlanningPreparationMode.Optional) != optional) continue;
                // A delivered action already preserves every evidenced required
                // recipient. Extra coverage cannot buy a delayed current action.
                var delivered = ((a.Actions >> i) & 1).CompareTo((b.Actions >> i) & 1);
                if (delivered != 0)
                    return delivered;
                var delivery = a.DeliveryTimes[i].CompareTo(b.DeliveryTimes[i]);
                if (delivery != 0)
                    return -delivery;
                var execution = a.Values[i].CompareTo(b.Values[i]);
                if (execution != 0)
                    return execution;
                var recovery = a.RecoveryTimes[i].CompareTo(b.RecoveryTimes[i]);
                if (recovery != 0)
                    return -recovery;
            }
            return 0;
        }

        bool Better(Label a, Label b)
        {
            var present = CompareActions(a, b, optional: false);
            if (present != 0) return present > 0;
            // Native goals keep their original relative ranking. Their aggregate
            // score is not a safety/assignment proof and cannot purchase a lost
            // current attack, heal or recipient. With native rotations this is
            // still the first comparison: there is no invented action prediction.
            var native = frames[a.Knockbacks].NativePriority(a.Cell).CompareTo(frames[b.Knockbacks].NativePriority(b.Cell));
            if (native != 0)
                return native > 0;
            if (context.HealingCoverage is { } healing)
            {
                var coverage = healing.Compare(a.Position, b.Position);
                if (coverage != 0) return coverage > 0;
            }
            if (context.Range is { } range)
            {
                if (a.RangeAccess is { } first && b.RangeAccess is { } second)
                {
                    // A complete final position can safely hold through its known
                    // readiness. Temporary positions must retain their whole escape.
                    first = RangeOutcome(a)!.Value;
                    second = RangeOutcome(b)!.Value;
                    var reached = (first.Loss == 0).CompareTo(second.Loss == 0);
                    if (reached != 0) return reached > 0;
                    // Partial proximity at readiness delivers no attack. It
                    // cannot buy a dash back out of a required escape, even if
                    // that detour later returns to cover before activation.
                    if (first.Loss == 0 && first.At != second.At) return first.At < second.At;
                }
                var rangeLoss = range.Loss(a.Position).CompareTo(range.Loss(b.Position));
                if (rangeLoss != 0 && (!range.RecoverExecution || range.Loss(a.Position) == 0 || range.Loss(b.Position) == 0))
                    return rangeLoss < 0;
                // A ranged filler is still an attack, but does not restore melee
                // execution. Compare complete recovery timing before saving a
                // charge; retain present action and native goals above this.
                if (range.NeedsRecovery(context.Origin) && range.Loss(a.Position) == 0)
                {
                    // The native GCD clock is an observed opportunity window,
                    // not a prediction of another attack. Arrivals before that
                    // window tie, allowing walking to preserve a useful charge.
                    var aReady = Math.Max(a.Time, range.RecoveryReadyIn);
                    var bReady = Math.Max(b.Time, range.RecoveryReadyIn);
                    if (aReady != bReady) return aReady < bReady;
                }
            }
            // A proposed setup may improve evidenced subsequent execution only
            // after present execution, native goals and actual melee recovery.
            var subsequent = CompareActions(a, b, optional: true);
            if (subsequent != 0) return subsequent > 0;
            var compareAt = Math.Max(a.Time, b.Time);
            if (context.ComboRecovery != null)
            {
                var useful = ComboValue(a).CompareTo(ComboValue(b));
                if (useful != 0) return useful > 0;
            }
            if (context.PartyContact is { } contact)
            {
                var contactLoss = contact.Loss(a.Position).CompareTo(contact.Loss(b.Position));
                if (contactLoss != 0) return contactLoss < 0;
            }
            var placementGain = PlacementValue(a).CompareTo(PlacementValue(b));
            if (placementGain != 0) return placementGain > 0;
            if (PlacementValue(a) == 0 && (a.FieldPlacementAt == null) != (b.FieldPlacementAt == null)) return a.FieldPlacementAt == null;
            var aInventory = a.Inventory.At(compareAt, context.Recharges);
            var bInventory = b.Inventory.At(compareAt, context.Recharges);
            var aResources = MovementPlanningContext.NoFewerResources(aInventory.Charges, bInventory.Charges);
            var bResources = MovementPlanningContext.NoFewerResources(bInventory.Charges, aInventory.Charges);
            if (aResources != bResources)
                return aResources;
            if (aInventory.Charges == bInventory.Charges)
            {
                var aEarlier = NoLater(aInventory.Next, bInventory.Next);
                var bEarlier = NoLater(bInventory.Next, aInventory.Next);
                if (aEarlier != bEarlier) return aEarlier;
            }
            // Still prefer useful progress when neither route restores attack
            // access, but only among resource-equivalent complete escapes.
            if (context.Range is { RecoverExecution: true } partialRange)
            {
                var loss = partialRange.Loss(a.Position).CompareTo(partialRange.Loss(b.Position));
                if (loss != 0) return loss < 0;
            }
            // Active friendly effects are distinct from cosmetic spacing. Keep
            // their categories separate, credit only arrival before expiry, and
            // never spend a charge or lose a current recipient to chase a zone.
            for (var benefit = PlanningZoneBenefit.PersonalCasting; benefit <= PlanningZoneBenefit.Healing; ++benefit)
            {
                var aCovered = context.Zones.Any(z => z.Benefit == benefit && z.Contains(a.Position, a.Time, a.RelocatedField));
                var bCovered = context.Zones.Any(z => z.Benefit == benefit && z.Contains(b.Position, b.Time, b.RelocatedField));
                if (aCovered != bCovered) return aCovered;
            }
            if (context.CommittedDestination is { } committed)
            {
                var aCommitted = a.Position.AlmostEqual(committed, 0.1f);
                var bCommitted = b.Position.AlmostEqual(committed, 0.1f);
                if (aCommitted != bCommitted)
                    return aCommitted;
            }
            if (context.HasTargetChoices && a.FocusTarget != b.FocusTarget)
            {
                if (a.FocusTarget == context.TargetId) return true;
                if (b.FocusTarget == context.TargetId) return false;
            }
            var presentation = context.Presentation(a.Cell).CompareTo(context.Presentation(b.Cell));
            if (presentation != 0)
                return presentation > 0;
            return a.Distance < b.Distance || a.Distance == b.Distance && a.Time < b.Time;
        }
    }
}
