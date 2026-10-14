namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

class Blackbolt(BossModule module) : Components.StackWithCastTargets(module, (uint)AID.Blackbolt, 6, 8);

class JestersReap(BossModule module) : Components.SimpleAOEs(module, (uint)AID.JestersReap, new AOEShapeCone(13.4f, 60.Degrees()));
class JestersReward(BossModule module) : Components.SimpleAOEs(module, (uint)AID.JestersReward, new AOEShapeCone(31.4f, 90.Degrees()));

class JongleursX(BossModule module) : Components.SingleTargetCast(module, (uint)AID.JongleursX);
class JugglingSphere(BossModule module) : Components.ChargeAOEs(module, (uint)AID.JugglingSphere, 3);
class JugglingSphere2(BossModule module) : Components.ChargeAOEs(module, (uint)AID.JugglingSphere2, 3);

class LuckyPierrot2(BossModule module) : Components.ChargeAOEs(module, (uint)AID.LuckyPierrot2, 3);
class Icefall(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Icefall, 5);
class FerdiadsFools(BossModule module) : Components.Adds(module, (uint)OID.FerdiadsFool, 1);

class PetrifyingEye(BossModule module) : Components.CastGaze(module, (uint)AID.PetrifyingEye);




[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 5509)]
public class A32FerdiadHollow(WorldState ws, Actor primary) : BossModule(ws, primary, new(-350, 225), new ArenaBoundsCircle(30))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.FerdiadsFool));
    }
}
