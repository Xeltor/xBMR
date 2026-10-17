using System;
using System.Text.Json;

namespace IntegrationExample;

// Require automatic-input-pacing:1 independently. The integration owner must also be
// renewed normally. Requests are per-frame; acceptance is pending native validation/input.
internal sealed class BossModV1AutomaticPace(Func<string, string> request, Func<string, string> clear)
{
    private string? instance;
    private string? owner;
    internal string? ProofId { get; private set; }

    internal bool TryRequest(JsonElement context, string ownerToken, long navigationGeneration, float scale, int leaseMs = 100)
    {
        if (context.GetProperty("schemaVersion").GetInt32() != 1 || context.GetProperty("stage").GetString() != "movement-proposed"
            || !float.IsFinite(scale) || scale is < 0.35f or > 1 || leaseMs is < 1 or > 100) return false;
        using var response = JsonDocument.Parse(request(JsonSerializer.Serialize(new
        {
            context, ownerToken, generation = navigationGeneration, scale, leaseMs
        })));
        if (!response.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean()) return false;
        instance = context.GetProperty("instanceGeneration").GetString(); owner = ownerToken;
        ProofId = response.RootElement.GetProperty("proofId").GetString();
        return ProofId != null;
    }

    internal bool Clear()
    {
        if (owner == null) return true;
        using var response = JsonDocument.Parse(clear(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, instanceGeneration = instance, ownerToken = owner
        })));
        if (!response.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean()) return false;
        owner = instance = ProofId = null;
        return true;
    }
}
