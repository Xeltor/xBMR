namespace BossMod.RealmReborn.Alliance.A12Thanatos;

class BlackCloud(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BlackCloud, 6);
class Cloudscourge(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Cloudscourge, 6);
class VoidFireII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VoidFireII, 5);
// Astral Light grants the alliance buff; treating it as an AOE drove players away from their pot.
class AstralLight(BossModule module) : Components.CastHint(module, (uint)AID.AstralLight, "Magic Pot is granting Astral Realignment");
class CrepusculeBlade(BossModule module) : Components.CastHint(module, (uint)AID.CrepusculeBlade, "Frontal cleave - move behind Thanatos", true);

class AstralRealignment(BossModule module) : BossComponent(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var aligned = actor.FindStatus(SID.AstralRealignment) != null;
        hints.Add(aligned ? "Astral Realignment: attack Thanatos" : "No Astral Realignment: protect the Magic Pots and kill adds", false);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aligned = actor.FindStatus(SID.AstralRealignment) != null;
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.OID == (uint)OID.Boss && !aligned)
                enemy.Priority = AIHints.Enemy.PriorityInvincible;
            else if (target.InCombat && enemy.Priority >= 0)
                enemy.Priority = target.OID switch
                {
                    (uint)OID.Sandman => 3,
                    (uint)OID.Nemesis => aligned ? 0 : 2,
                    (uint)OID.Boss => 1,
                    _ => enemy.Priority
                };
        }
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 710)]
public class A12Thanatos(WorldState ws, Actor primary) : BossModule(ws, primary, new(440, 280), new ArenaBoundsCircle(30))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.MagicPot), Colors.Object);
        Arena.Actors(Enemies((uint)OID.Nemesis));
        Arena.Actors(Enemies((uint)OID.Sandman));
    }
}
