namespace BossMod.RealmReborn.Alliance.A3HowlingAtomos;

class AstralPunch(BossModule module) : Components.GenericBaitAway(module)
{
    public override void Update()
    {
        CurrentBaits.Clear();
        foreach (var soldier in Module.Enemies((uint)OID.SacrificedSoldier))
            if (!soldier.IsDeadOrDestroyed && soldier.InCombat && WorldState.Actors.Find(soldier.TargetID) is { IsDeadOrDestroyed: false, Type: ActorType.Player } target)
                CurrentBaits.Add(new(soldier, target, new AOEShapeCone(11, 45.Degrees()),
                    arenaProjectionLayer: Module.ResolveArenaProjectionLayer(soldier)));
    }
}

class Aura(BossModule module) : Components.GenericBaitAway(module, centerAtTarget: true)
{
    public override void Update()
    {
        CurrentBaits.Clear();
        // No cast bar: maintain separation from the current soldier recipient rather
        // than invent a precise ground lock or repeat timer from a short pull.
        foreach (var soldier in Module.Enemies((uint)OID.SacrificedSoldier))
            if (!soldier.IsDeadOrDestroyed && soldier.InCombat && WorldState.Actors.Find(soldier.TargetID) is { IsDeadOrDestroyed: false, Type: ActorType.Player } target)
            {
                // This is ongoing splash separation, not a known scheduled Aura cast.
                // It must not forbid Shockwave's required under-boss position.
                if (Module.Enemies((uint)OID.Boss).Any(b => Module.ActorsMatchArenaProjectionLayer(target, b) && b.CastInfo?.Action.ID == 3416))
                    continue;
                CurrentBaits.Add(new(soldier, target, new AOEShapeCircle(8), arenaProjectionLayer: Module.ResolveArenaProjectionLayer(target)));
            }
    }
}

class HuntersMoon(BossModule module) : Components.GenericAOEs(module, warningText: "Avoid the moving moonring blade!")
{
    private readonly Dictionary<Actor, DateTime> _predicted = [];
    private readonly List<AOEInstance> _aoes = [];
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 2345 && WorldState.Actors.Find(spell.MainTargetID) is { } helper)
            _predicted[helper] = WorldState.FutureTime(1.8);
    }
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.MoonringBlade)
        {
            var helper = _predicted.Keys.Where(p => p.Position.InCircle(actor.Position, 4) && Module.ActorsMatchArenaProjectionLayer(p, actor))
                .MinBy(p => (p.Position - actor.Position).LengthSq());
            if (helper != null)
                _predicted.Remove(helper);
        }
    }
    public override void OnActorDestroyed(Actor actor) => _predicted.Remove(actor);
    public override void Update()
    {
        foreach (var helper in _predicted.Where(p => p.Key.IsDeadOrDestroyed || WorldState.CurrentTime > p.Value.AddSeconds(3)).Select(p => p.Key).ToArray())
            _predicted.Remove(helper);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var (helper, activation) in _predicted)
            if (Module.ActorsMatchArenaProjectionLayer(actor, helper))
                _aoes.Add(new(new AOEShapeCircle(5), helper.Position, activation: activation, color: Colors.AOE, actorID: helper.InstanceID,
                    arenaProjectionLayer: Module.ResolveArenaProjectionLayer(helper), restrictToArenaProjectionLayer: true));
        foreach (var blade in Module.Enemies((uint)OID.MoonringBlade))
            if (!blade.IsDeadOrDestroyed && Module.ActorsMatchArenaProjectionLayer(actor, blade))
                _aoes.Add(new(new AOEShapeCircle(5), blade.Position, actorID: blade.InstanceID,
                    arenaProjectionLayer: Module.ResolveArenaProjectionLayer(blade), restrictToArenaProjectionLayer: true));
        return CollectionsMarshal.AsSpan(_aoes);
    }
}
