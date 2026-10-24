using BossMod.CombatAssistance.Config;
using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Presentation is evaluated over the same complete native map as execution. It
// never changes traversal, assignments, action value, delivery time or resources.
internal static class NativePresentationCapture
{
    internal static float[] Capture(Map map, AIHints hints, WorldState world, Actor player,
        AssistancePreferences settings, float[] nativeCushion, bool encounter, FormationPreference? formation = null)
    {
        var peers = settings.ManageSocialSpacing
            ? world.Party.WithoutSlot().Where(a => a.InstanceID != player.InstanceID && !a.IsDestroyed)
                .Select(a => a.Position).ToArray() : [];
        var enemies = settings.AvoidStandingInsideEnemies
            ? hints.PriorityTargetsSpan.ToArray().Select(e => (e.Actor.Position, e.Actor.HitboxRadius)).ToArray() : [];
        // Read the selected native projection polygon on the framework thread.
        // No arena wrappers or polygon indices escape into the worker.
        var polygon = hints.PathfindMapBounds.Shape;
        if (hints.PathfindMapBounds is ArenaBoundsCustom { WorldProjectionLayers: { } layers }
            && hints.PathfindMapArenaProjectionLayer is int layer && layer >= 0 && layer < layers.Length)
            polygon = ((ArenaBoundsCustom)hints.PathfindMapBounds).ProjectionLayer2DShape(layer);
        var useEdge = encounter && settings.AvoidArenaEdge;
        if (useEdge)
            polygon.VerifyPolygonIndexExistance();
        var values = (float[])nativeCushion.Clone();
        if (settings.ManageForbiddenZoneDistance)
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
                score -= Math.Max(0, 1.15f - (point - peer).Length()) / 1.15f;
            foreach (var (center, radius) in enemies)
                if (radius > 0)
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
