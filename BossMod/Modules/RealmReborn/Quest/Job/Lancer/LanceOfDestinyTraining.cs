namespace BossMod.RealmReborn.Quest.Job.Lancer.LanceOfDestinyTraining;

public enum OID : uint
{
    SoftSpokenLancer = 0x220,
    GrizzledLancer = 0x221,
    SharpEyedLancer = 0x222,
}

public enum AID : uint
{
    Heartstopper = 866, // 2.5s corrected cast, range 3+R width 3 rectangle
}

sealed class Heartstopper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Heartstopper, new AOEShapeRect(3.5f, 1.5f));

sealed class LanceOfDestinyTrainingStates : StateMachineBuilder
{
    public LanceOfDestinyTrainingStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Defeat the three lancers"), "Guild trial")
            .ActivateOnEnter<Heartstopper>()
            .Raw.Update = () => Module.Enemies((uint)OID.SharpEyedLancer).Count != 0 && AllDeadOrDestroyed([(uint)OID.SharpEyedLancer]);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 308, NameID = 609, PrimaryActorOID = (uint)OID.SoftSpokenLancer)]
public sealed class LanceOfDestinyTraining(WorldState ws, Actor primary) : BossModule(ws, primary, new(161.5f, -273.5f), new ArenaBoundsRect(9f, 6f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    // Keep the first opponent as an anchor after death/destruction: only the final
    // opponent ends the phase. Event NPC Ywain is ineligible for native registration.
    // The quest layout establishes east/west barriers; other room edges are unmeasured.
    protected override bool CheckPull() => WorldState.CurrentZone == 238 && WorldState.CurrentCFCID == 308 && IsAnyActorInCombat([(uint)OID.SoftSpokenLancer, (uint)OID.GrizzledLancer, (uint)OID.SharpEyedLancer]);

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.SoftSpokenLancer, (uint)OID.GrizzledLancer, (uint)OID.SharpEyedLancer]);
}
