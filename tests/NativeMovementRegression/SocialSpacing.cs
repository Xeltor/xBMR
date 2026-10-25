using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Config;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void SocialSpacing()
    {
        var now = DateTime.UtcNow;
        var identity = ((ushort)1, 20ul, Class.SGE);
        var policy = new NativeSocialSpacing();
        ImmutableArray<SocialSpacingPeer> overlap = [new(10, default)];
        SocialSpacingPreference? Observe(int ms, ImmutableArray<SocialSpacingPeer>? peers = null, WPos position = default,
            bool allowed = true, bool positioning = false, bool melee = false)
            => policy.Observe(identity, now.AddMilliseconds(ms), position, peers ?? overlap, allowed, positioning, melee);
        Check(Observe(0) == null && Observe(200) == null, "Brief overlaps never request optional unstacking.");
        var active = Observe(350);
        Check(active != null && active.Value(new(1, 0)) > 0 && active.Value(new(0.5f, 0)) == 0
            && active.Value(new(3, 0)) == 0, "Persistent overlap permits only a useful local adjustment, without microsteps or long detours.");
        var continued = Observe(450, position: new(0.1f, 0));
        Check(continued?.Origin == active?.Origin, "Walking towards personal space never pushes the preference origin ahead of the player.");
        Check(ReferenceEquals(active, Observe(475, [new(10, new(0, .01f))], position: new(.1f, 0))),
            "Small peer motion cannot rotate or re-score an already selected cosmetic preference.");
        Check(Observe(500, positioning: true) == null && Observe(800) == null && Observe(1001) != null,
            "Combat positioning and the following 500ms settling window suppress social movement.");
        Check(Observe(1100, allowed: false) == null && Observe(1200) == null,
            "Saved opt-out/manual handoff clears observations instead of retaining an immediately active preference.");
        Check(Observe(2000) == null, "Stale observations start a fresh overlap rather than reusing an old activation.");

        policy.Reset();
        Check(Observe(0, [new(10, new(2, 0))]) == null && Observe(100) == null && Observe(450) == null,
            "A player who already occupied a spot holds it when an equal-priority ally arrives.");
        foreach (var ms in new[] { 800, 1200, 1600, 2000 })
            Check(Observe(ms, [new(10, default, Priority: true, Flexible: false)]) == null,
                "An arriving caster, tank or melee priority never evicts the first occupant, even after yield timers expire.");
        Check(Observe(2100, [new(10, default, Priority: true)], position: new(0.4f, 0)) != null,
            "Leaving the original occupied spot relinquishes first-arrival protection.");
        policy.Reset();
        Observe(0, [new(10, new(2, 0))]);
        for (var i = 1; i <= 5; ++i) Observe(i * 100, [new(10, new(2, 0))], position: new(i * 0.2f, 0));
        Check(Observe(900, [new(10, new(2, 0))], position: new(1, 0)) != null,
            "Gradual movement into a stationary player's space cannot continually renew a first-arrival claim.");
        policy.Reset();
        Observe(0, [new(10, new(2, 0))]);
        Observe(100, [new(10, new(2, 0))], position: new(-1, 0));
        Observe(450, [new(10, new(2, 0))], position: new(-1, 0));
        Check(Observe(550, [new(10, new(-1, 0), Priority: true)], position: new(-1, 0)) == null,
            "Settling at a new unoccupied spot establishes first arrival there before another player comes over.");
        policy.Reset();
        Observe(0);
        Check(Observe(350, [new(10, new(0.6f, 0))]) == null,
            "An overlapping ally already moving away does not trigger a competing correction.");
        policy.Reset();
        Observe(0);
        Check(Observe(350, [new(10, default), new(11, new(0.5f, 0)), new(12, new(-0.5f, 0))]) == null,
            "A dense deliberate stack does not produce optional social dispersal.");
        policy.Reset();
        Observe(0);
        Check(Observe(350, [new(10, default), new(11, new(2.5f, 0)), new(12, new(-2.5f, 0))]) == null,
            "A crowded local group accepts overlap even when its other members are outside the old narrow destacking radius.");
        policy.Reset();
        Observe(0, melee: true);
        Check(Observe(350, melee: true) == null && Observe(700, melee: true) == null && Observe(1001, melee: true) != null,
            "In-range melee gives flexible allies a bounded first opportunity to yield.");
        Check(policy.Observe((2, 20, Class.SGE), now.AddMilliseconds(1100), default, overlap, true, false) == null
            && policy.Observe(identity, now, default, overlap, true, false) == null,
            "Zone changes and clock reversal discard previous social observations.");

        var priority = new SocialSpacingPreference(default, [new(10, new(0.5f, 0), Priority: true)], new(1, 0));
        Check(priority.Value(new(1.5f, 0)) == 0 && priority.Value(new(-1, 0)) > 0,
            "A cosmetic adjustment may leave priority space but cannot cross through its occupant.");
        var peer = SocialActor(10);
        var player = SocialActor(20);
        Check(NativeSocialSpacing.EligiblePeer(peer, player), "Nearby living PCs contribute social occupancy.");
        peer.Type = ActorType.Enemy;
        Check(!NativeSocialSpacing.EligiblePeer(peer, player), "Duty Support NPCs never trigger player destacking.");
        peer.Type = ActorType.Player; peer.IsDead = true;
        Check(!NativeSocialSpacing.EligiblePeer(peer, player), "Dead players do not trigger destacking.");
        peer.IsDead = false; peer.PosRot.Y = 4;
        Check(!NativeSocialSpacing.EligiblePeer(peer, player), "Players on another floor do not trigger destacking.");

        // Test the actual native search with the local preference, not just the
        // gating policy: action delivery and a native assignment still win.
        var map = Grid(5, 1);
        var origin = Point(map, 2);
        var local = new SocialSpacingPreference(origin, [new(10, origin)], new(1, 0));
        var presentation = Enumerable.Range(0, 5).Select(i => local.Value(Point(map, i))).ToArray();
        var action = Action(map, 4, 0, new ulong[5], [0, 0, 1, 0, 0]);
        var context = new MovementPlanningContext(Context(map, 2).Identity, map, 0, 0,
            Enumerable.Repeat(true, 5).ToArray(), presentation, [action], []);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.ExecutionValues[0] == 1 && plan.DeliveryTimes[0] == 0,
            "Personal space cannot give up or delay an immediately deliverable action.");
        map.PixelPriority[2] = 1;
        context = new(context.Identity, map, 0, 0, Enumerable.Repeat(true, 5).ToArray(), presentation, [], []);
        Check(JointMovementSearch.Search(context, Exhaustive).UsefulDestination == origin,
            "Personal space cannot displace a native assignment.");
        SocialSpacingCapture();
    }

    private static void SocialSpacingCapture()
    {
        var now = DateTime.UtcNow;
        bool Captures(Action<Actor, AIHints, BossMod.Pathfinding.Map, AssistancePreferences>? configure = null)
        {
            var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
            typeof(WorldState).GetField(nameof(WorldState.Actors))!.SetValue(world, new ActorState());
            var player = SocialActor(20);
            player.InCombat = true;
            world.Actors.Actors.Add(20, player);
            world.Actors.Actors.Add(10, SocialActor(10));
            var hints = new AIHints();
            var settings = new AssistancePreferences { ManageSocialSpacing = true };
            var map = Grid(5, 1);
            configure?.Invoke(player, hints, map, settings);
            var spacing = new NativeSocialSpacing();
            world.Frame.Timestamp = now;
            spacing.Capture(map, hints, world, player, settings, true);
            world.Frame.Timestamp = now.AddMilliseconds(350);
            return spacing.Capture(map, hints, world, player, settings, true) != null;
        }
        Check(Captures(), "Framework capture enables persistent optional PC destacking in ordinary combat.");
        Check(!Captures((p, _, _, _) => p.CastInfo = new() { TotalTime = 2 }),
            "Framework capture protects an actual current cast from social movement.");
        Check(!Captures((p, h, _, _) =>
        {
            p.Class = Class.PLD;
            var enemy = SocialActor(99);
            enemy.Type = ActorType.Enemy; enemy.Class = Class.None; enemy.InCombat = true; enemy.TargetID = p.InstanceID;
            h.PotentialTargets.Add(new(enemy, 1, true));
        }), "Framework capture preserves an established tank holding an enemy.");
        Check(!Captures((p, _, _, s) => { p.Class = Class.BLM; s.BlackMageLateMovement = true; }),
            "Black Mage's configured casting-position preference suppresses cosmetic destacking.");
        Check(!Captures((_, _, _, s) => s.ManageSocialSpacing = false), "Framework capture respects the saved social opt-out.");
        Check(!Captures((_, _, m, _) => m.PixelPriority[0] = 1), "Native positioning recovery defers cosmetic movement.");
        Check(!Captures((_, h, _, _) => h.ForcedMovement = new(1, 0, 0)), "Forced mechanic movement defers cosmetic movement.");
    }

    private static Actor SocialActor(ulong id)
    {
        // Actor's real constructor queries live game sheets. This fixture only
        // supplies the frozen native fields actually consumed by social capture.
        var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = id; actor.Type = ActorType.Player; actor.Class = Class.SGE;
        actor.PendingKnockbacks = [];
        return actor;
    }
}
