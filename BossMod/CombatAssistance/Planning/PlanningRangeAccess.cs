namespace BossMod.CombatAssistance.Planning;

// Range at an evidenced readiness window, evaluated along the same complete
// native route. This credits no future attack or hazard expiry. A final warning
// area still needs its independently verified onward escape and useful endpoint.
internal readonly record struct PlanningRangeAccess(float ReadyAt, float Loss = float.PositiveInfinity, float At = float.PositiveInfinity)
{
    internal PlanningRangeAccess Advance(PlanningRange range, float ready, ImmutableArray<MovementStage> stages, WPos endpoint, float end)
    {
        var result = ready > ReadyAt ? new(ready) : this;
        foreach (var stage in stages)
            result = result.Advance(range, ready, stage.Kind, stage.From, stage.To, stage.StartSeconds, stage.EndSeconds);
        return end >= ready ? result.Observe(range, endpoint, end) : result;
    }

    internal PlanningRangeAccess Advance(PlanningRange range, float ready, MovementStageKind kind, WPos from, WPos to, float start, float end)
    {
        var result = ready > ReadyAt ? new(ready) : this;
        if (end < ready) return result;
        var earliest = Math.Max(ready, start);
        if (kind == MovementStageKind.Walk && end > start)
        {
            var delta = to - from;
            var length = delta.LengthSq();
            var minimum = (earliest - start) / (end - start);
            var progress = length > 0 ? Math.Clamp((range.Center - from).Dot(delta) / length, minimum, 1) : minimum;
            var closest = from + progress * delta;
            if (range.Loss(closest) == 0 && length > 0)
            {
                var offset = from - range.Center;
                var dot = offset.Dot(delta);
                var discriminant = dot * dot - length * (offset.LengthSq() - range.Radius * range.Radius);
                if (discriminant >= 0)
                {
                    var entry = Math.Clamp((-dot - MathF.Sqrt(discriminant)) / length, minimum, progress);
                    var time = start + entry * (end - start);
                    if (result.Loss > 0 || time < result.At) result = new(ready, 0, time);
                }
            }
            else result = result.Observe(range, closest, start + progress * (end - start));
        }
        else if (from == to)
            result = result.Observe(range, to, earliest);
        else // A displacement's intermediate trajectory cannot certify attack access.
            result = result.Observe(range, to, end);
        return result;
    }

    internal PlanningRangeAccess Observe(PlanningRange range, WPos position, float time)
    {
        if (time < ReadyAt || time > MovementPlanningContext.MaximumHorizonSeconds) return this;
        var loss = range.Loss(position);
        return loss < Loss || loss == Loss && time < At ? this with { Loss = loss, At = time } : this;
    }

    internal bool Dominates(PlanningRangeAccess other)
        => ReadyAt == other.ReadyAt && Loss <= other.Loss && At <= other.At;
}
