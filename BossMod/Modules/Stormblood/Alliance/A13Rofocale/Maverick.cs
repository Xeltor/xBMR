namespace BossMod.Stormblood.Alliance.A13Rofocale;

class Maverick(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Maverick1, (uint)AID.Maverick2, (uint)AID.Maverick3], new AOEShapeRect(60f, 8f))
{
    private readonly List<(Actor Caster, DateTime Activation)> _previews = [];
    private readonly List<AOEInstance> _visible = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _visible.Clear();
        foreach (var aoe in base.ActiveAOEs(slot, actor))
            _visible.Add(aoe with { Color = Colors.Danger });
        foreach (var (caster, activation) in _previews)
        {
            // Some cues precede the teleport, or play while Rofocale is returning to the arena.
            var inward = Arena.Center - caster.Position;
            if (inward.LengthSq() is >= 27f * 27f and <= 32f * 32f && inward.Normalized().Dot(caster.Rotation.ToDirection()) >= 0.9f)
                _visible.Add(new(Shape, caster.Position, caster.Rotation, activation, Colors.AOE, actorID: caster.InstanceID));
        }
        return CollectionsMarshal.AsSpan(_visible);
    }

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (id == 7747 && (actor.OID == (uint)OID.Rofocale2 || actor.OID == (uint)OID.Boss && !actor.IsTargetable)
            && !_previews.Any(p => p.Caster == actor) && !Casters.Any(aoe => aoe.ActorID == actor.InstanceID))
            _previews.Add((actor, WorldState.FutureTime(6.5d)));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (AIDs.Contains(spell.Action.ID))
            _previews.RemoveAll(p => p.Caster == caster);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (AIDs.Contains(spell.Action.ID))
        {
            _previews.RemoveAll(p => p.Caster == caster);
            Casters.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
        }
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _previews.Clear();
            Casters.Clear();
        }
        else
        {
            _previews.RemoveAll(p => p.Caster == actor);
            Casters.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
        }
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorTargetable(Actor actor) => _previews.RemoveAll(p => p.Caster == actor);

    public override void Update() => _previews.RemoveAll(p => WorldState.CurrentTime > p.Activation.AddSeconds(0.75d));
}
