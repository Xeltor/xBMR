using BossMod;
using System.Reflection;
using System.Runtime.CompilerServices;

static partial class Checks
{
    private static void FollowCancellation()
    {
        var type = typeof(AIHints).Assembly.GetType("BossMod.AI.AIBehaviour", true)!;
        var execute = type.GetMethod("Execute")!;
        Check(execute.ReturnType == typeof(Task) && execute.GetCustomAttribute<AsyncStateMachineAttribute>() != null,
            "follow retains its original native async executor");
        var behavior = RuntimeHelpers.GetUninitializedObject(type);
        type.GetMethod("Dispose")!.Invoke(behavior, null);
        Check((bool)type.GetField("cancel", InternalInstance)!.GetValue(behavior)!, "native stop marks the previous follow owner canceled");
        // Native SwitchToIdle/Dispose semantics are preserved. This fixture does not
        // claim that native async work has been drained or rewrite its continuation.
    }
}
