using System;
using System.Text.Json;

namespace IntegrationExample;

// Require cast-departure-hold:1 and renew the integration owner normally.
// Pending proof is never applied movement, cast permission or gameplay acceptance.
internal sealed class BossModV1DepartureHold(Func<string, string> request, Func<string, string> clear)
{
    private string? instance;
    private string? owner;
    internal string? ProofId { get; private set; }

    internal bool TryRequest(JsonElement context, string ownerToken, long navigationGeneration, int leaseMs = 100)
    {
        if (context.GetProperty("schemaVersion").GetInt32() != 1 || context.GetProperty("stage").GetString() != "movement-proposed"
            || string.IsNullOrEmpty(ownerToken) || navigationGeneration <= 0 || leaseMs is < 1 or > 100) return false;
        using var response = JsonDocument.Parse(request(JsonSerializer.Serialize(new
        {
            context, ownerToken, generation = navigationGeneration, leaseMs
        })));
        var result = response.RootElement;
        if (!result.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean()
            || result.GetProperty("kind").GetString() != "departure-hold" || result.GetProperty("scale").GetSingle() != 0
            || result.GetProperty("sourceNavigationGeneration").GetInt64() != navigationGeneration
            || result.GetProperty("castPermissionGranted").GetBoolean()) return false;
        instance = context.GetProperty("instanceGeneration").GetString(); owner = ownerToken;
        ProofId = result.GetProperty("proofId").GetString();
        return !string.IsNullOrEmpty(ProofId);
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
