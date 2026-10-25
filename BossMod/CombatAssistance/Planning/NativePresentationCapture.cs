using BossMod.CombatAssistance.Config;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

internal sealed record PlanningEnemyFootprint(ulong Id, WPos Center, float Radius);
internal sealed record PlanningArenaInterior(ImmutableArray<bool> Cells)
{
    internal bool Same(PlanningArenaInterior other) => Cells.AsSpan().SequenceEqual(other.Cells.AsSpan());
}

// Presentation is evaluated over the same complete native map as execution. It
// never changes traversal, assignments, action value, delivery time or resources.
internal static class NativePresentationCapture
{
    internal const float ArenaEdgeMargin = 2.5f;

    internal static PlanningArenaInterior? CaptureArenaInterior(Map map, AIHints hints, bool enabled)
    {
        if (!enabled || ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds)) return null;
        var polygon = hints.PathfindMapBounds.Shape;
        if (hints.PathfindMapBounds is ArenaBoundsCustom { WorldProjectionLayers: { Length: > 0 } layers } custom)
        {
            if (hints.PathfindMapArenaProjectionLayer is not int layer || layer < 0 || layer >= layers.Length) return null;
            polygon = custom.ProjectionLayer2DShape(layer);
        }
        if (polygon == null || polygon.Parts.Count == 0) return null;
        polygon.VerifyPolygonIndexExistance();
        var cells = ImmutableArray.CreateBuilder<bool>(map.Width * map.Height);
        for (var cell = 0; cell < cells.Capacity; ++cell)
        {
            var offset = map.GridToWorld(cell % map.Width, cell / map.Width, .5f, .5f) - hints.PathfindMapCenter;
            cells.Add(polygon.Contains(offset)
                && (polygon.ClosestPointOnBoundary(offset) - offset).Length() >= ArenaEdgeMargin);
        }
        return new(cells.MoveToImmutable());
    }

    internal static ImmutableArray<PlanningEnemyFootprint> CaptureEnemyFootprints(AIHints hints, Actor player)
        => hints.PriorityTargetsSpan.ToArray().Select(e => e.Actor)
            .Where(a => !a.IsDead && a.HitboxRadius > 0 && float.IsFinite(a.HitboxRadius)
                && Math.Abs(a.PosRot.Y - player.PosRot.Y) <= a.HitboxRadius + player.HitboxRadius)
            .Select(a => new PlanningEnemyFootprint(a.InstanceID, a.Position,
                MathF.Sqrt(Math.Max(0, (a.HitboxRadius + player.HitboxRadius) * (a.HitboxRadius + player.HitboxRadius)
                    - (a.PosRot.Y - player.PosRot.Y) * (a.PosRot.Y - player.PosRot.Y))) + .35f)).ToImmutableArray();

    // Port XCAI's bounded center clearance, not its dwell/after-arrival trigger.
    // A huge hitbox can cover most of the playable arena; its interior remains
    // usable once clear of the center. This is only a destination preference.
    internal static float EnemyCenterRadius(float hitboxRadius)
        => float.IsFinite(hitboxRadius) && hitboxRadius > 0 ? Math.Clamp(hitboxRadius + 0.35f, 1.25f, 4.35f) : 0;

    internal static float[] Capture(Map map, AIHints hints, WorldState world, Actor player,
        AssistancePreferences settings, float[] nativeCushion, bool encounter, FormationPreference? formation = null, SocialSpacingPreference? social = null)
    {
        var enemies = settings.AvoidStandingInsideEnemies
            ? hints.PriorityTargetsSpan.ToArray()
                // Being covered by a moving boss is not a new movement intent.
                .Where(e => !player.Position.InCircle(e.Actor.Position, e.Actor.HitboxRadius + player.HitboxRadius + .35f))
                .Select(e => (e.Actor.Position, EnemyCenterRadius(e.Actor.HitboxRadius))).ToArray() : [];
        // Read the selected native projection polygon on the framework thread.
        // No arena wrappers or polygon indices escape into the worker.
        var polygon = hints.PathfindMapBounds.Shape;
        if (hints.PathfindMapBounds is ArenaBoundsCustom { WorldProjectionLayers: { } layers }
            && hints.PathfindMapArenaProjectionLayer is int layer && layer >= 0 && layer < layers.Length)
            polygon = ((ArenaBoundsCustom)hints.PathfindMapBounds).ProjectionLayer2DShape(layer);
        var useEdge = encounter && settings.AvoidArenaEdge;
        if (useEdge)
            polygon.VerifyPolygonIndexExistance();
        var values = settings.ManageForbiddenZoneDistance ? new float[nativeCushion.Length] : (float[])nativeCushion.Clone();
        if (settings.ManageForbiddenZoneDistance && NativeNavigationSnapshot.HasForbiddenCells(map))
        {
            var cushion = new Map();
            cushion.Init(map, map.Center);
            NavigationDecision.AvoidForbiddenZone(cushion, settings.PreferredForbiddenZoneDistance);
            for (var i = 0; i < values.Length; ++i)
                values[i] = cushion.PixelPriority[i] - map.PixelPriority[i];
        }
        for (var cell = 0; cell < values.Length; ++cell)
        {
            var point = map.GridToWorld(cell % map.Width, cell / map.Width, 0.5f, 0.5f);
            var score = values[cell];
            if (social != null)
                score += social.Value(point);
            foreach (var (center, radius) in enemies)
                if (radius > 0 && (point - center).LengthSq() < radius * radius)
                    score -= Math.Max(0, radius - (point - center).Length()) / radius;
            if (formation != null)
                score += formation.Value(point);
            if (useEdge)
            {
                var offset = point - hints.PathfindMapCenter;
                var clearance = polygon.Contains(offset) ? (polygon.ClosestPointOnBoundary(offset) - offset).Length() : 0;
                score -= Math.Max(0, ArenaEdgeMargin - clearance) / ArenaEdgeMargin;
            }
            values[cell] = score;
        }
        return values;
    }
}
