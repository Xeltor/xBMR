namespace BossMod.RealmReborn.Quest.Job.Lancer.ProofOfMightFoulques;

public enum OID : uint
{
    Foulques = 0x223,
    SpurnedSoulLancer = 0x5FB,
    SpurnedSoulCaster = 0x5FC,
    Frost = 0x5FD,
}

public enum AID : uint
{
    Heartstopper = 866, // 2.5s corrected cast, range 3+R width 3 rectangle
    RingOfFrost = 1316, // 3s corrected cast, range 6+R circle, then persistent ice
    FireII = 147, // 3s cast, target circle radius 5
}

sealed class Heartstopper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Heartstopper, new AOEShapeRect(3.5f, 1.5f));
sealed class FireII(BossModule module) : Components.BaitAwayCast(module, (uint)AID.FireII, 5f);
sealed class RingOfFrost(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RingOfFrost, 6.5f);
// The matching spawned object persists while environmental Frostbite is observed.
// Its contact radius is uncalibrated; 6.5 is the originating action's nominal avoidance policy.
sealed class Frost(BossModule module) : Components.VoidzoneAtCastTarget(module, 6.5f, (uint)AID.RingOfFrost, m => m.Enemies((uint)OID.Frost).Where(a => !a.IsDeadOrDestroyed && a.EventState != 7))
{
    // RingOfFrost already owns the locked cast prediction. This component bridges
    // the resolving event to the spawned object, then follows its actual lifetime.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell) { }
}

sealed class ProofOfMightFoulquesStates : StateMachineBuilder
{
    public ProofOfMightFoulquesStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Heartstopper>()
            .ActivateOnEnter<FireII>()
            .ActivateOnEnter<RingOfFrost>()
            .ActivateOnEnter<Frost>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 310, NameID = 614, PrimaryActorOID = (uint)OID.Foulques)]
public sealed class ProofOfMightFoulques(WorldState ws, Actor primary) : BossModule(ws, primary, new(-285f, 292f), new ArenaBoundsRect(24f, 20f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    // Provisional floor envelope, not a verified natural-terrain boundary.
    protected override bool CheckPull() => WorldState.CurrentZone == 240 && WorldState.CurrentCFCID == 310 && IsAnyActorInCombat([(uint)OID.Foulques, (uint)OID.SpurnedSoulLancer, (uint)OID.SpurnedSoulCaster]);

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.Foulques, (uint)OID.SpurnedSoulLancer, (uint)OID.SpurnedSoulCaster]);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
        {
            var enemy = target.Actor;
            if (target.Priority < 0 || !enemy.IsTargetable || enemy.IsDeadOrDestroyed || enemy.IsAlly)
                continue;
            target.Priority = enemy.OID switch
            {
                (uint)OID.SpurnedSoulCaster => 2,
                (uint)OID.SpurnedSoulLancer => 1,
                (uint)OID.Foulques => 0,
                _ => target.Priority
            };
        }
    }
}
