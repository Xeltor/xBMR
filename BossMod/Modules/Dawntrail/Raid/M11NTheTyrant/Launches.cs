namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

sealed class ExplosionKnockUp(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.ExplosionKnockUp, 25f, true, shape: new AOEShapeCircle(4f))
{
    private readonly ArenaChanges _arena = module.FindComponent<ArenaChanges>()!;
    private readonly Dictionary<ulong, int> _landings = [];
    private readonly Dictionary<ulong, ulong> _required = [];
    private Actor[] _roster = [];
    private int _destroyedSide = int.MinValue;
    private bool _dirty = true;
    private bool _insufficient;

    public int LandingSide(Actor actor)
    {
        EnsurePlan();
        return _landings.GetValueOrDefault(actor.InstanceID, _arena.DestroyedSide != 0 ? -_arena.DestroyedSide : Math.Sign(actor.Position.X - 100f));
    }
    public ulong RequiredTower(Actor actor)
    {
        EnsurePlan();
        return _required.GetValueOrDefault(actor.InstanceID);
    }
    private void EnsurePlan()
    {
        if (Casters.Count == 0)
        {
            _landings.Clear();
            _required.Clear();
            _roster = [];
            _dirty = true;
            return;
        }
        var members = Raid.WithoutSlot(excludeAlliance: true, excludeNPCs: true).Where(a => !a.IsDestroyed).OrderBy(a => a.InstanceID).ToArray();
        if (!_dirty && _destroyedSide == _arena.DestroyedSide && members.Length == _roster.Length
            && members.Select((a, i) => ReferenceEquals(a, _roster[i])).All(same => same))
            return;
        _dirty = false;
        _destroyedSide = _arena.DestroyedSide;
        _roster = members;
        _landings.Clear();
        _required.Clear();
        var onFloor = members.Where(a => ArenaChanges.SplitBounds.Contains(a.Position - ArenaChanges.ArenaCenter)).ToArray();
        foreach (var member in members)
            _landings[member.InstanceID] = _destroyedSide != 0 ? -_destroyedSide : Math.Sign(member.Position.X - 100f);

        foreach (var side in new[] { -1, 1 })
        {
            var local = onFloor.Where(a => Math.Sign(a.Position.X - 100f) == side).ToList();
            var towers = Casters.Where(k => Math.Sign(k.Origin.X - 100f) == side).OrderBy(k => k.Origin.Z).ThenBy(k => k.ActorID).ToArray();
            if (_destroyedSide == side)
            {
                // Everyone must leave a doomed platform; sharing its tower is mandatory.
                foreach (var member in local)
                    if (towers.Length != 0)
                        _required[member.InstanceID] = towers.OrderBy(k => (member.Position - k.Origin).LengthSq()).ThenBy(k => k.ActorID).First().ActorID;
            }
            else
            {
                // Use identities, not nearest-player observations that can differ across clients.
                foreach (var tower in towers)
                {
                    var chosen = local.FirstOrDefault();
                    if (chosen == null)
                        break;
                    _required[chosen.InstanceID] = tower.ActorID;
                    local.Remove(chosen);
                }
            }
        }
        if (_destroyedSide == 0)
        {
            // Staying on the starting island is the default. Transfer only the deficit needed
            // for two future soakers, never exporting the last two from the donor island.
            foreach (var side in new[] { -1, 1 })
            {
                var local = onFloor.Where(a => Math.Sign(a.Position.X - 100f) == side).ToArray();
                var opposite = onFloor.Count(a => Math.Sign(a.Position.X - 100f) == -side);
                var transfers = Math.Min(Math.Max(0, 2 - opposite), Math.Max(0, local.Length - 2));
                foreach (var member in local.Where(a => _required.ContainsKey(a.InstanceID)).OrderBy(a => a.InstanceID).Take(transfers))
                    _landings[member.InstanceID] = -side;
            }
        }
        _insufficient = _destroyedSide == 0 && (onFloor.Count(a => _landings[a.InstanceID] < 0) < 2 || onFloor.Count(a => _landings[a.InstanceID] > 0) < 2);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction)
            _dirty = true;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            _dirty = true;
        if (Casters.Count == 0)
        {
            _landings.Clear();
            _required.Clear();
            _roster = [];
            _dirty = true;
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (Casters.RemoveAll(k => k.ActorID == actor.InstanceID) != 0)
            _dirty = true;
    }
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => !ArenaChanges.SplitBounds.Contains(pos - ArenaChanges.ArenaCenter) || _arena.DestroyedSide != 0 && Math.Sign(pos.X - 100f) == _arena.DestroyedSide;
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        EnsurePlan();
        if (Casters.Count != 0 && _insufficient)
            hints.Add("Cannot plan two living players per island - recover party/tower coverage!");
        if (Casters.Any(k => !_required.ContainsValue(k.ActorID)))
            hints.Add("A current meteor tower has no reachable assigned soaker!");
        if (PreparingAvalanche())
            hints.Add("Prepare near your island's meteor for Avalanche!");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (Casters.Count != 0 && RequiredTower(actor) is var required && required != 0)
            hints.Add(LandingSide(actor) < 0 ? "Use your meteor - land west!" : "Use your meteor - land east!", false);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var side = LandingSide(actor);
        var bounds = side < 0 ? ArenaChanges.WestBounds : ArenaChanges.EastBounds;
        var required = RequiredTower(actor);
        foreach (var kb in Casters)
        {
            var unsafeLanding = new SDKnockbackInComplexPolygonAwayFromOrigin(bounds.Center, kb.Origin, kb.Distance, side < 0 ? ArenaChanges.WestLanding : ArenaChanges.EastLanding);
            hints.AddForbiddenZone(new SDIntersection([new SDCircle(kb.Origin, 4f), unsafeLanding]), kb.Activation.AddSeconds(-0.3d));
            if (kb.ActorID == required)
                hints.AddForbiddenZone(new SDInvertedCircle(kb.Origin, 4f), kb.Activation.AddSeconds(-0.3d));
            // The two-tower cue precedes the facing cue. Keep everyone close enough to react
            // once the doomed side is known; this is preparation, not an early launch assignment.
            if (PreparingAvalanche() && Math.Sign(actor.Position.X - 100f) == Math.Sign(kb.Origin.X - 100f))
                hints.AddForbiddenZone(new SDInvertedCircle(kb.Origin, 12f), kb.Activation.AddSeconds(-5.3d));
        }
    }
    private bool PreparingAvalanche() => _arena.DestroyedSide == 0 && Casters.Count == 2
        && Casters.Any(k => k.Origin.X < 100f) && Casters.Any(k => k.Origin.X > 100f);
}

// Like Prishe's AuroralUppercutHint, these green shapes are launch positions, not hazards.
// They use the same assigned landing floor as the knockback AI; they never replace its constraints.
sealed class LaunchPositionHints(BossModule module) : Components.GenericAOEs(module)
{
    private readonly FlatlinerKnockUp _flat = module.FindComponent<FlatlinerKnockUp>()!;
    private readonly ExplosionKnockUp _towers = module.FindComponent<ExplosionKnockUp>()!;
    private readonly Dictionary<(ulong, DateTime, int), AOEInstance[]> _cache = [];
    private AOEInstance[] _output = [];
    private static readonly AOEShapeDonutSector _marker = new(1.5f, 3.5f, 5f.Degrees());
    private static readonly AOEShapeRect _landingInterior = new(19.25f, 9.25f, 19.25f);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_flat.Casters.Count == 0 && _towers.Casters.Count == 0)
        {
            _cache.Clear();
            return _output = [];
        }
        if (actor.IsDead)
            return [];
        var sources = _flat.Casters.Count != 0 ? _flat.Casters : _towers.Casters;
        var side = _flat.Casters.Count != 0 ? _flat.LandingSide(actor) : _towers.LandingSide(actor);
        var required = _flat.Casters.Count != 0 ? 0 : _towers.RequiredTower(actor);
        var result = new List<AOEInstance>();
        foreach (var kb in sources)
        {
            if (kb.Distance > 15f && (required != 0 ? kb.ActorID != required : kb.Shape?.Check(actor.Position, kb.Origin, kb.Direction) != true))
                continue;
            var key = (kb.ActorID, kb.Activation, side);
            if (!_cache.TryGetValue(key, out var markers))
            {
                var bounds = side < 0 ? ArenaChanges.WestBounds : ArenaChanges.EastBounds;
                var list = new List<AOEInstance>();
                for (var i = 0; i < 72; ++i)
                {
                    var dir = (i * 5f).Degrees();
                    // For an annular sector, rectangle-coordinate extrema occur at radial
                    // endpoints, angular endpoints, or cardinal axes within the sector.
                    var angles = new List<Angle> { dir - _marker.HalfAngle, dir + _marker.HalfAngle };
                    for (var axis = 0; axis < 4; ++axis)
                    {
                        var a = (axis * 90f).Degrees();
                        if ((a - dir).Normalized().Abs().Rad <= _marker.HalfAngle.Rad)
                            angles.Add(a);
                    }
                    if (angles.All(a => new[] { _marker.InnerRadius, _marker.OuterRadius }.All(r =>
                        Arena.InBounds(kb.Origin + r * a.ToDirection())
                        && _landingInterior.Check(kb.Origin + (r + kb.Distance) * a.ToDirection(), bounds.Center, default))))
                        list.Add(new(_marker, kb.Origin, dir, kb.Activation, Colors.SafeFromAOE, risky: false, actorID: kb.ActorID));
                }
                _cache[key] = markers = list.ToArray();
            }
            if (markers.Length != 0)
                result.Add(markers.MinBy(a => (actor.Position - (a.Origin + 2.5f * a.Rotation.ToDirection())).LengthSq()));
        }
        return _output = result.ToArray();
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (ActiveAOEs(slot, actor).Length != 0)
            hints.Add((_flat.Casters.Count != 0 ? _flat.LandingSide(actor) : _towers.LandingSide(actor)) < 0
                ? "Stand in the green wedge - land west!" : "Stand in the green wedge - land east!", false);
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, Colors.Safe, 2f);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) { }
}
