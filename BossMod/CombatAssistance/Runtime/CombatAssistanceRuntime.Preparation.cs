using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using CSActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace BossMod.CombatAssistance.Runtime;

internal sealed partial class CombatAssistanceRuntime
{
    private readonly NativePreparationProgress _preparation = new();

    // Only the archived productive fallback mappings are proposed. These are
    // optional branches of the joint search, not a predicted rotation or a queue.
    private unsafe ImmutableArray<PlanningAction> CapturePreparation(WorldState world, AIHints hints, Actor player,
        RsrActionEvidence? selected, ImmutableHashSet<uint> enabled)
    {
        var native = CSActionManager.Instance();
        if (native == null || !_rotationalDelivery.Available || _rotationalDelivery.ExplicitlyControlled
            || !SupportAllowed() || !_settings.Preferences.ManageMovement || CompulsoryMovementProtected
            || player.PendingKnockbacks.Count != 0 || player.Statuses.Any(s => s.ID is 1186 or 1317 && s.ExpireAt > world.CurrentTime))
        { _preparation.Reset(); return []; }
        var targetId = hints.ForcedTarget?.InstanceID ?? player.TargetID;
        var target = hints.PriorityTargetsSpan.ToArray().Select(t => t.Actor)
            .FirstOrDefault(t => t.InstanceID == targetId && !t.IsDead && !t.IsDestroyed && t.IsTargetable);
        if (target == null || _preparation.Active && (_preparation.Actor != player.InstanceID || _preparation.Target != targetId
                || world.CurrentTime < _preparation.Started || (world.CurrentTime - _preparation.Started).TotalSeconds >= MovementPlanningContext.MaximumHorizonSeconds))
        { _preparation.Reset(); return []; }
        bool Has(uint status) => player.FindStatus(status) is { } s && s.ExpireAt > world.CurrentTime;
        ImmutableArray<uint> steps;
        var committed = _preparation.Active;
        if (committed) steps = _preparation.Remaining;
        else
        {
            var distance = (target.Position - player.Position).Length() - player.HitboxRadius - target.HitboxRadius;
            if (distance <= 3 || selected?.TargetId != targetId) return [];
            steps = (player.Class, selected.ActionId) switch
            {
                (Class.NIN, 2247) when player.Level >= 90 && distance <= 20 && !Has(496) && !Has(497) && !Has(2690)
                    && native->GetAdjustedActionId(2260) == 2260 => [2259, 18806, 2267],
                (Class.WAR, 46) when !Has(2624) => [7389],
                (Class.DRG, 90) when world.Client.GetGauge<FFXIVClientStructs.FFXIV.Client.Game.Gauge.DragoonGauge>().LotdTimer == 0 => [3555],
                _ => []
            };
            if (steps.IsEmpty) return [];
        }
        var movement = NativeRotationalPreparation.MovementFor(player.Class, steps[^1]);
        if (movement == 0 || !enabled.Contains(movement) || steps.Any(id => !enabled.Contains(id))
            || player.Class == Class.NIN && (!enabled.Contains(2261) || Has(497)))
        { _preparation.Reset(); return []; }
        var latest = MovementPlanningContext.MaximumHorizonSeconds;
        if (committed) latest = Math.Min(latest, Math.Max(0, latest - (float)(world.CurrentTime - _preparation.Started).TotalSeconds));
        if (player.Class == Class.NIN && player.FindStatus(496) is { } mudra && mudra.ExpireAt > world.CurrentTime)
            latest = Math.Min(latest, Math.Max(0, (float)(mudra.ExpireAt - world.CurrentTime).TotalSeconds));
        var result = ImmutableArray.CreateBuilder<PlanningAction>();
        foreach (var id in steps)
        {
            var actionId = new ActionID(ActionType.Spell, id);
            var definition = ActionDefinitions.Instance[actionId];
            var shared = new ActionID(ActionType.Spell, NativeRotationalPreparation.SharedClockAction(id));
            if (definition == null || !definition.IsUnlocked(world, player) || _actions.GetAdjustedCastTime(actionId) != 0
                || !Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)
                || !Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(shared.ID, out var clockRow))
            { _preparation.Reset(); return []; }
            if (result.Count == 0 && (native->GetAdjustedActionId(id) != id
                || native->GetActionStatus(CSActionType.Action, id, checkRecastActive: false, checkCastingActive: false) != 0
                || id is 2259 or 7389 or 3555 && native->GetCurrentCharges(id) == 0))
                return []; // Acknowledgement/status can arrive on adjacent native frames; never repeat a mudra to advance it.
            var friendly = id is 2259 or 18806 or 7389;
            var targetActor = friendly ? player : target;
            var recast = definition.IsGCD ? _actions.GetAdjustedRecastTime(shared) * .001f : 0;
            var ready = result.Count == 0 ? Math.Max(_actions.EffectiveAnimationLock, definition.IsGCD ? _actions.GCD() : 0) : 0;
            if (!float.IsFinite(recast) || definition.IsGCD && recast <= 0 || ready > latest) return [];
            var evidence = new RsrActionEvidence(id, targetActor.InstanceID, targetActor.PosRot.XYZ(), [targetActor.InstanceID],
                friendly, false, row.CastType, definition.Range, row.EffectRange, row.XAxisModifier * .5f,
                Math.Max(0, ready), recast, 0, 0, definition.InstantAnimLock + _actions.AnimationLockDelayEstimate,
                BaseRecast: definition.IsGCD ? clockRow.Recast100ms * .1f : 0);
            var action = RsrExecutionCapture.Create(evidence, ActorValue(player), friendly ? [ActorValue(player)]
                : hints.PriorityTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(),
                friendly ? [] : hints.ForbiddenTargetsSpan.ToArray().Select(e => ActorValue(e.Actor)).ToArray(),
                latest, Positional.Any, null, out _, horizonLimited: true, improveCoverage: false);
            if (action == null) return [];
            result.Add(action with { RequiredEarlierActions = (byte)((1 << result.Count) - 1),
                Preparation = committed ? PlanningPreparationMode.Committed : PlanningPreparationMode.Optional,
                Timing = new(0, 0, 0, NativeRecast: recast, UnbuffedRecastUpperBound: recast) });
        }
        return result.ToImmutable();
    }

    private bool ApprovePreparation(MovementPlan plan, MovementPlanningContext context, int stage)
    {
        if (_rotation.WorldState.Party.Player() is not { } player || context.TargetId != player.TargetID
            || !SupportAllowed() || !ReferenceEquals(_rotation.MovementPlanning.CurrentPlan, plan)) return false;
        var remaining = context.Actions.Where(a => a.Preparation != PlanningPreparationMode.None).Select(a => a.Id).ToImmutableArray();
        return _preparation.Approve(player.InstanceID, context.TargetId, remaining, plan.Stages[stage].ActionId,
            _rotation.WorldState.CurrentTime);
    }
}
