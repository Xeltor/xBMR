namespace BossMod.RealmReborn.Quest.MSQ.SpiritholdBroken;

public enum OID : uint
{
    Boss = 0x225, // Clay Golem, R2.2
}

public enum AID : uint
{
    TrueGrit = 445, // range 6 + caster radius, 120-degree cone
    Plaincracker = 482, // range 6 + caster radius circle
}

sealed class TrueGrit(BossModule module) : Components.SimpleAOEs(module, (uint)AID.TrueGrit, new AOEShapeCone(8.2f, 60f.Degrees()));
sealed class Plaincracker(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Plaincracker, 8.2f);

sealed class SpiritholdBrokenStates : StateMachineBuilder
{
    public SpiritholdBrokenStates(BossModule module) : base(module)
    {
        TrivialPhase().ActivateOnEnter<TrueGrit>().ActivateOnEnter<Plaincracker>();
    }
}

// Planning envelope pending independent terrain verification; see implementation notes.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65665, NameID = 30)]
public sealed class SpiritholdBroken(WorldState ws, Actor primary) : BossModule(ws, primary, new(251f, 191f), new ArenaBoundsCircle(20f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    protected override bool CheckPull() => WorldState.CurrentZone == 225 && WorldState.CurrentCFCID == 297 && base.CheckPull();
}
