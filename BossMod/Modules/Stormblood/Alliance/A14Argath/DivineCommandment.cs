namespace BossMod.Stormblood.Alliance.A14Argath;

class DivineCommandment(BossModule module) : Components.GenericGaze(module)
{
    private const double ResolutionGrace = 0.3d;
    private const double MoveLead = 1.25d;
    private const double StopLead = 1d;
    private readonly Dictionary<Actor, ActorStatus> _expiredCommands = [];
    private readonly Eye[] _eye = new Eye[1];
    private readonly Dictionary<Actor, WPos> _movementStarts = [];
    private bool? _obey;
    private DateTime _maskExpires;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.MaskOfTruth or (uint)AID.MaskOfLies)
        {
            ClearMask();
            _obey = spell.Action.ID == (uint)AID.MaskOfTruth;
            // The four observed mask-to-Word intervals are about eight seconds.
            _maskExpires = WorldState.FutureTime(9d);
        }
        else if (spell.Action.ID == (uint)AID.TheWord && caster.OID == (uint)OID.ArgathThadalfus)
        {
            ClearMask();
        }
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (IsCommand(status.ID))
            _expiredCommands.Remove(actor);
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (!IsCommand(status.ID))
            return;
        var now = WorldState.CurrentTime;
        // The assignment can disappear a frame or two before The Word.
        // Keep a natural expiry briefly, but release an early cancellation.
        if (Obey(actor) != null && now >= status.ExpireAt.AddSeconds(-0.15d) && now < status.ExpireAt.AddSeconds(ResolutionGrace))
            _expiredCommands[actor] = status;
        else
            ClearAssignment(actor);
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            ClearMask();
        else
            ClearAssignment(actor);
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    private static bool IsCommand(uint id) => id is (uint)SID.DivineCommandmentFlee or (uint)SID.DivineCommandmentTurn;

    private bool? Obey(Actor actor) => !actor.IsDeadOrDestroyed && WorldState.CurrentTime < _maskExpires ? _obey : null;

    private ActorStatus? Command(Actor actor, SID id)
    {
        var status = actor.FindStatus((uint)id) ?? _expiredCommands.GetValueOrDefault(actor);
        return status.ID == (uint)id && WorldState.CurrentTime < status.ExpireAt.AddSeconds(ResolutionGrace) ? status : null;
    }

    private void ClearAssignment(Actor actor)
    {
        _movementStarts.Remove(actor);
        _expiredCommands.Remove(actor);
    }

    private void ClearMask()
    {
        _movementStarts.Clear();
        _expiredCommands.Clear();
        _obey = null;
        _maskExpires = default;
    }

    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor)
    {
        if (Obey(actor) is not bool obey || Command(actor, SID.DivineCommandmentTurn) is not { } turn)
            return [];

        // Truth hit players nearly side-on. Lies is safest when facing the boss directly.
        _eye[0] = new(Module.PrimaryActor.Position, turn.ExpireAt, inverted: !obey, facingHalfAngle: obey ? 90f : 45f);
        return _eye;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Obey(actor) is bool obey && Command(actor, SID.DivineCommandmentTurn) == null && Command(actor, SID.DivineCommandmentFlee) == null)
            hints.Add(obey ? "Mask of Truth. Follow your next command." : "Mask of Lies. Do the opposite of your next command.");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (Obey(actor) is not bool obey || Command(actor, SID.DivineCommandmentFlee) is not { } flee)
            return;

        var untilResponse = (flee.ExpireAt - WorldState.CurrentTime).TotalSeconds - (obey ? MoveLead : StopLead);
        if (untilResponse > 0)
            hints.Add(obey ? $"Start moving in {untilResponse:f1}s." : $"Stop moving and attacking in {untilResponse:f1}s.", false);
        else
            hints.Add(obey ? "Keep moving until The Word!" : "Stop moving and attacking until The Word!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Obey(actor) is not bool obey || Command(actor, SID.DivineCommandmentFlee) is not { } flee)
            return;

        if (!obey)
        {
            hints.AddSpecialMode(AIHints.SpecialMode.Pyretic, flee.ExpireAt, flee.ExpireAt.AddSeconds(ResolutionGrace));
        }
        else if (flee.ExpireAt <= WorldState.FutureTime(MoveLead))
        {
            // Keep requesting a small step until the command resolves. The planner still
            // chooses the direction using the arena and every concurrent hazard.
            if (!_movementStarts.TryGetValue(actor, out var start) || !actor.Position.InCircle(start, 1f))
                _movementStarts[actor] = start = actor.Position;
            hints.AddForbiddenZone(new SDCircle(start, 1.5f), WorldState.CurrentTime);
        }
    }
}
