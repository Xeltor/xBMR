using BossMod.Components;

namespace BossMod;

public sealed partial class AIHints
{
    internal bool ProjectionSourcesRequested;
    internal Integration.IntegrationSession.Context? ProjectionSourcesContext;
    private sealed record ProjectionSource(BossComponent Component, ShapeDistance Shape, DateTime Activation, ulong Actor,
        ShapeDistance[] Graph, Actor Player, ulong PlayerId);
    private readonly List<ProjectionSource> _projectionSources = [];
    private readonly List<(ShapeDistance shapeDistance, DateTime activation, ulong Source)> _projectionPrefix = [];
    private bool _projectionSourcesInvalid;
    private bool _collectingProjectionSource;

    private void ClearProjectionSources()
    {
        _projectionSources.Clear(); _projectionPrefix.Clear();
        _projectionSourcesInvalid = false; _collectingProjectionSource = false;
        ProjectionSourcesContext = null;
    }

    // Wrap the native producer, including components that append directly to the public list.
    // Capture only while a supported integration owner requests it; never change native hints.
    internal void CollectComponentHints(BossComponent component, int slot, Actor actor, PartyRolesConfig.Assignment assignment)
    {
        if (!ProjectionSourcesRequested || _projectionSourcesInvalid)
        { component.AddAIHints(slot, actor, assignment, this); return; }
        var candidate = component is GenericKnockback { SupportsCounterfactualProjection: true } knockback
            && knockback.ActiveKnockbacks(slot, actor).Length == 1;
        if (!candidate && _projectionSources.Count == 0)
        { component.AddAIHints(slot, actor, assignment, this); return; }
        if (_collectingProjectionSource || ForbiddenZones.Count > 128)
        {
            _projectionSourcesInvalid = true;
            component.AddAIHints(slot, actor, assignment, this); return;
        }
        _collectingProjectionSource = true;
        _projectionPrefix.Clear(); _projectionPrefix.AddRange(ForbiddenZones);
        try { component.AddAIHints(slot, actor, assignment, this); }
        finally
        {
            _collectingProjectionSource = false;
            if (ForbiddenZones.Count > 128 || ForbiddenZones.Count < _projectionPrefix.Count)
                _projectionSourcesInvalid = true;
            else
            {
                for (var i = 0; i < _projectionPrefix.Count; ++i)
                    if (!SameZone(_projectionPrefix[i], ForbiddenZones[i])) _projectionSourcesInvalid = true;
                if (candidate && !_projectionSourcesInvalid)
                    foreach (var zone in ForbiddenZones.Skip(_projectionPrefix.Count))
                    {
                        var graph = Integration.IntegrationLandingConstraints.Graph(zone.shapeDistance);
                        if (graph == null || _projectionSources.Count >= 128) { _projectionSourcesInvalid = true; break; }
                        _projectionSources.Add(new(component, zone.shapeDistance, zone.activation, zone.Source, graph, actor, actor.InstanceID));
                    }
            }
            _projectionPrefix.Clear();
        }
    }

    private static bool SameZone((ShapeDistance shapeDistance, DateTime activation, ulong Source) a,
        (ShapeDistance shapeDistance, DateTime activation, ulong Source) b)
        => ReferenceEquals(a.shapeDistance, b.shapeDistance) && a.activation == b.activation && a.Source == b.Source;

    internal bool ProjectionSourcesValid => ProjectionSourcesRequested && !_projectionSourcesInvalid && !_collectingProjectionSource;
    internal bool ProjectionSourcesMatch(BossModule module, Actor player)
        => _projectionSources.All(p => ReferenceEquals(p.Player, player) && p.PlayerId == player.InstanceID
            && ReferenceEquals(p.Component.Module, module));
    internal bool ProjectionSourceWasRecorded(ShapeDistance shape) => _projectionSources.Any(p => ReferenceEquals(p.Shape, shape));
    internal BossComponent? ProjectionProducer(ShapeDistance shape, DateTime activation, ulong source)
    {
        if (!ProjectionSourcesValid) return null;
        var key = (shape, activation, source);
        if (ForbiddenZones.Count(z => SameZone(z, key)) != 1) return null;
        var records = _projectionSources.Where(p => ReferenceEquals(p.Shape, shape) && p.Activation == activation && p.Actor == source).Take(2).ToArray();
        if (records.Length != 1) return null;
        var graph = Integration.IntegrationLandingConstraints.Graph(shape);
        return graph != null && graph.Length == records[0].Graph.Length
            && graph.Zip(records[0].Graph).All(p => ReferenceEquals(p.First, p.Second)) ? records[0].Component : null;
    }
}
