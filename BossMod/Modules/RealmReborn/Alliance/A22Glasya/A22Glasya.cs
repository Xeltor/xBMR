namespace BossMod.RealmReborn.Alliance.A22Glasya;

class Aura(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Aura, 8);
class VileUtterance(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VileUtterance, new AOEShapeCone(64, 22.5f.Degrees()));

class ClockworkWrights(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.ClockworkWright).Any(a => !a.IsDeadOrDestroyed))
            hints.Add("Avoid Clockwork Wright stun fields; interceptors handle the power tethers");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Do not make ordinary combat movement approach an inactive Wright and enter its stun field.
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID == (uint)OID.ClockworkWright)
                enemy.Priority = AIHints.Enemy.PriorityUndesirable;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 102, NameID = 2815)]
public class A22Glasya(WorldState ws, Actor primary) : BossModule(ws, primary, new(0, -200), new ArenaBoundsCircle(35));
