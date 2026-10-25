using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.Targeting;

public static partial class Checks
{
    private static void FateArrivalAcquisition()
    {
        var player = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = 10;
        actor.FateID = 1965;
        actor.IsTargetable = true;
        actor.HPMP = new(100, 100, 0, 0, 0);
        actor.PendingHPDifferences = [];
        var decision = new RsrTargetDecision(RsrTargetPermission.Allowed, 1);
        var enemy = RsrTargetRules.CreateEnemy(actor, AIHints.Enemy.PriorityUndesirable, false, decision, true);
        var hints = new AIHints();
        hints.PotentialTargets.Add(enemy);
        RsrTargetRules.ApplyPriorities(hints);
        hints.Normalize();
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(new ActorState()));
        bool Arrival(uint fate = 1965, bool synced = true, byte progress = 0)
            => FateTargetPolicy.HasAcquisition(hints, fate, synced, progress);
        Check(!NativeCombatEngagement.Active(world, hints, player) && Arrival(),
            "An allowed current FATE can bootstrap targeting on arrival before either player or party enters combat.");
        var frame = new OffensiveTargetFrame(new(1, default, 0, .5f, default), 0, 0, false, true, false, 0,
            [new(new(10, new(3, 0), 0, .5f, default), enemy.Priority, true, RsrPreferred: true)]);
        Check(frame.PriorityAcquisition(frame.Acquisition) == 10 && frame.MissingTargetAcquisition(false) == 10,
            "The permitted arrival reaches native missing-target acquisition without requiring an existing RSR action.");
        Check(!Arrival(0) && !Arrival(1966) && !Arrival(synced: false) && !Arrival(progress: 100),
            "Missing, unrelated, unsynced and completed FATEs do not grant arrival targeting authority.");
        foreach (var permission in new RsrTargetDecision?[] { null, new(RsrTargetPermission.Unavailable, 1),
            new(RsrTargetPermission.Excluded, 1), new(RsrTargetPermission.Allowed, 0) })
        {
            enemy.RsrTargetDecision = permission;
            Check(!Arrival(), "Arrival requires delegated RSR permission and an enabled specific preference.");
        }
        enemy.RsrTargetDecision = decision;
        foreach (var priority in new[] { AIHints.Enemy.PriorityForbidden, AIHints.Enemy.PriorityInvincible,
            AIHints.Enemy.PriorityPointless, AIHints.Enemy.PriorityUndesirable })
        {
            enemy.Priority = priority;
            Check(!Arrival(), "Arrival cannot override native forbidden, invincible, dying or passive priority.");
        }
        enemy.Priority = 0;
        actor.IsDead = true;
        Check(!Arrival(), "A dead FATE enemy cannot grant arrival authority.");
        actor.IsDead = false;
        actor.HPMP.CurHP = 1;
        Check(!Arrival(), "A dying FATE enemy cannot grant arrival authority.");
        actor.HPMP.CurHP = 100;
        actor.IsDestroyed = true;
        Check(!Arrival(), "A destroyed FATE enemy cannot grant arrival authority.");
        actor.IsDestroyed = false;
        actor.IsTargetable = false;
        Check(!Arrival(), "An untargetable FATE enemy cannot grant arrival authority.");
        Check((frame with { RetainSelection = true }).PriorityAcquisition(10) == 0
            && (frame with { Acquire = false }).MissingTargetAcquisition(false) == 0
            && (frame with { Forced = 2 }).MissingTargetAcquisition(false) == 0,
            "Arrival permission preserves manual control, saved acquisition opt-outs and authored target ownership.");
    }
}
