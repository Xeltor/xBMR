namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

// Filled supporting deck recovered independently from tr2022/tr2122 collision
// meshes in game 2026.10.06.0000.0000. Preserve the authored slight edge skew.
// The old rectangle at (300,410) extended beyond the east and north deck edges.
static class ArenaGeometry
{
    public static readonly ArenaBoundsCustom Deck = new([new PolygonCustom([
        new(269.01981f, 395.02054f), new(328.99789f, 395.00156f),
        new(328.99792f, 409.68610f), new(328.99792f, 424.30157f),
        new(299.00888f, 424.31308f), new(269.01984f, 424.32455f)
    ])]);
}

// These are the two b0250 purple seals, not the wooden rails broken at pull.
// Their actor facing differs from the bound SGB's authored -1.570451 rotation.
class ShipWalls(BossModule module) : BossComponent(module)
{
    private static readonly Angle Rotation = new(-1.570451f);
    private static readonly (uint OID, WPos Position)[] Doors = [(2002872, new(329.6231f, 409.6169f)), (2007602, new(268.5849f, 409.653f))];

    private IEnumerable<WPos> ActiveDoors()
    {
        foreach (var (oid, position) in Doors)
            foreach (var actor in Module.Enemies(oid))
                if (!actor.IsDeadOrDestroyed && actor.EventState == 0 && actor.Renderflags == 0 && actor.Position.AlmostEqual(position, 1))
                    yield return position;
    }

    public Components.GenericKnockback.SafeWall[] Walls(float lateralMargin = 0)
    {
        List<Components.GenericKnockback.SafeWall> result = [];
        var forward = Rotation.ToDirection();
        var right = forward.OrthoR();
        foreach (var position in ActiveDoors())
        {
            var halfWidth = Math.Max(0, 15 - lateralMargin);
            foreach (var normal in new[] { forward, -forward })
            {
                var center = position + 0.75f * normal;
                var side = halfWidth * right;
                result.Add(new(center - side, center + side));
            }
            foreach (var normal in new[] { right, -right })
            {
                var center = position + 15 * normal;
                var side = Math.Max(0, 0.75f - lateralMargin) * forward;
                result.Add(new(center - side, center + side));
            }
        }
        return [.. result];
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var position in ActiveDoors())
            hints.TemporaryObstacles.Add(new SDRect(position, Rotation, 0.75f, 0.75f, 15));
    }
}
