namespace BossMod.RealmReborn.Quest.MSQ.BigTroubleInLittleAlaMhigo;

public enum OID : uint
{
    Gundobald = 0x7BE,
    Shaman = 0x7BF,
    Wilred = 0x7C0,
    StrappingLad1 = 0x7C1,
    StrappingLad2 = 0x7C2,
    Lavafang = 0x7B9,
    Pugilist = 0x7BA,
    Lancer = 0x7BC,
    Drake = 0x7BD,
    Conscript = 0x6C1,
    Pikeman = 0x6C2,
}

public enum AID : uint
{
    FireII = 147, // hostile target-following 5y splash; friendly casts are excluded
    Devastate = 490,
    BurningCyclone = 517,
    Gutripper = 865,
    Heartstopper = 866,
}

// All retained hostile casts are independent. Reading current cast state also handles
// activation during a cast, simultaneous casters, cancellation, death and recreation.
sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (Actor Source, ActorCastInfo Cast, DateTime Deadline)> _activations = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!caster.IsAlly && BigTroubleInLittleAlaMhigo.EnemyOIDs.Contains(caster.OID)
            && (!_activations.TryGetValue(caster.InstanceID, out var previous) || previous.Source != caster || previous.Cast != spell))
            _activations[caster.InstanceID] = (caster, spell, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _activations.Remove(caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor.InstanceID);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var oid in BigTroubleInLittleAlaMhigo.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || !cast.IsSpell())
                    continue;
                AOEShape? shape = (AID)cast.Action.ID switch
                {
                    AID.Devastate => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
                    AID.BurningCyclone => new AOEShapeCone(6f + caster.HitboxRadius, 60f.Degrees()),
                    AID.Gutripper => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
                    AID.Heartstopper => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
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
        if (caster.OID == (uint)OID.Lavafang && !caster.IsAlly && spell.Action.ID == WatchedAction)
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
    public override void AddGlobalHints(Actor actor, GlobalHints hints) => hints.Add("Protect the Ala Mhigans: defeat the lancers and pugilists, then the drake and reinforcements.");

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.IsAlly || target.IsDeadOrDestroyed || !target.IsTargetable || enemy.Priority < 0)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Lancer => 6,
                OID.Pugilist => 5,
                OID.Drake => 4,
                OID.Pikeman => 3,
                OID.Conscript => 2,
                OID.Lavafang => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class BigTroubleInLittleAlaMhigoStates : StateMachineBuilder
{
    public BigTroubleInLittleAlaMhigoStates(BossModule module) : base(module)
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
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66318, NameID = 2069, PrimaryActorOID = (uint)OID.Gundobald)]
public sealed class BigTroubleInLittleAlaMhigo(WorldState ws, Actor primary) : BossModule(ws, primary, new(35f, -345f),
    new ArenaBoundsCircle(38f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.Lavafang, (uint)OID.Pugilist, (uint)OID.Lancer, (uint)OID.Drake, (uint)OID.Conscript, (uint)OID.Pikeman];

    protected override bool CheckPull() => WorldState.CurrentZone == 306 && WorldState.CurrentCFCID == 353 && IsAnyActorInCombat(EnemyOIDs);
    public override bool CheckReset() => WorldState.CurrentZone != 306 || WorldState.CurrentCFCID != 353;

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var oid in EnemyOIDs)
            Arena.Actors(Enemies(oid));
    }
}
