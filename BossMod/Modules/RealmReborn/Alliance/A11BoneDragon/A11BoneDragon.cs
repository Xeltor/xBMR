namespace BossMod.RealmReborn.Alliance.A11BoneDragon;

class Apocalypse(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Apocalypse, 6);
class EvilEye(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EvilEye, new AOEShapeCone(105, 60.Degrees()));
class Stone(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Stone);
class Level5Petrify(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Level5Petrify, new AOEShapeCone(7.8f, 45.Degrees()));

class Platinals(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.Platinal).Any(a => !a.IsDeadOrDestroyed))
            hints.Add("Kill skeletons apart; stop revived skeletons reaching the dragon");
        hints.Add("Use raised platforms when the lower floor fills with poison");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.InCombat && enemy.Priority >= 0)
                enemy.Priority = enemy.Actor.OID switch { (uint)OID.Platinal => 3, (uint)OID.Boss => 1, (uint)OID.RottingEye => 0, _ => enemy.Priority };
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 706)]
public class A11BoneDragon(WorldState ws, Actor primary) : BossModule(ws, primary, new(-451.2f, 23.93f), new ArenaBoundsCircle(49.8f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Platinal));
        Arena.Actors(Enemies((uint)OID.RottingEye));
    }
}
