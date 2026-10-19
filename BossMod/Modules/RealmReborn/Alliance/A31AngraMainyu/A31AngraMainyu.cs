namespace BossMod.RealmReborn.Alliance.A31AngraMainyu;

class MortalGaze1(BossModule module) : Components.CastGaze(module, (uint)AID.MortalGaze1);
class MortalGaze2(BossModule module) : Components.CastGaze(module, (uint)AID.MortalGaze2);
class Stare(BossModule module) : Components.Cleave(module, (uint)AID.Stare, new AOEShapeRect(63.2f, 4), activeWhileCasting: false)
{
    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (var (origin, target, angle) in OriginsAndTargets())
            if (pc != target)
                Shape.Draw(Arena, origin.Position, angle);
    }
}
class EyesOnMe(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EyesOnMe, 31.8f);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 111, NameID = 3231)]
public class A31AngraMainyu(WorldState ws, Actor primary) : BossModule(ws, primary, new(-147.07f, 296.92f), new ArenaBoundsCustom([new Polygon(new(-147.07f, 296.92f), 30, 32)]))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.FinalHourglass));
        Arena.Actors(Enemies((uint)OID.GrimReaper));
        Arena.Actors(Enemies((uint)OID.AngraMainyusDaewa));
    }
}
