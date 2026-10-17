namespace BossMod.Pathfinding;

// The inherited SimpleBossModule search origin is held independently of the moving arena.
// A terrain window must use that same origin, snapped to the source bitmap's lattice.
internal static class DefaultTerrainFrame
{
    internal sealed record Alignment(WPos OriginalCenter, WPos Center, WPos ObstacleOrigin, float Resolution, Bitmap.Region Region)
    {
        internal bool Matches(AIHints hints) => ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds)
            && hints.PathfindMapArenaProjectionLayer == null && hints.PathfindMapCenter == Center
            && ReferenceEquals(hints.PathfindMapObstacles.Bitmap, Region.Bitmap) && Region.Bitmap.PixelSize == Resolution
            && hints.PathfindMapObstacles.Rect.Equals(Region.Rect);
    }

    internal static Alignment? Align(AIHints hints, ObstacleMapDatabase.Entry? entry, Bitmap? bitmap)
    {
        if (!ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds) || hints.PathfindMapArenaProjectionLayer != null
            || entry == null || bitmap == null || !ReferenceEquals(hints.PathfindMapObstacles.Bitmap, bitmap)
            || bitmap.Width <= 0 || bitmap.Height <= 0 || bitmap.Resolution <= 0 || bitmap.PixelSize != AIHints.DefaultBounds.MapResolution)
            return null;
        var center = hints.PathfindMapCenter;
        var origin = entry.Origin;
        static bool Finite(WPos p) => float.IsFinite(p.X) && float.IsFinite(p.Z) && Math.Abs(p.X) <= 1_000_000 && Math.Abs(p.Z) <= 1_000_000;
        if (!Finite(center) || !Finite(origin)) return null;
        var resolution = bitmap.PixelSize;
        var local = (center - origin) / resolution;
        var half = AIHints.DefaultBounds.Radius / resolution;
        // Default bounds have an even, axis-aligned grid. Do not guess another grid's origin.
        if (!float.IsFinite(half) || half <= 0 || half > 4096 || half != MathF.Truncate(half)
            || !float.IsFinite(local.X) || !float.IsFinite(local.Z) || Math.Abs(local.X) > 1_000_000 || Math.Abs(local.Z) > 1_000_000)
            return null;
        var x = (int)MathF.Round(local.X);
        var z = (int)MathF.Round(local.Z);
        var aligned = origin + resolution * new WDir(x, z);
        if (!Finite(aligned)) return null;
        var extent = (int)half;
        return new(center, aligned, origin, resolution, new(bitmap, new(x - extent, z - extent, x + extent, z + extent)));
    }
}
