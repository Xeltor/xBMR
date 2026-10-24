using BossMod;

public static partial class Checks
{
    private static void NativeDashSafety()
    {
        var config = new ActionTweaksConfig { DashSafety = true };
        Service.Config._nodes[typeof(ActionTweaksConfig)] = config;
        // No data sheets are needed by these native eligibility branches.
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        world.Frame.Timestamp = DateTime.UtcNow;
        var player = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.PendingKnockbacks = [];
        var target = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        target.PosRot = new(0, 0, 3, 0);
        var entry = new ActionQueue.Entry(default, target, 1, 1, 0, 0, default, null, false, false);
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(10) };
        Check(ActionPredicate.AllowDashToTarget(world, player, entry, hints), "native target dash is allowed in unobstructed safe space");
        player.PendingKnockbacks.Add(default);
        Check(!ActionPredicate.AllowDashToTarget(world, player, entry, hints), "pending knockback rejects the target dash instead of approving it");
        player.PendingKnockbacks.Clear();
        hints.ForbiddenDirections.Add((default, 30.Degrees(), world.CurrentTime));
        Check(!ActionPredicate.AllowDashToTarget(world, player, entry, hints), "a forbidden target-facing direction cannot authorize a native dash");
        hints.ForbiddenDirections.Clear();
        foreach (var mode in new[] { AIHints.SpecialMode.Pyretic, AIHints.SpecialMode.NoMovement })
        {
            hints.ImminentSpecialMode = (mode, world.FutureTime(0.5), world.FutureTime(3));
            Check(!ActionPredicate.AllowDashToTarget(world, player, entry, hints), "imminent no-movement mechanics reject an otherwise clear dash landing");
        }
        hints.ImminentSpecialMode = default;
        config.DashSafety = false;
        Check(ActionPredicate.AllowDashToTarget(world, player, entry, hints), "the existing native dash-safety opt-out no longer disables every target dash");
        hints.ForbidDashes = true;
        Check(!ActionPredicate.AllowDashToTarget(world, player, entry, hints), "authored dash prohibition remains authoritative even with optional checks disabled");
        config.DashSafety = true;
    }
}
