namespace BossMod.Dawntrail.Alliance.A34Promathia;

sealed class DeadlyRebirthKnockback(BossModule module) : Components.GenericKnockback(module, (uint)AID.DeadlyRebirth)
{
    private Actor? _visualSource, _helperSource;
    private ActorCastInfo? _visual, _helper;
    private Knockback[] _sources = [];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => _sources;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_sources.Length != 0)
        {
            var source = _sources[0];
            // p + 20 * facing must land inside the restored 25y arena.
            // Immunity effectiveness is unverified: retain the safe approach.
            hints.AddForbiddenZone(new SDInvertedCircle(Arena.Center - source.Distance * source.Direction.ToDirection(), 25f), source.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.FalseGenesis)
            Reset();
        else if (spell.Action.ID == (uint)AID.DeadlyRebirthCast && !ReferenceEquals(_visual, spell))
        {
            Reset();
            _visualSource = caster;
            _visual = spell;
            Predict(caster, spell, 2d);
        }
        else if (spell.Action.ID == WatchedAction && !ReferenceEquals(_helper, spell))
        {
            _helperSource = caster;
            _helper = spell;
            Predict(caster, spell, 0d);
        }
    }

    private void Predict(Actor caster, ActorCastInfo spell, double delay)
        => _sources = [new(caster.Position, 20f, Module.CastFinishAt(spell, delay), direction: spell.Rotation,
            kind: Kind.DirForward, actorID: caster.InstanceID, ignoreImmunes: true)];

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && (ReferenceEquals(_visual, spell) || ReferenceEquals(_helper, spell)))
            Reset();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _sources.Length != 0)
        {
            ++NumCasts;
            Reset();
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (ReferenceEquals(_helperSource, actor) || ReferenceEquals(_visualSource, actor))
            Reset();
    }

    private void Reset()
    {
        _visualSource = _helperSource = null;
        _visual = _helper = null;
        _sources = [];
    }
}
