using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void MeleeRecoveryDelivery()
    {
        var clock = new NativeMeleeRecoveryWindow();
        var now = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
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
