using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Collections.Immutable;
using System.Diagnostics;

public static partial class Checks
{
    private static void PanicRecovery()
    {
        var map = Grid(7, 3);
        var identity = Context(map, 7, speed: 6).Identity;
        var count = map.Width * map.Height;
        bool First(int cell) => cell % 7 < 4;
        bool Second(int cell) => cell / 7 == 1 && cell % 7 is 2 or 3;
        ImmutableArray<ulong> Bits(Func<int, bool> inside)
        {
            var result = new ulong[(count + 63) >> 6];
            for (var cell = 0; cell < count; ++cell)
                if (inside(cell)) result[cell >> 6] |= 1ul << (cell & 63);
            return [.. result];
        }
        for (var cell = 0; cell < count; ++cell)
            if (First(cell)) { map.PixelMaxG[cell] = 0; map.PixelPriority[cell] = float.MinValue; }
        var hazards = new NativeRecoveryHazards([new(0, Bits(First)), new(0, Bits(Second))]);
        NavigationDecision Search(Map input)
        {
            var search = new ThetaStar();
            search.Start(input, identity.Origin, 1 / identity.Speed);
            var last = search.Execute();
            ref var node = ref search.NodeByIndex(last);
            var waypoints = NavigationDecision.GetFirstWaypoints(search, input, last, identity.Origin);
            var decision = new NavigationDecision { Destination = waypoints.first, NextWaypoint = waypoints.second,
                LeewaySeconds = node.PathLeeway, TimeToGoal = node.GScore };
            var witness = NavigationDecision.CopyWitness(search, last, identity.Origin, out var complete);
            decision.Publication = new(identity, null, witness, complete, false, node.Score, decision, false);
            return decision;
        }
        NavigationDecision Direct()
        {
            var decision = new NavigationDecision { Destination = Point(map, 11), LeewaySeconds = -1, TimeToGoal = 4f / 6 };
            decision.Publication = new(identity, null, [identity.Origin, Point(map, 11)], true, false,
                ThetaStar.Score.UnsafeImprove, decision, false);
            return decision;
        }
        var original = Direct();
        var recovered = hazards.Improve(map, identity, original, Search, default, respectFreshness: false);
        Check(recovered.Publication is { RecoveryExposureCount: 1, RecoveryHoldForFewerExposures: false }
            && recovered.Destination != null,
            "Panic recovery prefers a complete detour with one unavoidable mechanic over a direct route crossing two.");
        var transit = Grid(7, 3);
        for (var cell = 0; cell < count; ++cell)
            if (Second(cell)) transit.PixelMaxG[cell] = 0;
        var witness = recovered.Publication!.CopyWitness();
        var elapsed = 0f;
        for (var i = 1; i < witness.Length; ++i)
        {
            var duration = (witness[i] - witness[i - 1]).Length() / identity.Speed;
            Check(TimedTraversal.Check(transit, witness[i - 1], witness[i], elapsed, duration, 0, out _),
                "The whole detour avoids the second failed mechanic; an eventually safe endpoint is insufficient.");
            elapsed += duration;
        }
        for (var cell = 0; cell < count; ++cell)
            if (cell / 7 != 1) map.PixelMaxG[cell] = -1;
        var held = hazards.Improve(map, identity, original, Search, default, respectFreshness: false);
        Check(held.Publication is { RecoveryExposureCount: 1, RecoveryHoldForFewerExposures: true }
            && held.Destination == null,
            "When a wall prevents the one-hit escape, holding one exposure beats walking into two.");
        var executor = new MovementPlanExecutor();
        var plan = new MovementPlan(identity, MovementSearchStatus.NativeRecovery, [], identity.Origin, null, 0, 0, 0, 0, [],
            float.MinValue, 0, 0, "synthetic panic hold") { NativeRecovery = held };
        var execution = executor.AdvanceRecovery(plan, identity, true, true, identity.CapturedTimestamp);
        Check(execution.Kind == MovementExecutionKind.Wait && execution.Reason.Contains("fewer predicted")
            && executor.AdvanceRecovery(plan, identity, false, true, identity.CapturedTimestamp).Kind == MovementExecutionKind.Invalidated,
            "A lower-exposure panic hold is explicit, never reported as arrived safe, and cannot bypass manual authority.");
        var future = new NativeRecoveryHazards([new(0, Bits(First)), new(10, Bits(Second))]);
        var timed = future.Improve(map, identity, original, Search, default, respectFreshness: false);
        Check(timed.Publication?.RecoveryExposureCount == 1 && timed.Destination == original.Destination,
            "Crossing a future warning before its known exit deadline is not counted as an additional failed mechanic.");
        PanicCaptureCost();
    }

    private static void PanicCaptureCost()
    {
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(25, mapResolution: .5f), default, null, default, false);
        var identity = Context(Grid(2, 2), 0, speed: 6).Identity with { Origin = default };
        var captureTimes = new List<double>();
        var recoveryTimes = new List<double>();
        foreach (var number in new[] { 3, 8 })
        {
            for (var pass = 0; pass < 4; ++pass)
            {
                var fresh = identity with { CapturedTimestamp = Stopwatch.GetTimestamp() };
                var input = new NavigationDecision.CapturedInput(source, [],
                    Enumerable.Range(0, number).Select(i => ((ShapeDistance)new SDCircle(new(i * 1.5f - 1, 0), 3),
                        identity.WorldTime.AddSeconds(1), (ulong)(i + 1))).ToArray(), new([]), false, [], fresh, true);
                var timer = Stopwatch.StartNew();
                var native = NativeNavigationSnapshot.Capture(input);
                var capture = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                var recovery = native.Recover(new());
                var elapsed = timer.Elapsed.TotalMilliseconds;
                Check(recovery.Publication?.RecoveryExposureCount != null,
                    "Multi-warning native capture retains independent exposure evidence for panic recovery.");
                if (pass != 0) { captureTimes.Add(capture); recoveryTimes.Add(elapsed); }
                if (pass == 0)
                {
                    var changed = NativeNavigationSnapshot.Capture(input with
                    {
                        Warnings = input.Warnings.Select((w, i) => i == number - 1
                            ? ((ShapeDistance)new SDCircle(new(0, 1), 3), w.activation, w.Source) : w).ToArray()
                    });
                    Check(!native.SameSafetyMap(changed),
                        "Changed independent overlaps retire an escape even when the earliest aggregate deadline can stay unchanged.");
                }
            }
        }
        Console.WriteLine($"Synthetic 10000-cell independent recovery warnings (3/8 leaves): capture max={captureTimes.Max():f3}ms; recovery max={recoveryTimes.Max():f3}ms; full-witness exposure checks, not live acceptance.");
    }

    private static void RequiredRecoveryProgress()
    {
        var map = Grid(8, 4);
        var initial = Context(map, 9, speed: 6);
        var now = initial.Identity.WorldTime;
        var source = NavigationMapSource.Capture(new ArenaBoundsRect(4, 2, mapResolution: 1), default, null, default, false);
        var warning = new SDCircle(initial.Origin, 2);
        MovementPlanningContext Capture(DateTime time, float expiresIn, ShapeDistance shape)
        {
            var identity = initial.Identity with { WorldTime = time };
            var input = new NavigationDecision.CapturedInput(source, [], [(shape, time.AddSeconds(expiresIn), 5ul)], new([]), false, [], identity, true);
            var native = NativeNavigationSnapshot.Capture(input);
            return new(identity, native.CopyStrictMap(), 0, 0, Enumerable.Repeat(true, native.Cells).ToArray(), new float[native.Cells], [], [], native: native);
        }
        foreach (var context in new[] { Capture(now, 8, warning), Capture(now.AddSeconds(1), 5, new SDCircle(initial.Origin, 1.2f)) })
        {
            var recovery = NativeMovementSearch.Recover(context, new());
            Check(recovery.NativeRecovery is { Destination: not null, LeewaySeconds: > 0 }
                && !NativeMovementHost.CanDeferRecovery(context, recovery),
                "A known required escape executes while optimization is pending, including after the previous mechanic resolves.");
            var execution = new MovementPlanExecutor().AdvanceRecovery(recovery, context.Identity, true, true, context.Identity.CapturedTimestamp);
            Check(execution is { Kind: MovementExecutionKind.Walk, DepartureSeconds: > 0 },
                "Starting required recovery retains native leeway for a safe current cast instead of forcing indiscriminate cast cancellation.");
        }
    }
}
