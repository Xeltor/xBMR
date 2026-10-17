namespace BossMod.Integration;

// Additive per-frame ranges: never edit or remove an authored native direction.
internal sealed class IntegrationHeadingConstraints
{
    private sealed record Entry(string Owner, Angle Center, Angle HalfWidth, Func<bool> Live);
    private readonly Dictionary<string, Entry> _entries = [];
    internal bool Submit(string owner, float heading, Func<bool> live)
    {
        if (!live() || !float.IsFinite(heading) || (!_entries.ContainsKey(owner) && _entries.Count >= IntegrationSession.MaxOwners)) return false;
        foreach (var entry in _entries.Values)
            if (entry.Owner != owner && entry.Live() && Math.Abs((new Angle(heading + MathF.PI).Normalized() - entry.Center).Normalized().Rad) >= 8 * MathF.PI / 180)
                return false;
        _entries[owner] = new(owner, new Angle(heading + MathF.PI).Normalized(), (MathF.PI - 4 * MathF.PI / 180).Radians(), live);
        return true;
    }
    internal (Angle center, Angle halfWidth, DateTime activation)[] Current()
        => _entries.Values.Where(e => e.Live()).Select(e => (e.Center, e.HalfWidth, DateTime.MinValue)).ToArray();
}
