using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Planning;

internal readonly record struct CastModifier(uint Id, ushort Extra, ulong Source, DateTime ExpiresAt);
internal sealed record LeyLinesTimingConditions(ulong Player, ushort Zone, int Level, ClientState.Stats Stats, sbyte Element,
    ImmutableArray<CastModifier> Modifiers)
{
    internal bool Same(LeyLinesTimingConditions other) => Player == other.Player && Zone == other.Zone && Level == other.Level
        && Stats == other.Stats && Element == other.Element && Modifiers.Length == other.Modifiers.Length
        && Modifiers.Zip(other.Modifiers).All(pair => pair.First.Id == pair.Second.Id && pair.First.Extra == pair.Second.Extra && pair.First.Source == pair.Second.Source);
}

// Current-effect calibration, not a combat history or a future spell predictor.
// The native no-proc API supplies both samples under matching cast conditions;
// no fixed Ley Lines multiplier or unrelated action's timing is substituted.
internal sealed class NativeLeyLinesTiming
{
    private static readonly PlanningCastTiming NoActionTiming = new(0, 0, float.MaxValue);
    private sealed record Sample(float? Outside, float OutsideRecast, float? Inside = null, float? InsideRecast = null, DateTime InsideUntil = default);
    private readonly Dictionary<uint, Sample> _samples = [];
    private LeyLinesTimingConditions? _conditions;
    private ulong? _field;
    private ulong _effect;
    private bool _relocationObserved;
    private DateTime _fieldDeadline;
    private DateTime _observed;
    internal ObservedLeyLines? CurrentField { get; private set; }
    internal PlanningPersonalField? PlanningField(DateTime now)
        => CurrentField is { } field && field.ExpiresAt > now
            ? new(field.Owner, field.Object, field.Center, 3, (float)(field.ExpiresAt - now).TotalSeconds, field.EffectIdentity) : null;

    internal PlanningCastTiming Capture(WorldState world, Actor player, RsrActionEvidence action, PlanningCastTiming timing,
        bool assistance, bool allowWalking)
    {
        if (!assistance || player.Class != Class.BLM || player.IsDead || player.IsDestroyed)
        {
            Reset();
            return timing;
        }
        var conditions = Conditions(world, player);
        return Observe(conditions, Field(world, player), world.CurrentTime, player.Position,
            player.FindStatus((uint)BLM.SID.CircleOfPower) != null, action.ActionId, action.WithoutProcCastTime, timing, allowWalking, action.Recast);
    }

    internal ObservedLeyLines? ObserveField(WorldState world, Actor player, bool enabled)
    {
        if (!enabled || player.Class != Class.BLM || player.IsDead || player.IsDestroyed)
        {
            Reset();
            return null;
        }
        Observe(Conditions(world, player), Field(world, player), world.CurrentTime, player.Position,
            player.FindStatus((uint)BLM.SID.CircleOfPower) != null, 0, 0, NoActionTiming, true);
        return CurrentField;
    }

    private static LeyLinesTimingConditions Conditions(WorldState world, Actor player)
        => new(player.InstanceID, world.CurrentZone, player.Level, world.Client.PlayerStats,
            world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.BlackMageGauge>().ElementStance,
            player.Statuses.Where(s => s.ID != 0 && s.ID is not (737 or 738) && s.ExpireAt > world.CurrentTime)
                .Select(s => new CastModifier(s.ID, s.Extra, s.SourceID, s.ExpireAt)).OrderBy(s => s.Id).ThenBy(s => s.Source).ToImmutableArray());
    private static ObservedLeyLines? Field(WorldState world, Actor player)
    {
        var field = NativeLeyLines.Capture(world, player);
        return field != null && Math.Abs(field.Height - player.PosRot.Y) <= 2 ? field : null;
    }

    internal PlanningCastTiming Observe(LeyLinesTimingConditions conditions, ObservedLeyLines? field, DateTime now, WPos position,
        bool covered, uint action, float withoutProc, PlanningCastTiming timing, bool allowWalking, float recast = 0)
    {
        if (_conditions == null || _conditions.Player != conditions.Player || _conditions.Zone != conditions.Zone || now < _observed)
            Reset();
        else if (!_conditions.Same(conditions))
            // Rotation conditions retire cast calibration, not the identity or
            // original lifetime of the same still-observed personal effect.
            _samples.Clear();
        _conditions = conditions;
        _observed = now;
        foreach (var pair in _samples.Where(p => p.Value.Inside != null && p.Value.InsideUntil <= now).ToArray())
            _samples[pair.Key] = pair.Value with { Inside = null };
        var identity = field?.Object;
        if (field != null && _field != identity)
        {
            // Live Retrace replaces the ground object. Its owned action event,
            // the unique owned replacement and the still-live original effect
            // establish continuity; an unexplained replacement does not.
            if (_relocationObserved && field.Owner == conditions.Player && _fieldDeadline > now)
                _fieldDeadline = new[] { _fieldDeadline, field.ExpiresAt }.Min();
            else
            {
                foreach (var pair in _samples.ToArray()) _samples[pair.Key] = pair.Value with { Inside = null };
                _effect = field.Object;
                _fieldDeadline = field.ExpiresAt;
            }
            _field = identity;
            _relocationObserved = false;
        }
        else if (field != null && field.ExpiresAt < _fieldDeadline) _fieldDeadline = field.ExpiresAt;
        if (field == null)
            // A missing/ambiguous object withholds spatial proof. Retain the
            // old effect deadline so its reappearance cannot restart a lifetime.
            foreach (var pair in _samples.ToArray()) _samples[pair.Key] = pair.Value with { Inside = null };
        CurrentField = field != null && field.Owner == conditions.Player && _fieldDeadline > now
            ? field with { ExpiresAt = _fieldDeadline, Effect = _effect } : null;
        if (field != null && timing.HasteCenter != null)
            timing = timing with { HasteUntil = Math.Min(timing.HasteUntil, Math.Max(0, (float)(_fieldDeadline - now).TotalSeconds)) };
        var modifiersUntil = conditions.Modifiers.IsEmpty ? DateTime.MaxValue : conditions.Modifiers.Min(s => s.ExpiresAt);
        if (!float.IsFinite(withoutProc) || withoutProc < 0 || !float.IsFinite(recast) || recast < 0 || action == 0) return timing;
        if (!covered)
        {
            if (_samples.TryGetValue(action, out var old) && old.Outside == null && old.Inside is { } inside && inside < withoutProc)
                _samples[action] = old with { Outside = withoutProc, OutsideRecast = recast };
            else if (old == null || old.Outside != withoutProc || old.OutsideRecast != recast)
                _samples[action] = new(withoutProc, recast);
        }
        else if (field != null && field.ExpiresAt > now && position.InCircle(field.Center, 3))
        {
            var old = _samples.GetValueOrDefault(action) ?? new Sample(null, 0);
            _samples[action] = old with
            {
                Inside = withoutProc,
                InsideRecast = recast,
                InsideUntil = new[] { now.AddSeconds(Math.Min(timing.HasteUntil, (float)(_fieldDeadline - now).TotalSeconds)), modifiersUntil }.Min()
            };
        }
        if (field == null || field.Owner != conditions.Player || field.ExpiresAt <= now
            || !_samples.TryGetValue(action, out var pairSample) || pairSample.Inside is not { } fast
            || pairSample.Outside is not { } outside
            || fast >= outside && !(pairSample.InsideRecast is > 0 && pairSample.InsideRecast < pairSample.OutsideRecast)) return timing;
        var fastUntil = new[] { pairSample.InsideUntil, _fieldDeadline, modifiersUntil }.Min();
        if (fastUntil <= now) return timing;
        return timing with
        {
            HasteCenter = field.Center,
            HasteRadius = 3,
            HasteUntil = (float)(fastUntil - now).TotalSeconds,
            ObservedFieldCast = fast,
            ObservedOutsideCast = outside,
            NativeRecast = recast,
            ObservedFieldRecast = pairSample.InsideRecast,
            ObservedOutsideRecast = pairSample.OutsideRecast,
            OutsideObservationUntil = modifiersUntil == DateTime.MaxValue ? float.MaxValue : Math.Max(0, (float)(modifiersUntil - now).TotalSeconds),
            OutsideTimingKnown = true,
            FieldEntryByAbility = !allowWalking && !covered
        };
    }

    internal void Reset()
    {
        _samples.Clear();
        _conditions = null;
        _field = null;
        _effect = 0;
        _relocationObserved = false;
        _fieldDeadline = default;
        _observed = default;
        CurrentField = null;
    }

    internal void ObserveAction(ulong actor, ActionID action, DateTime now)
    {
        if (actor != _conditions?.Player || action.Type != ActionType.Spell) return;
        if (action.ID == 3573) Reset();
        else if (action.ID == 36988 && _field != null && now >= _observed && now < _fieldDeadline)
            _relocationObserved = true;
    }
}
