using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// RSR remains the external rotation owner. Only automatic True North selection
// is deferred while a fresh, verified native plan owns that same GCD opportunity.
// Preview, force-enable and all other actions keep their original behavior.
internal sealed class RsrPlannedUtilitySelection(RsrRuntimeAccess runtime, Func<bool> allowed, Func<bool> defer) : IDisposable
{
    private RsrOriginalActionSelectionLease? _lease;
    private object? _rotation;
    private DateTime _retry;
    internal bool Available => _lease?.Owned == true;
    internal string Status { get; private set; } = "RSR retains True North";

    internal void Update()
    {
        if (!allowed()) { Dispose(); return; }
        if (DateTime.UtcNow < _retry) return;
        _retry = DateTime.UtcNow.AddMilliseconds(250);
        try
        {
            if (!runtime.TryGetSupportContext(out _, out var basic, out var rotation)) { Dispose(); return; }
            if (ReferenceEquals(rotation, _rotation))
            {
                if (_lease?.Owned != true) Status = "True North selection yielded to an external change";
                return;
            }
            Dispose();
            _rotation = rotation;
            var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var preview = action.GetProperty("ActionPreview", flags) ?? throw new MissingMemberException("ActionPreview");
            var forced = action.GetProperty("ForceEnable", flags) ?? throw new MissingMemberException("ForceEnable");
            _lease = RsrOriginalActionSelectionLease.Acquire(rotation, "TrueNorthPvE", (original, method, args) =>
            {
                var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                if (result is not true) return result;
                try
                {
                    if (preview.GetValue(null) is true || forced.GetValue(null) is true) return result;
                    if (allowed() && defer())
                    {
                        if (args is { Length: > 0 }) args[0] = null;
                        return false;
                    }
                }
                catch (Exception error)
                {
                    Status = "True North plan check unavailable; RSR selection retained";
                    Service.Logger.Verbose(error, Status);
                }
                return result;
            });
            _lease.Resume();
            Status = "True North participates in native movement planning";
        }
        catch (Exception error)
        {
            _lease?.Dispose();
            _lease = null;
            Status = "True North selection contract unavailable; RSR retains ownership";
            Service.Logger.Verbose(error, Status);
        }
    }

    public void Dispose()
    {
        _lease?.Dispose();
        _lease = null;
        _rotation = null;
        Status = "RSR retains True North";
    }
}
