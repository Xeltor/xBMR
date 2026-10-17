namespace BossMod;

public sealed partial class AIHints
{
    internal const int MaxCastSources = 128;
    internal readonly record struct CastSource(ActorCastInfo Cast, ActionID Action, ulong TargetId)
    {
        internal CastSource(ActorCastInfo cast) : this(cast, cast.Action, cast.TargetID) { }
        internal bool Unchanged => Cast.Action == Action && Cast.TargetID == TargetId;
    }
    private readonly record struct ZoneCastSource(ShapeDistance Shape, DateTime Activation, ulong Source, CastSource Cast);
    private readonly List<ZoneCastSource> _zoneCastSources = [];
    internal bool CastSourcesTruncated { get; private set; }

    // Provenance comes from the producer, never from matching a cast's estimated finish time.
    public void AddForbiddenZoneFromCast(ShapeDistance shape, DateTime activation, ulong source, ActorCastInfo cast, int? arenaProjectionLayer = null)
        => AddForbiddenZoneWithCast(shape, activation, source, new(cast), arenaProjectionLayer);

    internal void AddForbiddenZoneWithCast(ShapeDistance shape, DateTime activation, ulong source, CastSource cast, int? arenaProjectionLayer)
    {
        var clipped = ClipToArenaProjectionLayer(shape, arenaProjectionLayer);
        ForbiddenZones.Add((clipped, activation, source));
        if (source == 0 || !cast.Unchanged) return;
        if (_zoneCastSources.Count < MaxCastSources) _zoneCastSources.Add(new(clipped, activation, source, cast));
        else CastSourcesTruncated = true;
    }

    internal CastSource? FindCastSource(ShapeDistance shape, DateTime activation, ulong source)
    {
        CastSource? result = null;
        foreach (var entry in _zoneCastSources)
        {
            if (!ReferenceEquals(shape, entry.Shape) || activation != entry.Activation || source != entry.Source) continue;
            if (!entry.Cast.Unchanged || result is { } previous &&
                (!ReferenceEquals(previous.Cast, entry.Cast.Cast) || previous.Action != entry.Cast.Action || previous.TargetId != entry.Cast.TargetId))
                return null;
            result = entry.Cast;
        }
        return result;
    }

    private void ClearCastSources()
    {
        _zoneCastSources.Clear();
        CastSourcesTruncated = false;
    }
}
