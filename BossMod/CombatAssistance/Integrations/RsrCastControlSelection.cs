using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Native cast control owns automatic selection only while a fresh framework
// update confirms authority. Saved action toggles are read, never overwritten.
// Foreign getter changes yield the complete set; manual/preview actions pass through.
internal sealed class RsrCastControlSelection(RsrRuntimeAccess runtime, Func<bool> allowed) : IDisposable
{
    private readonly List<(uint Id, RsrOriginalActionSelectionLease Lease, object Config, FieldInfo Enabled)> _leases = [];
    private object? _rotation;
    private uint _job;
    private DateTime _validUntil;
    internal RsrCastControlEligibility Eligibility { get; } = new();
    internal bool Owned => _leases.Count != 0 && _leases.All(l => l.Lease.Owned);
    internal bool Fresh => Owned && DateTime.UtcNow < _validUntil && allowed();
    internal string Status { get; private set; } = "RSR retains cast control";

    internal static (string Name, uint Id)[] Actions(uint job) => job switch
    {
        19 => [("InterjectPvE", 7538), ("LowBlowPvE", 7540), ("ShieldBashPvE", 16)],
        21 or 32 or 37 => [("InterjectPvE", 7538), ("LowBlowPvE", 7540)],
        20 or 22 or 30 or 34 or 39 or 41 => [("LegSweepPvE", 7863)],
        23 or 31 or 38 => [("HeadGrazePvE", 7551)],
        _ => []
    };

    internal bool Allows(uint id) => Fresh && _leases.Any(l => l.Id == id && l.Enabled.GetValue(l.Config) is true);

    internal bool Update(uint job)
    {
        if (!allowed() || Actions(job).Length == 0) { Dispose(); return false; }
        try
        {
            if (!runtime.TryGetSupportContext(out _, out var basic, out var rotation)) { Dispose(); return false; }
            if (!ReferenceEquals(_rotation, rotation) || _job != job)
            {
                Dispose();
                _rotation = rotation;
                _job = job;
                Eligibility.Prepare(basic);
                var actionType = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var preview = actionType.GetProperty("ActionPreview", flags) ?? throw new MissingMemberException("ActionPreview");
                var forced = actionType.GetProperty("ForceEnable", flags) ?? throw new MissingMemberException("ForceEnable");
                foreach (var spec in Actions(job))
                {
                    var lease = RsrOriginalActionSelectionLease.Acquire(rotation, spec.Name, (original, method, args) =>
                    {
                        var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                        if (result is not true) return result;
                        try
                        {
                            if (preview.GetValue(null) is true || forced.GetValue(null) is true || !Fresh) return result;
                            if (args is { Length: > 0 }) args[0] = null;
                            return false;
                        }
                        catch (Exception error)
                        {
                            _validUntil = default;
                            Service.Logger.Verbose(error, "Native cast-control selection unavailable; RSR retained.");
                            return result;
                        }
                    });
                    try
                    {
                        var config = lease.OriginalAction.GetType().GetProperty("Config")?.GetValue(lease.OriginalAction)
                            ?? throw new MissingMemberException("RSR action configuration");
                        var enabled = config.GetType().GetField("_isEnable", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (enabled?.FieldType != typeof(bool)) throw new MissingMemberException("RSR saved action toggle");
                        _leases.Add((spec.Id, lease, config, enabled));
                    }
                    catch { lease.Dispose(); throw; }
                }
                foreach (var item in _leases) item.Lease.Resume();
            }
            if (!Owned)
            {
                ReleaseLeases();
                Status = "Cast control yielded to a changed RSR action owner";
                return false;
            }
            _validUntil = DateTime.UtcNow.AddMilliseconds(250);
            Status = "Native cast control; saved RSR action choices retained";
            return true;
        }
        catch (Exception error)
        {
            ReleaseLeases();
            Status = "Cast-control contract unavailable; RSR retained";
            Service.Logger.Verbose(error, Status);
            return false;
        }
    }

    private void ReleaseLeases()
    {
        _validUntil = default;
        foreach (var item in _leases) item.Lease.Dispose();
        _leases.Clear();
    }

    public void Dispose()
    {
        ReleaseLeases();
        _rotation = null;
        _job = 0;
        Status = "RSR retains cast control";
    }
}
