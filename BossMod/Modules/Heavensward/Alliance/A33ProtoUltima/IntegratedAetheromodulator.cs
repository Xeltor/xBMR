namespace BossMod.Heavensward.Alliance.A33ProtoUltima;

// Action7582 -> Omen78 and the exact installed DecalRing asset establish donut topology,
// but not its world bounds. This is a timed cast warning, without an invented safe region.
class IntegratedAetheromodulator(BossModule module) : Components.CastHint(module, (uint)AID.IntegratedAetheromodulator,
    "Proto Bit donut: avoid the visible ring (bounds unverified)!", true)
{
    private readonly Dictionary<ulong, (Actor Actor, ActorCastInfo Spell, DateTime Expiry)> _casts = [];

    public override void OnActorCreated(Actor actor)
    {
        if (_casts.TryGetValue(actor.InstanceID, out var old) && old.Actor != actor)
            Remove(actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || caster.OID != (uint)OID.ProtoBit || WorldState.Actors.Find(caster.InstanceID) != caster || caster.CastInfo != spell)
            return;
        if (_casts.TryGetValue(caster.InstanceID, out var old) && old.Actor == caster && old.Spell == spell)
            return;
        Remove(caster.InstanceID);
        _casts[caster.InstanceID] = (caster, spell, Module.CastFinishAt(spell).AddSeconds(2));
        Casters.Add(caster);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster.InstanceID, out var pending) && pending.Actor == caster && pending.Spell == spell)
            Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction && _casts.TryGetValue(caster.InstanceID, out var pending) && pending.Actor == caster)
            Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_casts.TryGetValue(actor.InstanceID, out var pending) && pending.Actor == actor)
            Remove(actor.InstanceID);
    }

    public override void Update()
    {
        foreach (var (id, pending) in _casts.ToArray())
            if (pending.Actor.IsDeadOrDestroyed || WorldState.Actors.Find(id) != pending.Actor || pending.Actor.CastInfo != pending.Spell || WorldState.CurrentTime >= pending.Expiry)
                Remove(id); // Missing native cleanup must not leave a cast warning for the rest of the encounter.
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        // Distinct sources keep their actual native cast timers; the global countdown shows the next finish.
        Casters.Sort((a, b) => (a.CastInfo?.NPCRemainingTime ?? 0).CompareTo(b.CastInfo?.NPCRemainingTime ?? 0));
        base.AddGlobalHints(actor, hints);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var caster in Casters)
            Arena.TextWorld(caster.Position, $"Donut: {caster.CastInfo?.NPCRemainingTime ?? 0:f1}s (bounds unknown)", Colors.Object);
    }

    private void Remove(ulong id)
    {
        _casts.Remove(id);
        Casters.RemoveAll(c => c.InstanceID == id);
    }
}
