using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Framework-thread observations of an authored automatic transport. This retains
// only the first observed entrance time, not a predicted landing or hazard expiry.
// Leaving the trigger, handoff or a changed native schedule retires the observation.
internal sealed class NativeTeleportProgress
{
    private readonly Dictionary<(WPos Entrance, WPos Exit, float Radius, float UseTime), DateTime> _starts = [];
    private (ulong Actor, ushort Zone, ushort Content, int? Layer) _identity;
    private DateTime _observed;

    internal ImmutableArray<PlanningTeleport> Observe(NavigationIdentity identity, ImmutableArray<PlanningTeleport> teleports)
    {
        var key = (identity.ActorId, identity.ZoneId, identity.ContentId, identity.ProjectionLayer);
        if (_identity != key || identity.WorldTime < _observed) Reset();
        _identity = key;
        _observed = identity.WorldTime;
        var occupied = teleports.Where(t => t.Valid && identity.Origin.InCircle(t.Entrance, t.Radius)).ToArray();
        var retained = new HashSet<(WPos, WPos, float, float)>();
        var result = ImmutableArray.CreateBuilder<PlanningTeleport>();
        foreach (var transit in teleports)
        {
            if (!occupied.Contains(transit) || occupied.Any(t => t.Exit != transit.Exit))
            {
                result.Add(transit with { ElapsedUse = null });
                continue;
            }
            var route = (transit.Entrance, transit.Exit, transit.Radius, transit.UseTime);
            retained.Add(route);
            var proposedStart = identity.WorldTime.AddSeconds(transit.NotBefore);
            if (!_starts.TryGetValue(route, out var start)
                || transit.NotBefore > 0 && Math.Abs((proposedStart - start).TotalSeconds) > 0.01)
                _starts[route] = start = proposedStart;
            result.Add(transit with { ElapsedUse = Math.Clamp((float)(identity.WorldTime - start).TotalSeconds, 0, transit.UseTime) });
        }
        foreach (var route in _starts.Keys.Where(k => !retained.Contains(k)).ToArray()) _starts.Remove(route);
        return result.ToImmutable();
    }

    internal void Reset()
    {
        _starts.Clear();
        _identity = default;
        _observed = default;
    }
}
