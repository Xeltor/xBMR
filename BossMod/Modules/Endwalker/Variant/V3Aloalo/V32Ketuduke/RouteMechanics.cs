namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

// Route components adapted from BossMod Reborn 7.5.8.10 (croizat, ported by LTS).
sealed class BubbleNetSand(BossModule module) : Components.RaidwideCast(module, (uint)AID.BubbleNetSandAOE);

sealed class Roar(BossModule module) : Components.CastHint(module, (uint)AID.Roar, "Zaratan adds spawning");

sealed class TidalWave(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.TidalWaveAOE, 27, ignoreImmunes: true, shape: new AOEShapeRect(46, 23), kind: Kind.DirForward)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var c in Casters)
        {
            var displacement = c.Distance * c.Direction.ToDirection();
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareFixedDirection(Module.Center, displacement, Module.Bounds.Radius), c.Activation, c.ActorID);
            hints.GoalZones.Add(AIHints.GoalProximity(c.Origin, 4, 60));
        }
    }
}

sealed class Hydrosurge(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance? _aoe;
    private static readonly AOEShapeRect _shape = new(20, 5, 20);

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        if (_aoe is { } aoe)
            _aoesBuffer.Add(aoe);
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HydrosurgeAOE)
            _aoe = new(_shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HydrosurgeAOE && !spell.EventHappened && _aoe?.ActorID == caster.InstanceID)
            _aoe = null;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.FlukeTyphoonAnila)
            _aoe = null;
    }
}

sealed class WavefoamBubbles(BossModule module) : Components.GenericTowers(module)
{
    private bool _ogrebonBubbled;

    public override void Update()
        => _ogrebonBubbled = Module.Enemies((uint)OID.AloaloOgrebon).Any(o => !o.IsDeadOrDestroyed && o.FindStatus((uint)SID.Bubble) != null);

    public override ReadOnlySpan<Tower> ActiveTowers(int slot, Actor actor)
        => _ogrebonBubbled || actor.FindStatus((uint)SID.BubbleNet) != null ? [] : base.ActiveTowers(slot, actor);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.WavefoamBubble && !actor.IsDeadOrDestroyed && !Towers.Any(t => t.ActorID == actor.InstanceID))
            Towers.Add(new(actor.Position, 2, activation: WorldState.FutureTime(12), actorID: actor.InstanceID));
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.WavefoamBubble)
            Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.FlukeTyphoonAnila or (uint)AID.AerialShockAOE or (uint)AID.UpdraftStoneAOE)
            Towers.Clear();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_ogrebonBubbled)
        {
            foreach (var tower in Towers)
                hints.AddForbiddenZone(tower.ShapeDistance!);
            return;
        }
        base.AddAIHints(slot, actor, assignment, hints);
    }
}

sealed class FlukeTyphoonAnila(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.FlukeTyphoonAnila, 20, ignoreImmunes: true, shape: new AOEShapeRect(40, 20), kind: Kind.DirForward)
{
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
        => actor.FindStatus((uint)SID.BubbleNet) != null ? base.ActiveKnockbacks(slot, actor) : [];
}

sealed class AerialShock(BossModule module) : Components.GenericAOEs(module, (uint)AID.AerialShockAOE)
{
    private AOEInstance? _aoe;
    private static readonly AOEShapeCircle _shape = new(35);

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        if (_aoe is not { } aoe)
            return CollectionsMarshal.AsSpan(_aoesBuffer);
        var ogrebonBubbled = false;
        foreach (var o in Module.Enemies((uint)OID.AloaloOgrebon))
            ogrebonBubbled |= o.FindStatus((uint)SID.Bubble) != null;
        _aoesBuffer.Add(ogrebonBubbled ? aoe with { Color = Colors.SafeFromAOE, Risky = false } : aoe);
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    // WavefoamBubbles supplies the reachable setup regions. A planar full-arena
    // exclusion would also reject those bubbles before the updraft changes height.
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) { }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_aoe != null && !Module.Enemies((uint)OID.AloaloOgrebon).Any(o => !o.IsDeadOrDestroyed && o.FindStatus((uint)SID.Bubble) != null)
            && actor.FindStatus((uint)SID.BubbleNet) == null)
            hints.Add("Enter a bubble before the updraft!");
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _aoe = new(_shape, caster.Position, default, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _aoe = null;
    }
}

sealed class HundredLashings(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<(Actor Caster, AOEInstance AOE, bool Bubbled)> _aoes = [];
    private static readonly AOEShapeCone _shape = new(60, 90.Degrees());

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        foreach (var a in _aoes)
        {
            // bubbled half safe
            _aoesBuffer.Add(a.Bubbled ? (a.AOE with { Color = Colors.SafeFromAOE, Risky = false }) : a.AOE);
        }
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.HundredLashings)
            return;
        var bubbled = caster.FindStatus((uint)SID.Bubble) != null;
        _aoes.Add((caster, new(_shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell)), bubbled));
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID != (uint)SID.Bubble || actor.OID != (uint)OID.AloaloZaratan)
            return;
        var i = _aoes.FindIndex(a => a.Caster == actor);
        if (i >= 0)
        {
            var e = _aoes[i];
            _aoes[i] = (e.Caster, e.AOE, true);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.HundredLashings or (uint)AID.HundredLashingsVisual or (uint)AID.HundredLashingsElevated)
            _aoes.RemoveAll(a => a.Caster == caster || a.Caster.InstanceID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.HundredLashings or (uint)AID.HundredLashingsElevated)
        {
            _aoes.RemoveAll(a => a.Caster == caster || a.AOE.Origin.AlmostEqual(caster.Position, 1));
            ++NumCasts;
        }
    }
}
