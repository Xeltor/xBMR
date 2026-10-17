namespace BossMod.RealmReborn.Quest.MSQ.NouveauRiche;

public enum OID : uint
{
    Laurentius = 0x667,
    RedbellyStinger = 0x668,
    CoeurlclawHunter = 0x669,
    Bestiarius = 0x66A,
    Imaginifer = 0x66B,
    Funditor = 0x66C,
    Decurion = 0x66D,
    RedbellyGutter = 0x66E,
}

public enum AID : uint
{
    FireII = 147, // hostile target-following 5y splash; friendly casts are excluded
    Heartstopper = 866,
    Overpower = 720,
    Rive = 1135,
}

// All retained hostile casts are independent. Reading current cast state also handles
// activation during a cast, simultaneous casters, cancellation, death and recreation.
sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (Actor Source, ActorCastInfo Cast, DateTime Deadline)> _activations = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!caster.IsAlly && NouveauRiche.EnemyOIDs.Contains(caster.OID)
            && (!_activations.TryGetValue(caster.InstanceID, out var previous) || previous.Source != caster || previous.Cast != spell))
            _activations[caster.InstanceID] = (caster, spell, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _activations.Remove(caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor.InstanceID);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var oid in NouveauRiche.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || !cast.IsSpell())
                    continue;
                AOEShape? shape = (AID)cast.Action.ID switch
                {
                    AID.Heartstopper => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
                    AID.Overpower => new AOEShapeCone(6f + caster.HitboxRadius, 45f.Degrees()),
                    AID.Rive => new AOEShapeRect(30f + caster.HitboxRadius, 1f),
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
    public override void AddGlobalHints(Actor actor, GlobalHints hints) => hints.Add("Defeat the imperial reinforcements first, then Laurentius.");

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.IsAlly || target.IsDeadOrDestroyed || !target.IsTargetable || enemy.Priority < 0)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Imaginifer => 5,
                OID.Bestiarius => 4,
                OID.Funditor => 3,
                OID.Decurion => 2,
                OID.Laurentius => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class NouveauRicheStates : StateMachineBuilder
{
    public NouveauRicheStates(BossModule module) : base(module)
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
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66276, NameID = 80, PrimaryActorOID = (uint)OID.RedbellyGutter)]
public sealed class NouveauRiche(WorldState ws, Actor primary) : BossModule(ws, primary, new(44f, 6f),
    new ArenaBoundsCircle(42f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.Laurentius, (uint)OID.Bestiarius, (uint)OID.Imaginifer, (uint)OID.Funditor, (uint)OID.Decurion];

    protected override bool CheckPull() => WorldState.CurrentZone == 232 && WorldState.CurrentCFCID == 311 && IsAnyActorInCombat(EnemyOIDs);
    public override bool CheckReset() => WorldState.CurrentZone != 232 || WorldState.CurrentCFCID != 311;

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var oid in EnemyOIDs)
            Arena.Actors(Enemies(oid));
    }
}
