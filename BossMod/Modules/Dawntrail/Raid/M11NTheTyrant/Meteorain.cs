namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

// Unlike the generic tankbuster tether, these can initially target a rock outside the party,
// and the intercepted buster is single-target. Assign tanks to distinct source-to-rock segments.
sealed class ForegoneFatality(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, (Actor Source, Actor Target, DateTime Activation)> _tethers = [];
    private readonly Dictionary<Actor, DateTime> _deadlines = [];
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == (uint)TetherID._Gen_TankInterceptTether && WorldState.Actors.Find(tether.Target) is { } target)
        {
            if (!_deadlines.TryGetValue(source, out var deadline) || deadline <= WorldState.CurrentTime)
                _deadlines[source] = deadline = WorldState.FutureTime(8d);
            _tethers[source.InstanceID] = (source, target, deadline);
        }
    }
    public override void OnUntethered(Actor source, in ActorTetherInfo tether) => _tethers.Remove(source.InstanceID);
    public override void Update()
    {
        foreach (var (id, t) in _tethers.ToArray())
            if (t.Source.IsDestroyed || t.Target.IsDestroyed || WorldState.Actors.Find(id) != t.Source || WorldState.Actors.Find(t.Target.InstanceID) != t.Target)
                _tethers.Remove(id);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        foreach (var (id, t) in _tethers.ToArray())
            if (t.Source == actor || t.Target == actor)
                _tethers.Remove(id);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID._Spell_ForegoneFatality)
        {
            _tethers.Remove(caster.InstanceID);
            _deadlines.Remove(caster);
        }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_tethers.Count == 0)
            return;
        if (actor.Role == Role.Tank && !_tethers.Values.Any(t => t.Target == actor))
            hints.Add("Intercept a portal tether!");
        else if (actor.Role != Role.Tank && _tethers.Values.Any(t => t.Target == actor))
            hints.Add("Pass the tether to a tank!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var tethers = _tethers.Values.OrderBy(t => t.Source.Position.Z).ToArray();
        foreach (var t in tethers)
        {
            var targetSlot = Raid.FindSlot(t.Target.InstanceID);
            if (targetSlot >= 0)
                hints.AddPredictedDamage(new(1ul << targetSlot), t.Activation, AIHints.PredictedDamageType.Tankbuster);
        }
        if (actor.Role != Role.Tank || tethers.Any(t => t.Target == actor))
            return;
        var tanks = Raid.WithSlot().ToArray().Where(p => p.Item2.Role == Role.Tank && !p.Item2.IsDead && !tethers.Any(t => t.Target == p.Item2)).OrderBy(p => p.Item1).ToArray();
        var index = Array.FindIndex(tanks, p => p.Item2 == actor);
        var available = tethers.Where(t => t.Target.Role != Role.Tank).ToArray();
        if (index < 0 || index >= available.Length)
            return;
        var chosen = available[index];
        // The middle of the segment stays away from both the portal and the rock's collision body.
        var intercept = chosen.Source.Position + 0.5f * (chosen.Target.Position - chosen.Source.Position);
        hints.AddForbiddenZone(new SDInvertedCircle(intercept, 1f), chosen.Activation.AddSeconds(-0.5d));
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var t in _tethers.Values)
            Arena.AddLine(t.Source.Position, t.Target.Position, t.Target.Role == Role.Tank ? Colors.Safe : Colors.Danger);
    }
}

sealed class Tyrannhilation(BossModule module) : Components.GenericLineOfSightAOE(module, default, 60f, safeInsideHitbox: false)
{
    private readonly Dictionary<ulong, Actor> _rocks = [];
    private readonly HashSet<ulong> _shelters = [];
    private DateTime _first;
    private DateTime _second;
    private bool _active;
    private bool _firstResolved;
    private Actor? _caster;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID._Weaponskill_DoubleTyrannhilation1)
        {
            _active = true;
            _firstResolved = false;
            _shelters.Clear();
            _caster = caster;
            _first = Module.CastFinishAt(spell, 1.1d);
            _second = _first.AddSeconds(3.5d);
        }
        else if (spell.Action.ID == (uint)AID._Weaponskill_HiddenTyrannhilation)
            _second = Module.CastFinishAt(spell);
        RefreshCover();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        switch ((AID)spell.Action.ID)
        {
            case AID._Ability_CosmicKiss:
                _rocks[caster.InstanceID] = caster;
                break;
            case AID._Ability_1:
                _rocks.Remove(caster.InstanceID);
                break;
            case AID._Weaponskill_Shockwave:
                _firstResolved = true;
                break;
            case AID._Weaponskill_HiddenTyrannhilation:
                _active = false;
                break;
        }
        RefreshCover();
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _rocks.Remove(actor.InstanceID);
        if (_caster == actor)
            _active = false;
        RefreshCover();
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID._Weaponskill_DoubleTyrannhilation1 or (uint)AID._Weaponskill_HiddenTyrannhilation
            && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
        {
            _active = false;
            RefreshCover();
        }
    }
    public override void Update() => RefreshCover();
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_active && Blockers.Count == 0)
            hints.Add("No reliable two-rock cover found!");
        else
            base.AddHints(slot, actor, hints);
    }
    private void RefreshCover()
    {
        Safezones.Clear();
        if (!_active || _caster == null || _caster.IsDestroyed)
        {
            Modify(null, []);
            return;
        }
        var origin = _caster.Position;
        var rocks = _rocks.Values.Where(r => !r.IsDestroyed && !r.IsDead && WorldState.Actors.Find(r.InstanceID) == r).ToArray();
        // A far rock is eligible only when its entire shadow is inside a closer rock's shadow.
        // This excludes the six rocks destroyed by the first blast, before they actually disappear.
        var cover = rocks.Where(r => _firstResolved ? _shelters.Contains(r.InstanceID) : rocks.Any(near =>
        {
            var farDelta = r.Position - origin;
            var nearDelta = near.Position - origin;
            var fd = farDelta.Length();
            var nd = nearDelta.Length();
            return near != r && nd > near.HitboxRadius && fd > nd + r.HitboxRadius
                && Math.Abs((Angle.FromDirection(farDelta) - Angle.FromDirection(nearDelta)).Normalized().Rad)
                    + MathF.Asin(Math.Clamp(r.HitboxRadius / fd, 0f, 1f)) <= MathF.Asin(Math.Clamp(near.HitboxRadius / nd, 0f, 1f));
        })).ToArray();
        if (!_firstResolved)
        {
            _shelters.Clear();
            foreach (var rock in cover)
                _shelters.Add(rock.InstanceID);
        }
        var activation = _firstResolved ? _second : _first;
        Modify(origin, cover.Select(r => (r.Position, r.HitboxRadius)), activation);
        AddSafezone(activation);
        // Empty cover must warn/forbid the arena, never silently imply safety.
        if (Safezones.Count == 0)
            Safezones.Add(new(new AOEShapeCircle(60f), origin, activation: activation, shapeDistance: new SDCircle(origin, 60f)));
    }
}
