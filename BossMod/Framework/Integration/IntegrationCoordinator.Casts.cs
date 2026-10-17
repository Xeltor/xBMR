namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record CastControlQuery(IntegrationSession.Context Context, float[][]? Points = null);

    internal string GetCastControl(string json)
    {
        var request = IntegrationSession.Parse<CastControlQuery>(json);
        if (request == null || !Session.Matches(request.Context)) return Unavailable("invalid-cast-control-context");
        if (request.Points is { } points && (points.Length is < 1 or > IntegrationCastControl.MaxPoints
            || points.Any(p => p == null || p.Length != 2 || !float.IsFinite(p[0]) || !float.IsFinite(p[1]))))
            return Unavailable("invalid-cast-control-points");
        var player = World.Party.Player();
        if (player == null) return Unavailable("missing-player");
        var positions = request.Points?.Select(p => new WPos(p[0], p[1])).ToArray() ?? [player.Position];
        try { return IntegrationSession.Serialize(IntegrationCastControl.Capture(hints, World.CurrentTime, positions, request.Context)); }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Cast control observation failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("cast-control-unavailable");
        }
    }
}
