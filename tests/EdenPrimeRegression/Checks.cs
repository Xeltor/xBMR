using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Shadowbringers.Raid.E01EdenPrime;

static class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, float x = 100, float z = 100)
        => new(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 80, new Vector4(x, 0, z, 0));
    private static ActorCastInfo Cast(AID aid, WPos origin, float time = 4.7f, Angle rotation = default, ulong target = 0)
        => new() { Action = ActionID.MakeSpell(aid), Location = new(origin.X, 0, origin.Z), TotalTime = time, Rotation = rotation, TargetID = target };
    private static ActorCastEvent Hit(AID aid) => new(ActionID.MakeSpell(aid), 0, 0, 0, default, 0, 0, default);
    private static T Component<T>(BossModule m, string name) where T : BossComponent => (T)m.Components.Single(c => c.GetType().Name == name);
    private static E01EdenPrime Module()
    {
        var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(1, (uint)OID.Boss);
        ws.Actors.Actors.Add(1, boss);
        var m = new E01EdenPrime(ws, boss);
        m.StateMachine.Start(Now);
        return m;
    }
    private static AIHints Hints(BossComponent c, Actor player)
    {
        var hints = new AIHints();
        c.AddAIHints(0, player, default, hints);
        return hints;
    }
    private static bool Forbidden(AIHints hints, WPos p) => hints.ForbiddenZones.Any(z => z.shapeDistance.Distance(p) <= 0);
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        using var m = Module();
        var player = Actor(2, 0, 110, 110);
        m.WorldState.Actors.Actors.Add(2, player);
        Check(BossModuleRegistry.RegisteredModules[(uint)OID.Boss].ModuleType == typeof(E01EdenPrime), "generated module registry");
        Check(m.Components.Count == 21, "all component factories activate, including three intermission components");
        var bounds = m.Bounds;
        foreach (var delta in new[] { new WDir(19.49f, 0), new WDir(-19.49f, 0), new WDir(0, 19.49f), new WDir(0, -19.49f), new WDir(19.49f, 19.49f) })
            Check(bounds.Contains(delta), "inner floor remains usable");
        foreach (var delta in new[] { new WDir(19.51f, 0), new WDir(-19.51f, 0), new WDir(0, 19.51f), new WDir(0, -19.51f) })
            Check(!bounds.Contains(delta), "lethal border excluded");
        foreach (var aid in new[] { AID.DimensionalShift, AID.FragorMaximus, AID.EternalBreath })
        {
            foreach (var c in m.Components) c.OnEventCast(m.PrimaryActor, Hit(aid));
            Check(m.Bounds == bounds && m.Center == new WPos(100, 100), "scene changes preserve arena");
        }
        var light = Component<SimpleAOEs>(m, "PureLight");
        foreach (var corner in new[] { new WPos(87, 87), new WPos(113, 87), new WPos(87, 113), new WPos(113, 113) })
        {
            var forward = (m.Center - corner).Normalized();
            var cast = Cast(AID.PureLight, corner, 10.2f, Angle.FromDirection(forward));
            light.OnCastStarted(m.PrimaryActor, cast);
            var hints = Hints(light, player);
            Check(Forbidden(hints, m.Center), "center unsafe for every corner");
            Check(!Forbidden(hints, corner - 2 * forward), "rear safe pocket preserved");
            Check(hints.ForbiddenZones.Single().activation == Now.AddSeconds(10.5), "full early cast deadline reaches AI");
            light.OnCastFinished(m.PrimaryActor, cast);
            Check(light.ActiveAOEs(0, player).Length == 0, "cancelled or resolved corner cast retires");
        }
        var flare = Component<SimpleAOEs>(m, "EdensFlare");
        var fc = Cast(AID.EdensFlare, m.Center);
        flare.OnCastStarted(m.PrimaryActor, fc);
        Check(!Forbidden(Hints(flare, player), new(104.9f, 100)), "flare inner safe area");
        Check(Forbidden(Hints(flare, player), new(105.1f, 100)), "flare danger starts outside five yalms");
        flare.OnCastFinished(m.PrimaryActor, fc);
        var beam = Component<SimpleAOEs>(m, "PureBeam");
        var lost = Component<SimpleAOEs>(m, "ParadiseLost");
        var b1 = Actor(3, (uint)OID.Helper, 80, 100);
        var b2 = Actor(4, (uint)OID.Helper, 100, 80);
        var bc = Cast(AID.PureBeam, b1.Position, 2.7f, 90.Degrees());
        var bc2 = Cast(AID.PureBeam, b2.Position, 2.7f);
        var lc = Cast(AID.ParadiseLost, new(110, 110), 2.7f);
        beam.OnCastStarted(b1, bc);
        beam.OnCastStarted(b2, bc2);
        lost.OnCastStarted(b1, lc);
        Check(Forbidden(Hints(beam, player), new(100, 101.9f)), "beam half width is two");
        Check(!Forbidden(Hints(beam, player), new(105, 102.1f)), "adjacent space preserved");
        beam.OnCastFinished(b1, bc);
        Check(beam.ActiveAOEs(0, player).Length == 1 && lost.ActiveAOEs(0, player).Length == 1, "one beam ending retains other beam and ground circle");
        beam.OnCastFinished(b2, bc2);
        lost.OnCastFinished(b1, lc);
        var apathy = Component<IconStackSpread>(m, "ViceOfApathy");
        apathy.OnEventIcon(player, (uint)IconID.ViceOfApathy, player.InstanceID);
        apathy.OnEventIcon(player, (uint)IconID.ViceOfApathy, player.InstanceID);
        Check(apathy.Spreads.Count == 1, "repeated icon is idempotent");
        apathy.OnCastFinished(m.PrimaryActor, Cast(AID.ViceAndVirtueApathy, m.Center));
        Check(apathy.Spreads.Count == 0, "cancelled orb cast clears assignment");
        apathy.OnEventIcon(player, (uint)IconID.ViceOfApathy, player.InstanceID);
        m.WorldState.Actors.Actors[player.InstanceID] = Actor(2, 0);
        apathy.Update();
        Check(apathy.Spreads.Count == 0, "recreated actor does not inherit old assignment");
        m.WorldState.Actors.Actors[player.InstanceID] = player;
        m.WorldState.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        var second = Component<SingleTargetInstant>(m, "Heavensunder");
        var spear = Cast(AID.SpearOfParadise, m.Center, target: player.InstanceID);
        second.OnCastStarted(m.PrimaryActor, spear);
        Check(second.Targets.Count == 1 && Math.Abs((second.Targets[0].activation - Now).TotalSeconds - 6.46) < 0.001, "second hit predicted at first cast start");
        spear.EventHappened = true;
        second.OnCastFinished(m.PrimaryActor, spear);
        Check(second.Targets.Count == 1, "first buster cast finishing retains second-hit prediction");
        var heaven = new ActorCastEvent(ActionID.MakeSpell(AID.Heavensunder), player.InstanceID, 0, 0, default, 0, 0, default);
        second.OnEventCast(m.PrimaryActor, heaven);
        Check(second.Targets.Count == 0, "second damage retires prediction");
        spear.EventHappened = false;
        second.OnCastStarted(m.PrimaryActor, spear);
        second.OnCastFinished(m.PrimaryActor, spear);
        Check(second.Targets.Count == 0, "cancelled first buster clears second-hit prediction");
        var vanity = Component<GenericBaitAway>(m, "ViceOfVanity");
        var tank = Actor(5, 0, 90, 110);
        m.WorldState.Actors.Actors.Add(5, tank);
        vanity.OnEventIcon(player, (uint)IconID.ViceOfVanity, player.InstanceID);
        vanity.OnEventIcon(tank, (uint)IconID.ViceOfVanity, tank.InstanceID);
        var vc = Cast(AID.ViceAndVirtueVanity, m.Center); vc.EventHappened = true;
        vanity.OnCastFinished(m.PrimaryActor, vc);
        Check(vanity.CurrentBaits.Count == 2, "visual completion retains delayed line baits");
        vanity.OnEventCast(b1, Hit(AID.ViceOfVanity));
        vanity.OnEventCast(b1, Hit(AID.ViceOfVanity));
        Check(vanity.CurrentBaits.Count == 2, "first helper and repeated packet cannot clear second line");
        vanity.OnEventCast(b2, Hit(AID.ViceOfVanity));
        Check(vanity.CurrentBaits.Count == 0, "both helper impacts retire pair");
        vanity.OnEventIcon(tank, (uint)IconID.ViceOfVanity, tank.InstanceID);
        vanity.OnCastFinished(m.PrimaryActor, Cast(AID.ViceAndVirtueVanity, m.Center));
        Check(vanity.CurrentBaits.Count == 0, "cancelled line cast clears baits");
        using var reset = Module();
        Check(Component<GenericBaitAway>(reset, "ViceOfVanity").CurrentBaits.Count == 0
            && Component<IconStackSpread>(reset, "ViceOfApathy").Spreads.Count == 0, "fresh pull has no stale assignments");
        IntermissionLanding();
        GuardianRearPolicy();
        CinematicRaidwide();
        Console.WriteLine($"{assertions} Eden Prime regression assertions passed (synthetic callbacks, not physical or live acceptance).");
    }

    private static void Time(BossModule module, double seconds)
        => module.WorldState.Frame = new(Now.AddSeconds(seconds), 0, 0, 0, 0, 1);

    private static Actor NativeActor(BossModule module, ulong id, uint oid, WPos position, bool targetable = false, ActorType type = ActorType.Enemy)
    {
        module.WorldState.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, type, Class.None, 80,
            new Vector4(position.X, 0, position.Z, 0), 3f, default, targetable, false, 0, 0, 0));
        return module.WorldState.Actors.Find(id)!;
    }

    private static Actor NativePlayer(BossModule module)
    {
        var player = NativeActor(module, 2, 0, module.Center, true, ActorType.Player);
        module.WorldState.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        return player;
    }

    private static void IntermissionLanding()
    {
        using var module = Module();
        var world = module.WorldState;
        var player = NativePlayer(module);
        var landing = Component<BossComponent>(module, "ParadisalDive");
        var first = NativeActor(module, 30, (uint)OID.GuardianOfParadise, new(90, 100));
        var hints = Hints(landing, player);
        Check(hints.PredictedDamage.Single().Activation == Now.AddSeconds(9.7)
            && hints.ForbiddenZones.Single().activation == Now.AddSeconds(9.7), "actual untargetable Guardian creation supplies the early arrival-policy and damage deadline");
        Check(Forbidden(hints, module.Center) && !Forbidden(hints, new(100, 82)) && !Forbidden(hints, new(100, 118)),
            "proximity strategy preserves both north and south arrival regions without treating them as measured falloff boundaries");
        Check(module.Arena.InBounds(new(100, 81)) && module.Arena.InBounds(new(100, 119)), "authored arrival circles remain inside independently measured floor bounds");
        landing.OnActorCreated(first);
        Check(Hints(landing, player).PredictedDamage.Count == 1, "duplicate creation callback cannot duplicate a Guardian landing");
        Time(module, 1);
        var second = NativeActor(module, 31, (uint)OID.GuardianOfParadise, new(110, 100));
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, new()
        {
            Action = ActionID.MakeSpell(AID.ParadisalDive), TotalTime = 3.7f, ElapsedTime = .2f,
            Location = new(second.Position.X, 0, second.Position.Z)
        }));
        hints = Hints(landing, player);
        Check(hints.PredictedDamage.Count == 2 && hints.PredictedDamage.Any(p => Math.Abs((p.Activation - Now).TotalSeconds - 4.8) < .001),
            "simultaneous Guardians retain distinct forecasts and an actual cast replaces only its source's creation estimate");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(Hints(landing, player).PredictedDamage.Count == 1, "native cancellation retires only the actual landing cast");
        world.Execute(new ActorState.OpDestroy(second.InstanceID));
        world.Execute(new ActorState.OpTargetable(first.InstanceID, true));
        Check(Hints(landing, player).ForbiddenZones.Count == 0, "targetable arrival clears the remaining landing constraint immediately");
        var existing = NativeActor(module, 32, (uint)OID.GuardianOfParadise, new(90, 100));
        Time(module, 5);
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        landing = Component<BossComponent>(module, "ParadisalDive");
        Check(Hints(landing, player).PredictedDamage.Single().Activation == Now.AddSeconds(5),
            "mid-phase bootstrap gives an existing untargetable add immediate conservative guidance instead of inventing fresh creation age");
        world.Execute(new ActorState.OpCastInfo(existing.InstanceID, Cast(AID.ParadisalDive, existing.Position, 2.7f)));
        Check(Hints(landing, player).PredictedDamage.Single().Activation == Now.AddSeconds(8),
            "a current native cast supplies its actual remaining deadline after conservative bootstrap");
        world.Execute(new ActorState.OpCastEvent(existing.InstanceID, Hit(AID.ParadisalDive)));
        Check(Hints(landing, player).PredictedDamage.Count == 0, "actual landing damage clears its forecast");
        world.Execute(new ActorState.OpCastInfo(existing.InstanceID, null));
        var destroyed = NativeActor(module, 33, (uint)OID.GuardianOfParadise, new(110, 100));
        world.Execute(new ActorState.OpDestroy(destroyed.InstanceID));
        Check(Hints(landing, player).ForbiddenZones.Count == 0, "native despawn clears an uncast creation forecast");
        world.Execute(new ActorState.OpTargetable(existing.InstanceID, true));
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        landing = Component<BossComponent>(module, "ParadisalDive");
        Check(Hints(landing, player).PredictedDamage.Count == 0, "reset with only arrived targetable Guardians retains no landing history");
    }

    private static void GuardianRearPolicy()
    {
        using var module = Module();
        var world = module.WorldState;
        var tank = NativePlayer(module); tank.PosRot.Z = 106;
        var bystander = NativeActor(module, 3, 0, new(100, 103), true, ActorType.Player);
        var guardian = NativeActor(module, 40, (uint)OID.GuardianOfParadise, module.Center, true);
        world.Execute(new ActorState.OpTarget(guardian.InstanceID, tank.InstanceID));
        var policy = Component<Cleave>(module, "GuardianRearSafety");
        var hints = Hints(policy, bystander);
        Check(Forbidden(hints, new(100, 104)) && !Forbidden(hints, new(100, 96)),
            "targetable Guardian applies the documented conservative front-half positioning policy and leaves its rear available");
        Check(!Forbidden(hints, new(100, 110)), "rear policy retains its bounded nine-yalm near-add envelope rather than claiming a room-wide cone");
        var personal = new BossComponent.TextHints(); policy.AddHints(0, tank, personal);
        Check(personal.Count == 0, "the actual tank target is exempt from the non-target cleave warning");
        world.Execute(new ActorState.OpCastInfo(guardian.InstanceID, Cast(AID.ManaBurst, guardian.Position)));
        Check(Hints(policy, bystander).ForbiddenZones.Count == 0, "current Guardian casts suppress instant-cleave positioning pressure");
        world.Execute(new ActorState.OpCastInfo(guardian.InstanceID, null));
        Check(Hints(policy, bystander).ForbiddenZones.Count == 1, "native cancellation restores the living Guardian's rear-side policy");
        world.Execute(new ActorState.OpTargetable(guardian.InstanceID, false));
        Check(Hints(policy, bystander).ForbiddenZones.Count == 0, "untargetable landing Guardians do not apply the rear-side policy");
        world.Execute(new ActorState.OpTargetable(guardian.InstanceID, true));
        world.Execute(new ActorState.OpDestroy(guardian.InstanceID));
        Check(Hints(policy, bystander).ForbiddenZones.Count == 0, "native destruction removes the Guardian policy source");
    }

    private static void CinematicRaidwide()
    {
        using var module = Module();
        var world = module.WorldState;
        var player = NativePlayer(module);
        var component = Component<BossComponent>(module, "EternalBreath");
        var wrongScene = NativeActor(module, 50, 2007457, new(110, 100), type: ActorType.EventObj);
        world.Execute(new ActorState.OpEventObjectAnimation(wrongScene.InstanceID, 256, 512));
        Check(Hints(component, player).PredictedDamage.Count == 0, "same scene OID away from the arena owner cannot arm the cinematic prediction");
        var scene = NativeActor(module, 51, 2007457, module.Center, type: ActorType.EventObj);
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 256, 512));
        Check(Hints(component, player).PredictedDamage.Single().Activation == Now.AddSeconds(48.5),
            "the exact arena scene animation supplies the early cinematic damage deadline");
        Time(module, 1);
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 256, 512));
        Check(Hints(component, player).PredictedDamage.Single().Activation == Now.AddSeconds(48.5),
            "repeated initial cinematic cue cannot postpone the initial forecast");
        var eden = NativeActor(module, 52, (uint)OID.Eden, module.Center);
        Time(module, 5);
        world.Execute(new ActorState.OpCastEvent(eden.InstanceID, Hit(AID.PrimevalStasis)));
        Check(Hints(component, player).PredictedDamage.Single().Activation == Now.AddSeconds(48.4),
            "the actual Eden Primeval Stasis action refines the cinematic deadline");
        Time(module, 30);
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 1024, 2048));
        Check(Hints(component, player).PredictedDamage.Single().Activation == Now.AddSeconds(48.5),
            "the later exact cinematic cue updates the remaining damage time");
        Check(Hints(component, player).ForbiddenZones.Count == 0 && Hints(component, player).GoalZones.Count == 0,
            "cinematic damage prediction does not invent spatial danger or movement goals");
        var helper = NativeActor(module, 53, (uint)OID.Helper, module.Center);
        world.Execute(new ActorState.OpCastEvent(helper.InstanceID, Hit(AID.EternalBreath)));
        var text = new BossComponent.GlobalHints(); component.AddGlobalHints(player, text);
        Check(Hints(component, player).PredictedDamage.Count == 0 && text.Count == 0, "actual helper raidwide resolves the cinematic prediction and text");
        world.Execute(new ActorState.OpCastEvent(eden.InstanceID, Hit(AID.PrimevalStasis)));
        world.Execute(new ActorState.OpDestroy(eden.InstanceID)); component.Update();
        Check(Hints(component, player).PredictedDamage.Count == 0, "lost Eden source clears its action-backed cinematic forecast");
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 256, 512));
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 2, 32768));
        Check(Hints(component, player).PredictedDamage.Count == 0, "exact encounter-completion scene animation clears the remaining forecast");
        world.Execute(new ActorState.OpEventObjectAnimation(scene.InstanceID, 256, 512));
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        component = Component<BossComponent>(module, "EternalBreath");
        Check(Hints(component, player).PredictedDamage.Count == 0,
            "reset does not invent elapsed timing from a historical scene animation that native bootstrap cannot recover");
    }
}
