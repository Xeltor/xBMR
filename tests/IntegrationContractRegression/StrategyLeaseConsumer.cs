using IntegrationExample;
using System.Text.Json;

static partial class Checks
{
    private static void StrategyLeaseConsumer()
    {
        using var context = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"instance\",\"frameId\":8,\"stage\":\"hints-ready\"}");
        var releases = 0;
        var calls = 0;
        var client = new BossModV1StrategyLease(
            _ => "{\"available\":true,\"leased\":false,\"legacyTransientActive\":false,\"presetId\":\"preset-instance\",\"revision\":5,\"value\":\"opaque-value\"}",
            json =>
            {
                ++calls;
                using var request = JsonDocument.Parse(json);
                var root = request.RootElement;
                Check(root.GetProperty("presetId").GetString() == "preset-instance" && root.GetProperty("expectedValue").GetString() == "opaque-value"
                    && root.GetProperty("expectedRevision").GetInt64() == 5, "sample passes exact preset identity/value/revision without rebuilding the comparison token");
                return "{\"accepted\":true,\"leaseId\":\"lease-a\"}";
            },
            json =>
            {
                using var request = JsonDocument.Parse(json);
                Check(request.RootElement.GetProperty("leaseId").GetString() == "lease-a"
                    && request.RootElement.GetProperty("instanceGeneration").GetString() == "instance", "sample renewal is bound to the loaded provider and opaque lease");
                return "{\"accepted\":true}";
            },
            _ => ++releases == 1 ? "{\"accepted\":false,\"reason\":\"cleanup-pending\"}" : "{\"accepted\":true}");
        Check(client.TryAcquire(context.RootElement, "owner", "preset", "module", "Range", "GreedAutomatic") && calls == 1,
            "compile-checked strategy sample acquires from a fresh observation");
        Check(!client.TryAcquire(context.RootElement, "owner", "preset", "module", "Range", "Any") && calls == 1,
            "sample does not silently replace an already-held resource lease");
        Check(client.Renew(), "sample renews its known lease independently of owner registration");
        Check(!client.Release() && client.LeaseId == "lease-a", "sample retains failed cleanup identity rather than claiming restoration");
        Check(client.Release() && client.LeaseId == null && client.Release(), "successful cleanup forgets the lease and becomes idempotent");
    }
}
