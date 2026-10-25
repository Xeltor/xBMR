using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void CompleteOptimum()
    {
        for (var mask = 0; mask < 32; ++mask)
            for (var goal = 0; goal < 6; ++goal)
            {
                var map = Grid(3, 2);
                for (var cell = 1; cell < 6; ++cell)
                    if ((mask & (1 << (cell - 1))) != 0) map.PixelMaxG[cell] = -1;
                map.PixelPriority[goal] = 2;
                var action = Action(map, 12, 0, new ulong[6], Enumerable.Repeat(1f, 6).ToArray(), earliest: 0.5f);
                var context = Context(map, 0, [action]);
                var reference = JointMovementSearch.Search(context, Exhaustive, proveCompleteOptimum: false);
                var proved = JointMovementSearch.Search(context, Exhaustive);
                Check(proved.HasVerifiedRoute && proved.UsefulDestination == reference.UsefulDestination
                    && proved.NativePriority == reference.NativePriority && proved.RemainingResources == reference.RemainingResources
                    && proved.ExecutionValues.SequenceEqual(reference.ExecutionValues)
                    && proved.DeliveryTimes.SequenceEqual(reference.DeliveryTimes) && proved.DurationSeconds == reference.DurationSeconds,
                    "Objective-ceiling completion matches unrestricted search across reachable goals and disconnected cells.");
            }
        var clear = Grid(3, 2);
        var idle = Context(clear, 0);
        var complete = JointMovementSearch.Search(idle, Exhaustive);
        Check(complete.Status == MovementSearchStatus.Complete && complete.ExpandedStates == 0,
            "An already optimal permanent position completes by proof without walking enumeration.");
        clear.PixelMaxG[0] = 1;
        var warning = JointMovementSearch.Search(Context(clear, 0), Exhaustive);
        Check(warning.HasVerifiedRoute && warning.UsefulDestination != Point(clear, 0),
            "A current warning cannot be certified as a permanent stationary optimum.");
    }
}
