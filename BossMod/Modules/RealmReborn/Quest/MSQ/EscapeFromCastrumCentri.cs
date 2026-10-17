namespace BossMod.RealmReborn.Quest.MSQ.EscapeFromCastrumCentri;

public enum OID : uint
{
    RopeBonds = 0x1E8DFB, // quest object retained through both areas, not an attack target
    OtherRopeBonds = 0x1E8DFC,
    Optio = 0x879,
    Eques = 0x87A,
    Signifer = 0x87B,
    Biggs = 0x87C,
    Wedge = 0x87D,
    Carbuncle = 0x87E,
    Medicus = 0x87F,
    Hoplomachus = 0x880,
    Vanguard = 0x881,
    Colossus = 0x882,
    FieldGenerator = 0x883,
    OptioHealer = 0x884,
    Papalymo = 0x7FC,
    Urianger = 0x7FD,
}

public enum AID : uint
{
    FireII = 147, // Signifer -> player/ally, target-following radius 5
    DrillCannons = 525, // Vanguard -> self, nominal 30 + R, full width 5
    CermetDrill = 526, // Vanguard -> self, nominal 6 + R, full width 5
    Overcharge = 527, // Vanguard -> self, nominal 8 + R, 120-degree cone
    GrandSword = 534, // Colossus -> self, nominal 6 + R, 120-degree cone
    MagitekRay = 537, // Colossus -> location, radius 6
    Overpower = 720, // Optio -> self, nominal 6 + R, 90-degree cone
    SpiritsWithout = 1098, // Hoplomachus -> self, nominal 3 + R, full width 3
    Rive = 1135, // Optio -> self, nominal 30 + R, full width 2
    DarkBlizzardIII = 1304, // Signifer -> location, radius 5
}

sealed class QuestAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<Actor, (ActorCastInfo Cast, AOEInstance AOE)> _casts = [];
    private readonly List<AOEInstance> _aoes = [];

    private static AOEShape? Shape(Actor caster, ActorCastInfo cast) => cast.IsSpell() ? (caster.OID, (AID)cast.Action.ID) switch
    {
        ((uint)OID.Vanguard, AID.DrillCannons) => new AOEShapeRect(30f + caster.HitboxRadius, 2.5f),
        ((uint)OID.Vanguard, AID.CermetDrill) => new AOEShapeRect(6f + caster.HitboxRadius, 2.5f),
        ((uint)OID.Vanguard, AID.Overcharge) => new AOEShapeCone(8f + caster.HitboxRadius, 60.Degrees()),
        ((uint)OID.Colossus, AID.GrandSword) => new AOEShapeCone(6f + caster.HitboxRadius, 60.Degrees()),
        ((uint)OID.Colossus, AID.MagitekRay) => new AOEShapeCircle(6f),
        ((uint)OID.Optio, AID.Overpower) => new AOEShapeCone(6f + caster.HitboxRadius, 45.Degrees()),
        ((uint)OID.Optio, AID.Rive) => new AOEShapeRect(30f + caster.HitboxRadius, 1f),
        ((uint)OID.Hoplomachus, AID.SpiritsWithout) => new AOEShapeRect(3f + caster.HitboxRadius, 1.5f),
        ((uint)OID.Signifer, AID.DarkBlizzardIII) => new AOEShapeCircle(5f),
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
        foreach (var oid in EscapeFromCastrumCentri.EnemyOIDs)
            foreach (var caster in Module.Enemies(oid))
            {
                if (caster.IsAlly || caster.IsDeadOrDestroyed || caster.CastInfo is not { } cast || Shape(caster, cast) == null)
                    continue;
                if (!_casts.TryGetValue(caster, out var cached) || cached.Cast != cast)
                {
                    OnCastStarted(caster, cast);
                    cached = _casts[caster];
                }
                _aoes.Add(cached.AOE);
            }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

sealed class FireII(BossModule module) : Components.BaitAwayCast(module, (uint)AID.FireII, 5f, centerAtTarget: true)
{
    private readonly Dictionary<Actor, ActorCastInfo> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Signifer && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.FireII)
            && (!_casts.TryGetValue(caster, out var old) || old != spell))
        {
            _casts[caster] = spell;
            CurrentBaits.RemoveAll(b => b.Source == caster);
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster, out var old) && old == spell)
        {
            _casts.Remove(caster);
            base.OnCastFinished(caster, spell);
        }
    }

    public override void OnActorDeath(Actor actor)
    {
        _casts.Remove(actor);
        CurrentBaits.RemoveAll(b => b.Source == actor || b.Target == actor);
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsAlly || b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed
        || !_casts.TryGetValue(b.Source, out var cast) || b.Source.CastInfo != cast
        || WorldState.Actors.Find(b.Source.InstanceID) != b.Source || WorldState.Actors.Find(b.Target.InstanceID) != b.Target);
}

sealed class Objectives(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.Papalymo).Any(p => !p.IsDestroyed))
            hints.Add("Defeat the Signifers and field generators before focusing on the Vanguards; then defeat the Colossus and remaining soldiers.");
        else
            hints.Add("Defeat the guards and interact with both Rope Bonds to free Papalymo and Urianger.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (enemy.Priority < 0 || target.IsAlly || !target.IsTargetable || target.IsDeadOrDestroyed)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Medicus or OID.OptioHealer => 6,
                OID.Signifer => 5,
                OID.FieldGenerator => 4,
                OID.Optio or OID.Eques or OID.Hoplomachus => 3,
                OID.Vanguard or OID.Colossus => 2,
                _ => enemy.Priority
            };
        }
    }
}

sealed class EscapeFromCastrumCentriStates : StateMachineBuilder
{
    public EscapeFromCastrumCentriStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Escape from Castrum Centri"), "Quest battle")
            .ActivateOnEnter<QuestAOEs>()
            .ActivateOnEnter<FireII>()
            .ActivateOnEnter<Objectives>()
            .Raw.Update = () => Module.PrimaryActor.IsDestroyed;
    }
}

// The rope object survives the two combat areas, including the cutscene. This
// combined planning cap includes both areas and their connector; it is not a
// measured floor boundary. See docs/escape-from-castrum-centri.md.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 66057, PrimaryActorOID = (uint)OID.RopeBonds)]
public sealed class EscapeFromCastrumCentri(WorldState ws, Actor primary) : BossModule(ws, primary, new(-453f, -258f),
    new ArenaBoundsRect(65f, 50f, allowObstacleMap: true, allowDrawing3DArenaBounds: false))
{
    public static readonly uint[] EnemyOIDs = [(uint)OID.Optio, (uint)OID.Eques, (uint)OID.Signifer, (uint)OID.Medicus, (uint)OID.Hoplomachus,
        (uint)OID.Vanguard, (uint)OID.Colossus, (uint)OID.FieldGenerator, (uint)OID.OptioHealer];

    // Retain the existing duty script's passive-objective availability. The
    // priority component still preserves every supplied negative restriction.
    public override bool ShouldPrioritizeAllEnemies => true;
    public override bool KeepQuestBattleHints => true;
    public override Actor? GetDefaultTarget(int slot) => null;

    private bool HasQuestCombat() => EnemyOIDs.Any(oid => Enemies(oid).Any(e => !e.IsAlly && !e.IsDeadOrDestroyed && (e.InCombat || e.CastInfo != null)));
    protected override bool CheckPull() => WorldState.CurrentZone == 305 && WorldState.CurrentCFCID == 352 && !PrimaryActor.IsDestroyed && HasQuestCombat();

    // Release combat constraints between waves; quest objectives also continue
    // while active through KeepQuestBattleHints. The retained object bootstraps again.
    public override bool CheckReset() => WorldState.CurrentZone != 305 || WorldState.CurrentCFCID != 352 || !HasQuestCombat();

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemyOIDs);
}
