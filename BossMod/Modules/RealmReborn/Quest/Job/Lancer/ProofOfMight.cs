namespace BossMod.RealmReborn.Quest.Job.Lancer.ProofOfMight;

public enum OID : uint
{
    Thibain = 0x256, // R0.5; protected ally, stable encounter anchor
    Lindwurm = 0x59B, // R1.4
}

public enum AID : uint
{
    AutoAttack = 870,
    FrostBreath = 318, // 2.2s observed cast + native 0.3s correction; range 6 + hitbox, 90-degree cone
}

sealed class FrostBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.FrostBreath, new AOEShapeCone(7.4f, 45f.Degrees()));

sealed class ProtectThibain(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints) => hints.Add("Protect Thibain: defeat the lindwurms attacking him first.");

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (enemy.Priority < 0 || target.OID != (uint)OID.Lindwurm || !target.IsTargetable || target.IsDeadOrDestroyed || target.IsAlly)
                continue;

            // Finish a wounded attacker before switching to another one. Enemies already
            // attacking the player are less urgent than those killing the quest NPC.
            var threatensThibain = target.TargetID == Module.PrimaryActor.InstanceID;
            enemy.Priority = threatensThibain ? target.HPMP.CurHP < target.HPMP.MaxHP ? 3 : 2 : 1;
        }
    }
}

sealed class ProofOfMightStates : StateMachineBuilder
{
    public ProofOfMightStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Protect Thibain"), "Lindwurms")
            .ActivateOnEnter<FrostBreath>()
            .ActivateOnEnter<ProtectThibain>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed || AllDeadOrDestroyedInBounds([(uint)OID.Lindwurm]);
    }
}

// Anchor on the ally: individual lindwurm deaths must not unload the module.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65975, NameID = 130, PrimaryActorOID = (uint)OID.Thibain)]
public sealed class ProofOfMight(WorldState ws, Actor primary) : BossModule(ws, primary, new(-395f, -173f), new ArenaBoundsCircle(24f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    // Provisional central planning envelope inside the duty barriers, NOT a measured
    // physical arena edge. See docs/proof-of-might-lindwurms.md; keep WIP until mapped.
    protected override bool CheckPull() => WorldState.CurrentZone == 239 && WorldState.CurrentCFCID == 309 && IsAnyActorInCombat((uint)OID.Lindwurm);

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(Enemies((uint)OID.Lindwurm));
    protected override void DrawArenaForeground(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, Colors.Safe);
}
