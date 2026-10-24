// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Unlike the support action proxy, selection returns the ORIGINAL concrete action.
// RSR's BaseAction timing, target and post-use branches consequently remain intact.
internal class RsrMitigationSelectionProxy : DispatchProxy
{
    internal object Original = null!;
    internal Func<MethodInfo, object?[]?, object?>? Intercept;
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method == null
        ? throw new MissingMethodException("Mitigation selection method")
        : this.Intercept is { } intercept ? intercept(method, arguments) : RsrSupportActionProxy.Forward(this.Original, method, arguments);
}
