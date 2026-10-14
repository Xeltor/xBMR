namespace BossMod.Heavensward.Alliance.A34Scathach;

class ThirtyThorns4(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ThirtyThorns4, 8)
{
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == (uint)AID.ThirtyThorns1)
        {
            Casters.Clear();
            Casters.Add(new(Shape, caster.Position, activation: WorldState.FutureTime(4.4d), actorID: caster.InstanceID));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.Clear();
        base.OnCastStarted(caster, spell);
    }

    public override void Update()
    {
        for (var i = 0; i < Casters.Count; ++i)
        {
            var aoe = Casters[i];
            if (aoe.ActorID == Module.PrimaryActor.InstanceID)
            {
                aoe.Origin = Module.PrimaryActor.Position;
                Casters[i] = aoe;
            }
        }
        Casters.RemoveAll(a => a.Activation.AddSeconds(2d) < WorldState.CurrentTime);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}

class Shadesmite1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Shadesmite1, 15)
{
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == (uint)AID.Soar)
        {
            Casters.Clear();
            Casters.Add(new(Shape, new(spell.TargetPos), activation: WorldState.FutureTime(4.9d), actorID: caster.InstanceID));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.Clear();
        base.OnCastStarted(caster, spell);
    }

    public override void Update() => Casters.RemoveAll(a => a.Activation.AddSeconds(2d) < WorldState.CurrentTime);
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}
