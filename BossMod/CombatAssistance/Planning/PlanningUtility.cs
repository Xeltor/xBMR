namespace BossMod.CombatAssistance.Planning;

// An observed available action changes execution capability for a finite duration.
// It is part of the same route/resource search, not an arrival correction.
internal enum PlanningUtilityEffect { IgnorePositional, MovementSpeed, RelocatePersonalField, KnockbackImmunity }
internal sealed record PlanningUtility(uint ActionId, PlanningUtilityEffect Effect, float Duration, float LockSeconds,
    float ReadyIn, int Resource, float Speed = 0, ulong AdditionalResourceCost = 0, PlanningPersonalField? Field = null, float ApplicationSeconds = 0)
{
    internal ulong ResourceCost => (1ul << (Resource * 4)) | AdditionalResourceCost;
    internal bool Valid => ActionId != 0 && Enum.IsDefined(Effect) && Resource is >= 0 and < 16
        && (AdditionalResourceCost & (15ul << (Resource * 4))) == 0
        && float.IsFinite(Duration) && Duration > 0 && float.IsFinite(LockSeconds) && LockSeconds > 0
        && float.IsFinite(ReadyIn) && ReadyIn >= 0 && float.IsFinite(Speed)
        && (Effect != PlanningUtilityEffect.MovementSpeed || Speed > 0)
        && float.IsFinite(ApplicationSeconds) && (Effect == PlanningUtilityEffect.KnockbackImmunity
            ? ActionId is 7548 or 7559 && Duration == 6 && ApplicationSeconds > 0 && ApplicationSeconds <= LockSeconds
            : ApplicationSeconds == 0)
        && (Effect == PlanningUtilityEffect.RelocatePersonalField ? ActionId == 36988 && Field?.Valid == true : Field == null);
    internal bool Same(PlanningUtility other, float age)
        => ActionId == other.ActionId && Effect == other.Effect && Duration == other.Duration
            && AdditionalResourceCost == other.AdditionalResourceCost && ApplicationSeconds == other.ApplicationSeconds
            && LockSeconds == other.LockSeconds && Resource == other.Resource && Speed == other.Speed
            && (Field == null ? other.Field == null : other.Field != null && Field.Same(other.Field, age))
            && Math.Abs(ReadyIn - Math.Max(0, other.ReadyIn - age)) <= 0.01f;
}
