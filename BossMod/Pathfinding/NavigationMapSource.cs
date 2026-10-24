namespace BossMod.Pathfinding;

// ArenaBounds instances are replaced when authored geometry changes (the ArenaBounds contract).
// Copy the obstacle window; the source bitmap may be edited or replaced while a worker runs.
internal sealed record NavigationMapSource(ArenaBounds Bounds, WPos Center, int? ProjectionLayer, Bitmap.Region Obstacles)
{
    internal static NavigationMapSource Capture(ArenaBounds bounds, WPos center, int? layer, Bitmap.Region obstacles, bool disableObstacles)
    {
        if (disableObstacles || obstacles.Bitmap == null)
            return new(bounds, center, layer, default);
        var clipped = obstacles.Rect.Clamped(obstacles.Bitmap.FullRect);
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return new(bounds, center, layer, default);
        var copy = new Bitmap(clipped.Width, clipped.Height, obstacles.Bitmap.Color0, obstacles.Bitmap.Color1, obstacles.Bitmap.Resolution);
        new Bitmap.Region(obstacles.Bitmap, clipped).CopyTo(copy, 0, 0);
        var rect = obstacles.Rect;
        return new(bounds, center, layer, new(copy,
            new(rect.Left - clipped.Left, rect.Top - clipped.Top, rect.Right - clipped.Left, rect.Bottom - clipped.Top)));
    }

    internal void Init(Map map) => Init(map, Bounds, Center, ProjectionLayer, Obstacles);

    internal byte[]? InitForWorker(Map map)
    {
        InitBounds(map, Bounds, Center, ProjectionLayer);
        var count = checked(map.Width * map.Height);
        var blocks = count > 0 ? new byte[count] : null;
        if (blocks != null)
            for (var i = 0; i < count; ++i)
                blocks[i] = !float.IsFinite(map.PixelMaxG[i]) ? byte.MaxValue : map.PixelMaxG[i] < 0 ? (byte)1 : (byte)0;
        ApplyObstacles(map, Obstacles, blocks);
        return blocks;
    }

    internal static void Init(Map map, ArenaBounds bounds, WPos center, int? layer, Bitmap.Region obstacles)
    {
        InitBounds(map, bounds, center, layer);
        ApplyObstacles(map, obstacles, null);
    }

    private static void InitBounds(Map map, ArenaBounds bounds, WPos center, int? layer)
    {
        if (bounds is ArenaBoundsCustom custom)
            custom.PathfindMap(map, center, layer);
        else
            bounds.PathfindMap(map, center);
    }

    private static void ApplyObstacles(Map map, Bitmap.Region obstacles, byte[]? blocks)
    {
        if (obstacles.Bitmap == null)
            return;
        var offX = -obstacles.Rect.Left;
        var offY = -obstacles.Rect.Top;
        var r = obstacles.Rect.Clamped(obstacles.Bitmap.FullRect);
        for (var y = r.Top; y < r.Bottom; ++y)
        {
            var my = y + offY;
            if ((uint)my >= (uint)map.Height)
                continue;
            for (var x = r.Left; x < r.Right; ++x)
            {
                var mx = x + offX;
                if ((uint)mx >= (uint)map.Width || !obstacles.Bitmap[x, y])
                    continue;
                var index = map.GridToIndex(mx, my);
                if (blocks != null && blocks[index] == 0)
                    blocks[index] = 2;
                map.PixelMaxG[index] = -1000f;
                map.PixelPriority[index] = float.MinValue;
            }
        }
    }
}
