using BossMod.CombatAssistance.Diagnostics;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;
using System.Text.Json;

public static partial class Checks
{
    private static void Diagnostics()
    {
        const string request = """{"schemaVersion":1,"stream":"combat"}""";
        var snapshot = new NativeDiagnosticSnapshot();
        var now = DateTime.UtcNow;
        snapshot.Observe(true, 7, 1, now);
        var context = Context(Grid(1, 1), 0);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        using var first = JsonDocument.Parse(snapshot.Read(request, plan, true, true, "Ready", null, now));
        var root = first.RootElement;
        Check(root.GetProperty("ok").GetBoolean() && root.GetProperty("schemaVersion").GetInt32() == 1
            && root.GetProperty("plugin").GetString() == "BossModReborn" && root.GetProperty("stream").GetString() == "combat",
            "Current diagnostics use xBMR identity and the existing DevBridge provider envelope.");
        var id = root.GetProperty("lifecycle").GetProperty("id").GetString();
        var epoch = root.GetProperty("epoch").GetString();
        Check(root.GetProperty("snapshot").GetProperty("movement").GetProperty("status").GetString() == "Complete",
            "Diagnostics distinguish exhausted search from a bound or native recovery.");
        snapshot.Observe(true, 7, 1, now.AddSeconds(1));
        var bounded = plan with { Status = MovementSearchStatus.Bounded, Bound = MovementSearchBound.StateMemory,
            Stages = Enumerable.Repeat(new MovementStage(MovementStageKind.Wait, context.Origin, context.Origin, 0, 1), 100).ToImmutableArray() };
        using var second = JsonDocument.Parse(snapshot.Read(request, bounded, true, true, "Ready", null, now.AddSeconds(1)));
        var movement = second.RootElement.GetProperty("snapshot").GetProperty("movement");
        Check(second.RootElement.GetProperty("lifecycle").GetProperty("id").GetString() == id
            && second.RootElement.GetProperty("epoch").GetString() == epoch, "Repeated samples retain the current lifecycle and provider epoch.");
        Check(movement.GetProperty("status").GetString() == "Bounded" && movement.GetProperty("bound").GetString() == "StateMemory"
            && movement.GetProperty("stageCount").GetInt32() == 100 && movement.GetProperty("stagesTruncated").GetBoolean()
            && movement.GetProperty("stages").GetArrayLength() <= NativeDiagnosticSnapshot.MaximumDisplayedStages && bounded.Stages.Length == 100,
            "Display truncation is explicit and cannot truncate the executable plan.");
        var crowded = bounded with { ActionEvidence = Enumerable.Range(0, 8).Select(i => new PlannedActionEvidence((uint)i + 1,
            ulong.MaxValue, Enumerable.Range(0, 200).Select(n => ulong.MaxValue - (uint)n).ToImmutableArray(), 1)).ToImmutableArray() };
        var bytes = System.Text.Encoding.UTF8.GetByteCount(snapshot.Read(request, crowded, true, true, "Ready", new string('界', 512), now));
        Check(bytes <= NativeDiagnosticSnapshot.MaximumSnapshotBytes && crowded.ActionEvidence.All(a => a.RequiredRecipientIds.Length == 200),
            "current diagnostics fit the existing composed-provider byte limit without narrowing planner recipients or routes");
        using var stopped = JsonDocument.Parse(snapshot.Read(request, plan with { ImmediateWaypoint = context.Origin }, true, true, "Ready", null,
            now, execution: new(MovementExecutionKind.Wait, "current lock")));
        Check(stopped.RootElement.GetProperty("snapshot").GetProperty("movement").GetProperty("immediateWaypoint").ValueKind == JsonValueKind.Null,
            "waiting execution does not advertise an obsolete walking waypoint");
        snapshot.Observe(false, 0, 0, now.AddSeconds(2));
        using var ended = JsonDocument.Parse(snapshot.Read(request, null, false, false, "StandaloneLoaded", null, now.AddSeconds(2)));
        Check(!ended.RootElement.GetProperty("lifecycle").GetProperty("active").GetBoolean()
            && ended.RootElement.GetProperty("lifecycle").GetProperty("id").GetString() == id
            && ended.RootElement.GetProperty("lifecycle").GetProperty("endedAtUtc").ValueKind == JsonValueKind.String,
            "Logout retains only the current completed lifecycle metadata for bridge segmentation.");
        snapshot.Observe(true, 8, 2, now.AddSeconds(3));
        using var next = JsonDocument.Parse(snapshot.Read(request, plan with { Status = MovementSearchStatus.NativeRecovery }, false, false, "Ready", null, now.AddSeconds(3)));
        Check(next.RootElement.GetProperty("lifecycle").GetProperty("id").GetString() != id
            && next.RootElement.GetProperty("snapshot").GetProperty("movement").GetProperty("status").GetString() == "NativeRecovery",
            "A new fight has a new lifecycle and recovery is never described as completed optimization.");
        using var wrongThread = JsonDocument.Parse(snapshot.Read(request, plan, true, true, "Ready", null, now, false));
        Check(wrongThread.RootElement.GetProperty("error").GetString() == "framework_thread_required",
            "Diagnostics do not read live state from a foreign thread.");
        foreach (var invalid in new[] { "{", "[]", "{\"schemaVersion\":2,\"stream\":\"combat\"}", "{\"schemaVersion\":1,\"stream\":\"travel\"}" })
        {
            using var response = JsonDocument.Parse(snapshot.Read(invalid, null, false, false, "Ready", null, now));
            Check(!response.RootElement.GetProperty("ok").GetBoolean(), "Unsupported diagnostic request fails without a capture side effect.");
        }
        var timingReads = 0;
        NativeActionTimingObservation? ReadTiming()
        {
            ++timingReads;
            return new(now, now, 25, 100, 1, 2, 3, false, null,
                [new(3577, true, 2000, 2000, 2500, 2000, 2500, 0)], null);
        }
        using var invalidTiming = JsonDocument.Parse(snapshot.Read("{", null, true, false, "Ready", null, now, readTiming: ReadTiming));
        using var wrongTimingThread = JsonDocument.Parse(snapshot.Read(request, null, true, false, "Ready", null, now, false, readTiming: ReadTiming));
        Check(timingReads == 0, "Invalid and foreign-thread requests cannot sample the native timing API.");
        using var timingSnapshot = JsonDocument.Parse(snapshot.Read(request, crowded, true, false, "Ready", null, now, readTiming: ReadTiming));
        var timingData = timingSnapshot.RootElement.GetProperty("snapshot").GetProperty("nativeTiming");
        Check(timingReads == 1 && timingData.GetProperty("Actions")[0].GetProperty("AdjustedRecastMs").GetInt32() == 2500,
            "One valid current request samples timing once, even when the projected movement payload needs display truncation.");
        Check(NativeActionTimingObservation.ActionsFor(BossMod.Class.BLM).Length == 6
            && NativeActionTimingObservation.ActionsFor(BossMod.Class.PCT).Length == 5
            && NativeActionTimingObservation.ActionsFor(BossMod.Class.DRG).Length == 2
            && NativeActionTimingObservation.ActionsFor(BossMod.Class.WHM).IsEmpty,
            "Current timing diagnostics have a fixed mechanics-specific action set and no arbitrary action execution surface.");
    }
}
