using System.Numerics;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private sealed class GatheringTestStacks(BossModule module) : UniformStackSpread(module, 6, 5, 2, 4);
    private sealed class GatheringTestBaits(BossModule module) : GenericBaitStack(module);

    private static void StackGathering()
    {
        var (world, actor, module) = ProjectionFixture();
        var actors = (Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!;
        Actor Peer(int slot, WPos position)
        {
            var peer = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            peer.InstanceID = (ulong)(slot + 1); peer.PosRot = new(position.X, 0, position.Z, 0);
            world.Party.Members[slot] = new((ulong)(slot + 1), peer.InstanceID, false); actors[slot] = peer;
            return peer;
        }
        actor.PosRot = new(5.5f, 0, 0, 0);
        var carrier = Peer(1, default);
        var buddy = Peer(2, new(.5f, 0));
        var component = new GatheringTestStacks(module);
        component.Stacks.Add(new(carrier, 6, 2, 4, world.FutureTime(5)));
        AIHints Hints()
        {
            var hints = new AIHints();
            component.AddAIHints(0, actor, default, hints);
            return hints;
        }
        var hints = Hints();
        float Value(AIHints h, WPos p) => h.GoalZones.Sum(g => g(p));
        Check(Value(hints, new(1.5f, 0)) > Value(hints, new(5.5f, 0)), "stack participant prefers clear interior over technically valid outer edge");
        Check(Value(hints, default) == Value(hints, new(1.5f, 0)), "flat gathering interior avoids exact-centre micro-stepping");
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Distance(new(5.5f, 0)) > 0,
            "actual stack radius remains legal when hazards prevent a deeper gathering position");
        var stack = component.Stacks[0]; stack.ForbiddenPlayers.Set(0); component.Stacks[0] = stack;
        Check(Hints().GoalZones.Count == 0, "forbidden participant receives no stack attraction");
        stack.ForbiddenPlayers.Clear(0); stack.MaxSize = 2; component.Stacks[0] = stack;
        actor.PosRot.X = 7;
        Check(Hints().GoalZones.Count == 0, "full stack receives no extra participant attraction");
        stack.MaxSize = 4; component.Stacks[0] = stack;
        component.Spreads.Add(new(actor, 5, world.FutureTime(5)));
        Check(Hints().GoalZones.Count == 0, "spread carrier is not encouraged into somebody else's stack");
        component.Spreads.Clear(); component.Stacks.Clear();
        carrier.PosRot = new(-2, 0, 0, 0); buddy.PosRot = new(-1.5f, 0, 0, 0);
        Peer(3, new(7, 0));
        component.Stacks.Add(new(actor, 6, 2, 4, world.FutureTime(5)));
        hints = Hints();
        Check(Value(hints, new(-2, 0)) > Value(hints, new(7, 0)), "stack carrier gathers with the group rather than one isolated peer");
        Check(Value(hints, new(-2, 0)) > Value(hints, new(-4.5f, 0)), "stack carrier also prefers close contact over its previous allowed buddy edge");

        var line = new GatheringTestBaits(module);
        carrier.PosRot = new(0, 0, 0, 0); buddy.PosRot = new(.5f, 0, 0, 0);
        var source = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        source.PosRot = new(-10, 0, 0, 0);
        line.CurrentBaits.Add(new(source, carrier, new AOEShapeRect(20, 2), world.FutureTime(5)));
        var lineHints = new AIHints(); line.AddAIHints(0, actor, default, lineHints);
        Check(Value(lineHints, new(1, 0)) > Value(lineHints, new(5, 0)), "line stack joins eligible group instead of standing alone along the beam");
        var bait = line.CurrentBaits[0]; bait.Forbidden.Set(0); line.CurrentBaits[0] = bait;
        lineHints = new(); line.AddAIHints(0, actor, default, lineHints);
        Check(lineHints.GoalZones.Count == 0, "forbidden line-stack participant receives no gathering attraction");

        var map = Grid(9, 5); var origin = 2 * 9; var centre = Point(map, 4 + 2 * 9);
        var goalHints = new AIHints(); NativeStackGathering.AddGoal(goalHints, [centre], 1.5f);
        for (var i = 0; i < 45; ++i) map.PixelPriority[i] = Value(goalHints, Point(map, i));
        var gathered = JointMovementSearch.Search(Context(map, origin, speed: 6), Exhaustive);
        Check((gathered.UsefulDestination - centre).Length() <= 1.5f, "native search actually moves into the gathering interior");
        for (var i = 0; i < 45; ++i)
            if (map.PixelPriority[i] > 0) map.PixelMaxG[i] = -1;
        var outside = JointMovementSearch.Search(Context(map, origin, speed: 6), Exhaustive);
        Check(outside.HasVerifiedRoute && outside.NativePriority == 0, "unsafe gathering interior cannot outweigh physical or mechanic constraints");
        StackGatheringRecovery();
    }

    private static void StackGatheringRecovery()
    {
        var (world, actor, module) = ProjectionFixture();
        var actors = (Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!;
        Actor Peer(int slot, WPos position)
        {
            var peer = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            peer.InstanceID = (ulong)(slot + 1); peer.PosRot = new(position.X, 0, position.Z, 0);
            world.Party.Members[slot] = new((ulong)(slot + 1), peer.InstanceID, false); actors[slot] = peer;
            return peer;
        }
        // Penetrator Missile, Clyteum recording 2026-10-25 17:47:20.5.
        // Translate the recorded arena center to zero; the marked player stayed
        // here throughout the cast while each fresh route was deferred.
        actor.PosRot = new(8.6614f, 0, -6.21484f, 0);
        Peer(1, new(-3.7055f, 6.17926f));
        Peer(2, new(-.5947f, -6.52515f));
        Peer(3, new(12.8027f, -12.8424f));
        var stacks = new GatheringTestStacks(module);
        var boss = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        boss.IsTargetable = true; boss.HitboxRadius = 4;
        typeof(BossModule).GetField(nameof(BossModule.PrimaryActor))!.SetValue(module, boss);
        stacks.Stacks.Add(new(actor, 6, 4, 4, world.FutureTime(4.7f)));
        var hints = new AIHints(); stacks.AddAIHints(0, actor, default, hints);
        var identity = new NavigationIdentity(1, world.CurrentTime, DateTimeOffset.UtcNow,
            System.Diagnostics.Stopwatch.GetTimestamp(), actor.Position, false, 6, 0, null, actor.InstanceID, 1, 1);
        var input = new NavigationDecision.CapturedInput(
            new(new ArenaBoundsSquare(20, mapResolution: .5f), default, null, default), [], hints.ForbiddenZones.ToArray(),
            NavigationDecision.CaptureGoals(hints), true, [], identity, true);
        MovementPlanningContext Context(NativeNavigationSnapshot snapshot, float unlock = 0,
            PlanningExecutionRules? rules = null, bool stationary = false)
            => new(snapshot.Identity, snapshot.CopyStrictMap(), 0, unlock,
                Enumerable.Repeat(true, snapshot.Cells).ToArray(), snapshot.CopyPresentation(), [], [],
                native: snapshot, executionRules: rules, stationaryOnly: stationary);
        var native = NativeNavigationSnapshot.Capture(input);
        var context = Context(native);
        var recovery = NativeMovementSearch.Recover(context, new());
        Check((recovery.UsefulDestination - boss.Position).Length() < (actor.Position - boss.Position).Length(),
            "Recorded carrier takes its stack toward boss contact instead of the isolated ranged peer.");
        Check(recovery.NativeRecovery is { LeewaySeconds: > 0, Destination: not null }
            && NativeMovementHost.CanRecoverStackGathering(context, recovery)
            && !NativeMovementHost.CanDeferRecovery(context, recovery),
            "Recorded stack carrier starts a verified native gathering route despite positive leeway and a pending optimizer.");
        Check(!NativeMovementHost.CanRecoverStackGathering(Context(native, unlock: .5f), recovery),
            "Elective gathering finishes current cast/action locks before moving.");
        Check(!NativeMovementHost.CanRecoverStackGathering(Context(native, stationary: true), recovery),
            "Stack gathering cannot activate movement for stationary target-only assistance.");
        Check(!NativeMovementHost.CanRecoverStackGathering(Context(native, rules: new(0, 0, [])), recovery),
            "Stack scores cannot override an authored movement prohibition.");

        foreach (var peer in actors.OfType<Actor>().Where(a => a != actor)) peer.PosRot.X += 100;
        Check(native.ImprovesStackGathering(actor.Position, recovery.UsefulDestination),
            "Frozen gathering evidence never reads party actors after capture.");
        var reset = NativeNavigationSnapshot.Capture(input with { Goals = new([]), Warnings = [] });
        Check(!reset.ImprovesStackGathering(actor.Position, recovery.UsefulDestination),
            "Resolved or cancelled stack clears the gathering fallback in the next capture.");
        var disabled = NativeNavigationSnapshot.Capture(input with { GoalsEnabled = false });
        Check(!disabled.ImprovesStackGathering(actor.Position, recovery.UsefulDestination),
            "Disabled native goals do not retain stack attraction.");
        var untyped = NativeNavigationSnapshot.Capture(input with { Goals = new([_ => 1000]) });
        Check(!untyped.ImprovesStackGathering(actor.Position, recovery.UsefulDestination),
            "An opaque high score cannot impersonate an authored gathering instruction.");

        // A joiner uses the same native fallback, with no carrier-only shortcut.
        actor.PosRot = new(8, 0, 0, 0);
        var carrier = actors[1]!; carrier.PosRot = default;
        stacks.Stacks.Clear(); stacks.Stacks.Add(new(carrier, 6, 2, 4, world.FutureTime(5)));
        hints = new(); stacks.AddAIHints(0, actor, default, hints);
        input = input with { Identity = identity with { Origin = actor.Position },
            Warnings = hints.ForbiddenZones.ToArray(), Goals = NavigationDecision.CaptureGoals(hints) };
        native = NativeNavigationSnapshot.Capture(input); context = Context(native);
        recovery = NativeMovementSearch.Recover(context, new());
        Check(NativeMovementHost.CanRecoverStackGathering(context, recovery)
            && !NativeMovementHost.CanDeferRecovery(context, recovery),
            "Eligible participants also join the stack without waiting for optimizer publication.");
        var blocked = NativeNavigationSnapshot.Capture(input with { Obstacles = [new SDCircle(default, 7)] });
        context = Context(blocked); recovery = NativeMovementSearch.Recover(context, new());
        Check(!NativeMovementHost.CanRecoverStackGathering(context, recovery),
            "An unreachable gathering interior cannot turn emergency recovery into a verified elective stack walk.");
    }
}
