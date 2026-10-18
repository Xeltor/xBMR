namespace BossMod.RealmReborn.Alliance.A3TheNape;

public enum OID : uint { Boss = 0xDA0, XandeClone = 0xDA3, SmallGate = 0xE73, LargeGate = 0xE74 }
class AncientFlare(BossModule module) : Components.RaidwideCast(module, 2347);
class AncientQuaga(BossModule module) : Components.RaidwideCast(module, 3412);
class AuraCannon(BossModule module) : Components.SimpleAOEs(module, 2358, new AOEShapeRect(63, 5));
class Gates(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.SmallGate).Concat(Module.Enemies((uint)OID.LargeGate)).Any(a => !a.IsDeadOrDestroyed))
            hints.Add("Destroy the forbidden gates before Void Call summons more enemies!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.InCombat && enemy.Priority >= 0)
            {
                enemy.Priority = enemy.Actor.OID switch { (uint)OID.LargeGate => 4, (uint)OID.SmallGate => 3, _ => enemy.Priority };
                if (enemy.Actor.CastInfo is { Interruptible: true } cast && cast.Action.ID is 2347 or 3412 or 3439)
                    enemy.ShouldBeInterrupted = true;
            }
    }
}
class A3TheNapeStates : StateMachineBuilder
{
    public A3TheNapeStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<AncientFlare>().ActivateOnEnter<AncientQuaga>().ActivateOnEnter<AuraCannon>().ActivateOnEnter<Gates>()
            .Raw.Update = () => Module.Enemies((uint)OID.XandeClone).Any() && Module.Enemies((uint)OID.XandeClone).All(a => a.IsDeadOrDestroyed);
    }
}
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3247)]
public class A3TheNape(WorldState ws, Actor primary) : BossModule(ws, primary, new(130, 265), new ArenaBoundsCustom([new Polygon(new(130, 265), 30.8f, 24)]))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var actor in WorldState.Actors)
            if (actor.Type == ActorType.Enemy && !actor.IsDeadOrDestroyed && actor.Position.InCircle(Center, 35))
                Arena.Actor(actor);
    }
}
