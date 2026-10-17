namespace BossMod.RealmReborn.Quest.MSQ.LeiasLegacy;

public enum OID : uint
{
    Boss = 0x98, // Janremi Blackheart, R0.55
    HedgeKnightArcher = 0x99,
    HedgeKnightHealer = 0x9A,
    KupluKopo = 0x3A4,
}

public enum AID : uint
{
    Heartstopper = 866, // range 3 + caster radius, width 3 rectangle
}

sealed class Heartstopper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Heartstopper, new AOEShapeRect(3.55f, 1.5f));

sealed class TargetPriority(BossModule module) : BossComponent(module)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (enemy.Priority < 0 || target.IsAlly || !target.IsTargetable || target.IsDeadOrDestroyed)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.HedgeKnightHealer => 3,
                OID.HedgeKnightArcher => 2,
                OID.Boss => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class LeiasLegacyStates : StateMachineBuilder
{
    public LeiasLegacyStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Recover Leia's egg"), "Thieves")
            .ActivateOnEnter<Heartstopper>().ActivateOnEnter<TargetPriority>()
            .Raw.Update = () => AllDeadOrDestroyed([(uint)OID.Boss, (uint)OID.HedgeKnightArcher, (uint)OID.HedgeKnightHealer]);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65697, NameID = 185)]
public sealed class LeiasLegacy(WorldState ws, Actor primary) : BossModule(ws, primary, new(-177f, 290f), new ArenaBoundsCircle(20f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    // The boss can die before his surviving accomplices; completion checks all three OIDs.
    protected override bool CheckPull() => WorldState.CurrentZone == 227 && WorldState.CurrentCFCID == 299
        && (IsAnyActorInCombat((uint)OID.Boss) || IsAnyActorInCombat((uint)OID.HedgeKnightArcher) || IsAnyActorInCombat((uint)OID.HedgeKnightHealer));

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.Boss));
        Arena.Actors(Enemies((uint)OID.HedgeKnightArcher));
        Arena.Actors(Enemies((uint)OID.HedgeKnightHealer));
    }
}
