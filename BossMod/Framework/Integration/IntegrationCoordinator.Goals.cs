namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal string EvaluateNativeGoalDetails(string json)
    {
        var request = IntegrationSession.Parse<IntegrationSession.Query>(json);
        if (request == null || !Session.Matches(request.Context) || request.Points is not { Length: >= 1 and <= IntegrationNativeGoals.MaxPoints }
            || request.Points.Any(p => p == null || p.Length != 2 || p.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1_000_000))
            || hints.GoalZones.Count > IntegrationNativeGoals.MaxGoals) return Unavailable("invalid-native-goal-matrix-query");
        var native = hints.GoalZones.ToArray(); var budget = new IntegrationRoutes.Budget();
        var matrix = new float[request.Points.Length][];
        try
        {
            for (var p = 0; p < matrix.Length; ++p)
            {
                matrix[p] = new float[native.Length];
                for (var g = 0; g < native.Length; ++g)
                {
                    if (!budget.Step()) return Unavailable("native-goal-matrix-budget");
                    var value = native[g](new(request.Points[p][0], request.Points[p][1]));
                    if (!budget.Step()) return Unavailable("native-goal-matrix-budget");
                    if (!float.IsFinite(value)) return Unavailable("nonfinite-native-goal");
                    matrix[p][g] = value;
                }
            }
            if (!Session.Matches(request.Context) || (native.Length != hints.GoalZones.Count || native.Zip(hints.GoalZones).Any(pair => !ReferenceEquals(pair.First, pair.Second)))) return Unavailable("native-goal-set-changed");
            return IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, enabled = hints.GoalZonesEnabled, complete = true,
                points = request.Points, goalIds = native.Select(g => $"{request.Context.FrameId}:{request.Context.Stage}:{IntegrationNativeGoals.Id(g)}").ToArray(),
                goalCount = native.Length, scores = matrix,
                scope = request.Context.Stage == "hints-ready" ? "encounter-and-zone-before-rotation" : "all-current-native-goals",
                meaning = "scores[pointIndex][goalIndex]; finite raw native values, never sum equivalence, route or final-navigation-input proof"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native goal matrix unavailable: {error.GetType().Name}: {error.Message}");
            return Unavailable("native-goal-matrix-evaluation-failed");
        }
    }
}
