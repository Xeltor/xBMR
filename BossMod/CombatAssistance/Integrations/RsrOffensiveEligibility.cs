using Dalamud.Game.ClientState.Objects.Types;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// These are the original read-only target checks used inside GetCanTargets.
// Do not invoke CanUse or FindTarget: both mutate concrete action selection.
internal sealed class RsrOffensiveEligibility
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly object _action;
    private readonly object _setting;
    private readonly Func<IBattleChara, bool>[] _checks;
    private readonly Func<IBattleChara, bool, bool, bool> _status;
    private readonly PropertyInfo _targetPredicate;
    private readonly PropertyInfo _hpEnabled;
    private readonly PropertyInfo _hpMinimum;
    private readonly Func<IBattleChara, bool> _priority;
    private readonly Func<IBattleChara, bool> _namedPriority;
    private readonly Func<IBattleChara, uint> _nameplate;
    private readonly Func<uint, int> _iconPriority;

    internal RsrOffensiveEligibility(object action, object setting, object targetInfo)
    {
        _action = action; _setting = setting;
        var type = targetInfo.GetType();
        var helper = type.Assembly.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!;
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        _priority = helper.GetMethod("IsTopPriorityHostile", flags, [typeof(IBattleChara)])!.CreateDelegate<Func<IBattleChara, bool>>();
        _namedPriority = helper.GetMethod("IsTopPriorityNamedHostile", flags, [typeof(IBattleChara)])!.CreateDelegate<Func<IBattleChara, bool>>();
        _nameplate = helper.GetMethod("GetNamePlateIcon", flags, [typeof(IBattleChara)])!.CreateDelegate<Func<IBattleChara, uint>>();
        _iconPriority = helper.GetMethod("GetNamePlateIconPriority", flags, [typeof(uint)])!.CreateDelegate<Func<uint, int>>();
        Func<IBattleChara, bool> Bind(string name)
        {
            var method = type.GetMethod(name, Instance | BindingFlags.Static, [typeof(IBattleChara)])
                ?? throw new MissingMethodException(type.FullName, name);
            return method.IsStatic ? method.CreateDelegate<Func<IBattleChara, bool>>()
                : method.CreateDelegate<Func<IBattleChara, bool>>(targetInfo);
        }
        var restricted = Bind("IsRestrictedDotTarget");
        _checks = [candidate => !restricted(candidate), Bind("CheckResistance"), Bind("CheckTimeToKill"),
            Bind("TargetOnScreen"), Bind("CanUseTo")];
        _status = (type.GetMethod("CheckStatus", Instance, [typeof(IBattleChara), typeof(bool), typeof(bool)])
            ?? throw new MissingMethodException(type.FullName, "CheckStatus"))
            .CreateDelegate<Func<IBattleChara, bool, bool, bool>>(targetInfo);
        _targetPredicate = setting.GetType().GetProperty("CanTarget", Instance) ?? throw new MissingMemberException("CanTarget");
        _hpEnabled = action.GetType().GetProperty("MinHPFeature", Instance) ?? throw new MissingMemberException("MinHPFeature");
        _hpMinimum = action.GetType().GetProperty("MinHPPercent", Instance) ?? throw new MissingMemberException("MinHPPercent");
    }

    internal static bool PreservesPriority(bool originalTop, int originalIcon, bool candidateTop, int candidateIcon)
        => !originalTop || candidateTop && candidateIcon <= originalIcon;

    private (bool Top, int Icon)? Priority(IBattleChara candidate)
    {
        if (!candidate.IsValid() || candidate.Address == 0) return null;
        var id = candidate.GameObjectId;
        var top = _priority(candidate);
        if (!candidate.IsValid() || candidate.Address == 0 || candidate.GameObjectId != id) return null;
        if (!top) top = _namedPriority(candidate);
        if (!top) return (false, int.MaxValue);
        if (!candidate.IsValid() || candidate.Address == 0 || candidate.GameObjectId != id) return null;
        var icon = _nameplate(candidate);
        return (true, _iconPriority(icon));
    }

    internal bool Allows(IBattleChara candidate, IBattleChara original)
    {
        if (Priority(original) is not { } before || Priority(candidate) is not { } after
            || !PreservesPriority(before.Top, before.Icon, after.Top, after.Icon)) return false;
        if (!candidate.IsValid() || candidate.Address == 0 || candidate.MaxHp == 0) return false;
        var id = candidate.GameObjectId;
        var health = candidate.CurrentHp / (float)candidate.MaxHp;
        if (_hpEnabled.GetValue(_action) is true && health <= Convert.ToSingle(_hpMinimum.GetValue(_action))) return false;
        foreach (var check in _checks)
            if (!candidate.IsValid() || candidate.Address == 0 || candidate.GameObjectId != id || !check(candidate)) return false;
        if (!candidate.IsValid() || candidate.Address == 0 || candidate.GameObjectId != id || !_status(candidate, false, false)) return false;
        var predicate = _targetPredicate.GetValue(_setting) as Func<IBattleChara, bool>
            ?? throw new MissingMemberException("Original RSR target predicate");
        if (!candidate.IsValid() || candidate.Address == 0 || candidate.GameObjectId != id) return false;
        return predicate(candidate); // Do not dereference a wrapper after an external predicate.
    }
}
