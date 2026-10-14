namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

class VoidAeroIIIKnockback(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.Unknown3, 20)
{
    private readonly ShipWalls? _walls = module.FindComponent<ShipWalls>();
    private readonly List<Knockback> _sources = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(s => s.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(s => s.ActorID == actor.InstanceID);

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _sources.Clear();
        var walls = _walls?.Walls() ?? [];
        foreach (var source in Casters)
        {
            var offset = actor.Position - source.Origin;
            var direction = offset.LengthSq() > 0.0001f ? offset.Normalized() : default;
            // GenericKnockback clamps a miss to arena bounds when SafeWalls is nonempty.
            // Only attach faces to an actual collision: the long sides are lethal open edges.
            var stoppingWalls = walls.Any(w => Intersect.RaySegment(actor.Position, direction, w.Vertex1, w.Vertex2) <= source.Distance) ? walls : [];
            _sources.Add(new(source.Origin, source.Distance, source.Activation, source.Shape, source.Direction, source.Kind,
                source.MinDistance, stoppingWalls, source.ActorID, source.IgnoreImmunes, source.ArenaProjectionLayer, source.RestrictToArenaProjectionLayer));
        }
        return CollectionsMarshal.AsSpan(_sources);
    }

    private bool ArrivalUnsafe(int slot, Actor actor, WPos position, DateTime activation)
    {
        if (!Arena.InBounds(position) || Module.Enemies((uint)OID.FierceWind).Any(w => !w.IsDeadOrDestroyed && position.InCircle(w.Position, 4)))
            return true;
        // The recorded Bolt lands ~0.8s after this knockback. Reject a landing
        // in that overlapping cast rather than relying on the starting point.
        if (Module.FindComponent<BoltOfDarkness3>() is { } bolt)
            foreach (ref readonly var aoe in bolt.ActiveAOEs(slot, actor))
                if (aoe.Risky && aoe.Activation >= activation.AddSeconds(-0.1) && aoe.Activation <= activation.AddSeconds(1)
                    && aoe.Shape.Check(position, aoe.Origin, aoe.Rotation))
                    return true;
        return false;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos position)
        => ArrivalUnsafe(slot, actor, position, Casters.Count > 0 ? Casters[0].Activation : WorldState.CurrentTime);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var walls = _walls?.Walls(actor.HitboxRadius) ?? [];
        if (walls.Length == 0)
            return; // unobserved/inactive seals do not invent a solid wall or an impossible safe region
        foreach (var source in Casters)
            if (!IsImmune(slot, source.Activation))
                hints.AddForbiddenZone(new Landing(this, slot, actor, source.Origin, walls, source.Distance, source.Activation), source.Activation.AddSeconds(-0.3));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Casters.Count > 0 && !IsImmune(slot, Casters[0].Activation))
        {
            if ((_walls?.Walls().Length ?? 0) > 0)
                base.AddHints(slot, actor, hints);
            hints.Add("Aim knockback into a purple wall; avoid the open sides!", false);
        }
    }

    private sealed class Landing(VoidAeroIIIKnockback component, int slot, Actor actor, WPos origin, SafeWall[] walls, float distance, DateTime activation) : ShapeDistance
    {
        public override bool Contains(in WPos p)
        {
            var offset = p - origin;
            if (offset.LengthSq() < 0.0001f)
                return true;
            var direction = offset.Normalized();
            var travel = distance;
            foreach (var wall in walls)
                travel = Math.Min(travel, Intersect.RaySegment(p, direction, wall.Vertex1, wall.Vertex2));
            return component.ArrivalUnsafe(slot, actor, p + Math.Max(0, travel - 0.01f) * direction, activation);
        }
        public override float Distance(in WPos p) => Contains(p) ? 0 : 1;
    }
}
