// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// All mutations here are synchronous and scoped to a single RSR CanUse call.
// RSR's resource, cooldown, casting and clipping checks still run unchanged.
internal sealed class RsrSupportTargetBinding
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly object action;
    private readonly object setting;
    private readonly object targetInfo;
    private readonly PropertyInfo canTarget;
    private readonly PropertyInfo actionCheck;
    private readonly FieldInfo targetType;
    private readonly PropertyInfo targetOverride;
    private readonly PropertyInfo actionPreview;
    private readonly PropertyInfo forceEnable;
    private readonly PropertyInfo target;
    private readonly PropertyInfo targetActor;
    private readonly MethodInfo getCandidates;
    private readonly MethodInfo canUse;
    private readonly object genericTargetType;
    private readonly Func<IReadOnlyList<IBattleChara>, bool, IBattleChara?> select;
    private readonly Action<ulong> executed;
    private readonly bool replaceKardiaCheck;
    private readonly bool allowOriginalTargetFallback;
    private object?[]? lastArguments;
    private bool manualExecution;
    private readonly Func<bool>? manualRequested;

    public RsrSupportTargetBinding(object action, Type actionInterface,
        Func<IReadOnlyList<IBattleChara>, bool, IBattleChara?> select, Action<ulong> executed, bool replaceKardiaCheck,
        bool allowOriginalTargetFallback = false, Func<bool>? manualRequested = null)
    {
        this.manualRequested = manualRequested;
        this.action = action;
        this.select = select;
        this.executed = executed;
        this.replaceKardiaCheck = replaceKardiaCheck;
        this.allowOriginalTargetFallback = allowOriginalTargetFallback;
        var type = action.GetType();
        this.setting = type.GetProperty("Setting", Flags)!.GetValue(action)!;
        this.targetInfo = type.GetProperty("TargetInfo", Flags)!.GetValue(action)!;
        this.canTarget = this.setting.GetType().GetProperty("CanTarget", Flags)!;
        this.actionCheck = this.setting.GetType().GetProperty("ActionCheck", Flags)!;
        this.targetType = this.setting.GetType().GetField("_type", Flags)!;
        this.genericTargetType = Enum.Parse(this.targetType.FieldType, "Big");
        this.targetOverride = actionInterface.GetProperty("TargetOverride", BindingFlags.Static | BindingFlags.NonPublic)!;
        this.actionPreview = actionInterface.GetProperty("ActionPreview", BindingFlags.Static | BindingFlags.NonPublic)!;
        this.forceEnable = actionInterface.GetProperty("ForceEnable", BindingFlags.Static | BindingFlags.NonPublic)!;
        this.target = type.GetProperty("Target", Flags)!;
        this.targetActor = this.target.PropertyType.GetProperty("Target")!;
        this.getCandidates = this.targetInfo.GetType().GetMethod("GetCanTargets", Flags)!;
        this.canUse = actionInterface.GetMethod("CanUse")!;
        if (this.canTarget.PropertyType != typeof(Func<IBattleChara, bool>) || this.canUse.GetParameters().Length != 12 ||
            this.getCandidates.GetParameters().Length != 4 || this.targetActor.PropertyType != typeof(IBattleChara))
            throw new InvalidOperationException("RSR support targeting contract changed");
    }

    public object? Invoke(MethodInfo method, object?[]? arguments)
    {
        // RSR's explicit action commands retain the user's chosen behavior.
        if (this.forceEnable.GetValue(null) is true || this.manualRequested?.Invoke() == true)
        {
            var manualResult = RsrSupportActionProxy.Forward(this.action, method, arguments);
            if (method.Name == "CanUse" && manualResult is true && this.actionPreview.GetValue(null) is not true)
                this.manualExecution = true;
            return manualResult;
        }
        if (method.Name == "Use" && this.manualExecution)
        {
            this.manualExecution = false;
            return RsrSupportActionProxy.Forward(this.action, method, arguments);
        }
        if (method.Name == "CanUse" && arguments is { Length: 12 })
            return this.SelectAndCheck(arguments, execution: false);
        if (method.Name != "Use") return RsrSupportActionProxy.Forward(this.action, method, arguments);

        // A cached action must pass the same recipient policy against a fresh
        // eligible set immediately before RSR executes it. No extra action is
        // queued and no hard/focus/soft target is used as an intent channel.
        if (this.lastArguments == null || !this.SelectAndCheck((object?[])this.lastArguments.Clone(), execution: true))
            return false;
        var recipient = this.targetActor.GetValue(this.target.GetValue(this.action)) as IBattleChara;
        if (recipient == null || !recipient.IsValid() || recipient.Address == 0) return false;
        // Native action submission can change object lifetime. Keep only the
        // observed identity across Use, never dereference its wrapper afterward.
        var recipientId = recipient.GameObjectId;
        var result = RsrSupportActionProxy.Forward(this.action, method, arguments);
        if (result is true) this.executed(recipientId);
        return result;
    }

    private bool SelectAndCheck(object?[] arguments, bool execution)
    {
        arguments[0] = this.action;
        var originalType = this.targetType.GetValue(this.setting);
        var originalOverride = this.targetOverride.GetValue(null);
        var originalFilter = (Func<IBattleChara, bool>)this.canTarget.GetValue(this.setting)!;
        var originalCheck = this.actionCheck.GetValue(this.setting);
        var explicitOverride = arguments[11];
        try
        {
            if (!this.replaceKardiaCheck && originalCheck is Func<bool> check && !check()) return false;
            var candidates = this.getCandidates.Invoke(this.targetInfo,
                [arguments[1], arguments[3], this.genericTargetType, this.genericTargetType]) as IEnumerable<IBattleChara>;
            if (candidates == null) return false;
            var eligible = candidates.Where(candidate => candidate.IsValid() && candidate.Address != 0).ToArray();
            var selected = this.select(eligible, execution);
            if (selected == null || !selected.IsValid() || selected.Address == 0 || !eligible.Any(candidate => candidate.IsValid() && candidate.Address != 0 && candidate.GameObjectId == selected.GameObjectId))
            {
                if (!this.allowOriginalTargetFallback) return false;
                var fallback = RsrSupportActionProxy.Forward(this.action, this.canUse, arguments) is true;
                if (fallback && this.actionPreview.GetValue(null) is not true)
                {
                    this.lastArguments = (object?[])arguments.Clone();
                    this.manualExecution = false;
                }
                return fallback;
            }
            this.targetType.SetValue(this.setting, this.genericTargetType);
            this.targetOverride.SetValue(null, null);
            arguments[11] = this.genericTargetType;
            var selectedId = selected.GameObjectId;
            this.canTarget.SetValue(this.setting, (Func<IBattleChara, bool>)(candidate =>
                candidate.IsValid() && candidate.Address != 0 && candidate.GameObjectId == selectedId && originalFilter(candidate)));
            if (this.replaceKardiaCheck) this.actionCheck.SetValue(this.setting, (Func<bool>)(() => true));
            var accepted = RsrSupportActionProxy.Forward(this.action, this.canUse, arguments) is true;
            if (accepted && this.actionPreview.GetValue(null) is not true)
            {
                this.lastArguments = (object?[])arguments.Clone();
                // The generic selector is scoped to this check only. A later
                // execution may yield to RSR and must retain its original mode.
                this.lastArguments[11] = explicitOverride;
                this.manualExecution = false;
            }
            return accepted;
        }
        finally
        {
            arguments[11] = explicitOverride;
            this.actionCheck.SetValue(this.setting, originalCheck);
            this.canTarget.SetValue(this.setting, originalFilter);
            this.targetType.SetValue(this.setting, originalType);
            this.targetOverride.SetValue(null, originalOverride);
        }
    }
}
