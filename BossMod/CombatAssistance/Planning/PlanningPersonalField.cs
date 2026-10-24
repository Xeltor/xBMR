namespace BossMod.CombatAssistance.Planning;

// One currently observed owned Ley Lines. Relocation changes only Center;
// effect identity and expiry survive Retrace's replacement ground object. Only
// the framework observer may corroborate that continuity from the owned action.
internal sealed record PlanningPersonalField(ulong Owner, ulong Object, WPos Center, float Radius, float EndsIn, ulong Effect = 0)
{
    internal ulong EffectIdentity => Effect == 0 ? Object : Effect;
    internal bool Valid => Owner != 0 && Object != 0 && float.IsFinite(Center.X) && float.IsFinite(Center.Z)
        && float.IsFinite(Radius) && Radius > 0 && float.IsFinite(EndsIn) && EndsIn > 0;
    internal bool SameIdentity(PlanningPersonalField other) => Owner == other.Owner && EffectIdentity == other.EffectIdentity && Radius == other.Radius;
    internal bool Same(PlanningPersonalField other, float age) => SameIdentity(other) && Center == other.Center
        && Math.Abs(EndsIn - Math.Max(0, other.EndsIn - age)) < 0.01f;
    internal bool LiveAt(float time) => time >= 0 && time < EndsIn;
}
