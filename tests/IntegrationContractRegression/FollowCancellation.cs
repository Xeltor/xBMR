using BossMod;
using System.Reflection;
using System.Runtime.CompilerServices;

static partial class Checks
{
    private static void FollowCancellation()
    {
        var type = typeof(AIHints).Assembly.GetType("BossMod.AI.AIBehaviour", true)!;
        var execute = type.GetMethod("Execute")!;
        Check(execute.ReturnType == typeof(void) && execute.GetCustomAttribute<AsyncStateMachineAttribute>() == null,
            "follow application is synchronous in the native frame rather than a post-await controller continuation");
        var behavior = RuntimeHelpers.GetUninitializedObject(type);
        type.GetMethod("Dispose")!.Invoke(behavior, null);
        Check((bool)type.GetField("cancel", InternalInstance)!.GetValue(behavior)!, "native stop marks the previous follow owner canceled");
        // No actors/controller/world are supplied: a canceled owner must return before touching
        // any of them, even if the old executor is called again after follow stop.
        execute.Invoke(behavior, [null, null]);
        Check(true, "canceled follow owner cannot access/apply late actor/controller state");
    }
}
