namespace BossMod.Heavensward.Alliance.A33ProtoUltima;

// The written guide identifies a three-ray sequence. The original has side
// rays at +/-45 degrees, with 4.09s between hits. Their order is not assumed:
// both initially use the earliest side deadline, then the next actual cast
// identifies which remaining ray can wait another interval.
class AetherochemicalLasers(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(71, 4);
    private readonly Dictionary<uint, (Angle Rotation, DateTime Activation, bool Started)> _rays = [];
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<uint> _resolved = [];
    private Actor? _source;
    private uint _activeCast;

    private static bool IsRay(uint action) => action is (uint)AID.AetherochemicalLaser1 or (uint)AID.AetherochemicalLaser2 or (uint)AID.AetherochemicalLaser3;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        if (_source is { IsDeadOrDestroyed: false } source)
            foreach (var ray in _rays.Values.OrderBy(r => r.Activation))
                _aoes.Add(new(Shape, source.Position, ray.Rotation, ray.Activation, actorID: source.InstanceID));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;
        var action = spell.Action.ID;
        if (!IsRay(action))
        {
            Clear();
            return;
        }
        var finish = Module.CastFinishAt(spell);
        if (_source == caster && _activeCast == action && _rays.TryGetValue(action, out var existing)
            && Math.Abs((existing.Activation - finish).TotalSeconds) < 0.01)
            return;
        if (action == (uint)AID.AetherochemicalLaser1)
        {
            Clear();
            _source = caster;
            _rays[(uint)AID.AetherochemicalLaser2] = (spell.Rotation - 45.Degrees(), finish.AddSeconds(4), false);
            _rays[(uint)AID.AetherochemicalLaser3] = (spell.Rotation + 45.Degrees(), finish.AddSeconds(4), false);
        }
        else if (_source != caster)
        {
            Clear(); // loading midway supplies only the actual cast, not an invented missing sequence
            _source = caster;
        }
        _rays[action] = (spell.Rotation, finish, true);
        _resolved.Remove(action);
        _activeCast = action;
        if (action != (uint)AID.AetherochemicalLaser1)
            foreach (var (id, ray) in _rays.ToArray())
                if (id != action && !ray.Started)
                    _rays[id] = (ray.Rotation, finish.AddSeconds(4), false);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == _source && IsRay(spell.Action.ID))
        {
            ++NumCasts;
            _resolved.Add(spell.Action.ID);
            _rays.Remove(spell.Action.ID);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster != _source || !IsRay(spell.Action.ID))
            return;
        if (!_resolved.Contains(spell.Action.ID))
            Clear(); // cancellation withdraws the unconfirmed continuation
        else if (_activeCast == spell.Action.ID)
            _activeCast = 0;
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == _source)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _source)
            Clear();
    }

    public override void Update()
    {
        foreach (var (id, ray) in _rays.ToArray())
            if (WorldState.CurrentTime > ray.Activation.AddSeconds(1))
                _rays.Remove(id);
        if (_source is { IsDeadOrDestroyed: true } || _rays.Count == 0)
            Clear();
    }

    private void Clear()
    {
        _rays.Clear();
        _resolved.Clear();
        _source = null;
        _activeCast = 0;
    }
}
