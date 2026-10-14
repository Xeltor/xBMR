namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

class Blackfire2(BossModule module) : Components.GenericAOEs(module)
{
    // Conservative estimated final extent, not an exact physical-radius claim. Recorded stationary actor6
    // is hit at12.615y and excluded at13.548y; Action range7 ignores six AreaOfInfluenceUp stacks.
    // Forecast the full extent at cast start so planning does not wait for the growing visual.
    private static readonly AOEShapeCircle _estimatedFinalCircle = new(13.5f);
    private sealed class Pending(Actor actor, AOEInstance aoe, bool uncertain)
    {
        public readonly Actor Actor = actor;
        public AOEInstance AOE = aoe;
        public bool Uncertain = uncertain;
    }
    private readonly Dictionary<ulong, Pending> _casts = [];
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnActorCreated(Actor actor)
    {
        if (_casts.TryGetValue(actor.InstanceID, out var old) && old.Actor != actor)
            Remove(actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Blackfire2 || WorldState.Actors.Find(caster.InstanceID) != caster)
            return;
        if (_casts.TryGetValue(caster.InstanceID, out var old) && old.Actor == caster)
            return; // A duplicate observation cannot postpone the existing deadline.
        var uncertain = caster.FindStatus((uint)SID.AreaOfInfluenceUp)?.Extra > 6;
        _casts[caster.InstanceID] = new(caster,
            new(_estimatedFinalCircle, spell.LocXZ, activation: Module.CastFinishAt(spell), actorID: caster.InstanceID), uncertain);
        Rebuild();
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.AreaOfInfluenceUp && _casts.TryGetValue(actor.InstanceID, out var pending) && pending.Actor == actor)
        {
            pending.Uncertain = status.Extra > 6; // Only the observed0..6 family supports the estimate.
            Rebuild();
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.AreaOfInfluenceUp && _casts.TryGetValue(actor.InstanceID, out var pending) && pending.Actor == actor)
        {
            pending.Uncertain = false;
            Rebuild();
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Blackfire2 && _casts.TryGetValue(caster.InstanceID, out var pending) && pending.Actor == caster
            && WorldState.CurrentTime < pending.AOE.Activation.AddSeconds(-0.1))
            Remove(caster.InstanceID); // Early cancellation; normal completion retains the warning until the effect.
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Blackfire2 && _casts.TryGetValue(caster.InstanceID, out var pending) && pending.Actor == caster)
            Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_casts.TryGetValue(actor.InstanceID, out var pending) && pending.Actor == actor)
            Remove(actor.InstanceID);
    }

    public override void Update()
    {
        foreach (var (id, pending) in _casts.ToArray())
            if (pending.Actor.IsDestroyed || WorldState.Actors.Find(id) != pending.Actor || WorldState.CurrentTime >= pending.AOE.Activation.AddSeconds(2))
                Remove(id); // Missing-effect fallback bounds stale guidance; this is not a physical hazard-duration claim.
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_casts.Values.Any(p => p.Uncertain))
            hints.Add("Blackfire expansion is unrecognized: circle extent uncertain!");
    }

    private void Remove(ulong id)
    {
        _casts.Remove(id);
        Rebuild();
    }

    private void Rebuild()
    {
        _aoes.Clear();
        foreach (var pending in _casts.Values)
        {
            pending.AOE.Risky = !pending.Uncertain;
            pending.AOE.Color = pending.Uncertain ? Colors.Object : default;
            _aoes.Add(pending.AOE);
        }
    }
}
