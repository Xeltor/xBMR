namespace BossMod.Stormblood.Alliance.A13Rofocale;

class Trample(BossModule module) : Components.GenericAOEs(module, (uint)AID.Trample2)
{
    // The second opening hits a player just behind the cast origin.
    private static readonly AOEShapeRect OpeningShape = new(10f, 5.1f, 2f);
    private static readonly AOEShapeRect CrossingShape = new(10f, 5f, 10f);
    private const float LobeOffset = 14.1f;
    private const float LobeRadius = 18f;
    // The projected mesh leaves about 2.45 yalms around each lobe center.
    // Leave additional clearance until more hit observations bound the gap.
    private const float GapRadius = 1.5f;
    private static readonly AOEShapeDonut LoopShape = new(GapRadius, LobeRadius);
    private readonly AOEInstance[] _opening = new AOEInstance[1];
    private WPos _front;
    private WPos _back;
    private bool _openingActive;
    private bool _frontActive;
    private DateTime _frontActivation;
    private DateTime _rearActivation;
    private DateTime _sequenceEnd;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _openingActive ? _opening : [];

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_sequenceEnd != default)
            hints.Add(_frontActive ? "Trample: clear the first loop, then the second." : "Trample: stay clear of the second loop and crossing.");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_sequenceEnd != default && (_openingActive && _opening[0].Check(actor.Position)
            || CrossingShape.Check(actor.Position, _opening[0].Origin, _opening[0].Rotation)
            || _frontActive && LoopShape.Check(actor.Position, _front, default) || LoopShape.Check(actor.Position, _back, default)))
            hints.Add("Move into an inner gap or outside the figure eight!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_sequenceEnd == default)
            return;

        if (_frontActive)
            hints.AddForbiddenZone(LoopShape, _front, activation: _frontActivation);
        hints.AddForbiddenZone(LoopShape, _back, activation: _rearActivation);
        hints.AddForbiddenZone(CrossingShape, _opening[0].Origin, _opening[0].Rotation, _frontActivation);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_sequenceEnd == default)
            return;

        if (_frontActive)
        {
            Arena.ZoneCircleOutline(_front, LobeRadius, Colors.Danger);
            Arena.ZoneCircleOutline(_front, GapRadius, Colors.Safe);
        }
        Arena.ZoneCircleOutline(_back, LobeRadius, _frontActive ? Colors.AOE : Colors.Danger);
        Arena.ZoneCircleOutline(_back, GapRadius, Colors.Safe);
        CrossingShape.Outline(Arena, _opening[0].Origin, _opening[0].Rotation, Colors.Danger);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Trample1)
        {
            var finish = Module.CastFinishAt(spell);
            _opening[0] = new(OpeningShape, caster.Position, spell.Rotation, finish, Colors.Danger, actorID: caster.InstanceID);
            _openingActive = true;
            _frontActive = true;
            // The two rings conservatively enclose the visible loops.
            // Keep the crossing clear throughout;
            // the rear loop starts after the midpoint in the recorded order.
            var offset = LobeOffset * spell.Rotation.ToDirection();
            _front = caster.Position + offset;
            _back = caster.Position - offset;
            _frontActivation = finish.AddSeconds(-0.25d);
            _rearActivation = finish.AddSeconds(0.95d);
            _sequenceEnd = finish.AddSeconds(3d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster.InstanceID == _opening[0].ActorID && spell.Action.ID == (uint)AID.Trample1 && !spell.EventHappened)
            Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            _openingActive = false;
        else if (spell.Action.ID == (uint)AID.Trample6 && _sequenceEnd != default)
        {
            _frontActive = false;
            if (WorldState.CurrentTime < _rearActivation)
                _rearActivation = WorldState.CurrentTime;
        }
        else if (spell.Action.ID == (uint)AID.Trample10)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_opening[0].ActorID == actor.InstanceID)
            Clear();
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void Update()
    {
        if (_openingActive && WorldState.CurrentTime > _opening[0].Activation.AddSeconds(0.5d))
            _openingActive = false;
        if (_sequenceEnd != default && WorldState.CurrentTime > _sequenceEnd)
            Clear();
    }

    private void Clear()
    {
        _openingActive = false;
        _frontActive = false;
        _sequenceEnd = default;
    }
}
