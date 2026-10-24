using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// The mode IPC also replaces targeting/Boolean overrides. A temporary native
// pause must not erase an AutoDuty/user override, or pin a previously unset one.
// Restore only the exact default override values produced by our own call.
internal sealed class RsrModeOptions
{
    private readonly PropertyInfo _overrides;
    private readonly PropertyInfo _targeting;
    private readonly object? _previousOverrides;
    private readonly object? _previousTargeting;
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal RsrModeOptions(Type data)
    {
        _overrides = data.GetProperty("IpcOverrides", Flags) ?? throw new MissingMemberException("IpcOverrides");
        _targeting = data.GetProperty("TargetingTypeOverride", Flags) ?? throw new MissingMemberException("TargetingTypeOverride");
        if (_overrides.SetMethod == null || _targeting.SetMethod == null) throw new MissingMethodException("RSR override restoration");
        _previousOverrides = _overrides.GetValue(null);
        _previousTargeting = _targeting.GetValue(null);
    }

    internal void RestoreOwnWrite(RsrMode requested, RsrHostilePolicy policy)
    {
        var current = _overrides.GetValue(null);
        var own = requested == RsrMode.Off ? current == null : IsDefaultRequest(current, policy);
        if (own && ReferenceEquals(_overrides.GetValue(null), current)) _overrides.SetValue(null, _previousOverrides);
        // This service never starts PvP; preserve its native targeting if called
        // for that mode accidentally. Non-PvP ordinary mode IPC writes null.
        if (requested != RsrMode.PvP && _targeting.GetValue(null) == null) _targeting.SetValue(null, _previousTargeting);
    }

    private static bool IsDefaultRequest(object? value, RsrHostilePolicy policy)
    {
        if (value == null) return false;
        object? Get(string name) => value.GetType().GetProperty(name)?.GetValue(value)
            ?? (value.GetType().GetProperty(name) == null ? throw new MissingMemberException(name) : null);
        return Get("HostileType") is { } target && Convert.ToByte(target) == (byte)policy
            && Get("TargetFreely") == null && Get("AutoOffAfterCombat") == null && Get("FriendlyPartyNpcHealRaise") == null;
    }
}
