namespace BossMod.CombatAssistance.Planning;

internal enum PlanningZoneBenefit { PersonalCasting, Mitigation, Healing }

// An observed friendly effect is useful evidence, never a safety constraint or
// a prediction of future actions. Its lifetime belongs to the original capture.
internal sealed record PlanningZone(uint ActionId, ulong OwnerId, string Name, WPos Center, float Height,
    float Radius, float EndsIn, PlanningZoneBenefit Benefit, Angle? Facing = null, float HalfAngle = MathF.PI, bool CompleteFootprint = true)
{
    internal bool Valid => OwnerId != 0 && float.IsFinite(Center.X) && float.IsFinite(Center.Z) && float.IsFinite(Height)
        && float.IsFinite(Radius) && Radius >= 0 && float.IsFinite(EndsIn) && EndsIn > 0
        && float.IsFinite(HalfAngle) && HalfAngle > 0 && HalfAngle <= MathF.PI
        && (Facing == null || float.IsFinite(Facing.Value.Rad));

    internal WPos EffectiveCenter(PlanningPersonalField? field) => ActionId == 3573 && OwnerId == field?.Owner ? field.Center : Center;
    internal bool Contains(WPos position, float arrival, PlanningPersonalField? field = null)
    {
        if (arrival < 0 || arrival >= EndsIn) return false;
        if (ActionId == 3573 && OwnerId == field?.Owner && !field.LiveAt(arrival)) return false;
        var offset = position - EffectiveCenter(field);
        if (offset.LengthSq() > Radius * Radius) return false;
        return Facing == null || offset.LengthSq() > 0.01f
            && Math.Abs((Angle.FromDirection(offset) - Facing.Value).Normalized().Rad) <= HalfAngle;
    }

    internal bool Same(PlanningZone previous, float age)
        => this with { EndsIn = previous.EndsIn } == previous
            && Math.Abs(EndsIn - Math.Max(0, previous.EndsIn - age)) < 0.01f;
}
