using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;

public static partial class Checks
{
    private sealed class NativePhaseKnockback(BossModule module, int maxCasts = int.MaxValue) : SimpleKnockbacks(module, 0, 5, maxCasts: maxCasts)
    {
        internal readonly List<(ShapeDistance Shape, DateTime At, ulong Actor)> Regions = [];
        public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        {
            foreach (var region in Regions) hints.ForbiddenZones.Add((region.Shape, region.At, region.Actor));
        }
        internal void ImmunityUntil(DateTime until) => PlayerImmunes[0].RoleBuffExpire = until;
    }

    private sealed class PoseDependentDisplacement(BossModule module) : GenericKnockback(module)
    {
        private readonly GenericKnockback.Knockback[] _sources = [new(default, 5)];
        public override ReadOnlySpan<GenericKnockback.Knockback> ActiveKnockbacks(int slot, Actor actor)
            => actor.Position.X > 0 ? _sources : [];
    }

    private static void DisplacementInventoryAndPhases()
    {
        var (world, actor, module) = ProjectionFixture();
        actor.PendingKnockbacks = [];
        NativeDisplacementCapture Inventory(AIHints? hints = null) => NativeDisplacementCapture.Capture(world, module, actor, hints ?? new AIHints());
        Check(Inventory().Snapshot is { Complete: true, AuthoredAbsenceVerified: true },
            "A complete empty native source inventory can establish authored absence, separately from a movement permission.");
        Check(!NativeDisplacementCapture.Capture(world, module, actor).Snapshot.AuthoredAbsenceVerified,
            "An inventory without the native hint constraints cannot certify complete authored absence.");
        var markerOnly = new AIHints();
        markerOnly.ForbiddenZones.Add((new SDKnockbackInCircleAwayFromOrigin(default, default, 5, 8), world.FutureTime(3), 0));
        Check(Inventory(markerOnly).Snapshot is { PreparationConstraints: 1, AuthoredAbsenceVerified: false },
            "An authored preparation constraint vetoes learned admission even when no generic source was published.");
        Check(NativeKnockbackAuthorityCapture.Capture(Inventory(markerOnly).Snapshot) is { IncompleteEvidence: true },
            "An authored preparation-only publication cannot credit stationary execution through its unmodeled push.");
        markerOnly.ForbiddenZones[0] = (markerOnly.ForbiddenZones[0].shapeDistance, world.FutureTime(20), 0);
        Check(NativeKnockbackAuthorityCapture.Capture(Inventory(markerOnly).Snapshot) == null
            && !Inventory(markerOnly).Snapshot.AuthoredAbsenceVerified,
            "A far-future preparation rule does not interrupt the twelve-second forecast or become proof of authored absence.");
        var owner = new NativePhaseKnockback(module);
        module.Components.Add(owner);
        var at = world.FutureTime(3);
        owner.Casters.Add(new(default, 5, at, actorID: 10));
        var capture = Inventory();
        Check(capture.Snapshot.Knockbacks is [{ SourceActor: 10, ObservedImmune: false, Enabled: true }]
            && !capture.Snapshot.AuthoredAbsenceVerified && NativeKnockbackAuthorityCapture.Capture(capture.Snapshot) != null,
            "Every native source is inventoried before the ordinary movement-interruption filter.");
        owner.ImmunityUntil(world.FutureTime(10));
        var immune = Inventory();
        Check(immune.Snapshot.Knockbacks.Single().ObservedImmune && !immune.Snapshot.AuthoredAbsenceVerified
            && NativeKnockbackAuthorityCapture.Capture(immune.Snapshot) == null,
            "A null ordinary knockback gate due to observed immunity is never proof that authored sources are absent.");
        owner.ImmunityUntil(default);
        foreach (var value in new GenericKnockback.Knockback[] { new(default, 5, world.FutureTime(13)),
            new(default, 0, at), new(default, 5, at, kind: GenericKnockback.Kind.None) })
        {
            owner.Casters[0] = value;
            var present = Inventory();
            Check(!present.Snapshot.AuthoredAbsenceVerified && present.Snapshot.Knockbacks.Length == 1
                && NativeKnockbackAuthorityCapture.Capture(present.Snapshot) == null,
                "Outside-horizon, zero-distance and native-None publications retain authored provenance without forcing an ordinary dodge.");
        }
        owner.Casters.Clear();
        var dependent = new PoseDependentDisplacement(module);
        module.Components.Add(dependent); actor.PosRot.X = -1;
        var hidden = Inventory();
        Check(hidden.Snapshot.Knockbacks.IsEmpty && !hidden.Snapshot.Complete && !hidden.Snapshot.AuthoredAbsenceVerified,
            "A pose-dependent publisher returning no current source cannot authorize learned fallback for other route poses.");
        module.Components.Remove(dependent); actor.PosRot.X = 1;
        var march = new GenericForcedMarch(module);
        march.State[actor.InstanceID] = new() { PendingMoves = [(default, 1, world.FutureTime(3))] };
        module.Components.Add(march);
        Check(Inventory().Snapshot is { MarchPublished: true, AuthoredAbsenceVerified: false },
            "Native forced march remains independent authored displacement even without a knockback source.");
        module.Components.Remove(march);
        actor.PendingKnockbacks.Add(new(1, 0, 10, at, true));
        Check(!Inventory().Snapshot.AuthoredAbsenceVerified, "Pending native effects veto authored absence after a publisher clears.");
        actor.PendingKnockbacks.Clear();
        owner.Casters.Add(new(default, 5, at, actorID: 10));
        var pure = new SDKnockbackInCircleAwayFromOrigin(default, default, 5, 8);
        var damage = new SDCircle(new(-4, 0), 1);
        ShapeDistance[] children = [pure, damage];
        owner.Regions.Add((new SDUnion(children), at, 10));
        AIHints Hints()
        {
            var hints = new AIHints { ProjectionSourcesRequested = true, PathfindMapCenter = default, PathfindMapBounds = module.Bounds };
            hints.CollectComponentHints(owner, 0, actor, default);
            return hints;
        }
        NativeKnockbackPhases Freeze(AIHints hints)
        {
            var input = NavigationDecision.CaptureInput(world.CurrentTime, hints, actor, 6, 0, world.CurrentZone, world.CurrentCFCID);
            return NativeKnockbackPhaseCapture.Freeze(Inventory(hints), hints, module, actor, input);
        }
        var hints = Hints();
        var before = NativeNavigationSnapshot.Capture(NavigationDecision.CaptureInput(world.CurrentTime, hints, actor));
        var phases = Freeze(hints);
        Check(phases.Phases is [{ SeparatedLeaves: 1, IndependentLeaves: 1 }],
            "An exact native union separates its pure preparation leaf while retaining the independent damage leaf.");
        var phase = phases.Phases.Single();
        var timings = new List<double>();
        for (var sample = 0; sample < 10; ++sample)
        {
            var timer = Stopwatch.StartNew();
            _ = Freeze(hints);
            timings.Add(timer.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        Console.WriteLine($"Synthetic native knockback phase capture, {phase.After.Cells} cells: median={timings[timings.Count / 2]:F3}ms max={timings[^1]:F3}ms; framework capture only, not live frame-time acceptance.");
        float Limit(NativeNavigationSnapshot snapshot, WPos point)
        {
            var map = snapshot.CopyStrictMap(); var grid = map.WorldToGrid(point);
            return map.PixelMaxG[map.GridToIndex(grid)];
        }
        Check(Limit(before, new(6, 0)) < float.MaxValue && Limit(phase.After, new(6, 0)) == float.MaxValue,
            "The native after-phase raster removes preparation geometry rather than requiring a second pre-push position after landing.");
        Check(Limit(phase.After, new(-4, 0)) < float.MaxValue && hints.ForbiddenZones[0].shapeDistance is SDUnion,
            "Independent damage stays binding and the original recovery hints/union are never mutated.");
        Check(phase.LandingAllowed(new(6, 0), .5f) && !phase.LandingAllowed(new(9, 0), .5f)
            && Limit(phase.After, new(9, 0)) == float.MaxValue,
            "An authored landing envelope binds the forced landing without inventing a permanent post-event assignment.");
        children[1] = new SDCircle(new(6, 0), 1);
        Check(Freeze(hints).Phases.IsEmpty && Limit(phase.After, new(6, 0)) == float.MaxValue,
            "Changed producer graphs are rejected; an already frozen worker raster retains no mutable shape callback.");
        children[1] = damage;
        owner.Regions.Clear();
        owner.Regions.Add((new SDKnockbackInCircleAwayFromOriginMixedAOEs(default, default, 5, 8, [], 0), at, 10));
        Check(Freeze(Hints()).Phases.IsEmpty,
            "An opaque mixed preparation/AoE marker is not permission to discard its independent requirements.");
        owner.Regions.Clear();
        owner.Regions.Add((pure, at, 10));
        owner.Casters.Add(new(new(1, 0), 3, world.FutureTime(6), actorID: 11));
        owner.Regions.Add((new SDKnockbackInCircleAwayFromOrigin(default, new(1, 0), 3, 9), world.FutureTime(6), 11));
        var sequence = Freeze(Hints());
        Check(sequence.Phases.Length == 2 && sequence.Phases[0].Source.Activation < sequence.Phases[1].Source.Activation,
            "Distinct successive native sources receive ordered phase maps rather than an inherited single-source cap.");
        var limited = new NativePhaseKnockback(module, 1);
        limited.Casters.Add(new(default, 5, at)); limited.Casters.Add(new(default, 5, world.FutureTime(6)));
        module.Components.Add(limited);
        var limitedInput = Inventory();
        Check(limitedInput.Snapshot.Knockbacks.Count(s => s.MaximumAppliedSources == 1) == 2
            && Freeze(Hints()).Detail.Contains("source-count", StringComparison.Ordinal),
            "The inventory retains all publications and the native application limit; phase capture cannot invent extra applied pushes.");
        module.Components.Remove(limited);
        var secondOwner = new NativePhaseKnockback(module);
        secondOwner.Casters.Add(new(default, 5, at, actorID: 12)); module.Components.Add(secondOwner);
        var simultaneousHints = Hints();
        simultaneousHints.CollectComponentHints(secondOwner, 0, actor, default);
        Check(Freeze(simultaneousHints).Phases.IsEmpty,
            "Simultaneous independent native owners require authored composition rather than an invented order.");
        module.Components.Remove(secondOwner);
        owner.Casters.RemoveAt(1); owner.Regions.RemoveAt(1);
        var freshHints = Hints(); var first = Freeze(freshHints);
        var elapsed = .1f; world.Frame.Timestamp = world.Frame.Timestamp.AddSeconds(elapsed);
        var next = Freeze(Hints());
        Check(next.Same(first), "Repeated phase captures preserve original source times and age native warning deadlines without renewing them.");
        owner.Casters[0] = new(default, 6, at, actorID: 10);
        Check(!Freeze(Hints()).Same(next), "A material source change invalidates phase evidence immediately.");
        var diagnostic = new BossMod.CombatAssistance.Diagnostics.NativeDiagnosticSnapshot();
        using var json = System.Text.Json.JsonDocument.Parse(diagnostic.Read("""{"schemaVersion":1,"stream":"combat"}""",
            null, true, false, "Ready", null, world.CurrentTime, displacement: new(1, 1, 0, true, false, false, false, 0, "observed immunity")));
        Check(!json.RootElement.GetProperty("snapshot").GetProperty("displacement").GetProperty("AuthoredAbsenceVerified").GetBoolean(),
            "Current diagnostics distinguish no interrupting forecast from a proven empty authored inventory.");
    }
}
