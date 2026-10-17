using BossMod;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void ReflectedWriteAdapters()
    {
        var type = typeof(AIHints).Assembly.GetType("BossMod.Integration.IntegrationCoordinator", true)!;
        var align = type.GetMethod("AlignOwnedFateFrame", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? Align(WPos center, WPos origin, float resolution, float radius = 30)
            => align.Invoke(null, [center, origin, resolution, radius]);
        var positive = (ITuple)Align(new(2.4f, 3.4f), new(0.1f, 0.1f), 0.5f)!;
        Check((int)positive[1]! == 4 && (int)positive[2]! == 6 && (int)positive[3]! == 60,
            "owned FATE adapter retains the old truncated terrain window");
        var negative = (ITuple)Align(new(-1.2f, -1.7f), new(0.1f, 0.2f), 0.5f)!;
        Check((int)negative[1]! == -2 && (int)negative[2]! == -3,
            "negative FATE coordinates use original truncation, not a new rounding policy");
        Check(Align(new(float.NaN, 0), default, 0.5f) == null && Align(default, default, 0) == null
            && Align(default, default, float.PositiveInfinity) == null && Align(default, default, 0.00001f) == null,
            "invalid or unbounded FATE windows cannot alter hints");
        var allowed = (System.Collections.Generic.HashSet<string>)type.GetField("HallwaySavedIdentities", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Check(allowed.Count == 11, "saved writes are restricted to the original eleven audited hallway identities");
        var sessionType = typeof(AIHints).Assembly.GetType("BossMod.Integration.IntegrationSession", true)!;
        using var session = (IDisposable)Activator.CreateInstance(sessionType, BindingFlags.Public | InternalInstance, null, [null, 0L], null)!;
        var coordinator = RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        foreach (var endpoint in new[] { "ApplyFateFrame", "ApplyDutyIngress", "GetHallwaySavedEnabled", "SetHallwaySavedEnabled" })
        {
            foreach (var json in new[] { "{broken", "{\"context\":{\"schemaVersion\":1,\"instanceGeneration\":\"stale\",\"frameId\":1,\"stage\":\"hints-ready\"},\"ownerToken\":\"stale\"}" })
            {
                using var result = JsonDocument.Parse((string)type.GetMethod(endpoint, InternalInstance)!.Invoke(coordinator, [json])!);
                Check(!result.RootElement.GetProperty("available").GetBoolean(),
                    "invalid owned write rejects before accessing native game state");
            }
        }
    }
}
