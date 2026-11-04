using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.Targeting;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void IdleNavigation()
    {
        var player = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 1;
        var hints = new AIHints();
        bool Idle(bool encounter = false, bool engaged = false, MovementIntent? intent = null)
            => NativeMovementHost.CanSkipIdleNavigation(hints, player, encounter, engaged, intent ?? new());
        Check(Idle(), "An idle open-world player with no objective needs neither navigation capture nor a worker job.");
        Check(!Idle(encounter: true) && !Idle(engaged: true),
            "An active encounter or NPC/party combat immediately restores the full native planning path.");
        player.TargetID = 10;
        Check(Idle(), "Selecting an NPC or idle enemy is not a movement objective.");
        player.TargetID = 0;
        Check(!Idle(intent: new(ExplicitDestination: new WPos(1, 1))), "Explicit movement cannot be discarded as idle.");
        hints.GoalZones.Add(AIHints.GoalProximity(new WPos(1, 1), 1, 1));
        Check(!Idle(), "A leader-following or native rotation goal immediately restores planning.");
        hints.GoalZones.Clear();
        hints.ForbiddenZones.Add((new SDCircle(default, 1), DateTime.UtcNow.AddSeconds(3), 0));
        Check(!Idle(), "Even a future warning restores full fresh native safety capture immediately.");
        hints.ForbiddenZones.Clear();
        hints.TemporaryObstacles.Add(new SDCircle(default, 1));
        Check(Idle(), "A wall or doorway without a movement objective cannot cause autonomous clearance movement.");
        Check(!Idle(engaged: true), "Physical constraints remain binding once combat requires navigation.");
        hints.TemporaryObstacles.Clear();
        hints.PathfindMapBounds = new ArenaBoundsSquare(30);
        Check(Idle(), "Inactive module geometry alone cannot steer an out-of-combat player away from a door.");
        Check(!Idle(encounter: true), "Active arena bounds still require full native navigation.");
        hints.PathfindMapBounds = AIHints.DefaultBounds;
        hints.ForcedMovement = new System.Numerics.Vector3(1, 0, 0);
        Check(!Idle(), "Forced movement retains native execution and timing authority.");
        var combat = new MovementCombatIntent([], 0, true);
        Check(!NativeMovementHost.RequestsCombatOptimization(combat, new(), false, false),
            "An enabled assistance setting cannot authorize comfort movement or facing outside combat and active encounters.");
        Check(NativeMovementHost.RequestsCombatOptimization(combat, new(), true, false)
            && NativeMovementHost.RequestsCombatOptimization(combat, new(), false, true),
            "Party engagement and active encounter downtime each retain combat optimization.");
        var now = DateTime.UtcNow;
        bool Optimizes(MovementIntent intent, DateTime time)
            => NativeMovementHost.RequestsCombatOptimization(combat, intent, true, true,
                NativeMovementHost.NativeSteeringReason(hints, intent, combat, time) != null);

        hints.AddSpecialMode(AIHints.SpecialMode.Misdirection, now.AddSeconds(1));
        Check(Optimizes(new(), now),
            "A future arrow does not preempt ordinary combat planning before it starts.");
        Check(!Optimizes(new(), now.AddSeconds(1)),
            "Active misdirection uses native arrow steering instead of a free-steering combat plan.");
        Check(Optimizes(new(HandleMisdirection: false), now.AddSeconds(1)),
            "A preset that ignores special modes keeps its existing planning policy.");
        hints.Clear();
        Check(Optimizes(new(), now.AddSeconds(2)),
            "Clearing the arrow restores ordinary combat planning.");

        var circle = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        circle.InstanceID = 2;
        circle.IsTargetable = true;
        hints.InteractWithTarget = circle;
        Check(Optimizes(new(), now), "An ordinary interaction request does not reserve the encounter action window.");
        hints.PriorityInteraction = circle;
        Check(!Optimizes(new(), now) && !Optimizes(new(HandleMisdirection: false), now),
            "An eligible required interaction keeps elective mobility from claiming its action window.");
        circle.IsTargetable = false;
        Check(Optimizes(new(), now), "An unavailable circle releases the action window immediately.");
        circle.IsTargetable = true;
        hints.InteractWithTarget = player;
        Check(Optimizes(new(), now), "A replaced interaction target does not inherit the previous target's priority.");
        var frame = new OffensiveTargetFrame(new(1, default, 0, 0, default), 0, 0, false, true, true, 0, []);
        Check(!frame.NeedsActionConstraints && (frame with { Current = 10 }).NeedsActionConstraints
            && (frame with { Forced = 10 }).NeedsActionConstraints,
            "Only an empty idle target frame skips action-predicate wrapping; a target or assignment resumes it immediately.");
        Check((frame with { Actors = [new(new(10, default, 0, 0, default), 0, true)] }).NeedsActionConstraints,
            "A newly engaged permitted enemy cannot lose acquisition to the no-target optimization.");
    }
}
