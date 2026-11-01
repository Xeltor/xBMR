using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void MeleeRecoveryDelivery()
    {
        var clock = new NativeMeleeRecoveryWindow();
        var now = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        var tick = System.Diagnostics.Stopwatch.GetTimestamp();
        var halfSecond = System.Diagnostics.Stopwatch.Frequency / 2;
        Check(clock.AwaitPlanning(1, 10, 16143, tick)
            && clock.AwaitPlanning(1, 10, 16143, tick + 1)
            && !clock.AwaitPlanning(1, 10, 16143, tick + halfSecond)
            && !clock.AwaitPlanning(1, 10, 16143, tick + halfSecond + 1),
            "A new engaged target gets its first mobility assessment before filler, without renewing the bound on later callbacks.");
        Check(clock.AwaitPlanning(1, 11, 16143, tick)
            && !clock.AwaitPlanning(1, 11, 16143, tick + halfSecond),
            "The native freshness ceiling releases filler even when the owner's world clock stalls.");
        clock.PlanningObserved(1, 12, 16143);
        Check(!clock.AwaitPlanning(1, 12, 16143, tick),
            "An assessed target cannot restart the initial hold because its route is unavailable or invalidated.");
        clock.Reset();
        Check(clock.AwaitPlanning(1, 12, 16143, tick),
            "Releasing the movement lifecycle clears the old target observation.");
        foreach (var (job, action) in new[] { (Class.PLD, 24u), (Class.WAR, 46u), (Class.DRK, 3624u),
            (Class.GNB, 16143u), (Class.DRG, 90u), (Class.NIN, 2247u), (Class.SAM, 7486u), (Class.RPR, 24386u), (Class.VPR, 34632u) })
            Check(MeleeRecoveryPreference.IsFallback(job, action), "Tank and melee filler share the same melee recovery policy.");
        Check(!MeleeRecoveryPreference.IsFallback(Class.PLD, (uint)BossMod.PLD.AID.HolySpirit)
            && !MeleeRecoveryPreference.IsFallback(Class.VPR, (uint)BossMod.VPR.AID.UncoiledFury)
            && !MeleeRecoveryPreference.IsFallback(Class.BRD, 90),
            "Real ranged damage opportunities and ranged jobs are not demoted to melee throwaway filler.");
        Check(clock.Defer(1, 10, 90, now, now, 0, 2.112f, .8f),
            "A verified dash restoring melee before the ranged filler's recovery preserves the present GCD for melee.");
        Check(clock.Defer(1, 10, 90, now, now.AddSeconds(1), 0, 2.112f, .5f)
            && !clock.Defer(1, 10, 90, now, now.AddSeconds(2.2), 0, 2.112f, .1f),
            "Equivalent frames cannot renew a melee recovery reservation after its original observed GCD deadline.");
        Check(!clock.Defer(1, 11, 90, now, now, 0, 2.112f, 3)
            && clock.Defer(1, 11, 90, now, now, .2f, 2.112f, .5f),
            "A filler that recovers before melee access remains available; a fresh target requires its own verified opportunity.");
        Check(clock.Defer(1, 10, 90, now, now.AddSeconds(2.2), 0, 2.112f, .1f, problem: 1),
            "A materially changed and freshly revalidated problem may select a new recovery window; rereading equivalent frames cannot do so.");
        var map = Grid(9, 1);
        var identity = Context(map, 0).Identity;
        var target = Point(map, 8);
        var input = new MovementPlanningContext(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], [], [], 1,
            targetId: 10, mobilityMechanics: [new(36951, MobilityMechanic.Target, 20, 0, 0, 0, .8f, 0, [new(10, target, 0)])],
            range: new(10, target, 0, true));
        var plan = JointMovementSearch.Search(input, Exhaustive);
        using (var service = new MovementPlanningService((_, _) => plan, () => identity.CapturedTimestamp))
        using (var host = new NativeMovementHost(service))
        {
            service.Submit("normal", 1, input);
            Check(System.Threading.SpinWait.SpinUntil(() => !service.Busy, 1000) && host.OpportunityPending,
                "A finished worker result still precedes the owner's mobility assessment until it has been consumed.");
            var current = typeof(NativeMovementHost).GetProperty(nameof(NativeMovementHost.CurrentPlan),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            current.SetValue(host, plan with { ApproachPending = true });
            Check(host.OpportunityPending,
                "An early complete walking incumbent does not claim that gap-closer comparison has finished.");
            current.SetValue(host, plan);
            Check(!host.OpportunityPending,
                "An assessed complete approach releases the initial filler hold even during later optimization.");
            service.Poll("normal", 1, identity.WorldTime);
            Check(!service.HasUnconsumedWork, "Consuming a completed equivalent result ends its pending assessment.");
        }
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(plan, input, 0, 10, identity.CapturedTimestamp, identity.Origin) is > 0 and < 1
            && NativeMeleeRecoveryWindow.VerifiedArrival(plan, input, 0, 11, identity.CapturedTimestamp, identity.Origin) == null,
            "Only the complete exact native route to the same melee target can defer automatic filler.");
        map.PixelMaxG[8] = .2f;
        var dangerous = new MovementPlanningContext(identity, map, 0, 0, Enumerable.Repeat(true, 9).ToArray(), new float[9], [], [], 1,
            targetId: 10, mobilityMechanics: input.MobilityMechanics, range: input.Range);
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(plan, dangerous, 0, 10, identity.CapturedTimestamp, identity.Origin) == null,
            "A changed native hazard releases fallback instead of retaining an unsafe or unverified melee reservation.");
        var walkMap = Grid(4, 1);
        var walkIdentity = Context(walkMap, 0).Identity;
        var walkTarget = Point(walkMap, 3);
        MovementPlanningContext Walking(BossMod.Pathfinding.NavigationIdentity pose)
            => new(pose, walkMap, 0, 0, [true, true, true, true], [0, 0, 0, 0], [], [], 0,
                targetId: 10, range: new(10, walkTarget, 0, true));
        var oldWalk = JointMovementSearch.Search(Walking(walkIdentity), Exhaustive);
        var contact = new MovementPlanningContext(walkIdentity, walkMap, 0, 0, [true, true, true, true], [0, 0, 0, 0], [], [], 0,
            targetId: 10, range: new(10, walkTarget, 2, true));
        var contactProof = MovementPlanValidation.Validate(oldWalk, contact, 0, true, walkIdentity.CapturedTimestamp);
        var contactArrival = NativeMeleeRecoveryWindow.VerifiedArrival(oldWalk, contact, 0, 10, walkIdentity.CapturedTimestamp, walkIdentity.Origin);
        Check(contactProof.Allowed && contactProof.CompletionSeconds is > 2.9f and < 3.1f
            && contactArrival is > .9f and < 1.1f
            && new NativeMeleeRecoveryWindow().Defer(1, 10, 16143, now, now, 0, 2.112f, contactArrival.Value),
            "Melee access at the hitbox edge wins over filler even when the verified route continues to a later final position.");
        var gcdWait = new MovementPlanningContext(walkIdentity, walkMap, 0, 0, [true, true, true, true], [0, 0, 0, 0], [], [], 0,
            targetId: 10, range: new(10, walkTarget, 2, true, RecoveryReadyIn: 2));
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(oldWalk, gcdWait, 0, 10, walkIdentity.CapturedTimestamp, walkIdentity.Origin) is > 1.9f and < 2.1f,
            "Entering melee range cannot claim an attack before the observed GCD clock allows it.");
        var staleTick = walkIdentity.CapturedTimestamp + System.Diagnostics.Stopwatch.Frequency;
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(oldWalk, contact, 0, 10, staleTick, walkIdentity.Origin) == null,
            "Earlier hitbox entry never permits a stale safety snapshot to reserve the GCD.");
        walkMap.PixelMaxG[3] = 2;
        var blockedOnward = new MovementPlanningContext(walkIdentity, walkMap, 0, 0, [true, true, true, true], [0, 0, 0, 0], [], [], 0,
            targetId: 10, range: new(10, walkTarget, 2, true));
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(oldWalk, blockedOnward, 0, 10, walkIdentity.CapturedTimestamp, walkIdentity.Origin) == null,
            "Safe early contact cannot reserve melee when the complete onward route hits a later hazard.");
        walkMap.PixelMaxG[3] = float.MaxValue;
        var delayed = walkIdentity with { WorldTime = walkIdentity.WorldTime.AddSeconds(2.8),
            CapturedTimestamp = walkIdentity.CapturedTimestamp + (long)(2.8 * System.Diagnostics.Stopwatch.Frequency) };
        var remaining = NativeMeleeRecoveryWindow.VerifiedArrival(oldWalk, Walking(delayed), 0, 10, delayed.CapturedTimestamp, delayed.Origin);
        Check(remaining is > 2.9f and < 3.1f && !new NativeMeleeRecoveryWindow().Defer(1, 10, 90, now, now, 0, 2.112f, remaining.Value),
            "A delayed walk uses its freshly revalidated remaining travel time, not an old plan's nearly elapsed duration.");
        MovementPlan Recovery(bool complete, bool relaxed)
        {
            var decision = new BossMod.Pathfinding.NavigationDecision { Destination = walkTarget, TimeToGoal = 3 };
            decision.Publication = new(walkIdentity, null, [walkIdentity.Origin, walkTarget], complete, relaxed,
                BossMod.Pathfinding.ThetaStar.Score.SafeMaxPrio, decision, false);
            return oldWalk with { Status = MovementSearchStatus.NativeRecovery, Stages = [], NativeRecovery = decision };
        }
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(Recovery(true, false), Walking(walkIdentity), 0, 10,
            walkIdentity.CapturedTimestamp, walkIdentity.Origin) is > 2.9f and < 3.1f,
            "A complete strict native recovery witness supplies the same verified remaining arrival as enhanced walking.");
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(Recovery(false, false), Walking(walkIdentity), 0, 10,
                walkIdentity.CapturedTimestamp, walkIdentity.Origin) == null
,
            "An incomplete native recovery never reserves the GCD on an elective melee safety claim.");
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(Recovery(true, true), Walking(walkIdentity), 0, 10,
            walkIdentity.CapturedTimestamp, walkIdentity.Origin) is > 2.9f and < 3.1f,
            "A relaxed-source witness may be used only after its complete exact route is independently re-proved on the current strict map.");
        walkMap.PixelMaxG[1] = -1;
        Check(NativeMeleeRecoveryWindow.VerifiedArrival(Recovery(true, true), Walking(walkIdentity), 0, 10,
            walkIdentity.CapturedTimestamp, walkIdentity.Origin) == null,
            "Relaxed-source provenance never overrides a blocked strict route or weakens native safety evidence.");
    }
}
