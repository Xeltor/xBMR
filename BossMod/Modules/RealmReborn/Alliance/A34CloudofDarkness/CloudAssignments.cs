namespace BossMod.RealmReborn.Alliance.A34CloudofDarkness;

class CloudAssignments(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, DateTime> _entry = [];
    private readonly HashSet<Actor> _preexisting = [.. module.Enemies((uint)OID.HyperchargedCloud)];
    private readonly HashSet<uint> _closed = [];
    private static int Group(Actor cloud) => cloud.Position.Z < -407 ? 1 : cloud.Position.X < -300 ? 0 : 2;
    private static uint Barrier(int group) => group switch { 0 => (uint)OID.Actor1e9752, 1 => (uint)OID.Actor1e9750, _ => (uint)OID.Actor1e9751 };
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.HyperchargedCloud && !actor.IsDeadOrDestroyed)
            _entry.TryAdd(actor, _preexisting.Remove(actor) ? WorldState.CurrentTime : WorldState.FutureTime(7));
    }
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID is (uint)OID.Actor1e9750 or (uint)OID.Actor1e9751 or (uint)OID.Actor1e9752)
        {
            if (state == 0x00100020)
                _closed.Add(actor.OID);
            else if (state is 0x00010040 or 0x00040008)
                _closed.Remove(actor.OID);
        }
    }
    public override void OnActorDeath(Actor actor) => _entry.Remove(actor);
    public override void OnActorDestroyed(Actor actor)
    {
        _entry.Remove(actor);
        _closed.Remove(actor.OID);
    }
    public override void Update()
    {
        foreach (var cloud in _entry.Keys.Where(c => c.IsDeadOrDestroyed).ToArray())
            _entry.Remove(cloud);
    }
    private Actor? Assigned(int slot) => _entry.Keys.FirstOrDefault(c => Group(c) == WorldOfDarknessAlliance.Group(Raid, slot));
    private Actor? EntryDestination(int slot, Actor actor) => Assigned(slot) is { } cloud && !_closed.Contains(Barrier(Group(cloud)))
        && Module.FindComponent<FeintParticleBeam>()?.IsChased(actor) != true ? cloud : null;

    public bool HasBoxAssignment(int slot) => Assigned(slot) != null;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (EntryDestination(slot, actor) is { } entry)
            hints.Add("Enter your alliance's marked box!", !actor.Position.InCircle(entry.Position, 3));
        else if (Assigned(slot) != null)
            hints.Add("Defeat your alliance's Hypercharged Cloud!", false);
        else if (_entry.Count != 0 && WorldOfDarknessAlliance.Group(Raid, slot) < 0)
            hints.Add("Hypercharged clouds: A southwest, B north, C southeast!", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (EntryDestination(pcSlot, pc) is { } entry)
            Arena.AddCircleUnfilled(entry.Position, 3, Colors.Safe);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var own = Assigned(slot);
        var group = WorldOfDarknessAlliance.Group(Raid, slot);
        if (EntryDestination(slot, actor) is { } entry)
        {
            // Reach the center before closure. Once sealed, release the staging
            // restriction so the player can fight and dodge within the box.
            hints.AddForbiddenZone(new SDInvertedCircle(entry.Position, 3), _entry[entry]);
        }
        foreach (var enemy in hints.PotentialTargets)
        {
            if (!enemy.Actor.InCombat || enemy.Priority < 0)
                continue;
            if (enemy.Actor.OID == (uint)OID.HyperchargedCloud)
                enemy.Priority = group < 0 ? AIHints.Enemy.PriorityUndesirable : enemy.Actor == own ? 5 : AIHints.Enemy.PriorityUndesirable;
            else if (enemy.Actor.OID is (uint)OID.DarkCloud or (uint)OID.DarkStorm)
            {
                enemy.Priority = enemy.Actor.OID == (uint)OID.DarkStorm ? 4 : 3;
                // A body-block preference; ordinary hazards and movement constraints still win.
                if (own == null && _entry.Count == 0)
                {
                    var delta = Module.PrimaryActor.Position - enemy.Actor.Position;
                    var distance = delta.Length();
                    if (distance > 0)
                        hints.GoalZones.Add(AIHints.GoalProximity(enemy.Actor.Position + delta / distance, 1, 1));
                }
            }
            else if (enemy.Actor.OID == (uint)OID.Boss && group >= 0 && _closed.Contains(Barrier(group)))
                enemy.Priority = AIHints.Enemy.PriorityUndesirable;
        }
    }
}
