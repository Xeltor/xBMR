namespace BossMod.RealmReborn.Quest.Job.Dragoon.IntoTheDragonsMaw;

public enum OID : uint
{
    Boss = 0x637, // Estinien Wyrmblood, R0.55
    DragonsEye = 0x638, // R0.6; becomes targetable before empowering Estinien
    InnerDragon = 0x639, // friendly scripted actor, not an attack target
    Fire = 0x1E8D61, // EObj 2002273, inactive in state 7
}

public enum AID : uint
{
    AutoAttack = 871,
    Heartstopper = 866, // 2.5s corrected cast, range 3+R width 3 rectangle
    Jump = 1318, // 2s corrected cast, location-targeted radius 3 circle
    SkydragonDive = 1064, // 2.5s corrected cast, location-targeted radius 5 circle, leaves fire
    LifeSurge = 83, // instant self buff
    GutRip = 477, // instant self buff
    InnerDragon = 1063, // friendly scripted recovery
    DragonsCurse = 1319, // Dragon's Eye -> Estinien, self-destructs and applies Critical Skill
    DragonsCurseDamageUp = 1320, // Dragon's Eye -> Estinien, self-destructs and applies Physical Damage Up
}

sealed class Heartstopper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Heartstopper, new AOEShapeRect(3.55f, 1.5f));
sealed class Jump(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Jump, 3f);
sealed class SkydragonDive(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SkydragonDive, 5f);

// The fire contact radius is uncalibrated; use the originating dive's nominal
// radius as provisional avoidance policy. Keep each object until it deactivates
// or disappears, independently of later dives and Estinien's cast lifecycle.
sealed class Fire(BossModule module) : Components.GenericAOEs(module, (uint)AID.SkydragonDive, "GTFO from fire!")
{
    private static readonly AOEShapeCircle Shape = new(5f);
    private readonly List<WPos> _pending = [];
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var position in _pending)
            _aoes.Add(new(Shape, position));
        foreach (var fire in Module.Enemies((uint)OID.Fire))
            if (!fire.IsDeadOrDestroyed && fire.EventState != 7)
                _aoes.Add(new(Shape, fire.Position, actorID: fire.InstanceID));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _pending.Add(spell.TargetXZ);
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID != (uint)OID.Fire)
            return;
        // Only a newly created object can retire a prediction. Older nearby
        // puddles must not erase the new dive's fringe during the spawn delay.
        for (var i = 0; i < _pending.Count; ++i)
            if (_pending[i].InCircle(actor.Position, 0.1f))
            {
                _pending.RemoveAt(i);
                return;
            }
    }
}

sealed class IntoTheDragonsMawStates : StateMachineBuilder
{
    public IntoTheDragonsMawStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Heartstopper>()
            .ActivateOnEnter<Jump>()
            .ActivateOnEnter<SkydragonDive>()
            .ActivateOnEnter<Fire>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66608, NameID = 1394, PrimaryActorOID = (uint)OID.Boss)]
public sealed class IntoTheDragonsMaw(WorldState ws, Actor primary) : BossModule(ws, primary, new(265f, -680f),
    new ArenaBoundsCircle(25f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    // Provisional planning envelope around the quest's fixed encounter layout,
    // not a measured natural-terrain perimeter. No lethal edge/hole is verified.
    protected override bool CheckPull() => WorldState.CurrentZone == 316 && WorldState.CurrentCFCID == 365 && base.CheckPull();

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, [(uint)OID.Boss, (uint)OID.DragonsEye]);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var target in hints.PotentialTargets)
        {
            var enemy = target.Actor;
            if (target.Priority < 0 || !enemy.IsTargetable || enemy.IsDeadOrDestroyed || enemy.IsAlly)
                continue;
            target.Priority = enemy.OID switch
            {
                (uint)OID.DragonsEye => 1,
                (uint)OID.Boss => 0,
                _ => target.Priority
            };
        }
    }
}
