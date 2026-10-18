namespace BossMod.RealmReborn.Alliance.A24Xande;

class KnucklePress(BossModule module) : Components.SimpleAOEs(module, (uint)AID.KnucklePress, 10);
class BurningRave1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BurningRave1, 8);
class BurningRave2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BurningRave2, 8);
class AncientQuake(BossModule module) : Components.RaidwideCast(module, (uint)AID.AncientQuake);
class AuraCannon(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AuraCannon, new AOEShapeRect(64, 5));

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 102, NameID = 2824, ConfigType = typeof(SyrcusConfig))]
public class A24Xande(WorldState ws, Actor primary) : BossModule(ws, primary, new(-400, -200), new ArenaBoundsCircle(35))
{
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var small = Enemies((uint)OID.StonefallCircle);
        var anySmall = small.Any(a => !a.IsDeadOrDestroyed);
        var group = SyrcusAlliance.Group(Raid, slot);
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.OID == (uint)OID.AetherialMine)
            {
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
                continue;
            }
            if (!target.InCombat || enemy.Priority < 0)
                continue;
            if (target.OID == (uint)OID.StonefallCircle)
            {
                // West/east pairs and the central column retain the same ownership in both waves.
                var owner = target.Position.X < Center.X - 10 ? 0 : target.Position.X > Center.X + 10 ? 2 : 1;
                enemy.Priority = group < 0 || owner == group ? 3 : 2;
            }
            else if (target.OID == (uint)OID.StarfallCircle)
                enemy.Priority = anySmall ? 0 : 2;
        }
    }
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.StonefallCircle));
        Arena.Actors(Enemies((uint)OID.StarfallCircle));
    }
}
