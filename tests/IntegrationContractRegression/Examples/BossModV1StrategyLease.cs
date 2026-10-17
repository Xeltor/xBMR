using System;
using System.Text.Json;

namespace IntegrationExample;

// One resource lease. Supply subscriber InvokeFunc delegates and a separately renewed owner.
// Call TryAcquire in hints-ready. Handle provider disappearance/reload at the caller boundary.
internal sealed class BossModV1StrategyLease(
    Func<string, string> read, Func<string, string> apply,
    Func<string, string> renew, Func<string, string> release)
{
    private string? generation;
    private string? owner;
    internal string? LeaseId { get; private set; }

    internal bool TryAcquire(JsonElement context, string ownerToken, string presetName, string module, string track, string valueText)
    {
        if (LeaseId != null || context.GetProperty("schemaVersion").GetInt32() != 1
            || context.GetProperty("stage").GetString() != "hints-ready")
            return false;
        using var snapshot = JsonDocument.Parse(read(JsonSerializer.Serialize(new { context, presetName, module, track })));
        var state = snapshot.RootElement;
        if (!state.GetProperty("available").GetBoolean() || state.GetProperty("leased").GetBoolean()
            || state.GetProperty("legacyTransientActive").GetBoolean())
            return false;
        using var response = JsonDocument.Parse(apply(JsonSerializer.Serialize(new
        {
            context, ownerToken, presetName, presetId = state.GetProperty("presetId").GetString(), module, track,
            expectedRevision = state.GetProperty("revision").GetInt64(), expectedValue = state.GetProperty("value").GetString(),
            value = new { valueText, target = "Automatic", targetParam = 0 }, leaseMs = 1000
        })));
        if (!response.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean())
            return false;
        var id = response.RootElement.GetProperty("leaseId").GetString();
        if (string.IsNullOrEmpty(id))
            return false;
        LeaseId = id;
        generation = context.GetProperty("instanceGeneration").GetString();
        owner = ownerToken;
        return true;
    }

    private string Request(int leaseMs = 0) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, instanceGeneration = generation, ownerToken = owner, leaseId = LeaseId, leaseMs
    });

    internal bool Renew()
    {
        if (LeaseId == null) return false;
        using var response = JsonDocument.Parse(renew(Request(1000)));
        return response.RootElement.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean();
    }

    // Call in finally/unload before unregistering. A rejected/failed release must not be reported
    // as restoration. Provider expiry is a fallback; never restore saved values from the consumer.
    internal bool Release()
    {
        if (LeaseId == null) return true;
        using var response = JsonDocument.Parse(release(Request()));
        if (!response.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean())
            return false;
        LeaseId = null;
        generation = null;
        owner = null;
        return true;
    }
}
