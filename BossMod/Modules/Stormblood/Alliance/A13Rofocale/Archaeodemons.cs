namespace BossMod.Stormblood.Alliance.A13Rofocale;

class Archaeodemons(BossModule module) : BossComponent(module)
{
    // The paired hexagons in sgvf_w_btl_b1275 bound the closed wall.
    private sealed record PrisonGeometry(uint LayoutID, WPos Center, WPos[] Outline, ShapeDistance Outside, ShapeDistance Wall);
    private static readonly PrisonGeometry[] Prisons =
    [
        BuildPrison(7082097, new(92.15f, -183f)),
        BuildPrison(7082094, new(106f, -206.89f)),
        BuildPrison(7082095, new(119.85f, -183.02f)),
    ];
    private readonly DateTime[] _closingAt = new DateTime[Prisons.Length];
    private BitMask _closed;

    internal static int Prison(WPos center, WPos position) => position.Z < center.Z ? 1 : position.X < center.X ? 0 : 2;

    public override void OnActorEAnim(Actor actor, uint state)
    {
        var prison = WallIndex(actor);
        if (prison < 0)
            return;

        switch (state)
        {
            case 0x00040008 when !_closed[prison] && _closingAt[prison] == default:
                _closingAt[prison] = WorldState.FutureTime(8.75d);
                break;
            case 0x00100020:
                _closingAt[prison] = default;
                _closed.Set(prison);
                break;
            case 0x00010040:
                Clear(prison);
                break;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        var prison = WallIndex(actor);
        if (prison >= 0)
            Clear(prison);
        else
            OnActorDeath(actor);
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _closed = default;
            Array.Clear(_closingAt);
        }
    }

    public override void Update()
    {
        for (var i = 0; i < Prisons.Length; ++i)
            if (_closingAt[i] != default && WorldState.CurrentTime > _closingAt[i].AddSeconds(1d))
                _closingAt[i] = default;
    }

    private Actor? Assigned(int group, int occupied, Actor actor, IEnumerable<Actor> candidates)
    {
        var committed = group >= 0 && (_closed[group] || _closingAt[group] != default);
        return candidates.Where(add => add.IsTargetable && !add.IsDeadOrDestroyed && Arena.InBounds(add.Position))
            .Select(add => (Add: add, Prison: Prison(Arena.Center, add.Position)))
            .Where(candidate => Reachable(candidate.Prison, occupied) && (!committed || candidate.Prison == group))
            .OrderBy(candidate => group >= 0 && candidate.Prison != group)
            .ThenBy(candidate => (candidate.Add.Position - actor.Position).LengthSq())
            .Select(candidate => candidate.Add)
            .FirstOrDefault();
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var occupied = OccupiedPrison(actor);
        var group = occupied >= 0 ? occupied : Raid.AllianceGroupForSlot(slot);
        if (group >= 0 && _closingAt[group] != default && Prisons[group].Outside.Contains(actor.Position))
            hints.Add("Enter your Archaeodemon's prison before the wall closes!");
        else if (group >= 0 && !Reachable(group, occupied))
            hints.Add("Your Archaeodemon's prison is closed.");
        else if (Assigned(group, occupied, actor, Module.Enemies((uint)OID.Archaeodemon)) is { } add)
        {
            if (InterruptibleAttack(add))
                hints.Add("Interrupt the Archaeodemon if able.", false);
            else if (AimKarma(add, actor))
                hints.Add("Face the Archaeodemon away from the other prisons before Karma.", false);
            else
                hints.Add(group >= 0 && Prison(Arena.Center, add.Position) != group
                    ? "Help defeat the reachable Archaeodemon!"
                    : "Enter your Archaeodemon's prison and defeat it!", !actor.Position.InCircle(add.Position, 8f));
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var occupied = OccupiedPrison(pc);
        var group = occupied >= 0 ? occupied : Raid.AllianceGroupForSlot(pcSlot);
        var add = Assigned(group, occupied, pc, Module.Enemies((uint)OID.Archaeodemon));
        if (add != null)
            group = Prison(Arena.Center, add.Position);
        for (var i = 0; i < Prisons.Length; ++i)
            if (_closed[i] || _closingAt[i] != default)
                Arena.AddPolygon(Prisons[i].Outline, i == group ? Colors.Object : Colors.Border);
        if (add != null && AimKarma(add, pc))
            Arena.ZoneCircleOutline(KarmaPosition(add), 1f, Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var occupied = OccupiedPrison(actor);
        var group = occupied >= 0 ? occupied : Raid.AllianceGroupForSlot(slot);
        var candidates = hints.PotentialTargets.Where(enemy => enemy.Actor.OID == (uint)OID.Archaeodemon
            && (enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable)).ToArray();
        var add = Assigned(group, occupied, actor, candidates.Select(enemy => enemy.Actor));
        foreach (var enemy in candidates)
        {
            if (!Reachable(Prison(Arena.Center, enemy.Actor.Position), occupied))
            {
                // A closed wall blocks the approach in either direction.
                enemy.Priority = AIHints.Enemy.PriorityInvincible;
            }
            else if (add != null && enemy.Actor == add)
            {
                enemy.Priority = 4;
                enemy.ShouldBeInterrupted = InterruptibleAttack(add);
                if (actor.Role == Role.Tank
                    && (add.TargetID == actor.InstanceID || WorldState.Actors.Find(add.TargetID) is not { Role: Role.Tank, IsDeadOrDestroyed: false }))
                {
                    enemy.ShouldBeTanked = true;
                    if (AimKarma(add, actor))
                        enemy.DesiredRotation = Angle.FromDirection(add.Position - Arena.Center);
                }
            }
        }

        if (add != null)
            group = Prison(Arena.Center, add.Position);
        for (var i = 0; i < Prisons.Length; ++i)
            if (_closed[i])
                hints.AddForbiddenZone(Prisons[i].Wall, WorldState.CurrentTime);
            else if (i != group && _closingAt[i] != default)
                hints.AddForbiddenZone(Prisons[i].Wall, _closingAt[i]);

        WPos? destination = null;
        if (group >= 0 && _closingAt[group] != default)
        {
            var center = Prisons[group].Center;
            destination = center + 4f * (Arena.Center - center).Normalized();
            hints.AddForbiddenZone(Prisons[group].Outside, _closingAt[group]);
        }

        if (add != null)
        {
            // Approach the center-facing side of the prison, away from the orbit.
            if (AimKarma(add, actor))
                destination = KarmaPosition(add);
            else
                destination ??= add.Position + 4f * add.DirectionTo(Arena.Center);
        }
        if (destination is { } goal)
            hints.GoalZones.Add(AIHints.GoalProximity(goal, 18f, 8f));
    }

    private static bool AimKarma(Actor add, Actor actor)
        => actor.Role == Role.Tank && add.TargetID == actor.InstanceID && add.CastInfo is not { EventHappened: false };

    private static bool InterruptibleAttack(Actor add)
        => add.CastInfo is { Interruptible: true, EventHappened: false } cast
            && (cast.IsSpell(AID.Karma) || cast.IsSpell(AID.UnholyDarkness));

    private WPos KarmaPosition(Actor add) => add.Position + (add.HitboxRadius + 0.5f) * (add.Position - Arena.Center).Normalized();

    private bool Reachable(int prison, int occupied) => occupied >= 0 ? occupied == prison : !_closed[prison];

    private int OccupiedPrison(Actor actor)
    {
        for (var i = 0; i < Prisons.Length; ++i)
            if (_closed[i] && !Prisons[i].Outside.Contains(actor.Position))
                return i;
        return -1;
    }

    private void Clear(int prison)
    {
        _closed.Clear(prison);
        _closingAt[prison] = default;
    }

    private static int WallIndex(Actor actor) => actor.OID == (uint)OID.PrisonWall
        ? Array.FindIndex(Prisons, prison => prison.LayoutID == actor.LayoutID)
        : -1;

    private static PrisonGeometry BuildPrison(uint layoutID, WPos center)
    {
        static List<WDir> Hexagon(float apothem) => Enumerable.Range(0, 6)
            .Select(i => (apothem / MathF.Cos(MathF.PI / 6f)) * (30f + 60f * i).Degrees().ToDirection()).ToList();
        var inner = new RelSimplifiedComplexPolygon(Hexagon(8.7f));
        inner.InitPolygonIndex();
        var outer = new RelPolygonWithHoles(Hexagon(10f));
        var outline = outer.Vertices.Select(offset => center + offset).ToArray();
        outer.AddHole(Hexagon(8.7f));
        var wall = new RelSimplifiedComplexPolygon([outer]);
        wall.InitPolygonIndex();
        return new(layoutID, center, outline, new SDInvertedPolygonWithHoles(new(center, inner)), new SDPolygonWithHoles(new(center, wall)));
    }
}

class HeavenlySubjugation(BossModule module) : Components.RaidwideCast(module, (uint)AID.HeavenlySubjugation1, "Heavenly Subjugation incoming. Prepare mitigation and healing.")
{
    private DateTime _ascent;
    private float _launchHeight;
    private DateTime _expectedImpact;

    public override void OnActorModelStateChange(Actor actor, byte modelState, byte animState1, byte animState2)
    {
        if (actor != Module.PrimaryActor || actor.IsTargetable)
            return;

        if (modelState == 20)
        {
            _ascent = WorldState.CurrentTime;
            _launchHeight = actor.PosRot.Y;
        }
        else if (modelState == 0 && _ascent != default)
        {
            if (WorldState.CurrentTime <= _ascent.AddSeconds(25d) && actor.PosRot.Y >= _launchHeight + 20f)
                _expectedImpact = WorldState.FutureTime(4.7d);
            _ascent = default;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor || spell.Action.ID != WatchedAction)
            return;
        _ascent = _expectedImpact = default;
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.HeavenlySubjugation2 or (uint)AID.HeavenlySubjugation3)
        {
            ++NumCasts;
            Reset();
        }
        else if (spell.Action.ID == WatchedAction && caster == Module.PrimaryActor)
        {
            // The helper delivers the damage shortly after the visual cast ends.
            _expectedImpact = WorldState.FutureTime(0.07d);
            Casters.Remove(caster);
        }
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Reset();
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Reset();
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void Update()
    {
        if (_expectedImpact != default && WorldState.CurrentTime > _expectedImpact.AddSeconds(1d))
            _expectedImpact = default;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        base.AddGlobalHints(actor, hints);
        if (_expectedImpact != default)
            hints.Add(Hint);
        if (ChargeCast is not null)
            hints.Add("Move to either side of Rofocale's charge.");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (ChargeCast is not { } cast)
            return;

        var direction = cast.Rotation.ToDirection();
        var closest = cast.LocXZ + direction * (Arena.Center - cast.LocXZ).Dot(direction);
        var halfLength = direction * Arena.Bounds.Radius;
        Arena.AddLine(Arena.ClampToBounds(closest - halfLength), Arena.ClampToBounds(closest + halfLength), Colors.Object);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_expectedImpact != default)
        {
            var affected = AffectedPlayers();
            if (affected != default)
                hints.AddPredictedDamage(affected, _expectedImpact);
        }
        if (ChargeCast is { } cast)
        {
            var origin = cast.LocXZ;
            var lateral = cast.Rotation.ToDirection().OrthoL();
            hints.GoalZones.Add(position => 0.5f * Math.Min(MathF.Abs((position - origin).Dot(lateral)), 12f));
        }
    }

    private ActorCastInfo? ChargeCast => Active && Module.PrimaryActor.CastInfo is { EventHappened: false } cast
        && cast.IsSpell(AID.HeavenlySubjugation1) ? cast : null;

    private void Reset()
    {
        _ascent = _expectedImpact = default;
        Casters.Clear();
    }
}

class AetherialAcceleration(BossModule module) : Components.GenericAOEs(module, (uint)AID.CirclingCharge, "Avoid Rofocale's circling charge!")
{
    // Recorded damage and knockback land on both sides of the pulse origin.
    private static readonly AOEShapeRect Shape = new(5f, 5f, 5f);
    private const double PulseInterval = 0.29d;
    private readonly AOEInstance[] _pulses = new AOEInstance[7];
    private DateTime _preparingAt;
    private DateTime _validUntil;
    private DateTime _lastPulse;
    private Angle _lastPositionAngle;
    private bool _ended;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _validUntil != default && WorldState.CurrentTime <= _validUntil ? _pulses.AsSpan(0, 3) : [];

    public override void Update()
    {
        if (_preparingAt == default || _validUntil != default || WorldState.CurrentTime > _preparingAt.AddSeconds(3.4d))
            return;

        // Wait until the observed approach is headed towards the recorded opening lane.
        var radial = Module.PrimaryActor.Position - Arena.Center;
        if (radial.LengthSq() >= 12f * 12f && Angle.FromDirection(radial).Deg is >= 125f and <= 150f)
            PredictOpening(_preparingAt.AddSeconds(3.4d));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_preparingAt != default && _lastPulse == default && WorldState.CurrentTime <= _preparingAt.AddSeconds(4.5d))
            hints.Add("Circling charge incoming. Approach your prison from the center side.", false);
        base.AddHints(slot, actor, hints);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveAOEs(slot, actor).IsEmpty)
            return;

        foreach (var pulse in _pulses)
            hints.AddForbiddenZone(Shape, pulse.Origin, pulse.Rotation, pulse.Activation);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction || _ended)
            return;

        var radial = spell.TargetXZ - Arena.Center;
        if (radial.LengthSq() is < 18f * 18f or > 22f * 22f)
            return;

        var positionAngle = Angle.FromDirection(radial);
        var now = WorldState.CurrentTime;
        // Grouped effects can be delivered out of order. Preserve the leading observed lane.
        if (_lastPulse != default && now <= _lastPulse.AddSeconds(0.5d) && (positionAngle - _lastPositionAngle).Normalized().Deg <= 0f)
            return;

        _lastPulse = now;
        _lastPositionAngle = positionAngle;
        Predict(radial.Length(), positionAngle + 17f.Degrees(), spell.Rotation + 15f.Degrees(), now.AddSeconds(PulseInterval));
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor && Module.Enemies((uint)OID.Archaeodemon).Any(add => add.IsTargetable && !add.IsDeadOrDestroyed))
        {
            Clear();
            _preparingAt = WorldState.CurrentTime;
            _ended = false;
        }
    }

    public override void OnActorModelStateChange(Actor actor, byte modelState, byte animState1, byte animState2)
    {
        if (actor != Module.PrimaryActor)
            return;

        if (modelState == 20)
        {
            End();
        }
        else if (modelState == 19 && !_ended && _lastPulse == default)
        {
            var radial = actor.Position - Arena.Center;
            if (radial.LengthSq() is >= 17f * 17f and <= 21f * 21f && Angle.FromDirection(radial).Deg is >= 140f and <= 160f)
                PredictOpening(WorldState.FutureTime(0.38d));
        }
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            End();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HeavenlySubjugation1)
            End();
    }

    private void PredictOpening(DateTime activation) => Predict(20f, 179f.Degrees(), (-96f).Degrees(), activation);

    private void Predict(float radius, Angle positionAngle, Angle rotation, DateTime activation)
    {
        for (var i = 0; i < _pulses.Length; ++i)
        {
            var origin = Arena.Center + radius * (positionAngle + (17f * i).Degrees()).ToDirection();
            _pulses[i] = new(Shape, origin, rotation + (15f * i).Degrees(), activation.AddSeconds(PulseInterval * i), i == 0 ? Colors.Danger : Colors.AOE);
        }
        _validUntil = activation.AddSeconds(0.65d);
    }

    private void End()
    {
        Clear();
        _ended = true;
    }

    private void Clear()
    {
        _preparingAt = default;
        _validUntil = default;
        _lastPulse = default;
    }
}
