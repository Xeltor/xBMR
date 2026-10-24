using BossMod;
using BossMod.Components;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Runtime;
using BossMod.Pathfinding;

public static partial class Checks
{
    private static void KnockbackAuthority()
    {
        var (world, actor, module) = ProjectionFixture();
        actor.PendingKnockbacks = [];
        NativeKnockbackAuthority? Capture() => NativeKnockbackAuthorityCapture.Capture(world, module, actor);
        Check(Capture() == null, "No published or pending displacement leaves ordinary native planning available.");
        var source = new ActualActorUnsafe(module);
        module.Components.Add(source);
        source.Casters.Add(new(default, 5, world.FutureTime(3)));
        Check(Capture() is { SourceCount: 1, FirstActivationIn: 3, IncompleteEvidence: false },
            "An authored native knockback is captured before an ordinary walking/casting forecast.");
        source.ImmuneUntil(world.FutureTime(4));
        Check(Capture() == null, "Confirmed native immunity covers a known activation without a second immunity model.");
        source.Casters[0] = new(default, 5, world.FutureTime(3), ignoreImmunes: true);
        Check(Capture()?.SourceCount == 1, "An immunity-ignoring authored source retains native displacement authority.");
        source.Casters[0] = new(default, 5);
        Check(Capture() is { IncompleteEvidence: true },
            "An active buff cannot prove protection at an unknown activation; no default timestamp becomes a future safety proof.");
        source.ImmuneUntil(default);
        source.Casters[0] = new(default, 5, world.FutureTime(13));
        Check(Capture() == null, "A known event beyond twelve seconds does not interrupt a supported current forecast.");
        source.Casters[0] = new(default, 5, world.FutureTime(-1));
        Check(Capture() != null, "A still-published past activation does not invent hazard or movement expiry.");
        source.Casters[0] = new(default, 5, world.FutureTime(3), shape: new AOEShapeCircle(.1f));
        Check(Capture() != null, "Current footprint exclusion does not prove that all future route positions avoid a knockback.");
        source.Casters[0] = new(default, 0, world.FutureTime(3));
        Check(Capture() == null, "A native zero-distance source does not create a forced move.");
        source.Casters[0] = new(default, 5, world.FutureTime(3), kind: GenericKnockback.Kind.None);
        Check(Capture() == null, "A native None source does not block unrelated combat planning.");
        source.Casters[0] = new(default, float.NaN, world.FutureTime(3));
        Check(Capture() is { IncompleteEvidence: true }, "Invalid source geometry cannot authorize an ordinary complete-route forecast.");
        source.Casters.Clear();
        actor.PendingKnockbacks.Add(new(1, 0, 10, world.FutureTime(3), true));
        Check(NativeKnockbackAuthorityCapture.Capture(world, null, actor) is { PendingEffect: true },
            "An effect pending after source/module removal still retains native displacement authority.");
        actor.PendingKnockbacks.Clear();
        Check(Capture() == null, "Native resolution clears the temporary handoff without a consumer lease or retry timer.");

        var start = new WPos(3, 4);
        (GenericKnockback.Kind Kind, WPos Expected)[] cases =
        [
            (GenericKnockback.Kind.AwayFromOrigin, new(6, 8)),
            (GenericKnockback.Kind.TowardsOrigin, new(1.2f, 1.6f)),
            (GenericKnockback.Kind.DirForward, new(3, 9)),
            (GenericKnockback.Kind.DirBackward, new(3, 2)),
            (GenericKnockback.Kind.DirLeft, new(8, 4)),
            (GenericKnockback.Kind.DirRight, new(-2, 4))
        ];
        foreach (var item in cases)
        {
            Check(GenericKnockback.TryUnclippedMovement(start, default, 5, default, item.Kind, 2, out var direction, out var distance)
                && (start + distance * direction).AlmostEqual(item.Expected, .00001f),
                "Independent native direction/pull endpoint table: " + item.Kind);
        }
        Check(!GenericKnockback.TryUnclippedMovement(default, default, 5, default,
            GenericKnockback.Kind.AwayFromOrigin, 0, out _, out _), "Coincident radial origins retain native no-direction semantics.");
        Check(!GenericKnockback.TryUnclippedMovement(new(0, 1), default, 5, default,
            GenericKnockback.Kind.TowardsOrigin, 2, out _, out _), "Pull minimum distance retains native no-movement semantics.");

        var map = Grid(2, 1);
        var ordinary = Context(map, 0);
        var action = Action(map, 6, 0, [0, 0], [1, 1], cast: 5);
        var combat = new MovementCombatIntent([action], 0, true, NativeRecoveryReason: "native knockback authority");
        Check(!NativeMovementHost.RequestsCombatOptimization(combat, new())
            && NativeMovementHost.RequestsCombatOptimization(combat with { NativeRecoveryReason = null }, new()),
            "The actual host request boundary cannot optimize a cast through an unmodeled forced displacement.");
        Check(!NativeMovementHost.RequestsCombatOptimization(combat with { NativeRecoveryReason = null }, new(ExplicitDestination: ordinary.Origin)),
            "Explicit instructions retain their separate native authority rather than acquiring combat optimization.");
        var old = JointMovementSearch.Search(new(ordinary.Identity, map, 0, 0, [true, true], [0, 0], [action], []), Exhaustive);
        var disabled = new MovementPlanningContext(ordinary.Identity, map, 0, 0, [true, true], [0, 0], [], [], optimize: false);
        Check(NativeMovementHost.CommitCandidate(old, disabled) == null,
            "An old complete cast-bearing worker result cannot be committed after native recovery takes authority.");
    }
}
