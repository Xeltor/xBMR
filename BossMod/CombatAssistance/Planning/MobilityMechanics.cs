namespace BossMod.CombatAssistance.Planning;

internal enum MobilityMechanic { Target, FixedTowardTarget, Directional, TargetBackstep, Ground, FixedTowardGround, Return }
internal sealed record MobilityAnchor(ulong Id, WPos Position, float Radius, ExecutionOpportunity? Effect = null, float MinimumTravel = 0)
{
    internal bool SameAnchor(MobilityAnchor other, bool forRevalidation = false) => Id == other.Id && (forRevalidation || Position == other.Position) && Radius == other.Radius && MinimumTravel == other.MinimumTravel
        && (Effect == null ? other.Effect == null : other.Effect != null
            && (forRevalidation ? Effect.SameBinding(other.Effect) : Effect.SameGeometry(other.Effect)));
}

// One available mechanical action, captured on the framework thread. Legality,
// cooldowns and target eligibility are native observations. Geometry is evaluated
// at each search state, so walking to a usable anchor is part of the same search.
internal sealed class MobilityMechanics
{
    internal uint ActionId { get; }
    internal MobilityMechanic Mechanic { get; }
    internal float Range { get; }
    internal float Displacement { get; }
    internal float PlayerRadius { get; }
    internal float SetupSeconds { get; }
    internal float TravelSeconds { get; }
    internal int Resource { get; }
    internal int Charges { get; }
    internal float ReadyIn { get; }
    internal float MinimumTravel { get; }
    internal ulong AdditionalResourceCost { get; }
    internal ulong ResourceCost => ((ulong)Charges << (4 * Resource)) | AdditionalResourceCost;
    internal ImmutableArray<MobilityAnchor> Anchors { get; }
    internal WPos? ReturnPosition { get; }
    internal bool AllowStationary { get; }
    internal bool IsGCD { get; }
    internal float RecastSeconds { get; }
    internal byte RequiredEarlierActions { get; }
    internal float AvailableUntil { get; }

    internal MobilityMechanics(uint actionId, MobilityMechanic mechanic, float range, float displacement,
        float playerRadius, float setupSeconds, float travelSeconds, int resource, IEnumerable<MobilityAnchor>? anchors = null,
        WPos? returnPosition = null, int charges = 1, float readyIn = 0, float minimumTravel = 0, ulong additionalResourceCost = 0, bool allowStationary = false,
        bool isGCD = false, float recastSeconds = 0, byte requiredEarlierActions = 0, float availableUntil = float.MaxValue)
    {
        ActionId = actionId;
        Mechanic = mechanic;
        Range = range;
        Displacement = displacement;
        PlayerRadius = playerRadius;
        SetupSeconds = setupSeconds;
        TravelSeconds = travelSeconds;
        Resource = resource;
        Charges = charges;
        ReadyIn = readyIn;
        MinimumTravel = minimumTravel;
        AdditionalResourceCost = additionalResourceCost;
        Anchors = anchors?.ToImmutableArray() ?? [];
        ReturnPosition = returnPosition;
        AllowStationary = allowStationary;
        IsGCD = isGCD; RecastSeconds = recastSeconds; RequiredEarlierActions = requiredEarlierActions;
        AvailableUntil = availableUntil;
        if (actionId == 0 || !Enum.IsDefined(mechanic) || !float.IsFinite(range) || range < 0
            || !float.IsFinite(displacement) || !float.IsFinite(playerRadius) || playerRadius < 0
            || !float.IsFinite(setupSeconds) || setupSeconds < 0 || !float.IsFinite(travelSeconds) || travelSeconds <= 0
            || !float.IsFinite(readyIn) || readyIn < 0 || !float.IsFinite(minimumTravel) || minimumTravel < 0
            || !float.IsFinite(recastSeconds) || (isGCD ? recastSeconds <= 0 : recastSeconds != 0)
            || !float.IsFinite(availableUntil) || availableUntil < 0
            || resource is < 0 or > 15 || charges is < 1 or > 15
            || (additionalResourceCost & (15ul << (4 * resource))) != 0
            || mechanic is MobilityMechanic.Directional or MobilityMechanic.TargetBackstep or MobilityMechanic.FixedTowardGround or MobilityMechanic.FixedTowardTarget && displacement == 0
            || Anchors.Any(a => a.Id == 0 || !Finite(a.Position) || !float.IsFinite(a.Radius) || a.Radius < 0
                || !float.IsFinite(a.MinimumTravel) || a.MinimumTravel < 0)
            || returnPosition is { } point && !Finite(point))
            throw new ArgumentException("Incomplete mobility mechanics.");
    }

    internal bool SameMechanics(MobilityMechanics other, float age = 0, bool forRevalidation = false)
        => ActionId == other.ActionId && Mechanic == other.Mechanic && Range == other.Range
            && Displacement == other.Displacement && PlayerRadius == other.PlayerRadius && SetupSeconds == other.SetupSeconds
            && (forRevalidation || TravelSeconds == other.TravelSeconds) && Resource == other.Resource && Charges == other.Charges
            && Math.Abs(ReadyIn - Math.Max(0, other.ReadyIn - age)) <= 0.01f && MinimumTravel == other.MinimumTravel
            && AdditionalResourceCost == other.AdditionalResourceCost
            && AllowStationary == other.AllowStationary && ReturnPosition == other.ReturnPosition && Anchors.Length == other.Anchors.Length
            && IsGCD == other.IsGCD && RecastSeconds == other.RecastSeconds && RequiredEarlierActions == other.RequiredEarlierActions
            && (AvailableUntil == float.MaxValue ? other.AvailableUntil == float.MaxValue
                : Math.Abs(AvailableUntil - Math.Max(0, other.AvailableUntil - age)) <= 0.01f)
            && Anchors.Select((a, i) => a.SameAnchor(other.Anchors[i], forRevalidation)).All(same => same);

    internal IEnumerable<PlanningMobility> From(MovementPlanningContext context, WPos origin, WPos? returnOverride = null)
    {
        var source = context.Cell(origin);
        if (source < 0)
            yield break;
        if (Mechanic is MobilityMechanic.Target or MobilityMechanic.FixedTowardTarget or MobilityMechanic.TargetBackstep)
        {
            foreach (var anchor in Anchors)
            {
                if (anchor.Effect != null && !anchor.Effect.Evaluate(origin).Allowed)
                    continue;
                var delta = anchor.Position - origin;
                var distance = delta.Length();
                if (distance < 0.001f || distance > Range + PlayerRadius + anchor.Radius)
                {
                    if (distance < 0.001f && AllowStationary && Create(context, origin, origin, anchor.Id, null, MovementStageKind.TargetDash, anchor.MinimumTravel) is { } stationary)
                        yield return stationary;
                    continue;
                }
                var direction = delta / distance;
                var travel = Mechanic switch
                {
                    MobilityMechanic.Target => Math.Max(0, distance - PlayerRadius - anchor.Radius),
                    MobilityMechanic.TargetBackstep => -Math.Abs(Displacement),
                    _ => Displacement
                };
                if (Create(context, origin, origin + direction * travel, anchor.Id, Angle.FromDirection(direction),
                    Mechanic == MobilityMechanic.TargetBackstep ? MovementStageKind.DirectionalMovement : MovementStageKind.TargetDash, anchor.MinimumTravel) is { } edge)
                    yield return edge;
            }
            yield break;
        }
        if (Mechanic == MobilityMechanic.Return)
        {
            if ((returnOverride ?? ReturnPosition) is { } position && (position - origin).Length() <= Range
                && Make(position, 0, null, MovementStageKind.Return) is { } edge)
                yield return edge;
            yield break;
        }
        if (context.Range is { RecoverExecution: true } recovery
            && Toward(context, origin, recovery.Center) is { } aimed)
            yield return aimed;
        // Every native map cell is a supported ground choice or a direction to aim
        // at. No cluster around the old endpoint and no fixed 16/24-direction ring.
        for (var cell = 0; cell < context.Cells; ++cell)
        {
            var candidate = context.Point(cell);
            var delta = candidate - origin;
            var distance = delta.Length();
            if (distance < 0.001f)
                continue;
            WPos landing;
            Angle? facing = null;
            if (Mechanic == MobilityMechanic.Directional)
            {
                landing = origin + delta / distance * Math.Abs(Displacement);
                facing = Angle.FromDirection(delta * Math.Sign(Displacement));
            }
            else
            {
                if (distance > Range)
                    continue;
                landing = Mechanic == MobilityMechanic.FixedTowardGround ? origin + delta / distance * Displacement : candidate;
            }
            if (Make(landing, 0, facing, Mechanic == MobilityMechanic.Directional
                ? MovementStageKind.DirectionalMovement : MovementStageKind.GroundMovement) is { } edge)
                yield return edge;
        }

        PlanningMobility? Make(WPos landing, ulong anchor, Angle? facing, MovementStageKind kind)
            => Create(context, origin, landing, anchor, facing, kind);
    }

    // Final execution resolves just the committed control. It must never enumerate
    // the full native map on the framework thread merely to rediscover this edge.
    internal PlanningMobility? Resolve(MovementPlanningContext context, WPos origin, MovementStage stage, WPos? returnOverride = null,
        bool exactBinding = true)
    {
        if (stage.ActionId != ActionId)
            return null;
        WPos landing;
        Angle? facing = null;
        MovementStageKind kind;
        float minimumTravel = 0;
        if (Mechanic is MobilityMechanic.Target or MobilityMechanic.FixedTowardTarget or MobilityMechanic.TargetBackstep)
        {
            var anchor = Anchors.FirstOrDefault(a => a.Id == stage.AnchorId);
            if (anchor == null || anchor.Effect != null && !anchor.Effect.Evaluate(origin).Allowed)
                return null;
            minimumTravel = anchor.MinimumTravel;
            var delta = anchor.Position - origin;
            var distance = delta.Length();
            if (distance > Range + PlayerRadius + anchor.Radius || distance < 0.001f && !AllowStationary) return null;
            facing = distance < 0.001f ? null : Angle.FromDirection(delta / distance);
            landing = distance < 0.001f ? origin : origin + delta / distance * (Mechanic == MobilityMechanic.Target ? Math.Max(0, distance - PlayerRadius - anchor.Radius)
                : Mechanic == MobilityMechanic.TargetBackstep ? -Math.Abs(Displacement) : Displacement);
            kind = Mechanic == MobilityMechanic.TargetBackstep ? MovementStageKind.DirectionalMovement : MovementStageKind.TargetDash;
        }
        else if (stage.AnchorId != 0)
            return null;
        else if (Mechanic == MobilityMechanic.Return)
        {
            if ((returnOverride ?? ReturnPosition) is not { } saved || (saved - origin).Length() > Range)
                return null;
            landing = saved;
            kind = MovementStageKind.Return;
        }
        else if (Mechanic == MobilityMechanic.Directional)
        {
            if (stage.Facing is not { } direction)
                return null;
            facing = direction;
            landing = origin + direction.ToDirection() * Displacement;
            kind = MovementStageKind.DirectionalMovement;
        }
        else
        {
            var delta = stage.To - origin;
            var distance = delta.Length();
            if (distance < 0.001f || distance > Range)
                return null;
            landing = Mechanic == MobilityMechanic.FixedTowardGround ? origin + delta / distance * Displacement : stage.To;
            kind = MovementStageKind.GroundMovement;
        }
        return kind == stage.Kind && (!exactBinding || landing.AlmostEqual(stage.To, 0.01f) && facing == stage.Facing)
            ? Create(context, origin, landing, stage.AnchorId, facing, kind, minimumTravel) : null;
    }

    // The first opportunity pass must include a deliberate reverse jump or
    // ground step toward combat reach, before the full map/facing enumeration.
    // This is an additional candidate, never a restriction on the full search.
    internal PlanningMobility? Toward(MovementPlanningContext context, WPos origin, WPos target)
    {
        if (Mechanic is not (MobilityMechanic.Directional or MobilityMechanic.Ground or MobilityMechanic.FixedTowardGround)) return null;
        var delta = target - origin;
        var length = delta.Length();
        if (length < .001f) return null;
        var landing = origin + delta / length * (Mechanic == MobilityMechanic.Ground
            ? Math.Min(length, Range) : Math.Abs(Displacement));
        return Create(context, origin, landing, 0,
            Mechanic == MobilityMechanic.Directional ? Angle.FromDirection(delta * Math.Sign(Displacement)) : null,
            Mechanic == MobilityMechanic.Directional ? MovementStageKind.DirectionalMovement : MovementStageKind.GroundMovement);
    }

    private PlanningMobility? Create(MovementPlanningContext context, WPos origin, WPos landing, ulong anchor, Angle? facing, MovementStageKind kind, float minimumTravel = 0)
    {
        var source = context.Cell(origin);
        var cell = context.Cell(landing);
        // XCAI's friendly escape minimum yielded to a useful uptime relay.
        // Admit that candidate here; the joint search still has to prove its
        // complete route, execution window and advantage over walking.
        var executionRelay = minimumTravel > 0 && (
            context.Range is { RecoverExecution: true } range && range.Loss(landing) < range.Loss(origin)
            || context.HealingCoverage?.Compare(landing, origin) > 0
            || context.Actions.Any(a => a.Preparation == PlanningPreparationMode.None
                && !context.Evaluate(a, origin).Allowed && context.Evaluate(a, landing).Allowed));
        var requiredTravel = Math.Max(MinimumTravel, executionRelay ? 0 : minimumTravel);
        return source >= 0 && cell >= 0 && (AllowStationary || !landing.AlmostEqual(origin, 0.001f)) && (landing - origin).Length() >= requiredTravel
            ? new(source, cell, kind, ActionId, anchor, facing, SetupSeconds, TravelSeconds, 0, Resource, Charges, origin, landing, ReadyIn, AdditionalResourceCost, IsGCD, RecastSeconds, RequiredEarlierActions, AvailableUntil) : null;
    }

    private static bool Finite(WPos point) => float.IsFinite(point.X) && float.IsFinite(point.Z);
}
