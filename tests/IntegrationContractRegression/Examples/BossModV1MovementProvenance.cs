using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace IntegrationExample;

// Require controller-input-provenance:1 independently. Feed all native stage
// snapshots to Observe. Input may describe a remembered assignment from an older
// frame; this joins identity only, never safety, displacement or arrival.
internal sealed class BossModV1MovementProvenance
{
    private sealed record Assignment(long Frame, string Decision, string Owner);
    internal sealed record Observation(long InputGeneration, long AssignmentGeneration, long AssignmentFrame,
        string ControllerDecisionId, string ControllerOwner, Vector2 Output);
    private readonly Dictionary<long, Assignment> assignments = [];
    private readonly Queue<long> order = [];
    private string? instance;
    private long lastInput;

    internal Observation? Observe(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("available", out var available) || available.ValueKind != JsonValueKind.True
            || !snapshot.TryGetProperty("context", out var context) || !context.TryGetProperty("schemaVersion", out var schema)
            || !schema.TryGetInt32(out var version) || version != 1 || !context.TryGetProperty("instanceGeneration", out var provider)
            || provider.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(provider.GetString())) return null;
        if (instance != provider.GetString())
        {
            assignments.Clear(); order.Clear(); lastInput = 0; instance = provider.GetString();
        }
        if (context.TryGetProperty("stage", out var stage) && stage.GetString() == "movement-applied"
            && context.TryGetProperty("frameId", out var frame) && frame.TryGetInt64(out var currentFrame)
            && snapshot.TryGetProperty("overrideAssignment", out var applied) && applied.ValueKind == JsonValueKind.Object
            && applied.GetProperty("instanceGeneration").GetString() == instance && applied.GetProperty("frameId").GetInt64() == currentFrame
            && applied.GetProperty("generation").TryGetInt64(out var generation) && generation > 0
            && applied.TryGetProperty("controllerDecisionId", out var source) && source.ValueKind == JsonValueKind.String
            && applied.TryGetProperty("controllerOwner", out var owner) && owner.ValueKind == JsonValueKind.String
            && snapshot.TryGetProperty("controllerDecision", out var controller) && controller.ValueKind == JsonValueKind.Object
            && controller.GetProperty("id").GetString() == source.GetString() && controller.GetProperty("wroteMovement").GetBoolean()
            && controller.GetProperty("instanceGeneration").GetString() == instance && controller.GetProperty("frameId").GetInt64() == currentFrame
            && controller.GetProperty("owner").GetString() == owner.GetString() && controller.GetProperty("matchesEffectiveMovement").GetBoolean()
            && !assignments.ContainsKey(generation))
        {
            assignments[generation] = new(currentFrame, source.GetString()!, owner.GetString()!);
            order.Enqueue(generation);
            while (order.Count > 8) assignments.Remove(order.Dequeue());
        }
        if (!snapshot.TryGetProperty("lastInputRead", out var input) || input.ValueKind != JsonValueKind.Object
            || input.GetProperty("kind").GetString() != "walk-input-result" || !input.GetProperty("automatic").GetBoolean()
            || input.GetProperty("followPath").GetBoolean() || input.GetProperty("misdirection").GetBoolean()
            || !input.GetProperty("ageSeconds").TryGetDouble(out var age) || !double.IsFinite(age) || age is < 0 or >= 0.25
            || !input.GetProperty("generation").TryGetInt64(out var inputGeneration) || inputGeneration <= lastInput
            || !input.TryGetProperty("matchingAssignment", out var match) || match.ValueKind != JsonValueKind.Object
            || match.GetProperty("instanceGeneration").GetString() != instance
            || !match.GetProperty("generation").TryGetInt64(out var assignmentGeneration) || !assignments.TryGetValue(assignmentGeneration, out var known)
            || match.GetProperty("frameId").GetInt64() != known.Frame || match.GetProperty("controllerDecisionId").GetString() != known.Decision
            || match.GetProperty("controllerOwner").GetString() != known.Owner) return null;
        var output = input.GetProperty("outputDirection");
        var user = input.GetProperty("userDirection");
        if (output.GetArrayLength() != 2 || user.GetArrayLength() != 2 || user[0].GetSingle() != 0 || user[1].GetSingle() != 0
            || !output[0].TryGetSingle(out var x) || !output[1].TryGetSingle(out var z) || !float.IsFinite(x) || !float.IsFinite(z)) return null;
        lastInput = inputGeneration;
        return new(inputGeneration, assignmentGeneration, known.Frame, known.Decision, known.Owner, new(x, z));
    }
}
