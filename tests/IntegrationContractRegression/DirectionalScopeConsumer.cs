using IntegrationExample;
using System.Text.Json;

static partial class Checks
{
    private static void DirectionalScopeConsumer()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"instance\",\"frameId\":5,\"stage\":\"hints-ready\"}");
        var called = 0;
        var scoped = false;
        string GetSettings(string request)
        {
            Check(request == document.RootElement.GetRawText(), "scope consumer passes exact callback context to discovery");
            return "{\"available\":true,\"settings\":[{\"key\":\"audited-key\",\"synchronousOnly\":true,\"leased\":false,\"revision\":7,\"value\":\"true\"}]}";
        }
        string Scope(string request, Func<bool> callback)
        {
            using var json = JsonDocument.Parse(request);
            Check(json.RootElement.GetProperty("expectedRevision").GetInt64() == 7
                && json.RootElement.GetProperty("expectedValue").GetString() == "true", "scope consumer uses the observed compare-and-restore revision/value");
            scoped = true;
            bool requested;
            try { requested = callback(); }
            finally { scoped = false; }
            return JsonSerializer.Serialize(new { accepted = true, releaseComplete = true, actionRequested = requested });
        }
        Check(BossModV1DirectionalScope.TryRequest(document.RootElement, "owner", 1, "audited-key", GetSettings, Scope,
            () => { ++called; Check(scoped, "action request executes within the synchronous scope"); return true; }) && called == 1 && !scoped,
            "compile-checked example observes scoped request and restoration");
        Check(!BossModV1DirectionalScope.TryRequest(document.RootElement, "owner", 1, "audited-key", _ => "{\"available\":false}", Scope, () => { ++called; return true; })
            && called == 1, "unavailable setting discovery never requests an action");
        Check(!BossModV1DirectionalScope.TryRequest(document.RootElement, "owner", 1, "audited-key", GetSettings,
            (_, _) => "{\"accepted\":false,\"reason\":\"changed-setting\"}", () => { ++called; return true; }) && called == 1,
            "scope rejection does not turn into action success");
    }
}
