namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

class ParticleBeam(BossModule module) : Components.GenericTowers(module)
{
    private const uint OuterTower = 0x1EA24E;
    private const uint TankTower = 0x1EA24F;
    private readonly Dictionary<ulong, Actor> _sources = [];
    private readonly HashSet<Actor> _bootstrapSources = module.WorldState.Actors.Where(a => a.OID is OuterTower or TankTower).ToHashSet();

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Towers.Exists(t => _sources.GetValueOrDefault(t.ActorID) is { } source && _bootstrapSources.Contains(source)))
            hints.Add("Existing meteor towers: soak now; original timing unavailable.");
    }

    public override void OnActorCreated(Actor actor)
    {
        if (_sources.GetValueOrDefault(actor.InstanceID) == actor)
            return;
        // Native OpCreate can replace an Actor reference without destroying the old one first.
        Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
        if (_sources.GetValueOrDefault(actor.InstanceID) is { } previous)
            _bootstrapSources.Remove(previous);
        _sources.Remove(actor.InstanceID);
        if (actor.OID is not (OuterTower or TankTower))
            return;
        _sources[actor.InstanceID] = actor;
        BitMask forbidden = default;
        if (actor.OID == TankTower)
            foreach (var (slot, player) in Raid.WithSlot(true, false, true))
                if (player.Role != Role.Tank)
                    forbidden.Set(slot);
        // Two observed waves give 9.79/9.91 seconds from creation to impact.
        // Bootstrap has no original creation time; immediate guidance avoids a
        // fresh ten-second allowance for a tower that may already be about to hit.
        var activation = _bootstrapSources.Contains(actor) ? WorldState.CurrentTime : WorldState.FutureTime(9.7d);
        Towers.Add(new(actor.Position, 5, 1, int.MaxValue, forbidden, activation, actor.InstanceID));
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

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ParticleBeam1 or (uint)AID.ParticleBeam2 or (uint)AID.ParticleBeam3 or (uint)AID.ParticleBeam4)
            Towers.RemoveAll(t => t.Position.AlmostEqual(spell.TargetXZ, 1));
    }

    private bool IsTankTower(Tower tower) => _sources.GetValueOrDefault(tower.ActorID)?.OID == TankTower;
    private bool Eligible(Tower tower, Actor actor) => !IsTankTower(tower) || actor.Role == Role.Tank;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (actor.Role != Role.Tank && Towers.Exists(t => IsTankTower(t) && t.IsInside(actor)))
            hints.Add("Leave the tank tower!");
        else if (Towers.Exists(t => Eligible(t, actor) && t.IsInside(actor)))
            hints.Add("Soak the tower!", false);
        else if (Towers.Exists(t => Eligible(t, actor) && t.NumInside(Module) < t.MinSoakers))
            hints.Add("Soak an empty tower!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var eligible = Towers.FindAll(t => Eligible(t, actor));
        var tankGaps = actor.Role == Role.Tank ? eligible.FindAll(t => IsTankTower(t) && t.NumInside(Module) < t.MinSoakers) : [];
        var occupied = eligible.FindIndex(t => t.IsInside(actor));
        var destinations = tankGaps.Count > 0 ? tankGaps : occupied >= 0 ? new List<Tower> { eligible[occupied] } : eligible.FindAll(t => t.NumInside(Module) < t.MinSoakers);
        if (destinations.Count > 0)
            hints.AddForbiddenZone(new SDIntersection(destinations.Select(t => t.InvertedShapeDistance ?? t.Shape.InvertedDistance(t.Position, t.Rotation)).ToArray()), destinations.Min(t => t.Activation));
        if (actor.Role != Role.Tank)
            foreach (var t in Towers.Where(IsTankTower))
                hints.AddForbiddenZone(t.Shape, t.Position, t.Rotation, t.Activation);
        if (Towers.Count > 0)
        {
            BitMask soakers = default;
            foreach (var (partySlot, member) in Raid.WithSlot(false, false, true))
                if (Towers.Exists(t => Eligible(t, member) && t.IsInside(member)))
                    soakers.Set(partySlot);
            if (soakers.Any())
                hints.AddPredictedDamage(soakers, Towers.Min(t => t.Activation));
        }
    }

    public override void Update()
    {
        foreach (var (id, actor) in _sources.ToArray())
            if (actor.IsDeadOrDestroyed || WorldState.Actors.Find(id) != actor)
            {
                Towers.RemoveAll(t => t.ActorID == id);
                _sources.Remove(id);
                _bootstrapSources.Remove(actor);
            }
        Towers.RemoveAll(t => WorldState.CurrentTime > t.Activation.AddSeconds(
            _sources.GetValueOrDefault(t.ActorID) is { } source && _bootstrapSources.Contains(source) ? 11.7d : 2d));
        BitMask nonTanks = default;
        foreach (var (slot, player) in Raid.WithSlot(true, false, true))
            if (player.Role != Role.Tank)
                nonTanks.Set(slot);
        for (var i = 0; i < Towers.Count; ++i)
            if (IsTankTower(Towers[i]))
            {
                var t = Towers[i];
                t.ForbiddenSoakers = nonTanks;
                Towers[i] = t;
            }
    }
}
