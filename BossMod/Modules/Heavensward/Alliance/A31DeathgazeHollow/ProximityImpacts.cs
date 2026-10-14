namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

// The boss's3s preparation finishes before the helpers begin their5s impact casts.
// The Action range describes affected extent, not a safe distance or damage falloff.
class VoidBlizzardIV(BossModule module) : Components.GenericAOEs(module, (uint)AID.VoidBlizzardIVAOE)
{
    private sealed record Pending(Actor Caster, uint Action, WPos Origin, DateTime Activation);
    private static readonly AOEShapeCircle EffectRange = new(40);
    private readonly Dictionary<ulong, Pending> _casts = [];
    private readonly List<AOEInstance> _aoes = [];

    public override void OnActorCreated(Actor actor)
    {
        if (_casts.TryGetValue(actor.InstanceID, out var previous) && previous.Caster != actor)
            _casts.Remove(actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.VoidBlizzardIV1 or (uint)AID.VoidBlizzardIV2 or (uint)AID.VoidBlizzardIVAOE)
            || WorldState.Actors.Find(caster.InstanceID) != caster)
            return;
        if (_casts.TryGetValue(caster.InstanceID, out var previous) && previous.Caster == caster && previous.Action == spell.Action.ID)
            return; // Repeated observations must not move an established response deadline.
        _casts[caster.InstanceID] = new(caster, spell.Action.ID, spell.LocXZ, Module.CastFinishAt(spell));
    }

    private void Remove(Actor caster, uint action)
    {
        if (_casts.TryGetValue(caster.InstanceID, out var pending) && pending.Caster == caster && pending.Action == action)
            _casts.Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        Remove(caster, spell.Action.ID);
        if (spell.Action.ID == WatchedAction)
            ++NumCasts;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) => Remove(caster, spell.Action.ID);

    public override void OnActorDestroyed(Actor actor)
    {
        if (_casts.GetValueOrDefault(actor.InstanceID)?.Caster == actor)
            _casts.Remove(actor.InstanceID);
    }

    public override void Update()
    {
        foreach (var (id, pending) in _casts.ToArray())
            if (pending.Caster.IsDestroyed || WorldState.Actors.Find(id) != pending.Caster || WorldState.CurrentTime > pending.Activation.AddSeconds(2))
                _casts.Remove(id); // Bounds lost-event guidance; it is not a measured hazard expiry.
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var pending in _casts.Values)
            if (pending.Action == WatchedAction)
                _aoes.Add(new(EffectRange, pending.Origin, activation: pending.Activation, risky: false, actorID: pending.Caster.InstanceID));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_casts.Count != 0)
        {
            var impacts = _casts.Values.Where(p => p.Action == WatchedAction).ToArray();
            hints.Add(impacts.Length != 0
                ? $"Proximity impacts in {Math.Max(0, (impacts.Min(p => p.Activation) - WorldState.CurrentTime).TotalSeconds):f1}s: stay opposite the impact markers; then line up with ice!"
                : "Proximity damage next: prepare to stand opposite the impact markers, then line up with ice!");
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        BitMask affected = default;
        foreach (var (partySlot, _) in Raid.WithSlot())
            affected.Set(partySlot);
        if (affected.Any())
            foreach (var pending in _casts.Values)
                if (pending.Action == WatchedAction)
                    hints.AddPredictedDamage(affected, pending.Activation);
        // No guessed safe radius or outward preference: this overlaps preparation
        // for the subsequent ice stop, and blindly maximizing distance is unsafe.
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc) { }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var pending in _casts.Values)
            if (pending.Action == WatchedAction)
            {
                // Small crosses mark source points only, never a safe/danger boundary.
                Arena.AddLine(pending.Origin + new WDir(-0.7f, -0.7f), pending.Origin + new WDir(0.7f, 0.7f), Colors.Object);
                Arena.AddLine(pending.Origin + new WDir(-0.7f, 0.7f), pending.Origin + new WDir(0.7f, -0.7f), Colors.Object);
            }
    }
}
