using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

// Native functions stay inside BMR and use the same captured goal array as navigation.
internal sealed class IntegrationNativeGoals(Func<WPos, float>[] native)
{
    internal const int MaxGoals = 64;
    internal const int MaxPoints = 32;
    private readonly IntegrationRoutes.Budget _captureBudget = new();
    private int _captured, _failed;
    private string? _failure;
    internal object Observation => new
    {
        nativeGoalCount = native.Length, capturedGuards = _captured, failedGuards = _failed,
        complete = _failed == 0, reason = _failure,
        scope = "each-final-native-goal-at-navigation-input-capture",
        meaning = "per-leaf no-loss gate before category aggregation; never route, assignment or application proof"
    };
    private Func<Vector2, bool>? Fail(string reason)
    { ++_failed; _failure = reason; return null; }

    internal Func<Vector2, bool>? Capture(Vector2[] baselines)
    {
        if (native.Length > MaxGoals || baselines.Length is < 1 or > IntegrationSession.MaxNoLossBaselines)
            return Fail("native-goal-or-baseline-work-limit");
        var thresholds = Enumerable.Repeat(float.NegativeInfinity, native.Length).ToArray();
        try
        {
            for (var g = 0; g < native.Length; ++g)
                foreach (var point in baselines)
                {
                    if (!_captureBudget.Step()) return Fail("native-comparison-capture-budget");
                    var score = native[g](new(point.X, point.Y));
                    if (!_captureBudget.Step()) return Fail("native-comparison-capture-budget");
                    if (!float.IsFinite(score)) return Fail("nonfinite-native-baseline");
                    thresholds[g] = Math.Max(thresholds[g], score);
                }
        }
        catch { return Fail("native-baseline-evaluation-failed"); }
        ++_captured;
        return point =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                for (var g = 0; g < native.Length; ++g)
                {
                    var score = native[g](new(point.X, point.Y));
                    if (!float.IsFinite(score) || score < thresholds[g]
                        || Stopwatch.GetElapsedTime(started).TotalMilliseconds > 4) return false;
                }
                return true;
            }
            catch { return false; }
        };
    }

    private sealed class Identity { internal readonly string Id = Guid.NewGuid().ToString("N"); }
    private static readonly ConditionalWeakTable<Func<WPos, float>, Identity> Identities = new();
    internal static string Id(Func<WPos, float> goal) => Identities.GetOrCreateValue(goal).Id;
}
