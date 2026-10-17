using System;
using System.Text.Json;

namespace IntegrationExample;

// One module resource, acquired at hints-ready with a separately renewed integration owner.
// Native rejection is authoritative. Handle provider disappearance/reload at the caller boundary.
internal sealed class BossModV1ModuleLease(Func<string, string> read, Func<string, string> apply,
    Func<string, string> renew, Func<string, string> release)
{
    private string? generation;
    private string? owner;
    internal string? LeaseId { get; private set; }

    internal bool TryAcquire(JsonElement context, string ownerToken, string identity, bool enabled, bool explicitUserEnable = false)
    {
        if (LeaseId != null || context.GetProperty("schemaVersion").GetInt32() != 1
            || context.GetProperty("stage").GetString() != "hints-ready") return false;
        using var response = JsonDocument.Parse(read(JsonSerializer.Serialize(new { context, identity })));
        var state = response.RootElement;
        if (!state.GetProperty("available").GetBoolean() || state.GetProperty("leased").GetBoolean()
            || !state.GetProperty("meetsMinimumMaturity").GetBoolean()
            || !enabled && !state.GetProperty("canTemporarilyDisable").GetBoolean()) return false;
        var savedEnabled = state.GetProperty("savedEnabled").GetBoolean();
        if (enabled && !savedEnabled && !explicitUserEnable) return false;
        using var result = JsonDocument.Parse(apply(JsonSerializer.Serialize(new
        {
            context, ownerToken, identity, expectedRevision = state.GetProperty("revision").GetInt64(),
            expectedEnabled = state.GetProperty("enabled").GetBoolean(), enabled, leaseMs = 1000,
            reason = enabled && !savedEnabled ? "user-requested-temporary-enable" : null
        })));
        if (!result.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean()) return false;
        var id = result.RootElement.GetProperty("leaseId").GetString();
        if (string.IsNullOrEmpty(id)) return false;
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

    // Removing a layer restores configuration eligibility, not an instruction to unload a live
    // native phase. Do not edit saved values or force a module unload from the consumer.
    internal bool Release()
    {
        if (LeaseId == null) return true;
        using var response = JsonDocument.Parse(release(Request()));
        if (!response.RootElement.TryGetProperty("accepted", out var accepted) || !accepted.GetBoolean()) return false;
        LeaseId = null; generation = null; owner = null;
        return true;
    }
}
