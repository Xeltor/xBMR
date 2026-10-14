namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

// b1046 collider10 and independent EventRanges have 7.5y horizontal half-extents.
// Placed SGB rotations are identity/half-turns (including equivalent paired X/Z half-turns),
// so the square's world axes are unchanged; actor facing is not the authored SGB transform.
// Forecast the observed one-shot strike, not the unresolved lifetime of its client collider.
class FloorStrikes(BossModule module) : Components.GenericAOEs(module, warningText: "Leave the flashing floor tile!")
{
    private static readonly AOEShapeRect Shape = new(7.5f, 7.5f, 7.5f);
    private readonly Dictionary<ulong, (Actor Actor, AOEInstance AOE)> _pending = [];
    private readonly HashSet<Actor> _uncertain = [];
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var prediction in _pending.Values)
            _aoes.Add(prediction.AOE);
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID is < 2007609 or > 2007616 || WorldState.Actors.Find(actor.InstanceID) != actor)
            return;
        _uncertain.Remove(actor);
        // Native parameters are packed (stored state << 16) | immediate timeline mask.
        if (state == 0x00100020)
        {
            if (!_pending.TryGetValue(actor.InstanceID, out var previous) || previous.Actor != actor)
                _pending[actor.InstanceID] = (actor, new(Shape, actor.Position, activation: WorldState.FutureTime(4.1), actorID: actor.InstanceID));
            // Duplicate cues retain the first deadline. Observed impact is +4.427..4.460s;
            // 4.1s is a conservative response margin, not a measured server snapshot.
        }
        else if (_pending.GetValueOrDefault(actor.InstanceID).Actor == actor)
            _pending.Remove(actor.InstanceID); // 4/16 resolves; 1/2 resets; other states withdraw an unsupported prediction
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_pending.GetValueOrDefault(actor.InstanceID).Actor == actor)
            _pending.Remove(actor.InstanceID);
        _uncertain.Remove(actor);
    }

    public override void OnActorCreated(Actor actor)
    {
        if (_pending.TryGetValue(actor.InstanceID, out var previous) && previous.Actor != actor)
            _pending.Remove(actor.InstanceID);
        _uncertain.RemoveWhere(a => a.InstanceID == actor.InstanceID && a != actor);
    }

    public override void Update()
    {
        foreach (var (id, prediction) in _pending.ToArray())
        {
            var actor = prediction.Actor;
            if (actor.IsDeadOrDestroyed || WorldState.Actors.Find(id) != actor)
                _pending.Remove(id);
            else if (WorldState.CurrentTime > prediction.AOE.Activation.AddSeconds(2))
            {
                _pending.Remove(id);
                _uncertain.Add(actor); // bounded lost-evidence cleanup, not an assertion that the floor is safe
            }
        }
        _uncertain.RemoveWhere(a => a.IsDeadOrDestroyed || WorldState.Actors.Find(a.InstanceID) != a);
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_uncertain.Count != 0)
            hints.Add("Floor strike cue lost - verify the floor!");
    }
}
