namespace BossMod.RealmReborn.Alliance.A3HowlingAtomos;

public enum OID : uint { Boss = 0xDA4, AtomosPrime = 0xE6E, SacrificedSoldier = 0xDA5, MoonringBlade = 0xBD8 }
class WhiteHole(BossModule module) : Components.SimpleAOEs(module, 3415, new AOEShapeCone(19, 60.Degrees()))
{
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(c => c.ActorID == actor.InstanceID);
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction)
        {
            var aoe = Casters[^1];
            aoe.ArenaProjectionLayer = Module.ResolveArenaProjectionLayer(caster);
            Casters[^1] = aoe;
        }
    }
}
class Platforms(BossModule module) : BossComponent(module)
{
    private readonly HashSet<Actor> _shockwaves = [];
    public override void OnCastStarted(Actor caster, ActorCastInfo spell) { if (spell.Action.ID == 3416) _shockwaves.Add(caster); }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { if (spell.Action.ID == 3416) _shockwaves.Remove(caster); }
    public override void OnActorDestroyed(Actor actor) => _shockwaves.Remove(actor);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_shockwaves.Any(c => Module.ActorsMatchArenaProjectionLayer(actor,c)))
            hints.Add("Shockwave: stand directly under Atomos in the purple circle!");
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_shockwaves.Any(c => !c.IsDeadOrDestroyed && c.CastInfo?.Action.ID == 3416 && Module.ActorsMatchArenaProjectionLayer(actor, c))
            && Module.Enemies((uint)OID.SacrificedSoldier).Any(s => !s.IsDeadOrDestroyed && s.InCombat && Module.ActorsMatchArenaProjectionLayer(actor, s)))
            hints.Add("Kill or stun the soldier to clear the Shockwave stack position!");
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var caster in _shockwaves)
            if (!caster.IsDeadOrDestroyed && caster.CastInfo?.Action.ID == 3416 && Module.ActorsMatchArenaProjectionLayer(pc, caster))
            {
                using (Arena.WorldProjectionLayer(Module.ResolveArenaProjectionLayer(caster), true))
                {
                    Arena.AddCircleUnfilled(caster.Position, 1, Colors.Safe, 3);
                    Arena.AddLine(pc.Position, caster.Position, Colors.Safe, 2);
                }
            }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var layer = Module.ResolveArenaProjectionLayer(actor);
        foreach (var caster in _shockwaves)
            if (!caster.IsDeadOrDestroyed && caster.CastInfo is { } cast && Module.ActorsMatchArenaProjectionLayer(actor,caster))
                // One yard is an under-boss arrival tolerance, not a claimed knockback-immunity radius.
                hints.AddForbiddenZone(new SDInvertedCircle(caster.Position, 1), Module.CastFinishAt(cast,-0.5), arenaProjectionLayer: layer);
        foreach (var target in hints.PotentialTargets)
        {
            if (!Module.ActorsMatchArenaProjectionLayer(actor,target.Actor))
                target.Priority = AIHints.Enemy.PriorityUndesirable;
            else if (target.Actor.InCombat && target.Priority>=0)
                target.Priority = target.Actor.OID switch
                {
                    (uint)OID.SacrificedSoldier => 3,
                    (uint)OID.Boss => 1,
                    _ => target.Priority
                };
            if (target.Actor.OID == (uint)OID.SacrificedSoldier && target.Priority >= 0 && target.Actor.InCombat
                && target.Actor.IsTargetable && !target.Actor.IsDeadOrDestroyed
                && _shockwaves.Any(c => !c.IsDeadOrDestroyed && c.CastInfo?.Action.ID == 3416 && Module.ActorsMatchArenaProjectionLayer(target.Actor, c)))
            {
                // Request native control/kill priority without erasing the real
                // frontal hazard or claiming that a stun has already succeeded.
                target.Priority = 5;
                target.ShouldBeStunned = true;
            }
        }
    }
}
class A3HowlingAtomosStates : StateMachineBuilder
{
    public A3HowlingAtomosStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<Platforms>().ActivateOnEnter<WhiteHole>()
            .ActivateOnEnter<BlackHole>().ActivateOnEnter<JumpPads>()
            .ActivateOnEnter<AstralPunch>().ActivateOnEnter<Aura>().ActivateOnEnter<HuntersMoon>()
            .Raw.Update = () => Module.Enemies((uint)OID.AtomosPrime).Any() && Module.Enemies((uint)OID.AtomosPrime).All(a => a.IsDeadOrDestroyed);
    }
}
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3380)]
public class A3HowlingAtomos(WorldState ws, Actor primary) : BossModule(ws, primary, PlatformsArena.Bounds.Center, PlatformsArena.Bounds)
{
    public static readonly WPos[] PlatformCenters = [new(-31.177f,49),new(0,-5),new(31.177f,49)];
    public static int Group(Actor actor) => Enumerable.Range(0,3).MinBy(i=>(actor.Position-PlatformCenters[i]).LengthSq());
    protected override bool CheckPull() => Enemies((uint)OID.Boss).Any(a=>a.InCombat && !a.IsDeadOrDestroyed);
    public override int? ResolveArenaProjectionLayer(Actor actor)
    {
        var group=Group(actor);
        return group+(actor.PosRot.Y<93 ? 3 : 0);
    }
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach(var enemy in WorldState.Actors)
            if (enemy.Type==ActorType.Enemy && !enemy.IsDeadOrDestroyed && ActorsMatchArenaProjectionLayer(pc,enemy))
                Arena.Actor(enemy);
    }
}
