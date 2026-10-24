using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Planning;

// A native cast-time observation is conditional on the proc and location where
// it was captured. Losing either condition must not retain the faster timing.
internal sealed record PlanningCastTiming(float WithoutProc, float UnbuffedUpperBound, float ProcUntil,
    float HasteUntil = float.MaxValue, WPos? HasteCenter = null, float HasteRadius = 0,
    float? ObservedFieldCast = null, bool FieldEntryByAbility = false, float? ObservedOutsideCast = null, float OutsideObservationUntil = float.MaxValue,
    float NativeRecast = 0, float? ObservedFieldRecast = null, float? ObservedOutsideRecast = null, float UnbuffedRecastUpperBound = 0,
    bool OutsideTimingKnown = true, uint FieldBenefitExcludedAfterAction = 0)
{
    internal bool Valid => float.IsFinite(WithoutProc) && WithoutProc >= 0 && float.IsFinite(UnbuffedUpperBound) && UnbuffedUpperBound >= WithoutProc
        && float.IsFinite(ProcUntil) && ProcUntil >= 0 && float.IsFinite(HasteUntil) && HasteUntil >= 0
        && float.IsFinite(HasteRadius) && HasteRadius >= 0
        && (ObservedFieldCast == null || float.IsFinite(ObservedFieldCast.Value) && ObservedFieldCast >= 0)
        && (ObservedOutsideCast == null || float.IsFinite(ObservedOutsideCast.Value) && ObservedOutsideCast >= 0)
        && float.IsFinite(OutsideObservationUntil) && OutsideObservationUntil >= 0
        && float.IsFinite(NativeRecast) && NativeRecast >= 0
        && float.IsFinite(UnbuffedRecastUpperBound) && UnbuffedRecastUpperBound >= 0
        && (ObservedFieldRecast == null || float.IsFinite(ObservedFieldRecast.Value) && ObservedFieldRecast >= 0)
        && (ObservedOutsideRecast == null || float.IsFinite(ObservedOutsideRecast.Value) && ObservedOutsideRecast >= 0)
        && (HasteCenter == null || float.IsFinite(HasteCenter.Value.X) && float.IsFinite(HasteCenter.Value.Z))
        && (!FieldEntryByAbility || ObservedFieldCast != null && HasteCenter != null);

    internal bool Covers(WPos position, float start, PlanningPersonalField? field = null) => (field?.Center ?? HasteCenter) is { } center
        && start < HasteUntil && (field == null || field.LiveAt(start))
        && (position - center).LengthSq() <= HasteRadius * HasteRadius;

    internal float RecastAt(WPos position, float start, bool enteredByAbility = false, PlanningPersonalField? field = null, bool fieldBenefitAllowed = true)
    {
        if (NativeRecast <= 0) return 0;
        if (fieldBenefitAllowed && Covers(position, start, field) && (!FieldEntryByAbility || enteredByAbility))
            return ObservedFieldRecast is { } fast ? Math.Min(NativeRecast, fast) : NativeRecast;
        if (HasteCenter != null && (!OutsideTimingKnown || start >= OutsideObservationUntil)) return float.PositiveInfinity;
        return HasteCenter == null ? NativeRecast : Math.Max(NativeRecast,
            start < OutsideObservationUntil && ObservedOutsideRecast is { } outside ? outside : UnbuffedRecastUpperBound);
    }

    internal float Seconds(float observed, WPos position, float start, bool enteredByAbility = false, PlanningPersonalField? field = null, bool fieldBenefitAllowed = true)
    {
        var seconds = start < ProcUntil ? observed : WithoutProc;
        // A relocated field can reuse only a paired observation of this exact
        // personal effect. Other jobs' pose-only haste evidence is unchanged.
        if (ObservedFieldCast == null) field = null;
        if (seconds > 0 && HasteCenter != null && (!fieldBenefitAllowed || !Covers(position, start, field)))
            seconds = !OutsideTimingKnown || start >= OutsideObservationUntil ? float.PositiveInfinity
                : Math.Max(seconds, ObservedOutsideCast is { } outside ? outside : UnbuffedUpperBound);
        else if (seconds > 0 && HasteCenter != null && start < HasteUntil && ObservedFieldCast is { } fast && (!FieldEntryByAbility || enteredByAbility))
            seconds = Math.Min(seconds, fast);
        return seconds;
    }

    internal bool Same(PlanningCastTiming previous, float age)
        => WithoutProc == previous.WithoutProc && UnbuffedUpperBound == previous.UnbuffedUpperBound
            && HasteCenter == previous.HasteCenter && HasteRadius == previous.HasteRadius
            && ObservedFieldCast == previous.ObservedFieldCast && FieldEntryByAbility == previous.FieldEntryByAbility
            && ObservedOutsideCast == previous.ObservedOutsideCast && Countdown(OutsideObservationUntil, previous.OutsideObservationUntil, age)
            && NativeRecast == previous.NativeRecast && ObservedFieldRecast == previous.ObservedFieldRecast && ObservedOutsideRecast == previous.ObservedOutsideRecast
            && UnbuffedRecastUpperBound == previous.UnbuffedRecastUpperBound
            && FieldBenefitExcludedAfterAction == previous.FieldBenefitExcludedAfterAction
            && OutsideTimingKnown == previous.OutsideTimingKnown
            && Countdown(ProcUntil, previous.ProcUntil, age) && Countdown(HasteUntil, previous.HasteUntil, age);

    private static bool Countdown(float current, float previous, float age)
        => current == float.MaxValue ? previous == float.MaxValue : Math.Abs(current - Math.Max(0, previous - age)) < 0.01f;

    internal static bool BenefitAfter(bool previous, MovementStage stage, ImmutableArray<FrozenPlanningAction> actions)
        => previous && !actions.Any(a => a.Timing is { FieldBenefitExcludedAfterAction: not 0 } timing
            && stage.ActionId == timing.FieldBenefitExcludedAfterAction
            && stage.Kind is MovementStageKind.TargetDash or MovementStageKind.GroundMovement
                or MovementStageKind.DirectionalMovement or MovementStageKind.Return);

    internal static bool EntryAfter(bool previous, MovementStage stage, ImmutableArray<FrozenPlanningAction> actions, PlanningPersonalField? field = null)
    {
        foreach (var action in actions)
            if (action.Timing is { FieldEntryByAbility: true } timing && timing.Covers(stage.To, stage.EndSeconds, field))
                return previous || stage.ActionId != 0 && stage.Kind is MovementStageKind.Return or MovementStageKind.TargetDash
                    or MovementStageKind.GroundMovement or MovementStageKind.DirectionalMovement
                    || stage.Kind == MovementStageKind.UtilityAction && stage.ActionId == 36988 && stage.Field != null;
        return false;
    }

    internal static PlanningCastTiming Capture(WorldState world, Actor player, RsrActionEvidence evidence)
    {
        var proc = evidence.CastTimeProc;
        var status = proc switch
        {
            1 => 165u,
            3 => 167u,
            4 => 841u,
            5 => 1249u,
            7 => 1211u,
            9 => 1368u,
            12 => 2845u,
            14 => 1238u,
            15 => 2673u,
            19 => 3679u,
            _ => 0u
        };
        var until = proc == 0 ? float.MaxValue : status != 0 && player.FindStatus(status) is { } buff
            ? Math.Max(0, (float)(buff.ExpireAt - world.CurrentTime).TotalSeconds) : 0;
        if (proc is 8 or 11)
            until = world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.SummonerGauge>().SummonTimer * 0.001f;
        var profile = new PlanningCastTiming(evidence.WithoutProcCastTime, Math.Max(evidence.BaseCastTime, evidence.WithoutProcCastTime), until,
            NativeRecast: evidence.Recast, UnbuffedRecastUpperBound: Math.Max(evidence.BaseRecast, evidence.Recast));
        if (player.Class == Class.BLM && player.FindStatus(738) is { } circle)
        {
            var ground = NativeLeyLines.Capture(world, player);
            // An absent/ambiguous object proves only the current observed pose.
            profile = profile with
            {
                HasteCenter = ground?.Center ?? player.Position,
                HasteRadius = ground != null ? 3 : 0,
                OutsideTimingKnown = false,
                HasteUntil = Math.Max(0, (float)((ground == null || circle.ExpireAt < ground.ExpiresAt ? circle.ExpireAt : ground.ExpiresAt) - world.CurrentTime).TotalSeconds)
            };
        }
        if (player.Class == Class.PCT && player.FindStatus(3689) is { } inspiration && (evidence.CastTime > 0 || evidence.Recast > 0))
            // Current native timing is valid here. Do not infer the landscape's
            // cast-speed footprint from its separate thirty-yalm party buff.
            profile = profile with
            {
                HasteCenter = player.Position,
                HasteRadius = 0,
                OutsideTimingKnown = false,
                HasteUntil = Math.Max(0, (float)(inspiration.ExpireAt - world.CurrentTime).TotalSeconds)
            };
        return profile;
    }
}
