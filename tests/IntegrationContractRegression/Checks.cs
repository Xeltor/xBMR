using BossMod;
using BossMod.Integration;
using BossMod.Pathfinding;

static partial class Checks
{
    private static int _checks;
    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
        ++_checks;
    }
    private static IntegrationSession.Registration Registration(IntegrationSession s, string name = "test", int ms = 2000)
        => new(1, s.InstanceGeneration, name, ms);
    private static IntegrationSession.Lease Lease(IntegrationSession s, string token, int ms = 2000)
        => new(1, s.InstanceGeneration, token, ms);
    private static string Owner(IntegrationSession s, string name = "test", Action<string>? callback = null)
    {
        var r = s.Register(Registration(s, name), callback ?? (_ => { }));
        Check(r.Accepted && r.OwnerToken != null, "owner registered");
        return r.OwnerToken!;
    }
    public static void Run()
    {
        RegistrationAndStages();
        ExpiryAndIsolation();
        ScoringFailures();
        Composition();
        ActionEffects();
        ArenaGeometry();
        ConsumerTransport();
        NavigationSnapshots();
        NativeNavigationCapture();
        CurrentWalkRoutes();
        OwnedConfiguration();
        LeaseLifetimes();
        MechanicPublications();
        InputObservations();
        FollowCancellation();
        DirectionalScopeConsumer();
        NativeSearch();
        OwnedStrategies();
        ModuleEnablementCache();
        StrategyLeaseConsumer();
        DamagePublications();
        ModuleEnablementOwnership();
        ModuleLeaseConsumer();
        CastControlPublications();
        AutomaticPacing();
        NativeAutomaticPacing();
        NativeControllerProvenance();
        MovementProvenanceConsumer();
        CounterfactualProjections();
        NativeProjectionPoses();
        NativeHintPublications();
        Console.WriteLine($"{_checks} integration contract assertions passed");
    }

    private static void RegistrationAndStages()
    {
        long now = 100;
        using var s = new IntegrationSession(() => now, 1000);
        Check(!s.HasActiveOwners, "a new provider creates no raster publication demand");
        Check(!s.Register(Registration(s) with { SchemaVersion = 99 }, _ => { }).Accepted, "unknown schema rejected");
        Check(!s.Register(Registration(s) with { InstanceGeneration = "old" }, _ => { }).Accepted, "old instance rejected");
        Check(!s.Register(Registration(s, ms: 2001), _ => { }).Accepted, "unbounded lease rejected");
        Check(IntegrationSession.Parse<IntegrationSession.Context>("{broken") == null, "malformed input rejected");
        Check(IntegrationSession.Parse<IntegrationSession.Context>(new string('x', 8193)) == null, "oversized input rejected");
        Check(IntegrationSession.Parse<IntegrationSession.Context>("{\"schemaVersion\":1,\"unknown\":1}") == null, "unknown property rejected");
        var stages = new List<string>();
        var token = Owner(s, callback: json =>
        {
            stages.Add(s.Stage);
            Check(!s.Publish(s.Stage, _ => ""), "callback cannot redispatch a stage");
        });
        Check(s.HasActiveOwners, "registered consumer creates bounded raster publication demand");
        Check(!s.Publish("hints-ready", _ => ""), "no publication before frame");
        s.BeginFrame("zone:player:preset", true);
        Check(s.Matches(s.CurrentContext), "current stage matches");
        var hints = s.CurrentContext;
        Check(s.Publish("hints-ready", IntegrationSession.Serialize), "hints publication");
        Check(!s.Publish("hints-ready", _ => ""), "no duplicate publication");
        Check(!s.Publish("movement-applied", _ => ""), "no skipped stage");
        Check(s.Submit(new(s.CurrentContext, token, "combat"), _ => 2).Accepted, "goal accepted before navigation");
        Check(s.Publish("movement-proposed", IntegrationSession.Serialize), "proposed publication");
        Check(!s.Matches(hints), "old stage invalidated");
        Check(!s.Submit(new(s.CurrentContext, token, "combat"), _ => 2).Accepted, "late goal rejected");
        Check(s.Publish("movement-applied", IntegrationSession.Serialize), "applied publication");
        Check(stages.SequenceEqual(new[] { "hints-ready", "movement-proposed", "movement-applied" }), "exactly three ordered stages");
        Check(s.LatestPublication == IntegrationSession.Serialize(s.CurrentContext), "latest is the exact copied stage publication");
        Check(Task.Run(() => s.Renew(Lease(s, token)).Accepted).GetAwaiter().GetResult(), "consumer lifecycle can renew on another thread");
        Check(!Task.Run(() => s.Matches(s.CurrentContext)).GetAwaiter().GetResult(), "off-framework query unavailable");
        s.BeginFrame("zone:player:preset", false);
        Check(!s.Submit(new(s.CurrentContext, token, "combat"), _ => 2).Accepted, "manual input suppresses contribution");
        now += 251;
        Check(!s.Matches(s.CurrentContext), "stalled frame unavailable");
        Check(!s.Fresh, "stalled latest publication unavailable");
        s.BeginFrame("zone:player:preset", true);
        Check(s.LatestPublication == null, "beginning a new frame cannot refresh an older cached publication");
        s.Publish("hints-ready", _ => throw new InvalidOperationException("snapshot failure"));
        Check(!s.CanContribute && s.LatestPublication!.Contains("snapshot-unavailable"), "snapshot failure publishes unavailable and leaves native stages running");
        Check(s.Publish("movement-proposed", IntegrationSession.Serialize), "native stage sequence continues after snapshot failure");

        using var initializedElsewhere = Task.Run(() => new IntegrationSession()).GetAwaiter().GetResult();
        var otherToken = Owner(initializedElsewhere);
        initializedElsewhere.BeginFrame("draw-thread", true);
        Check(initializedElsewhere.Submit(new(initializedElsewhere.CurrentContext, otherToken, "combat"), _ => 1).Accepted,
            "initialization thread cannot prevent contributions on BMR's real frame thread");
        long demandClock = 100;
        using var demand = new IntegrationSession(() => demandClock, 1000);
        var demandToken = Owner(demand);
        Check(demand.HasActiveOwners, "a live consumer requests detailed navigation publication");
        demandClock += 2001;
        Check(!demand.HasActiveOwners, "expired consumers stop raster allocation demand even without a native frame or dictionary pruning");
        demand.Dispose();
        Check(!demand.HasActiveOwners, "provider disposal clears diagnostic allocation demand");
    }

    private static void ExpiryAndIsolation()
    {
        long now = 100;
        using var s = new IntegrationSession(() => now, 1000);
        var a = Owner(s, "a"); var b = Owner(s, "b");
        s.BeginFrame("zone", true);
        s.Submit(new(s.CurrentContext, a, "combat"), _ => 3);
        s.Submit(new(s.CurrentContext, b, "combat"), _ => 7);
        var captured = s.CaptureGoals("combat");
        Check(captured.Sum(g => g(default)) == 10, "independent owners compose");
        s.Clear(Lease(s, a));
        Check(captured.Sum(g => g(default)) == 7, "clear revokes captured owner goals only");
        s.Unregister(Lease(s, b));
        Check(captured.Sum(g => g(default)) == 0, "unregister revokes captured delegates");
        s.Submit(new(s.CurrentContext, a, "combat"), _ => 4);
        captured = s.CaptureGoals("combat");
        now += 250;
        Check(captured.Sum(g => g(default)) == 0, "worker cannot retain expired frame scoring");
        now += 2000;
        Check(!s.Renew(Lease(s, a)).Accepted, "expired owner cannot be revived");
        var fresh = Owner(s, "a");
        s.BeginFrame("zone", true);
        s.Submit(new(s.CurrentContext, fresh, "combat"), _ => 4);
        captured = s.CaptureGoals("combat");
        s.BeginFrame("new-zone", true);
        Check(captured.Sum(g => g(default)) == 0, "territory/preset/death scope change revokes old worker scoring");
        s.Submit(new(s.CurrentContext, fresh, "combat"), _ => 4);
        captured = s.CaptureGoals("combat");
        s.BeginFrame("new-zone", false);
        Check(captured.Sum(g => g(default)) == 0, "manual handoff invalidates worker scoring");
        s.BeginFrame("new-zone", true);
        for (var i = 0; i < 4; ++i)
            Check(s.Submit(new(s.CurrentContext, fresh, "combat"), _ => 1).Accepted, "bounded goal accepted");
        Check(!s.Submit(new(s.CurrentContext, fresh, "combat"), _ => 1).Accepted, "excess goal rejected");
        captured = s.CaptureGoals("combat");
        s.Dispose();
        Check(captured.Sum(g => g(default)) == 0, "reload/unload invalidates retained worker delegates");
    }

    private static void ScoringFailures()
    {
        foreach (var failure in new Func<Vector2, float>[] { _ => throw new InvalidOperationException(), _ => float.NaN, _ => 17, _ => -1 })
        {
            long now = 100;
            using var s = new IntegrationSession(() => now, 1000);
            var token = Owner(s);
            s.BeginFrame("scope", true);
            s.Submit(new(s.CurrentContext, token, "combat"), failure);
            var goal = s.CaptureGoals("combat").Single();
            Parallel.For(0, 1000, _ =>
            {
                if (goal(default) != 0) throw new InvalidOperationException("invalid score escaped");
            });
            Check(!s.Renew(Lease(s, token)).Accepted, "scoring failure revokes owner and leaves native processing available");
        }
        using var callbacks = new IntegrationSession();
        Owner(callbacks, "broken", _ => throw new InvalidOperationException());
        var called = false;
        Owner(callbacks, "healthy", _ => called = true);
        callbacks.BeginFrame("scope", true);
        callbacks.Publish("hints-ready", _ => "{}");
        Check(called, "one callback failure does not stop other consumers/native stages");
    }

    private static void Composition()
    {
        using var s = new IntegrationSession();
        var token = Owner(s);
        s.BeginFrame("scope", true);
        Func<WPos, float> encounter = p => p.X < 0 ? 100 : 0;
        Func<WPos, float> nativeCombat = p => p.Z < 0 ? 1 : 0;
        s.Submit(new(s.CurrentContext, token, "presentation"), p => p.X > 0 ? 16 : 0);
        s.Submit(new(s.CurrentContext, token, "combat"), p => p.Y > 0 ? 2 : 0);
        var set = new IntegrationGoalSet(s, [encounter]);
        var snapshot = set.Capture([encounter, nativeCombat])!;
        Check(snapshot != null, "scoring surface captured");
        Check(set.Capture([nativeCombat]) == null, "native goal replacement fails closed");
        var map = new Map();
        map.InitGrid(1, default, 8, 8);
        map.PixelMaxG[0] = -1;
        map.PixelPriority[0] = float.MinValue;
        IntegrationGoalSet.Rasterize(map, snapshot!);
        float At(float x, float z) => map.PixelPriority[map.GridToIndex(map.WorldToGrid(new(x, z)))];
        Check(At(-2, -2) > At(2, 2), "encounter-authored goal outranks combat and presentation");
        Check(At(-2, 2) > At(-2, -2), "contributed combat value composes with native combat");
        Check(map.PixelMaxG[0] == -1 && map.PixelPriority[0] == float.MinValue, "goals cannot alter physical hazards");
        var priorities = map.PixelPriority.ToArray();
        Parallel.For(0, 4, _ =>
        {
            var local = new Map(); local.InitGrid(1, default, 8, 8);
            local.PixelMaxG[0] = -1; local.PixelPriority[0] = float.MinValue;
            IntegrationGoalSet.Rasterize(local, snapshot!);
            if (!priorities.SequenceEqual(local.PixelPriority)) throw new InvalidOperationException("non-deterministic worker publication");
        });
        Check(true, "immutable captured scoring works in concurrent native rasterizers");
    }

    private static void ConsumerTransport()
    {
        using var session = new IntegrationSession();
        using var consumer = new IntegrationExample.BossModV1Consumer(
            () => IntegrationSession.Serialize(new { schemaVersion = 1, instanceGeneration = session.InstanceGeneration,
                families = new Dictionary<string, int> { ["frame-stages"] = 1, ["goal-scoring"] = 1, ["movement-observation"] = 1 } }),
            (request, callback) => IntegrationSession.Serialize(session.Register(IntegrationSession.Parse<IntegrationSession.Registration>(request), callback)),
            request => IntegrationSession.Serialize(session.Renew(IntegrationSession.Parse<IntegrationSession.Lease>(request))),
            (request, score) => IntegrationSession.Serialize(session.Submit(IntegrationSession.Parse<IntegrationSession.GoalRequest>(request), score)),
            request => IntegrationSession.Serialize(session.Unregister(IntegrationSession.Parse<IntegrationSession.Lease>(request))));
        Check(consumer.Connect(), "BCL-only sample connects through JSON/delegate transport");
        consumer.SetScore(p => p.LengthSquared() < 25 ? 2 : 0);
        var observations = new List<string>();
        consumer.MovementProposed += _ => observations.Add("proposed");
        consumer.MovementApplied += _ => observations.Add("applied");
        string Snapshot(IntegrationSession.Context context) => IntegrationSession.Serialize(new { available = true, context });
        session.BeginFrame("scope", true);
        session.Publish("hints-ready", Snapshot);
        var captured = session.CaptureGoals("combat");
        Check(consumer.Status == "scoring" && captured.Single()(default) == 2, "sample submits a scoring surface before native navigation");
        session.Publish("movement-proposed", Snapshot);
        session.Publish("movement-applied", Snapshot);
        Check(observations.SequenceEqual(new[] { "proposed", "applied" }), "sample observes both movement boundaries without executing BMR's frame");
        consumer.Dispose();
        Check(captured.Single()(default) == 0, "sample unload releases captured scoring closure");
        session.BeginFrame("scope", true);
        session.Publish("hints-ready", Snapshot);
        session.Publish("movement-proposed", Snapshot);
        Check(observations.Count == 2, "disposed sample receives no further callbacks");
    }
}
