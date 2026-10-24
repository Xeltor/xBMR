using BossMod.CombatAssistance.Integrations;

namespace BossMod.CombatAssistance.Planning;

internal readonly record struct ExecutionActor(ulong Id, WPos Position, float Height, float Radius, Angle Rotation,
    bool Dead = false, bool Omnidirectional = false);

// Converts external evidence and native actor values once, before navigation starts.
// Required recipients are identities, including recipients which cannot be recovered
// in this snapshot. A missing recipient rejects the opportunity instead of hiding loss.
internal static class RsrExecutionCapture
{
    internal static PlanningAction? Create(RsrActionEvidence evidence, ExecutionActor player,
        IReadOnlyList<ExecutionActor> eligible, IReadOnlyList<ExecutionActor> forbidden,
        float latestCompletion, Positional positional, TargetFacingRequirement? tankFacing, out string reason, bool horizonLimited = false,
        bool improveCoverage = true, bool improveTargetChoice = false)
    {
        reason = "Incomplete RSR action opportunity";
        if (evidence.ActionId == 0 || !float.IsFinite(latestCompletion) || latestCompletion < evidence.ReadyIn + evidence.CastTime
            || latestCompletion > MovementPlanningContext.MaximumHorizonSeconds)
            return null;
        var target = evidence.TargetId == player.Id ? player : eligible.FirstOrDefault(a => a.Id == evidence.TargetId);
        if (target.Id == 0 || target.Dead)
        {
            reason = "RSR target is no longer eligible in native hints";
            return null;
        }
        var shape = Shape(evidence, player.Id);
        if (shape == null)
        {
            reason = "Selected action geometry is unsupported; native goals remain in control";
            return null;
        }
        var required = evidence.AffectedRecipients.ToHashSet();
        if (shape != ExecutionShape.CasterCircle || evidence.TargetId != player.Id)
            required.Add(evidence.TargetId);
        if (required.Any(id => id != player.Id && !eligible.Any(a => a.Id == id && !a.Dead)))
        {
            reason = "An evidenced recipient is missing or no longer eligible";
            return null;
        }
        var anchor = evidence.GroundTargeted ? new WPos(evidence.TargetPoint.X, evidence.TargetPoint.Z) : target.Position;
        var height = shape switch
        {
            ExecutionShape.GroundCircle => evidence.TargetPoint.Y,
            ExecutionShape.TargetCircle => target.Height,
            _ => player.Height
        };
        // Target choice can improve target circles or directional aim. The
        // separate movement preference still prevents a target-only option
        // from creating a walk to discover additional recipients.
        var valueExtraRecipients = improveCoverage || improveTargetChoice && !evidence.Friendly
            && !evidence.GroundTargeted && shape is ExecutionShape.TargetCircle or ExecutionShape.Cone or ExecutionShape.Rectangle;
        var recipients = eligible.Where(a => !a.Dead).Append(player).DistinctBy(a => a.Id)
            .Where(a => evidence.Friendly || a.Id != player.Id)
            .Select(a => new ExecutionRecipient(a.Id, a.Position, a.Radius, valueExtraRecipients || required.Contains(a.Id) ? 1 : 0,
                required.Contains(a.Id), a.Height - height)).ToArray();
        // Target range uses native hitbox semantics. Ground ranges refer to the selected
        // point, while a self-centered effect has no target-distance condition.
        var reach = evidence.Range + (evidence.GroundTargeted ? 0 : player.Radius + target.Radius);
        var opportunity = new ExecutionOpportunity(shape.Value, target.Id, anchor, reach, evidence.EffectRange, recipients,
            evidence.HalfWidth, shape == ExecutionShape.Cone ? 60f.Degrees() : default,
            target.Omnidirectional ? Positional.Any : positional, target.Rotation, tankFacing,
            evidence.Friendly ? [] : forbidden.Select(a => new ExecutionRecipient(a.Id, a.Position, a.Radius, 0, HeightOffset: a.Height - height)));
        reason = "Selected action and each evidenced recipient captured before navigation";
        return new(evidence.ActionId, target.Id, evidence.ReadyIn, latestCompletion, evidence.CastTime,
            evidence.AnimationLock, 0, [], [], Opportunity: opportunity, HorizonLimited: horizonLimited,
            UsesHardTarget: !evidence.Friendly && !evidence.GroundTargeted && evidence.Range > 0);
    }

    internal static ExecutionShape? Shape(RsrActionEvidence action, ulong playerId)
    {
        if (action.GroundTargeted)
            return action.CastType is 1 or 2 && action.EffectRange > 0 ? ExecutionShape.GroundCircle : null;
        if (action.CastType == 1 && action.EffectRange == 0)
            return ExecutionShape.SingleTarget;
        if (action.CastType == 3 && action.EffectRange > 0)
            return ExecutionShape.Cone;
        if ((action.CastType == 4 || action.CastType == 1 && !action.Friendly && action.HalfWidth > 0)
            && action.HalfWidth > 0 && action.EffectRange > 0)
            return ExecutionShape.Rectangle;
        if (action.CastType is 1 or 2 && action.EffectRange > 0)
            return action.TargetId == playerId || action.Range == 0 ? ExecutionShape.CasterCircle : ExecutionShape.TargetCircle;
        return null;
    }
}
