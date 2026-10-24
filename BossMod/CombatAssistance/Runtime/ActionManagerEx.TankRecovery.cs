using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace BossMod;

public sealed unsafe partial class ActionManagerEx
{
    internal bool TryExecuteTankRecovery(ActionID action, Actor player, AIHints.Enemy enemy, ulong victim, bool authoredSwap)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread || _inst == null || EffectiveAnimationLock > 0
            || QueuedAction || _manualQueue.HasPendingInput || AutoQueue.Manual || _movement.IsMoveRequested()
            || _movement.IsForceUnblocked() || _movement.NativeInput.ManualFacing || _hints.AutomaticMovementPaused
            || player.IsDead || player.MountId != 0 || player.CastInfo != null || player.FindStatus(418) != null
            || player.PendingKnockbacks.Count != 0 || _ws.Party.Player()?.InstanceID != player.InstanceID
            || enemy.Priority < 0 || !enemy.ShouldBeTanked || !_hints.PotentialTargets.Contains(enemy)
            || enemy.Actor.IsDead || enemy.Actor.IsDestroyed || !enemy.Actor.IsTargetable || !enemy.Actor.InCombat
            || enemy.Actor.TargetID != victim || victim == player.InstanceID || authoredSwap && !enemy.PreferProvoking
            || !authoredSwap && enemy.Actor.CastInfo is { EventHappened: false }
            || ActionDefinitions.Instance[action] is not { } definition || !definition.IsUnlocked(_ws, player)
            || GetAdjustedActionID(action.ID) != action.ID || definition.CastTime != 0
            || (definition.IsGCD ? GCD() > 0 : GCD() < definition.InstantAnimLock + AnimationLockDelayEstimate)
            || !PlanningExecutionRules.Capture(_ws, _hints).PermitsAction(player.Rotation, 0, definition.InstantAnimLock)
            || Service.ObjectTable.SearchById(enemy.Actor.InstanceID) is not IBattleChara target
            || !target.IsValid() || target.Address == 0 || target.IsDead || target.TargetObjectId != victim
            || !authoredSwap && target.IsCasting || GetActionStatus(action, enemy.Actor.InstanceID) != 0)
            return false;
        var entry = new ActionQueue.Entry(action, enemy.Actor, ActionQueue.Priority.Medium, float.MaxValue, 0, 0,
            enemy.Actor.PosRot.XYZ(), player.Rotation, false, false);
        if (definition.AllowExecute?.Invoke(_ws, player, entry, _hints) == false) return false;
        var option = Framework.Instance()->SystemConfig.GetConfigOption((uint)ConfigOption.AutoFaceTargetOnAction);
        var original = option->Value.UInt;
        try
        {
            option->Value.UInt = 0;
            return ExecuteAction(action, enemy.Actor.InstanceID, enemy.Actor.PosRot.XYZ());
        }
        finally
        {
            if (option->Value.UInt == 0) option->Value.UInt = original;
        }
    }
}
