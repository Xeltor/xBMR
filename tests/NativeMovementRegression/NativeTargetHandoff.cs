using BossMod;
using BossMod.CombatAssistance.Targeting;
using BossMod.CombatAssistance.Planning;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void NativeTargetHandoff()
    {
        Check(!BossMod.CombatAssistance.Integrations.RsrOffensiveEligibility.PreservesPriority(true, 1, false, 0)
            && !BossMod.CombatAssistance.Integrations.RsrOffensiveEligibility.PreservesPriority(true, 1, true, 2)
            && BossMod.CombatAssistance.Integrations.RsrOffensiveEligibility.PreservesPriority(true, 1, true, 1)
            && BossMod.CombatAssistance.Integrations.RsrOffensiveEligibility.PreservesPriority(false, int.MaxValue, false, int.MaxValue),
            "More area hits cannot override the selected RSR marked-target priority or icon order.");
        var owner = new NativeTargetSetupOwner();
        var setup = new NativeTargetSetup(2, 3, 100, [2, 3, 4], 100, 200);
        owner.Begin(setup);
        Check(owner.Observe(3, 100, 3, [2, 3, 4], 99, 150, true) == TargetSetupProgress.Pending,
            "An old cached action cannot confirm a new target setup.");
        Check(owner.Observe(3, 100, 3, [2, 3], 110, 150, true) == TargetSetupProgress.Pending,
            "A target handoff independently preserves every promised action recipient.");
        Check(owner.Observe(3, 101, 3, [2, 3, 4], 110, 150, true) == TargetSetupProgress.Pending,
            "A different selected action does not confirm the planned opportunity.");
        Check(owner.Observe(3, 100, 3, [2, 3, 4], 110, 150, true) == TargetSetupProgress.Confirmed
            && owner.Pending == null, "A fresh original selection confirms the exact target, action and complete recipient set.");
        owner.Begin(setup);
        Check(owner.Observe(2, 100, 2, [2, 3], 110, 180, true) == TargetSetupProgress.Pending
            && owner.Pending?.Deadline == 200, "Polling retains the original target setup deadline.");
        Check(owner.Observe(3, 100, 2, [2, 3], 190, 200, true) == TargetSetupProgress.Failed
            && owner.Pending == null, "The original deadline expires without renewing a failed handoff.");
        owner.Begin(setup);
        Check(owner.Observe(9, 100, 3, [2, 3, 4], 110, 150, true) == TargetSetupProgress.Foreign
            && owner.Pending == null, "A player or external target change releases native setup ownership.");
        owner.Begin(setup);
        Check(owner.Observe(3, 100, 3, [2, 3, 4], 110, 150, false) == TargetSetupProgress.Released,
            "Fresh action evidence cannot bypass a loss of execution authority.");
        owner.Begin(setup); owner.Release();
        Check(owner.Pending == null, "Lifecycle release retains no target ownership.");
        ExecutionRecipient[] recipients = [new(2, new(5, 0), .5f, 1, true),
            new(3, new(8, 0), 1, 1, true, 1), new(4, new(11, 0), .5f, 1, false, 1)];
        var opportunity = new ExecutionOpportunity(ExecutionShape.TargetCircle, 2, new(5, 0), 26, 3, recipients,
            forbiddenRecipients: [new(9, new(20, 0), .5f, 0, HeightOffset: 2)]);
        var choice = NativeOffensiveTargeting.Reanchor(opportunity, 3, new(8, 0), 1, -1, .05f, 1);
        Check(choice != null && choice.Preserves(opportunity) && choice.Opportunity.CastReach == 26.5f
            && choice.Opportunity.Recipients.All(r => r.Required),
            "A native alternative anchor preserves hitbox range, old recipients and every new hit.");
        Check(choice!.Opportunity.Recipients.Single(r => r.Id == 2).HeightOffset == -1
            && choice.Opportunity.ForbiddenRecipients.Single().HeightOffset == 1,
            "Target-circle reanchoring shifts all recipient and collateral heights coherently.");
        Check(!choice.Opportunity.ForbiddenRecipients.Any(r => choice.Opportunity.Covers(new(0, 0), r.Id)),
            "Forbidden collateral remains independently represented after changing the target anchor.");
    }
}
