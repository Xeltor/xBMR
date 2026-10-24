using System.Net;
using System.Text.Json;
using BossMod.CombatAssistance.Sharing;
using System.Collections.Immutable;

internal static class ProcessingStatusChecks
{
    public static async Task Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "processing-status-" + Guid.NewGuid());
        string config = Path.Combine(root, "BossModReborn");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(Path.Combine(root, "BossModReborn", "replays"));
        string journal = Path.Combine(config, "fight-upload-receipts.json");
        string digest = new('a', 64);
        try
        {
            foreach (string state in new[] { "queued", "processing", "failed", "indexed", "wrong-digest", "outage" })
            {
                // Simulate a restart after durable custody, with no local file.
                File.WriteAllText(journal, JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["fixture"] = new { LastPart = -1, Complete = false, Failures = 0, RetryAfter = DateTime.MinValue,
                        DeliveryId = digest, DeliveryState = "queued", StatusAfter = DateTime.MinValue }
                }));
                var handler = new Handler(state, digest);
                await using var uploader = new FightEvidenceUploader(config, new("parser", ImmutableDictionary<uint, bool>.Empty), handler, TimeSpan.Zero, TimeSpan.FromMilliseconds(10),
                    nativeDecode: (_, _, _, _, _, _) => throw new Exception("Accepted deliveries must not be decoded or uploaded again."));
                uploader.Observe(true, true, new(true, null));
                await Task.Delay(80);
                if (handler.Requests != 0) throw new Exception("Processing checks must pause during combat.");
                uploader.Observe(true, false, new(true, null));
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                while (true)
                {
                    using var saved = JsonDocument.Parse(File.ReadAllText(journal));
                    var receipt = saved.RootElement.GetProperty("fixture");
                    if (receipt.TryGetProperty("StatusAfter", out var next) && next.GetDateTime() > DateTime.UtcNow)
                    {
                        bool indexed = state == "indexed";
                        if (receipt.GetProperty("Complete").GetBoolean() != indexed)
                            throw new Exception("Only matching indexed status can complete a receipt.");
                        string expected = state is "wrong-digest" or "outage" ? "queued" : state;
                        if (receipt.GetProperty("DeliveryState").GetString() != expected)
                            throw new Exception("Failed processing and uncertain lookups must preserve custody state.");
                        break;
                    }
                    if (deadline.IsCancellationRequested) throw new Exception($"Processing fixture timed out: {state}, requests={handler.Requests}, status={uploader.Status}, journal={File.ReadAllText(journal)}");
                    await Task.Delay(10);
                }
                await Task.Delay(50, deadline.Token);
                if (handler.Requests != 1) throw new Exception("Status polling must back off and never retransmit custody.");
                if (state == "failed" && !uploader.Status.Contains("processing failed"))
                    throw new Exception("Later archive rejection must be visible.");
            }
            Checks.Check(true, "Archive custody survives restart, pauses for combat, verifies digest and avoids duplicate uploads.");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Handler(string state, string digest) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref this.Requests);
            if (request.Method != HttpMethod.Get || request.RequestUri?.AbsoluteUri != "https://bmr-fights.xel-serv.com/v2/recordings"
                || request.Headers.GetValues("X-Delivery-SHA256").Single() != digest || request.Headers.Authorization != null)
                throw new Exception("Only a bounded custody lookup is allowed.");
            return Task.FromResult(new HttpResponseMessage(state == "outage" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { ok = true, state = state == "wrong-digest" ? "indexed" : state,
                    delivery_id = state == "wrong-digest" ? new string('b', 64) : digest }))
            });
        }
    }
}
