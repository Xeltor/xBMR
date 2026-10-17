using BossMod;
using BossMod.Pathfinding;
using System.Reflection;
using System.Text.Json;

static partial class Checks
{
    private static void NavigationSnapshots()
    {
        var bitmap = new Bitmap(10, 10, default, default, 1024);
        bitmap[4, 5] = true;
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(2, mapResolution: 1), default, null,
            new(bitmap, new(3, 4, 7, 8)), false);
        bitmap[4, 5] = false;
        bitmap[3, 4] = true;
        var map = new Map();
        source.Init(map);
        Check(map.PixelMaxG[map.GridToIndex(1, 1)] < 0 && map.PixelMaxG[map.GridToIndex(0, 0)] == float.MaxValue,
            "captured obstacle window cannot be changed by later bitmap writes");
        bitmap[0, 0] = true;
        var clipped = NavigationMapSource.Capture(new ArenaBoundsSquare(2, mapResolution: 1), default, null,
            new(bitmap, new(-1, 0, 3, 4)), false);
        clipped.Init(map);
        Check(map.PixelMaxG[map.GridToIndex(1, 0)] < 0, "clipped obstacle window preserves source-to-map translation");
        var disabled = NavigationMapSource.Capture(new ArenaBoundsSquare(2, mapResolution: 1), default, null,
            new(bitmap, new(-1, 0, 3, 4)), true);
        disabled.Init(map);
        Check(map.PixelMaxG.Take(16).All(v => v == float.MaxValue), "captured obstacle-disable state is authoritative");

        map.PixelMaxG[1] = 3;
        map.PixelPriority[1] = 7;
        var blocks = new byte[16]; blocks[2] = 1; blocks[3] = 2;
        var raster = new NavigationRaster(map, null, blocks);
        Array.Fill(map.PixelMaxG, 0);
        Array.Fill(map.PixelPriority, 99);
        Array.Fill(blocks, (byte)2);
        var copy = raster.CopyWindow(0, 0, 4, 4)!;
        Check(copy.TimeLimitsSeconds[0] == null && copy.TimeLimitsSeconds[1] == 3 && copy.GoalPriority[1] == 7,
            "published raster owns independent time/priority arrays");
        Check(copy.Blocks[2] == 1 && copy.Blocks[3] == 2, "arena and physical blocks stay separate");
        copy.Blocks[2] = 0; copy.TimeLimitsSeconds[1] = 999;
        Check(raster.CopyWindow(0, 0, 4, 4)!.TimeLimitsSeconds[1] == 3 && raster.CopyWindow(0, 0, 4, 4)!.Blocks[2] == 1,
            "returned window copies cannot mutate the publication");
        Check(raster.CopyWindow(-1, 0, 1, 1) == null && raster.CopyWindow(0, 0, int.MaxValue, int.MaxValue) == null,
            "invalid/overflow-sized windows fail without allocation");
        Parallel.For(0, 32, _ =>
        {
            if (raster.CopyWindow(0, 0, 4, 4)!.TimeLimitsSeconds[1] != 3)
                throw new InvalidOperationException("published raster changed under concurrent reads");
        });
        Check(true, "concurrent readers never touch worker arrays");
        var id = new NavigationIdentity(1, "instance", 10, DateTime.UtcNow, DateTimeOffset.UtcNow,
            System.Diagnostics.Stopwatch.GetTimestamp(), default, false, 6, 1, null);
        var built = new NavigationPublication(id, raster, [default, new(1, 1)], true, false,
            ThetaStar.Score.SafeMaxPrio, new NavigationDecision { Destination = new(1, 1), TimeToGoal = 1 }, false);
        Check(built.PublishedUtc == null, "worker completion is not a publication");
        var published = built.Publish();
        Check(published.PublishedUtc != null && ReferenceEquals(published.Publish(), published),
            "retained native result cannot acquire a new publication identity/time");
        Check(published.Matches("instance", 1, 0, 0, 0, id.CapturedTimestamp), "exact source identity can be queried");
        Check(!built.Matches("instance", 1, 0, 0, 0, id.CapturedTimestamp), "completed but unpublished worker cannot be queried");
        Check(!published.Matches("other", 1, 0, 0, 0, id.CapturedTimestamp) && !published.Matches("instance", 2, 0, 0, 0, id.CapturedTimestamp),
            "changed BMR/navigation generation invalidates retained data");
        Check(!published.Matches("instance", 1, 1, 0, 0, id.CapturedTimestamp) && !published.Matches("instance", 1, 0, 1, 0, id.CapturedTimestamp)
            && !published.Matches("instance", 1, 0, 0, 1, id.CapturedTimestamp), "actor/territory/content transitions invalidate navigation");
        Check(!published.Matches("instance", 1, 0, 0, 0, id.CapturedTimestamp + System.Diagnostics.Stopwatch.Frequency)
            && !published.Matches("instance", 1, 0, 0, 0, id.CapturedTimestamp - 1), "expired or rewound monotonic navigation clock is unavailable");
        var witness = published.CopyWitness(); witness[0] = new(99, 99);
        Check(published.CopyWitness()[0] == default, "witness route is copied on read");
        Check(!built.TryRemainingLeeway(id.WorldTime, 0, 0, 0, out _, id.CapturedTimestamp), "unpublished work cannot grant a cast/movement window");
        Check(published.TryRemainingLeeway(id.WorldTime, 0, 0, 0, out var initialLeeway, id.CapturedTimestamp), "native publication can supply its original leeway");
        Check(!published.TryRemainingLeeway(id.WorldTime.AddSeconds(-1), 0, 0, 0, out _, id.CapturedTimestamp)
            && !published.TryRemainingLeeway(id.WorldTime, 1, 0, 0, out _, id.CapturedTimestamp)
            && !published.TryRemainingLeeway(id.WorldTime, 0, 1, 0, out _, id.CapturedTimestamp), "rewound or foreign native results cannot be applied");
        var deadlineSource = new NavigationPublication(id, raster, [default], true, false, ThetaStar.Score.SafeMaxPrio,
            new NavigationDecision { LeewaySeconds = 2 }, false).Publish();
        Check(deadlineSource.TryRemainingLeeway(id.WorldTime.AddSeconds(0.05), 0, 0, 0, out var remaining,
            id.CapturedTimestamp + System.Diagnostics.Stopwatch.Frequency / 10) && Math.Abs(remaining - 1.9f) < 0.001,
            "remaining leeway uses the older monotonic/world deadline and does not refresh on observation");
        Check(!deadlineSource.TryRemainingLeeway(id.WorldTime, 0, 0, 0, out _, id.CapturedTimestamp + System.Diagnostics.Stopwatch.Frequency),
            "a paused world clock cannot extend retained native navigation");
        var next = NavigationPublication.NextGeneration();
        Check(NavigationPublication.NextGeneration() > next, "each scheduled search has a distinct identity");
    }

    private static object? NativeField(object value, string field)
        => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(value);

    private static void NativeNavigationCapture()
    {
        var assembly = typeof(NavigationDecision).Assembly;
        var configType = assembly.GetType("BossMod.AI.AIConfig", true)!;
        // Set only the offline native config required by AIHints; no game/world/services are started.
        if (!Service.Config._nodes.ContainsKey(configType))
            Service.Config._nodes[configType] = (ConfigNode)Activator.CreateInstance(configType, nonPublic: true)!;
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(3, mapResolution: 1), PathfindMapCenter = new(20, 30) };
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.PosRot = new(20.2f, 0, 30.2f, 0);
        var now = new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);
        hints.ForbiddenZones.Add((new SDCircle(new(20, 30), 100), now.AddSeconds(10), 1));
        var capture = typeof(NavigationDecision).GetMethod("CaptureInput", BindingFlags.Static | BindingFlags.NonPublic)!;
        var build = typeof(NavigationDecision).GetMethod("BuildCaptured", BindingFlags.Static | BindingFlags.NonPublic)!;
        var context = new NavigationDecision.Context();
        var noConsumerInput = capture.Invoke(null, [now, hints, actor, 6f, 0f, (ushort)0, (ushort)0])!;
        Check(!(bool)noConsumerInput.GetType().GetProperty("PublishRaster")!.GetValue(noConsumerInput)!,
            "no supported subscriber means no full IPC raster copy is requested");
        var noConsumerDecision = (NavigationDecision)build.Invoke(null, [context, noConsumerInput])!;
        var lightPublication = NativeField(noConsumerDecision, "Publication")!;
        Check(NativeField(lightPublication, "Raster") == null && NativeField(lightPublication, "Source") != null,
            "unsubscribed native navigation retains its deadline identity without allocating a diagnostic raster");

        var sessionType = assembly.GetType("BossMod.Integration.IntegrationSession", true)!;
        using var nativeSession = (IDisposable)Activator.CreateInstance(sessionType, BindingFlags.Instance | BindingFlags.NonPublic, null, [null, 0L], null)!;
        var generation = (string)sessionType.GetField("InstanceGeneration", InternalInstance)!.GetValue(nativeSession)!;
        var registrationType = sessionType.GetNestedType("Registration", BindingFlags.NonPublic)!;
        var registration = Activator.CreateInstance(registrationType, [1, generation, "raster-fixture", 2000])!;
        sessionType.GetMethod("Register", InternalInstance)!.Invoke(nativeSession, [registration, (Action<string>)(_ => { })]);
        sessionType.GetMethod("BeginFrame", InternalInstance)!.Invoke(nativeSession, ["fixture", true]);
        var goalSetType = assembly.GetType("BossMod.Integration.IntegrationGoalSet", true)!;
        var goalSet = Activator.CreateInstance(goalSetType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [nativeSession, Array.Empty<Func<WPos, float>>()], null)!;
        typeof(AIHints).GetField("IntegrationGoals", InternalInstance)!.SetValue(hints, goalSet);
        var input = capture.Invoke(null, [now, hints, actor, 6f, 0f, (ushort)0, (ushort)0])!;
        Check((bool)input.GetType().GetProperty("PublishRaster")!.GetValue(input)!, "a live registered subscriber opts in to immutable raster publication");
        hints.ForbiddenZones.Clear();
        hints.PathfindMapCenter = new(100, 100);
        hints.PathfindMapBounds = new ArenaBoundsSquare(100);
        actor.PosRot = new(999, 0, 999, 0);
        var decision = (NavigationDecision)build.Invoke(null, [context, input])!;
        Check(decision.Destination == noConsumerDecision.Destination && decision.LeewaySeconds == noConsumerDecision.LeewaySeconds,
            "publication demand cannot change the native movement or cast deadline decision");
        Check(context.Map.Center == new WPos(20, 30) && context.Map.Width == 6,
            "real native worker uses its captured bounds/center rather than later hints");
        var publication = NativeField(decision, "Publication")!;
        var identity = NativeField(publication, "Source")!;
        var origin = (WPos)identity.GetType().GetProperty("Origin")!.GetValue(identity)!;
        Check(origin == new WPos(20.2f, 30.2f), "real native worker uses its captured actor position");
        Check((bool)NativeField(publication, "NativeRelaxedWarnings")!, "native all-warning fallback is explicitly marked");
        var nativeRaster = NativeField(publication, "Raster")!;
        var window = nativeRaster.GetType().GetMethod("CopyWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(nativeRaster, [0, 0, 6, 6])!;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(window, window.GetType(), BossMod.Integration.IntegrationSession.Json));
        Check(json.RootElement.GetProperty("timeLimitsSeconds").EnumerateArray().All(t => t.GetSingle() == 9),
            "exported true warning deadlines survive native least-dangerous-cell relaxation");
        Check(context.Map.PixelMaxG.Take(36).All(t => t == float.MaxValue), "fallback changes native search map only, not the exported safety limits");
        var firstGeneration = (long)identity.GetType().GetProperty("Generation")!.GetValue(identity)!;
        var another = capture.Invoke(null, [now, hints, actor, 6f, 0f, (ushort)0, (ushort)0])!;
        var nextIdentity = another.GetType().GetProperty("Identity")!.GetValue(another)!;
        Check((long)nextIdentity.GetType().GetProperty("Generation")!.GetValue(nextIdentity)! > firstGeneration,
            "actual native scheduling carries a new generation, not an observation counter");

        var stressHints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(40, mapResolution: 1) };
        actor.PosRot = default;
        var light = capture.Invoke(null, [now, stressHints, actor, 6f, 0f, (ushort)0, (ushort)0])!;
        typeof(AIHints).GetField("IntegrationGoals", InternalInstance)!.SetValue(stressHints, goalSet);
        var detailed = capture.Invoke(null, [now, stressHints, actor, 6f, 0f, (ushort)0, (ushort)0])!;
        Check((bool)detailed.GetType().GetProperty("PublishRaster")!.GetValue(detailed)!, "allocation comparison includes an actually subscribed capture");
        // Reuse the real native map/search context. Measure current-thread allocations after
        // warmup; no game client, heap dump, forced live GC or broad profiling is involved.
        build.Invoke(null, [context, light]);
        build.Invoke(null, [context, detailed]);
        long Allocations(object captured)
        {
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 8; ++i) build.Invoke(null, [context, captured]);
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        var withoutSubscriber = Allocations(light);
        var withSubscriber = Allocations(detailed);
        Check(withSubscriber - withoutSubscriber >= 8 * 80 * 80 * 9,
            "unsubscribed repeated native searches avoid the full per-cell diagnostic arrays");
        Console.WriteLine($"Native navigation allocation fixture (8 x 6400 cells): unsubscribed={withoutSubscriber:N0} bytes, subscribed={withSubscriber:N0} bytes");
    }
}
