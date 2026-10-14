using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private static void PromathiaLinkedPuddles()
    {
        var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
        var boss = Actor(100, 0x4DEE, new(-820, -844.5411f));
        Set(module, "PrimaryActor", boss);
        var actor = Actor(1, 0, new(-820, -820), true);
        var component = Component<GenericAOEs>(module, PromathiaPrefix + "EmptySalvationPuddles");
        var cast = Cast(50317, boss.Position);
        cast.TotalTime = 4.7f;
        boss.CastInfo = cast;
        component.OnCastStarted(boss, cast);
        var pairs = new[]
        {
            (new WPos(-832.9904f, -827.5f), 120f.Degrees(), new WPos(-820, -835)),
            (new WPos(-832.9904f, -812.5f), 60f.Degrees(), new WPos(-820, -805)),
            (new WPos(-807.0096f, -812.5f), 180f.Degrees(), new WPos(-807.0096f, -827.5f))
        };
        var pools = pairs.Select((p, i) => Actor((ulong)(200 + i), 0x1EC098, p.Item1)).ToArray();
        for (var i = 0; i < pools.Length; ++i)
        {
            pools[i].PosRot = new Vector4(pairs[i].Item1.X, -900, pairs[i].Item1.Z, pairs[i].Item2.Rad);
            component.OnActorCreated(pools[i]);
            component.OnActorCreated(pools[i]);
        }
        var aoes = component.ActiveAOEs(0, actor).ToArray();
        Check(aoes.Length == 6 && aoes.All(a => a.Activation == module.CastFinishAt(cast, 0.8)),
            "three post-island objects forecast six Heavy endpoints once");
        foreach (var (first, _, second) in pairs)
        {
            Check(aoes.Count(a => a.Origin.AlmostEqual(first, 0.01f)) == 1 && aoes.Count(a => a.Origin.AlmostEqual(second, 0.01f)) == 1,
                "each separately listed hexagon endpoint is represented");
            Check(!aoes.Any(a => a.Check(first + (second - first) * 0.5f)), "the linking segment is not invented as a Heavy pool");
        }
        foreach (var pool in pools) component.OnActorEAnim(pool, 0x00010002);
        var hints = new AIHints();
        component.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 6 && hints.ForbiddenZones.All(z => z.activation == Now),
            "all six activated black pools immediately constrain AI navigation");
        Check(hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(pairs[0].Item1 + new WDir(2.25f, 0)))
            && !hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(pairs[0].Item1 + new WDir(2.75f, 0))),
            "nominal linked-pool edge preserves the space outside its 2.5y footprint");
        foreach (var state in new[] { 0x00100020u, 0x00400080u })
        {
            foreach (var pool in pools) component.OnActorEAnim(pool, state);
            Check(component.ActiveAOEs(0, actor).Length == 6, "orb travel animations preserve both persistent endpoints");
        }
        component.OnActorEAnim(pools[0], 0x00040008);
        Check(component.ActiveAOEs(0, actor).Length == 4, "fade removes both endpoints of its own pair");
        component.OnActorDestroyed(pools[1]);
        Check(component.ActiveAOEs(0, actor).Length == 2, "destruction removes exactly its own pair");
        component.OnCastStarted(boss, Cast(50343, boss.Position));
        Check(component.ActiveAOEs(0, actor).Length == 0, "a new island phase clears linked pools");
    }

    private static void PromathiaRebirthKnockback()
    {
        foreach (var rotation in new[] { 0f, 90f, -60f })
        {
            var center = new WPos(-820, -820);
            var direction = rotation.Degrees();
            var module = Module(PromathiaPrefix + "A34Promathia", center);
            module.Arena.Bounds = new ArenaBoundsCircle(25);
            var boss = Actor(100, 0x4DEE, center - direction.ToDirection() * 24.5f);
            var player = Actor(1, 0, center + direction.ToDirection() * 10, true);
            var helper = Actor(200, 0x233C, boss.Position);
            var component = Component<GenericKnockback>(module, PromathiaPrefix + "DeadlyRebirthKnockback");
            var cast = Cast(50694, boss.Position);
            cast.TotalTime = 7.7f;
            cast.Rotation = direction;
            component.OnCastStarted(boss, cast);
            var predicted = component.ActiveKnockbacks(0, player).ToArray().Single();
            Check(predicted.Kind == GenericKnockback.Kind.DirForward && predicted.Distance == 20
                && predicted.Activation == module.CastFinishAt(cast, 2), "visual cue forecasts the full directional push early");
            var movement = component.CalculateMovements(0, player).Single();
            Check(movement.to.AlmostEqual(player.Position + direction.ToDirection() * 20, 0.01f)
                && component.DestinationUnsafe(0, player, movement.to), "forward push detects falling off the restored circle");
            var hints = new AIHints();
            component.AddAIHints(0, player, default, hints);
            Check(hints.ForbiddenZones.Single().shapeDistance.Contains(player.Position)
                && !hints.ForbiddenZones.Single().shapeDistance.Contains(center - direction.ToDirection() * 10),
                "AI rejects dangerous takeoff positions while keeping a reachable safe side");
            module.WorldState.Frame = new(Now.AddSeconds(3), 0, 0, 0, 0, 1);
            component.OnCastStarted(boss, cast);
            Check(component.ActiveKnockbacks(0, player)[0].Activation == predicted.Activation, "duplicate visual cue cannot drift the deadline");
            cast.EventHappened = true;
            component.OnCastFinished(boss, cast);
            Check(component.ActiveKnockbacks(0, player).Length == 1, "visual finish retains the pending helper push");
            var refine = Cast(50347, boss.Position);
            refine.Rotation = direction;
            refine.TotalTime = 1.7f;
            component.OnCastStarted(helper, refine);
            Check(component.ActiveKnockbacks(0, player)[0].Activation == module.CastFinishAt(refine), "helper cue refines the impending snapshot");
            component.OnEventCast(helper, Effect(50347));
            Check(component.ActiveKnockbacks(0, player).Length == 0 && component.NumCasts == 1, "resolved push clears its forecast once");
            component.OnCastStarted(boss, Cast(50694, boss.Position));
            component.OnCastFinished(boss, cast);
            Check(component.ActiveKnockbacks(0, player).Length == 1, "stale visual finish cannot cancel a replacement");
            component.OnActorDestroyed(boss);
            Check(component.ActiveKnockbacks(0, player).Length == 0, "destroyed source cleans up its pending push");
            component.OnCastStarted(helper, refine);
            component.OnCastFinished(helper, refine);
            Check(component.ActiveKnockbacks(0, player).Length == 0, "interrupted helper retires a bootstrapped push");
            component.OnCastStarted(boss, Cast(50694, boss.Position));
            component.OnCastStarted(boss, Cast(50343, boss.Position));
            var cleared = new AIHints();
            component.AddAIHints(0, player, default, cleared);
            Check(component.ActiveKnockbacks(0, player).Length == 0 && cleared.ForbiddenZones.Count == 0,
                "new Genesis clears both drawing and AI forecast");
            var cancelled = Cast(50694, boss.Position);
            component.OnCastStarted(boss, cancelled);
            component.OnCastFinished(boss, cancelled);
            Check(component.ActiveKnockbacks(0, player).Length == 0, "interrupted visual cannot leave a phantom knockback");
        }
    }
}
