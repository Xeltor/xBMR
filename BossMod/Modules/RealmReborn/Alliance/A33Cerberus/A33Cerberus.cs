namespace BossMod.RealmReborn.Alliance.A33Cerberus;

class TailBlow(BossModule module) : Components.SimpleAOEs(module, (uint)AID.TailBlow, new AOEShapeCone(19.8f, 45.Degrees()), arenaProjectionLayer: 0);
class Slabber(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Slabber, 8, arenaProjectionLayer: 0);
class SulphurousBreath1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SulphurousBreath1, new AOEShapeRect(35.8f, 3), arenaProjectionLayer: 0);
class SulphurousBreath2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SulphurousBreath2, new AOEShapeRect(40.5f, 3), arenaProjectionLayer: 0);
class LightningBolt2(BossModule module) : Components.ChargeAOEs(module, (uint)AID.LightningBolt2, 2, arenaProjectionLayer: 0);
class HoundOutOfHell(BossModule module) : Components.ChargeAOEs(module, (uint)AID.HoundOutOfHell, 7, arenaProjectionLayer: 0);
class Ululation(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ululation, arenaProjectionLayer: 0);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3224)]
public class A33Cerberus(WorldState ws, Actor primary) : BossModule(ws, primary, CerberusArena.Bounds.Center, CerberusArena.Bounds)
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        if (!BellyAndChains.Inside(pc))
            Arena.Actor(PrimaryActor);
        if (!BellyAndChains.Inside(pc))
            Arena.Actors(Enemies((uint)OID.GastricJuice));
        if (BellyAndChains.Inside(pc))
            Arena.Actors(Enemies((uint)OID.StomachWall));
        if (!BellyAndChains.Inside(pc))
            Arena.Actors(Enemies((uint)OID.Wolfsbane));
    }
}
