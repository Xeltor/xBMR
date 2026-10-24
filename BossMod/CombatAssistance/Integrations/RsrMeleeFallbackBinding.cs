using System;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// RSR 7.5.6.8 rejects melee ranged attacks for as long as LastAction is a
// movement action. Keep that short movement hold, but not an indefinite hold.
// Only the matching history-list entry is masked, synchronously around CanUse.
// In particular LastAction, SpecialType, target filters and cast gates are intact.
internal sealed class RsrMeleeFallbackBinding(
    object action,
    Array movingActions,
    Func<uint> lastAction,
    Func<TimeSpan> elapsed,
    Func<bool> allowed,
    Func<bool> manual,
    Func<bool> preview,
    Action<bool> evaluated)
{
    internal const double MovementHoldSeconds = 0.8;
    private bool correctedSelection;

    internal static bool HasElapsed(TimeSpan age)
        => age >= TimeSpan.FromSeconds(MovementHoldSeconds);

    public object? Invoke(MethodInfo method, object?[]? arguments)
    {
        if (method.Name == "Use")
        {
            var corrected = this.correctedSelection;
            this.correctedSelection = false;
            if (corrected && !allowed()) return false;
            return RsrSupportActionProxy.Forward(action, method, arguments);
        }

        if (method.Name != "CanUse" || arguments is not { Length: 12 })
            return RsrSupportActionProxy.Forward(action, method, arguments);

        var isPreview = preview();
        if (!isPreview) this.correctedSelection = false;
        if (!allowed() || manual())
            return RsrSupportActionProxy.Forward(action, method, arguments);
        var last = lastAction();
        if (last == 0 || !HasElapsed(elapsed()))
            return RsrSupportActionProxy.Forward(action, method, arguments);

        var index = -1;
        for (var i = 0; i < movingActions.Length; i++)
        {
            if (Convert.ToUInt32(movingActions.GetValue(i)) != last) continue;
            if (index >= 0) throw new InvalidOperationException("RSR movement history contains duplicate actions");
            index = i;
        }
        if (index < 0) return RsrSupportActionProxy.Forward(action, method, arguments);

        var original = movingActions.GetValue(index)!;
        var empty = Enum.ToObject(original.GetType(), 0u);
        try
        {
            movingActions.SetValue(empty, index);
            var result = RsrSupportActionProxy.Forward(action, method, arguments);
            if (!isPreview)
            {
                this.correctedSelection = result is true;
                evaluated(result is true);
            }
            return result;
        }
        finally
        {
            // Do not overwrite a foreign writer, even during an exceptional exit.
            if (!Equals(movingActions.GetValue(index), empty))
                throw new InvalidOperationException("RSR movement history ownership changed during fallback check");
            movingActions.SetValue(original, index);
        }
    }
}
