namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

// The portal casts supply an explicit outgoing line before the add emerges.
// Their own action/cast end is a visual cue, ~0.8s before the damaging add charge.
class TransportedLuckyPierrot(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (WPos Endpoint, DateTime VisualFinish)> _exits = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.Unknown4 or (uint)AID.Unknown6))
            return;
        var endpoint = spell.LocXZ;
        var direction = endpoint - caster.Position;
        var shape = new AOEShapeRect(direction.Length(), 3); // all three actions have full width6 in the current Action sheet
        var finish = Module.CastFinishAt(spell);
        _exits[caster.InstanceID] = (endpoint, finish);
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        // Nine observed hit delays are 0.782-0.802s after the portal visual; earlier client deadline, not verified snapshot.
        _aoes.Add(new(shape, caster.Position, Angle.FromDirection(direction), finish.AddSeconds(0.7), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Unknown4 or (uint)AID.Unknown6
            && _exits.TryGetValue(caster.InstanceID, out var exit) && WorldState.CurrentTime < exit.VisualFinish.AddSeconds(-0.1))
            Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID != (uint)OID.FerdiadsFool || spell.Action.ID != (uint)AID.LuckyPierrot1)
            return;
        // No inferred portal colours or bindings: the actual charge's explicit endpoint must identify one forecast uniquely.
        var matches = _exits.Where(e => e.Value.Endpoint.AlmostEqual(spell.TargetXZ, 0.1f)).Select(e => e.Key).ToArray();
        if (matches.Length == 1)
        {
            Remove(matches[0]);
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor) => Remove(actor.InstanceID);

    public override void Update()
    {
        // Actual hits followed visuals within0.81s in both waves. Allow3s for late/missing delivery,
        // then retire the unsupported forecast instead of constraining the rest of the encounter.
        foreach (var id in _exits.Where(e => e.Value.VisualFinish.AddSeconds(3) < WorldState.CurrentTime).Select(e => e.Key).ToArray())
            Remove(id);
    }

    private void Remove(ulong instanceID)
    {
        _exits.Remove(instanceID);
        _aoes.RemoveAll(a => a.ActorID == instanceID);
    }
}
