using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

static class KetudukeChecks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int count;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        ++count;
    }
    private static Actor Add(WorldState world, ulong id, uint oid, WPos position, bool player = false)
    {
        var actor = new Actor(id, oid, 0, 0, "", 0, player ? ActorType.Player : ActorType.Enemy, Class.None, 90,
            new Vector4(position.X, 14, position.Z, 0), hpmp: new(100, 100, 0, 100, 100)) { IsTargetable = true, InCombat = true };
        world.Actors.Actors[id] = actor;
        return actor;
    }
    private static (V32Ketuduke Module, Actor Player) Setup()
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Add(world, 100, (uint)OID.Boss, V32Ketuduke.ArenaCenter);
        var player = Add(world, 1, 0, V32Ketuduke.ArenaCenter, true);
        world.Party.Members[0] = new(1, player.InstanceID, false);
        var party = (Actor?[])typeof(PartyState).GetField("_actors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world.Party)!;
        party[0] = player;
        var module = new V32Ketuduke(world, boss);
        module.Update();
        Check(module.Components.Count == 10, "all ten components activate through generated factories");
        return (module, player);
    }
    private static T Component<T>(BossModule module, string name) where T : BossComponent
        => (T)module.Components.Single(c => c.GetType().Name == name);
    private static ActorCastInfo Cast(AID action, WPos origin, float total = 2.7f, float rotation = 0)
        => new() { Action = ActionID.MakeSpell(action), Location = new(origin.X, 14, origin.Z), TotalTime = total, Rotation = new(rotation) };
    private static ActorCastEvent Effect(AID action, ulong target = 0)
    {
        var effect = new ActorCastEvent(ActionID.MakeSpell(action), target, 0, 1, default, 0, 0, default);
        if (target != 0) effect.Targets.Add(new(target, default));
        return effect;
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        Check(BossModuleRegistry.FindByOID((uint)OID.Boss)?.ModuleType == typeof(V32Ketuduke), "Variant primary OID is registered");
        ArenaAndReset();
        Crystals();
        Sequences();
        Water();
        BootstrapAndRaidwide();
        BubbleLifecycle();
        Console.WriteLine($"{count} Ketuduke assertions passed (synthetic callback/geometry checks; no physical or live acceptance).");
    }
    private static void ArenaAndReset()
    {
        var (module, player) = Setup();
        using (module)
        {
            var hints = new AIHints();
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.PathfindMapBounds == module.Bounds && hints.PathfindMapCenter == V32Ketuduke.ArenaCenter,
                "arena bounds reach native AI pathfinding immediately");
            foreach (var direction in new[] { new WDir(1, 0), new WDir(-1, 0), new WDir(0, 1), new WDir(0, -1) })
            {
                Check(module.Bounds.Contains(direction * 19.9f), "inside each nominal arena edge is walkable");
                Check(!module.Bounds.Contains(direction * 20.1f), "outside each nominal arena edge is excluded");
            }
            module.PrimaryActor.IsDead = true;
            module.Update();
            Check(module.Components.Count == 0, "native death phase clears every mechanic component");
        }
        var (fresh, _) = Setup();
        using (fresh) Check(fresh.Components.OfType<GenericAOEs>().All(c => c.ActiveAOEs(0, player).Length == 0), "fresh module has no hazards from prior pull");
    }
    private static void Crystals()
    {
        foreach (var direction in new[] { MathF.PI / 2, -MathF.PI / 2 })
        {
            var (module, player) = Setup();
            using (module)
            {
                var crystals = Component<GenericAOEs>(module, "SpringCrystals");
                var first = Add(module.WorldState, 10, (uint)OID.SpringCrystalRect, new(-775, -390));
                var second = Add(module.WorldState, 11, (uint)OID.SpringCrystalRect, new(-775, -400));
                crystals.OnActorCreated(first);
                crystals.OnActorCreated(first);
                crystals.OnActorCreated(second);
                crystals.OnEventCast(first, Effect(AID.RectAppear));
                crystals.OnEventCast(second, Effect(AID.RectAppear));
                Check(crystals.ActiveAOEs(0, player).Length == 2, "duplicate create preserves two distinct crystals");
                var status = new ActorStatus((uint)SID.Bubble, 0xC8, Now.AddSeconds(20), 0);
                crystals.OnStatusGain(first, ref status);
                Check(crystals.ActiveAOEs(0, player).Length == 0, "bubble invalidates static forecasts until wind is known");
                var wind = Cast(AID.FlukeTyphoonAOE, module.Center, 4.7f, direction);
                crystals.OnCastStarted(module.PrimaryActor, wind);
                var expected = first.Position + wind.Rotation.ToDirection() * 20f;
                var early = crystals.ActiveAOEs(0, player).ToArray();
                Check(early.Single(a => a.ActorID == first.InstanceID).Origin.AlmostEqual(expected, 0.001f), "forecast follows observed wind direction");
                Check(early.Single(a => a.ActorID == second.InstanceID).Origin == second.Position, "unbubbled crystal stays put");
                crystals.OnStatusGain(first, ref status);
                crystals.OnCastStarted(module.PrimaryActor, wind);
                Check(crystals.ActiveAOEs(0, player).ToArray().Single(a => a.ActorID == first.InstanceID).Origin.AlmostEqual(expected, 0.001f), "refresh/duplicate wind cannot move a crystal twice");
                var castOrigin = expected - new WDir(0, 38);
                var cast = Cast(AID.SaturateRectFast, castOrigin, .7f);
                crystals.OnCastStarted(first, cast);
                var actual = crystals.ActiveAOEs(0, player).ToArray().Single(a => a.ActorID == first.InstanceID);
                Check(actual.Origin == castOrigin && actual.Check(expected) && !actual.Check(expected + new WDir(5.1f, 0)), "actual cast uses the front-origin 76-by-10 rectangle");
                crystals.OnEventCast(first, Effect(AID.SaturateRectFast));
                crystals.OnEventCast(first, Effect(AID.SaturateRectFast));
                Check(crystals.ActiveAOEs(0, player).Length == 1, "duplicate first resolution cannot clear the second crystal");
                crystals.OnActorDestroyed(second);
                Check(crystals.ActiveAOEs(0, player).Length == 0, "crystal destruction removes only its instance");
                var cancelled = Cast(AID.SaturateRect, first.Position);
                crystals.OnCastStarted(first, cancelled);
                crystals.OnCastFinished(first, cancelled);
                Check(crystals.ActiveAOEs(0, player).Length == 0, "cancelled Saturate removes its forecast");
            }
        }
    }
    private static void Sequences()
    {
        var (module, player) = Setup();
        using (module)
        {
            var tides = Component<GenericAOEs>(module, "Twintides");
            foreach (var (first, second) in new[] { (AID.RecedingTwintides, AID.FarTide), (AID.EncroachingTwintides, AID.NearTide), (AID.RecedingTwintides, AID.FarTide) })
            {
                var cast = Cast(first, module.Center, 4.7f);
                tides.OnCastStarted(module.PrimaryActor, cast);
                tides.OnCastStarted(module.PrimaryActor, cast);
                Check(tides.ActiveAOEs(0, player).Length == 1, "only current in/out step constrains movement");
                tides.OnEventCast(module.PrimaryActor, Effect(first));
                tides.OnEventCast(module.PrimaryActor, Effect(first));
                Check(tides.ActiveAOEs(0, player).Length == 1, "duplicate first hit cannot clear follow-up");
                tides.OnEventCast(module.PrimaryActor, Effect(second));
                Check(tides.ActiveAOEs(0, player).Length == 0, "both orders and a repeated order resolve fully");
            }
            var lanes = Component<GenericAOEs>(module, "StrewnBubbles");
            var cancelled = Cast(AID.EncroachingTwintides, module.Center);
            tides.OnCastStarted(module.PrimaryActor, cancelled);
            tides.OnCastFinished(module.PrimaryActor, cancelled);
            Check(tides.ActiveAOEs(0, player).Length == 0, "cancelled first tide clears its dependent follow-up");
            lanes.OnEventCast(module.PrimaryActor, Effect(AID.StrewnBubbles));
            var firstLane = Add(module.WorldState, 20, (uint)OID.BubbleStrewer, module.Center);
            var secondLane = Add(module.WorldState, 21, (uint)OID.BubbleStrewer, module.Center + new WDir(0, 10));
            lanes.OnActorCreated(firstLane);
            lanes.OnActorCreated(firstLane);
            lanes.OnActorCreated(secondLane);
            lanes.OnEventCast(firstLane, Effect(AID.SphereShatter));
            lanes.OnEventCast(firstLane, Effect(AID.SphereShatter));
            Check(lanes.ActiveAOEs(0, player).Length == 1 && lanes.ActiveAOEs(0, player)[0].ActorID == secondLane.InstanceID, "helper resolves its lane only");
        }
    }
    private static void Water()
    {
        var (module, player) = Setup();
        using (module)
        {
            var component = Component<BossComponent>(module, "WaterIII");
            var apa = Add(module.WorldState, 30, (uint)OID.SummonedApa, module.Center);
            var tether = new ActorTetherInfo((uint)TetherID.WaterIIIStretch, player.InstanceID);
            component.OnTethered(apa, tether);
            var cast = Cast(AID.WaterIII, apa.Position, 9.7f);
            apa.CastInfo = cast;
            component.OnCastStarted(apa, cast);
            var hints = new AIHints();
            component.AddAIHints(0, player, default, hints);
            Check(hints.GoalZones.Count == 1 && hints.PredictedDamage.Count == 1, "assigned target gets one stretch preference and one damage forecast");
            Check(hints.GoalZones[0](module.Center + new WDir(15, 0)) > hints.GoalZones[0](module.Center + new WDir(5, 0)), "stretch preference increases distance without inventing a safe threshold");
            cast.EventHappened = true;
            component.OnCastFinished(apa, cast);
            component.OnUntethered(apa, tether);
            hints = new();
            component.AddAIHints(0, player, default, hints);
            Check(hints.GoalZones.Count == 0 && hints.PredictedDamage.Count == 1, "cast finish and unlink preserve delayed damage but stop stretch preference");
            component.OnEventCast(apa, Effect(AID.WaterIIIApply, 99));
            hints = new(); component.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 1, "wrong recipient cannot consume pending damage");
            component.OnEventCast(apa, Effect(AID.WaterIIIApplyUnstretched, player.InstanceID));
            hints = new(); component.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 0, "matching helper retires its assigned hit");
            var cancelled = Cast(AID.WaterIII, apa.Position, 9.7f);
            component.OnTethered(apa, tether);
            component.OnCastStarted(apa, cancelled);
            component.OnCastFinished(apa, cancelled);
            hints = new(); component.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 0, "interrupted Apa cast does not leave a delayed-hit forecast");
            component.OnTethered(apa, tether);
            component.OnCastStarted(apa, cast);
            player.IsDestroyed = true;
            var replacement = Add(module.WorldState, player.InstanceID, 0, player.Position, true);
            component.OnTethered(apa, tether); // No player destruction callback and no intermediate hint sample.
            hints = new(); component.AddAIHints(0, replacement, default, hints);
            Check(hints.PredictedDamage.Count == 0, "recreated player cannot revive an old cast forecast");
        }
    }
    private static void BubbleLifecycle()
    {
        var (module, player) = Setup();
        using (module)
        {
            var bubbles = Component<GenericAOEs>(module, "BlowingBubbles");
            var first = Add(module.WorldState, 40, (uint)OID.AiryBubble, module.Center);
            var second = Add(module.WorldState, 41, (uint)OID.AiryBubble, module.Center + new WDir(10, 0));
            bubbles.OnActorCreated(first); bubbles.OnActorCreated(first); bubbles.OnActorCreated(second);
            first.PosRot.X += 2;
            Check(bubbles.ActiveAOEs(0, player).Length == 2 && bubbles.ActiveAOEs(0, player)[0].Origin == first.Position, "bubble warning follows each unique moving source");
            bubbles.OnActorDestroyed(second);
            Check(bubbles.ActiveAOEs(0, player).Length == 1, "destroying one bubble preserves the other");
            first.IsDestroyed = true;
            Add(module.WorldState, first.InstanceID, (uint)OID.AiryBubble, first.Position);
            Check(bubbles.ActiveAOEs(0, player).Length == 0, "actor identity reuse cannot leave an old bubble warning");
        }
    }

    private static void BootstrapAndRaidwide()
    {
        var (module, player) = Setup();
        using (module)
        {
            var apa = Add(module.WorldState, 50, (uint)OID.SummonedApa, module.Center);
            apa.Tether = new((uint)TetherID.WaterIIIStretch, player.InstanceID);
            apa.CastInfo = Cast(AID.WaterIII, apa.Position, 9.7f);
            var type = module.Components.Single(c => c.GetType().Name == "WaterIII").GetType();
            typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(module, null);
            var water = Component<BossComponent>(module, "WaterIII");
            var hints = new AIHints();
            water.AddAIHints(0, player, default, hints);
            Check(hints.GoalZones.Count == 1 && hints.PredictedDamage.Count == 1, "late activation bootstraps an already-active Apa cast and tether");

            var companion = Add(module.WorldState, 51, 0, module.Center);
            module.WorldState.Party.Members[1] = new(0, companion.InstanceID, false);
            var party = (Actor?[])typeof(PartyState).GetField("_actors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module.WorldState.Party)!;
            party[1] = companion;
            var roar = Component<BossComponent>(module, "TidalRoar");
            var cast = Cast(AID.TidalRoar, module.Center, 4.7f);
            roar.OnCastStarted(module.PrimaryActor, cast);
            cast.EventHappened = true;
            roar.OnCastFinished(module.PrimaryActor, cast);
            hints = new(); roar.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Players.Raw == 3,
                "post-visual raidwide has one prediction containing player and companion");
            roar.OnEventCast(module.PrimaryActor, Effect(AID.TidalRoarAOE));
            hints = new(); roar.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 0, "raidwide helper retires its forecast");
            var cancelled = Cast(AID.TidalRoar, module.Center);
            roar.OnCastStarted(module.PrimaryActor, cancelled);
            roar.OnCastFinished(module.PrimaryActor, cancelled);
            hints = new(); roar.AddAIHints(0, player, default, hints);
            Check(hints.PredictedDamage.Count == 0, "interrupted raidwide leaves no phantom forecast");
        }
    }
}
