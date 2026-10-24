using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

internal enum RsrMode : byte { Off, Auto, TargetOnly, Manual, AutoDuty, Henched, PvP }
internal enum RsrHostilePolicy : byte { AllTargetsCanAttack, TargetsHaveTarget, AllTargetsWhenSoloInDuty, AllTargetsWhenSolo, SoloDeepDungeonSmart }
internal enum RsrSettingOverride : byte { UseSetting }

// The remaining mode boundary is external RSR. Preserve its existing target
// policy and Boolean settings; never recreate the old xBMR preset bridge.
internal sealed class RsrOperatingMode
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private readonly RsrRuntimeAccess _runtime;
    private readonly ICallGateSubscriber<RsrMode, RsrHostilePolicy, RsrSettingOverride, RsrSettingOverride, RsrSettingOverride, object> _change;
    private Type? _data;
    private readonly Dictionary<string, PropertyInfo> _properties = [];
    private DateTime _nextFailureLog;
    internal object? ProviderIdentity => _data?.Assembly;
    internal string? Failure { get; private set; }

    internal RsrOperatingMode(IDalamudPluginInterface plugin, RsrRuntimeAccess runtime)
    {
        _runtime = runtime;
        _change = plugin.GetIpcSubscriber<RsrMode, RsrHostilePolicy, RsrSettingOverride, RsrSettingOverride, RsrSettingOverride, object>(
            "RotationSolverReborn.ChangeOperatingModeWithOverrides");
    }

    internal RsrMode? Read()
    {
        try
        {
            if (!_runtime.TryGetSupportContext(out _, out var basic, out _)) return null;
            var data = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            if (_data != data) { _data = data; _properties.Clear(); }
            foreach (var (name, mode) in new[] { ("IsHenched", RsrMode.Henched), ("IsAutoDuty", RsrMode.AutoDuty),
                ("IsPvPStateEnabled", RsrMode.PvP), ("IsTargetOnly", RsrMode.TargetOnly), ("IsManual", RsrMode.Manual), ("State", RsrMode.Auto) })
                if (Value(name) is true) return mode;
            return RsrMode.Off;
        }
        catch (Exception error) { Failed(error); return null; }
    }

    private object? Value(string name)
    {
        if (!_properties.TryGetValue(name, out var property))
            _properties[name] = property = _data!.GetProperty(name, Flags) ?? throw new MissingMemberException(name);
        return property.GetValue(null);
    }

    internal bool Set(RsrMode mode)
    {
        try
        {
            if (Read() == null || !_change.HasAction) return false;
            var policy = (RsrHostilePolicy)Convert.ToByte(Value("CurrentTargetToHostileType"));
            if (!Enum.IsDefined(policy)) return false;
            var options = new RsrModeOptions(_data!);
            try
            {
                _change.InvokeAction(mode, policy, RsrSettingOverride.UseSetting, RsrSettingOverride.UseSetting, RsrSettingOverride.UseSetting);
            }
            finally
            {
                if (Read() == mode) options.RestoreOwnWrite(mode, policy);
            }
            var confirmed = Read() == mode;
            Failure = confirmed ? null : "RSR did not confirm the requested mode.";
            return confirmed;
        }
        catch (Exception error) { Failed(error); return false; }
    }

    private void Failed(Exception error)
    {
        Failure = "RSR mode control is unavailable.";
        if (DateTime.UtcNow < _nextFailureLog) return;
        _nextFailureLog = DateTime.UtcNow.AddSeconds(10);
        Service.Logger.Verbose(error, "External RSR mode contract unavailable; native control retained.");
    }
}
