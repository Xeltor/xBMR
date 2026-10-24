using BossMod.CombatAssistance.Planning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace BossMod;

public sealed unsafe partial class ActionManagerEx
{
    // Called synchronously after full native route validation. UseActionLocation
    // retains native action observation while avoiding the manual-input camera
    // transforms: this action already has an explicit, checked facing and landing.
    internal bool TryExecutePlannedAction(MovementExecution execution, Actor expected, WPos origin, Vector3 ground, bool displacement,
        out string reason)
    {
        reason = "movement authority or action lock changed";
        if (!Service.Framework.IsInFrameworkUpdateThread || execution.Kind != MovementExecutionKind.Action || execution.ActionId == 0
            || _inst == null || EffectiveAnimationLock > 0 || QueuedAction || _manualQueue.HasPendingInput
            || AutoQueue.Manual || displacement && _hints.ForbidDashes || expected.PendingKnockbacks.Count != 0
            || _movement.IsMoveRequested() || _movement.IsForceUnblocked())
            return false;
        var player = GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        if (player == null || player->GetGameObjectId() != expected.InstanceID
            || !new WPos(player->Position.X, player->Position.Z).AlmostEqual(origin, 0.01f)
            || execution.Facing is { } facing && !player->Rotation.Radians().AlmostEqual(facing, 0.01f))
        {
            reason = "native position or facing changed before action submission";
            return false;
        }
        var action = new ActionID(ActionType.Spell, execution.ActionId);
        var definition = ActionDefinitions.Instance[action];
        if (definition == null || definition.IsGCD && !NativeRotationalMobility.Contains(action.ID) || !definition.IsUnlocked(_ws, expected)
            || GetAdjustedActionID(action.ID) != action.ID || !definition.IsGCD && _inst->GetCurrentCharges(action.ID) == 0
            || definition.IsGCD && (GCD() > 0 || GetAdjustedCastTime(action) != 0))
        {
            reason = "planned action or charge is unavailable";
            return false;
        }
        var target = execution.TargetId != 0 ? execution.TargetId
            : (definition.AllowedTargets & ActionTargets.Area) != 0 ? InvalidEntityId : expected.InstanceID;
        if (target != InvalidEntityId)
        {
            var wrapper = Service.ObjectTable.SearchById(target);
            if (wrapper == null || !wrapper.IsValid() || wrapper.Address == 0
                || !ActionManager.CanUseActionOnTarget(action.ID, (GameObject*)wrapper.Address))
            {
                reason = "action target is no longer a legal native target";
                return false;
            }
        }
        if (GetActionStatus(action, target) != 0)
        {
            reason = "native action availability changed before submission";
            return false;
        }
        var option = Framework.Instance()->SystemConfig.GetConfigOption((uint)ConfigOption.AutoFaceTargetOnAction);
        var original = option->Value.UInt;
        // Scope ends in the same synchronous call. No persistent config edit or
        // cross-plugin lease is necessary to preserve an explicitly planned facing.
        try
        {
            option->Value.UInt = 0;
            var accepted = ExecuteAction(action, target, ground);
            reason = accepted ? "native planned action submitted" : "native planned action rejected";
            return accepted;
        }
        finally
        {
            if (option->Value.UInt == 0)
                option->Value.UInt = original;
        }
    }
}
