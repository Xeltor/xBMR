using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;

namespace BossMod.CombatAssistance.Targeting;

internal sealed record OffensiveTargetActor(ExecutionActor Actor, int Priority, bool Engaged,
    bool ForbidDots = false, bool ForbiddenCollateral = false);
internal sealed record OffensiveActionGeometry(uint Id, int CastType, float Range, float EffectRange,
    float HalfWidth, bool Ground, bool Friendly, bool ProvidesTargetStatus);

// One immutable selection input. Encounter priority, engagement, selected target
// and each current recipient remain independent constraints, never one score.
internal sealed record OffensiveTargetFrame(ExecutionActor Player, ulong Current, ulong Forced,
    bool RetainSelection, bool Acquire, bool ImproveAoe, ulong RecoveryTarget,
    ImmutableArray<OffensiveTargetActor> Actors, uint CurrentAction = 0,
    ImmutableArray<ulong> RequiredRecipients = default, ulong SelectedTarget = 0)
{
    internal bool Eligible(ulong id) => id != 0 && Actors.Any(a => a.Actor.Id == id && Valid(a.Actor)
        && !a.Actor.Dead && a.Engaged && a.Priority >= 0);
    private static bool Valid(ExecutionActor actor) => actor.Id != 0 && float.IsFinite(actor.Position.X)
        && float.IsFinite(actor.Position.Z) && float.IsFinite(actor.Height) && float.IsFinite(actor.Radius)
        && actor.Radius >= 0;
    // Same three-dimensional acquisition envelope as native Plugin.SetTarget.
    // Retaining a present target is independent of acquiring a new one.
    private bool CanSelect(ExecutionActor actor) => actor.Id == Current || (actor.Position - Player.Position).LengthSq()
        + (actor.Height - Player.Height) * (actor.Height - Player.Height) < 2500;
    private int Priority => Actors.Where(a => !a.Actor.Dead && a.Engaged && a.Priority >= 0).Select(a => a.Priority).DefaultIfEmpty(-1).Max();
    internal ulong Acquisition => Actors.Where(a => Eligible(a.Actor.Id) && CanSelect(a.Actor) && a.Priority == Priority)
        .OrderByDescending(a => a.Actor.Id == RecoveryTarget)
        .ThenByDescending(a => a.Actor.Id == SelectedTarget)
        .ThenBy(a => (a.Actor.Position - Player.Position).LengthSq())
        .ThenBy(a => a.Actor.Id).Select(a => a.Actor.Id).FirstOrDefault();

    // Acquiring a hard target is not an action approval. Henched RSR needs the
    // hard target before it can produce any action/recipient evidence at all.
    // Never replace a usable player target or an authored/manual instruction here.
    internal ulong MissingTargetAcquisition(bool currentTargetUsable)
        => !currentTargetUsable && Acquire && !RetainSelection && Forced == 0 && Valid(Player)
            ? Acquisition : 0;

    // Constraints shared by original RSR selection and publication. This is not
    // a replacement rotation: single-target status/resource ordering stays in RSR.
    internal bool AllowsNative(OffensiveActionGeometry action, ulong candidate)
    {
        if (candidate == Player.Id) return (action.Range == 0 || action.Friendly) && AllowsArea(action);
        var actor = Actors.FirstOrDefault(a => a.Actor.Id == candidate);
        var manualUndesirable = actor?.Priority == AIHints.Enemy.PriorityUndesirable && candidate == Current;
        if (actor == null || !Valid(actor.Actor) || actor.Actor.Dead || !actor.Engaged && !manualUndesirable
            || actor.Priority is AIHints.Enemy.PriorityForbidden or AIHints.Enemy.PriorityInvincible
            || !CanSelect(actor.Actor)
            || action.ProvidesTargetStatus && actor.ForbidDots) return false;
        if (actor.Priority < Priority && Forced != candidate && !manualUndesirable
            || actor.Priority == AIHints.Enemy.PriorityPointless && candidate != Current && candidate != Forced
            || Forced != 0 && candidate != Forced || RetainSelection && candidate != Current) return false;
        if (!SafeCollateral(action, candidate, preserveRecipients: false)) return false;
        if (!action.Friendly && !action.Ground && action.CastType == 2 && action.Range > 0 && action.EffectRange > 0
            && (ImproveAoe || RecoveryTarget != 0) && Eligible(Current))
            return Allows(action, candidate);
        return AllowsArea(action);
    }

    internal bool Allows(OffensiveActionGeometry action, ulong candidate)
    {
        var chosen = Actors.FirstOrDefault(a => a.Actor.Id == candidate);
        if (!Valid(Player) || chosen == null || !Eligible(candidate) || !CanSelect(chosen.Actor) || chosen.Priority < Priority && Forced == 0
            || action.ProvidesTargetStatus && chosen.ForbidDots) return false;
        if (Forced != 0 && candidate != Forced || RetainSelection && candidate != Current) return false;
        if (!SafeCollateral(action, candidate)) return false;
        if (candidate == Current || candidate == Forced) return true;
        // A partial external recipient set is not evidence of an improvement.
        if (RequiredRecipients.IsDefault && (CurrentAction == 0 || CurrentAction == action.Id)) return false;
        if (!Eligible(Current)) return !RetainSelection && Acquire && candidate == (Forced != 0 ? Forced : Acquisition);
        if (action.Friendly || action.Ground || action.Range <= 0 || action.EffectRange <= 0
            || !ImproveAoe && RecoveryTarget == 0) return false;
        var old = Opportunity(action, Current);
        var replacement = Opportunity(action, candidate);
        if (old == null || replacement == null) return false;
        var baseline = old.Evaluate(Player.Position);
        var alternative = replacement.Evaluate(Player.Position);
        if (!baseline.Allowed || !alternative.Allowed || alternative.Value < baseline.Value) return false;
        // Only a strict execution gain can change an otherwise equivalent target.
        // Confirmed loose-enemy recovery can break a coverage tie, but cannot drop
        // the current target or any independently required recipient to do so.
        return ImproveAoe && alternative.Value > baseline.Value
            || RecoveryTarget != 0 && replacement.Covers(Player.Position, RecoveryTarget)
                && !old.Covers(Player.Position, RecoveryTarget);
    }

    internal bool AllowsArea(OffensiveActionGeometry action)
    {
        var restricted = Actors.Where(a => a.ForbiddenCollateral || action.ProvidesTargetStatus && a.ForbidDots);
        if (action.Ground)
            // RSR chooses the ground point after ActionCheck. Do not approve a
            // whole set of possible centers based on its old cached point.
            return restricted.All(a => (a.Actor.Position - Player.Position).Length() > action.Range + action.EffectRange + a.Actor.Radius);
        if (action.Range == 0 && action.EffectRange > 0 && action.CastType is not (1 or 2))
            return restricted.All(a => (a.Actor.Position - Player.Position).Length() > action.EffectRange + a.Actor.Radius);
        if (action.Range == 0 && action.EffectRange > 0 && action.CastType is 1 or 2)
            return restricted.All(a => (a.Actor.Position - Player.Position).LengthSq()
                + (a.Actor.Height - Player.Height) * (a.Actor.Height - Player.Height)
                > (action.EffectRange + a.Actor.Radius) * (action.EffectRange + a.Actor.Radius));
        return true;
    }

    private bool SafeCollateral(OffensiveActionGeometry action, ulong candidate, bool preserveRecipients = true)
    {
        if (action.Ground || action.Range == 0) return AllowsArea(action);
        if (action.EffectRange == 0)
            return Actors.Any(a => a.Actor.Id == candidate && a.Priority != AIHints.Enemy.PriorityForbidden
                && (!action.ProvidesTargetStatus || !a.ForbidDots));
        if (Actors.Any(a => a.Actor.Id == candidate && a.ForbiddenCollateral)) return false;
        // Directional selection does not prove an executable turn. When collateral
        // matters, require the entire possible facing envelope to be clear.
        if (action.CastType is 3 or 4 || action.CastType == 1 && action.HalfWidth > 0)
            return Actors.Where(a => a.ForbiddenCollateral || action.ProvidesTargetStatus && a.ForbidDots)
                .All(a => (a.Actor.Position - Player.Position).Length() > action.EffectRange + a.Actor.Radius);
        if (!preserveRecipients && !Actors.Any(a => a.ForbiddenCollateral || action.ProvidesTargetStatus && a.ForbidDots))
            return true; // RSR retains exact native geometry for unsupported shapes.
        return Opportunity(action, candidate, preserveRecipients)?.Evaluate(Player.Position).Allowed == true;
    }

    private ExecutionOpportunity? Opportunity(OffensiveActionGeometry action, ulong targetId, bool preserveRecipients = true)
    {
        var target = Actors.FirstOrDefault(a => a.Actor.Id == targetId);
        if (target == null) return null;
        var evidence = new RsrActionEvidence(action.Id, targetId, default, [], action.Friendly, action.Ground,
            action.CastType, action.Range, action.EffectRange, action.HalfWidth, 0, 1, 0, 0, 0);
        var shape = RsrExecutionCapture.Shape(evidence, Player.Id);
        if (shape == null || shape is ExecutionShape.CasterCircle or ExecutionShape.GroundCircle) return null;
        var required = preserveRecipients && (Forced == 0 || Forced == Current) && CurrentAction == action.Id && !RequiredRecipients.IsDefault
            ? RequiredRecipients.ToHashSet() : [];
        if (preserveRecipients && (Forced == 0 || Forced == Current) && Eligible(Current)) required.Add(Current);
        var height = shape == ExecutionShape.TargetCircle ? target.Actor.Height : Player.Height;
        var recipients = Actors.Where(a => (Eligible(a.Actor.Id) || a.Actor.Id == targetId
                && a.Priority == AIHints.Enemy.PriorityPointless && Valid(a.Actor) && !a.Actor.Dead && a.Engaged) && !a.ForbiddenCollateral)
            .Select(a => new ExecutionRecipient(a.Actor.Id, a.Actor.Position, a.Actor.Radius, 1,
                required.Contains(a.Actor.Id), a.Actor.Height - height)).ToArray();
        if (required.Any(id => !recipients.Any(a => a.Id == id))) return null;
        return new(shape.Value, targetId, target.Actor.Position, action.Range + Player.Radius + target.Actor.Radius,
            action.EffectRange, recipients, action.HalfWidth, shape == ExecutionShape.Cone ? 60.Degrees() : default,
            forbiddenRecipients: Actors.Where(a => a.ForbiddenCollateral || action.ProvidesTargetStatus && a.ForbidDots)
                .Select(a => new ExecutionRecipient(a.Actor.Id, a.Actor.Position, a.Actor.Radius, 0, HeightOffset: a.Actor.Height - height)));
    }
}
