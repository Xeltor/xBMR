namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

// Installed b1053 shared-group CollisionBox: local centre (0,8,0), half-extents (4,8,4).
// Both ic_on timelines enable collider6 at frame0; both ic_off timelines disable it.
class IceBlocks(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, Actor> _active = [];
    private static readonly AOEShapeRect Shape = new(4, 4, 4);

    public override void OnActorEAnim(Actor actor, uint state)
    {
        // Use the native/Excel decimal row IDs directly to avoid lossy manual hexadecimal transcription.
        if (actor.OID is < 2007603 or > 2007608)
            return;
        // BossModule packs the native parameters as (shared state << 16) | immediate timeline bitmask.
        if (state == 0x00040008)
            _active[actor.InstanceID] = actor;
        else
            _active.Remove(actor.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_active.GetValueOrDefault(actor.InstanceID) == actor)
            _active.Remove(actor.InstanceID);
    }

    public override void Update()
    {
        foreach (var (id, actor) in _active.ToArray())
            if (actor.IsDeadOrDestroyed || WorldState.Actors.Find(id) != actor)
                _active.Remove(id);
    }

    public Components.GenericKnockback.SafeWall[] Walls(float lateralMargin = 0)
    {
        List<Components.GenericKnockback.SafeWall> walls = [];
        var halfWidth = Math.Max(0, 4 - lateralMargin);
        foreach (var actor in _active.Values)
        {
            var forward = actor.Rotation.ToDirection();
            var right = forward.OrthoR();
            foreach (var normal in new[] { forward, -forward, right, -right })
            {
                var centre = actor.Position + 4 * normal;
                var side = halfWidth * normal.OrthoR();
                walls.Add(new(centre - side, centre + side));
            }
        }
        return [.. walls];
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var ice in _active.Values)
            hints.TemporaryObstacles.Add(new SDRect(ice.Position, ice.Rotation, 4, 4, 4));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var ice in _active.Values)
            Shape.Outline(Arena, ice.Position, ice.Rotation, Colors.Object);
    }
}
