using BossMod;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void NativeGoalMatrix()
    {
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod.Integration." + name, true)!;
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name,
            BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        var coordinator = RuntimeHelpers.GetUninitializedObject(Type("IntegrationCoordinator"));
        using var session = (IDisposable)Activator.CreateInstance(Type("IntegrationSession"), BindingFlags.Public | InternalInstance,
            null, new object?[] { (Func<long>)(() => 1L), 1000L }, null)!;
        coordinator.GetType().GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        var hints = new AIHints();
        coordinator.GetType().GetField("<hints>P", InternalInstance)!.SetValue(coordinator, hints);
        Call(session, "BeginFrame", "fixture", true);
        Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c)));
        string Context() => IntegrationSession.Serialize(session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!);
        string Request(int count = 2) => "{\"context\":" + Context() + ",\"points\":" + IntegrationSession.Serialize(Enumerable.Range(0, count).Select(i => new[] { (float)i, 0f })) + "}";
        JsonDocument Query(string? json = null) => JsonDocument.Parse((string)Call(coordinator, "EvaluateNativeGoalDetails", json ?? Request())!);
        Func<WPos, float> left = p => p.X < 1 ? 10 : 0;
        Func<WPos, float> right = p => p.X >= 1 ? 10 : 0;
        hints.GoalZones.Add(left); hints.GoalZones.Add(right);
        using (var warm = Query()) { }
        using (var matrix = Query())
        {
            var root = matrix.RootElement; var scores = root.GetProperty("scores");
            Check(root.GetProperty("available").GetBoolean() && root.GetProperty("complete").GetBoolean(), "actual native goal matrix endpoint returns a complete bounded observation");
            Check(scores[0][0].GetSingle() == 10 && scores[0][1].GetSingle() == 0
                && scores[1][0].GetSingle() == 0 && scores[1][1].GetSingle() == 10,
                "actual matrix keeps individual losses visible despite equal total native utility");
            Check(root.GetProperty("scope").GetString() == "encounter-and-zone-before-rotation", "matrix scopes early observations before later rotation goals");
            using var again = Query();
            Check(root.GetProperty("goalIds").GetRawText() == again.RootElement.GetProperty("goalIds").GetRawText()
                && root.GetProperty("goalIds")[0].GetString() != root.GetProperty("goalIds")[1].GetString(),
                "same native functions have stable distinct opaque identities inside an exact context");
        }
        hints.GoalZonesEnabled = false;
        using (var disabled = Query()) Check(!disabled.RootElement.GetProperty("enabled").GetBoolean(), "matrix preserves native goal enablement instead of granting utility permission");
        hints.GoalZones.Clear();
        using (var empty = Query()) Check(empty.RootElement.GetProperty("goalCount").GetInt32() == 0
            && empty.RootElement.GetProperty("scores")[0].GetArrayLength() == 0, "an exact native empty goal set is distinct from query failure");
        hints.GoalZones.Add(_ => float.NaN);
        using (var nan = Query()) Check(!nan.RootElement.GetProperty("available").GetBoolean(), "nonfinite native values cannot be serialized as valid individual-goal evidence");
        hints.GoalZones.Clear(); hints.GoalZones.Add(_ => { hints.GoalZones.Clear(); return 1; });
        using (var changed = Query()) Check(!changed.RootElement.GetProperty("available").GetBoolean(), "native source mutation during evaluation invalidates the whole matrix");
        hints.GoalZones.Clear(); hints.GoalZones.AddRange(Enumerable.Repeat(left, 65));
        using (var bound = Query()) Check(!bound.RootElement.GetProperty("available").GetBoolean(), "native goal matrix caps goal count without an incomplete safe-looking row");
        hints.GoalZones.Clear(); hints.GoalZones.Add(left);
        using (var points = Query(Request(33))) Check(!points.RootElement.GetProperty("available").GetBoolean(), "native matrix bounds candidate count");
        using (var extra = Query(Request().Replace("\"stage\":\"hints-ready\"", "\"stage\":\"hints-ready\",\"stageOrder\":0")))
            Check(!extra.RootElement.GetProperty("available").GetBoolean(), "computed consumer properties cannot silently enter the strict provider context schema");
        Check(Task.Run(() => { using var wrong = Query(); return !wrong.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(),
            "native individual-goal matrix stays on the exact native callback thread");
    }
}
