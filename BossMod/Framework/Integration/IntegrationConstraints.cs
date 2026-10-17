using BossMod.Pathfinding;

namespace BossMod.Integration;

// Additional timed constraints, independently owned. Authored native lists remain untouched.
internal sealed class IntegrationConstraints
{
    internal sealed record Entry(string Owner, IntegrationConstraintMask Shape, DateTime Activation, Func<bool> Validate, Action Retire);
    private readonly List<Entry> _entries = [];
    internal ShapeDistance[] Capture()
    {
        foreach (var entry in _entries.ToArray())
            if (!entry.Shape.Live || !entry.Validate()) { entry.Retire(); _entries.Remove(entry); }
        return _entries.Select(e => (ShapeDistance)e.Shape).ToArray();
    }
    internal (ShapeDistance shapeDistance, DateTime activation, ulong Source)[] CaptureZones()
    {
        Capture();
        return _entries.OrderBy(e => e.Activation).Select(e => ((ShapeDistance)e.Shape, e.Activation, 0UL)).ToArray();
    }
    internal ShapeDistance[] CaptureExcept(string owner)
    {
        Capture();
        return _entries.Where(e => e.Owner != owner).Select(e => (ShapeDistance)e.Shape).ToArray();
    }
    internal bool Add(Entry entry)
    {
        Capture();
        foreach (var old in _entries.Where(e => e.Owner == entry.Owner).ToArray()) { old.Retire(); _entries.Remove(old); }
        if (_entries.Count >= IntegrationSession.MaxOwners) return false;
        _entries.Add(entry); return true;
    }
}

// Immutable native-cell mask. Callback execution happens only during bounded worker construction.
internal sealed class IntegrationConstraintMask(NavigationRaster grid, byte[] blocked, Func<bool> live) : ShapeDistance
{
    internal bool Live => live();
    public override bool Contains(in WPos p)
    {
        if (!Live) return false;
        var cell = grid.WorldToGrid(p); var x = (int)MathF.Floor(cell.X); var y = (int)MathF.Floor(cell.Y);
        return !float.IsFinite(cell.X) || !float.IsFinite(cell.Y) || x < 0 || y < 0 || x >= grid.Width || y >= grid.Height || blocked[y * grid.Width + x] != 0;
    }
    public override float Distance(in WPos p) => Contains(p) ? -1 : float.MaxValue;
    public override bool RowIntersectsShape(WPos rowStart, WDir dx, float width, float cushion = default) => Live;
}
