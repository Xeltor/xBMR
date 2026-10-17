namespace BossMod.RealmReborn.Quest.MSQ.TheLominsanWay;

public enum OID : uint
{
    Trachtoum = 0x6E9,
    LargeBoulder = 0x6EA, // player's objective; creation R0.5 updates to R1.5
    SmallBoulder = 0x6EB, // opponent's objective; destroying it loses the contest
    Bomb = 0x6EC, // script INIT_POP_2_BOM_01 -> matching Level row
}

public enum AID : uint
{
    Paralyze = 1118, // Trachtoum -> player, 4.0s single-target cast
    Rive = 1299, // Trachtoum -> self, 2.5s line; 1135 is another variant
}

public enum SID : uint
{
    Paralysis = 17, // QuestBattle DEF_PARALYSIS
}

sealed class Rive(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Rive, new AOEShapeRect(30.5f, 1f))
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Trachtoum && !caster.IsAlly && spell.IsSpell(AID.Rive))
        {
            Casters.RemoveAll(a => a.ActorID == caster.InstanceID);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnActorDeath(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}

sealed class Contest(BossModule module) : BossComponent(module)
{
    private Actor? _opponentBoulder;
    public bool OpponentFinished { get; private set; }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.SmallBoulder)
        {
            _opponentBoulder = actor;
            OpponentFinished = actor.IsDeadOrDestroyed;
        }
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == _opponentBoulder)
            OpponentFinished = true;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _opponentBoulder)
            OpponentFinished = true;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        hints.Add("Attack only the Large Boulder; ignore Trachtoum, the Small Boulder and his bomb.");
        if (Module.Enemies((uint)OID.Bomb).Any(b => !b.IsAlly && !b.IsDeadOrDestroyed))
            hints.Add("Bomb active: finish the Large Boulder before it explodes!");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (WorldState.Actors.Find(actor.TargetID)?.OID is (uint)OID.Trachtoum or (uint)OID.SmallBoulder or (uint)OID.Bomb)
            hints.Add("Attack the Large Boulder!");
        foreach (var caster in Module.Enemies((uint)OID.Trachtoum))
            if (!caster.IsAlly && !caster.IsDeadOrDestroyed && caster.CastInfo is { } cast && cast.IsSpell(AID.Paralyze) && cast.TargetID == actor.InstanceID)
                hints.Add("Paralyze incoming: prepare to cleanse if possible.");
        if (actor.FindStatus((uint)SID.Paralysis) != null)
            hints.Add("Paralyzed: cleanse if possible.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!actor.IsDeadOrDestroyed && actor.FindStatus((uint)SID.Paralysis) != null)
            hints.ShouldCleanse.Set(slot);
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.IsAlly || !target.IsTargetable || target.IsDeadOrDestroyed)
                continue;
            if (target == Module.PrimaryActor)
            {
                // The objective never enters combat in the retained failed attempt.
                // Promote this one passive target without overriding native exclusions.
                if (enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable)
                    enemy.Priority = 1;
            }
            else if (target.OID is (uint)OID.Trachtoum or (uint)OID.SmallBoulder or (uint)OID.Bomb)
            {
                // Pointless (-1) still permits incidental AOE hits. Strengthen even
                // that restriction: damaging the opponent's boulder loses the race.
                if (enemy.Priority > AIHints.Enemy.PriorityForbidden)
                    enemy.ForcePriority(AIHints.Enemy.PriorityForbidden);
                enemy.ForbidDOTs = true;
            }
        }
    }
}

sealed class TheLominsanWayStates : StateMachineBuilder
{
    public TheLominsanWayStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Break the Large Boulder"), "Boulder contest")
            .ActivateOnEnter<Rive>()
            .ActivateOnEnter<Contest>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed || Module.FindComponent<Contest>()?.OpponentFinished == true;
    }
}

// The matching quest barriers support this central planning cap, not a complete
// physical floor boundary. Keep WIP; see docs/the-lominsan-way.md.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66347, NameID = 2074, PrimaryActorOID = (uint)OID.LargeBoulder)]
public sealed class TheLominsanWay(WorldState ws, Actor primary) : BossModule(ws, primary, new(695f, -292f),
    new ArenaBoundsCircle(12f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    protected override bool CheckPull() => WorldState.CurrentZone == 307 && WorldState.CurrentCFCID == 354 && PrimaryActor.IsTargetable && !PrimaryActor.IsDeadOrDestroyed;
    public override bool CheckReset() => WorldState.CurrentZone != 307 || WorldState.CurrentCFCID != 354;

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.LargeBoulder, (uint)OID.SmallBoulder, (uint)OID.Trachtoum]);
}
