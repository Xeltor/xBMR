namespace BossMod.CombatAssistance.Planning;

// A stable circular consensus is a presentation preference during real downtime.
// It can finish an existing plan; it never creates a walk or delays an action.
internal sealed class PartyFacingPreference
{
    private Angle? _stable;
    private DateTime _since;
    private DateTime _last;
    internal static readonly Angle Tolerance = 22.5f.Degrees();

    // This preference belongs to encounter downtime, including immunity phases
    // where the local combat flag or timeline state can temporarily disappear.
    internal static bool EligibleDowntime(bool modulePresent, bool targetsAvailable,
        DateTime nextDowntimeStart, DateTime nextDowntimeEnd)
        => modulePresent && (!targetsAvailable || nextDowntimeEnd != DateTime.MaxValue
            && nextDowntimeEnd < nextDowntimeStart);

    internal Angle? Observe(DateTime now, bool allowed, IEnumerable<Angle> allies, int required, Angle current)
    {
        if (!allowed || now < _last || now - _last > TimeSpan.FromSeconds(1)) Reset();
        _last = now;
        if (!allowed) return null;
        var consensus = Consensus(allies, required);
        if (consensus == null) { Reset(); return null; }
        if (_stable == null || !_stable.Value.AlmostEqual(consensus.Value, 12f.Degrees().Rad))
        {
            _stable = consensus;
            _since = now;
            return null;
        }
        var delta = MathF.Abs((_stable.Value - current).Normalized().Rad);
        return now - _since >= TimeSpan.FromMilliseconds(500) && delta > Tolerance.Rad && delta <= 160f.Degrees().Rad
            ? _stable : null;
    }

    internal static Angle? Consensus(IEnumerable<Angle> directions, int required)
    {
        var sorted = directions.Where(d => float.IsFinite(d.Rad)).Select(d => (d.Rad % MathF.Tau + MathF.Tau) % MathF.Tau)
            .Order().ToArray();
        if (required < 2 || sorted.Length < required) return null;
        var extended = sorted.Concat(sorted.Select(a => a + MathF.Tau)).ToArray();
        Angle? best = null;
        var count = 0;
        var concentration = 0f;
        for (var i = 0; i < sorted.Length; ++i)
        {
            var sum = default(WDir);
            var n = 0;
            for (var j = i; j < i + sorted.Length && extended[j] - extended[i] <= 35f.Degrees().Rad; ++j)
            {
                sum += extended[j].Radians().ToDirection();
                ++n;
            }
            var strength = sum.Length() / n;
            if (n < required || sum.LengthSq() < 1e-6f || n < count || n == count && strength <= concentration) continue;
            best = Angle.FromDirection(sum);
            count = n;
            concentration = strength;
        }
        return best;
    }

    internal void Reset() { _stable = null; _since = default; _last = default; }
}
