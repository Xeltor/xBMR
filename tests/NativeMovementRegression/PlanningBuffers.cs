using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void PlanningBuffers()
    {
        var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            new(-2, -2), false, 6, 1, null, 1, 1, 1);
        var source = NavigationMapSource.Capture(new ArenaBoundsSquare(4, mapResolution: 1), default, null, default, false);
        var input = new NavigationDecision.CapturedInput(source, [], [], new([p => p.X > 0 ? 2 : 0]), true, [], identity, false);
        var goalCalls = 0;
        var once = NativeNavigationSnapshot.Capture(input with { Goals = new([p => { ++goalCalls; return p.X; }]) });
        var onceMap = once.CopyStrictMap();
        Check(goalCalls == (onceMap.Width + 1) * (onceMap.Height + 1),
            "One frozen native goal capture serves both recovery and joint planning without repeated callbacks.");
        var unscoredInput = input with { Goals = new([p => p.X < 0 ? float.MinValue : 2]) };
        var unscoredNative = new NavigationDecision.Context();
        var unscoredExpected = NavigationDecision.BuildCaptured(unscoredNative, unscoredInput);
        var unscoredSnapshot = NativeNavigationSnapshot.Capture(unscoredInput);
        var unscoredActual = new NavigationDecision.Context();
        var unscoredRecovery = unscoredSnapshot.Recover(unscoredActual);
        Check(unscoredExpected.Destination == unscoredRecovery.Destination
            && unscoredActual.Map.PixelPriority.SequenceEqual(unscoredNative.Map.PixelPriority),
            "Shared goal capture retains native recovery's unscored-cell semantics.");
        var recoveryContext = new NavigationDecision.Context();
        NavigationPublication? previousPublication = null;
        WPos[]? previousCopy = null;
        foreach (var captured in new[] { input, input with { Obstacles = [new SDCircle(default, 1)] },
            input with { Warnings = [(new SDCircle(default, 1), identity.WorldTime.AddSeconds(4), 0)] },
            input with { Warnings = [(new SDCircle(default, 100), identity.WorldTime.AddSeconds(4), 0)] } })
        {
            var snapshot = NativeNavigationSnapshot.Capture(captured);
            var freshRecovery = snapshot.Recover(new());
            var reusedRecovery = snapshot.Recover(recoveryContext);
            Check(freshRecovery.Destination == reusedRecovery.Destination && freshRecovery.TimeToGoal == reusedRecovery.TimeToGoal
                && freshRecovery.LeewaySeconds == reusedRecovery.LeewaySeconds,
                "Worker recovery scratch reuse retains exact routing and deadlines across changed geometry and warnings.");
            if (previousPublication != null) Check(previousPublication.CopyWitness().SequenceEqual(previousCopy!),
                "Reusing recovery scratch does not mutate a previously published route witness.");
            previousPublication = reusedRecovery.Publication!;
            previousCopy = previousPublication.CopyWitness();
            var strict = snapshot.CopyStrictMap();
            var priorities = strict.PixelPriority.ToArray();
            NavigationDecision.AvoidForbiddenZone(strict, identity.AvoidanceBuffer);
            Check(snapshot.CopyPresentation().SequenceEqual(strict.PixelPriority.Select((p, i) => p - priorities[i])),
                "Skipping empty cushion work preserves native presentation for clear, physically blocked and warning maps.");
            var inspection = snapshot.CopyRecoveryMap();
            var safetyBuffer = inspection.PixelMaxG;
            var priorityBuffer = inspection.PixelPriority;
            Check(ReferenceEquals(snapshot.CopyRecoveryMap(inspection), inspection)
                && ReferenceEquals(inspection.PixelMaxG, safetyBuffer) && ReferenceEquals(inspection.PixelPriority, priorityBuffer),
                "Framework inspection reuses only its own buffers without creating another map per frame.");
            var ownedMap = snapshot.CopyStrictMap();
            var owned = new MovementPlanningContext(identity, ownedMap, 0, 0,
                Enumerable.Repeat(true, snapshot.Cells).ToArray(), snapshot.CopyPresentation(), [], [], takeOwnership: true);
            var originalPriority = owned.NativePriority(0);
            Array.Fill(inspection.PixelMaxG, -1000);
            Array.Fill(inspection.PixelPriority, 99);
            Check(owned.NativePriority(0) == originalPriority
                && snapshot.CopyRecoveryMap(inspection).PixelMaxG.Any(p => p == float.MaxValue),
                "Reused framework inspection never mutates worker-owned context or frozen native safety.");
        }
        var map = new Map(); map.InitGrid(1, default, 2, 2);
        var allowed = Enumerable.Repeat(true, 4).ToArray();
        var scores = new float[4];
        var copied = new MovementPlanningContext(identity, map, 0, 0, allowed, scores, [], []);
        allowed[0] = false; scores[0] = 99; map.PixelPriority[0] = 99;
        Check(copied.DestinationAllowed(0) && copied.Presentation(0) == 0 && copied.NativePriority(0) == 0,
            "Ordinary context callers retain defensive copies of mutable maps and score arrays.");
    }
}
