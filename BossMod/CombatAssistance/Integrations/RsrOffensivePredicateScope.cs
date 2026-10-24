using Dalamud.Game.ClientState.Objects.Types;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Native RSR performs its original CanUse/selection and delivery. These two
// predicates add constraints on the original settings object, including cached
// action references. Nothing replaces a concrete BaseAction or intercepts Use.
internal sealed class RsrOffensivePredicateScope : IDisposable
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly object _setting;
    private readonly PropertyInfo _actionCheck;
    private readonly PropertyInfo _canTarget;
    private readonly Func<bool>? _originalCheck;
    private readonly Func<IBattleChara, bool> _originalTarget;
    private readonly Func<bool> _ownedCheck;
    private readonly Func<IBattleChara, bool> _ownedTarget;
    private bool _disposed;

    internal RsrOffensivePredicateScope(object setting, Func<bool> mayUse, Func<ulong, bool> mayTarget,
        Func<bool> manual, Action<Exception> failed)
    {
        _setting = setting;
        _actionCheck = setting.GetType().GetProperty("ActionCheck", Flags) ?? throw new MissingMemberException("RSR ActionCheck");
        _canTarget = setting.GetType().GetProperty("CanTarget", Flags) ?? throw new MissingMemberException("RSR CanTarget");
        if (_actionCheck.PropertyType != typeof(Func<bool>) || _canTarget.PropertyType != typeof(Func<IBattleChara, bool>)
            || _actionCheck.SetMethod == null || _canTarget.SetMethod == null)
            throw new NotSupportedException("RSR offensive predicate contract changed");
        _originalCheck = _actionCheck.GetValue(setting) as Func<bool>;
        _originalTarget = _canTarget.GetValue(setting) as Func<IBattleChara, bool>
            ?? throw new MissingMemberException("RSR original target predicate");
        _ownedCheck = () =>
        {
            if (_disposed) return _originalCheck?.Invoke() ?? true;
            try { return (manual() || mayUse()) && (_originalCheck?.Invoke() ?? true); }
            catch (Exception error) { failed(error); return false; }
        };
        _ownedTarget = candidate =>
        {
            if (_disposed) return _originalTarget(candidate);
            if (!candidate.IsValid() || candidate.Address == 0) return false;
            var id = candidate.GameObjectId;
            try { return (manual() || mayTarget(id)) && _originalTarget(candidate); }
            catch (Exception error) { failed(error); return false; }
        };
        try { _actionCheck.SetValue(setting, _ownedCheck); _canTarget.SetValue(setting, _ownedTarget); }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (ReferenceEquals(_canTarget.GetValue(_setting), _ownedTarget)) _canTarget.SetValue(_setting, _originalTarget); }
        finally { if (ReferenceEquals(_actionCheck.GetValue(_setting), _ownedCheck)) _actionCheck.SetValue(_setting, _originalCheck); }
    }
}
