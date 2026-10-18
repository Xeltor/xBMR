namespace BossMod.RealmReborn.Alliance.A3TheEye;

public enum OID : uint { Boss = 0xD9C, TwoHeadedDragon = 0xD9D }
class MarrowDrain(BossModule module) : Components.SimpleAOEs(module, 3341, new AOEShapeCone(9.7f, 60.Degrees()));
class RamsVoice(BossModule module) : Components.SimpleAOEs(module, 3343, 9.7f);
class MeanThrash(BossModule module) : Components.SimpleAOEs(module, 3345, new AOEShapeCone(10, 60.Degrees()));
class BallOfFire(BossModule module) : Components.SimpleAOEs(module, 3347, 6);
class BallOfIce(BossModule module) : Components.SimpleAOEs(module, 3348, 6);
class SummoningCircle(BossModule module) : Components.SimpleAOEs(module, 3410, 14);
class DragonsVoice(BossModule module) : BossComponent(module)
{
    private readonly HashSet<Actor> _casters = [];
    public override void OnCastStarted(Actor caster, ActorCastInfo spell) { if (spell.Action.ID == 3344) _casters.Add(caster); }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { if (spell.Action.ID == 3344) _casters.Remove(caster); }
    public override void OnActorDestroyed(Actor actor) => _casters.Remove(actor);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_casters.Count != 0)
            hints.Add("Dragon's Voice: get under the casting Garm or far outside its range!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var caster in _casters)
            if (!caster.IsDeadOrDestroyed && caster.CastInfo is { } cast)
                // Inner damage boundary is unverified. One yard at the caster is a conservative
                // arrival goal supported by the guide's point-blank response, not a drawn donut.
                hints.AddForbiddenZone(new SDIntersection([new SDInvertedCircle(caster.Position, 1), new SDCircle(caster.Position, 30)]), Module.CastFinishAt(cast, -0.5));
        foreach (var target in hints.PotentialTargets)
            if (target.Actor.InCombat && target.Priority >= 0 && target.Actor.CastInfo is { Interruptible: true } cast && cast.Action.ID is 3343 or 3344)
                target.ShouldBeInterrupted = true;
    }
}
class A3TheEyeStates : StateMachineBuilder
{
    public A3TheEyeStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<MarrowDrain>().ActivateOnEnter<RamsVoice>().ActivateOnEnter<DragonsVoice>()
            .ActivateOnEnter<MeanThrash>().ActivateOnEnter<BallOfFire>().ActivateOnEnter<BallOfIce>().ActivateOnEnter<SummoningCircle>()
            .Raw.Update = () => Module.Enemies((uint)OID.TwoHeadedDragon).Any() && Module.Enemies((uint)OID.TwoHeadedDragon).All(a => a.IsDeadOrDestroyed) && Module.Enemies((uint)OID.Boss).All(a => a.IsDeadOrDestroyed);
    }
}
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3243)]
public class A3TheEye(WorldState ws, Actor primary) : BossModule(ws, primary, new(-77.06f, 382.94f), new ArenaBoundsCustom([new Polygon(new(-77.06f, 382.94f), 30.8f, 24)]))
{
    protected override bool CheckPull() => Enemies((uint)OID.Boss).Any(a => a.InCombat);
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.Boss));
        Arena.Actors(Enemies((uint)OID.TwoHeadedDragon));
    }
}
