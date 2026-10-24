namespace BossMod.CombatAssistance.Planning;

// A finite speed boost cannot make the entire forecast faster. Unknown speed
// after an observed buff expires is zero: routes must finish before that point
// or wait for a new observation, rather than inventing a usable future speed.
internal sealed record PlanningTravel(float Baseline, float Boost = 0, float Until = 0)
{
    internal bool Valid => float.IsFinite(Baseline) && Baseline >= 0 && float.IsFinite(Boost) && Boost >= 0
        && float.IsFinite(Until) && Until >= 0 && (Baseline > 0 || Boost > 0 && Until > 0);
    internal float At(float time) => time < Until ? Math.Max(Baseline, Boost) : Baseline;
    internal float Seconds(float distance, float start)
    {
        if (distance <= 0) return 0;
        var fast = Math.Max(0, Until - start);
        var speed = At(start);
        if (speed > 0 && distance <= fast * speed) return distance / speed;
        var rest = Math.Max(0, distance - fast * speed);
        return Baseline > 0 ? fast + rest / Baseline : float.PositiveInfinity;
    }
    internal bool Same(PlanningTravel other, float age) => Baseline == other.Baseline && Boost == other.Boost
        && Math.Abs(Until - Math.Max(0, other.Until - age)) <= 0.01f;
    internal bool Dominates(PlanningTravel other, float from)
        => new[] { from, Until, other.Until }.Where(t => t >= from).All(t => At(t) >= other.At(t));
}
