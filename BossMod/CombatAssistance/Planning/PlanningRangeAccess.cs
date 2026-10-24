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
        {
            if (stage.EndSeconds < ready) continue;
            var earliest = Math.Max(ready, stage.StartSeconds);
            if (stage.Kind == MovementStageKind.Walk && stage.EndSeconds > stage.StartSeconds)
            {
                var delta = stage.To - stage.From;
                var length = delta.LengthSq();
                var minimum = (earliest - stage.StartSeconds) / (stage.EndSeconds - stage.StartSeconds);
                var progress = length > 0 ? Math.Clamp((range.Center - stage.From).Dot(delta) / length, minimum, 1) : minimum;
                var closest = stage.From + progress * delta;
                if (range.Loss(closest) == 0 && length > 0)
                {
                    var offset = stage.From - range.Center;
                    var dot = offset.Dot(delta);
                    var discriminant = dot * dot - length * (offset.LengthSq() - range.Radius * range.Radius);
                    if (discriminant >= 0)
                    {
                        var entry = Math.Clamp((-dot - MathF.Sqrt(discriminant)) / length, minimum, progress);
                        var time = stage.StartSeconds + entry * (stage.EndSeconds - stage.StartSeconds);
                        if (result.Loss > 0 || time < result.At) result = new(ready, 0, time);
                    }
                }
                else result = result.Observe(range, closest,
                    stage.StartSeconds + progress * (stage.EndSeconds - stage.StartSeconds));
            }
            else if (stage.From == stage.To)
                result = result.Observe(range, stage.To, earliest);
            else // A displacement's intermediate trajectory cannot certify attack access.
                result = result.Observe(range, stage.To, stage.EndSeconds);
        }
        return end >= ready ? result.Observe(range, endpoint, end) : result;
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
