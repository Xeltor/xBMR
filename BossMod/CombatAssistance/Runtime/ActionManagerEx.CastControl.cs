using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace BossMod;

public sealed unsafe partial class ActionManagerEx
{
    // The complete native cast identity and immediate native target state are
    // rechecked together. A successful request does not delete its warning.
    internal bool TryExecuteCastControl(uint id, Actor player, Actor enemy, ActorCastInfo expected, bool interrupt, out string reason)
    {
        reason = "native action authority changed";
        if (!Service.Framework.IsInFrameworkUpdateThread || _inst == null || EffectiveAnimationLock > 0
            || QueuedAction || _manualQueue.HasPendingInput || AutoQueue.Manual || _movement.IsMoveRequested()
            || _movement.IsForceUnblocked() || _movement.NativeInput.ManualFacing || _hints.AutomaticMovementPaused
            || player.IsDead || player.MountId != 0 || player.FindStatus(418) != null || player.PendingKnockbacks.Count != 0
            || _ws.Party.Player()?.InstanceID != player.InstanceID || !ReferenceEquals(enemy.CastInfo, expected)
            || expected.EventHappened || expected.RemainingTime <= 0.4f || interrupt && !expected.Interruptible
            || !_hints.PotentialTargets.Any(e => e.Actor == enemy && e.Priority >= 0)) return false;
        var action = new ActionID(ActionType.Spell, id);
        var definition = ActionDefinitions.Instance[action];
        if (definition == null || !definition.IsUnlocked(_ws, player) || GetAdjustedActionID(id) != id
            || !PlanningExecutionRules.Capture(_ws, _hints).PermitsAction(player.Rotation, 0, definition.InstantAnimLock)
            || Service.ObjectTable.SearchById(enemy.InstanceID) is not IBattleChara target || !target.IsValid() || target.Address == 0
            || !target.IsCasting || target.CastActionId != expected.Action.ID || target.CurrentCastTime + 0.1f < expected.ElapsedTime
            || target.TotalCastTime - target.CurrentCastTime <= 0.4f || interrupt && !target.IsCastInterruptible
            || !interrupt && target.StatusList.Any(s => CombatAssistance.Control.CastControlImmunity.BlocksStun(s.StatusId))
            || GetActionStatus(action, enemy.InstanceID) != 0) return false;
        // These targeted controls need no directional aim. Keep the checked
        // facing throughout submission, including an imminent gaze mechanic.
        var option = Framework.Instance()->SystemConfig.GetConfigOption((uint)ConfigOption.AutoFaceTargetOnAction);
        var original = option->Value.UInt;
        try
        {
            option->Value.UInt = 0;
            var accepted = ExecuteAction(action, enemy.InstanceID, enemy.PosRot.XYZ());
            reason = accepted ? "native control submitted" : "native control rejected";
            return accepted;
        }
        finally
        {
            if (option->Value.UInt == 0) option->Value.UInt = original;
        }
    }
}
