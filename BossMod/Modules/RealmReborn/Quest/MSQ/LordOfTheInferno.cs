namespace BossMod.RealmReborn.Quest.MSQ.LordOfTheInferno;

public enum OID : uint
{
    FlameSergeant = 0x3C3,
    FlameGladiator = 0x3C4,
    FlamePugilist = 0x3C5,
    FlameThaumaturge = 0x3C6,
    Highcant = 0x3C7,
    Bruiser = 0x3C8,
    Ranger = 0x3C9,
    Halberdier = 0x3CA,
}

public enum AID : uint
{
    Devastate = 490,
    BlizzardII = 146,
}

// All retained hostile casts are independent. Reading current cast state also handles
// activation during a cast, simultaneous casters, cancellation, death and recreation.
sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (Actor Source, ActorCastInfo Cast, DateTime Deadline)> _activations = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!caster.IsAlly && LordOfTheInferno.EnemyOIDs.Contains(caster.OID)
            && (!_activations.TryGetValue(caster.InstanceID, out var previous) || previous.Source != caster || previous.Cast != spell))
            _activations[caster.InstanceID] = (caster, spell, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => _activations.Remove(caster.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor.InstanceID);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var oid in LordOfTheInferno.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || !cast.IsSpell())
                    continue;
                AOEShape? shape = (AID)cast.Action.ID switch
                {
                    AID.Devastate => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
                    AID.BlizzardII => new AOEShapeCircle(5f),
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

sealed class Priorities(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints) => hints.Add("Protect the Flames: defeat the bruisers and rangers; deal with halberdiers attacking you.");

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.IsAlly || target.IsDeadOrDestroyed || !target.IsTargetable || enemy.Priority < 0)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Bruiser => 3,
                OID.Ranger => 2,
                OID.Halberdier => target.TargetID == actor.InstanceID ? 4 : 1,
                OID.Highcant => AIHints.Enemy.PriorityInvincible,
                _ => enemy.Priority
            };
        }
    }
}

sealed class LordOfTheInfernoStates : StateMachineBuilder
{
    public LordOfTheInfernoStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Quest battle"), "Quest battle")
            .ActivateOnEnter<QuestAOEs>()
            .ActivateOnEnter<Priorities>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed;
    }
}

// The stable encounter anchor survives individual enemy deaths and reinforcement gaps.
// The planning envelope is provisional, not a measured terrain or lethal-edge boundary.
// See docs/arr-later-quest-modules.md; floor/collision validation remains WIP.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65879, NameID = 993, PrimaryActorOID = (uint)OID.FlameSergeant)]
public sealed class LordOfTheInferno(WorldState ws, Actor primary) : BossModule(ws, primary, new(-338f, -238f),
    new ArenaBoundsCircle(32f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.Highcant, (uint)OID.Bruiser, (uint)OID.Ranger, (uint)OID.Halberdier];

    protected override bool CheckPull() => WorldState.CurrentZone == 275 && WorldState.CurrentCFCID == 339 && IsAnyActorInCombat(EnemyOIDs);
    public override bool CheckReset() => WorldState.CurrentZone != 275 || WorldState.CurrentCFCID != 339;

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var oid in EnemyOIDs)
            Arena.Actors(Enemies(oid));
    }
}
