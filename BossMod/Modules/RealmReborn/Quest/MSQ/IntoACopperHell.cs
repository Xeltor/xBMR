namespace BossMod.RealmReborn.Quest.MSQ.IntoACopperHell;

public enum OID : uint
{
    Boss = 0x3C0, // Filthy Bodyguard
    BoastfulBodyguard = 0x3C1,
    RaucousBodyguard = 0x3C2,
}

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
                OID.RaucousBodyguard => 3,
                OID.Boss => 2,
                OID.BoastfulBodyguard => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class IntoACopperHellStates : StateMachineBuilder
{
    public IntoACopperHellStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Defeat the bodyguards"), "Bodyguards")
            .ActivateOnEnter<TargetPriority>()
            .Raw.Update = () => AllDeadOrDestroyed([(uint)OID.Boss, (uint)OID.BoastfulBodyguard, (uint)OID.RaucousBodyguard]);
    }
}

// This is the post-dungeon solo battle in Ul'dah, not Copperbell Mines.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66196, NameID = 991)]
public sealed class IntoACopperHell(WorldState ws, Actor primary) : BossModule(ws, primary, new(39f, -145f), new ArenaBoundsCircle(15f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    protected override bool CheckPull() => WorldState.CurrentZone == 274 && WorldState.CurrentCFCID == 338
        && (IsAnyActorInCombat((uint)OID.Boss) || IsAnyActorInCombat((uint)OID.BoastfulBodyguard) || IsAnyActorInCombat((uint)OID.RaucousBodyguard));

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.Boss));
        Arena.Actors(Enemies((uint)OID.BoastfulBodyguard));
        Arena.Actors(Enemies((uint)OID.RaucousBodyguard));
    }
}
