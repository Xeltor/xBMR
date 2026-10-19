namespace BossMod.RealmReborn.Alliance.A21Scylla;

class Topple(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Topple, new AOEShapeCircle(6.75f));
class SearingChain(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SearingChain, new AOEShapeRect(61, 2));
class InfiniteAnguish(BossModule module) : Components.SimpleAOEs(module, (uint)AID.InfiniteAnguish, new AOEShapeDonut(6, 12));
class AncientFlare(BossModule module) : Components.RaidwideCast(module, (uint)AID.AncientFlare);
class FirewalkerPositioning(BossModule module) : Components.FrontalPositioning(module, 5)
{
    protected override bool AppliesToActor(Actor actor) => base.AppliesToActor(actor)
        && actor.FindStatus(SID.DeepFreeze) == null && actor.FindStatus(SID.DeepFreezeLegacy) == null
        && !WorldState.Actors.Any(a => !a.IsDeadOrDestroyed && a.Tether.Target == actor.InstanceID
            && a.OID is (uint)OID.ShudderingSoul or (uint)OID.ShiveringSoul or (uint)OID.SmolderingSoul);
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 102, NameID = 2809, ConfigType = typeof(SyrcusConfig))]
public class A21Scylla(WorldState ws, Actor primary) : BossModule(ws, primary, new(0, -192), new ArenaBoundsCircle(35))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.StaffOfEldering));
        Arena.Actors(Enemies((uint)OID.ShudderingSoul));
        Arena.Actors(Enemies((uint)OID.ShiveringSoul));
        Arena.Actors(Enemies((uint)OID.SmolderingSoul));
    }
}
