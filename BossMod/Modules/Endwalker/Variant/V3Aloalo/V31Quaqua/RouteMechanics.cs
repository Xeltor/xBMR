namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

// Route components adapted from BossMod Reborn 7.5.8.10 (croizat, ported by LTS).
sealed class ElementalImpact(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ElementalImpact, 14);

sealed class ArcaneIntervention(BossModule module) : Components.CastGaze(module, (uint)AID.ArcaneIntervention);

sealed class ArcanePursuit(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<Actor> _lasers = [];
    private static readonly AOEShapeRect _laser = new(50, 3);
    private const float KeepAway = 3.5f;
    private static readonly AOEShapeCircle _contact = new(KeepAway);

    private readonly List<Actor> _charges = [];

    // note: returns a shared buffer, which is overwritten by the next access
    private List<Actor> Charges
    {
        get
        {
            _charges.Clear();
            foreach (var c in Module.Enemies((uint)OID.PursuitCharge))
                if (!c.IsDeadOrDestroyed)
                    _charges.Add(c);
            return _charges;
        }
    }

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        foreach (var c in Charges)
            _aoesBuffer.Add(new(_contact, c.Position));
        // safe zones
        foreach (var l in _lasers)
            if (l.CastInfo != null)
                _aoesBuffer.Add(new(_laser, l.Position, l.CastInfo.Rotation, Module.CastFinishAt(l.CastInfo), Colors.SafeFromAOE, risky: false));
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var charges = Charges;
        if (charges.AnyInRadius(actor.Position, KeepAway))
            hints.Add("Away from orb!");
        else if (_lasers.Count > 0 && charges.Any(c => c.TargetID == actor.InstanceID))
            hints.Add("Lead orb into statue laser!", false);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pursued = false;
        foreach (var c in Charges)
        {
            hints.AddForbiddenZone(new SDCircle(c.Position, KeepAway));
            hints.AddForbiddenZone(new SDCapsule(c.Position, c.Rotation.ToDirection(), 8, KeepAway), WorldState.FutureTime(2));
            pursued |= c.TargetID == actor.InstanceID;
        }

        // A statue's beam is harmless to players. Prefer to bait a pursuing orb
        // into one beam without requiring all players to stand in every beam.
        if (pursued)
        {
            var beams = _lasers.Where(l => l.CastInfo != null).Select(l => _laser.Distance(l.Position, l.CastInfo!.Rotation)).ToArray();
            if (beams.Length != 0)
                hints.GoalZones.Add(p => beams.Any(beam => beam.Distance(p) <= 0) ? 40 : 0);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var c in Charges)
        {
            var target = WorldState.Actors.Find(c.TargetID);
            if (target != null)
            {
                Arena.AddLine(c.Position, target.Position, target == pc ? Colors.Danger : Colors.PlayerGeneric);
            }
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ArcanePursuitLaser or (uint)AID.ArcanePursuitLaserSecond && !_lasers.Contains(caster))
            _lasers.Add(caster);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ArcanePursuitLaser or (uint)AID.ArcanePursuitLaserSecond)
            _lasers.Remove(caster);
    }
}

sealed class ArcaneArmamentsPoison(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<(Actor Actor, DateTime Spawn)> _puddles = [];
    private bool _armamentsActive;

    private const float PuddleStartRadius = 6;
    private const float PuddleMaxRadius = 16;
    private const float GrowSeconds = 14;

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        foreach (var (puddle, spawn) in _puddles)
        {
            var t = (float)(WorldState.CurrentTime - spawn).TotalSeconds;
            var radius = Math.Clamp(PuddleStartRadius + (PuddleMaxRadius - PuddleStartRadius) * (t / GrowSeconds), PuddleStartRadius, PuddleMaxRadius);
            _aoesBuffer.Add(new(new AOEShapeCircle(radius), puddle.Position, default, spawn));
            if (radius < PuddleMaxRadius - 0.5f)
                _aoesBuffer.Add(new(new AOEShapeCircle(PuddleMaxRadius), puddle.Position, default, spawn.AddSeconds(GrowSeconds), Colors.AOE, risky: false));
        }
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ArcaneArmamentsPoisonSpears)
        {
            _armamentsActive = true;
            _puddles.Clear();
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ArcaneArmamentsPoisonSpears)
            _armamentsActive = true; // puddles still inbound briefly after cast
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.PoisonPuddle)
        {
            _puddles.Add((actor, WorldState.CurrentTime));
            _armamentsActive = false;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _puddles.RemoveAll(p => p.Actor == actor);
    }

    public override void OnActorEState(Actor actor, ushort state)
    {
        if (actor.OID == (uint)OID.PoisonPuddle)
            _puddles.RemoveAll(p => p.Actor == actor);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);

        var origins = _originsBuffer;
        origins.Clear();
        if (_puddles.Count > 0)
        {
            foreach (var p in _puddles)
                origins.Add(p.Actor.Position);
        }
        else if (_armamentsActive)
        {
            foreach (var s in Module.Enemies((uint)OID.SpearMarker))
                if (!s.IsDead && s.Tether.ID == (uint)TetherID.WaterSpear)
                    origins.Add(s.Position);
        }
        if (origins.Count < 2)
            return;

        // midpoint between furthest spears
        WPos a = default, b = default;
        var best = 0f;
        for (var i = 0; i < origins.Count; ++i)
        {
            for (var j = i + 1; j < origins.Count; ++j)
            {
                var d = (origins[i] - origins[j]).LengthSq();
                if (d > best)
                {
                    best = d;
                    a = origins[i];
                    b = origins[j];
                }
            }
        }

        var mid = a + (b - a) * 0.5f;
        var toWall = mid - Module.Center;
        if (toWall.LengthSq() < 0.01f)
            return;
        var safe = Module.Center + toWall.Normalized() * (Module.Bounds.Radius - 1.5f);
        hints.GoalZones.Add(AIHints.GoalProximity(safe, 3, 40));
    }

    private readonly List<WPos> _originsBuffer = [];
}

sealed class FlowingLance(BossModule module) : Components.GenericAOEs(module)
{
    private class Lance(WPos origin, Angle baseRot, Angle increment, DateTime firstActivation)
    {
        public WPos Origin = origin;
        public Angle BaseRot = baseRot;
        public Angle Increment = increment;
        public DateTime FirstActivation = firstActivation;
        public int NumFinished;
        public DateTime? ImminentOverride;
    }

    private readonly List<Lance> _lances = [];
    private static readonly AOEShapeCross _shape = new(24, 6);
    private const int TotalCasts = 7;
    private const int MaxShown = 2;
    private const float Step = 2.1f;

    private readonly List<AOEInstance> _aoesBuffer = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoesBuffer.Clear();
        foreach (var l in _lances)
        {
            var remaining = TotalCasts - l.NumFinished;
            if (remaining <= 0)
                continue;

            var show = Math.Min(MaxShown, remaining);
            for (var i = 0; i < show; ++i)
            {
                var idx = l.NumFinished + i;
                var rot = l.BaseRot + idx * l.Increment;
                var time = i == 0 && l.ImminentOverride is { } im ? im : l.FirstActivation.AddSeconds(Step * idx);
                _aoesBuffer.Add(new(_shape, l.Origin, rot, time, i == 0 ? Colors.Danger : Colors.AOE));
            }
        }
        return CollectionsMarshal.AsSpan(_aoesBuffer);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID)
        {
            case (uint)AID.FlowingLanceCW:
                _lances.Add(new(caster.Position, spell.Rotation, -15.Degrees(), Module.CastFinishAt(spell)));
                break;
            case (uint)AID.FlowingLanceCCW:
                _lances.Add(new(caster.Position, spell.Rotation, 15.Degrees(), Module.CastFinishAt(spell)));
                break;
            case (uint)AID.FlowingLanceRest:
                if (FindLance(caster.Position, spell.LocXZ) is { } l)
                {
                    l.BaseRot = spell.Rotation - l.NumFinished * l.Increment;
                    l.ImminentOverride = Module.CastFinishAt(spell);
                }
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.FlowingLanceCW or (uint)AID.FlowingLanceCCW or (uint)AID.FlowingLanceRest))
            return;

        if (FindLance(caster.Position, spell.TargetXZ) is not { } l)
        {
            ReportError($"Failed to find FlowingLance near {caster.Position}");
            return;
        }

        ++l.NumFinished;
        ++NumCasts;
        l.ImminentOverride = null;
        if (l.NumFinished >= TotalCasts)
            _lances.Remove(l);
    }

    private Lance? FindLance(WPos casterPos, WPos locXZ) => _lances.FirstOrDefault(l => l.Origin.AlmostEqual(casterPos, 1)) ?? _lances.FirstOrDefault(l => l.Origin.AlmostEqual(locXZ, 1));
}
