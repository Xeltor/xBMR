using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace IntegrationExample;

// Compile-checked BCL transport. Each status describes a limited observation,
// never a completed route, mechanic, recovery or action safety approval.
public sealed class BossModV1LandingConstraints(Func<string, string> evaluate)
{
    public sealed record Observation(Vector2 Position, Vector2 Landing, string CurrentPublishedConstraintsStatus,
        string Reason, bool ArenaFootprintClear, bool OwnLandingEnvelopesClear);

    // Call synchronously inside a native callback after family/component discovery.
    // Register before the next native hint build so producer capture is available.
    public Observation[]? Inspect(JsonElement context, string componentId, Vector2[] positions, float[] facings)
    {
        if (positions == null || facings == null || positions.Length is < 1 or > 4 || positions.Length != facings.Length || string.IsNullOrEmpty(componentId)) return null;
        var poses = new object[positions.Length];
        for (var i = 0; i < poses.Length; ++i)
        {
            if (!float.IsFinite(positions[i].X) || !float.IsFinite(positions[i].Y) || !float.IsFinite(facings[i])) return null;
            poses[i] = new { position = new[] { positions[i].X, positions[i].Y }, facing = facings[i] };
        }
        try
        {
            using var response = JsonDocument.Parse(evaluate(JsonSerializer.Serialize(new { context, componentId, poses })));
            var root = response.RootElement;
            if (!root.TryGetProperty("available", out var available) || !available.GetBoolean() || root.GetProperty("status").GetString() != "unknown"
                || root.GetProperty("componentId").GetString() != componentId || !root.GetProperty("independentPublishedConstraintsEvaluated").GetBoolean()
                || root.GetProperty("physicalObstaclesEvaluated").GetBoolean() || root.GetProperty("recoveryReachabilityEvaluated").GetBoolean()
                || root.GetProperty("context").GetRawText() != context.GetRawText()) return null;
            var observations = root.GetProperty("observations");
            if (observations.GetArrayLength() != poses.Length) return null;
            var result = new Observation[poses.Length];
            for (var i = 0; i < result.Length; ++i)
            {
                var observation = observations[i];
                var geometry = observation.GetProperty("geometry");
                var position = geometry.GetProperty("position");
                var moves = geometry.GetProperty("movements");
                var status = observation.GetProperty("currentPublishedConstraintsStatus").GetString();
                if (position.GetArrayLength() != 2 || position[0].GetSingle() != positions[i].X || position[1].GetSingle() != positions[i].Y
                    || geometry.GetProperty("facing").GetSingle() != facings[i] || moves.GetArrayLength() != 1
                    || moves[0].GetProperty("sourceIndex").GetInt32() != 0 || status is not ("clear" or "blocked" or "unknown")) return null;
                var landing = moves[0].GetProperty("to");
                if (landing.GetArrayLength() != 2) return null;
                var point = new Vector2(landing[0].GetSingle(), landing[1].GetSingle());
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return null;
                result[i] = new(positions[i], point, status, observation.GetProperty("reason").GetString() ?? "unknown",
                    observation.GetProperty("arenaFootprintClear").GetBoolean(), observation.GetProperty("ownLandingEnvelopesClear").GetBoolean());
            }
            return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { return null; }
    }
}
