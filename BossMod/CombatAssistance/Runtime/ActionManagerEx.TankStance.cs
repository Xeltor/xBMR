using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;

namespace BossMod;

public sealed unsafe partial class ActionManagerEx
{
    internal bool TryExecuteTankStance(ActionID action, Actor player, uint stance, bool enable, bool noSelectedGcd = false)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread || _inst == null || EffectiveAnimationLock > 0
            || QueuedAction || _manualQueue.HasPendingInput || AutoQueue.Manual || _movement.IsMoveRequested()
            || _movement.IsForceUnblocked() || _movement.NativeInput.ManualFacing || _hints.AutomaticMovementPaused
            || player.IsDead || player.MountId != 0 || player.CastInfo != null || player.PendingKnockbacks.Count != 0
            || _ws.Party.Player()?.InstanceID != player.InstanceID || player.FindStatus(418) != null
            || ActionDefinitions.Instance[action] is not { } definition || !definition.IsUnlocked(_ws, player)
            || !noSelectedGcd && GCD() < definition.InstantAnimLock + AnimationLockDelayEstimate
            || !PlanningExecutionRules.Capture(_ws, _hints).PermitsAction(player.Rotation, 0, definition.InstantAnimLock)
            || Service.ObjectTable.LocalPlayer is not IBattleChara local || !local.IsValid() || local.Address == 0
            || local.GameObjectId != player.InstanceID || local.IsCasting || local.IsDead
            || local.StatusList.Any(s => s.StatusId == stance) == enable
            || GetActionStatus(action, player.InstanceID) != 0)
            return false;
        return ExecuteAction(action, player.InstanceID, player.PosRot.XYZ());
    }
}
