namespace BossMod.RealmReborn.Alliance.A23Amon;

class AddsAndAssignments(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, DateTime> _iceMarkers = [];
    private readonly Dictionary<Actor, Actor> _intercepts = [];
    private readonly HashSet<Actor> _resolvedOrbs = [];
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
        foreach (var orb in _intercepts.Where(p => p.Key.IsDeadOrDestroyed || p.Key.Tether.Target == 0 || p.Value.IsDeadOrDestroyed
            || p.Value.FindStatus(SID.Minimum) != null).Select(p => p.Key).ToArray())
            _intercepts.Remove(orb);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _iceMarkers.Remove(actor);
        _intercepts.Remove(actor);
        _resolvedOrbs.Remove(actor);
        foreach (var orb in _intercepts.Where(p => p.Value == actor).Select(p => p.Key).ToArray())
            _intercepts.Remove(orb);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Mini && caster.OID == (uint)OID.DimensionalCompression)
        {
            _resolvedOrbs.Add(caster);
            _intercepts.Remove(caster);
        }
    }
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == (uint)OID.DimensionalCompression && tether.Target != 0)
            _resolvedOrbs.Remove(source);
    }
    private Actor? Orb(Actor player) => Module.Enemies((uint)OID.DimensionalCompression).FirstOrDefault(a => !a.IsDeadOrDestroyed && !_resolvedOrbs.Contains(a) && a.Tether.Target == player.InstanceID);
    private WPos? Intercept(Actor player)
    {
        if (Orb(player) is not { } orb)
            return null;
        var candidates = Module.Enemies((uint)OID.KumKum).Where(a => !a.IsDeadOrDestroyed && a.InCombat && a.FindStatus(SID.Minimum) == null)
            .OrderBy(a => _intercepts.GetValueOrDefault(orb) == a ? 0 : 1).ThenBy(a => (a.Position - player.Position).LengthSq()).ThenBy(a => a.InstanceID);
        foreach (var kum in candidates)
        {
            var direction = kum.Position - orb.Position;
            if (direction.LengthSq() <= 0.01f)
                continue;
            // Stand on the far side so the pursuing orb crosses the add first.
            // One yard is an arrival tolerance, not a measured interception radius.
            var destination = kum.Position + (kum.HitboxRadius + player.HitboxRadius + 1) * direction.Normalized();
            if (!Arena.InBounds(destination) || Module.Components.OfType<Components.GenericAOEs>().Any(c => c.ActiveAOEs(Raid.FindSlot(player.InstanceID), player).ToArray().Any(a => a.Risky && a.Check(destination))))
                continue;
            if (kum.CastInfo is { } cast && cast.Action.ID == (uint)AID.Darkness
                && destination.InCircle(kum.Position, 6 + kum.HitboxRadius) && (destination - kum.Position).Dot(cast.Rotation.ToDirection()) >= 0)
                continue;
            _intercepts[orb] = kum;
            return destination;
        }
        return null;
    }
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
        var frozen = actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.DeepFreezeLegacy) != null;
        if (!frozen && _iceMarkers.ContainsKey(actor))
            hints.AddForbiddenZone(new SDCircle(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius + 1.8f));
        if (!frozen && Intercept(actor) is { } interception)
            hints.AddForbiddenZone(new SDInvertedCircle(interception, 1));
        if (!frozen && actor.FindStatus(SID.FireToad) != null)
            foreach (var snake in Module.Enemies((uint)OID.Kichiknebik))
                if (!snake.IsDeadOrDestroyed && snake.IsTargetable)
                    // Contact-avoidance policy for the pursuing snakes; not a drawn AOE.
                    hints.AddForbiddenZone(new SDCircle(snake.Position, snake.HitboxRadius + actor.HitboxRadius + 3));
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
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (pc.FindStatus(SID.DeepFreeze) == null && pc.FindStatus(SID.DeepFreezeLegacy) == null && Intercept(pc) is { } destination)
        {
            Arena.AddCircleUnfilled(destination, 1, Colors.Safe);
            Arena.AddLine(pc.Position, destination, Colors.Safe);
        }
    }
}
