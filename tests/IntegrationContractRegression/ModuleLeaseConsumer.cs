using IntegrationExample;
using System.Text.Json;

static partial class Checks
{
    private static void ModuleLeaseConsumer()
    {
        using var context = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"provider\",\"frameId\":1,\"stage\":\"hints-ready\"}");
        var savedEnabled = true;
        var canDisable = true;
        var releaseSucceeds = false;
        var applyCount = 0;
        var consumer = new BossModV1ModuleLease(_ => JsonSerializer.Serialize(new
        {
            available = true, leased = false, meetsMinimumMaturity = true, canTemporarilyDisable = canDisable,
            savedEnabled, enabled = savedEnabled, revision = 17
        }), json =>
        {
            ++applyCount;
            using var request = JsonDocument.Parse(json);
            var root = request.RootElement;
            Check(root.GetProperty("expectedEnabled").GetBoolean() == savedEnabled && root.GetProperty("expectedRevision").GetInt64() == 17
                && root.GetProperty("identity").GetString() == "41E2:fixture" && root.GetProperty("context").GetProperty("frameId").GetInt64() == 1,
                "module example echoes exact native revision/value/identity and frame rather than restoring a guessed list");
            Check(root.GetProperty("enabled").GetBoolean() != savedEnabled
                && (savedEnabled ? root.GetProperty("reason").ValueKind == JsonValueKind.Null
                    : root.GetProperty("reason").GetString() == "user-requested-temporary-enable"),
                "module example submits explicit boolean intent and the required user-enable reason");
            return "{\"accepted\":true,\"leaseId\":\"module-lease\"}";
        }, json =>
        {
            using var request = JsonDocument.Parse(json);
            Check(request.RootElement.GetProperty("leaseId").GetString() == "module-lease"
                && request.RootElement.GetProperty("ownerToken").GetString() == "owner", "module example renews the exact independent resource owner");
            return "{\"accepted\":true}";
        }, _ => JsonSerializer.Serialize(new { accepted = releaseSucceeds }));
        Check(consumer.TryAcquire(context.RootElement, "owner", "41E2:fixture", false) && consumer.LeaseId == "module-lease",
            "compile-checked module consumer uses discovered resource state");
        Check(!consumer.TryAcquire(context.RootElement, "owner", "41E2:fixture", false) && applyCount == 1, "example cannot silently acquire a second resource");
        Check(consumer.Renew() && !consumer.Release() && consumer.LeaseId != null, "failed release is not falsely reported as restored");
        releaseSucceeds = true;
        Check(consumer.Release() && consumer.LeaseId == null && consumer.Release(), "successful release is idempotent locally");
        savedEnabled = false;
        Check(!consumer.TryAcquire(context.RootElement, "owner", "41E2:fixture", true) && applyCount == 1, "example preserves configured opt-outs without actual user enable intent");
        Check(consumer.TryAcquire(context.RootElement, "owner", "41E2:fixture", true, explicitUserEnable: true), "explicit temporary enable intent can be submitted for native validation");
        consumer.Release();
        canDisable = false;
        Check(!consumer.TryAcquire(context.RootElement, "owner", "41E2:fixture", false), "example respects native inability to disable an active/pulling module");
    }
}
