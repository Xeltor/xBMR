namespace BossMod.CombatAssistance.Planning;

// A positioning opportunity, not a selected/predicted action. RSR still decides
// what to execute after arrival. Every point in the returned disc covers the
// required count and current target, without touching excluded collateral.
internal static class NativeRadialCoverageRegion
{
    internal static PlanningRange? Capture(ExecutionActor player, ulong target, float effectRadius, int minimumTargets,
        IReadOnlyList<ExecutionActor> eligible, IReadOnlyList<ExecutionActor> excluded, float readyIn)
    {
        if (target == 0 || minimumTargets <= 0 || eligible.Count < minimumTargets || !float.IsFinite(effectRadius) || effectRadius <= 0
            || eligible.FirstOrDefault(a => a.Id == target) is not { Id: not 0 } primary) return null;
        var centers = eligible.Select(a => a.Position).ToList();
        var local = eligible.Where(a => (a.Position - primary.Position).Length() <= 2 * effectRadius + a.Radius + primary.Radius).ToArray();
        if (local.Length >= minimumTargets)
            centers.Add(new(local.Average(a => a.Position.X), local.Average(a => a.Position.Z)));
        PlanningRange? best = null;
        foreach (var center in centers.Distinct())
        {
            var margins = eligible.Select(a => Reach(a) - (center - a.Position).Length()).OrderDescending().ToArray();
            var radius = Math.Min(margins[minimumTargets - 1], Reach(primary) - (center - primary.Position).Length())
                - MovementPlanValidation.ArrivalTolerance;
            foreach (var forbidden in excluded)
            {
                var reach = Reach(forbidden);
                if (reach >= 0)
                    radius = Math.Min(radius, (center - forbidden.Position).Length() - reach - MovementPlanValidation.ArrivalTolerance);
            }
            if (!float.IsFinite(radius) || radius <= 0) continue;
            var region = new PlanningRange(target, center, radius, RecoverExecution: true, RecoveryReadyIn: readyIn);
            if (best == null || region.Loss(player.Position) < best.Loss(player.Position)
                || region.Loss(player.Position) == best.Loss(player.Position) && region.Radius > best.Radius) best = region;
        }
        return best;

        float Reach(ExecutionActor actor)
        {
            var reach = effectRadius + actor.Radius;
            var height = actor.Height - player.Height;
            return Math.Abs(height) > reach ? float.NegativeInfinity : MathF.Sqrt(reach * reach - height * height);
        }
    }
}
