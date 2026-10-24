namespace BossMod.CombatAssistance.Camera;

// Camera-only values, gathered with the native world frame. The arena remains a
// native geometry object used on the framework thread, never a JSON approximation.
internal sealed record CameraArena(ArenaBounds Bounds, WPos Origin)
{
    internal Vector2 Center => new(Origin.X, Origin.Z);
    internal float BoundingRadius => Bounds.Radius;
    internal bool Contains(Vector2 position) => Bounds.Contains(new(position.X - Origin.X, position.Y - Origin.Z));
}

internal sealed record CameraScene(ulong PlayerId, Vector3 PlayerPosition, Vector3 FightCenter,
    ImmutableArray<CameraSubject> Enemies, ImmutableArray<CameraSubject> Party,
    ImmutableArray<CameraSubject> Alliance, CameraArena? Arena)
{
    internal static CameraScene? Capture(WorldState world, AIHints hints, bool authoredArena)
    {
        var player = world.Party.Player();
        if (player is not { InCombat: true, IsDead: false, IsDestroyed: false })
            return null;
        var position = player.PosRot.XYZ();
        var enemies = hints.PriorityTargetsSpan.ToArray().Select(e => e.Actor)
            .Where(a => !a.IsDead && !a.IsDestroyed && a.InCombat && a.IsTargetable
                && Vector3.Distance(position, a.PosRot.XYZ()) <= 30 + a.HitboxRadius).ToArray();
        if (enemies.Length == 0)
            return null;
        var center = enemies.Aggregate(Vector3.Zero, (sum, actor) => sum + actor.PosRot.XYZ()) / enemies.Length;
        var party = ImmutableArray.CreateBuilder<CameraSubject>();
        var alliance = ImmutableArray.CreateBuilder<CameraSubject>();
        foreach (var (slot, actor) in world.Party.WithSlot())
        {
            if (actor.InstanceID == player.InstanceID || Vector3.DistanceSquared(position, actor.PosRot.XYZ()) > 1600)
                continue;
            (slot < PartyState.MaxPartySize || slot >= PartyState.MaxAllianceSize ? party : alliance)
                .Add(new(actor.PosRot.XYZ(), actor.HitboxRadius, 2, 1));
        }
        return new(player.InstanceID, position, center,
            enemies.Select(a => new CameraSubject(a.PosRot.XYZ(), a.HitboxRadius, Math.Clamp(a.HitboxRadius, 2, 20), 2)).ToImmutableArray(),
            party.ToImmutable(), alliance.ToImmutable(), authoredArena ? new(hints.PathfindMapBounds, hints.PathfindMapCenter) : null);
    }
}
