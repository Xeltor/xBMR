namespace BossMod.Dawntrail.Alliance.A34Promathia;

sealed class EmptySalvation(BossModule module) : Components.RaidwideCast(module, (uint)AID.EmptySalvation);

sealed class Explosion(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Explosion, 16f);

sealed class WheelofImpregnability(BossModule module) : Components.GenericAOEs(module, (uint)AID.WheelOfImpregnabilityFire)
{
    private ActorCastInfo? _cast;
    private Actor? _source;
    private AOEInstance[] _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        // The ring travels with Promathia when he repositions for Pestilent Penance.
        // Only the resolution deadline is fixed by the visual cast.
        if (_source != null && _aoes.Length != 0)
            _aoes[0].Origin = _source.Position;
        return _aoes;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.WheelOfImpregnabilityCast && !ReferenceEquals(_cast, spell))
        {
            _cast = spell;
            _source = caster;
            // The uncast helper resolves about 10.6s after the boss visual finishes.
            // Freeze the forecast: moving it forward every frame prevents AI departure.
            _aoes = [new(new AOEShapeCircle(13f), caster.Position, activation: Module.CastFinishAt(spell, 10.6d))];
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ReferenceEquals(_cast, spell) && !spell.EventHappened)
        {
            _cast = null;
            _aoes = [];
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _cast = null;
            _aoes = [];
        }
    }
}

sealed class BastionOfTwilight(BossModule module) : Components.GenericAOEs(module, (uint)AID.BastionOfTwilightFire)
{
    private ActorCastInfo? _cast;
    private Actor? _source;
    private AOEInstance[] _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        // The ring travels with Promathia when he repositions for Pestilent Penance.
        // Only the resolution deadline is fixed by the visual cast.
        if (_source != null && _aoes.Length != 0)
            _aoes[0].Origin = _source.Position;
        return _aoes;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.BastionOfTwilightCast && !ReferenceEquals(_cast, spell))
        {
            _cast = spell;
            _source = caster;
            _aoes = [new(new AOEShapeDonut(8f, 50f), caster.Position, activation: Module.CastFinishAt(spell, 10.6d))];
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ReferenceEquals(_cast, spell) && !spell.EventHappened)
        {
            _cast = null;
            _aoes = [];
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _cast = null;
            _aoes = [];
        }
    }
}

sealed class EmptySalvationPuddles(BossModule module) : Components.GenericAOEs(module, warningText: "Heavy puddle!")
{
    // Nominal small footprint follows the concentric 4y tower; the expanded
    // footprint follows the 8y Infernal Deliverance helper. Physical edges need live checking.
    private readonly Dictionary<ulong, (Actor Source, float Radius, DateTime Activation)> _sources = [];
    private ActorCastInfo? _salvation;
    private readonly AOEShapeCircle _small = new(4f), _large = new(8f);
    private readonly List<AOEInstance> _aoes = [];

    public override void OnActorCreated(Actor actor)
    {
        // The same OID also supplies a central link visual, not one of the six pools.
        if (actor.OID != (uint)OID.Actor1ec097 || actor.Position.InCircle(new(-820f, -820f), 1f))
            return;
        if (_sources.TryGetValue(actor.InstanceID, out var previous) && ReferenceEquals(previous.Source, actor))
            return;
        var cast = Module.PrimaryActor.CastInfo;
        var activation = cast?.Action.ID == (uint)AID.EmptySalvation ? Module.CastFinishAt(cast, 0.8d) : WorldState.CurrentTime;
        _sources[actor.InstanceID] = (actor, 4f, activation);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (!_sources.TryGetValue(actor.InstanceID, out var source) || !ReferenceEquals(source.Source, actor))
            return;
        switch (state)
        {
            case 0x00010002u: // small puddle becomes active
            case 0x00011000u: // returns to small after the tower follow-up
                _sources[actor.InstanceID] = (actor, 4f, WorldState.CurrentTime);
                break;
            case 0x04000800u: // tower puddle expands
                _sources[actor.InstanceID] = (actor, 8f, WorldState.CurrentTime);
                break;
            case 0x00040008u: // puddle disappears, before its object is destroyed
                _sources.Remove(actor.InstanceID);
                break;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_sources.TryGetValue(actor.InstanceID, out var source) && ReferenceEquals(source.Source, actor))
            _sources.Remove(actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.EmptySalvation && !ReferenceEquals(_salvation, spell))
        {
            _sources.Clear();
            _salvation = spell;
        }
        else if (spell.Action.ID == (uint)AID.FalseGenesis)
        {
            _sources.Clear();
            _salvation = null;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ReferenceEquals(_salvation, spell) && !spell.EventHappened)
        {
            _sources.Clear();
            _salvation = null;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        var tower = Module.FindComponent<InfernalDeliveranceTower>()?.AssignedTower(slot, actor);
        foreach (var (_, source) in _sources)
        {
            if (source.Source.IsDestroyed)
                continue;
            var soaking = tower is { } t && t.Position.AlmostEqual(source.Source.Position, 1f);
            _aoes.Add(new(source.Radius == 4f ? _small : _large, source.Source.Position, activation: source.Activation,
                risky: !soaking, actorID: source.Source.InstanceID));
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

sealed class PestilentPenance(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PestilentPenance, new AOEShapeRect(50f, 25f));

sealed class Comet(BossModule module) : Components.SpreadFromIcon(module, (uint)IconID.Comet, (uint)AID.Comet, 6f, 4d)
{
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Meteor1 or (uint)AID.Meteor2) // Hey guess what?  The tankbuster in meteor is actually just Comet again!
        {
            Spreads.Clear();
        }
    }
}

sealed class FalseGenesis(BossModule module) : BossComponent(module)
{
    private ActorCastInfo? _cast;
    private DateTime _activation;

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (_cast == null)
            return;
        foreach (var (center, rotation) in A34Promathia.Platforms)
            Arena.ZoneRect(center, rotation, 6.5f, 6.5f, 6.5f, Colors.SafeFromAOE);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_cast != null && A34Promathia.OutsidePlatforms.Contains(actor.Position))
            hints.Add("Move to platform spawn!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Keep the connecting floor available for travel until the cast resolves.
        // A drawing or soft goal alone does not require arrival before it disappears.
        if (_cast != null)
            hints.AddForbiddenZone(A34Promathia.OutsidePlatforms, _activation);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.FalseGenesis && !ReferenceEquals(_cast, spell))
        {
            _cast = spell;
            _activation = Module.CastFinishAt(spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ReferenceEquals(_cast, spell))
            _cast = null;
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == 0x0C && state == 0x00080004u)
            _cast = null;
    }
}

sealed class ArenaChanges(BossModule module) : BossComponent(module)
{
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // Cast-finish also fires on interruption; only successful resolution removes the floor.
        if (spell.Action.ID == (uint)AID.FalseGenesis)
        {
            var bounds = new ArenaBoundsCustom(A34Promathia.Platforms.Select(p => new Square(p.Center, 6.5f, p.Rotation)).ToArray());
            Arena.Bounds = bounds;
            Arena.Center = bounds.Center;
        }
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == 0x0C && state == 0x00080004u)
        {
            Arena.Bounds = new ArenaBoundsCircle(25f);
            Arena.Center = new(-820f, -820f);
        }
    }
}

sealed class MemoryReceptacle(BossModule module) : Components.Adds(module, (uint)OID.MemoryReceptacle);

sealed class EmptyBeleaguer(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EmptyBeleaguer, 6f);
sealed class AuroralDrape(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AuroralDrape, new AOEShapeRect(7f, 3.5f));
sealed class WindsOfPromyvion(BossModule module) : Components.GenericRotatingAOE(module)
{
    private readonly AOEShapeRect _rect = new(16f, 3f);
    private Actor? _caster;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.WindsLeft)
        {
            Sequences.Add(new(_rect, actor.Position, actor.Rotation, 30.Degrees(), WorldState.FutureTime(4.5d), 1.4d, 4));
            _caster = actor;
        }
        if (iconID == (uint)IconID.WindsRight)
        {
            Sequences.Add(new(_rect, actor.Position, actor.Rotation, -30.Degrees(), WorldState.FutureTime(4.5d), 1.4d, 4));
            _caster = actor;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.WindsOfPromyvionCast or (uint)AID.WindsOfPromyvionSpam)
        {
            AdvanceSequence(0, WorldState.CurrentTime);
        }
    }
    public override void OnActorRenderflagsChange(Actor actor, int renderflags)
    {
        if (_caster != null)
        {
            if (actor.InstanceID == _caster.InstanceID && renderflags == 16384)
            {
                Sequences.Clear();
                Update();
            }
        }
    }
}
sealed class EmptySeed(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.EmptySeed, 10f, shape: new AOEShapeCircle(10f))
{
    private readonly Dictionary<ulong, ShapeDistance> _unsafeDirections = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;

        var source = spell.LocXZ;
        var orientation = A34Promathia.Platforms.MinBy(p => (p.Center - source).LengthSq()).Rotation;
        var walls = new SafeWall[8];
        var safeDirections = new ShapeDistance[4];
        for (var i = 0; i < 4; ++i)
        {
            var angle = orientation + (45f + i * 90f).Degrees();
            // Preserve the upstream nominal corner and barrier lengths; these are
            // collision proxies, not newly measured physical boundaries.
            var corner = source + angle.ToDirection() * 8.9f;
            var end1 = corner + (angle + 225f.Degrees()).ToDirection() * 3.5f;
            var end2 = corner + (angle - 225f.Degrees()).ToDirection() * 3.5f;
            walls[2 * i] = new(corner, end1);
            walls[2 * i + 1] = new(corner, end2);
            // Every ray in this polygon reaches one of this corner's barriers.
            // The farthest barrier vertex is within the 10-yalm displacement.
            safeDirections[i] = new SDConvexPolygon([source, end1, corner, end2], true);
        }

        Casters.RemoveAll(k => k.ActorID == caster.InstanceID);
        Casters.Add(new(source, Distance, Module.CastFinishAt(spell), Shape, spell.Rotation, KnockbackKind,
            safeWalls: walls, actorID: caster.InstanceID));
        _unsafeDirections[caster.InstanceID] = new SDUnion([new SDInvertedUnion(safeDirections), new SDCircle(source, 0f)]);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (spell.Action.ID == WatchedAction)
            _unsafeDirections.Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        Casters.RemoveAll(k => k.ActorID == actor.InstanceID);
        _unsafeDirections.Remove(actor.InstanceID);
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index == 0x0C && state == 0x00080004u)
        {
            Casters.Clear();
            _unsafeDirections.Clear();
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in Casters)
        {
            if (!IsImmune(slot, kb.Activation) && kb.Shape!.Check(actor.Position, kb.Origin, kb.Direction)
                && _unsafeDirections.TryGetValue(kb.ActorID, out var unsafeDirections))
                hints.AddForbiddenZone(unsafeDirections, kb.Activation);
        }
    }
}

sealed class MalevolentBlessingCone(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MalevolentBlessingCone, new AOEShapeCone(40f, 11.5f.Degrees()));

sealed class MalevolentBlessingRect(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MalevolentBlessingRect, new AOEShapeRect(50f, 25f));

sealed class PestilentPenanceLink(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PestilentPenanceLink, new AOEShapeRect(50f, 2.5f));

sealed class InfernalDeliveranceTower(BossModule module) : Components.CastTowers(module, (uint)AID.InfernalDeliveranceTower, 4f, 1, 8)
{
    private readonly Dictionary<ulong, ActorCastInfo> _casts = [];
    private readonly Dictionary<ulong, (Actor Actor, ulong CasterID, bool Entered)> _assignments = [];

    public Tower? AssignedTower(int slot, Actor actor)
    {
        if (slot < 0 || actor.IsDeadOrDestroyed)
            return null;
        _assignments.TryGetValue(actor.InstanceID, out var assignment);
        Tower? nearest = null;
        foreach (var tower in Towers)
        {
            var inside = tower.IsInside(actor);
            if (tower.ForbiddenSoakers[slot] || tower.NumInside(Module) is var count && (count > tower.MaxSoakers || count == tower.MaxSoakers && !inside))
                continue;
            if (inside)
            {
                _assignments[actor.InstanceID] = (actor, tower.ActorID, true);
                return tower;
            }
            if (ReferenceEquals(assignment.Actor, actor) && assignment.CasterID == tower.ActorID)
                nearest = tower;
        }
        if (nearest is { })
            return nearest;
        foreach (var tower in Towers)
        {
            if (!tower.ForbiddenSoakers[slot] && tower.NumInside(Module) < tower.MaxSoakers
                && (nearest == null || (tower.Position - actor.Position).LengthSq() < (nearest.Value.Position - actor.Position).LengthSq()))
                nearest = tower;
        }
        if (nearest is { } selected)
            _assignments[actor.InstanceID] = (actor, selected.ActorID, false);
        else
            _assignments.Remove(actor.InstanceID);
        return nearest;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (AssignedTower(slot, actor) is { } tower)
        {
            // Once soaking, stay in this tower through impact. A union of every
            // tower otherwise permits an uptime detour out of the current stack.
            var activation = _assignments[actor.InstanceID].Entered ? default : tower.Activation;
            hints.AddForbiddenZone(tower.InvertedShapeDistance!, activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            if (_casts.TryGetValue(caster.InstanceID, out var previous) && ReferenceEquals(previous, spell))
                return;
            RemoveTower(caster.InstanceID);
            _casts[caster.InstanceID] = spell;
            base.OnCastStarted(caster, spell);
        }
        else if (spell.Action.ID is (uint)AID.EmptySalvation or (uint)AID.FalseGenesis)
        {
            Towers.Clear();
            _casts.Clear();
            _assignments.Clear();
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID is (uint)AID.InfernalDeliveranceTower or (uint)AID.UnmitigatedExplosion)
            RemoveTower(caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.TryGetValue(caster.InstanceID, out var current) && ReferenceEquals(current, spell) && !spell.EventHappened)
            RemoveTower(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => RemoveTower(actor.InstanceID);

    private void RemoveTower(ulong casterID)
    {
        Towers.RemoveAll(t => t.ActorID == casterID);
        _casts.Remove(casterID);
        foreach (var actorID in _assignments.Where(a => a.Value.CasterID == casterID).Select(a => a.Key).ToArray())
            _assignments.Remove(actorID);
    }
}

sealed class InfernalDeliveranceAOE(BossModule module) : Components.SimpleAOEs(module, (uint)AID.InfernalDeliveranceAOE, 8f);

sealed class Meteor(BossModule module) : Components.SpreadFromIcon(module, (uint)IconID.Meteor, (uint)AID.Meteor2, 6f, 5f);  // this is the regular spreads only, Tankbuster is handled in Comet.

sealed class DeadlyRebirth(BossModule module) : Components.RaidwideCast(module, (uint)AID.DeadlyRebirth);

[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "The Combat Reborn Team, HerStolenLight", PrimaryActorOID = (uint)OID.Promathia, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 1117u, NameID = 14779u)]

public sealed class A34Promathia(WorldState ws, Actor primary) : BossModule(ws, primary, new(-820f, -820f), new ArenaBoundsCircle(25f))
{
    internal static readonly (WPos Center, Angle Rotation)[] Platforms =
    [
        (new(-820f, -807f), default),
        (new(-808.724f, -826.5f), 120f.Degrees()),
        (new(-831.258f, -826.5f), -120f.Degrees())
    ];
    internal static readonly ShapeDistance OutsidePlatforms = new SDInvertedUnion(
        Platforms.Select(p => new SDRect(p.Center, p.Rotation, 6.5f, 6.5f, 6.5f)).ToArray());

    public override bool ShouldPrioritizeAllEnemies => true;
}
