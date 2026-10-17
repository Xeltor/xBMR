namespace BossMod.RealmReborn.Quest.Job.Lancer.ADangerousProposition;

public enum OID : uint
{
    Foulques = 0x666, // friendly encounter anchor; survives individual wolf deaths
    AlphaWolf = 0x21F, // R2.2
    PackWolf = 0x599, // R1.65
}

public enum AID : uint
{
    NeckRip = 975, // 2.5s corrected cast, range 3+R width 2 rectangle
    NerveSever = 999, // 2.5s corrected cast, range 3+R width 2 rectangle, paralysis
}

sealed class NeckRip(BossModule module) : Components.SimpleAOEs(module, (uint)AID.NeckRip, new AOEShapeRect(4.65f, 1f));
sealed class NerveSever(BossModule module) : Components.SimpleAOEs(module, (uint)AID.NerveSever, new AOEShapeRect(5.2f, 1f));

sealed class ADangerousPropositionStates : StateMachineBuilder
{
    public ADangerousPropositionStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Defeat the alpha wolf"), "Wolves")
            .ActivateOnEnter<NeckRip>()
            .ActivateOnEnter<NerveSever>()
            .Raw.Update = () => Module.PrimaryActor.IsDestroyed || AllDeadOrDestroyed([(uint)OID.AlphaWolf]);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 307, NameID = 608, PrimaryActorOID = (uint)OID.Foulques)]
public sealed class ADangerousProposition(WorldState ws, Actor primary) : BossModule(ws, primary, new(305f, -285f), new ArenaBoundsRect(30f, 30f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    // Provisional planning envelope, not a measured floor perimeter; see implementation notes.
    protected override bool CheckPull() => WorldState.CurrentZone == 237 && WorldState.CurrentCFCID == 307 && IsAnyActorInCombat([(uint)OID.AlphaWolf, (uint)OID.PackWolf]);

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.AlphaWolf, (uint)OID.PackWolf]);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
        {
            var enemy = target.Actor;
            if (target.Priority < 0 || !enemy.IsTargetable || enemy.IsDeadOrDestroyed || enemy.IsAlly)
                continue;
            target.Priority = enemy.OID switch
            {
                (uint)OID.AlphaWolf => 2, // quest victory condition
                (uint)OID.PackWolf => 1,
                _ => target.Priority
            };
        }
    }
}
