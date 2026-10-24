using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;
using System.Numerics;

public static partial class Checks
{
    private static void RsrEvidence()
    {
        Check(RsrKnockbackSelection.ActionFor(Class.BLM) == 7559 && RsrKnockbackSelection.ActionFor(Class.SGE) == 7559
            && RsrKnockbackSelection.ActionFor(Class.DRG) == 7548 && RsrKnockbackSelection.ActionFor(Class.MCH) == 7548
            && RsrKnockbackSelection.ActionFor(Class.PLD) == 7548 && RsrKnockbackSelection.ActionFor(Class.BLU) == 7559
            && RsrKnockbackSelection.ActionFor(Class.None) == 0,
            "Native immunity proposals use the actual role action, including physical ranged, limited caster and unavailable jobs.");
        Check(RsrActionEvidenceSource.NamedPositional("None") == Positional.Any
            && RsrActionEvidenceSource.NamedPositional("Rear") == Positional.Rear
            && RsrActionEvidenceSource.NamedPositional("Flank") == Positional.Flank,
            "Selected-action positional names cannot be replaced by a stale event cache or mismatched enum ordinal.");
        ExecutionActor player = new(1, default, 0, 0.5f, default);
        ExecutionActor target = new(2, new(6, 0), 0, 1, default);
        var single = new RsrActionEvidence(10, 2, new(6, 0, 0), [], false, false, 1, 3, 0, 0, 1, 2.5f, 0.1f, 0, 0.6f);
        var action = RsrExecutionCapture.Create(single, player, [target], [], 2, Positional.Any, null, out _);
        Check(action?.Opportunity != null && !action.Opportunity.Evaluate(default).Allowed
            && action.Opportunity.Evaluate(new(2, 0)).Allowed, "RSR range uses native target and player hitboxes without a reflected movement destination");
        Check(action!.Opportunity!.Recipients.Single().Id == target.Id && action.Opportunity.Recipients.Single().Required,
            "the selected offensive target is an independent required recipient");
        Check(RsrExecutionCapture.Create(single, player, [], [], 2, Positional.Any, null, out _) == null,
            "an RSR target outside native eligible targets cannot acquire movement authority");
        Check(RsrExecutionCapture.Create(single, player, [target with { Dead = true }], [], 2, Positional.Any, null, out _) == null,
            "a dead selected target invalidates the current opportunity");

        var soul = single with { ActionId = 16506, TargetId = player.Id, Range = 0, Friendly = false,
            AffectedRecipients = [player.Id], ReadyIn = 0 };
        var selfBuff = RsrExecutionCapture.Create(soul, player, [target], [], 5, Positional.Rear,
            new(default, 10.Degrees()), out _);
        Check(selfBuff is { UsesHardTarget: false, Opportunity.Shape: ExecutionShape.Self }
            && selfBuff.Opportunity.Evaluate(new(20, 0)).Allowed
            && selfBuff.Opportunity.Evaluate(new(20, 0)).Value == 1
            && !selfBuff.Opportunity.Covers(default, target.Id),
            "Recorded Umbral Soul with RSR IsFriendly=false remains a self-only opportunity without a capture exception or origin pin.");
        var release = RsrExecutionCapture.Create(soul with { ActionId = 7389, Recast = 0 }, player, [target], [], 5,
            Positional.Any, null, out _);
        Check(release is { IsGCD: false, Opportunity.Shape: ExecutionShape.Self },
            "Inner Release uses the same native self-only geometry without requiring RSR's friendly selection category.");

        var allies = Enumerable.Range(1, 96).Select(i => new ExecutionActor((ulong)i, new(i % 2, 0), 0, 0.5f, default)).ToArray();
        var heal = single with { ActionId = 20, TargetId = 1, Friendly = true, Range = 0, EffectRange = 5,
            AffectedRecipients = allies.Select(a => a.Id).ToImmutableArray() };
        var healing = RsrExecutionCapture.Create(heal, player, allies, [], 2, Positional.Any, null, out _);
        Check(healing?.Opportunity?.Recipients.Count(r => r.Required) == 96 && healing.Opportunity.Evaluate(default).Allowed,
            "native RSR capture retains all evidenced healing recipients without the old mask limit");
        var partialHeal = heal with { AffectedRecipients = [1, 2] };
        var optedOut = RsrExecutionCapture.Create(partialHeal, player, allies, [], 2, Positional.Any, null, out _, improveCoverage: false);
        var improved = RsrExecutionCapture.Create(partialHeal, player, allies, [], 2, Positional.Any, null, out _);
        Check(optedOut?.Opportunity?.Evaluate(default).Value == 2 && improved?.Opportunity?.Evaluate(default).Value == 96,
            "coverage opt-out preserves selected recipients while removing extra-recipient optimization");
        Check(optedOut?.Opportunity?.Evaluate(new(20, 0)).Allowed == false,
            "coverage opt-out never permits loss of an evidenced healing recipient");
        Check(RsrExecutionCapture.Create(heal, player, allies[..95], [], 2, Positional.Any, null, out _) == null,
            "a missing healing recipient rejects the opportunity instead of silently shrinking its requirement");
        var targetedHeal = heal with { TargetId = 2, Range = 30, AffectedRecipients = [2] };
        Check(RsrExecutionCapture.Shape(targetedHeal, player.Id) == ExecutionShape.TargetCircle,
            "a friendly target-centered heal is not reinterpreted as a caster-centered effect");

        var cleave = single with { CastType = 3, EffectRange = 8, Range = 8, AffectedRecipients = [2] };
        ExecutionActor forbidden = new(99, new(4, 0), 0, 1, default);
        var forbiddenCleave = RsrExecutionCapture.Create(cleave, player, [target], [forbidden], 2, Positional.Any, null, out _);
        Check(forbiddenCleave?.Opportunity?.Evaluate(default).Allowed == false,
            "forbidden collateral is a hard rejection before combat scoring");
        var allowedCleave = RsrExecutionCapture.Create(cleave, player, [target], [forbidden with { Position = new(-4, 0) }], 2, Positional.Any, null, out _);
        Check(allowedCleave?.Opportunity?.Evaluate(default).Allowed == true,
            "a forbidden actor outside the executed cone does not suppress a legal action");
        Check(!allowedCleave!.Opportunity!.SameGeometry(forbiddenCleave!.Opportunity!),
            "changing forbidden-target geometry materially invalidates a previous plan");
        Check(RsrExecutionCapture.Shape(single with { CastType = 4, EffectRange = 8, HalfWidth = 0 }, 1) == null,
            "missing beam width is not replaced by an invented movement footprint");
        Check(RsrExecutionCapture.Shape(single with { CastType = 10, EffectRange = 8 }, 1) == null,
            "unsupported action geometry leaves native goals authoritative");
        Check(RsrExecutionCapture.Create(single, player, [target], [], 0.5f, Positional.Any, null, out _) == null,
            "an execution window ending before the evidenced ready time is not invented");

        var tracker = new RsrRotationSelectionTracker();
        var oldRotation = new object(); var nextRotation = new object(); var oldAction = new object();
        Check(tracker.Observe(oldRotation, oldAction) && !tracker.Observe(nextRotation, oldAction)
            && tracker.Observe(nextRotation, new object()), "rotation changes reject the old reflected selection until RSR publishes a fresh one");
    }
}
