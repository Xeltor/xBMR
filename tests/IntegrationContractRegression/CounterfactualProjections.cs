using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;

static partial class Checks
{
    private sealed class PoseDependentSources(BossModule module) : SimpleKnockbacks(module, 0, 5)
    {
        public override ReadOnlySpan<GenericKnockback.Knockback> ActiveKnockbacks(int slot, Actor actor)
            => actor.Position.X > 0 ? base.ActiveKnockbacks(slot, actor) : [];
    }
    private sealed class ActualActorUnsafe(BossModule module) : SimpleKnockbacks(module, 0, 5)
    {
        internal WPos SeenActor;
        public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) { SeenActor = actor.Position; return pos.X > 2; }
        internal void ImmuneUntil(DateTime time) => PlayerImmunes[0].RoleBuffExpire = time;
    }

    private static (WorldState World, Actor Actor, MechanicsModule Module) ProjectionFixture()
    {
        Service.Config._nodes.TryAdd(typeof(BossModuleConfig), new BossModuleConfig());
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        world.Frame = new(new DateTime(2026, 10, 17, 13, 0, 0, DateTimeKind.Utc), 0, 1, 0.016f, 0.016f, 1);
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(new ActorState()));
        var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        actor.InstanceID = 1; actor.PosRot = new(1, 0, 2, 0.4f); actor.HitboxRadius = 0.5f;
        world.Party.Members[0] = new(1, 1, false);
        ((Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!)[0] = actor;
        var module = (MechanicsModule)RuntimeHelpers.GetUninitializedObject(typeof(MechanicsModule));
        typeof(BossModule).GetField(nameof(BossModule.WorldState))!.SetValue(module, world);
        typeof(BossModule).GetField(nameof(BossModule.Arena))!.SetValue(module, new MiniArena(default, new ArenaBoundsSquare(10)));
        typeof(BossModule).GetField(nameof(BossModule.Components))!.SetValue(module, new List<BossComponent>());
        typeof(BossModule).GetField("_actorArenaProjectionLayers", InternalInstance)!.SetValue(module, new Dictionary<ulong, int>());
        return (world, actor, module);
    }

    private static void CounterfactualProjections()
    {
        var (world, actor, module) = ProjectionFixture();
        var original = actor.PosRot;
        var knockback = new ActualActorUnsafe(module);
        knockback.Casters.Add(new(default, 5, world.FutureTime(3)));
        IntegrationRoutes.Budget Budget() => new(clock: () => 0, frequency: 1000);
        IntegrationCounterfactualProjection.Result? Project(BossComponent component, WPos p, float facing = 0, int slot = 0)
            => IntegrationCounterfactualProjection.Project(component, actor, slot, new([p.X, p.Z], facing), Budget());
        var result = Project(knockback, new(3, 4))!;
        Check(result.Movements is [{ To: [6, 8] }] && result.Movements[0].NativeDestinationUnsafe,
            "arbitrary radial starting position uses native projection and custom destination rule");
        Check(knockback.SeenActor == actor.Position && result.Movements[0].NativeDestinationContext == "native-override-with-actual-actor-context"
            && actor.PosRot == original, "custom destination observation retains real actor context without cloning or moving that actor");
        knockback.ImmuneUntil(world.FutureTime(4));
        Check(Project(knockback, new(3, 4))!.Status == "no-published-displacement", "native immunity applies to hypothetical starting positions");
        knockback.Casters[0] = new(default, 5, world.FutureTime(3), ignoreImmunes: true);
        Check(Project(knockback, new(3, 4))!.Movements.Length == 1, "native source can ignore immunity without a consumer-side reconstruction");
        knockback.ImmuneUntil(default);
        knockback.Casters[0] = new(default, 5, world.FutureTime(3), shape: new AOEShapeCircle(2));
        Check(Project(knockback, new(3, 4))!.Movements.Length == 0 && Project(knockback, new(1, 0))!.Movements.Length == 1,
            "source footprint applicability uses the hypothetical start, not the actual actor position");
        var dependent = new PoseDependentSources(module);
        dependent.Casters.Add(new(default, 5));
        Check(!dependent.SupportsCounterfactualProjection && Project(dependent, new(-1, 0)) == null,
            "derived pose-dependent source provider cannot inherit simple-source support");
        Check(IntegrationCounterfactualProjection.Supports(new SimpleKnockbackGroups(module, [1u, 2u], 5)),
            "inheriting the unchanged native simple publication retains support");

        foreach (var kind in Enum.GetValues<GenericKnockback.Kind>())
        foreach (var wallMode in new[] { 0, 1, 2, 3 })
        foreach (var start in new WPos[] { new(3, 4), new(-4, 2), new(0, 0) })
        {
            var source = new SimpleKnockbacks(module, 0, 12, stopAtWall: wallMode == 1, stopAfterWall: wallMode == 2);
            source.Casters.Add(new(new(-3, 1), 12, world.FutureTime(2), direction: 90f.Degrees(), kind: kind, minDistance: 2,
                safeWalls: wallMode == 3 ? [new(new(6, -20), new(6, 20))] : []));
            // Only an owned offline fixture actor is moved for the legacy oracle.
            // Restore it before calling the new native query under test.
            actor.PosRot = new(start.X, original.Y, start.Z, original.W);
            var legacy = source.CalculateMovementProjections(0, actor);
            actor.PosRot = original;
            var projected = Project(source, start)!;
            Check(projected.Movements.Length == legacy.Count, "counterfactual and legacy projection source counts agree for every native kind/wall mode");
            for (var i = 0; i < legacy.Count; ++i)
                Check(Vector2.Distance(new(legacy[i].To.X, legacy[i].To.Z), new(projected.Movements[i].To[0], projected.Movements[i].To[1])) < 0.00001f,
                    "shared native math preserves pull limits, wall clipping and explicit safe-wall projection");
            Check(actor.PosRot == original, "every alternative projection leaves actual actor pose unchanged");
        }
        var limited = new SimpleKnockbacks(module, 0, 2, maxCasts: 1);
        limited.Casters.Add(new(default, 2, world.FutureTime(2), kind: GenericKnockback.Kind.DirForward));
        limited.Casters.Add(new(default, 4, world.FutureTime(4), kind: GenericKnockback.Kind.DirForward));
        Check(Project(limited, new(1, 1))!.Movements.Length == 1, "native MaxCasts remains authoritative rather than claiming complete future sequence coverage");
        var timed = new ActualActorUnsafe(module);
        timed.Casters.Add(new(default, 2, world.FutureTime(2), kind: GenericKnockback.Kind.DirForward));
        timed.Casters.Add(new(default, 4, world.FutureTime(4), kind: GenericKnockback.Kind.DirForward));
        timed.ImmuneUntil(world.FutureTime(3));
        var surviving = Project(timed, new(1, 1))!.Movements;
        Check(surviving.Length == 1 && surviving[0].SourceIndex == 1 && surviving[0].ActivationTicks == world.FutureTime(4).Ticks.ToString(),
            "immunity filtering preserves original source index and clock for counterfactual sequences");
        for (var i = 0; i < 33; ++i) limited.Casters.Add(new(default, 1));
        Check(Project(limited, default) == null, "oversized native publications remain unavailable even if MaxCasts would hide the excess");

        var march = new GenericForcedMarch(module);
        march.AddForcedMovement(actor, 90f.Degrees(), 1, world.FutureTime(2));
        march.AddForcedMovement(actor, -90f.Degrees(), 0.5f, world.FutureTime(5));
        foreach (var active in new[] { false, true })
        foreach (var overrideDirection in new[] { false, true })
        foreach (var face in new[] { -MathF.PI, -MathF.PI / 2, 0f, MathF.PI / 2, MathF.PI })
        {
            if (active) march.ActivateForcedMovement(actor, world.FutureTime(1));
            march.OverrideDirection = overrideDirection;
            var start = new WPos(-2, -3);
            actor.PosRot = new(start.X, original.Y, start.Z, face);
            var legacy = march.CalculateMovementProjections(actor);
            actor.PosRot = original;
            var projected = Project(march, start, face)!;
            Check(legacy.Count == projected.Movements.Length && projected.Movements.Length == (active ? 3 : 2),
                "counterfactual march retains both pending sequence and optional active remainder");
            for (var i = 0; i < legacy.Count; ++i)
                Check(Vector2.Distance(new(legacy[i].To.X, legacy[i].To.Z), new(projected.Movements[i].To[0], projected.Movements[i].To[1])) < 0.00001f
                    && Math.Abs(legacy[i].Direction.Rad - projected.Movements[i].Facing!.Value) < 0.00001f,
                    "native relative/override heading and active march facing match the shared legacy calculation");
            Check(actor.PosRot == original, "march projection never writes queried facing back to the native actor");
        }
        var one = Project(march, new(-2, -3), 0)!.Movements[0];
        Check(one.SourceIndex == null && one.EndTicks == world.FutureTime(1).Ticks.ToString(), "active march timing remains distinct from pending status activation");
        foreach (var stopAtWall in new[] { false, true })
        {
            var wallMarch = new GenericForcedMarch(module, stopAtWall: stopAtWall, stopAfterWall: !stopAtWall);
            wallMarch.AddForcedMovement(actor, default, 2, world.FutureTime(2));
            var start = new WPos(9, 0);
            actor.PosRot = new(start.X, original.Y, start.Z, MathF.PI / 2);
            var legacy = wallMarch.CalculateMovementProjections(actor);
            actor.PosRot = original;
            var clipped = Project(wallMarch, start, MathF.PI / 2)!.Movements[0];
            Check(Math.Abs(clipped.To[0] - legacy[0].To.X) < 0.00001f && clipped.To[0] < 10.01f,
                "hypothetical march preserves native stop-at and stop-after-wall clipping");
        }
        var activationLimited = new GenericForcedMarch(module, activationLimit: 1);
        activationLimited.AddForcedMovement(actor, default, 1, world.FutureTime(3));
        Check(Project(activationLimited, default)!.Status == "no-published-displacement",
            "native activation publication limit remains distinct from an empty complete mechanic forecast");
        Check(Project(knockback, new(float.NaN, 0)) == null && Project(knockback, new(300, 0)) == null
            && Project(knockback, default, float.PositiveInfinity) == null && Project(knockback, default, slot: -1) == null,
            "invalid pose, oversized displacement and missing party slot cannot produce geometry");
        Check(IntegrationCounterfactualProjection.Project(march, actor, 0, new([0, 0], 0), new(clock: () => 0, frequency: 1000, evaluations: 0)) == null,
            "an exhausted query budget cannot return a complete empty sequence");
        march.MovementSpeed = float.NaN;
        Check(Project(march, default) == null, "non-finite native movement output remains unavailable");
        actor.PosRot = new(float.NaN, 0, 0, 0);
        Check(Project(knockback, default) == null, "unknown actual actor coordinates cannot satisfy a counterfactual neighborhood bound");
        actor.PosRot = original;
    }
}
