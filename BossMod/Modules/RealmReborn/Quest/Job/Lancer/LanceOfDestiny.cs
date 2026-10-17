namespace BossMod.RealmReborn.Quest.Job.Lancer.LanceOfDestiny;

public enum OID : uint
{
    Kamapuaa = 0x6CD, // R3
    WildHog = 0x6CE, // R2.4
    WaterSprite = 0x6CF,
    FireSprite = 0x6D0,
    Foulques = 0x6D1, // friendly competitor
}

public enum AID : uint
{
    RecklessCharge = 334, // 2.5s corrected cast, stationary range 3+R width 3 rectangle
    WaterII = 1016, // 2.5s cast, target circle radius 5
}

sealed class RecklessCharge(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RecklessCharge, new AOEShapeRect(6f, 1.5f))
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            var shape = new AOEShapeRect(3f + caster.HitboxRadius, 1.5f);
            var origin = spell.LocXZ;
            Casters.Add(new(shape, origin, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID, shapeDistance: shape.Distance(origin, spell.Rotation)));
        }
    }
}
sealed class WaterII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.WaterII, 5f);

sealed class LanceOfDestinyStates : StateMachineBuilder
{
    public LanceOfDestinyStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RecklessCharge>()
            .ActivateOnEnter<WaterII>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 355, NameID = 1942, PrimaryActorOID = (uint)OID.Kamapuaa)]
public sealed class LanceOfDestiny(WorldState ws, Actor primary) : BossModule(ws, primary, new(-235f, 21f), new ArenaBoundsRect(25f, 25f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    // Provisional planning envelope; terrain/barrier reconstruction remains open.
    protected override bool CheckPull() => WorldState.CurrentZone == 290 && WorldState.CurrentCFCID == 355 && IsAnyActorInCombat([(uint)OID.Kamapuaa, (uint)OID.WildHog, (uint)OID.WaterSprite, (uint)OID.FireSprite]);

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.Kamapuaa, (uint)OID.WildHog, (uint)OID.WaterSprite, (uint)OID.FireSprite]);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
        {
            var enemy = target.Actor;
            if (target.Priority < 0 || !enemy.IsTargetable || enemy.IsDeadOrDestroyed || enemy.IsAlly)
                continue;
            target.Priority = enemy.OID switch
            {
                (uint)OID.WaterSprite => 3, // observed Cure heals the enemy pack
                (uint)OID.FireSprite => 2,
                (uint)OID.WildHog => 1,
                (uint)OID.Kamapuaa => 0,
                _ => target.Priority
            };
        }
    }
}
