using System.Globalization;

namespace BossMod.Integration;

// A bounded current-update observation, not a combat-history recorder.
internal sealed class IntegrationActionEffects : IDisposable
{
    internal const int MaxEffects = 64;
    internal sealed record Effect(long ObservationSequence, uint WorldFrame, string WorldTimeTicks, string WorldTimeKind,
        DateTimeOffset ObservationUtc, string ActorId, uint ActionId, int ActionType, string MainTargetId,
        float[]? GroundPosition, uint GlobalSequence, uint SourceSequence);
    internal sealed record Snapshot(bool Complete, int Dropped, Effect[] Effects);

    private readonly Func<(uint Frame, DateTime WorldTime)> _clock;
    private readonly EventSubscription _subscription;
    private readonly List<Effect> _effects = [];
    private long _sequence;
    private int _dropped;

    internal IntegrationActionEffects(WorldState world) : this(world.Actors, () => (world.Frame.Index, world.CurrentTime)) { }

    internal IntegrationActionEffects(ActorState actors, Func<(uint Frame, DateTime WorldTime)> clock)
    {
        _clock = clock;
        _subscription = actors.CastEvent.Subscribe(Observe);
    }

    internal void BeginUpdate()
    {
        _effects.Clear();
        _dropped = 0;
    }

    private void Observe(Actor actor, ActorCastEvent effect)
    {
        ++_sequence;
        if (_effects.Count == MaxEffects)
        {
            ++_dropped;
            return;
        }
        var p = effect.TargetPos;
        var (frame, worldTime) = _clock();
        _effects.Add(new(_sequence, frame, worldTime.Ticks.ToString(CultureInfo.InvariantCulture),
            worldTime.Kind.ToString(), DateTimeOffset.UtcNow, actor.InstanceID.ToString("X"), effect.Action.ID,
            (int)effect.Action.Type, effect.MainTargetID.ToString("X"),
            float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z) ? [p.X, p.Y, p.Z] : null,
            effect.GlobalSequence, effect.SourceSequence));
    }

    internal Snapshot Capture() => new(_dropped == 0, _dropped, _effects.ToArray());

    public void Dispose()
    {
        _subscription.Dispose();
        BeginUpdate();
    }
}
