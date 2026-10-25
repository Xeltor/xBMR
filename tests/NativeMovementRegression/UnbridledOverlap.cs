using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;

public static partial class Checks
{
    private static void UnbridledOverlap()
    {
        Service.Config._nodes.TryAdd(typeof(ColorConfig), new ColorConfig());
        // Recorded 2026-10-25: the second jump's preparation annulus became
        // active while the 21:43:07.692 Heated Outburst wave was still pending.
        // The actual local player was hit by two distinct sources in that wave.
        var (world, actor, module) = ProjectionFixture();
        var start = new DateTime(2026, 10, 25, 21, 42, 53).AddTicks(7160485);
        void At(DateTime time) => world.Frame = world.Frame with { Timestamp = time };
        At(start);
        actor.Class = Class.WHM;
        actor.PosRot = new(609.9348f, 79, 807.86743f, 0);
        module.Arena.Center = new(620, 800);
        module.Arena.Bounds = new ArenaBoundsCircle(25, 0.5f);
        var assembly = typeof(BossModule).Assembly;
        const string prefix = "BossMod.Dawntrail.Foray.CriticalEngagement.CE107Unbridled.";
        var heat = (SimpleAOEs)Activator.CreateInstance(assembly.GetType(prefix + "HeatedOutburst", true)!, module)!;
        module.Components.Add(heat);
        var hops = (GenericAOEs)Activator.CreateInstance(assembly.GetType(prefix + "HoppingMad", true)!, module)!;
        module.Components.Add(hops);
        ActorCastInfo Cast(uint id, WPos point, float total, float elapsed)
            => new() { Action = new(ActionType.Spell, id), Location = new(point.X, 79, point.Z), TotalTime = total, ElapsedTime = elapsed };
        hops.OnCastStarted(actor, Cast(30873, new(644.98364f, 799.98474f), 15.2f, .0333793f));
        hops.OnCastStarted(actor, Cast(37041, new(619.98926f, 824.979f), 22.2f, .0333793f));
        hops.OnCastStarted(actor, Cast(30872, new(598.35205f, 787.4723f), 8.7f, .0333793f));
        ActorCastEvent Effect(uint id) => new(new(ActionType.Spell, id), 0, 0, 0, default, 0, 0, default);
        hops.OnEventCast(actor, Effect(30872));
        hops.OnEventCast(actor, Effect(37805));
        At(new DateTime(2026, 10, 25, 21, 43, 1).AddTicks(7273225));
        var sources = new[] { new WPos(627.985f, 782.9862f), new WPos(616.99854f, 816.9833f), new WPos(621.973f, 801.9989f) };
        foreach (var (point, index) in sources.Select((p, i) => (p, i)))
        {
            var helper = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            helper.InstanceID = (ulong)(100 + index);
            heat.OnCastStarted(helper, Cast(37804, point, 5.7f, .0281204f));
        }
        At(new DateTime(2026, 10, 25, 21, 43, 4).AddTicks(8366340));
        AIHints Hints()
        {
            var hints = new AIHints { PathfindMapCenter = new(620, 800), PathfindMapBounds = module.Arena.Bounds };
            heat.AddAIHints(0, actor, default, hints);
            hops.AddAIHints(0, actor, default, hints);
            hints.Normalize();
            return hints;
        }
        var left = new WPos(606.7393f, 808.23474f);
        var hints = Hints();
        Check(hints.ForbiddenZones.All(z => z.shapeDistance.Distance(left) > 0),
            "Preparing for the next jump must not forbid the nearby escape from the entire expanding-circle wave.");
        Check(hints.GoalZones.Count > 0,
            "Jump preparation remains visible as a positioning preference, independently of actual damage constraints.");
        var overlap = new WPos(624.8939f, 810.1777f);
        Check(hints.ForbiddenZones.Count(z => z.shapeDistance.Distance(overlap) < 0) >= 2,
            "Both independently recorded Heated Outburst circles remain dangerous at the fatal overlap.");
        var input = NativeMechanicTiming.Apply(NavigationDecision.CaptureInput(world.CurrentTime, hints, actor, 6.6f), Class.WHM);
        var native = NativeNavigationSnapshot.Capture(input);
        var recovery = native.Recover(new());
        Check(recovery.Publication is { } route && route.Endpoint.X < actor.Position.X
            && (route.Endpoint - actor.Position).Length() < 6,
            "Overdue native recovery keeps the short left escape rather than crossing two circles to satisfy jump preparation.");
        // The extra healer clearance is a preference. Independently check the
        // WHOLE replacement witness against the normal native safety margin,
        // all damage warnings and cumulative travel time, not just its endpoint.
        var transit = NativeNavigationSnapshot.Capture(input with { WarningTimeCushion = NavigationDecision.ActivationTimeCushion }).CopyStrictMap();
        var witness = recovery.Publication!.CopyWitness();
        var elapsed = 0f;
        for (var i = 1; i < witness.Length; ++i)
        {
            var duration = (witness[i] - witness[i - 1]).Length() / input.Identity.Speed;
            Check(TimedTraversal.Check(transit, witness[i - 1], witness[i], elapsed, duration, 0, out _),
                "Every replacement escape segment clears every recorded active warning before its native exit deadline.");
            elapsed += duration;
        }
        heat.Casters.Clear();
        var solo = Hints();
        Check(solo.ForbiddenZones.All(z => z.shapeDistance.Distance(left) > 0) && solo.GoalZones.Count > 0,
            "The same preparation preference does not fabricate damage when the expanding-circle wave has resolved.");
        var insideJump = new WPos(644.98364f, 799.98474f);
        Check(solo.ForbiddenZones.Any(z => z.shapeDistance.Distance(insideJump) < 0),
            "The actual upcoming jump circle remains a timed hard constraint.");
        hops.OnEventCast(actor, Effect(30873));
        var donut = Hints();
        Check(donut.ForbiddenZones.Any(z => z.shapeDistance.Distance(left) < 0)
            && donut.ForbiddenZones.All(z => z.shapeDistance.Distance(insideJump) > 0),
            "After the jump resolves, the predicted donut still requires moving inside its actual safe radius.");
    }
}
