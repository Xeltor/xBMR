namespace BossMod.Shadowbringers.Raid.E01EdenPrime;

// These are arrival regions for the guide's N/S proximity strategy, not calibrated
// damage-falloff boundaries. Their centers are 1.5y inside the measured floor edge.
sealed class ParadisalDive(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, (Actor Caster, DateTime Activation)> _landings = [];
    private readonly HashSet<Actor> _uncertain = [];
    private readonly HashSet<Actor> _presentAtActivation = [.. module.WorldState.Actors];
    private static readonly WPos North = new(100f, 82f);
    private static readonly WPos South = new(100f, 118f);
    private static readonly AOEShapeCircle Arrival = new(1f);
    private static readonly ShapeDistance OutsideArrivals = new SDInvertedUnion([new SDCircle(North, 1f), new SDCircle(South, 1f)]);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.GuardianOfParadise && !actor.IsTargetable && !actor.IsDeadOrDestroyed)
        {
            if (_uncertain.Contains(actor))
                return; // A duplicate creation cannot restart an expired estimate.
            _uncertain.RemoveWhere(a => a.InstanceID == actor.InstanceID);
            // A bootstrap callback has no creation age. Move immediately until a
            // current cast supplies its deadline; never grant it a fresh 9.7s.
            var activation = _presentAtActivation.Remove(actor) ? WorldState.CurrentTime : WorldState.FutureTime(9.7d);
            if (!_landings.TryGetValue(actor.InstanceID, out var existing) || !ReferenceEquals(existing.Caster, actor))
                _landings[actor.InstanceID] = (actor, activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.GuardianOfParadise && spell.Action.ID == (uint)AID.ParadisalDive)
        {
            _uncertain.Remove(caster);
            _landings[caster.InstanceID] = (caster, Module.CastFinishAt(spell));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ParadisalDive)
            Retire(caster);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.ParadisalDive)
            Retire(caster);
    }

    private void Retire(Actor actor)
    {
        _uncertain.Remove(actor);
        if (_landings.TryGetValue(actor.InstanceID, out var landing) && ReferenceEquals(landing.Caster, actor))
            _landings.Remove(actor.InstanceID);
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor.IsTargetable)
            Retire(actor);
    }
    public override void OnActorDestroyed(Actor actor) => Retire(actor);

    public override void Update()
    {
        _uncertain.RemoveWhere(a => a.IsDeadOrDestroyed || a.IsTargetable || WorldState.Actors.Find(a.InstanceID) != a);
        foreach (var (id, landing) in _landings.ToArray())
            if (landing.Caster.IsDeadOrDestroyed || landing.Caster.IsTargetable || WorldState.Actors.Find(id) != landing.Caster)
                _landings.Remove(id);
            else if (WorldState.CurrentTime > landing.Activation.AddSeconds(2d))
            {
                _landings.Remove(id);
                _uncertain.Add(landing.Caster);
            }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_uncertain.Count != 0)
            hints.Add("Guardian landing resolution not observed; watch the proximity markers!");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_landings.Count != 0)
            hints.Add("Proximity damage: move to the north or south edge!", OutsideArrivals.Distance(actor.Position) < 0);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_landings.Count == 0)
            return;
        var affected = Raid.WithSlot().Aggregate(default(BitMask), (mask, p) => mask.WithBit(p.Item1));
        foreach (var landing in _landings.Values)
        {
            hints.AddForbiddenZone(OutsideArrivals, landing.Activation);
            if (affected.Any())
                hints.AddPredictedDamage(affected, landing.Activation);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_landings.Count != 0)
        {
            Arrival.Outline(Arena, North, default, Colors.Safe);
            Arrival.Outline(Arena, South, default, Colors.Safe);
        }
    }
}

// Conservative rear-side positioning policy while the instant frontal cleave's
// exact angle is unmeasured. 6y sheet range + the recorded 3y caster hitbox gives
// the near-add envelope. Do not draw this 180-degree policy as a measured AOE.
sealed class GuardianRearSafety(BossModule module) : Components.Cleave(module, (uint)AID.ManaSlice,
    new AOEShapeCone(9f, 90.Degrees()), [(uint)OID.GuardianOfParadise], activeWhileCasting: false)
{
    public override void DrawArenaForeground(int pcSlot, Actor pc) { }
}

sealed class EternalBreath(BossModule module) : BossComponent(module)
{
    public DateTime Activation;
    private Actor? _scene;
    private Actor? _eden;
    private int _cue;
    private bool _resolved;
    private bool _cueLost;

    private void Clear(bool resolved = false)
    {
        Activation = default;
        _scene = _eden = null;
        _cue = 0;
        _cueLost = false;
        _resolved = resolved;
    }

    private void Predict(int cue, double delay)
    {
        // Each later reliable cue may refine the estimate once. Repeated or
        // reordered callbacks must not keep postponing the same raidwide.
        if (_resolved || cue <= _cue)
            return;
        _cue = cue;
        Activation = WorldState.FutureTime(delay);
        _cueLost = false;
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        // This EObj owns the arena scene. Other copies of its OID do not arm it.
        if (actor.OID != 2007457 || !actor.Position.AlmostEqual(Module.Center, 1f))
            return;
        switch (state)
        {
            case 0x01000200: // adds defeated: begin cinematic
                if (!_resolved && _cue == 0)
                    _scene = actor;
                Predict(1, 48.5d);
                break;
            case 0x04000800: // later cinematic cue
                if (!_resolved && _cue < 3)
                    _scene = actor;
                Predict(3, 18.5d);
                break;
            case 0x00028000: // encounter completion
                Clear(true);
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.Eden && spell.Action.ID == (uint)AID.PrimevalStasis)
        {
            if (!_resolved && _cue < 2)
                _eden = caster;
            Predict(2, 43.4d);
        }
        else if (caster.OID == (uint)OID.Helper && spell.Action.ID == (uint)AID.EternalBreath)
        {
            Clear(true);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (ReferenceEquals(actor, _scene) || ReferenceEquals(actor, _eden))
            Clear();
    }

    public override void Update()
    {
        if (_scene is { } scene && (scene.IsDestroyed || WorldState.Actors.Find(scene.InstanceID) != scene)
            || _eden is { } eden && (eden.IsDestroyed || WorldState.Actors.Find(eden.InstanceID) != eden))
            Clear();
        else if (Activation != default && WorldState.CurrentTime > Activation.AddSeconds(2d))
        {
            // Lost resolution is not proof of cancellation or a new damage time.
            // Withdraw stale healing pressure and keep an explicit uncertainty.
            Activation = default;
            _cueLost = true;
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Activation != default)
            hints.Add("Eternal Breath: raidwide damage after the cinematic!");
        else if (_cueLost)
            hints.Add("Eternal Breath resolution not observed; watch the cinematic!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Activation == default)
            return;
        var affected = Raid.WithSlot().Aggregate(default(BitMask), (mask, p) => mask.WithBit(p.Item1));
        if (affected.Any())
            hints.AddPredictedDamage(affected, Activation);
    }
}
