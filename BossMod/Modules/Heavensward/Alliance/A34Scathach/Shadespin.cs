namespace BossMod.Heavensward.Alliance.A34Scathach;

// The boss's four-second visual identifies the diagonal pair before the helpers' short casts.
class Shadespin(BossModule module) : Components.GenericAOEs(module, (uint)AID.Shadespin2)
{
    private readonly List<AOEInstance> _aoes = [];
    private static readonly AOEShapeCone Shape = new(30, 45.Degrees());
    private bool _visualResolved;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Shadespin or (uint)AID.Shadespin1)
        {
            _aoes.Clear();
            _visualResolved = false;
            var rotation = spell.Rotation + (spell.Action.ID == (uint)AID.Shadespin ? 45 : -45).Degrees();
            // Observed helper hits are 1.16-1.20s after the visual finish. Keep a small early margin;
            // this is not a measured server snapshot or permission to greed until the damage event.
            var activation = Module.CastFinishAt(spell, 1.1d);
            _aoes.Add(new(Shape, caster.Position, rotation, activation, actorID: caster.InstanceID));
            _aoes.Add(new(Shape, caster.Position, rotation + 180.Degrees(), activation, actorID: caster.InstanceID));
        }
        else if (spell.Action.ID == WatchedAction)
        {
            // Replace just the matching prediction; callbacks for the opposite helper may arrive later.
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID
                || a.ActorID == Module.PrimaryActor.InstanceID && Math.Abs((a.Rotation - spell.Rotation).Normalized().Rad) < 0.1f);
            _aoes.Add(new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Shadespin or (uint)AID.Shadespin1)
            _visualResolved = true;
        else if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        else if (spell.Action.ID is (uint)AID.Shadespin or (uint)AID.Shadespin1 && !_visualResolved
            && !_aoes.Exists(a => a.ActorID != caster.InstanceID))
            _aoes.Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _aoes.Clear();
        else
            _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    }

    public override void Update() => _aoes.RemoveAll(a => a.Activation.AddSeconds(2d) < WorldState.CurrentTime);
}
