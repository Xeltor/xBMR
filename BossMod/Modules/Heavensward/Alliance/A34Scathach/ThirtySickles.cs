namespace BossMod.Heavensward.Alliance.A34Scathach;

// In both recorded pulls, the first (+45/+225) Shadespin pair resolves exactly four
// seconds before Thirty Sickles. Wings also precede the recorded later pulse,
// but their variable duration supports preparation only, not a pulse countdown.
class ThirtySickles(BossModule module) : Components.GenericKnockback(module, (uint)AID.ThirtySickles)
{
    private static readonly AOEShapeCircle AffectedArea = new(15);
    private readonly List<Knockback> _sources = [];
    private bool _awaitingShadespin;
    private bool _visualResolved;
    private bool _haveResolvedShadespin;
    private DateTime _helperDeadline;
    private DateTime _activation;

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        var boss = Module.PrimaryActor;
        // Read current status, rather than caching its gain, so component bootstrap,
        // loss and actor replacement cannot retain or restart an invented timer.
        if (!boss.IsDeadOrDestroyed && boss.IsTargetable && WorldState.Actors.Find(boss.InstanceID) == boss
            && boss.FindStatus((uint)SID.ShadowLimb) != null)
            hints.Add("Knockback possible: keep a safe landing away from the edge");
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _sources.Clear();
        if (_activation != default)
            _sources.Add(new(Module.PrimaryActor.Position, 10, _activation, AffectedArea, actorID: Module.PrimaryActor.InstanceID));
        return CollectionsMarshal.AsSpan(_sources);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;
        _activation = default;
        _awaitingShadespin = !_haveResolvedShadespin && spell.Action.ID == (uint)AID.Shadespin;
        _visualResolved = false;
        _helperDeadline = _awaitingShadespin ? Module.CastFinishAt(spell, 2d) : default;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.Shadespin && !_visualResolved)
            Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        ExpirePendingHelper();
        base.OnEventCast(caster, spell);
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.Shadespin or (uint)AID.Shadespin1)
        {
            _visualResolved = _awaitingShadespin && spell.Action.ID == (uint)AID.Shadespin;
            // Recorded helper resolutions follow the actual visual by 1.16-1.20s.
            // Missing helpers must not leave a cue that can arm an unrelated later pulse.
            _helperDeadline = _visualResolved ? WorldState.FutureTime(2d) : default;
            _haveResolvedShadespin = true;
        }
        else if (spell.Action.ID == (uint)AID.Shadespin2 && _awaitingShadespin && _visualResolved
            && caster.OID == (uint)OID.Connla && caster.Position.AlmostEqual(Module.PrimaryActor.Position, 1f))
        {
            // Keep a 0.1s response margin against the observed 4.00s interval.
            _activation = WorldState.FutureTime(3.9d);
            _awaitingShadespin = false; // the opposite helper must not duplicate or postpone the pulse
            _helperDeadline = default;
        }
        else if (spell.Action.ID is (uint)AID.ThirtySickles or (uint)AID.Soar or (uint)AID.ShadowRelease)
            Clear();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_activation != default && !IsImmune(slot, _activation) && Module.Bounds is ArenaBoundsCircle bounds)
        {
            var origin = Module.PrimaryActor.Position;
            // Outside the 15y attack area no displacement applies. Inside it, preserve every
            // position whose 10y radial landing remains inside the authored arena.
            hints.AddForbiddenZone(new SDIntersection([
                AffectedArea.Distance(origin, default),
                new SDKnockbackInCircleAwayFromOrigin(Module.Center, origin, 10, bounds.Radius)
            ]), _activation);
        }
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void Update()
    {
        ExpirePendingHelper();
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(1d))
            Clear();
    }

    private void ExpirePendingHelper()
    {
        if (_awaitingShadespin && _helperDeadline != default && WorldState.CurrentTime > _helperDeadline)
            Clear();
    }

    private void Clear()
    {
        _activation = default;
        _awaitingShadespin = false;
        _visualResolved = false;
        _helperDeadline = default;
        _sources.Clear();
    }
}
