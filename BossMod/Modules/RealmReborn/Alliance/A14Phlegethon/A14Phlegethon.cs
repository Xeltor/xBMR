namespace BossMod.RealmReborn.Alliance.A14Phlegethon;

class MegiddoFlame2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MegiddoFlame2, 3);
class MegiddoFlame3(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MegiddoFlame3, 4);
class MegiddoFlame4(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MegiddoFlame4, 5);
class MegiddoFlame5(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MegiddoFlame5, 6);
class MoonfallSlash(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MoonfallSlash, new AOEShapeCone(15, 60.Degrees()));
class VacuumSlash2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VacuumSlash2, new AOEShapeCone(80, 22.5f.Degrees()));
class QuakeIII(BossModule module) : Components.RaidwideCast(module, (uint)AID.QuakeIII);
// The exact half-ring geometry is not established by the retained Action range alone.
class AbyssalSlash(BossModule module) : Components.CastHint(module, (uint)AID.AbyssalSlash1, "Abyssal Slash: avoid the half-ring telegraphs", true);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 732, ConfigType = typeof(LabyrinthConfig))]
public class A14Phlegethon(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    private static readonly ArenaBoundsCustom arena = new([new Circle(new(-110, 181.6f), 40),
        new Circle(AncientFlare.Platforms[0], 5), new Circle(AncientFlare.Platforms[1], 5), new Circle(AncientFlare.Platforms[2], 5)]);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.InCombat && enemy.Priority >= 0)
                enemy.Priority = enemy.Actor.OID switch { (uint)OID.IronClaws => 3, (uint)OID.IronGiant => 2, _ => enemy.Priority };
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.IronClaws));
        Arena.Actors(Enemies((uint)OID.IronGiant));
    }
}
