using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Planning;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Movement is selected by the native planner. RSR still owns rotation selection,
// action configuration and manually forced uses. One missing/changed action is
// local: it cannot disable the remaining legal mobility options.
internal sealed class RsrNativeMobilitySelection(RsrRuntimeAccess runtime, Func<bool> allowed, Func<uint, object, bool> stationaryUse) : IDisposable
{
    private readonly Dictionary<uint, RsrOriginalActionSelectionLease> _leases = [];
    private readonly HashSet<uint> _yielded = [];
    private object? _rotation;
    private DateTime _validUntil;
    private bool Fresh => DateTime.UtcNow < _validUntil && allowed();
    internal ImmutableHashSet<uint> Owned => Fresh
        ? _leases.Where(pair => pair.Value.Owned).Select(pair => pair.Key).ToImmutableHashSet() : [];
    internal string Status { get; private set; } = "RSR retains mobility";

    // RSR generates the public property from this verified enum name. Match the
    // actual interface and getter; never infer a capability from a rotation class name.
    internal static string? Property(Type rotation, Type actionIds, Type action, uint id)
    {
        if (!actionIds.IsEnum || Enum.GetUnderlyingType(actionIds) != typeof(uint)) return null;
        var name = Enum.GetName(actionIds, id);
        return name != null && rotation.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            is { } property && action.IsAssignableFrom(property.PropertyType) ? name : null;
    }

    internal void Update(Class job)
    {
        if (!allowed()) { Dispose(); return; }
        try
        {
            if (!runtime.TryGetSupportContext(out _, out var basic, out var rotation) || RsrRuntimeAccess.Job(rotation) != (uint)job)
            { Dispose(); return; }
            if (!ReferenceEquals(rotation, _rotation))
            {
                Dispose();
                _rotation = rotation;
            }
            // The saved feature gates select which actions the planner may use.
            // They must not reopen an independent automatic movement writer.
            var desired = NativeMobilityCapture.Definitions.Select(d => d.Id).Concat([3u, 41646u, 36988u]).ToHashSet();
            foreach (var pair in _leases.ToArray())
                if (!desired.Contains(pair.Key) || !pair.Value.Owned)
                {
                    if (!pair.Value.Owned) _yielded.Add(pair.Key);
                    pair.Value.Dispose();
                    _leases.Remove(pair.Key);
                }
            var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            var ids = basic.GetType("RotationSolver.Basic.Data.ActionID", true)!;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var preview = action.GetProperty("ActionPreview", flags) ?? throw new MissingMemberException("ActionPreview");
            var forced = action.GetProperty("ForceEnable", flags) ?? throw new MissingMemberException("ForceEnable");
            foreach (var id in desired)
            {
                if (_leases.ContainsKey(id) || _yielded.Contains(id)) continue;
                var name = Property(rotation.GetType(), ids, action, id);
                if (name == null) { _yielded.Add(id); continue; }
                try
                {
                    var lease = RsrOriginalActionSelectionLease.Acquire(rotation, name, (original, method, args) =>
                    {
                        var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                        if (result is not true) return result;
                        try
                        {
                            if (!Fresh || preview.GetValue(null) is true || forced.GetValue(null) is true
                                || !_leases.TryGetValue(id, out var owned) || !owned.Owned) return result;
                            // A targeted rotational action with no displacement
                            // remains RSR's current execution opportunity.
                            if (stationaryUse(id, original)) return result;
                            if (args is { Length: > 0 }) args[0] = null;
                            return false;
                        }
                        catch (Exception error)
                        {
                            _validUntil = default;
                            Service.Logger.Verbose(error, "Mobility ownership unavailable; RSR selection retained.");
                            return result;
                        }
                    });
                    try { lease.Resume(); _leases.Add(id, lease); }
                    catch { lease.Dispose(); throw; }
                }
                catch (Exception error)
                {
                    _yielded.Add(id);
                    Service.Logger.Verbose(error, "Native mobility action {Action} unavailable; other actions remain eligible.", id);
                }
            }
            _validUntil = DateTime.UtcNow.AddMilliseconds(250);
            Status = $"Native planner owns {_leases.Count} mobility action selectors";
        }
        catch (Exception error)
        {
            Dispose();
            Status = "Mobility selection contract unavailable; RSR retains ownership";
            Service.Logger.Verbose(error, Status);
        }
    }

    public void Dispose()
    {
        _validUntil = default;
        foreach (var lease in _leases.Values) lease.Dispose();
        _leases.Clear();
        _yielded.Clear();
        _rotation = null;
        Status = "RSR retains mobility";
    }
}
