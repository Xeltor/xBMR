namespace BossMod.Heavensward.Alliance.A34Scathach;

class ParticleBeam(BossModule module) : Components.GenericTowers(module)
{
    private readonly Dictionary<ulong, Actor> _sources = [];
    private readonly HashSet<Actor> _bootstrapSources = module.WorldState.Actors.Where(a => a.OID == (uint)OID.MeteorTower).ToHashSet();

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Towers.Exists(t => _sources.GetValueOrDefault(t.ActorID) is { } source && _bootstrapSources.Contains(source)))
            hints.Add("Existing meteor towers: soak now; original timing unavailable.");
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Towers.Exists(t => t.IsInside(actor)))
            hints.Add("Soak the tower!", false);
        else if (Towers.Exists(t => t.NumInside(Module) < t.MinSoakers))
            hints.Add("Soak an empty tower!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Several players may safely soak, but an already covered tower must not attract every AI
        // while another tower remains empty. Keep an existing soaker in place; send others to gaps.
        var occupied = Towers.FindIndex(t => t.IsInside(actor));
        var destinations = occupied >= 0 ? new List<Tower> { Towers[occupied] } : Towers.FindAll(t => t.NumInside(Module) < t.MinSoakers);
        if (destinations.Count > 0)
        {
            var zones = destinations.Select(t => t.InvertedShapeDistance ?? t.Shape.InvertedDistance(t.Position, t.Rotation)).ToArray();
            hints.AddForbiddenZone(new SDIntersection(zones), destinations.Min(t => t.Activation));
        }
        if (Towers.Count > 0)
        {
            BitMask soakers = default;
            foreach (var (partySlot, member) in Raid.WithSlot())
                if (Towers.Exists(t => t.IsInside(member)))
                    soakers.Set(partySlot);
            hints.AddPredictedDamage(soakers, Towers.Min(t => t.Activation), DamageType);
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        if (_sources.GetValueOrDefault(actor.InstanceID) == actor)
            return; // a duplicate creation must not postpone the established deadline
        // OpCreate may replace an Actor without a preceding destruction callback.
        // A replacement of another object type also invalidates the old tower.
        Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
        if (_sources.GetValueOrDefault(actor.InstanceID) is { } previous)
            _bootstrapSources.Remove(previous);
        _sources.Remove(actor.InstanceID);
        if (actor.OID != (uint)OID.MeteorTower)
            return;
        _sources[actor.InstanceID] = actor;
        // All observed sets resolve 9.8-10.0s after the event objects appear.
        // An existing object has no retained creation timestamp. Do not restart
        // its countdown after mid-fight component activation: require soaking now.
        var activation = _bootstrapSources.Contains(actor) ? WorldState.CurrentTime : WorldState.FutureTime(9.8d);
        Towers.Add(new(actor.Position, 5f, maxSoakers: int.MaxValue, activation: activation, actorID: actor.InstanceID));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ParticleBeam1 or (uint)AID.ParticleBeam2)
        {
            ++NumCasts;
            var position = new WPos(spell.TargetPos);
            Towers.RemoveAll(t => t.Position.AlmostEqual(position, 1f));
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_sources.GetValueOrDefault(actor.InstanceID) == actor)
        {
            Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
            _sources.Remove(actor.InstanceID);
            _bootstrapSources.Remove(actor);
        }
    }

    public override void Update()
    {
        foreach (var (id, source) in _sources.ToArray())
            if (source.IsDeadOrDestroyed || WorldState.Actors.Find(id) != source)
            {
                Towers.RemoveAll(t => t.ActorID == id);
                _sources.Remove(id);
                _bootstrapSources.Remove(source);
            }
        // Unknown-age objects can have at most the observed full lifetime left.
        // This is stale-state cleanup, not a newly asserted impact time.
        Towers.RemoveAll(t => WorldState.CurrentTime > t.Activation.AddSeconds(
            _sources.GetValueOrDefault(t.ActorID) is { } source && _bootstrapSources.Contains(source) ? 11.8d : 2d));
    }
}
