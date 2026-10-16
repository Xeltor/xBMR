namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

sealed class ArenaChanges : BossComponent
{
    public static readonly WPos ArenaCenter = new(100f, 100f);
    public static readonly ArenaBounds InitialBounds = new ArenaBoundsSquare(20f);
    // Normal moves the two original 20x40 halves outwards by six yalms, unlike Savage's five.
    private static readonly Shape[] _west = [new Rectangle(new(84f, 100f), 10f, 20f)];
    private static readonly Shape[] _east = [new Rectangle(new(116f, 100f), 10f, 20f)];
    public static readonly ArenaBoundsCustom SplitBounds = new([.. _west, .. _east]);
    public static readonly ArenaBoundsCustom WestBounds = new(_west);
    public static readonly ArenaBoundsCustom EastBounds = new(_east);
    public static readonly RelSimplifiedComplexPolygon WestLanding = WestBounds.Shape.Offset(-0.75f);
    public static readonly RelSimplifiedComplexPolygon EastLanding = EastBounds.Shape.Offset(-0.75f);
    public bool Split;
    public int DestroyedSide; // -1 west, +1 east; impending destruction is separate from current bounds
    public DateTime Destruction;
    private DateTime _restoration;
    private Actor? _restorationCaster;
    private static readonly AOEShapeRect _restoredInterior = new(19.25f, 19.25f, 19.25f);
    private static readonly AOEShapeRect _outerStrip = new(20f, 3f, 20f);
    private static readonly AOEShapeRect _platform = new(20f, 10f, 20f);
    private Actor? _destructionCaster;
    private uint _destructionAction;
    // Display marker only. Launch assignments own evacuation; an ordinary walking ban here
    // would fight the required meteor position on the still-present doomed floor.
    public Components.GenericAOEs.AOEInstance? PendingDestruction => DestroyedSide != 0 && Arena.Bounds == SplitBounds
        ? new(_platform, new(DestroyedSide < 0 ? 84f : 116f, 100f), activation: Destruction, color: Colors.Danger, risky: false)
        : null;

    public ArenaChanges(BossModule module) : base(module)
    {
        Arena.Center = ArenaCenter;
        Arena.Bounds = InitialBounds;
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ArcadionAvalancheVariant1 or (uint)AID._Weaponskill_ArcadionAvalanche
            or (uint)AID.ArcadionAvalancheVariant3 or (uint)AID.ArcadionAvalanche)
        {
            DestroyedSide = spell.Rotation.ToDirection().X < 0f ? -1 : 1;
            Destruction = Module.CastFinishAt(spell);
            _destructionCaster = caster;
            _destructionAction = spell.Action.ID;
        }
        else if (spell.Action.ID == (uint)AID._Weaponskill_CrownOfArcadia && Split && DestroyedSide != 0)
        {
            // Crown precedes restoration. The surviving island's outer six yalms disappear;
            // move onto the overlap with the original floor while the gap is still impassable.
            _restoration = Module.CastFinishAt(spell);
            _restorationCaster = caster;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Normal cast completion precedes the director update; keep the prediction until then.
        if (caster == _restorationCaster && spell.Action.ID == (uint)AID._Weaponskill_CrownOfArcadia && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            ClearRestoration();
        if (caster == _destructionCaster && spell.Action.ID == _destructionAction && !spell.EventHappened && spell.NPCRemainingTime > 0.5f && Arena.Bounds == SplitBounds)
            ClearPendingDestruction();
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _restorationCaster)
            ClearRestoration();
        if (actor == _destructionCaster && Arena.Bounds == SplitBounds)
            ClearPendingDestruction();
    }
    private void ClearPendingDestruction()
    {
        DestroyedSide = 0;
        Destruction = default;
        _destructionCaster = null;
        _destructionAction = 0;
    }
    private void ClearRestoration()
    {
        _restoration = default;
        _restorationCaster = null;
    }
    public override void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4)
    {
        if (updateID != 0x8000000Du)
            return;
        switch (param1)
        {
            case 0u: // duty completion/reset
            case 1u:
                ClearRestoration();
                Split = false;
                ClearPendingDestruction();
                Arena.Center = ArenaCenter;
                Arena.Bounds = InitialBounds;
                break;
            case 2u:
                ClearRestoration();
                Split = true;
                Arena.Center = ArenaCenter;
                Arena.Bounds = SplitBounds;
                break;
            case 8u:
                Split = true;
                DestroyedSide = -1;
                Arena.Center = new(116f, 100f);
                Arena.Bounds = EastBounds;
                break;
            case 16u:
                Split = true;
                DestroyedSide = 1;
                Arena.Center = new(84f, 100f);
                Arena.Bounds = WestBounds;
                break;
        }
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_restoration != default)
            hints.Add("Arena reforms - leave the outer edges!");
        if (PendingDestruction != null)
            hints.Add(DestroyedSide < 0 ? "West platform is being destroyed - land east!" : "East platform is being destroyed - land west!");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (PendingDestruction is { } doomed && doomed.Check(actor.Position))
            hints.Add("Use your meteor to leave the doomed platform!");
        if (_restoration != default)
            hints.Add("Move inward before the arena reforms!", !_restoredInterior.Check(actor.Position, ArenaCenter, default));
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_restoration != default)
            hints.AddForbiddenZone(_restoredInterior.InvertedDistance(ArenaCenter, default), _restoration.AddSeconds(-0.3d));
    }
    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (PendingDestruction is { } doomed)
            doomed.Shape.Draw(Arena, doomed.Origin, doomed.Rotation, doomed.Color);
        if (_restoration == default)
            return;
        // Preview only the disappearing strip on the current surviving floor.
        var pos = new WPos(DestroyedSide > 0 ? 77f : 123f, 100f);
        _outerStrip.Draw(Arena, pos, default, Colors.AOE);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (PendingDestruction is { } doomed)
        {
            doomed.Shape.Outline(Arena, doomed.Origin, doomed.Rotation, Colors.Danger, 3f);
        }
    }
}

sealed class FlatlinerKnockUp(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID._Weaponskill_FlatlinerKnockup, 15f, true)
{
    // Party slot zero is the local player on every client. Server actor IDs give all clients
    // the same ordering and therefore the same split, regardless of their local party slots.
    public int LandingSide(Actor actor)
    {
        var members = Raid.WithoutSlot(excludeAlliance: true, excludeNPCs: true).OrderBy(a => a.InstanceID).ToArray();
        var index = Array.FindIndex(members, a => a == actor);
        return index >= 0 ? ((index & 1) == 0 ? -1 : 1) : Math.Sign(actor.Position.X - 100f);
    }
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !ArenaChanges.SplitBounds.Contains(pos - ArenaChanges.ArenaCenter);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // At least two players must reach each platform for the four later towers.
        var bounds = LandingSide(actor) < 0 ? ArenaChanges.WestBounds : ArenaChanges.EastBounds;
        foreach (var kb in Casters)
            hints.AddForbiddenZone(new SDKnockbackInComplexPolygonAwayFromOrigin(bounds.Center, kb.Origin, kb.Distance, LandingSide(actor) < 0 ? ArenaChanges.WestLanding : ArenaChanges.EastLanding), kb.Activation.AddSeconds(-0.3d));
    }
}

sealed class ExplosionTower(BossModule module) : Components.CastTowers(module, (uint)AID.ExplosionKnockUp, 4f, 1, 8)
{
    // Walking cannot cross the gap; only advertise towers on the actor's current platform.
    private Tower[] _local = [];
    private readonly ArenaChanges _arena = module.FindComponent<ArenaChanges>()!;
    public override ReadOnlySpan<Tower> ActiveTowers(int slot, Actor actor)
    {
        var required = Module.FindComponent<ExplosionKnockUp>()?.RequiredTower(actor) ?? 0;
        _local = Towers.Where(t => Math.Sign(t.Position.X - 100f) == Math.Sign(actor.Position.X - 100f) && (required == 0 || t.ActorID == required)).Select(t =>
        {
            t.MaxSoakers = required != 0 || _arena.DestroyedSide != 0 && Math.Sign(t.Position.X - 100f) == _arena.DestroyedSide ? 8 : 1;
            return t;
        }).ToArray();
        return _local;
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !Towers.Exists(t => t.ActorID == caster.InstanceID))
            base.OnCastStarted(caster, spell);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor) => Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
}

sealed class ArcadionAvalancheToss(BossModule module) : Components.SimpleAOEGroups(module,
    [(uint)AID.ArcadionAvalancheTossVariant1, (uint)AID._Weaponskill_ArcadionAvalanche1,
     (uint)AID.ArcadionAvalancheTossVariant3, (uint)AID.ArcadionAvalancheToss], new AOEShapeRect(40f, 20f))
{
    private readonly ArenaChanges _arena = module.FindComponent<ArenaChanges>()!;
    private AOEInstance[] _output = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = base.ActiveAOEs(slot, actor);
        if (_output.Length != aoes.Length)
            _output = new AOEInstance[aoes.Length];
        aoes.CopyTo(_output);
        foreach (ref var aoe in _output.AsSpan())
            aoe.Color = _arena.PendingDestruction != null ? Colors.AOE : Colors.Danger;
        return _output;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_arena.PendingDestruction == null)
            base.AddHints(slot, actor, hints);
        else if (ActiveAOEs(slot, actor).Length != 0)
            hints.Add("Platform impact follows the launches - prepare to dodge!", false);
    }
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}

sealed class MajesticMeteorain(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<byte, AOEInstance> _predicted = [];
    private readonly Dictionary<ulong, AOEInstance> _casts = [];
    private AOEInstance[] _output = [];
    private static readonly AOEShapeRect _shape = new(60f, 5f);
    public override void OnMapEffect(byte index, uint state)
    {
        var full = index is >= 22 and <= 29;
        var lane = full ? index - 20 : index;
        if (lane is < 2 or > 9)
            return;
        if (state == 0x00020001u)
        {
            var pos = lane <= 5 ? new WPos((full ? 85f : 79f) + 10f * (lane - 2) + (!full && lane >= 4 ? 12f : 0f), 75f)
                : new WPos(full ? 125f : 131f, 85f + 10f * (lane - 6));
            _predicted[index] = new(_shape, pos, lane <= 5 ? default : -90f.Degrees(), WorldState.FutureTime(10.2d));
        }
        else if (state == 0x00080004u)
            _predicted.Remove(index);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID._Spell_MajesticMeteorain)
            return;
        foreach (var key in _predicted.Where(p => p.Value.Origin.AlmostEqual(spell.LocXZ, 1f)).Select(p => p.Key).ToArray())
            _predicted.Remove(key);
        _casts[caster.InstanceID] = new(_shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID._Spell_MajesticMeteorain)
            _casts.Remove(caster.InstanceID);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _output = [.. _predicted.Values, .. _casts.Values];
        Components.AOESequence.HighlightNext(_output);
        return _output;
    }
    public override void DrawArenaBackground(int pcSlot, Actor pc) => Components.AOESequence.Draw(Arena, ActiveAOEs(pcSlot, pc));
    public override void OnActorDestroyed(Actor actor) => _casts.Remove(actor.InstanceID);
}
