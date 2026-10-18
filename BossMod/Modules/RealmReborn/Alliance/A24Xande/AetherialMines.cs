namespace BossMod.RealmReborn.Alliance.A24Xande;

class AetherialMines(BossModule module) : Components.GenericTowers(module)
{
    private readonly Dictionary<ulong, ulong> _assignments = [];
    private readonly HashSet<Actor> _preexisting = [.. module.Enemies((uint)OID.AetherialMine)];
    private readonly HashSet<ulong> _unknownTiming = [];
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.AetherialMine && !actor.IsDeadOrDestroyed && !Towers.Any(t => t.ActorID == actor.InstanceID))
        {
            // A reload cannot recover an existing mine's age. Never grant it another9s.
            var existing = _preexisting.Remove(actor);
            if (existing)
                _unknownTiming.Add(actor.InstanceID);
            // Observed spawn-to-explosion is 9.94s. Aim to arrive in 9s; radius3 is Action2366.
            Towers.Add(new(actor.Position, 3, 1, int.MaxValue, activation: existing ? WorldState.CurrentTime : WorldState.FutureTime(9), actorID: actor.InstanceID));
        }
    }
    public override void OnActorDeath(Actor actor) => Remove(actor);
    public override void OnActorDestroyed(Actor actor) => Remove(actor);
    private void Remove(Actor actor)
    {
        Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
        _assignments.Remove(actor.InstanceID);
        _unknownTiming.Remove(actor.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.AetherochemicalExplosion or (uint)AID.AetherochemicalReaction)
            Remove(caster);
    }
    public override void Update()
    {
        var previous = new Dictionary<ulong, ulong>(_assignments);
        _assignments.Clear();
        var players = Raid.WithoutSlot().Where(p => !p.IsDeadOrDestroyed && p.Type == ActorType.Player && Arena.InBounds(p.Position)).ToArray();
        var used = new HashSet<ulong>();
        // Honor players already soaking a circle, including manually controlled alliance members.
        foreach (var tower in Towers.OrderBy(t => t.ActorID))
        {
            var inside = players.Where(p => tower.IsInside(p)).OrderBy(p => p.InstanceID).ToArray();
            if (inside.Length != 0)
            {
                _assignments[tower.ActorID] = inside[0].InstanceID;
                foreach (var p in inside)
                    used.Add(p.InstanceID);
            }
        }
        foreach (var tower in Towers.OrderBy(t => t.ActorID))
        {
            if (_assignments.ContainsKey(tower.ActorID))
                continue;
            var volunteers = players.Where(p => !used.Contains(p.InstanceID) && !(p.Role == Role.Tank && Module.PrimaryActor.TargetID == p.InstanceID));
            var selected = previous.TryGetValue(tower.ActorID, out var assigned) ? volunteers.FirstOrDefault(p => p.InstanceID == assigned) : null;
            selected ??= volunteers.OrderBy(p => (p.Position - tower.Position).LengthSq()).ThenBy(p => p.InstanceID).FirstOrDefault();
            if (selected != null)
            {
                _assignments[tower.ActorID] = selected.InstanceID;
                used.Add(selected.InstanceID);
            }
        }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var tower in Towers)
            if (_assignments.GetValueOrDefault(tower.ActorID) == actor.InstanceID)
                hints.Add("Soak your assigned yellow circle!", !tower.IsInside(actor));
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_unknownTiming.Count != 0)
            hints.Add("Yellow circles already present: explosion timing unknown; soak now");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var tower in Towers)
            if (_assignments.GetValueOrDefault(tower.ActorID) == actor.InstanceID)
                hints.AddForbiddenZone(new SDInvertedCircle(tower.Position, 2.5f), tower.Activation);
    }
}
