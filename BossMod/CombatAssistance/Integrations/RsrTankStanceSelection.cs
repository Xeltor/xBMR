using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Only automatic stance selection is scoped. The original action/configuration
// remains authoritative, and manual commands, previews and forced actions pass
// through. Foreign getter replacement yields until the rotation changes.
internal sealed class RsrTankStanceSelection(RsrRuntimeAccess runtime, Func<bool> allowed) : IDisposable
{
    private RsrOriginalActionSelectionLease? _lease;
    private object? _rotation;
    private object? _config;
    private FieldInfo? _enabled;
    private PropertyInfo? _manualCommands;
    private PropertyInfo? _special;
    private PropertyInfo? _intercepted;
    private PropertyInfo? _targetOnly;
    private DateTime _until;
    internal string Status { get; private set; } = "RSR retains tank stance";
    internal bool Active => _lease?.Owned == true && DateTime.UtcNow < _until && allowed()
        && _enabled?.GetValue(_config) is true && AutomaticContext();

    internal static string? Property(Class job) => job switch
    {
        Class.PLD or Class.GLA => "IronWillPvE",
        Class.WAR or Class.MRD => "DefiancePvE",
        Class.DRK => "GritPvE",
        Class.GNB => "RoyalGuardPvE",
        _ => null
    };

    private bool AutomaticContext() => _targetOnly?.GetValue(null) is false
        && _intercepted?.GetValue(null) == null
        && !RsrCommandPolicy.HasExplicitCommand(_manualCommands?.GetValue(null) as Enum, _special?.GetValue(null) as Enum);

    internal bool Update(Class job)
    {
        if (!allowed() || Property(job) is not { } property) { Dispose(); return false; }
        try
        {
            if (!runtime.TryGetSupportContext(out _, out var basic, out var rotation)
                || RsrRuntimeAccess.Job(rotation) != (uint)job) { Dispose(); return false; }
            if (!ReferenceEquals(rotation, _rotation))
            {
                Dispose();
                _rotation = rotation;
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
                _manualCommands = center.GetProperty("CommandStatus", flags) ?? throw new MissingMemberException("CommandStatus");
                _special = center.GetProperty("SpecialType", flags) ?? throw new MissingMemberException("SpecialType");
                _intercepted = center.GetProperty("CurrentInterceptedAction", flags) ?? throw new MissingMemberException("CurrentInterceptedAction");
                _targetOnly = center.GetProperty("IsTargetOnly", flags) ?? throw new MissingMemberException("IsTargetOnly");
                var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
                var preview = action.GetProperty("ActionPreview", flags) ?? throw new MissingMemberException("ActionPreview");
                var force = action.GetProperty("ForceEnable", flags) ?? throw new MissingMemberException("ForceEnable");
                _lease = RsrOriginalActionSelectionLease.Acquire(rotation, property, (original, method, args) =>
                {
                    var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                    if (result is not true) return result;
                    try
                    {
                        if (preview.GetValue(null) is true || force.GetValue(null) is true || !Active) return result;
                        if (args is { Length: > 0 }) args[0] = null;
                        return false;
                    }
                    catch (Exception error)
                    {
                        _until = default;
                        Service.Logger.Verbose(error, "Tank stance ownership unavailable; RSR retained.");
                        return result;
                    }
                });
                _config = _lease.OriginalAction.GetType().GetProperty("Config")?.GetValue(_lease.OriginalAction)
                    ?? throw new MissingMemberException("RSR action configuration");
                _enabled = _config.GetType().GetField("_isEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_enabled?.FieldType != typeof(bool)) throw new MissingMemberException("RSR saved stance toggle");
                _lease.Resume();
            }
            _until = DateTime.UtcNow.AddMilliseconds(250);
            Status = Active ? "Native stance coordination" : "Stance ownership or automatic selection unavailable";
            return Active;
        }
        catch (Exception error)
        {
            _lease?.Dispose();
            _lease = null;
            _until = default;
            Status = "Tank stance contract unavailable; RSR retained";
            Service.Logger.Verbose(error, Status);
            return false;
        }
    }

    public void Dispose()
    {
        _until = default;
        _lease?.Dispose();
        _lease = null;
        _rotation = _config = null;
        _enabled = null;
        _manualCommands = _intercepted = _targetOnly = null;
        Status = "RSR retains tank stance";
    }
}
