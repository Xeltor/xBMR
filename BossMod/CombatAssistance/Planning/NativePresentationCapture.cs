using BossMod.CombatAssistance.Config;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Presentation is evaluated over the same complete native map as execution. It
// never changes traversal, assignments, action value, delivery time or resources.
internal static class NativePresentationCapture
{
    // Port XCAI's bounded center clearance, not its dwell/after-arrival trigger.
    // A huge hitbox can cover most of the playable arena; its interior remains
    // usable once clear of the center. This is only a destination preference.
    internal static float EnemyCenterRadius(float hitboxRadius)
        => float.IsFinite(hitboxRadius) && hitboxRadius > 0 ? Math.Clamp(hitboxRadius + 0.35f, 1.25f, 4.35f) : 0;

    internal static float[] Capture(Map map, AIHints hints, WorldState world, Actor player,
        AssistancePreferences settings, float[] nativeCushion, bool encounter, FormationPreference? formation = null)
    {
        var peers = settings.ManageSocialSpacing
            ? world.Party.WithoutSlot().Where(a => a.InstanceID != player.InstanceID && !a.IsDestroyed)
                .Select(a => a.Position).ToArray() : [];
        var enemies = settings.AvoidStandingInsideEnemies
            ? hints.PriorityTargetsSpan.ToArray().Select(e => (e.Actor.Position, EnemyCenterRadius(e.Actor.HitboxRadius))).ToArray() : [];
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
            foreach (var peer in peers)
                if ((point - peer).LengthSq() < 1.15f * 1.15f)
                    score -= Math.Max(0, 1.15f - (point - peer).Length()) / 1.15f;
            foreach (var (center, radius) in enemies)
                if (radius > 0 && (point - center).LengthSq() < radius * radius)
                    score -= Math.Max(0, radius - (point - center).Length()) / radius;
            if (formation != null)
                score += formation.Value(point);
            if (useEdge)
            {
                var offset = point - hints.PathfindMapCenter;
                var clearance = polygon.Contains(offset) ? (polygon.ClosestPointOnBoundary(offset) - offset).Length() : 0;
                score -= Math.Max(0, 2.5f - clearance) / 2.5f;
            }
            values[cell] = score;
        }
        return values;
    }
}
