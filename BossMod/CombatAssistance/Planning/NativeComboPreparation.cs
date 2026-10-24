using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Planning;

internal static class NativeComboPreparation
{
    internal static PlanningComboRecovery? Capture(RsrRedMageIntent intent, ExecutionActor player,
        IReadOnlyList<ExecutionActor> eligible, IReadOnlyList<ExecutionActor> forbidden, float eligibilityEndsIn = float.MaxValue)
    {
        var choice = intent.Choice;
        if (intent.Action.ActionId != choice.Action || intent.Action.Friendly || intent.Action.TargetId == 0
            || !float.IsFinite(choice.EndsIn) || choice.EndsIn <= 0 || choice.MinimumTargets < 1
            || !float.IsFinite(intent.RecastSeconds) || intent.RecastSeconds < 0
            || !float.IsFinite(eligibilityEndsIn) || eligibilityEndsIn <= 0) return null;
        var main = RsrExecutionCapture.Create(intent.Action, player, eligible, forbidden,
            MovementPlanningContext.MaximumHorizonSeconds, Positional.Any, null, out _);
        var alternate = intent.Fallback is { Friendly: false } fallback && fallback.ActionId == choice.Fallback
            && fallback.TargetId == intent.Action.TargetId
                ? RsrExecutionCapture.Create(fallback, player, eligible, forbidden,
                    MovementPlanningContext.MaximumHorizonSeconds, Positional.Any, null, out _) : null;
        if (main?.Opportunity is not { } opportunity)
        {
            if (alternate?.Opportunity == null) return null;
            main = alternate;
            alternate = null;
            opportunity = main.Opportunity;
            choice = choice with { Action = main.Id, Fallback = 0, MinimumTargets = 1 };
        }
        return new(intent.Action.TargetId, opportunity.Anchor, opportunity.CastReach, choice.EndsIn)
        {
            ActionId = main.Id,
            FallbackActionId = alternate?.Id ?? 0,
            AfterActionId = intent.AfterActionId,
            RecastSeconds = intent.RecastSeconds,
            ActionLockSeconds = main.LockSeconds,
            FallbackLockSeconds = alternate?.LockSeconds ?? 0,
            EligibilityEndsIn = eligibilityEndsIn,
            Kind = choice.Kind,
            MinimumTargets = choice.MinimumTargets,
            Opportunity = opportunity,
            Fallback = alternate?.Opportunity
        };
    }
}
