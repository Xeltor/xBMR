namespace BossMod.RealmReborn.Alliance.A23Amon;

class AddsAndAssignments(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, DateTime> _iceMarkers = [];
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == (uint)IconID.Icon3)
            _iceMarkers[actor] = WorldState.FutureTime(8);
    }
    public override void Update()
    {
        foreach (var actor in _iceMarkers.Keys.Where(a => a.IsDeadOrDestroyed || WorldState.Actors.Find(a.InstanceID) != a
            || a.FindStatus(SID.DeepFreeze) != null || a.FindStatus(SID.DeepFreezeLegacy) != null || WorldState.CurrentTime > _iceMarkers[a]).ToArray())
            _iceMarkers.Remove(actor);
    }
    private Actor? Orb(Actor player) => Module.Enemies((uint)OID.DimensionalCompression).FirstOrDefault(a => !a.IsDeadOrDestroyed && a.Tether.Target == player.InstanceID);
    private Actor? KumKum(Actor player) => Module.Enemies((uint)OID.KumKum).Where(a => !a.IsDeadOrDestroyed && a.InCombat && a.FindStatus(SID.Minimum) == null)
        .OrderBy(a => (a.Position - player.Position).LengthSq()).ThenBy(a => a.InstanceID).FirstOrDefault();
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_iceMarkers.ContainsKey(actor))
            hints.Add("Ice marker: leave Amon's hitbox so the ice provides cover!", actor.Position.InCircle(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius + 1.8f));
        if (Orb(actor) != null)
            hints.Add("Guide the shrinking orb into an unshrunk Kum Kum", false);
        if (actor.FindStatus(SID.FireToad) != null)
            hints.Add("Fire toad: evade the snakes and preserve ice for Curtain Call!", false);
        if (actor.FindStatus(SID.AetherochemicalBomb) != null)
            hints.Add("Aetherochemical Bomb: cleanse with Esuna!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_iceMarkers.ContainsKey(actor))
            hints.AddForbiddenZone(new SDCircle(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius + 1.8f));
        if (Orb(actor) != null && KumKum(actor) is { } kum)
            hints.GoalZones.Add(AIHints.GoalProximity(kum.Position, 1, 10));
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (target.OID == (uint)OID.IceCage)
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
            else if (target.OID == (uint)OID.DimensionalCompression)
                enemy.Priority = AIHints.Enemy.PriorityUndesirable;
            else if (target.InCombat && enemy.Priority >= 0)
            {
                enemy.Priority = target.OID switch
                {
                    (uint)OID.ExperimentalByProduct33 or (uint)OID.ExperimentalByProduct66 => 4,
                    (uint)OID.Kichiknebik => 3,
                    (uint)OID.KumKum => 2,
                    (uint)OID.Boss => 1,
                    _ => enemy.Priority
                };
                if (target.OID is (uint)OID.ExperimentalByProduct33 or (uint)OID.ExperimentalByProduct66)
                    enemy.ShouldBeStunned = true;
            }
        }
    }
}
