namespace BossMod.RealmReborn.Quest.MSQ.TheHereticAmongUs;

public enum OID : uint
{
    InquisitorGuillaime = 0x6D2,
    Guillaime = 0x6D3, // separate actor after Dragon's Blood
    Wyrmhound = 0x6D4,
    Wyvern = 0x6D5,
    Drillemont = 0x6D6,
    Conjurer = 0x6D7,
    Archer = 0x6D8,
}

public enum AID : uint
{
    BodySlam = 497, // Wyrmhound -> self, 2.5s cast, nominal 3 + R circle
    StoneGaze = 499, // Wyrmhound -> self, 4.0s cast, nominal 6 + R, 90-degree cone
    ElectricVelitation = 738, // Guillaime -> location, 3.0s cast, radius 5
    Collapse = 1136, // Wyvern -> self, 2.5s cast, nominal 8 + R, 90-degree cone
    DragonsBlood = 1162, // Inquisitor -> self, 4.0s transformation
    RottenMeat = 1163, // Inquisitor -> self, summons reinforcements
    SpiritDart = 1164, // Guillaime -> player/ally, 4.0s single-target cast
    Ruin = 31085, // Inquisitor -> Drillemont, 1.5s single-target cast
}

sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<Actor, (ActorCastInfo Cast, AOEInstance AOE)> _casts = [];
    private readonly List<AOEInstance> _aoes = [];

    private static AOEShape? Shape(Actor caster, ActorCastInfo cast) => cast.IsSpell() ? (caster.OID, (AID)cast.Action.ID) switch
    {
        ((uint)OID.Wyrmhound, AID.BodySlam) => new AOEShapeCircle(3f + caster.HitboxRadius),
        ((uint)OID.Wyrmhound, AID.StoneGaze) => new AOEShapeCone(6f + caster.HitboxRadius, 45.Degrees()),
        ((uint)OID.Wyvern, AID.Collapse) => new AOEShapeCone(8f + caster.HitboxRadius, 45.Degrees()),
        ((uint)OID.Guillaime, AID.ElectricVelitation) => new AOEShapeCircle(5f),
        _ => null
    } : null;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!caster.IsAlly && !caster.IsDeadOrDestroyed && Shape(caster, spell) is { } shape
            && (!_casts.TryGetValue(caster, out var old) || old.Cast != spell))
            _casts[caster] = (spell, new(shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster, out var old) && old.Cast == spell)
            _casts.Remove(caster);
    }

    public override void OnActorDeath(Actor actor) => _casts.Remove(actor);
    public override void OnActorDestroyed(Actor actor) => _casts.Remove(actor);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var oid in TheHereticAmongUs.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || Shape(caster, cast) == null)
                    continue;
                if (!_casts.TryGetValue(caster, out var cached) || cached.Cast != cast)
                {
                    OnCastStarted(caster, cast); // also supports activation during a cast
                    cached = _casts[caster];
                }
                _aoes.Add(cached.AOE);
            }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

sealed class SpiritDart(BossModule module) : Components.SingleTargetCast(module, (uint)AID.SpiritDart, "Spirit Dart", AIHints.PredictedDamageType.Raidwide)
{
    private readonly Dictionary<Actor, (ActorCastInfo Cast, DateTime Deadline)> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Guillaime && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.SpiritDart)
            && (!_casts.TryGetValue(caster, out var old) || old.Cast != spell))
        {
            _casts[caster] = (spell, Module.CastFinishAt(spell));
            Casters.Remove(caster);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster, out var old) && old.Cast == spell)
        {
            _casts.Remove(caster);
            base.OnCastFinished(caster, spell);
        }
    }

    public override void OnActorDeath(Actor actor) { _casts.Remove(actor); Casters.Remove(actor); }
    public override void OnActorDestroyed(Actor actor) { _casts.Remove(actor); Casters.Remove(actor); }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var (caster, state) in _casts)
            if (!caster.IsAlly && !caster.IsDeadOrDestroyed && caster.CastInfo == state.Cast
                && WorldState.Actors.Find(state.Cast.TargetID) is { IsDeadOrDestroyed: false } target
                && Raid.FindSlot(target.InstanceID) is var targetSlot && targetSlot >= 0)
                hints.AddPredictedDamage(new BitMask().WithBit(targetSlot), state.Deadline, AIHints.PredictedDamageType.Raidwide);
    }
}

sealed class Ruin(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Ruin, "Ruin", AIHints.PredictedDamageType.Raidwide)
{
    private readonly Dictionary<Actor, (ActorCastInfo Cast, DateTime Deadline)> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.InquisitorGuillaime && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.Ruin)
            && (!_casts.TryGetValue(caster, out var old) || old.Cast != spell))
        {
            _casts[caster] = (spell, Module.CastFinishAt(spell));
            Casters.Remove(caster);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster, out var old) && old.Cast == spell)
        {
            _casts.Remove(caster);
            base.OnCastFinished(caster, spell);
        }
    }

    public override void OnActorDeath(Actor actor) { _casts.Remove(actor); Casters.Remove(actor); }
    public override void OnActorDestroyed(Actor actor) { _casts.Remove(actor); Casters.Remove(actor); }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var (caster, state) in _casts)
            if (!caster.IsAlly && !caster.IsDeadOrDestroyed && caster.CastInfo == state.Cast
                && WorldState.Actors.Find(state.Cast.TargetID) is { IsDeadOrDestroyed: false } target
                && Raid.FindSlot(target.InstanceID) is var targetSlot && targetSlot >= 0)
                hints.AddPredictedDamage(new BitMask().WithBit(targetSlot), state.Deadline, AIHints.PredictedDamageType.Raidwide);
    }
}

sealed class ProtectDrillemont(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        hints.Add("Protect Drillemont; defeat Wyrmhounds and Wyverns first.");
        foreach (var caster in Module.Enemies((uint)OID.InquisitorGuillaime))
            if (!caster.IsAlly && !caster.IsDeadOrDestroyed && caster.CastInfo is { } cast)
            {
                if (cast.IsSpell(AID.RottenMeat))
                    hints.Add("Reinforcements incoming!");
                else if (cast.IsSpell(AID.DragonsBlood))
                    hints.Add("Guillaime is transforming!");
            }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (enemy.Priority < 0 || target.IsAlly || target.IsDeadOrDestroyed || !target.IsTargetable)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Wyrmhound or OID.Wyvern => 3,
                OID.InquisitorGuillaime or OID.Guillaime => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class TheHereticAmongUsStates : StateMachineBuilder
{
    public TheHereticAmongUsStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Protect Drillemont"), "Quest battle")
            .ActivateOnEnter<QuestAOEs>()
            .ActivateOnEnter<SpiritDart>()
            .ActivateOnEnter<Ruin>()
            .ActivateOnEnter<ProtectDrillemont>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed;
    }
}

// Planning bounds only: the matching entrance barrier and scripted spawn layout
// do not establish the complete cliff/ramp floor. Preserve terrain obstacle maps
// and avoid drawing this envelope as a physical arena. See implementation notes.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66477, NameID = 2085, PrimaryActorOID = (uint)OID.Drillemont)]
public sealed class TheHereticAmongUs(WorldState ws, Actor primary) : BossModule(ws, primary, new(-860f, 0f),
    new ArenaBoundsRect(60f, 40f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.InquisitorGuillaime, (uint)OID.Guillaime, (uint)OID.Wyrmhound, (uint)OID.Wyvern];

    protected override bool CheckPull() => WorldState.CurrentZone == 302 && WorldState.CurrentCFCID == 344 && !PrimaryActor.IsDeadOrDestroyed && IsAnyActorInCombat(EnemyOIDs);
    public override bool CheckReset() => WorldState.CurrentZone != 302 || WorldState.CurrentCFCID != 344;

    // The primary is a protected ally, never a fallback attack target. Eligible
    // enemies are selected through the normal target priorities instead.
    public override Actor? GetDefaultTarget(int slot) => null;

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemyOIDs);
}
