namespace BossMod.Heavensward.Alliance.A34Scathach;

// Guides establish opposite close/away responses, but not exact NPC boundaries.
// Actual casts supply actionable instructions without a false full-room hazard.
abstract class HoundVoice(BossModule module, uint aid, string instruction) : Components.CastHint(module, aid, instruction)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.ShadowcourtHound && spell.Action.ID == WatchedAction && !Casters.Contains(caster))
        {
            Casters.RemoveAll(c => c.InstanceID == caster.InstanceID);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Casters.Remove(caster);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.Remove(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        Casters.RemoveAll(c => c.IsDestroyed || WorldState.Actors.Find(c.InstanceID) != c);
        base.AddGlobalHints(actor, hints);
    }
}

class TheRamsVoice(BossModule module) : HoundVoice(module, (uint)AID.TheRamsVoice, "The Ram's Voice: move away from the hound or interrupt!");
class TheDragonsVoice(BossModule module) : HoundVoice(module, (uint)AID.TheDragonsVoice, "The Dragon's Voice: move close to the hound or interrupt!");

//3341 shares the exact range/cast/120-degree omen with existing3340/3342.
// Retain the existing6+hound-radius model and independent per-source lifetime.
abstract class MarrowDrain(BossModule module, uint aid) : Components.SimpleAOEs(module, aid, new AOEShapeCone(10.44f, 60.Degrees()))
{
    private readonly Dictionary<ulong, (Actor Actor, ActorCastInfo Cast)> _sources = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.ShadowcourtHound || spell.Action.ID != WatchedAction)
            return;
        if (_sources.TryGetValue(caster.InstanceID, out var previous) && previous.Actor == caster && previous.Cast == spell)
            return;
        Casters.RemoveAll(a => a.ActorID == caster.InstanceID);
        _sources[caster.InstanceID] = (caster, spell);
        base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_sources.TryGetValue(caster.InstanceID, out var current) && current.Actor == caster && current.Cast == spell)
            Remove(caster);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Remove(caster);
    }

    public override void OnActorDestroyed(Actor actor) => Remove(actor);

    private void Remove(Actor actor)
    {
        if (_sources.TryGetValue(actor.InstanceID, out var current) && current.Actor == actor)
        {
            Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
            _sources.Remove(actor.InstanceID);
        }
    }
}

class MarrowDrain1(BossModule module) : MarrowDrain(module, (uint)AID.MarrowDrain1);
class MarrowDrain2(BossModule module) : MarrowDrain(module, (uint)AID.MarrowDrain2);
class MarrowDrain3(BossModule module) : MarrowDrain(module, (uint)AID.MarrowDrain3);
