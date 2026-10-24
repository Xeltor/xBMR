using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Planning;

internal sealed record ObservedStarryMuse(ulong Owner, uint Sequence, WPos Center, float Height, DateTime ExpiresAt);
internal sealed record StarryMuseTimingConditions(ulong Player, ushort Zone, int Level, ClientState.Stats Stats,
    ImmutableArray<CastModifier> Modifiers)
{
    internal bool Same(StarryMuseTimingConditions other) => Player == other.Player && Zone == other.Zone && Level == other.Level
        && Stats == other.Stats && Modifiers.Length == other.Modifiers.Length
        && Modifiers.Zip(other.Modifiers).All(pair => pair.First.Id == pair.Second.Id
            && pair.First.Extra == pair.Second.Extra && pair.First.Source == pair.Second.Source);
}

// Calibration for one observed owned landscape, not a history or a multiplier.
// The center is a bounded return alternative only after Inspiration and this
// exact selected action's timing have actually been observed there. No radius
// is inferred from the party buff, visual effect or a few covered positions.
internal sealed class NativePictomancerTiming
{
    private sealed record Sample(float? Outside, float OutsideRecast, float? Inside = null,
        float InsideRecast = 0, DateTime InsideUntil = default);
    private readonly Dictionary<uint, Sample> _samples = [];
    private StarryMuseTimingConditions? _conditions;
    private ObservedStarryMuse? _field;
    private DateTime _observed;

    internal PlanningCastTiming Capture(WorldState world, Actor player, ObservedStarryMuse? field,
        RsrActionEvidence action, PlanningCastTiming timing, bool enabled, bool allowSmudge)
    {
        if (!enabled || player.Class != Class.PCT || player.IsDead || player.IsDestroyed
            || player.FindStatus(3688) is not { } hyperphantasia)
        {
            Reset();
            return timing;
        }
        var conditions = new StarryMuseTimingConditions(player.InstanceID, world.CurrentZone, player.Level, world.Client.PlayerStats,
            player.Statuses.Where(s => s.ID != 0 && s.ID != 3689 && s.ExpireAt > world.CurrentTime)
                .Select(s => new CastModifier(s.ID, s.Extra, s.SourceID, s.ExpireAt))
                .OrderBy(s => s.Id).ThenBy(s => s.Source).ToImmutableArray());
        return Observe(conditions, field, world.CurrentTime, player.Position, player.FindStatus(3689) != null,
            hyperphantasia.ExpireAt, action.ActionId, action.WithoutProcCastTime, action.Recast, timing, allowSmudge);
    }

    internal PlanningCastTiming Observe(StarryMuseTimingConditions conditions, ObservedStarryMuse? field, DateTime now,
        WPos position, bool covered, DateTime modifierDeadline, uint action, float cast, float recast,
        PlanningCastTiming timing, bool allowSmudge)
    {
        if (_conditions == null || !_conditions.Same(conditions) || now < _observed
            || field == null || field.Owner != conditions.Player || field.Sequence == 0 || field.ExpiresAt <= now
            || _field == null || field.Owner != _field.Owner || field.Sequence != _field.Sequence
            || field.Center != _field.Center || field.Height != _field.Height)
            Reset();
        _conditions = conditions;
        _observed = now;
        if (field == null || field.Owner != conditions.Player || field.Sequence == 0 || field.ExpiresAt <= now)
            return timing;
        _field = field with { ExpiresAt = _field == null ? field.ExpiresAt : new[] { _field.ExpiresAt, field.ExpiresAt }.Min() };
        if (_field.ExpiresAt <= now || modifierDeadline <= now || action == 0
            || !float.IsFinite(cast) || cast < 0 || !float.IsFinite(recast) || recast <= 0) return timing;
        var until = conditions.Modifiers.Select(s => s.ExpiresAt).Append(modifierDeadline).Append(_field.ExpiresAt).Min();
        var sample = _samples.GetValueOrDefault(action) ?? new Sample(null, 0);
        if (!covered)
        {
            if (sample.Outside != cast || sample.OutsideRecast != recast)
                sample = sample with { Outside = cast, OutsideRecast = recast };
        }
        else if (position == _field.Center)
            sample = sample with { Inside = cast, InsideRecast = recast,
                InsideUntil = sample.Inside == cast && sample.InsideRecast == recast && sample.InsideUntil > now
                    ? new[] { sample.InsideUntil, until }.Min() : until };
        _samples[action] = sample;
        if (sample.Inside is not { } fast || sample.Outside is not { } outside || sample.InsideUntil <= now
            || fast >= outside && sample.InsideRecast >= sample.OutsideRecast) return timing;
        return timing with
        {
            // At execution the native buff/timing corroborates the actual stopped
            // pose, which need not equal the mathematical return waypoint exactly.
            HasteCenter = covered ? position : _field.Center,
            HasteRadius = 0,
            HasteUntil = (float)(new[] { sample.InsideUntil, until }.Min() - now).TotalSeconds,
            ObservedFieldCast = fast,
            ObservedOutsideCast = outside,
            ObservedFieldRecast = sample.InsideRecast,
            ObservedOutsideRecast = sample.OutsideRecast,
            OutsideTimingKnown = true,
            OutsideObservationUntil = (float)(until - now).TotalSeconds,
            FieldBenefitExcludedAfterAction = allowSmudge ? 0u : 34684u
        };
    }

    internal void Reset()
    {
        _samples.Clear();
        _conditions = null;
        _field = null;
        _observed = default;
    }
}
