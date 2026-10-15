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
    public bool Split;
    public int DestroyedSide; // -1 west, +1 east; impending destruction is separate from current bounds
    public DateTime Destruction;

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
        }
    }
    public override void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4)
    {
        if (updateID != 0x8000000Du)
            return;
        switch (param1)
        {
            case 0u: // duty completion/reset
            case 1u:
                Split = false;
                DestroyedSide = 0;
                Destruction = default;
                Arena.Center = ArenaCenter;
                Arena.Bounds = InitialBounds;
                break;
            case 2u:
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
        if (DestroyedSide != 0)
            hints.Add(DestroyedSide < 0 ? "West platform is being destroyed - land east!" : "East platform is being destroyed - land west!");
    }
}

sealed class FlatlinerKnockUp(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID._Weaponskill_FlatlinerKnockup, 15f, true)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !ArenaChanges.SplitBounds.Contains(pos - ArenaChanges.ArenaCenter);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // At least two players must reach each platform for the four later towers.
        // Alternating stable party slots distributes tanks/healers/DPS without chasing current neighbors.
        var bounds = (slot & 1) == 0 ? ArenaChanges.WestBounds : ArenaChanges.EastBounds;
        foreach (var kb in Casters)
            hints.AddForbiddenZone(new SDKnockbackInComplexPolygonAwayFromOrigin(bounds.Center, kb.Origin, kb.Distance, bounds.Shape.Offset(-0.75f)), kb.Activation.AddSeconds(-0.3d));
    }
}

sealed class ExplosionTower(BossModule module) : Components.CastTowers(module, (uint)AID.ExplosionKnockUp, 4f, 1, 8)
{
    // Walking cannot cross the gap; only advertise towers on the actor's current platform.
    private Tower[] _local = [];
    private readonly ArenaChanges _arena = module.FindComponent<ArenaChanges>()!;
    public override ReadOnlySpan<Tower> ActiveTowers(int slot, Actor actor)
    {
        _local = Towers.Where(t => Math.Sign(t.Position.X - 100f) == Math.Sign(actor.Position.X - 100f)).Select(t =>
        {
            t.MaxSoakers = _arena.DestroyedSide != 0 && Math.Sign(t.Position.X - 100f) == _arena.DestroyedSide ? 8 : 1;
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
}

sealed class ExplosionKnockUp(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.ExplosionKnockUp, 25f, true, shape: new AOEShapeCircle(4f))
{
    private readonly ArenaChanges _arena = module.FindComponent<ArenaChanges>()!;
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => !ArenaChanges.SplitBounds.Contains(pos - ArenaChanges.ArenaCenter) || _arena.DestroyedSide != 0 && Math.Sign(pos.X - 100f) == _arena.DestroyedSide;
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in Casters)
        {
            // Scope landing constraints to affected points; another tower must never constrain this launch.
            var bounds = _arena.DestroyedSide switch { -1 => ArenaChanges.EastBounds, 1 => ArenaChanges.WestBounds, _ => ArenaChanges.SplitBounds };
            var unsafeLanding = new SDKnockbackInComplexPolygonAwayFromOrigin(bounds.Center, kb.Origin, kb.Distance, bounds.Shape.Offset(-0.75f));
            hints.AddForbiddenZone(new SDIntersection([new SDCircle(kb.Origin, 4f), unsafeLanding]), kb.Activation.AddSeconds(-0.3d));
        }
        // Everyone on the doomed side has to launch, even if a party member already occupies the tower.
        if (_arena.DestroyedSide != 0 && Math.Sign(actor.Position.X - 100f) == _arena.DestroyedSide)
        {
            var local = Casters.Where(k => Math.Sign(k.Origin.X - 100f) == _arena.DestroyedSide).ToArray();
            if (local.Length != 0)
                hints.AddForbiddenZone(new SDIntersection(local.Select(k => (ShapeDistance)new SDInvertedCircle(k.Origin, 4f)).ToArray()), local.Min(k => k.Activation).AddSeconds(-0.3d));
        }
    }
}

sealed class ArcadionAvalancheToss(BossModule module) : Components.SimpleAOEGroups(module,
    [(uint)AID.ArcadionAvalancheTossVariant1, (uint)AID._Weaponskill_ArcadionAvalanche1,
     (uint)AID.ArcadionAvalancheTossVariant3, (uint)AID.ArcadionAvalancheToss], new AOEShapeRect(40f, 20f));

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
