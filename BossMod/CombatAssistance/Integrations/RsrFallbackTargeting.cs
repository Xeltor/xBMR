using Dalamud.Game.ClientState.Objects.Types;
using System.Collections;
using System.Reflection;
using CSActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace BossMod.CombatAssistance.Integrations;

// Framework-only fallback. Original action checks establish an acquisition
// opportunity; RSR still owns rotation ordering and actual action selection.
internal sealed class RsrFallbackTargeting(RsrHostilePolicy policy, ulong player, bool solo, bool inDuty,
    bool nearestWithoutAction, Func<IBattleChara, bool> smart, Func<IEnumerable> actions,
    Func<IBattleChara, uint, bool, bool> usable, Func<IBattleChara, bool>? available = null)
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private List<(uint Id, RsrOffensiveEligibility Eligibility)>? _readyActions;

    internal static unsafe bool IsUsable(IBattleChara candidate, uint action, bool checkRange)
    {
        var player = Service.ObjectTable.LocalPlayer;
        var manager = CSActionManager.Instance();
        if (manager == null || player == null || !player.IsValid() || player.Address == 0
            || !candidate.IsValid() || candidate.Address == 0) return false;
        var adjusted = manager->GetAdjustedActionId(action);
        if (manager->GetActionStatus(CSActionType.Action, adjusted, candidate.GameObjectId) != 0) return false;
        if (checkRange)
        {
            var error = CSActionManager.GetActionInRangeOrLoS(adjusted, (CSGameObject*)player.Address, (CSGameObject*)candidate.Address);
            if (error != 0 && error != 565) return false;
        }
        return CSActionManager.CanUseActionOnTarget(adjusted, (CSGameObject*)candidate.Address);
    }

    internal static bool AllowsEngagement(RsrHostilePolicy policy, bool targetsPlayerOrPet, bool hasTarget,
        bool solo, bool inDuty, bool smart)
        => targetsPlayerOrPet || (policy switch
        {
            RsrHostilePolicy.AllTargetsCanAttack => true,
            RsrHostilePolicy.TargetsHaveTarget => hasTarget,
            RsrHostilePolicy.AllTargetsWhenSolo => solo || hasTarget,
            RsrHostilePolicy.AllTargetsWhenSoloInDuty => solo && inDuty || hasTarget,
            RsrHostilePolicy.SoloDeepDungeonSmart => smart,
            _ => false
        });

    internal bool Allows(IBattleChara candidate)
    {
        try
        {
            if (available != null && !available(candidate)) return false;
            var target = candidate.TargetObject;
            return AllowsEngagement(policy, target != null && (target.GameObjectId == player || target.OwnerId == player),
                target != null, solo, inDuty, policy == RsrHostilePolicy.SoloDeepDungeonSmart && smart(candidate));
        }
        catch { return false; }
    }

    internal bool CanAcquire(IBattleChara candidate)
    {
        try
        {
            // A disabled nearest-target switch does not disable normal action
            // targeting. Find an original usable hostile action without calling
            // CanUse/FindTarget or changing the provider's manual/henched mode.
            if (nearestWithoutAction) return true;
            foreach (var action in _readyActions ??= CaptureReadyActions())
                if (usable(candidate, action.Id, true) && action.Eligibility.Allows(candidate, candidate)) return true;
        }
        catch { }
        return false;
    }

    private List<(uint Id, RsrOffensiveEligibility Eligibility)> CaptureReadyActions()
    {
        var ready = new List<(uint, RsrOffensiveEligibility)>();
        // Read action readiness once for this framework snapshot, not once for
        // every candidate in an otherwise empty native target list.
        foreach (var action in actions())
            {
                if (action == null) continue;
                object? Read(object o, string name) => o.GetType().GetProperty(name, Instance)?.GetValue(o);
                if (Read(action, "Setting") is not { } setting || Read(setting, "IsFriendly") is not false
                    || Read(action, "Info") is not { } info || Read(action, "TargetInfo") is not { } targetInfo) continue;
                var id = Convert.ToUInt32(Read(action, "AdjustedID") ?? 0u);
                if (id == 0 || !Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)
                    || !row.CanTargetHostile || row.AttackType.RowId == 0
                    // A single-target opportunity needs no guessed AoE count or
                    // facing. Area actions remain owned by original selection.
                    || row.CastType != 1 || row.EffectRange != 0) continue;
                var basic = info.GetType().GetMethod("BasicCheck", Instance);
                if (basic?.GetParameters() is not { Length: 6 } parameters
                    || basic.Invoke(info, [false, false, false, false, false, Enum.ToObject(parameters[5].ParameterType, 0)]) is not true) continue;
                ready.Add((id, new(action, setting, targetInfo)));
            }
        return ready;
    }
}
