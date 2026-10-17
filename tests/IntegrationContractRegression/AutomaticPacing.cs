using BossMod.Integration;
using IntegrationExample;
using System.Text.Json;

static partial class Checks
{
    private static void AutomaticPacing()
    {
        long ownerTime = 1;
        using (var session = new IntegrationSession(() => ownerTime, 1000))
        {
            var owner = session.Register(Registration(session, ms: 50), _ => { }).OwnerToken!;
            var probe = session.CaptureOwnerLiveness(owner)!;
            Check(probe() && session.CaptureOwnerLiveness("unknown") == null, "native input captures only an already-live owner");
            var lifecycleGate = typeof(IntegrationSession).GetField("_gate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
            using var held = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            var holding = Task.Run(() => { lock (lifecycleGate) { held.Set(); release.Wait(); } });
            held.Wait();
            try { Check(Task.Run(probe).Wait(TimeSpan.FromSeconds(1)), "captured input liveness never waits for the lifecycle dictionary lock"); }
            finally { release.Set(); holding.GetAwaiter().GetResult(); }
            ownerTime += 50;
            Check(!Task.Run(probe).GetAwaiter().GetResult(), "captured liveness observes monotonic owner expiry from the input thread");
            var next = Owner(session, "next");
            var nextProbe = session.CaptureOwnerLiveness(next)!;
            session.Unregister(Lease(session, next));
            Check(!nextProbe(), "unregister revokes a previously captured owner probe");
            var last = Owner(session, "last"); var disposedProbe = session.CaptureOwnerLiveness(last)!;
            session.Dispose();
            Check(!disposedProbe(), "provider disposal revokes captured input liveness");
        }
        long now = 100;
        var gate = new IntegrationPacing(() => now);
        var direction = new Vector3(2, 0, 0);
        var assignment = new IntegrationInputObservation.Assignment("provider", 1, direction);
        var alive = true;
        IntegrationPacing.Ticket Ticket(string owner = "owner", float scale = 0.5f) => new("proof", owner, "provider", 1, 17,
            direction, scale, now + 100, () => alive);
        IntegrationPacing.Applied Apply(bool automatic = true, bool allowed = true, bool blocked = false, bool follow = false, bool misdirection = false,
            Vector2 user = default, Vector2? actual = null, Vector2? output = null, IntegrationInputObservation.Assignment? assigned = null)
            => gate.Apply(assigned ?? assignment, direction, user, actual ?? Vector2.UnitY, output ?? Vector2.UnitY,
                automatic, allowed, blocked, follow, misdirection);
        var captured = gate.CaptureEpoch("owner");
        gate.ClearOwner("owner");
        Check(!gate.Queue(Ticket(), captured), "owner clear invalidates a request still validating before its queue operation");
        captured = gate.CaptureEpoch("owner");
        gate.ClearOwner("other");
        Check(gate.Queue(Ticket(), captured), "clearing an absent unrelated owner cannot cancel another owner's validation epoch");
        gate.Clear();
        Check(!gate.Queue(Ticket(), captured), "a new native frame invalidates validation work captured in the previous frame");
        var ticket = Ticket();
        Check(gate.Queue(ticket), "a bounded provider proof can be pending");
        Check(Apply().Scale == 1, "pending proof cannot pace input before native assignment");
        Check(gate.Arm(ticket, assignment), "exact native assignment arms the pace");
        var paced = Apply();
        Check(paced.Scale == 0.5f && paced.Output == Vector2.UnitY * 0.5f && paced.ProofId == "proof",
            "pace changes only the magnitude of already-produced automatic input");
        Check(!gate.Queue(Ticket("other")) && !gate.ClearOwner("other") && Apply().Scale == 0.5f,
            "another owner cannot replace or clear an active proof");
        Check(gate.ClearOwner("owner") && gate.Current == null && !gate.Arm(ticket, assignment) && Apply().Scale == 1,
            "clearing a pending/armed proof cannot be undone by delayed native arming");
        foreach (var scale in new[] { float.NaN, float.PositiveInfinity, 0f, 0.34f, 1.01f })
            Check(!gate.Queue(Ticket(scale: scale)), "invalid, stopping and speed-increasing scales reject");
        foreach (var change in new[] { assignment with { FrameId = 2 }, assignment with { InstanceGeneration = "reloaded" }, assignment with { Direction = Vector3.UnitZ } })
        {
            ticket = Ticket(); Check(gate.Queue(ticket), "fresh proof queued for assignment mismatch");
            Check(!gate.Arm(ticket, change) && gate.Current == null, "mismatched frame/instance/direction cannot acquire input ownership");
        }
        foreach (var mode in new[] { "manual", "follow", "misdirection", "blocked", "not-automatic", "not-allowed" })
        {
            ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "mode fixture armed");
            var result = Apply(automatic: mode != "not-automatic", allowed: mode != "not-allowed", blocked: mode == "blocked",
                follow: mode == "follow", misdirection: mode == "misdirection", user: mode == "manual" ? Vector2.UnitX : default);
            Check(result.Scale == 1 && result.Output == Vector2.UnitY && gate.Current == null,
                "native/manual/follow/special-mode authority immediately revokes pacing: " + mode);
        }
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "output comparison fixture armed");
        Check(Apply(output: Vector2.UnitX).Scale == 1 && gate.Current == null, "foreign input revokes its old proof");
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "nonunit fixture armed");
        Check(Apply(actual: Vector2.UnitY * 0.5f, output: Vector2.UnitY * 0.5f).Scale == 1 && gate.Current == null, "nonunit input cannot be paced");
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "new assignment fixture armed");
        Check(Apply(assigned: assignment with { FrameId = 2 }).Scale == 1 && gate.Current == null, "a new assignment cannot inherit an old pace");
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "expiry fixture armed");
        now += 100;
        Check(Apply().Scale == 1 && gate.Current == null, "monotonic lease expiry restores native input without a new draw/frame");
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "owner-loss fixture armed");
        alive = false;
        Check(Apply().Scale == 1 && gate.Current == null, "owner loss cannot retain an input pace");
        alive = true;
        ticket = new("fault", "owner", "provider", 1, 17, direction, 0.5f, now + 100, () => throw new InvalidOperationException("fixture"));
        Check(gate.Queue(ticket) && !gate.Arm(ticket, assignment) && gate.Current == null, "provider-probe failures revoke input proof without throwing");
        ticket = Ticket(); Check(gate.Queue(ticket), "clear-before-arm fixture queued");
        gate.Clear();
        Check(!gate.Arm(ticket, assignment) && Apply().Scale == 1, "new frame/disposal cleanup cannot resurrect a pending proof");
        ticket = Ticket(); Check(gate.Queue(ticket) && gate.Arm(ticket, assignment), "concurrent-clear fixture armed");
        Task.Run(() => gate.ClearOwner("owner")).GetAwaiter().GetResult();
        Check(Apply().Scale == 1 && gate.Current == null, "consumer unload cleanup may cancel from another thread");
        using var context = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"provider\",\"frameId\":1,\"stage\":\"movement-proposed\"}");
        var requests = 0; var mayClear = false;
        var consumer = new BossModV1AutomaticPace(request =>
        {
            ++requests; using var payload = JsonDocument.Parse(request);
            Check(payload.RootElement.GetProperty("generation").GetInt64() == 17 && payload.RootElement.GetProperty("ownerToken").GetString() == "owner"
                && payload.RootElement.GetProperty("context").GetProperty("stage").GetString() == "movement-proposed",
                "compiled pacing consumer preserves exact source generation, owner and proposed-stage context");
            return "{\"accepted\":true,\"proofId\":\"pending-proof\"}";
        }, request =>
        {
            using var payload = JsonDocument.Parse(request);
            Check(payload.RootElement.GetProperty("schemaVersion").GetInt32() == 1 && payload.RootElement.GetProperty("instanceGeneration").GetString() == "provider",
                "consumer unload cleanup is scoped to its provider instance");
            return JsonSerializer.Serialize(new { accepted = mayClear });
        });
        Check(!consumer.TryRequest(context.RootElement, "owner", 17, 1.1f) && requests == 0, "consumer rejects speed increases before IPC");
        Check(consumer.TryRequest(context.RootElement, "owner", 17, 0.5f) && consumer.ProofId == "pending-proof",
            "consumer tracks acknowledgement as a pending proof rather than applied physical movement");
        Check(!consumer.Clear() && consumer.ProofId != null, "failed pacing cleanup remains explicit for retry/unregister");
        mayClear = true;
        Check(consumer.Clear() && consumer.ProofId == null && consumer.Clear(), "successful pacing cleanup is locally idempotent");
    }
}
