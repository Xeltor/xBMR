namespace BossMod.RealmReborn.Alliance.A22Glasya;

class Aura(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Aura, 8);
class VileUtterance(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VileUtterance, new AOEShapeCone(64, 22.5f.Degrees()));
// No cast bar: preview the current recipient's frontal continuously between casts.
// Action2346 is range8 plus the boss's4y hitbox, with the90-degree fan omen.
class AstralPunch(BossModule module) : Components.Cleave(module, (uint)AID.AstralPunch, new AOEShapeCone(12, 45.Degrees()), activeWhileCasting: false)
{
    public override List<(Actor origin, Actor target, Angle angle)> OriginsAndTargets()
        => base.OriginsAndTargets().Where(e => e.origin.InCombat && !e.origin.IsDeadOrDestroyed
            && !e.target.IsDeadOrDestroyed && e.target.Type == ActorType.Player).ToList();
}

class ClockworkWrights(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.ClockworkWright).Any(a => !a.IsDeadOrDestroyed))
            hints.Add("Quick kill: focus Glasya, ignore Clockwork Wrights and avoid their electric fields");
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
