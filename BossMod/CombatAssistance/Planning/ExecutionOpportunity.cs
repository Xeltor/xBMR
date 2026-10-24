namespace BossMod.CombatAssistance.Planning;

internal enum ExecutionShape { SingleTarget, CasterCircle, TargetCircle, GroundCircle, Cone, Rectangle, Self }
internal sealed record ExecutionRecipient(ulong Id, WPos Position, float Radius, float Value, bool Required = false, float HeightOffset = 0);
internal sealed record TargetFacingRequirement(Angle Direction, Angle Tolerance);
internal readonly record struct ExecutionCoverage(bool Allowed, float Value, Angle? Facing = null);

// Combat geometry is independent of encounter safety. Inputs are captured values; this
// evaluator never reads an Actor, Dalamud wrapper, reflected action, or live callback.
// The selected target/ground point stays fixed. A larger total cannot buy a lost recipient.
internal sealed class ExecutionOpportunity
{
    internal ExecutionShape Shape { get; }
    internal ulong TargetId { get; }
    internal WPos Anchor { get; }
    internal float CastReach { get; }
    internal float EffectRadius { get; }
    internal float HalfWidth { get; }
    internal Angle HalfAngle { get; }
    internal Angle TargetFacing { get; }
    internal Positional Positional { get; }
    internal TargetFacingRequirement? TankFacing { get; }
    internal ImmutableArray<ExecutionRecipient> Recipients { get; }
    internal ImmutableArray<ExecutionRecipient> ForbiddenRecipients { get; }
    internal float MaximumValue => Recipients.Aggregate(0f, (sum, r) => (Shape is ExecutionShape.SingleTarget or ExecutionShape.Self)
        && r.Id != TargetId ? sum : sum + r.Value);

    // CastReach already includes the appropriate hitbox radii for the action's actual
    // range semantics. EffectRadius is not inflated here except by each recipient radius.
    internal ExecutionOpportunity(ExecutionShape shape, ulong targetId, WPos anchor, float castReach, float effectRadius,
        IEnumerable<ExecutionRecipient> recipients, float halfWidth = 0, Angle halfAngle = default,
        Positional positional = Positional.Any, Angle targetFacing = default, TargetFacingRequirement? tankFacing = null,
        IEnumerable<ExecutionRecipient>? forbiddenRecipients = null)
    {
        Shape = shape;
        TargetId = targetId;
        Anchor = anchor;
        CastReach = castReach;
        EffectRadius = effectRadius;
        HalfWidth = halfWidth;
        HalfAngle = halfAngle;
        Positional = positional;
        TargetFacing = targetFacing;
        TankFacing = tankFacing;
        Recipients = recipients.ToImmutableArray();
        ForbiddenRecipients = forbiddenRecipients?.ToImmutableArray() ?? [];
        if (!Enum.IsDefined(shape) || !Enum.IsDefined(positional) || !Finite(anchor)
            || !float.IsFinite(castReach) || castReach < 0 || !float.IsFinite(effectRadius) || effectRadius < 0
            || !float.IsFinite(halfWidth) || halfWidth < 0 || !float.IsFinite(halfAngle.Rad) || halfAngle.Rad < 0 || halfAngle.Rad > MathF.PI
            || !float.IsFinite(targetFacing.Rad) || tankFacing != null && (!float.IsFinite(tankFacing.Direction.Rad)
                || !float.IsFinite(tankFacing.Tolerance.Rad) || tankFacing.Tolerance.Rad < 0 || tankFacing.Tolerance.Rad > MathF.PI)
            || shape is not (ExecutionShape.CasterCircle or ExecutionShape.GroundCircle) && targetId == 0
            || shape == ExecutionShape.Self && (positional != Positional.Any || tankFacing != null)
            || Recipients.IsEmpty || Recipients.Concat(ForbiddenRecipients).Any(r => r.Id == 0 || !Finite(r.Position) || !float.IsFinite(r.Radius) || r.Radius < 0
                || !float.IsFinite(r.Value) || r.Value < 0 || !float.IsFinite(r.HeightOffset))
            || Recipients.Select(r => r.Id).Distinct().Count() != Recipients.Length)
            throw new ArgumentException("Incomplete executed action geometry.");
    }

    internal ExecutionOpportunity WithCastReach(float reach) => new(Shape, TargetId, Anchor, reach, EffectRadius,
        Recipients, HalfWidth, HalfAngle, Positional, TargetFacing, TankFacing, ForbiddenRecipients);

    internal ExecutionCoverage Evaluate(WPos position, bool ignorePositional = false)
    {
        if (!CanExecuteFrom(position, out var facing, ignorePositional))
            return default;
        foreach (var recipient in ForbiddenRecipients)
            if (CoversFrom(position, recipient, facing))
                return default;
        var value = 0f;
        foreach (var recipient in Recipients)
        {
            if (CoversFrom(position, recipient, facing))
                value += recipient.Value;
            else if (recipient.Required)
                return default;
        }
        return float.IsFinite(value) ? new(true, value, facing) : default;
    }

    internal bool Covers(WPos position, ulong recipientId, bool ignorePositional = false)
    {
        if (!CanExecuteFrom(position, out var facing, ignorePositional))
            return false;
        foreach (var recipient in Recipients)
            if (recipient.Id == recipientId)
                return CoversFrom(position, recipient, facing);
        return false;
    }

    private bool CanExecuteFrom(WPos position, out Angle? facing, bool ignorePositional = false)
    {
        facing = null;
        if (!Finite(position) || Shape is not (ExecutionShape.CasterCircle or ExecutionShape.Self) && !position.InCircle(Anchor, CastReach))
            return false;
        if (Shape == ExecutionShape.Self) return true;
        var outward = position - Anchor;
        var directional = Shape is ExecutionShape.Cone or ExecutionShape.Rectangle;
        if (outward.LengthSq() <= 1e-8f)
            return !directional && (ignorePositional || Positional == Positional.Any) && TankFacing == null;
        if (!ignorePositional && Positional != Positional.Any)
        {
            var dot = outward.Normalized().Dot(TargetFacing.ToDirection());
            var inPosition = Positional switch
            {
                Positional.Rear => dot <= -0.7071068f,
                Positional.Flank => Math.Abs(dot) <= 0.7071068f,
                Positional.Front => dot >= 0.7071068f,
                _ => false
            };
            if (!inPosition)
                return false;
        }
        if (TankFacing is { } tank && !Angle.FromDirection(outward).AlmostEqual(tank.Direction, tank.Tolerance.Rad))
            return false;
        if (directional)
            facing = Angle.FromDirection(-outward);
        return true;
    }

    private bool CoversFrom(WPos position, ExecutionRecipient recipient, Angle? facing)
    {
        var reach = EffectRadius + recipient.Radius;
        return Shape switch
        {
            ExecutionShape.SingleTarget or ExecutionShape.Self => recipient.Id == TargetId,
            ExecutionShape.CasterCircle => (position - recipient.Position).LengthSq() + recipient.HeightOffset * recipient.HeightOffset <= reach * reach,
            ExecutionShape.TargetCircle or ExecutionShape.GroundCircle => (Anchor - recipient.Position).LengthSq()
                + recipient.HeightOffset * recipient.HeightOffset <= reach * reach,
            ExecutionShape.Cone => Intersect.CircleCone(recipient.Position, recipient.Radius, position, EffectRadius, facing!.Value.ToDirection(), HalfAngle),
            ExecutionShape.Rectangle => Intersect.CircleRect(recipient.Position, recipient.Radius,
                position + facing!.Value.ToDirection() * (EffectRadius * 0.5f), facing.Value.ToDirection(), HalfWidth, EffectRadius * 0.5f),
            _ => false
        };
    }

    internal bool SameGeometry(ExecutionOpportunity other)
        => Shape == other.Shape && TargetId == other.TargetId && Anchor == other.Anchor && CastReach == other.CastReach
            && EffectRadius == other.EffectRadius && HalfWidth == other.HalfWidth && HalfAngle == other.HalfAngle
            && Positional == other.Positional && (Positional == Positional.Any || TargetFacing == other.TargetFacing)
            && TankFacing == other.TankFacing
            && Recipients.SequenceEqual(other.Recipients) && ForbiddenRecipients.SequenceEqual(other.ForbiddenRecipients);

    // Work may finish across a pose refresh, but its result still needs exact
    // geometry validation before publication. Identity/requirements cannot drift.
    internal bool SameBinding(ExecutionOpportunity other)
        => Shape == other.Shape && TargetId == other.TargetId && CastReach == other.CastReach
            && EffectRadius == other.EffectRadius && HalfWidth == other.HalfWidth && HalfAngle == other.HalfAngle
            && Positional == other.Positional && TankFacing == other.TankFacing
            && Recipients.Length == other.Recipients.Length && ForbiddenRecipients.Length == other.ForbiddenRecipients.Length
            && Recipients.Select((r, i) => (r with { Position = other.Recipients[i].Position, HeightOffset = other.Recipients[i].HeightOffset }) == other.Recipients[i]).All(s => s)
            && ForbiddenRecipients.Select((r, i) => (r with { Position = other.ForbiddenRecipients[i].Position,
                HeightOffset = other.ForbiddenRecipients[i].HeightOffset }) == other.ForbiddenRecipients[i]).All(s => s);

    private static bool Finite(WPos point) => float.IsFinite(point.X) && float.IsFinite(point.Z);
}
