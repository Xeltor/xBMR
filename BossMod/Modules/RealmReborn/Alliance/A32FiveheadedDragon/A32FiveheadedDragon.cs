namespace BossMod.RealmReborn.Alliance.A32FiveheadedDragon;

class WhiteBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.WhiteBreath, new AOEShapeCone(30, 60.Degrees()));
class BreathOfFire(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BreathOfFire, 6);
class BreathOfLight(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BreathOfLight, 6);
class BreathOfPoison(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BreathOfPoison, 6);
class BreathOfIce(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BreathOfIce, 6);

class Radiance(BossModule module) : Components.RaidwideCast(module, (uint)AID.Radiance);
class Discordance(BossModule module) : Components.RaidwideCast(module, (uint)AID.Discordance);
class HeatWave(BossModule module) : Components.RaidwideCast(module, (uint)AID.HeatWave);
class AutoattackPositioning(BossModule module) : Components.FrontalPositioning(module, 6)
{
    protected override bool AppliesToActor(Actor actor) => base.AppliesToActor(actor)
        && actor.FindStatus(SID.DeepFreeze) == null && actor.FindStatus(SID.Pyretic) == null
        && actor.FindStatus(SID.Prey1) == null && actor.FindStatus(SID.Prey2) == null
        && actor.FindStatus(SID.PoisonResistanceUp) == null
        && Module.FindComponent<HeatWavePreparation>()?.Active != true
        && !(Module.FindComponent<Markers>() is { } markers && (markers.Stacks.Count != 0 || markers.Spreads.Count != 0));
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3227)]
public class A32FiveheadedDragon(WorldState ws, Actor primary) : BossModule(ws, primary, new(200, 179), new ArenaBoundsCustom([new Polygon(new(200, 179), 30, 32)]))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.HeadOfFire));
        Arena.Actors(Enemies((uint)OID.HeadOfPoison));
        Arena.Actors(Enemies((uint)OID.HeadOfThunder));
        Arena.Actors(Enemies((uint)OID.HeadOfIce));
        Arena.Actors(Enemies((uint)OID.Prominence));
        Arena.Actors(Enemies((uint)OID.PoisonSlime));
        Arena.Actors(Enemies((uint)OID.ToxicSlime));
        Arena.Actors(Enemies((uint)OID.DragonfireFly));
    }
}
