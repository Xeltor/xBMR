namespace BossMod.RealmReborn.Quest.MSQ.TheCompanyYouKeepMaelstrom;

public enum OID : uint
{
    StormMarauder = 0x406,
    StormConjurer = 0x407,
    StormArcher = 0x408,
    Decurion = 0x3FC,
    Bestiarius = 0x3FD,
    Imaginifer = 0x3FE,
    Funditor = 0x3FF,
    Vanguard = 0x3F8,
}

public enum AID : uint
{
    FireII = 147, // hostile target-following 5y splash; friendly casts are excluded
    BlizzardII = 146,
    CermetDrill = 526,
    DrillCannons = 525,
}

// All retained hostile casts are independent. Reading current cast state also handles
// activation during a cast, simultaneous casters, cancellation, death and recreation.
sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (Actor Source, ActorCastInfo Cast, DateTime Deadline)> _activations = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!caster.IsAlly && TheCompanyYouKeepMaelstrom.EnemyOIDs.Contains(caster.OID)
            && (!_activations.TryGetValue(caster.InstanceID, out var previous) || previous.Source != caster || previous.Cast != spell))
            _activations[caster.InstanceID] = (caster, spell, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _activations.Remove(caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor.InstanceID);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var oid in TheCompanyYouKeepMaelstrom.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || !cast.IsSpell())
                    continue;
                AOEShape? shape = (AID)cast.Action.ID switch
                {
                    AID.BlizzardII => new AOEShapeCircle(5f),
                    AID.CermetDrill => new AOEShapeRect(6f + caster.HitboxRadius, 2.5f),
                    AID.DrillCannons => new AOEShapeRect(30f + caster.HitboxRadius, 2.5f),
                    _ => null
                };
                if (shape != null)
                {
                    if (!_activations.TryGetValue(caster.InstanceID, out var activation) || activation.Source != caster || activation.Cast != cast)
                        _activations[caster.InstanceID] = activation = (caster, cast, Module.CastFinishAt(cast));
                    _aoes.Add(new(shape, cast.LocXZ, cast.Rotation, activation.Deadline, actorID: caster.InstanceID));
                }
            }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

sealed class FireII(BossModule module) : Components.BaitAwayCast(module, (uint)AID.FireII, 5f)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Imaginifer && !caster.IsAlly && spell.Action.ID == WatchedAction)
        {
            CurrentBaits.RemoveAll(b => b.Source == caster);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnActorDestroyed(Actor actor) => CurrentBaits.RemoveAll(b => b.Source == actor || b.Target == actor);

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsAlly || b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed
        || b.Source.CastInfo?.Action.ID != WatchedAction || WorldState.Actors.Find(b.Source.InstanceID) != b.Source || WorldState.Actors.Find(b.Target.InstanceID) != b.Target);
}

sealed class Priorities(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints) => hints.Add("Defeat the ranged soldiers first; keep the Vanguard's line attacks away from allies.");

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.IsAlly || target.IsDeadOrDestroyed || !target.IsTargetable || enemy.Priority < 0)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Imaginifer => 4,
                OID.Funditor => 4,
                OID.Bestiarius => 3,
                OID.Decurion => 2,
                OID.Vanguard => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class TheCompanyYouKeepMaelstromStates : StateMachineBuilder
{
    public TheCompanyYouKeepMaelstromStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Quest battle"), "Quest battle")
            .ActivateOnEnter<QuestAOEs>()
            .ActivateOnEnter<FireII>()
            .ActivateOnEnter<Priorities>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed;
    }
}

// The stable encounter anchor survives individual enemy deaths and reinforcement gaps.
// The planning envelope is provisional, not a measured terrain or lethal-edge boundary.
// See docs/arr-later-quest-modules.md; floor/collision validation remains WIP.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66217, NameID = 1003, PrimaryActorOID = (uint)OID.StormMarauder)]
public sealed class TheCompanyYouKeepMaelstrom(WorldState ws, Actor primary) : BossModule(ws, primary, new(369f, -352f),
    new ArenaBoundsCircle(45f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.Decurion, (uint)OID.Bestiarius, (uint)OID.Imaginifer, (uint)OID.Funditor, (uint)OID.Vanguard];

    protected override bool CheckPull() => WorldState.CurrentZone == 279 && WorldState.CurrentCFCID == 342 && IsAnyActorInCombat(EnemyOIDs);
    public override bool CheckReset() => WorldState.CurrentZone != 279 || WorldState.CurrentCFCID != 342;

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var oid in EnemyOIDs)
            Arena.Actors(Enemies(oid));
    }
}
