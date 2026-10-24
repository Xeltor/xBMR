using BossMod.CombatAssistance.Planning;
using System.Numerics;

namespace BossMod.CombatAssistance.Knockback;

internal readonly record struct KnockbackEffectDefinition(float Distance, float Speed, byte Direction);
internal sealed record NativeKnockbackObservationStatus(bool Active, int Sources, int TimingRecords, bool Pending, string Detail);

// Framework-owned evidence for published BMR mechanics only. This has no file,
// import, unknown-source inference, geometry classifier or party-history buffer.
internal sealed class NativeKnockbackObserver(WorldState world, Func<bool> manualInput,
    Func<uint, KnockbackEffectDefinition?> effectDefinition, Action<Exception, string>? log = null) : IDisposable
{
    private sealed record Cast(AuthoredKnockbackSource Source, ulong Generation, float Elapsed, DateTime Seen, uint Counter = 0, bool Ambiguous = false);
    private sealed record Timing(AuthoredKnockbackSource Source, float Earliest, float Latest, float LandingLatest, long Revision);
    private sealed record Motion(Cast Cast, uint Action, uint Counter, Vector2 Launch, float Speed,
        DateTime PacketAt, DateTime Deadline, Vector2 LastPosition, DateTime LastAt,
        DateTime Started = default, DateTime OnsetEarliest = default, DateTime StableSince = default);
    private readonly Dictionary<ulong, Cast> _casts = [];
    private readonly List<Timing> _timings = [];
    private IDisposable? _subscription;
    private object? _module;
    private ulong _player;
    private ushort _zone;
    private ushort _content;
    private DateTime _lastAt;
    private Vector2 _position;
    private DateTime _positionAt;
    private ulong _generation;
    private long _revision;
    private bool _active;
    private bool _disposed;
    private Motion? _motion;
    private KnockbackCompletionEvidence? _completion;
    private DateTime _blockedUntil;
    private string _detail = "No authored observation active.";

    internal NativeKnockbackObservationStatus Status => new(_active, _casts.Count, _timings.Count, _motion != null, _detail);
    internal KnockbackCompletionEvidence? Completion => _active && !manualInput() && _completion is { } value
        && world.CurrentTime >= value.ObservedUtc && world.CurrentTime - value.ObservedUtc < TimeSpan.FromMilliseconds(250)
        ? value : null;

    internal void Update(object? module, bool allowed)
    {
        if (_disposed) return;
        _subscription ??= world.Actors.CastEvent.Subscribe((source, effect) =>
        {
            try { OnEffect(source, effect); }
            catch (Exception error)
            {
                _motion = null; _completion = null; _detail = "Native effect observation unavailable.";
                log?.Invoke(error, _detail);
            }
        });
        var player = world.Party.Player();
        var now = world.CurrentTime;
        var active = allowed && module != null && world.CurrentZone != 0 && player is { IsDead: false, IsDestroyed: false, HPMP.CurHP: > 0 };
        if (!active || !ReferenceEquals(module, _module) || _player != player?.InstanceID || _zone != world.CurrentZone
            || _content != world.CurrentCFCID || now < _lastAt)
        {
            _casts.Clear(); _timings.Clear(); _motion = null; _completion = null; _blockedUntil = default;
        }
        _active = active; _module = module; _player = player?.InstanceID ?? 0; _zone = world.CurrentZone;
        _content = world.CurrentCFCID; _lastAt = now;
        if (!active || player == null) { _detail = "No authored observation active."; return; }
        if (manualInput()) { _motion = null; _completion = null; }
        foreach (var key in _casts.Where(pair => now > pair.Value.Source.Activation.AddSeconds(6)).Select(pair => pair.Key).ToArray())
            _casts.Remove(key);
        ObserveMotion(player, now);
        _position = new(player.Position.X, player.Position.Z); _positionAt = now;
    }

    // Presence is read from native publications, never inferred from an action
    // name, an old learned record or the absence of a current preparation shape.
    internal void Track(NativeDisplacementInventory inventory)
    {
        if (!_active || inventory.Actor != _player || inventory.Zone != _zone || inventory.At != world.CurrentTime) return;
        foreach (var group in inventory.Knockbacks.Where(source => source.Enabled && source.Participant && source.GeometryKnown
            && source.SourceActor != 0 && source.SourceDataId != 0 && source.CastActionId != 0 && source.Activation != default)
            .GroupBy(source => source.SourceActor))
        {
            if (group.Count() != 1) { _casts.Remove(group.Key); continue; }
            var source = group.Single();
            var actor = world.Actors.Find(source.SourceActor);
            if (actor?.CastInfo is not { EventHappened: false, Action.Type: ActionType.Spell } cast
                || cast.Action.ID != source.CastActionId || actor.OID != source.SourceDataId
                || !float.IsFinite(cast.ElapsedTime) || !float.IsFinite(cast.NPCRemainingTime) || cast.NPCRemainingTime < 0) continue;
            var same = _casts.TryGetValue(source.SourceActor, out var held) && SameCast(held.Source, source)
                && cast.ElapsedTime >= held.Elapsed - .05f;
            // A caster can start an unrelated action while a native component
            // still publishes an older event. Fresh binding needs the actual
            // native cast clock; an existing generation keeps its original time
            // through the native zero-remaining-time clamp, never a renewed end.
            if (Math.Abs(cast.NPCRemainingTime - (source.Activation - world.CurrentTime).TotalSeconds) > (same ? .3 : .01))
            { _casts.Remove(source.SourceActor); continue; }
            if (same && held != null)
                _casts[source.SourceActor] = held with { Elapsed = cast.ElapsedTime, Seen = world.CurrentTime };
            else if (_casts.Count < 32 || _casts.ContainsKey(source.SourceActor))
                _casts[source.SourceActor] = new(source, ++_generation, cast.ElapsedTime, world.CurrentTime,
                    Ambiguous: held is { Counter: 0 } && world.CurrentTime <= held.Source.Activation.AddSeconds(1.5));
        }
    }

    internal bool TryBind(AuthoredKnockbackSource source, out KnockbackActivationWindow? window,
        out ulong generation, out long revision, out string? reason)
    {
        window = null; generation = 0; revision = 0;
        reason = "authored source has no current native effect timing";
        if (!_active || !_casts.TryGetValue(source.SourceActor, out var cast) || cast.Ambiguous || !SameCast(cast.Source, source)
            || world.CurrentTime < cast.Seen || world.CurrentTime - cast.Seen > TimeSpan.FromMilliseconds(250)) return false;
        var timing = _timings.SingleOrDefault(value => SameTimingSource(value.Source, source));
        if (timing == null) return false;
        window = new(source.Activation.AddSeconds(timing.Earliest), source.Activation,
            source.Activation.AddSeconds(timing.Latest), source.Activation.AddSeconds(timing.LandingLatest));
        generation = cast.Generation; revision = timing.Revision; reason = null;
        return true;
    }

    private void OnEffect(Actor source, ActorCastEvent effect)
    {
        if (!_active || _disposed || manualInput() || source.IsDestroyed || effect.Action.Type != ActionType.Spell
            || effect.GlobalSequence == 0 || effect.Targets.Count is <= 0 or > 128) return;
        var effects = new List<ActionEffect>();
        var displacement = false;
        foreach (var target in effect.Targets)
            if (target.ID == _player)
                foreach (ref readonly var value in target.Effects.ValidEffects())
                {
                    displacement |= value.Type is >= ActionEffectType.Knockback and <= ActionEffectType.AttractCustom3;
                    if (value.Type == ActionEffectType.Knockback && value.Value != 0 && !value.FromTarget && !value.AtSource)
                        effects.Add(value);
                }
        if (!displacement) return;
        if (_completion?.EffectCounter != effect.GlobalSequence) _completion = null;
        if (_motion is { } pending && pending.Counter != effect.GlobalSequence)
        {
            _blockedUntil = pending.Deadline; _motion = null;
            _detail = "Overlapping displacement effects; completion unavailable.";
            return;
        }
        if (effects.Count != 1 || world.CurrentTime < _blockedUntil
            || !_casts.TryGetValue(source.InstanceID, out var cast) || source.OID != cast.Source.SourceDataId
            || cast.Ambiguous || cast.Counter != 0 || world.CurrentTime - cast.Seen > TimeSpan.FromSeconds(6)
            || (world.CurrentTime - cast.Source.Activation).TotalSeconds is < -.5 or > 1.5) return;
        var knockback = effects[0];
        var row = effectDefinition(knockback.Value);
        var now = world.CurrentTime;
        if (row is not { } definition || !float.IsFinite(definition.Speed) || definition.Speed is <= 0 or > 1000
            || !float.IsFinite(definition.Distance) || Math.Abs(definition.Distance + knockback.Param0 - cast.Source.Distance) > .01f
            || now < _positionAt || now - _positionAt > TimeSpan.FromMilliseconds(250)
            || world.Party.Player() is not { } player || !Finite(_position)
            || Vector2.DistanceSquared(_position, new(player.Position.X, player.Position.Z)) > .04f) return;
        var duration = cast.Source.Distance / definition.Speed;
        if (!float.IsFinite(duration) || duration is < .05f or > 3.5f) return;
        _casts[source.InstanceID] = cast with { Counter = effect.GlobalSequence };
        _completion = null;
        if (_motion != null) { _motion = null; _detail = "Overlapping native displacement effects; completion unavailable."; return; }
        _motion = new(cast, effect.Action.ID, effect.GlobalSequence, _position, definition.Speed,
            now, now.AddSeconds(duration + .75f), _position, _positionAt);
        _detail = "Observing an authored native effect; physical completion required.";
    }

    private void ObserveMotion(Actor player, DateTime now)
    {
        if (_motion is not { } motion) return;
        var position = new Vector2(player.Position.X, player.Position.Z);
        if (!Finite(position) || now < motion.LastAt || now > motion.Deadline || now - motion.LastAt > TimeSpan.FromMilliseconds(250))
        { _motion = null; _detail = "Native displacement observation expired or lost continuity."; return; }
        if (Vector2.DistanceSquared(position, motion.LastPosition) > .0225f)
        {
            _motion = motion with { LastPosition = position, LastAt = now, StableSince = default,
                Started = motion.Started == default ? now : motion.Started,
                OnsetEarliest = motion.Started == default ? motion.LastAt : motion.OnsetEarliest };
            return;
        }
        motion = motion with { LastAt = now, StableSince = motion.StableSince == default ? now : motion.StableSince };
        _motion = motion;
        if (motion.Started == default || player.PendingKnockbacks is { Count: > 0 }
            || now - motion.StableSince < TimeSpan.FromMilliseconds(120)
            || (now - motion.PacketAt).TotalSeconds < motion.Cast.Source.Distance / motion.Speed + .05f) return;
        _completion = new(_zone, _player, motion.Cast.Source.SourceActor, motion.Cast.Source.SourceDataId,
            motion.Cast.Source.CastActionId, motion.Action, motion.Counter, motion.Launch, position,
            motion.Started, motion.StableSince, now, motion.Cast.Generation);
        // Timing belongs only to this authored source family in this module
        // session. Native geometry is always recaptured, never learned here.
        var earliest = Math.Min(0, (float)(motion.OnsetEarliest - motion.Cast.Source.Activation).TotalSeconds);
        var latest = Math.Max(0, (float)(motion.Started - motion.Cast.Source.Activation).TotalSeconds);
        var landing = Math.Max((float)(now - motion.Cast.Source.Activation).TotalSeconds,
            latest + motion.Cast.Source.Distance / motion.Speed + .75f);
        var prior = _timings.SingleOrDefault(value => SameTimingSource(value.Source, motion.Cast.Source));
        if (prior != null) _timings.Remove(prior);
        if (_timings.Count < 64)
            _timings.Add(new(motion.Cast.Source, prior == null ? earliest : Math.Min(earliest, prior.Earliest),
                prior == null ? latest : Math.Max(latest, prior.Latest), prior == null ? landing : Math.Max(landing, prior.LandingLatest), ++_revision));
        _motion = null; _detail = "Authored effect and stable physical completion observed; no unknown mechanic inferred.";
    }

    private static bool SameTimingSource(AuthoredKnockbackSource left, AuthoredKnockbackSource right)
        => left.OwnerType == right.OwnerType && left.CastActionId == right.CastActionId && left.SourceDataId == right.SourceDataId
            && left.Kind == right.Kind && left.Distance == right.Distance && left.MinimumDistance == right.MinimumDistance
            && left.StopAtWall == right.StopAtWall && left.StopAfterWall == right.StopAfterWall
            && left.ShapeType == right.ShapeType && left.ProjectionLayer == right.ProjectionLayer
            && left.RestrictLayer == right.RestrictLayer && left.Walls.SequenceEqual(right.Walls);

    private static bool SameCast(AuthoredKnockbackSource left, AuthoredKnockbackSource right)
        => left.Same(right with { ObservedImmune = left.ObservedImmune });

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _active = false; _subscription?.Dispose();
        _casts.Clear(); _timings.Clear(); _motion = null; _completion = null;
    }
}
