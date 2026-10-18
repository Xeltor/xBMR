namespace BossMod.RealmReborn.Alliance.A34CloudofDarkness;

class FeintParticleBeam(BossModule module) : Components.GenericAOEs(module)
{
    private Actor? _target;
    private WPos _position;
    private DateTime _activation;
    private int _remaining;
    private bool _initialHit;
    private static readonly AOEShapeCircle Initial = new(8), Followup = new(3);
    public bool IsChased(Actor actor) => _target == actor && _activation != default;
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.FeintParticleBeam)
        {
            _target = actor;
            _position = actor.Position;
            _activation = WorldState.FutureTime(3.5);
            _remaining = 10;
            _initialHit = false;
        }
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.FeintParticleBeam1)
        {
            _position = spell.LocXZ;
            _activation = Module.CastFinishAt(spell);
            _initialHit = false;
            _remaining = 10;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.FeintParticleBeam1 && !_initialHit)
            Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.FeintParticleBeam1)
        {
            _position = spell.TargetXZ;
            _initialHit = true;
            _activation = WorldState.FutureTime(3.1);
        }
        else if (spell.Action.ID == (uint)AID.FeintParticleBeam2 && _initialHit)
        {
            _position = spell.TargetXZ;
            _activation = WorldState.FutureTime(0.54);
            if (--_remaining <= 0)
                Clear();
        }
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor || actor == _target)
            Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void Update()
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed || _target?.IsDeadOrDestroyed == true || _activation != default && WorldState.CurrentTime > _activation.AddSeconds(2))
            Clear();
    }
    private void Clear() { _activation = default; _target = null; _remaining = 0; }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_activation == default || _initialHit && _target == null)
            return [];
        var position = _position;
        if (_initialHit && _target != null)
        {
            var delta = _target.Position - position;
            var distance = delta.Length();
            position += distance > 3 ? delta * (3 / distance) : delta;
        }
        return new AOEInstance[] { new(_initialHit ? Followup : Initial, position, activation: _activation) };
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_activation != default && _target == null)
            hints.Add("Chasing beam target unknown after reload; watch the beams and keep clear!");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsChased(actor))
            hints.Add("Bait chasing beams away from the raid; keep moving!", false);
    }
}
