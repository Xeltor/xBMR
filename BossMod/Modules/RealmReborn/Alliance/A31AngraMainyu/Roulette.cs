namespace BossMod.RealmReborn.Alliance.A31AngraMainyu;

class Roulette(BossModule module) : Components.GenericAOEs(module, warningText: "Leave the final Roulette quadrant!")
{
    private Actor? _selected;
    private DateTime _lastSelection, _activation;
    private bool _sawHourglass;
    private static readonly AOEShapeCone Quarter = new(60, 45.Degrees());

    private static Angle? Direction(uint oid) => oid switch
    {
        2004748 => -135.Degrees(),
        2004774 => 135.Degrees(),
        2004775 => 45.Degrees(),
        2004776 => -45.Degrees(),
        _ => null
    };

    public override void OnActorEState(Actor actor, ushort state)
    {
        if (Direction(actor.OID) == null)
            return;
        if (state == 4)
        {
            _selected = null;
            _activation = default;
            _sawHourglass = false;
        }
        else if (state == 8)
        {
            _selected = actor;
            _lastSelection = WorldState.CurrentTime;
            _activation = default;
        }
        else if (state == 1 && _selected == actor)
            _selected = null;
    }

    public override void Update()
    {
        var alive = Module.Enemies((uint)OID.FinalHourglass).Any(a => !a.IsDeadOrDestroyed);
        _sawHourglass |= alive;
        // The locked-quarter animation is only ~0.3s before Death in the new recording.
        // Warn when the final hourglass dies, or the one-second rotation stops instead.
        // Treat departure as urgent; the server snapshot and timer-expiry branch are unverified.
        if (_selected != null && _activation == default && (_sawHourglass && !alive || WorldState.CurrentTime > _lastSelection.AddSeconds(1.5)))
            _activation = WorldState.CurrentTime;
        if (_activation != default && WorldState.CurrentTime > _activation.AddSeconds(10))
            _selected = null;
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor != _selected)
            return;
        if (state == 0x00010010 && _activation == default)
            _activation = WorldState.CurrentTime;
        else if (state == 0x00010002)
            _selected = null;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.GrimReaper && spell.Action.ID == (uint)AID.Death)
            _selected = null;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _selected is { IsDestroyed: false } selected && _activation != default && Direction(selected.OID) is { } direction
            ? new AOEInstance[] { new(Quarter, selected.Position, direction, _activation, actorID: selected.InstanceID) } : [];
}
