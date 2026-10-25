using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;

public static partial class Checks
{
    private static void EarlyMechanicClearance()
    {
        var now = DateTime.UtcNow;
        var map = Grid(6, 2);
        var origin = Point(map, 0);
        var identity = Context(map, 0).Identity with { WorldTime = now };
        var source = NavigationMapSource.Capture(new ArenaBoundsRect(3, 1, mapResolution: 1), default, null, default, false);
        // A known tank-buster circle on another actor: avoid it without moving
        // the authored timestamp or losing the actor used for attribution.
        var warning = new SDCircle(origin, 1.1f);
        var input = new NavigationDecision.CapturedInput(source, [], [(warning, now.AddSeconds(8), 99ul)],
            new([]), false, [], identity, true);
        foreach (var job in new[] { Class.CNJ, Class.WHM, Class.SCH, Class.AST, Class.SGE, Class.ARC, Class.BRD, Class.MCH, Class.DNC })
        {
            var early = NativeMechanicTiming.Apply(input, job);
            var snapshot = NativeNavigationSnapshot.Capture(early);
            var strict = snapshot.CopyStrictMap();
            var cell = strict.WorldToGrid(origin);
            Check(strict.PixelMaxG[strict.GridToIndex(cell)] == 5.5f,
                $"{job} must clear an eight-second warning 2.5 seconds before activation.");
            Check(ReferenceEquals(early.Warnings, input.Warnings) && early.Identity == input.Identity,
                $"{job} clearance preserves exact authored geometry, timestamps, source actor and capture clocks.");
            var recovery = snapshot.Recover(new());
            var baseline = NativeNavigationSnapshot.Capture(input).Recover(new());
            Check(recovery.Destination != null && Math.Abs(baseline.LeewaySeconds - recovery.LeewaySeconds - 1.5f) < .001f,
                $"{job} native recovery receives the earlier deadline even without combat optimization.");
            var fallback = NavigationDecision.BuildCaptured(new(), early);
            Check(fallback.Destination == recovery.Destination && fallback.LeewaySeconds == recovery.LeewaySeconds,
                $"{job} capture-failure recovery and frozen recovery retain identical early-clearance timing.");

            var cells = strict.Width * strict.Height;
            var action = Action(strict, 10, 1, Enumerable.Repeat(1ul, cells).ToArray(), Enumerable.Repeat(1f, cells).ToArray(), cast: 6);
            var context = new MovementPlanningContext(identity, strict, 0, 0, Enumerable.Repeat(true, cells).ToArray(),
                new float[cells], [action], [], native: snapshot);
            var plan = JointMovementSearch.Search(context, Exhaustive);
            Check(plan.HasVerifiedRoute && plan.Stages.First(s => s.From != s.To).StartSeconds < 5.5f
                && !plan.Stages.Any(s => s.Kind == MovementStageKind.Cast && s.From == origin),
                $"{job} moves before casting when a cast would consume the early-clearance window.");
        }
        foreach (var job in new[] { Class.PLD, Class.WAR, Class.DRK, Class.GNB, Class.DRG, Class.BLM, Class.RDM, Class.PCT })
            Check(ReferenceEquals(NativeMechanicTiming.Apply(input, job), input) && !NativeMechanicTiming.PrefersEarlyClearance(job),
                $"{job} retains its existing movement timing policy.");
        var imminent = NativeNavigationSnapshot.Capture(NativeMechanicTiming.Apply(
            input with { Warnings = [(warning, now.AddSeconds(1), 99ul)] }, Class.WHM)).CopyStrictMap();
        var start = imminent.WorldToGrid(origin);
        Check(imminent.PixelMaxG[imminent.GridToIndex(start)] == 0,
            "A late-published warning becomes immediately urgent instead of a blocked physical cell or a new wait.");
    }
}
