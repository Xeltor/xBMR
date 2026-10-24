using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Suspend only RSR's independent fallback during its original synchronous
// callback. Its action delivery still uses the policy-constrained original
// concrete action. No saved setting, state/mode or lasting lease is introduced.
internal sealed class RsrAutomaticTargetScope : IDisposable
{
    private readonly Func<bool> _configured;
    private readonly Action<bool> _setConfigured;
    private readonly Func<bool> _session;
    private readonly Action<bool> _setSession;
    private readonly Func<object?> _overrides;
    private readonly Action<object?> _setOverrides;
    private readonly bool _oldConfigured;
    private readonly bool _oldSession;
    private readonly object? _oldOverrides;
    private readonly object? _ownedOverrides;
    private bool _disposed;

    internal RsrAutomaticTargetScope(Func<bool> configured, Action<bool> setConfigured,
        Func<bool> session, Action<bool> setSession, Func<object?> overrides, Action<object?> setOverrides,
        Func<object, object> suppressOverride)
    {
        _configured = configured; _setConfigured = setConfigured; _session = session; _setSession = setSession;
        _overrides = overrides; _setOverrides = setOverrides;
        _oldConfigured = configured(); _oldSession = session(); _oldOverrides = overrides();
        _ownedOverrides = _oldOverrides == null ? null : suppressOverride(_oldOverrides);
        try
        {
            setConfigured(false); setSession(false);
            if (_ownedOverrides != null) setOverrides(_ownedOverrides);
        }
        catch { Dispose(); throw; }
    }

    internal static RsrAutomaticTargetScope Enter(Assembly basic)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var config = basic.GetType("RotationSolver.Basic.Service", true)!.GetProperty("Config", flags)!.GetValue(null)!;
        var condition = config.GetType().GetProperty("TargetFreely", flags)!.GetValue(config)!;
        var value = condition.GetType().GetProperty("Value", flags)!;
        var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
        var session = center.GetProperty("TargetFreelyOverride", flags)!;
        var overrides = center.GetProperty("IpcOverrides", flags)!;
        if (value.PropertyType != typeof(bool) || session.PropertyType != typeof(bool)
            || value.SetMethod == null || session.SetMethod == null || overrides.SetMethod == null)
            throw new MissingMemberException("RSR automatic targeting override contract changed");
        return new(() => (bool)value.GetValue(condition)!, v => value.SetValue(condition, v),
            () => (bool)session.GetValue(null)!, v => session.SetValue(null, v),
            () => overrides.GetValue(null), v => overrides.SetValue(null, v), Suppress);
    }

    internal static object Suppress(object original)
    {
        string[] names = ["HostileType", "TargetFreely", "AutoOffAfterCombat", "FriendlyPartyNpcHealRaise"];
        var properties = names.Select(name => original.GetType().GetProperty(name)
            ?? throw new MissingMemberException("RSR IPC targeting overrides", name)).ToArray();
        if (properties[1].PropertyType != typeof(bool?)) throw new NotSupportedException("RSR TargetFreely override changed");
        var constructor = original.GetType().GetConstructor(properties.Select(p => p.PropertyType).ToArray())
            ?? throw new MissingMethodException("RSR IPC override constructor changed");
        return constructor.Invoke(properties.Select((p, i) => i == 1 ? false : p.GetValue(original)).ToArray());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // All writes are synchronous. A foreign replacement/value still wins.
        try { if (_ownedOverrides != null && ReferenceEquals(_overrides(), _ownedOverrides)) _setOverrides(_oldOverrides); }
        finally
        {
            try { if (!_session()) _setSession(_oldSession); }
            finally { if (!_configured()) _setConfigured(_oldConfigured); }
        }
    }
}
