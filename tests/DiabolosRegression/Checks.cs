using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A35Diabolos;
using BossMod.Heavensward.Alliance.A36DiabolosHollow;

static partial class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos, Class job = Class.None)
        => new(id, oid, 0, 0, "", 0, job == Class.None ? ActorType.Enemy : ActorType.Player, job, 60,
            new Vector4(pos.X, 1, pos.Z, 0), hpmp: new(100, 100, 0, 100, 100));
    private static ActorCastEvent Hit(uint aid, WPos pos = default, ulong target = 0)
        => new(new(ActionType.Spell, aid), target, 0, 0, new(pos.X, 1, pos.Z), 0, 0, default);
    private static T Component<T>(BossModule module, string name) where T : BossComponent
        => (T)module.Components.Single(c => c.GetType().Name == name);
    private static WorldState World() => new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
    private static void Member(WorldState world, Actor actor, int slot)
    {
        world.Actors.Actors[actor.InstanceID] = actor;
        world.Execute(new PartyState.OpModify(slot, new(0, actor.InstanceID, false)));
    }
    private static A36DiabolosHollow Hollow(WorldState? world = null)
    {
        world ??= World();
        var primary = Actor(1, 0x1908, new(-350, -445));
        world.Actors.Actors[1] = primary;
        var module = new A36DiabolosHollow(world, primary);
        module.StateMachine.Start(Now);
        return module;
    }
    private static AIHints Hints(BossModule module, BossComponent component, int slot, Actor actor)
    {
        var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        component.AddAIHints(slot, actor, default, hints);
        return hints;
    }
    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        NightTerror();
        OmenVisualTiming();
        GateGuidanceLifecycle();
        GateIntermissionLifecycle();
        HollowStackGaze();
        EarthshakerOrder();
        TowerEligibility();
        BlindsideTiming();
        BlindsideUnavailableTarget();
        GravityLifecycle();
        DamageBuffLifecycle();
        PortalFallback();
        PortalConsumerPolicy();
        TowerGenerationPolicy();
        HollowTowerBootstrap();
        HollowNox();
        Console.WriteLine($"{assertions} Diabolos assertions passed (synthetic callbacks; no physical or live acceptance).");
    }
    private static void NightTerror()
    {
        var world = World();
        var target = Actor(20, 0, new(-350, -445), Class.SGE);
        Member(world, target, 0);
        using var module = new A35Diabolos(world, Actor(1, 0x1907, new(-350, -445)));
        module.StateMachine.Start(Now);
        var stack = Component<StackWithIcon>(module, "NightTerror");
        stack.OnEventIcon(target, 62, target.InstanceID);
        world.Frame = new(Now.AddSeconds(0.25), 0, 0, 0, 0, 1);
        stack.OnEventIcon(target, 62, target.InstanceID);
        Check(stack.Stacks.Count == 1, "repeated instant stack marker is idempotent");
        Check(stack.Stacks[0].Activation == Now.AddSeconds(5.2), "stack gives early future deadline");
        stack.OnEventCast(module.PrimaryActor, Hit(7187, target: target.InstanceID));
        Check(stack.Stacks.Count == 0, "instant Night Terror hit clears marker");
        stack.OnEventIcon(target, 62, target.InstanceID);
        world.Frame = new(Now.AddSeconds(8), 0, 0, 0, 0, 1);
        stack.Update();
        Check(stack.Stacks.Count == 0, "missing resolution cannot leave stale stack forever");
    }
    private static void HollowStackGaze()
    {
        using var module = Hollow();
        var marker = Actor(20, 0, module.Center + new WDir(5, 0), Class.SGE);
        var observer = Actor(21, 0, module.Center, Class.WHM);
        var stack = Component<StackWithIcon>(module, "HollowNight");
        var gaze = Component<GenericGaze>(module, "HollowNightGaze");
        stack.OnEventIcon(marker, 91, marker.InstanceID);
        module.WorldState.Frame = new(Now.AddSeconds(0.25), 0, 0, 0, 0, 1);
        stack.OnEventIcon(marker, 91, marker.InstanceID);
        Check(stack.Stacks.Count == 1, "Hollow Night marker is idempotent");
        var eyes = gaze.ActiveEyes(0, observer);
        Check(eyes.Length == 1 && eyes[0].Position == marker.Position, "gaze originates at marked player rather than boss");
        Check(eyes[0].Activation == Now.AddSeconds(6), "gaze and stack share future timing");
        Check(gaze.ActiveEyes(0, marker).Length == 0, "marked player is not instructed to look away from self");
        Check(Hints(module, gaze, 0, observer).ForbiddenDirections.Count == 1, "AI consumes player-origin gaze");
        stack.OnEventCast(module.PrimaryActor, Hit(7197, target: marker.InstanceID));
        Check(gaze.ActiveEyes(0, observer).Length == 0, "instant stack hit retires its associated gaze");
    }
    private static void EarthshakerOrder()
    {
        using var module = Hollow();
        var first = Actor(20, 0, module.Center + new WDir(15, 0), Class.SGE);
        var second = Actor(21, 0, module.Center + new WDir(-15, 0), Class.WHM);
        var component = Component<BaitAwayIcon>(module, "EarthShaker2");
        component.OnEventIcon(first, 40, first.InstanceID);
        module.WorldState.Frame = new(Now.AddSeconds(0.25), 0, 0, 0, 0, 1);
        component.OnEventIcon(first, 40, first.InstanceID);
        Check(component.CurrentBaits[0].Activation == Now.AddSeconds(4.7), "repeated Earthshaker icon cannot postpone departure");
        component.OnEventIcon(second, 40, second.InstanceID);
        Check(component.CurrentBaits.Count == 2, "distinct Earthshaker markers deduplicate independently");
        var hit = Hit(7210);
        hit.Targets.Add(new(second.InstanceID, default));
        component.OnEventCast(module.PrimaryActor, hit);
        Check(component.CurrentBaits.Count == 1 && component.CurrentBaits[0].Target == first, "helper hit retires matching recipient rather than icon order");
        component.OnEventCast(module.PrimaryActor, Hit(7210));
        Check(component.CurrentBaits.Count == 1, "unmatched helper does not retire arbitrary bait");
        Check(Hints(module, component, 0, second).ForbiddenZones.Count > 0, "AI sees pending bait before instant resolution");
        module.WorldState.Frame = new(Now.AddSeconds(7), 0, 0, 0, 0, 1);
        component.Update();
        Check(component.CurrentBaits.Count == 0, "unresolved bait has bounded stale-state expiry");
    }
    private static void TowerEligibility()
    {
        var world = World();
        var healer = Actor(20, 0, new(-320, -445), Class.SGE);
        var tank = Actor(21, 0, new(-320, -445), Class.PLD);
        var allianceHealer = Actor(22, 0, new(-320, -445), Class.WHM);
        Member(world, healer, 0);
        Member(world, tank, 1);
        Member(world, allianceHealer, 8);
        using var module = Hollow(world);
        var towers = Component<GenericTowers>(module, "ParticleBeam");
        var center = Actor(30, 0x1EA24F, module.Center);
        var outer = Actor(31, 0x1EA24E, module.Center + new WDir(24, 0));
        world.Actors.Actors.Add(center.InstanceID, center);
        world.Actors.Actors.Add(outer.InstanceID, outer);
        towers.OnActorCreated(center);
        towers.OnActorCreated(center);
        towers.OnActorCreated(outer);
        towers.Update();
        Check(towers.Towers.Count == 2, "tower spawn callback is idempotent");
        var middle = towers.Towers.Single(t => t.ActorID == center.InstanceID);
        Check(middle.ForbiddenSoakers[0] && middle.ForbiddenSoakers[8] && !middle.ForbiddenSoakers[1], "central tank tower excludes non-tanks in every alliance");
        Check(middle.Activation == Now.AddSeconds(9.7), "tower available before its future impact");
        var tankHints = Hints(module, towers, 1, tank);
        Check(tankHints.ForbiddenZones.Any(z => z.shapeDistance.Distance(module.Center) > 0), "tank has a center-soak constraint");
        var healerHints = Hints(module, towers, 8, allianceHealer);
        Check(healerHints.ForbiddenZones.Any(z => z.shapeDistance.Distance(module.Center) < 0), "alliance healer avoids tank tower");
        towers.OnEventCast(module.PrimaryActor, Hit(7207, module.Center));
        Check(towers.Towers.Count == 1 && towers.Towers[0].ActorID == outer.InstanceID, "center hit preserves unresolved outer tower");
        towers.OnActorDestroyed(outer);
        Check(towers.Towers.Count == 0, "tower despawn removes pending assignment");
    }
    private static void BlindsideTiming()
    {
        var world = World();
        var target = Actor(20, 0, new(-350, -445), Class.PLD);
        Member(world, target, 0);
        using var module = Hollow(world);
        var component = Component<BossComponent>(module, "Blindside");
        component.OnEventIcon(target, 93, target.InstanceID);
        world.Frame = new(Now.AddSeconds(0.25), 0, 0, 0, 0, 1);
        component.OnEventIcon(target, 93, target.InstanceID);
        var hints = Hints(module, component, 0, target);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == Now.AddSeconds(5), "Blindside supplies an early healing deadline");
        Check(hints.ForbiddenZones.Count == 0, "unknown Blindside angle is not fabricated as a hard zone");
        component.OnEventCast(module.PrimaryActor, Hit(7208));
        Check(Hints(module, component, 0, target).PredictedDamage.Count == 0, "Blindside instant action clears forecast");
    }
    private static void BlindsideUnavailableTarget()
    {
        using var module = Hollow();
        var world = module.WorldState;
        var component = Component<BossComponent>(module, "Blindside");
        Actor CreateTank(ulong id)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.GNB, 60,
                new Vector4(-350, 1, -445, 0), 0.5f, new(100, 100, 0, 100, 100), true, false, 0, 0, 0));
            var tank = world.Actors.Find(id)!;
            Member(world, tank, 0);
            return tank;
        }
        void Icon(Actor actor) => world.Execute(new ActorState.OpIcon(actor.InstanceID, 93, actor.InstanceID));
        BossComponent.GlobalHints Global()
        {
            var hints = new BossComponent.GlobalHints();
            component.AddGlobalHints(module.PrimaryActor, hints);
            return hints;
        }
        var target = CreateTank(20);
        Icon(target);
        var prediction = Hints(module, component, 0, target).PredictedDamage.Single();
        Check(prediction.Activation == Now.AddSeconds(5), "native Blindside marker creates the early target healing deadline");
        world.Frame = new(Now.AddSeconds(2), 0, 0, 0, 0, 1);
        world.Execute(new ActorState.OpDead(target.InstanceID, true));
        component.Update();
        Check(Global().Count == 1 && Global()[0].Contains("still incoming") && Global()[0].Contains("watch boss facing"),
            "marked tank death before damage retains a bounded warning and explicitly withdraws certainty about aim");
        Check(Hints(module, component, 0, target).PredictedDamage.Count == 0,
            "dead marked tank receives no stale target-specific healing forecast");
        world.Execute(new ActorState.OpCastEvent(module.PrimaryActor.InstanceID, Hit(7208)));
        Check(Global().Count == 0, "actual Blindside clears the warning even after the marked target died");
        world.Execute(new ActorState.OpDestroy(target.InstanceID));
        target = CreateTank(20);
        Icon(target);
        world.Execute(new ActorState.OpDestroy(target.InstanceID));
        component.Update();
        Check(Global().Count == 1 && Global()[0].Contains("watch boss facing")
            && Hints(module, component, 0, target).PredictedDamage.Count == 0,
            "marked actor destruction retains only the incoming global warning");
        var replacement = CreateTank(20);
        Check(Global()[0].Contains("unavailable") && Hints(module, component, 0, replacement).PredictedDamage.Count == 0,
            "actor-ID replacement cannot inherit an old generation's Blindside healing assignment");
        world.Frame = new(Now.AddSeconds(3), 0, 0, 0, 0, 1);
        Icon(replacement);
        Check(Global()[0].Contains("marked frontal") && !Global()[0].Contains("unavailable")
            && Hints(module, component, 0, replacement).PredictedDamage.Single().Activation == Now.AddSeconds(8),
            "fresh marker on a new generation restores verified target identity with a fresh deadline");
        world.Frame = new(Now.AddSeconds(3.25), 0, 0, 0, 0, 1);
        Icon(replacement);
        Check(Hints(module, component, 0, replacement).PredictedDamage.Single().Activation == Now.AddSeconds(8),
            "duplicate native marker on the same generation cannot postpone departure or stale expiry");
        world.Frame = new(Now.AddSeconds(10), 0, 0, 0, 0, 1);
        component.Update();
        Check(Global().Count == 0 && Hints(module, component, 0, replacement).PredictedDamage.Count == 0,
            "missing Blindside hit expires both warning and healing exactly2s after the forecast deadline");
        Icon(replacement);
        Check(Hints(module, component, 0, replacement).PredictedDamage.Single().Activation == Now.AddSeconds(15),
            "later wave on the same live actor gets a fresh marker deadline after stale cleanup");
        module.StateMachine.Reset();
        module.StateMachine.Start(Now.AddSeconds(10));
        component = Component<BossComponent>(module, "Blindside");
        Check(Global().Count == 0 && Hints(module, component, 0, replacement).PredictedDamage.Count == 0,
            "module reset discards the unresolved incoming Blindside state");
    }

    private static void GravityLifecycle()
    {
        using var module = Hollow();
        var component = Component<GenericAOEs>(module, "CriticalGravity");
        var first = Actor(50, 0x19AF, module.Center + new WDir(20, 0));
        var second = Actor(51, 0x19AF, module.Center + new WDir(-20, 0));
        module.WorldState.Actors.Actors[first.InstanceID] = first;
        module.WorldState.Actors.Actors[second.InstanceID] = second;
        component.OnActorCreated(first);
        component.OnActorCreated(first);
        component.OnActorCreated(second);
        Check(component.ActiveAOEs(0, module.PrimaryActor).Length == 2, "orb creation is source-specific and idempotent");
        const string interceptionWarning = "Critical Gravity: if intercepting, check the 10y knockback landing and keep the blast away from the party!";
        BossComponent.GlobalHints GravityWarnings()
        {
            BossComponent.GlobalHints result = [];
            component.AddGlobalHints(module.PrimaryActor, result);
            return result;
        }
        Check(GravityWarnings().SequenceEqual([interceptionWarning]),
            "orb creation immediately warns of the supported outward displacement without requiring interception");
        BossComponent.TextHints contact = [];
        component.AddHints(0, first, contact);
        Check(contact.SequenceEqual([("Orb contact explodes and knocks you 10y away: check your landing!", false)]),
            "near-orb manual instruction includes blast and knockback landing without claiming an exact contact threshold");
        var hints = Hints(module, component, 0, module.PrimaryActor);
        Check(hints.ForbiddenZones.Count == 0 && hints.GoalZones.Count == 0, "contact orbs do not invent alliance-wide interception or avoidance assignments");
        Check(hints.ForbiddenDirections.Count == 0 && hints.PredictedDamage.Count == 0 && hints.ForcedMovement == null,
            "the displacement warning introduces no invented contact deadline or automatic landing constraint");
        component.OnEventCast(second, Hit(7767));
        Check(component.ActiveAOEs(0, first).Length == 1 && component.ActiveAOEs(0, first)[0].ActorID == first.InstanceID, "second orb detonating first preserves the other orb");
        Check(GravityWarnings().SequenceEqual([interceptionWarning]), "one orb resolving preserves the remaining interception warning");
        component.OnEventCast(second, Hit(7767));
        Check(component.ActiveAOEs(0, first).Length == 1, "duplicate detonation does not consume another orb");
        var replacement = Actor(first.InstanceID, 0x19AF, module.Center);
        module.WorldState.Actors.Actors[first.InstanceID] = replacement;
        component.OnActorCreated(replacement);
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, replacement).Length == 1 && component.ActiveAOEs(0, replacement)[0].Origin == replacement.Position, "old-generation destruction preserves replacement orb");
        replacement.IsDestroyed = true;
        component.Update();
        Check(component.ActiveAOEs(0, replacement).Length == 0, "destroyed orb clears without requiring damage packet");
        Check(GravityWarnings().Count == 0, "the last orb disappearing retires its displacement warning");
    }

    private static void DamageBuffLifecycle()
    {
        using var module = Hollow();
        var component = Component<BossComponent>(module, "DamageBuffs");
        BossComponent.GlobalHints hints = [];
        component.AddGlobalHints(module.PrimaryActor, hints);
        Check(hints.Count == 0, "absent damage buffs produce no permanent warning");
        module.PrimaryActor.Statuses[0] = new(777, 0, DateTime.MaxValue, 0);
        component.AddGlobalHints(module.PrimaryActor, hints);
        Check(hints.Count == 1, "existing void barrier is reconstructed without a new status callback");
        module.PrimaryActor.Statuses[0] = new(1145, 0, DateTime.MaxValue, 1);
        hints.Clear();
        component.AddGlobalHints(module.PrimaryActor, hints);
        Check(hints.Count == 1 && hints[0].Contains("Keen Edge"), "barrier loss transitions to the current damage buff rather than retaining both");
        module.PrimaryActor.Statuses[0] = default;
        hints.Clear();
        component.AddGlobalHints(module.PrimaryActor, hints);
        Check(hints.Count == 0, "damage buff removal immediately clears its instruction");
    }

    private static void PortalFallback()
    {
        using var module = Hollow();
        var world = module.WorldState;
        var player = Actor(5, 0, module.Center, Class.PLD);
        Member(world, player, 0);
        var portal = Component<BossComponent>(module, "PortalSafety");
        Check(module.Components.Last() == portal, "portal fallback executes last and cannot be overwritten by a main-floor component");
        var manualTarget = Actor(70, 0xFFFF, new(350, -445));
        var focusTarget = Actor(71, 0xFFFE, new(351, -445));
        var action = new ActionID(ActionType.Spell, 119);
        var forcedMovement = new Vector3(0.25f, 0, -0.5f);
        var bitmap = new Bitmap(4, 4, default, default);
        AIHints Seed()
        {
            var hints = new AIHints
            {
                PathfindMapCenter = module.Center,
                PathfindMapBounds = module.Bounds,
                PathfindMapObstacles = new(bitmap, new(0, 0, 4, 4)),
                ForcedTarget = manualTarget,
                ForcedFocusTarget = focusTarget,
                ForcedMovement = forcedMovement,
                RecommendedPositional = (manualTarget, Positional.Rear, true, false)
            };
            hints.ForbiddenZones.Add((new SDCircle(module.Center, 5), Now.AddSeconds(5), 123));
            hints.GoalZones.Add(_ => 7);
            hints.TemporaryObstacles.Add(new SDCircle(module.Center, 3));
            hints.Teleporters.Add(new(module.Center, new(350, -445), 1, false));
            hints.ForbiddenDirections.Add((default, 45.Degrees(), Now.AddSeconds(5)));
            hints.PredictedDamage.Add(new(BitMask.Build(0), Now.AddSeconds(5)));
            hints.PotentialTargets.Add(new(manualTarget, 7, false));
            hints.ActionsToExecute.Push(action, manualTarget, ActionQueue.Priority.ManualGCD, manual: true);
            return hints;
        }
        const string warning = "Portal arena: move manually; automatic movement is paused.";
        var oldAutomaticActions = BossModule.WindowConfig.AllowAutomaticActions;
        BossModule.WindowConfig.AllowAutomaticActions = true;
        try
        {
            // Exact native samples1766084/1768986/1770354: actor5 at X350,Y-300.
            player.PosRot = new(350, -300, -445, 0);
            var hints = Seed();
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.ForbiddenZones.Count == 0 && hints.GoalZones.Count == 0 && hints.TemporaryObstacles.Count == 0
                && hints.Teleporters.Count == 0 && hints.ForbiddenDirections.Count == 0 && hints.PredictedDamage.Count == 0,
                "portal post-component pass withdraws every seeded main-floor guidance collection");
            Check(hints.PathfindMapObstacles == default && hints.RecommendedPositional == default,
                "portal post-component pass withdraws main-floor bitmap and positional guidance");
            Check(!hints.GoalZonesEnabled && hints.ForbidDashes && hints.ImminentSpecialMode.mode == AIHints.SpecialMode.NoMovement
                && hints.ImminentSpecialMode.activation == Now, "known portal region pauses navigation immediately without a guessed replacement arena");
            Check(hints.NoMovementAllowsManualInput && !hints.SpecialModeBlocksManualMovement,
                "portal navigation pause explicitly permits keyboard movement under the default movement-block consumer");
            Check(hints.PathfindMapCenter == module.Center && ReferenceEquals(hints.PathfindMapBounds, module.Bounds)
                && !module.Arena.InBounds(player.Position), "inherited main-floor bounds remain metadata under the pause rather than fabricated portal geometry");
            Check(hints.ForcedTarget == manualTarget && hints.ForcedFocusTarget == focusTarget && hints.ForcedMovement == forcedMovement,
                "portal fallback preserves explicit target, focus and movement input");
            Check(hints.PotentialTargets.Count == 1 && hints.PotentialTargets[0].Actor == manualTarget && hints.PotentialTargets[0].Priority == 7
                && hints.ActionsToExecute.Entries.Count == 1 && hints.ActionsToExecute.Entries[0].Action == action
                && hints.ActionsToExecute.Entries[0].Target == manualTarget && hints.ActionsToExecute.Entries[0].Manual,
                "portal fallback preserves attack candidates and the manually queued attack");
            var personal = new BossComponent.TextHints { ("main-floor personal assignment", true) };
            var global = new BossComponent.GlobalHints { "main-floor global instruction" };
            portal.AddHints(0, player, personal);
            portal.AddGlobalHints(player, global);
            Check(personal.Count == 1 && personal[0] == (warning, false) && global.SequenceEqual([warning]),
                "last portal component replaces prior personal/global guidance with one manual instruction");

            var shadethrust = Component<SimpleAOEs>(module, "Shadethrust");
            var cast = new ActorCastInfo { Action = new(ActionType.Spell, 7194), TotalTime = 2.7f,
                Location = new(module.Center.X, 1, module.Center.Z) };
            shadethrust.OnCastStarted(module.PrimaryActor, cast);
            module.PrimaryActor.Statuses[0] = new(777, 0, DateTime.MaxValue, 0);
            Check(module.CalculateGlobalHints(player).SequenceEqual([warning]), "actual main-floor buff warning is suppressed for a portal participant");
            foreach (var position in new Vector4[]
            {
                new(-350, 1, -445, 0), new(350, 1, -445, 0), new(-350, -300, -445, 0),
                new(0, -300, -445, 0), new(350, -100, -445, 0)
            })
            {
                player.PosRot = position;
                hints = Seed();
                module.CalculateAIHints(0, player, default, hints);
                Check(hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal && hints.GoalZonesEnabled && !hints.NoMovementAllowsManualInput
                    && !hints.ForbidDashes && hints.ForbiddenZones.Count > 0 && hints.GoalZones.Count == 1
                    && hints.TemporaryObstacles.Count == 1 && hints.Teleporters.Count == 1 && hints.PredictedDamage.Count == 1,
                    "main floor and unrelated coordinates do not activate the narrow known-portal classifier " + position);
                personal = [("retained personal", true)];
                global = ["retained global"];
                portal.AddHints(0, player, personal);
                portal.AddGlobalHints(player, global);
                Check(personal.Count == 1 && personal[0].Item1 == "retained personal" && global.SequenceEqual(["retained global"]),
                    "nonportal actors retain their personal/global instructions " + position);
            }
            player.PosRot = new(350, -300, -445, 0);
            hints = new();
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.ImminentSpecialMode.mode == AIHints.SpecialMode.NoMovement, "return test first enters the actual portal hold");
            player.PosRot = new(module.Center.X, 1, module.Center.Z, 0);
            hints.Clear(); // the normal per-frame hint rebuild
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal && hints.GoalZonesEnabled && !hints.ForbidDashes
                && !hints.NoMovementAllowsManualInput && hints.ForbiddenZones.Count > 0,
                "return to main floor restores freshly generated hazards without retaining portal hold flags");
            Check(module.CalculateHintsForRaidMember(0, player).Any(h => h.Item2 && h.Item1.Contains("aoe"))
                && module.CalculateGlobalHints(player).Any(h => h.Contains("barrier", StringComparison.OrdinalIgnoreCase)),
                "return restores actual main-floor personal hazard and buff instructions");
            shadethrust.OnCastFinished(module.PrimaryActor, cast);
        }
        finally
        {
            BossModule.WindowConfig.AllowAutomaticActions = oldAutomaticActions;
        }

        var policy = new AIHints { ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, Now, default) };
        Check(policy.SpecialModeBlocksManualMovement, "real NoMovement mechanics retain default manual-input blocking");
        policy.NoMovementAllowsManualInput = true;
        Check(!policy.SpecialModeBlocksManualMovement, "explicit automation-only exemption permits manual input");
        policy.ImminentSpecialMode = (AIHints.SpecialMode.Pyretic, Now, default);
        Check(policy.SpecialModeBlocksManualMovement, "manual-input exemption cannot bypass a real Pyretic mechanic");
        policy.Clear();
        policy.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, Now, default);
        Check(!policy.NoMovementAllowsManualInput && policy.SpecialModeBlocksManualMovement,
            "per-frame Clear resets the exemption before subsequent real movement-lock mechanics");
    }

}
