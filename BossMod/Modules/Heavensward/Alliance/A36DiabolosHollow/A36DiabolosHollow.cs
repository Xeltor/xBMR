namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

class Shadethrust(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Shadethrust, new AOEShapeRect(43, 2.5f));
class HollowCamisado(BossModule module) : Components.SingleTargetCast(module, (uint)AID.HollowCamisado);
class HollowNightmare(BossModule module) : Components.CastGaze(module, (uint)AID.HollowNightmare);
// Boss7202 is a non-damaging visual; helper7203 owns the actual deadline,
// about5s later. Keep the warning without predicting an extra damage instance.
class HollowOmen1(BossModule module) : Components.CastHint(module, (uint)AID.HollowOmen1, "Raidwide: Hollow Omen (delayed impact)");
class HollowOmen2(BossModule module) : Components.RaidwideCast(module, (uint)AID.HollowOmen2);







class Nox(BossModule module) : DunScaithNox(module, (uint)AID.NoxAOEFirst, (uint)AID.NoxAOERest);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 5526)]
public class A36DiabolosHollow(WorldState ws, Actor primary) : BossModule(ws, primary, new(-350, -445), new ArenaBoundsCircle(35))
{
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        PortalSafety.HoldAutomaticMovement(actor, hints, WorldState.CurrentTime);
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && (enemy.Actor.OID == (uint)OID.Deathgate || enemy.Actor.OID == (uint)OID.NightHound))
                enemy.Priority = 2;
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Deathgate));
        Arena.Actors(Enemies((uint)OID.DiabolicGate));
        Arena.Actors(Enemies((uint)OID.Shadowsphere));
        Arena.Actors(Enemies((uint)OID.NightHound));
    }
}
