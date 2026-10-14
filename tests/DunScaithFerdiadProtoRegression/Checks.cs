using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A32FerdiadHollow;
using BossMod.Heavensward.Alliance.A33ProtoUltima;

static class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 16, 0, 0, DateTimeKind.Utc);
    private static int _assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++_assertions;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos) => new(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
        new Vector4(pos.X, 0, pos.Z, 0));
    private static ActorCastInfo Cast(Actor actor, uint aid, float duration)
        => actor.CastInfo = new() { Action = new(ActionType.Spell, aid), Location = new(actor.Position.X, 0, actor.Position.Z), TotalTime = duration };
    private static ActorCastEvent Hit(uint aid, ulong target = 0) => new(new(ActionType.Spell, aid), target, 0, 0, default, 0, 0, default);
    private static BossComponent Component(BossModule module, string name) => module.Components.Single(c => c.GetType().Name == name);
    private static void Time(BossModule module, double seconds) => module.WorldState.Frame = new(Now.AddSeconds(seconds), 0, 0, 0, 0, 1);
    private static AIHints Hints(BossComponent c, Actor actor)
    {
        var hints = new AIHints();
        c.AddAIHints(0, actor, default, hints);
        return hints;
    }
    private static BossModule Module(bool proto = false)
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, proto ? 0x1967u : 0x1928u, proto ? new(-350, -50) : new(-350, 225));
        world.Actors.Actors.Add(boss.InstanceID, boss);
        BossModule module = proto ? new A33ProtoUltima(world, boss) : new A32FerdiadHollow(world, boss);
        module.StateMachine.Start(Now);
        return module;
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ActionData(game);
        Ferdiad();
        BlackfireExpansion();
        DebilitatorColors();
        TransportedCharges();
        Proto();
        ProtoLasers();
        ProtoBitDonutWarnings();
        EikonizerNativeLifecycle();
        Console.WriteLine($"{_assertions} Ferdiad/Proto assertions passed (synthetic callbacks; no independent physical/live proof).");
    }
    private static void ActionData(Lumina.GameData game)
    {
        var sheet = game.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Lumina.Data.Language.English)!;
        foreach (var (id, type, range, width) in new (uint, int, int, int)[]
        {
            (7241, 1, 0, 0), (7324, 1, 0, 0), (7325, 1, 0, 0),
            (7329, 2, 20, 0), (7330, 2, 75, 0), (7332, 2, 30, 0),
            (7326, 8, 0, 6), (7766, 8, 0, 6), (7342, 8, 0, 6), (7343, 8, 0, 6),
            (7340, 2, 5, 0), (7344, 2, 5, 0), (7345, 2, 4, 0),
            (7581, 5, 40, 0), (7582, 5, 15, 0), (7583, 3, 20, 0), (7588, 5, 31, 0),
            (7589, 2, 3, 0), (7593, 5, 40, 0), (7596, 2, 8, 0), (7597, 2, 40, 0)
        })
        {
            var row = sheet.GetRow(id);
            Check(row.CastType == type && row.EffectRange == range && row.XAxisModifier == width, "current Action fields corroborate pinned reference " + id);
        }
    }

    private static void ProtoLasers()
    {
        foreach (var secondID in new uint[] { 7575, 7576 })
        {
            using var module = Module(true);
            var component = (GenericAOEs)Component(module, "AetherochemicalLasers");
            var boss = module.PrimaryActor;
            var first = Cast(boss, 7574, 3);
            first.Rotation = (-0.5021467f).Radians();
            component.OnCastStarted(boss, first);
            component.OnCastStarted(boss, first);
            var rays = component.ActiveAOEs(0, boss).ToArray();
            Check(rays.Length == 3, "first laser immediately forecasts all three rays; duplicate cue is idempotent");
            Check(rays.Count(r => Math.Abs((r.Rotation - first.Rotation).Normalized().Rad) < .001f) == 1
                && rays.Count(r => Math.Abs(Math.Abs((r.Rotation - first.Rotation).Normalized().Rad) - MathF.PI / 4) < .001f) == 2, "side laser forecasts are exactly +/-45 degrees from actual first cast");
            Check(rays[0].Activation == module.CastFinishAt(first) && rays.Skip(1).All(r => r.Activation == module.CastFinishAt(first).AddSeconds(4)), "unknown side order retains earliest possible side deadline");
            Check(Hints(component, boss).ForbiddenZones.Count == 3, "AI receives future laser deadlines at first cue");
            component.OnEventCast(boss, Hit(7574));
            component.OnCastFinished(boss, first);
            Check(component.ActiveAOEs(0, boss).Length == 2, "first hit/completion retires only first ray");
            Time(module, 4.4);
            var second = Cast(boss, secondID, 2.7f);
            second.Rotation = first.Rotation + (secondID == 7576 ? 45 : -45).Degrees();
            component.OnCastStarted(boss, second);
            rays = component.ActiveAOEs(0, boss).ToArray();
            Check(rays.Length == 2 && rays[0].Activation == module.CastFinishAt(second), "actual second cast replaces its forecast in either order");
            Check(rays[1].Activation == module.CastFinishAt(second).AddSeconds(4), "identifying second ray postpones remaining ray to its own future deadline");
            boss.PosRot.X += 1;
            Check(component.ActiveAOEs(0, boss).ToArray().All(r => r.Origin == boss.Position), "remaining forecasts follow actual source position");
            component.OnEventCast(boss, Hit(secondID));
            component.OnCastFinished(boss, second);
            Check(component.ActiveAOEs(0, boss).Length == 1, "second ray resolution retains remaining side");
            Time(module, 8.5);
            var thirdID = secondID == 7575 ? 7576u : 7575u;
            var third = Cast(boss, thirdID, 2.7f);
            third.Rotation = first.Rotation + (thirdID == 7576 ? 45 : -45).Degrees();
            component.OnCastStarted(boss, third);
            component.OnEventCast(boss, Hit(thirdID));
            component.OnCastFinished(boss, third);
            Check(component.ActiveAOEs(0, boss).Length == 0 && Hints(component, boss).ForbiddenZones.Count == 0, "third hit clears complete sequence");
            Time(module, 0);
            component.OnCastStarted(boss, first);
            component.OnCastFinished(boss, first);
            Check(component.ActiveAOEs(0, boss).Length == 0, "first cast cancellation withdraws unconfirmed sides");
            component.OnCastStarted(boss, second);
            Check(component.ActiveAOEs(0, boss).Length == 1, "mid-sequence load adds only actual side cast");
            component.OnActorDestroyed(boss);
            Check(component.ActiveAOEs(0, boss).Length == 0, "source destruction clears sequence");
            component.OnCastStarted(boss, first);
            component.OnActorUntargetable(boss);
            Check(component.ActiveAOEs(0, boss).Length == 0, "phase exit clears forecast");
            component.OnCastStarted(boss, first);
            component.OnCastStarted(boss, Cast(boss, 7762, 5));
            Check(component.ActiveAOEs(0, boss).Length == 0, "unrelated boss cast clears an interrupted sequence");
            component.OnCastStarted(boss, first);
            Time(module, 8.5);
            component.Update();
            Check(component.ActiveAOEs(0, boss).Length == 0, "missing continuation cannot leave stale laser hazards");
        }
    }
    private static void ProtoBitDonutWarnings()
    {
        using var module = Module(true);
        var world = module.WorldState;
        var component = (CastHint)Component(module, "IntegratedAetheromodulator");
        var player = Actor(1, 0, module.Center);
        Actor Create(ulong id, uint oid = 0x1968)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
                new Vector4(-341.5865f, 1, -34.63113f, 0), .6f, default, true, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        ActorCastInfo Start(Actor source, float elapsed = .1f, uint action = 7582)
        {
            var cast = new ActorCastInfo { Action = new(ActionType.Spell, action), TotalTime = 2.5f, ElapsedTime = elapsed,
                Location = new(source.Position.X, 1, source.Position.Z) };
            world.Execute(new ActorState.OpCastInfo(source.InstanceID, cast));
            return source.CastInfo!;
        }
        string Warning()
        {
            var hints = new BossComponent.GlobalHints(); component.AddGlobalHints(player, hints);
            return hints.SingleOrDefault() ?? "";
        }
        var first = Create(363);
        var second = Create(364);
        var firstCast = Start(first);
        var secondCast = Start(second, 1.1f);
        Check(component.Casters.Count == 2 && Warning().Contains("bounds unverified") && Warning().EndsWith("1.7s left"),
            "distinct native bit casts warn immediately and show the nearest actual corrected remaining timer");
        component.OnCastStarted(first, firstCast); component.OnCastStarted(second, secondCast);
        Check(component.Casters.Count == 2, "duplicate start callbacks cannot duplicate source-bound bit warnings");
        secondCast.ElapsedTime += .5f;
        Check(Warning().EndsWith("1.2s left"), "warning countdown consumes current native cast progress instead of a guessed fixed delay");
        var ai = Hints(component, player);
        Check(ai.ForbiddenZones.Count == 0 && ai.GoalZones.Count == 0 && ai.PredictedDamage.Count == 0,
            "ring topology alone creates no hard donut bounds, safe-region goal or fabricated damage prediction");
        world.Execute(new ActorState.OpCastEvent(first.InstanceID, Hit(7582)));
        Check(component.Casters.Count == 1 && component.Casters.Single() == second, "actual effect clears only its own bit warning");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(component.Casters.Count == 1, "late completion of an already-resolved source leaves other casts intact");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(component.Casters.Count == 0 && Warning() == "", "native early cancellation clears the final warning immediately");

        var staleCast = Start(first);
        var oldFirst = first;
        first = Create(first.InstanceID);
        Check(component.Casters.Count == 0, "native actor-ID replacement withdraws the old generation immediately");
        var freshCast = Start(first);
        component.OnCastStarted(oldFirst, staleCast);
        component.OnCastFinished(oldFirst, staleCast);
        component.OnActorDestroyed(oldFirst);
        component.OnEventCast(oldFirst, Hit(7582));
        Check(component.Casters.Count == 1 && component.Casters.Single() == first,
            "stale-generation start, finish, destruction and action callbacks cannot remove or duplicate the replacement warning");
        Start(first, .2f);
        component.OnCastFinished(first, freshCast);
        Check(component.Casters.Count == 1, "a late same-generation finish cannot clear a newer actual cast");
        world.Execute(new ActorState.OpDestroy(first.InstanceID));
        Check(component.Casters.Count == 0 && Warning() == "", "native despawn clears a live bit cast");

        Start(second, .8f);
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        component = (CastHint)Component(module, "IntegratedAetheromodulator");
        Check(component.Casters.Count == 1 && component.Casters.Single() == second && Warning().EndsWith("2.0s left"),
            "mid-cast component bootstrap reconstructs only the actual live native cast with its remaining timer");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        component = (CastHint)Component(module, "IntegratedAetheromodulator");
        Check(component.Casters.Count == 0 && Warning() == "", "reset after cancellation cannot retain warning history");
        var otherOID = Create(365, 0x196A);
        Start(otherOID); Start(second, action: 7583);
        Check(component.Casters.Count == 0, "same action on an unexpected actor type and unrelated bit casts do not create this warning");
        Time(module, 0);
        Start(second);
        component.OnCastStarted(second, second.CastInfo!);
        Time(module, 4.8); component.Update();
        Check(component.Casters.Count == 0 && Warning() == "", "lost native cleanup is bounded at corrected cast finish plus two seconds");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Start(second);
        second.CastInfo = null;
        component.Update();
        Check(component.Casters.Count == 0, "stale world cast data is withdrawn even without its completion callback");
    }
    private static void EikonizerNativeLifecycle()
    {
        using var module = Module(true);
        var world = module.WorldState;
        var component = Component(module, "Eikonizer");
        var player = Actor(1, 0, module.Center); player.Type = ActorType.Player;
        world.Actors.Actors.Add(player.InstanceID, player);
        world.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        Actor Create(ulong id)
        {
            world.Execute(new ActorState.OpCreate(id, 0x196C, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
                new Vector4(-350, 1, -50, 0), 1.5f, default, false, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        void Mark(Actor orb) => world.Execute(new ActorState.OpIcon(orb.InstanceID, 62, orb.InstanceID));
        var orb = Create(355);
        Mark(orb);
        var firstDeadline = Hints(component, player).PredictedDamage.Single().Activation;
        Check(firstDeadline == Now.AddSeconds(5.4), "native Eikonizer marker starts the evidence-backed early client deadline");
        Time(module, 3);
        Mark(orb);
        Check(Hints(component, player).PredictedDamage.Single().Activation == firstDeadline && Hints(component, player).GoalZones.Count == 1,
            "duplicate native marker across time cannot postpone damage or duplicate gathering guidance");
        Time(module, 7.3); component.Update();
        Check(Hints(component, player).GoalZones.Count == 1, "missing resolution retains the warning through its bounded grace interval");
        Time(module, 7.4); component.Update();
        var text = new BossComponent.GlobalHints(); component.AddGlobalHints(player, text);
        Check(Hints(component, player).GoalZones.Count == 0 && Hints(component, player).PredictedDamage.Count == 0 && text.Count == 0,
            "missing resolution clears text, gathering and damage forecast at first activation plus two seconds");
        Time(module, 8); Mark(orb);
        Check(Hints(component, player).GoalZones.Count == 0, "late duplicate from the same unresolved generation cannot restart an expired forecast");
        world.Execute(new ActorState.OpCastEvent(orb.InstanceID, Hit(7596)));
        Time(module, 8.1); Mark(orb);
        Check(Math.Abs((Hints(component, player).PredictedDamage.Single().Activation - Now.AddSeconds(13.5)).Ticks) <= 1,
            "late actual resolution permits a genuinely fresh marker wave on the same source");
        world.Execute(new ActorState.OpCastEvent(orb.InstanceID, Hit(7596)));
        Check(Hints(component, player).GoalZones.Count == 0 && Hints(component, player).PredictedDamage.Count == 0,
            "normal native own-orb action clears both guidance and healing deadline");
        Time(module, 9); Mark(orb);
        var old = orb;
        Time(module, 9.5); orb = Create(orb.InstanceID);
        Check(Hints(component, player).GoalZones.Count == 0, "native generation replacement clears the old orb immediately");
        component.OnEventIcon(old, 62, old.InstanceID);
        Check(Hints(component, player).GoalZones.Count == 0, "stale-generation icon cannot create gathering guidance");
        Mark(orb);
        component.OnActorDestroyed(old); component.OnEventCast(old, Hit(7596));
        Check(Math.Abs((Hints(component, player).PredictedDamage.Single().Activation - Now.AddSeconds(14.9)).Ticks) <= 1,
            "fresh generation receives a fresh deadline while old destruction and resolution are ignored");
        world.Execute(new ActorState.OpDead(orb.InstanceID, true));
        Mark(orb);
        Check(Hints(component, player).GoalZones.Count == 0 && Hints(component, player).PredictedDamage.Count == 0,
            "native source death clears guidance and dead-source icons cannot restore it");
        Time(module, 15);
        var next = Create(356); Mark(next);
        Check(Math.Abs((Hints(component, player).PredictedDamage.Single().Activation - Now.AddSeconds(20.4)).Ticks) <= 1,
            "a fresh orb wave starts independently of expired or dead prior generations");
        world.Execute(new ActorState.OpDestroy(next.InstanceID));
        Check(Hints(component, player).GoalZones.Count == 0, "native source destruction clears the fresh wave");
        var resetOrb = Create(357); Mark(resetOrb);
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        component = Component(module, "Eikonizer");
        Check(Hints(component, player).GoalZones.Count == 0 && Hints(component, player).PredictedDamage.Count == 0,
            "module reset discards marker history rather than inventing a new unresolved stack");
        Mark(resetOrb);
        Check(Hints(component, player).GoalZones.Count == 1 && Hints(component, player).ForbiddenZones.Count == 0,
            "fresh post-reset marker restores only the existing soft preference without a hard safe radius");
    }
    private static void Ferdiad()
    {
        using var module = Module();
        Check(module.Components.Any(c => c.GetType().Name == "Icefall") && module.Components.Any(c => c.GetType().Name == "LuckyPierrot2"), "add-phase attacks have generated active components");
        var player = Actor(1, 0, new(-350, 225));
        var portal = Actor(2, 0x1929, new(-350, 206));
        var second = Actor(3, 0x192A, new(-350, 244));
        var wormhole = (GenericAOEs)Component(module, "Wormhole");
        var visual = Cast(portal, 7324, 8.2f);
        wormhole.OnCastStarted(portal, visual);
        wormhole.OnCastStarted(portal, visual);
        Check(wormhole.ActiveAOEs(0, player).Length == 1, "portal prediction is idempotent");
        var aoe = wormhole.ActiveAOEs(0, player)[0];
        Check(aoe.Origin.AlmostEqual(new(-350, 210.1f), 0.01f), "exit forecast uses inward projectile stop, not portal centre");
        Check(Math.Abs((aoe.Activation - Now).TotalSeconds - 11.9) < 0.01, "prediction includes visual-to-explosion interval");
        Time(module, 8.5);
        wormhole.OnCastFinished(portal, visual);
        Check(wormhole.ActiveAOEs(0, player).Length == 1, "successful portal transport cannot retire explosion warning");
        wormhole.OnCastStarted(second, Cast(second, 7324, 8.2f));
        Check(wormhole.ActiveAOEs(0, player).Length == 2, "simultaneous exits remain separate");
        var projectile = Actor(4, 0x192B, aoe.Origin);
        wormhole.OnCastStarted(projectile, Cast(projectile, 7329, 0.7f));
        Check(wormhole.ActiveAOEs(0, player).Length == 2, "actual explosion replaces matching forecast without clearing other exit");
        Time(module, 9.5);
        wormhole.OnEventCast(projectile, Hit(7329));
        Check(wormhole.ActiveAOEs(0, player).Length == 1, "one explosion retires only its own warning");
        wormhole.OnActorDestroyed(second);
        Check(wormhole.ActiveAOEs(0, player).Length == 0, "destroyed source clears remaining forecast");
        Time(module, 0);
        wormhole.OnCastStarted(portal, visual);
        Time(module, 2);
        wormhole.OnCastFinished(portal, visual);
        Check(wormhole.ActiveAOEs(0, player).Length == 0, "cancelled portal clears prediction");
        var bait = (GenericBaitAway)Component(module, "BlackWind");
        var p2 = Actor(5, 0, new(-340, 225));
        bait.OnEventIcon(player, 23, player.InstanceID);
        bait.OnEventIcon(p2, 23, p2.InstanceID);
        bait.OnEventIcon(player, 23, player.InstanceID);
        Check(bait.CurrentBaits.Count == 2, "duplicate icon does not duplicate bait");
        bait.OnEventCast(module.PrimaryActor, Hit(7340, p2.InstanceID));
        Check(bait.CurrentBaits.Count == 1 && bait.CurrentBaits[0].Target == player, "hit removes actual target instead of first icon target");
        var raidwide = Component(module, "FerdiadRaidwides");
        var jig = Cast(module.PrimaryActor, 7332, 4.7f);
        raidwide.OnCastStarted(module.PrimaryActor, jig);
        Check(Hints(raidwide, player).ForbiddenZones.Count == 0, "Jester's Jig does not invent a dodgeable central circle");
        var text = new BossComponent.GlobalHints();
        raidwide.AddGlobalHints(player, text);
        Check(text.Count == 1 && text[0].Contains("Jester's Jig"), "Jester's Jig warns at visual cast start");
        Time(module, 7);
        raidwide.OnCastFinished(module.PrimaryActor, jig);
        text.Clear(); raidwide.AddGlobalHints(player, text);
        Check(text.Count == 1, "raidwide warning survives visual completion");
        raidwide.OnEventCast(projectile, Hit(7333));
        text.Clear(); raidwide.AddGlobalHints(player, text);
        Check(text.Count == 0, "helper damage clears raidwide warning");
        // Exercise native creation dispatch rather than manually populating both world and lazy enemy cache.
        module.WorldState.Execute(new ActorState.OpCreate(6, 0x194E, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
            new Vector4(-360, 0, 225, 0), 1, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
        var scythe = module.WorldState.Actors.Find(6)!;
        var zones = (GenericAOEs)Component(module, "DeathScythe");
        Check(zones.ActiveAOEs(0, player).Length == 1, "existing spawned scythe is constrained");
        module.WorldState.Execute(new ActorState.OpDestroy(scythe.InstanceID));
        Check(zones.ActiveAOEs(0, player).Length == 0, "destroyed scythe is no longer hazardous");
        var debilitator = Component(module, "Debilitator");
        var weakness = Cast(module.PrimaryActor, 7334, 2.7f);
        debilitator.OnCastStarted(module.PrimaryActor, weakness);
        text.Clear(); debilitator.AddGlobalHints(player, text);
        Check(text.Count == 1 && text[0].Contains("opposite element"), "Debilitator preparation warns from earliest cast before status arrives");
        debilitator.OnCastFinished(module.PrimaryActor, weakness);
        text.Clear(); debilitator.AddGlobalHints(player, text);
        Check(text.Count == 0, "Debilitator cast completion or cancellation clears preparation hint");
        player.Statuses[0] = new(1137, 0, Now.AddMinutes(2), module.PrimaryActor.InstanceID);
        var personal = new BossComponent.TextHints();
        debilitator.AddHints(0, player, personal);
        Check(personal.Count == 1 && personal[0].Item1.Contains("blue (water)"), "fire weakness retains guide-supported opposite-colour response");
        player.Statuses[0] = new(1157, 0, Now.AddMinutes(2), module.PrimaryActor.InstanceID);
        personal.Clear(); debilitator.AddHints(0, player, personal);
        Check(personal.Count == 1 && personal[0].Item1.Contains("red (fire)"), "water weakness retains guide-supported opposite-colour response");
        Check(Hints(debilitator, player).ForbiddenZones.Count == 0, "colour guidance cannot invent a physical puddle constraint");
        using var reset = Module();
        Check(((GenericAOEs)Component(reset, "Wormhole")).ActiveAOEs(0, player).Length == 0, "new pull resets portal state");
    }
    private static void BlackfireExpansion()
    {
        using var module = Module();
        var world = module.WorldState;
        var component = (GenericAOEs)Component(module, "Blackfire2");
        var player = Actor(1, 0, module.Center);
        // Exact7339 cast/impact centres from the one recorded wave. These probes verify the conservative
        // estimate's useful safe space and observed hit/nonhit distinction, not an exact server radius.
        WPos[] centres = [new(-350.02673f, 203.02124f), new(-342.67188f, 224.90259f), new(-351.46106f, 227.25256f),
            new(-371.96918f, 225.0553f), new(-328.05377f, 224.96375f), new(-350.8202f, 235.4314f),
            new(-359.9146f, 236.56055f), new(-349.99622f, 246.96729f)];
        Actor Create(ulong id, WPos pos)
        {
            world.Execute(new ActorState.OpCreate(id, 0x192E, 0, 0, "", 0, ActorType.Helper, Class.None, 60,
                new Vector4(pos.X, -7, pos.Z, 0), .5f, default, false, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        ActorCastInfo Start(Actor helper)
        {
            var cast = new ActorCastInfo { Action = new(ActionType.Spell, 7339), TotalTime = 8.7f, ElapsedTime = .0172308f,
                Location = new(helper.Position.X, -7, helper.Position.Z) };
            world.Execute(new ActorState.OpCastInfo(helper.InstanceID, cast));
            return cast;
        }
        void Growth(Actor helper, ushort stacks)
            => world.Execute(new ActorState.OpStatus(helper.InstanceID, 0, new(618, stacks, Now.AddSeconds(20), 0)));
        ulong[] helperIDs = [295, 292, 289, 293, 296, 290, 291, 294];
        var helpers = centres.Select((p, i) => Create(helperIDs[i], p)).ToArray();
        var casts = helpers.Select(Start).ToArray();
        var aoes = component.ActiveAOEs(0, player).ToArray();
        var activation = aoes[0].Activation;
        Check(aoes.Length == 8 && aoes.All(a => a.Shape is AOEShapeCircle { Radius: 13.5f } && a.Risky),
            "all eight native helper casts immediately forecast conservative estimated final radius13.5");
        Check(Math.Abs((activation - Now).TotalSeconds - 8.982769) < .001 && Hints(component, player).ForbiddenZones.Count == 8,
            "AI receives the full expanded footprint at earliest cast cue with its future deadline");
        Time(module, 1);
        component.OnCastStarted(helpers[0], casts[0]);
        for (ushort stack = 0; stack <= 6; ++stack)
            Growth(helpers[0], stack);
        Check(component.ActiveAOEs(0, player).Length == 8 && component.ActiveAOEs(0, player)[0].Activation == activation
            && component.ActiveAOEs(0, player).ToArray().All(a => a.Shape is AOEShapeCircle { Radius: 13.5f }),
            "duplicate cast and every known native growth value preserve the original full footprint and deadline");
        Growth(helpers[0], 7);
        var text = new BossComponent.GlobalHints(); component.AddGlobalHints(player, text);
        Check(text.Count == 1 && text[0].Contains("uncertain") && Hints(component, player).ForbiddenZones.Count == 7
            && component.ActiveAOEs(0, player).ToArray().Single(a => a.ActorID == helpers[0].InstanceID).Risky == false,
            "unrecognized growth withdraws only that helper's hard AI claim and explicitly warns while retaining an informational estimate");
        Growth(helpers[0], 6);
        text.Clear(); component.AddGlobalHints(player, text);
        Check(text.Count == 0 && Hints(component, player).ForbiddenZones.Count == 8,
            "returning to the known growth family restores the bounded estimate without duplicating helpers");
        Growth(helpers[0], 9);
        world.Execute(new ActorState.OpStatus(helpers[0].InstanceID, 0, default));
        Check(Hints(component, player).ForbiddenZones.Count == 8, "native growth loss restores the unexpanded case inside the conservative estimate");

        var recordedHit = new WPos(-345.84576f, 214.92322f);
        Check(aoes.Count(a => a.Check(recordedHit)) == 2 && !aoes[2].Check(recordedHit)
            && centres.All(p => !recordedHit.InCircle(p, 7)),
            "estimated expansion covers both simultaneous recorded actor6 hits and excludes its third nonhit; radius7 missed both");
        WPos[] recordedNonhits = [new(-337.40207f, 212.20615f), new(-359.6491f, 216.0532f), new(-338.79608f, 211.44421f),
            new(-359.73145f, 215.56409f), new(-337.27502f, 240.20483f), new(-359.71152f, 214.36583f),
            new(-362.46762f, 211.25362f), new(-361.27994f, 212.95406f), new(-363.19928f, 210.83917f),
            new(-362.50674f, 212.96176f), new(-365.44415f, 203.7269f), new(-374.74634f, 241.01624f),
            new(-364.0733f, 211.16954f), new(-366.64337f, 209.98409f), new(-366.0182f, 204.79126f),
            new(-334.0658f, 242.45056f), new(-367.14734f, 207.599f), new(-368.09567f, 206.6714f),
            new(-330.64777f, 243.3966f), new(-370.91327f, 205.97655f), new(-330.9201f, 244.425f), new(-370.22968f, 203.57056f)];
        Check(recordedNonhits.All(p => module.Arena.InBounds(p) && aoes.All(a => !a.Check(p))),
            "all22 retained living nonhit positions remain inside the arena and outside the conservative final union");
        var safeCells = 0;
        for (var x = -380f; x <= -320; x += .5f)
            for (var z = 195f; z <= 255; z += .5f)
                if (module.Arena.InBounds(new(x, z)) && aoes.All(a => !a.Check(new(x, z)))) ++safeCells;
        Check(safeCells > 1000, "all eight conservative final circles retain substantial feasible space inside current radius30 bounds");

        // Actual helper effects retire independently, even when normal completion arrives before an effect.
        Time(module, 8.98);
        world.Execute(new ActorState.OpCastInfo(helpers[0].InstanceID, null));
        Check(component.ActiveAOEs(0, player).Length == 8, "normal cast completion retains pending damage guidance");
        world.Execute(new ActorState.OpCastEvent(helpers[0].InstanceID, Hit(7339)));
        Check(component.ActiveAOEs(0, player).Length == 7, "native damage retires only its exact helper");
        var stale = helpers[1];
        var replacement = Create(stale.InstanceID, centres[1]);
        Check(component.ActiveAOEs(0, player).Length == 6, "native actor generation replacement withdraws stale geometry");
        Start(replacement);
        component.OnActorDestroyed(stale); component.OnEventCast(stale, Hit(7339));
        component.OnCastStarted(stale, casts[1]);
        var staleGrowth = new ActorStatus(618, 8, Now.AddSeconds(20), 0); component.OnStatusGain(stale, ref staleGrowth);
        Check(component.ActiveAOEs(0, player).Length == 7 && Hints(component, player).ForbiddenZones.Count == 7,
            "old-generation destroy, damage, cast and status callbacks cannot affect its replacement");
        world.Execute(new ActorState.OpCastInfo(replacement.InstanceID, null));
        Check(component.ActiveAOEs(0, player).Length == 6, "early native cancellation clears the replacement forecast immediately");
        world.Execute(new ActorState.OpDestroy(helpers[2].InstanceID));
        Check(component.ActiveAOEs(0, player).Length == 5, "native destruction clears its helper without disturbing others");
        Time(module, 11.1); component.Update();
        Check(component.ActiveAOEs(0, player).Length == 0 && Hints(component, player).ForbiddenZones.Count == 0,
            "missing effects expire two seconds after predicted resolution instead of lasting for the rest of the encounter");
        foreach (var helper in helpers.Skip(3)) world.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        module.StateMachine.Reset(); module.StateMachine.Start(world.CurrentTime);
        component = (GenericAOEs)Component(module, "Blackfire2");
        Check(component.ActiveAOEs(0, player).Length == 0, "reset cannot retain old helper forecasts");
        Growth(replacement, 8); Start(replacement);
        text.Clear(); component.AddGlobalHints(player, text);
        Check(component.ActiveAOEs(0, player).Length == 1 && Hints(component, player).ForbiddenZones.Count == 0 && text.Count == 1,
            "an already-present unknown growth status is uncertain from the first cast cue");
    }
    private static void DebilitatorColors()
    {
        using var module = Module();
        var component = Component(module, "Debilitator");
        var world = module.WorldState;
        var player = Actor(1, 0, module.Center);
        Actor Create(ulong id, uint oid = 2007621, ActorType type = ActorType.EventObj)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, type, Class.None, 60,
                new Vector4(-333.0512f, -7, 242.0001f, 0), 0.5f, default, false, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        void Animate(ulong id, ushort shared, ushort mask)
            => world.Execute(new ActorState.OpEventObjectAnimation(id, shared, mask));
        string Summary()
        {
            var hints = new BossComponent.GlobalHints();
            component.AddGlobalHints(player, hints);
            return hints.SingleOrDefault(h => h.StartsWith("Puddles:")) ?? "";
        }
        List<(Actor Actor, string Label, uint Color)> Markers()
            => ((IEnumerable<(Actor, string, uint)>)component.GetType().GetMethod("PuddleMarkers")!.Invoke(component, [player])!).ToList();
        void StartDebilitator()
            => world.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID,
                new() { Action = new(ActionType.Spell, 7334), TotalTime = 2.7f }));

        var first = Create(216);
        Create(217);
        Create(218);
        Check(Summary() == "" && Markers().Count == 0, "puddles make no colour assignment before the actual weakness");
        player.Statuses[0] = new(1157, 0, Now.AddMinutes(2), module.PrimaryActor.InstanceID);
        Check(Summary() == "Puddles: 0 wrong, 3 unknown (need red/fire)", "native raw OID creation counts live unknown puddles immediately");
        Animate(216, 16, 32);
        Check(Summary() == "Puddles: 1 wrong, 2 unknown (need red/fire)", "native first blue family updates counts without waiting for Update or cast finish");
        Animate(217, 64, 128);
        Animate(218, 64, 128);
        Animate(216, 16, 32);
        Check(Summary() == "Puddles: 1 wrong, 0 unknown (need red/fire)" && Markers().Count == 3, "repeated native EAnim never duplicates a puddle");
        var marks = Markers();
        Check(marks.Single(m => m.Actor == first).Label == "BLUE: change" && marks.Single(m => m.Actor == first).Color == Colors.Danger
            && marks.Single(m => m.Actor.InstanceID == 217).Label == "RED: keep", "point labels distinguish wrong blue from correct red at exact actor positions");
        Animate(216, 256, 512);
        Check(Summary() == "Puddles: 0 wrong, 0 unknown (need red/fire)", "recorded 1-to-2 flip immediately becomes red");
        Animate(217, 1024, 2048);
        Check(Summary() == "Puddles: 1 wrong, 0 unknown (need red/fire)", "recorded 2-to-1 flip immediately becomes blue");
        player.Statuses[0] = new(1137, 0, Now.AddMinutes(2), module.PrimaryActor.InstanceID);
        Check(Summary() == "Puddles: 2 wrong, 0 unknown (need blue/water)"
            && Markers().Single(m => m.Actor.InstanceID == 217).Label == "BLUE: keep", "both actual weakness statuses choose the opposite colour without cached party assumptions");
        Animate(217, 16, 128);
        Check(Summary() == "Puddles: 2 wrong, 1 unknown (need blue/water)"
            && Markers().Single(m => m.Actor.InstanceID == 217).Label == "?: unknown", "unrecognized pair clears the old colour instead of matching either parameter loosely");
        Animate(216, 4, 8);
        Animate(218, 1, 2);
        Check(Summary() == "Puddles: 0 wrong, 1 unknown (need blue/water)" && Markers().Count == 1, "both recorded terminal families retire their own puddle immediately");
        Animate(217, 16, 32);
        var old = world.Actors.Find(217)!;
        var replacement = Create(217);
        Check(Summary() == "Puddles: 0 wrong, 1 unknown (need blue/water)", "native actor replacement does not inherit the old generation's known colour");
        Animate(217, 64, 128);
        component.OnActorDestroyed(old);
        component.OnActorEAnim(old, 0x00100020);
        Check(Summary() == "Puddles: 1 wrong, 0 unknown (need blue/water)" && Markers().Single().Actor == replacement,
            "native ID reuse starts a new actor generation and stale callbacks cannot clear or recolour it");
        world.Execute(new ActorState.OpDestroy(217));
        Check(Summary() == "" && Markers().Count == 0, "native destruction clears the last point and count");

        Create(219, 2007622);
        Animate(219, 16, 32);
        Check(Summary() == "", "identical animations on a different raw OID are ignored");
        Create(220);
        Animate(220, 64, 128);
        module.StateMachine.Reset();
        module.StateMachine.Start(Now);
        component = Component(module, "Debilitator");
        Check(Summary() == "Puddles: 0 wrong, 3 unknown (need blue/water)" && Markers().All(m => m.Label == "?: unknown"),
            "native module reset discards colour history; existing event objects bootstrap as unknown");
        world.Execute(new ActorState.OpDestroy(216));
        world.Execute(new ActorState.OpDestroy(218));
        var helper = Create(221, 0x192E, ActorType.Helper);
        foreach (var aid in new uint[] { 7336, 7337 })
        {
            StartDebilitator();
            Animate(220, 16, 32);
            world.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID, null));
            Check(Summary() == "Puddles: 0 wrong, 0 unknown (need blue/water)", "next Debilitator reopens observation after reset or prior resolution");
            world.Execute(new ActorState.OpCastEvent(helper.InstanceID, Hit(aid)));
            var personal = new BossComponent.TextHints();
            component.AddHints(0, player, personal);
            Animate(220, 64, 128);
            Check(Summary() == "" && Markers().Count == 0 && personal.Count == 0, "actual Flameflow clears counts, markers and instructions despite retained weakness or late EAnim " + aid);
        }
        var ai = Hints(component, player);
        Check(ai.ForbiddenZones.Count == 0 && ai.GoalZones.Count == 0, "colour states do not invent toggle radius or assign the whole party to one puddle");
    }
    private static void TransportedCharges()
    {
        using var module = Module();
        var component = (GenericAOEs)Component(module, "TransportedLuckyPierrot");
        var player = Actor(1, 0, module.Center);
        var portal = Actor(2, 0x1929, new(-350, 233));
        var other = Actor(3, 0x1929, new(-357, 229));
        var endpoint = new WPos(-350, 249);
        var cast = Cast(portal, 7326, 2.7f);
        cast.Location = new(endpoint.X, 0, endpoint.Z);
        component.OnCastStarted(portal, cast);
        component.OnCastStarted(portal, cast);
        Check(component.ActiveAOEs(0, player).Length == 1, "duplicate exit cast does not duplicate outgoing charge");
        var aoe = component.ActiveAOEs(0, player)[0];
        Check(aoe.Check(new(-350, 240)) && !aoe.Check(new(-346.9f, 240)), "explicit outgoing line retains nominal width6");
        Check(Math.Abs((aoe.Activation - Now).TotalSeconds - 3.7) < 0.01, "exit forecast includes visual-to-add gap");
        Time(module, 3);
        component.OnCastFinished(portal, cast);
        Check(component.ActiveAOEs(0, player).Length == 1, "successful exit visual retains charge prediction");
        var second = Cast(other, 7766, 6.2f);
        second.Location = new(-370, 0, 238);
        component.OnCastStarted(other, second);
        Check(component.ActiveAOEs(0, player).Length == 2, "simultaneous outgoing lines retain independent deadlines");
        var add = Actor(4, 0x192D, endpoint);
        component.OnEventCast(add, new(new(ActionType.Spell, 7342), 0, 0, 0, new(endpoint.X, 0, endpoint.Z), 0, 0, default));
        Check(component.ActiveAOEs(0, player).Length == 1, "actual add charge clears only its exact endpoint");
        component.OnActorDestroyed(other);
        Check(component.ActiveAOEs(0, player).Length == 0, "destroyed portal clears remaining exit forecast");
        Time(module, 0);
        component.OnCastStarted(portal, cast);
        Time(module, 1);
        component.OnCastFinished(portal, cast);
        Check(component.ActiveAOEs(0, player).Length == 0, "cancelled portal cast clears outgoing forecast");
        Time(module, 0);
        component.OnCastStarted(portal, cast);
        var ambiguous = Cast(other, 7326, 2.7f);
        ambiguous.Location = cast.Location;
        component.OnCastStarted(other, ambiguous);
        component.OnEventCast(add, new(new(ActionType.Spell, 7342), 0, 0, 0, new(endpoint.X, 0, endpoint.Z), 0, 0, default));
        Check(component.ActiveAOEs(0, player).Length == 2, "ambiguous shared endpoint never retires an arbitrary outgoing line");
        Time(module, 5.9);
        component.Update();
        Check(component.ActiveAOEs(0, player).Length == 2, "missing or ambiguous hit retains warning through conservative grace interval");
        Time(module, 6.1);
        component.Update();
        Check(component.ActiveAOEs(0, player).Length == 0, "missing or ambiguous hit expires3s after visual finish without leaking forever");
    }
    private static void Proto()
    {
        using var module = Module(true);
        var player = Actor(1, 0, module.Center);
        player.Type = ActorType.Player;
        module.WorldState.Actors.Actors.Add(player.InstanceID, player);
        module.WorldState.Execute(new PartyState.OpModify(0, new(0, player.InstanceID, false)));
        var flare = Component(module, "FlareStar");
        var middle = Actor(2, 0x196A, module.Center);
        flare.OnCastStarted(middle, Cast(middle, 7588, 6.7f));
        var hints = Hints(flare, player);
        Check(hints.ForbiddenZones.Count == 0, "central proximity cannot forbid the entire radius30 arena");
        Check(hints.GoalZones.Count == 0 && hints.PredictedDamage.Count == 1, "central proximity retains damage warning without rewarding the overlapping unverified outer pool");
        flare.OnCastFinished(middle, middle.CastInfo!);
        var north = Actor(3, 0x196A, module.Center + new WDir(0, -20));
        var south = Actor(4, 0x196A, module.Center + new WDir(0, 20));
        flare.OnCastStarted(north, Cast(north, 7588, 6.7f));
        flare.OnCastStarted(south, Cast(south, 7588, 6.7f));
        hints = Hints(flare, player);
        Check(hints.GoalZones.Count == 0 && hints.PredictedDamage.Count == 2, "paired proximity retains both damage deadlines without outward incentive into pool");
        flare.OnCastFinished(north, north.CastInfo!);
        flare.OnCastFinished(south, south.CastInfo!);
        Check(Hints(flare, player).GoalZones.Count == 0, "proximity clears when all casts finish");
        var pool = (CastHint)Component(module, "AetherialPool");
        var poolCast = Cast(middle, 7587, 3.2f);
        pool.OnCastStarted(middle, poolCast);
        pool.OnCastStarted(middle, poolCast);
        var poolText = new BossComponent.GlobalHints();
        pool.AddGlobalHints(player, poolText);
        Check(pool.Casters.Count == 1 && poolText.SequenceEqual(new[] { "Aetherial Pool: avoid the outer region!" }), "actual pool cast supplies early duplicate-safe global instruction");
        Check(Hints(pool, player).ForbiddenZones.Count == 0 && Hints(pool, player).GoalZones.Count == 0, "unverified pool eligibility does not invent full-arena pull or safe annulus");
        pool.OnCastFinished(middle, poolCast);
        Check(!pool.Active, "pool cancellation/completion clears instruction");
        pool.OnCastStarted(middle, poolCast);
        pool.OnActorDestroyed(middle);
        Check(!pool.Active, "destroyed pool source cannot leave stale instruction");
        Check(module.Components.Any(c => c.GetType().Name == "SupernovaEnrage") && module.Components.Any(c => c.GetType().Name == "ProtoUltimaAdds"), "collector phase has enrage and targeting guidance");
        var eikonizer = Component(module, "Eikonizer");
        var collector = Actor(10, 0x1969, module.Center);
        var orb = Actor(11, 0x196C, module.Center);
        module.WorldState.Actors.Actors.Add(orb.InstanceID, orb);
        eikonizer.OnEventIcon(collector, 62, collector.InstanceID);
        Check(Hints(eikonizer, player).GoalZones.Count == 0, "collector cannot masquerade as marked Eikonizer orb");
        eikonizer.OnEventIcon(orb, 62, orb.InstanceID);
        eikonizer.OnEventIcon(orb, 62, orb.InstanceID);
        hints = Hints(eikonizer, player);
        Check(hints.GoalZones.Count == 1 && hints.ForbiddenZones.Count == 0, "orb gathering has no invented hard safe radius");
        Check(hints.GoalZones[0](orb.Position) > hints.GoalZones[0](orb.Position + new WDir(10, 0)), "gathering preference points toward marked orb");
        eikonizer.OnEventCast(collector, Hit(7596));
        Check(Hints(eikonizer, player).GoalZones.Count == 1, "different source cannot clear orb assignment");
        eikonizer.OnEventCast(orb, Hit(7596));
        Check(Hints(eikonizer, player).GoalZones.Count == 0, "own orb resolution clears gathering guidance before Flare Star");
        eikonizer.OnEventIcon(orb, 62, orb.InstanceID);
        module.WorldState.Actors.Actors[orb.InstanceID] = Actor(orb.InstanceID, 0x196C, module.Center);
        eikonizer.Update();
        Check(Hints(eikonizer, player).GoalZones.Count == 0, "actor replacement cannot retain stale orb identity");
        var pillar = (GenericAOEs)Component(module, "LightPillar");
        pillar.OnCastStarted(middle, Cast(middle, 7589, 1.9f));
        Check(pillar.ActiveAOEs(0, player).Length == 1, "Light Pillar cast produces warning");
        pillar.OnCastFinished(middle, middle.CastInfo!);
        Check(pillar.ActiveAOEs(0, player).Length == 0, "Light Pillar cast warning clears");
    }
}
