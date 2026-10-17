using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace IntegrationExample;

// Require counterfactual-projection-geometry:1 and a fresh supported component ID.
// This reader returns native geometry observations, never safety approval.
internal sealed class BossModV1ProjectionGeometry(Func<string, string> query)
{
    internal sealed record Step(Vector2 From, Vector2 To, float? Facing, bool NativeDestinationUnsafe,
        string NativeDestinationContext, string? ActivationTicks, string? EndTicks, int? SourceIndex);
    internal sealed record Geometry(Vector2 Position, float Facing, Step[] Movements);

    internal Geometry[]? TryProject(JsonElement context, string componentId, Vector2[] positions, float[] facings)
    {
        if (string.IsNullOrEmpty(componentId) || positions.Length is < 1 or > 4 || positions.Length != facings.Length
            || positions.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y))
            || facings.Any(f => !float.IsFinite(f) || Math.Abs(f) > 2 * MathF.PI)) return null;
        try
        {
            if (context.GetProperty("schemaVersion").GetInt32() != 1) return null;
            using var response = JsonDocument.Parse(query(JsonSerializer.Serialize(new
            {
                context, componentId,
                poses = positions.Select((p, i) => new { position = new[] { p.X, p.Y }, facing = facings[i] }).ToArray()
            })));
            var root = response.RootElement;
            if (!root.GetProperty("available").GetBoolean() || !root.GetProperty("counterfactualSupported").GetBoolean()
                || root.GetProperty("status").GetString() != "unknown" || root.GetProperty("componentId").GetString() != componentId
                || root.GetProperty("independentHazardsEvaluated").GetBoolean() || root.GetProperty("recoveryReachabilityEvaluated").GetBoolean()) return null;
            var echoed = root.GetProperty("context");
            if (echoed.GetProperty("schemaVersion").GetInt32() != 1
                || echoed.GetProperty("instanceGeneration").GetString() != context.GetProperty("instanceGeneration").GetString()
                || echoed.GetProperty("frameId").GetInt64() != context.GetProperty("frameId").GetInt64()
                || echoed.GetProperty("stage").GetString() != context.GetProperty("stage").GetString()) return null;
            Vector2 Point(JsonElement value)
            {
                if (value.GetArrayLength() != 2) throw new FormatException("Invalid point");
                var p = new Vector2(value[0].GetSingle(), value[1].GetSingle());
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y)) throw new FormatException("Invalid point");
                return p;
            }
            var poses = root.GetProperty("poses");
            if (poses.GetArrayLength() != positions.Length) return null;
            var result = new Geometry[positions.Length];
            for (var i = 0; i < result.Length; ++i)
            {
                var pose = poses[i]; var position = Point(pose.GetProperty("position")); var facing = pose.GetProperty("facing").GetSingle();
                var steps = pose.GetProperty("movements");
                if (position != positions[i] || facing != facings[i] || steps.GetArrayLength() > 33) return null;
                result[i] = new(position, facing, steps.EnumerateArray().Select(s => new Step(
                    Point(s.GetProperty("from")), Point(s.GetProperty("to")), s.GetProperty("facing").ValueKind == JsonValueKind.Null ? null : s.GetProperty("facing").GetSingle(),
                    s.GetProperty("nativeDestinationUnsafe").GetBoolean(), s.GetProperty("nativeDestinationContext").GetString()!,
                    s.GetProperty("activationTicks").GetString(), s.GetProperty("endTicks").GetString(),
                    s.GetProperty("sourceIndex").ValueKind == JsonValueKind.Null ? null : s.GetProperty("sourceIndex").GetInt32())).ToArray());
            }
            return result;
        }
        catch (Exception) { return null; } // Missing provider, malformed/stale/unsupported observations all stop this dependent optimization.
    }
}
