using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Dawntrail.Trial.T07Doomtrain;

static partial class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, float x, float y, float z)
        => new(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 100, new Vector4(x, y, z, 0));
    private static ActorCastInfo Cast(uint aid, WPos pos)
        => new() { Action = new(ActionType.Spell, aid), Location = new(pos.X, 0, pos.Z), TotalTime = 10 };
    private static ActorCastEvent Hit(uint aid) => new(new(ActionType.Spell, aid), 0, 0, 0, default, 0, 0, default);
    private static T Component<T>(BossModule module, string name) where T : BossComponent
        => (T)module.Components.Single(c => c.GetType().Name == name);
    private static T07Doomtrain Module(int car = 1, bool intermission = false)
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var primary = Actor(1, (uint)OID.Doomtrain, 100, 0, 75 + (car - 1) * 50);
        if (intermission)
        {
            var add = Actor(90, (uint)OID.AetherIntermission, -400, -900, -400);
            add.IsTargetable = true;
            world.Actors.Actors.Add(add.InstanceID, add);
        }
        var module = new T07Doomtrain(world, primary);
        module.StateMachine.Start(Now);
        return module;
    }
    private static AIHints Hints(BossModule module, BossComponent component, Actor actor)
    {
        var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        component.AddAIHints(0, actor, default, hints);
        return hints;
    }
    private static bool Forbidden(AIHints hints, WPos point) => hints.ForbiddenZones.Any(z => z.shapeDistance.Distance(point) <= 0);

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ArenaTransitions();
        FloorsAndSources();
        Signals();
        TowersAndPreview();
        ExplosionDestinations();
        MovementRoutes();
        Console.WriteLine($"{assertions} Doomtrain assertions passed (synthetic callbacks; no physical or live gameplay proof).");
    }
    private static void ArenaTransitions()
    {
        using var module = Module();
        var geometry = Component<BossComponent>(module, "CarGeometry");
        Check(module.Components.Count == 22, "all generated component factories activate at pull");
        foreach (var (index, state, z) in new[] { (5, 0x00020001u, 150f), (6, 0x00200001u, 200f), (7, 0x00020001u, 250f) })
        {
            geometry.OnMapEffect((byte)index, state);
            Check(module.Center == new WPos(100, z), "transfer updates arena without a cast-order timeout");
            Check(!module.Bounds.Contains(new WDir(-10.1f, 0)) && !module.Bounds.Contains(new WDir(10.1f, 0)), "east/west lethal edges remain bounded");
        }
        geometry.OnMapEffect(0, 0x00020001);
        Check(module.Center == new WPos(-400, -400) && module.Bounds is ArenaBoundsCircle, "enter circular add arena");
        Check(!module.Bounds.Contains(new WDir(25, 0)), "railway scenery is not navigable floor");
        geometry.OnMapEffect(0, 0x00080004);
        Check(module.Center == new WPos(100, 250), "return to car four on recorded environment event");
        geometry.OnMapEffect(10, 0x00020001);
        Check(module.Bounds.Contains(new WPos(100, 250) - module.Center) && module.Bounds.Contains(new WPos(100, 300) - module.Center), "both cars exist during transfer");
        Check(!module.Bounds.Contains(new WPos(100, 275) - module.Center), "lethal gap is not a walkable corridor");
        var player = Actor(5, 0, 100, 0, 250);
        Check(Hints(module, geometry, player).Teleporters.Count == 1, "escape connection only opens with the pad");
        geometry.OnMapEffect(10, 0x00080004);
        Check(Hints(module, geometry, player).Teleporters.Count == 0, "escape connection clears when pad closes");
        geometry.OnMapEffect(24, 0x00400040);
        Check(module.Center == new WPos(100, 300), "car five uses the encoded environment state");
        using var fresh = Module(3);
        Check(fresh.Center == new WPos(100, 200), "fresh module reconstructs current car");
        using var reset = Module();
        Check(reset.Center == new WPos(100, 100), "new pull resets arena");
        using var lateIntermission = Module(4, true);
        Check(lateIntermission.Center == new WPos(-400, -400), "existing live intermission add recovers the circle on bootstrap");
        var transfer = Component<CastHint>(reset, "RunawayTrain");
        transfer.OnCastStarted(reset.PrimaryActor, Cast(45638, reset.PrimaryActor.Position));
        Check(Hints(reset, transfer, player).PredictedDamage.Count == 0, "harmless intermission transport does not predict damage");
        var windpipe = Component<SimpleKnockbacks>(reset, "WindpipeDrawIn");
        windpipe.OnCastStarted(reset.PrimaryActor, Cast(45667, new(100, 85)));
        Check(!windpipe.Casters[0].IgnoreImmunes, "Windpipe movement respects knockback immunity");
    }
    private static void FloorsAndSources()
    {
        using var module = Module(3);
        var emission = Component<GenericAOEs>(module, "HeadOnEmission");
        var first = Actor(5, 0x233C, 100, 0, 165);
        var second = Actor(6, 0x4A33, 100, 5, 185);
        var player = Actor(7, 0, 100, 0, 200);
        var breath = Cast(45635, first.Position);
        var headlight = Cast(45637, second.Position);
        emission.OnCastStarted(first, breath);
        emission.OnCastStarted(first, breath);
        Check(emission.ActiveAOEs(0, player).Length == 1, "repeated start cannot duplicate floor hazard");
        var hints = Hints(module, emission, player);
        Check(Forbidden(hints, new(100, 200)) && !Forbidden(hints, new(92.5f, 205)), "breath forbids ground and keeps raised destinations available");
        Check(hints.ForbiddenZones[0].activation == Now.AddSeconds(10.3f), "floor AI receives actual cast activation");
        emission.OnCastStarted(second, headlight);
        emission.OnCastFinished(first, breath);
        Check(emission.ActiveAOEs(0, player).Length == 1, "one caster finishing does not clear another caster");
        hints = Hints(module, emission, player);
        Check(!Forbidden(hints, new(100, 200)) && Forbidden(hints, new(92.5f, 205)), "headlight forbids platforms and leaves ground available");
        emission.OnActorDestroyed(second);
        Check(emission.ActiveAOEs(0, player).Length == 0, "destruction removes its floor warning");
        var upper = Component<GenericAOEs>(module, "ElectrayUpper");
        upper.OnCastStarted(second, Cast(45630, new(85, 205)));
        var third = Actor(8, 0x4A32, 85, 5, 210);
        upper.OnCastStarted(third, Cast(45630, new(85, 210)));
        upper.OnCastFinished(second, Cast(45630, new(85, 205)));
        Check(upper.ActiveAOEs(0, player).Length == 1, "simultaneous upper turrets retire independently");
        var shape = (AOEShapeRect)upper.ActiveAOEs(0, player)[0].Shape;
        Check(shape.LengthFront == 25, "upper turret uses the matching Action-sheet range");
    }
    private static void Signals()
    {
        using var module = Module();
        var signal = Component<GenericAOEs>(module, "LevinSignal");
        var upper = Actor(5, 0x4A31, 92.5f, 5, 85);
        signal.OnActorCreated(upper);
        Check(signal.ActiveAOEs(0, upper).Length == 0, "raised car-one portal does not invent a ground AOE");
        Component<BossComponent>(module, "CarGeometry").OnMapEffect(6, 0x00200001);
        upper.PosRot = new(92.5f, 5, 185, 0);
        signal.OnActorCreated(upper);
        signal.OnActorCreated(upper);
        Check(signal.ActiveAOEs(0, upper).Length == 1, "spawn warning is idempotent");
        var hints = Hints(module, signal, upper);
        Check(Forbidden(hints, new(92.5f, 205)) && !Forbidden(hints, new(92.5f, 190)), "upper beam is clipped to upper deck");
        signal.OnCastStarted(upper, Cast(45620, upper.Position));
        Check(signal.ActiveAOEs(0, upper).Length == 1 && signal.ActiveAOEs(0, upper)[0].Activation > Now.AddSeconds(7), "cast replaces estimate without duplicate warning");
        signal.OnCastFinished(upper, Cast(45620, upper.Position));
        Check(signal.ActiveAOEs(0, upper).Length == 0, "cancelled signal cast clears warning");
        signal.OnActorCreated(upper);
        signal.OnActorDestroyed(upper);
        Check(signal.ActiveAOEs(0, upper).Length == 0, "signal despawn clears prediction");
    }
    private static void TowersAndPreview()
    {
        using var module = Module(4);
        var tower = Component<GenericTowers>(module, "DerailmentSiegeCircle");
        var owner = Actor(5, 0x233C, 100, 0, 240);
        var damage = Actor(6, 0x233C, 100, 0, 240);
        var cast = Cast(45649, owner.Position);
        tower.OnCastStarted(owner, cast);
        tower.OnCastStarted(owner, cast);
        Check(tower.Towers.Count == 1 && tower.Towers[0].Activation < module.CastFinishAt(cast).AddSeconds(-2.8), "tower arrival precedes first of three pulses");
        tower.OnEventCast(damage, Hit(45650));
        Check(tower.Towers.Count == 1, "first tower pulse retains soak");
        tower.OnEventCast(damage, Hit(45650));
        Check(tower.Towers.Count == 1, "second tower pulse retains soak");
        tower.OnEventCast(damage, Hit(45651));
        Check(tower.Towers.Count == 0, "third tower pulse completes soak");
        tower.OnCastStarted(owner, cast);
        tower.OnEventCast(damage, Hit(47236));
        tower.OnEventCast(damage, Hit(47236));
        tower.OnEventCast(damage, Hit(47237));
        Check(tower.Towers.Count == 0, "Battering Arms uses its distinct three pulse IDs");
        tower.OnCastStarted(owner, cast);
        tower.OnCastFinished(owner, cast);
        Check(tower.Towers.Count == 0, "tower cancellation clears state");
        var preview = Component<GenericAOEs>(module, "ArcaneRevelation");
        var indicator = Actor(8, 0x4A36, 100, 0, 240);
        preview.OnActorCreated(indicator);
        preview.OnActorCreated(indicator);
        Check(preview.ActiveAOEs(0, owner).Length == 0, "harmless indicator spawn is not an active danger zone");
        Check(Hints(module, preview, owner).ForbiddenZones.Count == 0, "harmless movement does not force AI into speculative safe pockets");
        preview.OnActorDestroyed(indicator);
        Check(preview.ActiveAOEs(0, owner).Length == 0, "preview despawn clears state");
        var hail = Component<GenericAOEs>(module, "HailOfThunder");
        hail.OnCastStarted(owner, Cast(45659, owner.Position));
        Check(hail.ActiveAOEs(0, owner)[0].RestrictToArenaProjectionLayer == null, "locked Hail affects both heights");
    }

    private static void ExplosionDestinations()
    {
        // Independent diamond traversal expectations, including both directions.
        foreach (var (cue, dx, expected) in new[]
        {
            (45656u, -0.5f, new WPos(100, 310)),
            (45657u, -0.5f, new WPos(104.8f, 300)),
            (45656u, 0.5f, new WPos(100, 310)),
            (45657u, 0.5f, new WPos(95.2f, 300))
        })
        {
            using var module = Module(5);
            var prediction = Component<GenericAOEs>(module, "ArcaneRevelation");
            var indicator = Actor(5, 0x4A36, 100, 0, 290);
            prediction.OnActorCreated(indicator);
            prediction.OnEventCast(module.PrimaryActor, Hit(cue));
            Check(prediction.ActiveAOEs(0, indicator).Length == 0, "unknown travel direction remains explicit");
            indicator.PosRot = new(100 + dx, 0, 291, 0);
            prediction.Update();
            var aoe = prediction.ActiveAOEs(0, indicator).ToArray().Single();
            Check(aoe.Origin == expected && aoe.Origin != indicator.Position, "predict final vertex rather than moving indicator");
            Check(Math.Abs((aoe.Activation - Now).TotalSeconds - (cue == 45656 ? 7.8 : 10.8)) < 0.001, "forecast carries two/three-step impact deadline and conservative margin");
            Check(aoe.Risky && aoe.RestrictToArenaProjectionLayer == null, "forecast is actionable on both physical heights");
            var hints = Hints(module, prediction, indicator);
            var harmlessStart = new WPos(100, 290);
            Check(Forbidden(hints, expected), "AI receives dangerous destination early");
            if (cue == 45656)
                Check(!Forbidden(hints, harmlessStart), "harmless moving location remains available when outside final blast");
            prediction.OnCastStarted(indicator, Cast(45659, expected));
            Check(prediction.ActiveAOEs(0, indicator).Length == 0, "locked cast replaces early forecast");
        }
        using var stationaryModule = Module(4);
        var stationary = Component<GenericAOEs>(stationaryModule, "ArcaneRevelation");
        var first = Actor(5, 0x4A36, 100, 0, 240);
        stationary.OnEventCast(stationaryModule.PrimaryActor, Hit(47527));
        stationary.OnActorCreated(first);
        stationary.OnEventCast(stationaryModule.PrimaryActor, Hit(45656));
        Check(stationary.ActiveAOEs(0, first).Length == 1 && stationary.ActiveAOEs(0, first)[0].Origin == new WPos(100, 240), "car four first explosion is stationary and warns at its cue");
        stationary.OnEventCast(first, Hit(45659));
        stationary.OnEventCast(stationaryModule.PrimaryActor, Hit(45657));
        first.PosRot = new(99.5f, 0, 241, 0);
        stationary.Update();
        Check(stationary.ActiveAOEs(0, first)[0].Origin == new WPos(104.8f, 250), "second car four explosion follows the moving three-step pattern");
        stationary.OnActorDestroyed(first);
        Check(stationary.ActiveAOEs(0, first).Length == 0, "indicator destruction cancels forecast");
        using var lateModule = Module(4);
        var late = Component<GenericAOEs>(lateModule, "ArcaneRevelation");
        late.OnActorCreated(first);
        late.OnEventCast(lateModule.PrimaryActor, Hit(45656));
        Check(late.ActiveAOEs(0, first).Length == 0, "bootstrap without revelation history cannot assume a stationary first blast");
    }
}
