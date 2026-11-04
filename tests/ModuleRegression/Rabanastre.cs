using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;

static partial class Regression
{
    private static void RabanastreTowers()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var module = Module(prefix + "A12Hashmal", new(-320, -46));
        var component = Component<GenericAOEs>(module, prefix + "Towerfall");
        var first = Actor(1, 0x1EA7DF, new(-320, -57));
        var second = Actor(2, 0x1EA7DF, new(-310.474f, -40.5f));
        component.OnActorCreated(first);
        component.OnActorCreated(first);
        component.OnActorCreated(second);
        var aoes = component.ActiveAOEs(0, first);
        Check(aoes.Length == 2, "tower predictions are distinct and duplicate spawn is idempotent");
        Check(aoes[0].Activation == Now.AddSeconds(9), "fall direction gives nine seconds of advance warning");
        Check(aoes[0].Check(first.Position + new WDir(4.9f, 20)) && !aoes[0].Check(first.Position + new WDir(5.1f, 20)), "tower full width is ten, with safe space on either side");
        var hints = new AIHints();
        component.AddAIHints(0, first, default, hints);
        Check(hints.ForbiddenZones.Count == 2, "both towers constrain AI before their hits");
        component.OnEventCast(Actor(3, 0x18D6, second.Position), Effect(9668));
        component.OnEventCast(Actor(3, 0x18D6, second.Position), Effect(9668));
        aoes = component.ActiveAOEs(0, first);
        Check(aoes.Length == 1 && aoes[0].ActorID == first.InstanceID, "out-of-order and duplicate helper hits cannot clear another tower");
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, first).Length == 0, "tower disappearance clears its own prediction");
    }

    private static void RabanastreCasts()
    {
        foreach (var (moduleName, componentName, action, center, inside, outside) in new (string, string, uint, WPos, WDir, WDir)[]
        {
            ("A12Hashmal", "ExtremeEdge", 9680, new(-320, -46), new(17.9f, 20), new(18.1f, 20)),
            ("A13Rofocale", "Maverick", 9865, new(106, -191), new(7.9f, 20), new(8.1f, 20)),
            ("A14Argath", "Heartless", 9778, new(106, -386), new(5.4f, 20), new(5.6f, 20))
        })
        {
            var prefix = "Stormblood.Alliance." + moduleName + ".";
            var module = Module(prefix + moduleName, center);
            var component = Component<GenericAOEs>(module, prefix + componentName);
            var caster = Actor(1, 0x18D6, center);
            var cast = Cast(action, center);
            component.OnCastStarted(caster, cast);
            var aoes = component.ActiveAOEs(0, caster);
            Check(aoes.Length == 1 && aoes[0].Check(center + inside) && !aoes[0].Check(center + outside), componentName + " preserves both sides of its width boundary");
            var hints = new AIHints();
            component.AddAIHints(0, caster, default, hints);
            Check(hints.ForbiddenZones.Count == 1, componentName + " publishes a timed AI constraint");
            component.OnCastFinished(caster, cast);
            Check(component.ActiveAOEs(0, caster).Length == 0, componentName + " retires on cast finish, including interruption");
            if (componentName is "ExtremeEdge" or "Maverick")
            {
                var alternate = Cast(componentName == "ExtremeEdge" ? 9678u : 10022u, center);
                component.OnCastStarted(caster, alternate);
                aoes = component.ActiveAOEs(0, caster);
                Check(aoes.Length == 1 && aoes[0].Check(center + inside) && !aoes[0].Check(center + outside),
                    componentName + " publishes the separate sheet-backed variant");
                component.OnCastFinished(caster, alternate);
                Check(component.ActiveAOEs(0, caster).IsEmpty, componentName + " cleans up the alternate variant");
            }
        }
    }

    private static void RabanastreArenas()
    {
        foreach (var (name, oid, center, inside, outside, count, gaugeRow, gaugeSample, expectedPercent) in new (string, uint, WPos, WDir, WDir, int, uint, uint, string)[]
        {
            ("A11Mateus", 0x1F9B, new(-320, 240), new(29, 29), new(30, 0), 18, 12, 9999, "99%"),
            ("A12Hashmal", 0x1F83, new(-320, -46), new(29, 0), new(30, 0), 20, 8, 2666, "26%"),
            ("A13Rofocale", 0x1FAF, new(106, -191), new(0, 29), new(0, 30), 15, 13, 5227, "52%"),
            ("A14Argath", 0x1F97, new(106, -386), new(23, 23), new(25, 0), 18, 10, 4894, "48%")
        })
        {
            var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
            var boss = Actor(1, oid, center);
            using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod.Stormblood.Alliance." + name + "." + name, true)!, world, boss)!;
            Check(module.Info != null, name + " is registered by primary OID");
            Check(module.Arena.Center == center && module.Arena.InBounds(center + inside) && !module.Arena.InBounds(center + outside), name + " has the authored movement bounds, including square corners");
            module.StateMachine.Start(Now);
            Check(module.Components.Count == count, name + " state machine activates its actual component factories");
            var gauge = module.Components.Single(component => component.GetType().Name == "RabanastreGauge");
            world.Execute(new WorldState.OpDirectorUpdate(0x8003756A, 0x8000000D, gaugeRow, 5000, 0, 0));
            world.Execute(new WorldState.OpDirectorUpdate(0x8003756A, 0x8000000C, gaugeRow + 1, 5000, 0, 0));
            Check(GaugeHints().Count == 0, name + " ignores other rows and other director updates");
            UpdateGauge(0);
            Check(GaugeHints().Single().Contains("0%"), name + " shows an active empty gauge");
            UpdateGauge(gaugeSample);
            Check(GaugeHints().Single().Contains(expectedPercent), name + " uses the client's 10000-unit gauge scale");
            UpdateGauge(10001);
            Check(GaugeHints().Single().EndsWith("is full."), name + " clamps display at full");
            UpdateGauge(uint.MaxValue);
            Check(GaugeHints().Count == 0, name + " clears a hidden gauge");
            UpdateGauge(gaugeSample);
            gauge.OnActorDeath(boss);
            Check(GaugeHints().Count == 0, name + " clears gauge guidance on boss death");

            void UpdateGauge(uint value)
                => world.Execute(new WorldState.OpDirectorUpdate(0x8003756A, 0x8000000C, gaugeRow, value, 0, 0));

            BossComponent.GlobalHints GaugeHints()
            {
                var hints = new BossComponent.GlobalHints();
                gauge.AddGlobalHints(boss, hints);
                return hints;
            }
        }
    }

    private static void RabanastreSandSpheres()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1F83, new(-320, -46));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A12Hashmal", true)!, world, boss)!;
        var dust = Component<RaidwideCast>(module, prefix + "ToDust");
        var player = AddActor(1, 0, Class.WAR, ActorType.Player, module.Center);
        world.Execute(new PartyState.OpModify(0, new(0, 1, false)));
        world.Party.SetAllianceGroups(0, 1, 2);
        var command = Cast(9660, new(-320.02747f, -46.00598f));
        command.TotalTime = 2.7f;
        command.ElapsedTime = 0.0253083f;
        var northWestStage = new WPos(-334.5f, -57);
        var southStage = new WPos(-320, -26);
        var northEastStage = new WPos(-306, -57.5f);
        dust.OnCastStarted(boss, command);
        Check(StagingHints(0).GoalZones.Single()(northWestStage) == 4f
            && StagingHints(8).GoalZones.Single()(southStage) == 4f
            && StagingHints(16).GoalZones.Single()(northEastStage) == 4f,
            "Command Tower publishes each alliance's staging direction before the knockback");
        dust.OnCastFinished(boss, command);
        Check(StagingHints(0).GoalZones.Count == 0, "interrupting Command Tower removes the staging goals");
        dust.OnCastStarted(boss, command);
        var knockback = Component<SimpleKnockbacks>(module, prefix + "CommandTowerKnockback");
        knockback.OnCastStarted(boss, command);
        var launch = new WPos(-310.9344f, -35.208134f);
        var recordedLanding = new WPos(-301.1128f, -23.545227f);
        player.PosRot = launch.ToVec4();
        var projectedLanding = knockback.CalculateMovements(0, player).Single().to;
        Check(knockback.ActiveKnockbacks(0, player)[0].Distance == 50f && module.Arena.InBounds(projectedLanding)
            && projectedLanding.InCircle(recordedLanding, 0.5f),
            "the wall-clipped Command Tower projection agrees with the recorded launch and landing");
        knockback.OnCastFinished(boss, command);
        Check(knockback.ActiveKnockbacks(0, player).IsEmpty, "Command Tower releases its knockback source at cast end");
        player.PosRot = module.Center.ToVec4();
        world.Frame = new(Now.AddSeconds(3.41544), 0, 0, 0, 0, 2);
        dust.OnEventCast(boss, Effect(9660));
        dust.OnCastFinished(boss, command);
        Check(StagingHints(0).GoalZones.Count == 1, "the actual knockback reconciles and preserves the preparation window");
        var tower = AddActor(5, 0x1F86, Class.None, ActorType.Enemy, new(-320.0005f, -45.99996f));
        tower.HitboxRadius = 7.3f;
        world.Frame = new(Now.AddSeconds(5.22837), 0, 0, 0, 0, 2);
        var freshTower = TowerHints();
        Check(freshTower.PotentialTargets.Single().Priority == 2 && freshTower.GoalZones.Single()(northWestStage) == 4f,
            "the recorded targetable tower is attackable before enmity while the alliance still stages");
        var staging = StagingHints(0);
        var blockedStage = new SDCircle(northWestStage, 3f);
        staging.AddForbiddenZone(blockedStage, world.CurrentTime);
        var stagingContext = new NavigationDecision.Context();
        var stagingRoute = NavigationDecision.Build(stagingContext, world.CurrentTime, staging, player, forbiddenZoneCushion: 0.5f);
        var stagingEndpoint = stagingContext.ThetaStar.CellCenter(stagingContext.ThetaStar.BestIndex());
        Check(stagingRoute.Destination != null && staging.GoalZones.Single()(stagingEndpoint) > 3f
            && blockedStage.Distance(stagingEndpoint) > 0.5f && module.Arena.InBounds(stagingEndpoint),
            "an active hazard can move the staging endpoint without changing its assigned sector");
        world.Frame = new(Now.AddSeconds(9.2), 0, 0, 0, 0, 3);
        Check(StagingHints(0).GoalZones.Count == 0, "a skipped sphere wave leaves no lingering staging goal");
        world.Frame = new(Now, 0, 0, 0, 0, 1);
        dust.OnCastStarted(boss, command);
        var west = AddActor(2, 0x1F84, Class.None, ActorType.Enemy, new(-337, -57));
        var firstSphere = new AIHints();
        firstSphere.PotentialTargets.Add(new(west, AIHints.Enemy.PriorityUndesirable, false));
        module.CalculateAIHints(8, player, default, firstSphere);
        Check(firstSphere.PotentialTargets[0].Priority == AIHints.Enemy.PriorityUndesirable
            && firstSphere.GoalZones.Single()(southStage) == 4f && StagingHints(16).GoalZones.Single()(northEastStage) == 4f,
            "one early sphere does not pull the other alliances out of their staging sectors");
        Check(StagingHints(0).GoalZones.Count == 0, "only the northwest assignment stops staging when its sphere appears");
        var south = AddActor(3, 0x1F84, Class.None, ActorType.Enemy, new(-320, -24));
        var east = AddActor(4, 0x1F84, Class.None, ActorType.Enemy, new(-303, -57));
        Check(StagingHints(0).GoalZones.Count == 0, "a targetable Sand Sphere immediately replaces staging even before it enters combat");
        var hints = Hints(0);
        Check(Preferred(hints) == west && Preferred(Hints(8)) == south && Preferred(Hints(16)) == east,
            "alliance assignments activate for targetable spheres before they enter combat");
        Check(Preferred(Hints(0, west)) != west, "ineligible spheres are never made selectable");

        var danger = new SDCircle(new(-329, -52), 3f);
        hints.AddForbiddenZone(danger, Now);
        hints.Normalize();
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination is { } step && module.Arena.InBounds(step) && module.Arena.InBounds(endpoint)
            && hints.GoalZones.Single()(endpoint) == 5f && danger.Distance(endpoint) > 0.5f,
            "the native planner approaches the assigned sphere around a hazard");

        west.CastInfo = Cast(9673, west.Position);
        west.CastInfo.TotalTime = 19.7f;
        south.CastInfo = Cast(9673, south.Position);
        south.CastInfo.TotalTime = 19.7f;
        south.CastInfo.ElapsedTime = 4f;
        dust.OnCastStarted(west, west.CastInfo);
        dust.OnCastStarted(south, south.CastInfo);
        var text = new BossComponent.TextHints();
        dust.AddHints(0, player, text);
        Check(text.Any(hint => hint.Item1.Contains("northwest") && hint.Item1.Contains("To Dust")),
            "players see their sector and the active cast deadline");
        var damage = Hints(0).PredictedDamage;
        Check(damage.Count == 2 && damage.All(hit => hit.Players[0])
            && damage.Select(hit => hit.Activation).Distinct().Count() == 2,
            "each live sphere publishes its own raidwide deadline");
        west.IsDead = true;
        dust.OnActorDeath(west);
        hints = Hints(0);
        Check(Preferred(hints) != west && hints.GoalZones.Count == 1 && hints.PredictedDamage.Single().Activation == module.CastFinishAt(south.CastInfo),
            "destroying a sphere releases the assignment and only that cast prediction");
        dust.OnEventCast(south, Effect(9673));
        Check(Hints(0).PredictedDamage.Count == 0, "a completed cast clears its prediction immediately");
        dust.OnCastStarted(south, south.CastInfo);
        dust.OnActorDestroyed(south);
        Check(Hints(0).PredictedDamage.Count == 0, "despawn cancels the active cast forecast");

        south.IsDead = east.IsDead = true;
        player.PosRot = recordedLanding.ToVec4();
        hints = Hints(0);
        var approachHazard = new SDCircle(new(-309f, -33f), 4f);
        hints.AddForbiddenZone(approachHazard, Now);
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        text.Clear();
        dust.AddHints(0, player, text);
        Check(hints.PotentialTargets.Single(enemy => enemy.Actor == tower).Priority == 2
            && route.Destination != null && endpoint.InCircle(tower.Position, 10.3f) && approachHazard.Distance(endpoint) > 0.5f
            && text.Any(hint => hint.Item1.Contains("Destroy the Command Tower")),
            "once the spheres die, the player warning and native route return to the tower around a hazard");
        var excludedTower = TowerHints(AIHints.Enemy.PriorityInvincible);
        Check(excludedTower.PotentialTargets.Single().Priority == AIHints.Enemy.PriorityInvincible && excludedTower.GoalZones.Count == 0,
            "the tower assignment preserves native attack exclusions");
        tower.IsTargetable = false;
        Check(TowerHints().GoalZones.Count == 0, "an untargetable tower cannot retain an approach goal");

        AIHints StagingHints(int slot)
        {
            var result = new AIHints();
            module.CalculateAIHints(slot, player, default, result);
            return result;
        }

        Actor AddActor(ulong id, uint oid, Class job, ActorType type, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, type, job, 70,
                new(position.X, 0, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, type == ActorType.Player, 0, 0, 0));
            return world.Actors.Find(id)!;
        }

        AIHints Hints(int slot, Actor? ineligible = null)
        {
            var result = new AIHints();
            foreach (var sphere in new[] { west, south, east })
                result.PotentialTargets.Add(new(sphere, sphere == ineligible ? AIHints.Enemy.PriorityInvincible : AIHints.Enemy.PriorityUndesirable, false));
            result.PotentialTargets.Add(new(tower, AIHints.Enemy.PriorityUndesirable, false));
            module.CalculateAIHints(slot, player, default, result);
            return result;
        }

        AIHints TowerHints(int priority = AIHints.Enemy.PriorityUndesirable)
        {
            var result = new AIHints();
            result.PotentialTargets.Add(new(tower, priority, false));
            module.CalculateAIHints(0, player, default, result);
            return result;
        }

        static Actor Preferred(AIHints result) => result.PotentialTargets.Single(enemy => enemy.Priority == 4).Actor;
    }

    private static void RabanastreHashmalAddPhases()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1F83, new(-320, -46));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A12Hashmal", true)!, world, boss)!;
        var golems = Component<RaidwideCast>(module, prefix + "PennantstoneGolems");
        var landwaster = Component<RaidwideCast>(module, prefix + "Landwaster");
        var main = AddActor(1, 0, Class.WAR, ActorType.Player, 0);
        var off1 = AddActor(2, 0, Class.PLD, ActorType.Player, 16);
        var off2 = AddActor(3, 0, Class.DRK, ActorType.Player, 8);
        var west = AddActor(4, 0x1F85, Class.None, ActorType.Enemy, -1);
        var east = AddActor(5, 0x1F85, Class.None, ActorType.Enemy, -1);
        west.PosRot.X -= 17f;
        east.PosRot.X += 17f;
        boss.TargetID = main.InstanceID;

        var summon = Cast(9684, boss.Position);
        golems.OnCastStarted(boss, summon);
        var global = new BossComponent.GlobalHints();
        golems.AddGlobalHints(main, global);
        Check(global.Count == 1 && global[0].Contains("Golems"), "Summon gives players the add instruction before the raidwide casts");
        golems.OnCastFinished(boss, summon);
        Check(Assigned(off1) == west && Assigned(off2) == east && Assigned(main) == null,
            "off-tank pickups are split by stable actor identity independently of local party order");
        var pickup = PickupHints(off1);
        Check(pickup.GoalZones.Single()(west.Position) == 5f && pickup.GoalZones[0](east.Position) == 0f,
            "the assigned off-tank receives an approach to its own pickup");
        var lane = new SDCircle(new(-328f, -46f), 3f);
        pickup.AddForbiddenZone(lane, Now);
        var routeContext = new NavigationDecision.Context();
        var route = NavigationDecision.Build(routeContext, Now, pickup, off1, forbiddenZoneCushion: 0.5f);
        var endpoint = routeContext.ThetaStar.CellCenter(routeContext.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && pickup.GoalZones[0](endpoint) == 5f
            && lane.Distance(endpoint) > 0.5f,
            "the native pathfinder reaches the assigned pickup with a concurrent hazard");
        west.TargetID = off2.InstanceID;
        Check(Assigned(off2) == west && Assigned(off1) == east,
            "an existing live tank keeps the golem while the free off-tank takes the other");
        Check(Assigned(off1, east) == null, "the assignment respects existing native ineligibility");
        off1.PosRot.X -= 31f;
        Check(Assigned(off1) == null && PickupHints(off2).PotentialTargets.Single(enemy => enemy.PreferProvoking).Actor == east,
            "an off-tank outside the arena does not hold the assignment");
        off1.PosRot.X += 31f;
        off1.IsDead = off2.IsDead = true;
        Check(Assigned(main) != null, "the boss tank can pick up golems when both off-tanks are unavailable");
        off1.IsDead = off2.IsDead = false;

        west.CastInfo = Cast(9686, west.Position);
        west.CastInfo.TotalTime = 3.7f;
        east.CastInfo = Cast(9686, east.Position);
        east.CastInfo.TotalTime = 3.7f;
        east.CastInfo.ElapsedTime = 1f;
        golems.OnCastStarted(west, west.CastInfo);
        golems.OnCastStarted(east, east.CastInfo);
        var damage = Damage(golems);
        Check(damage.PredictedDamage.Count == 2 && damage.PredictedDamage.All(hit => hit.Players[0])
            && damage.PredictedDamage.Select(hit => hit.Activation).Distinct().Count() == 2,
            "each golem's active Demolish publishes its own timing");
        golems.OnEventCast(east, Effect(9686));
        Check(Damage(golems).PredictedDamage.Single().Activation == module.CastFinishAt(west.CastInfo),
            "one golem's Demolish impact retires only its own forecast");
        golems.OnCastStarted(east, east.CastInfo);
        golems.OnActorDeath(east);
        Check(Damage(golems).PredictedDamage.Single().Activation == module.CastFinishAt(west.CastInfo),
            "killing one golem removes only that golem's forecast");
        golems.OnCastFinished(west, west.CastInfo);
        Check(Damage(golems).PredictedDamage.Count == 0, "a resolved or interrupted golem cast clears its prediction");

        var tower = Actor(6, 0x1F86, boss.Position);
        var helper = Actor(7, 0x18D6, boss.Position);
        landwaster.OnActorDestroyed(tower);
        Check(Damage(landwaster).PredictedDamage.Count == 0, "a reset despawn is not a completed Command Tower check");
        landwaster.OnActorDeath(tower);
        damage = Damage(landwaster);
        Check(damage.PredictedDamage.Count == 1 && damage.PredictedDamage[0].Players[0]
            && damage.PredictedDamage[0].Activation == Now.AddSeconds(10.4d),
            "the Command Tower death gives the raid early mitigation time");
        world.Frame = new(Now.AddSeconds(3.65), 0, 0, 0, 0, 2);
        helper.CastInfo = Cast(9670, helper.Position);
        helper.CastInfo.TotalTime = 6.7f;
        landwaster.OnCastStarted(helper, helper.CastInfo);
        Check(Damage(landwaster).PredictedDamage.Single().Activation == module.CastFinishAt(helper.CastInfo),
            "the helper cast replaces the provisional deadline without publishing duplicate damage");
        landwaster.OnEventCast(helper, Effect(9670));
        Check(Damage(landwaster).PredictedDamage.Count == 0, "Landwaster impact clears the prediction");

        Actor AddActor(ulong id, uint oid, Class job, ActorType type, int slot)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, type, job, 70,
                new(-320, 0, -46, 0), 0.5f, new(100, 100, 0, 0, 0), true, type == ActorType.Player, 0, 0, 0));
            if (slot >= 0)
                world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Actors.Find(id)!;
        }

        Actor? Assigned(Actor tank, Actor? ineligible = null)
            => PickupHints(tank, ineligible).PotentialTargets.FirstOrDefault(enemy => enemy.ShouldBeTanked && enemy.Priority >= 2)?.Actor;

        AIHints PickupHints(Actor tank, Actor? ineligible = null)
        {
            var hints = new AIHints();
            foreach (var golem in new[] { west, east })
                hints.PotentialTargets.Add(new(golem, golem == ineligible ? AIHints.Enemy.PriorityInvincible : AIHints.Enemy.PriorityUndesirable, false));
            module.CalculateAIHints(0, tank, default, hints);
            return hints;
        }

        AIHints Damage(BossComponent component)
        {
            var hints = new AIHints();
            component.AddAIHints(0, main, default, hints);
            return hints;
        }
    }

    private static void RabanastreHammerShadows()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var module = Module(prefix + "A12Hashmal", new(-320, -46));
        var hammer = Component<GenericAOEs>(module, prefix + "EarthHammer");
        var world = module.WorldState;
        world.Execute(new ActorState.OpCreate(10, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
            new(-320, 10, -46, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 10, false)));
        var first = Actor(1, 0x1EA7E1, new(-332, -46));
        var second = Actor(2, 0x1EA7E1, new(-308, -46));
        hammer.OnActorCreated(first);
        hammer.OnActorCreated(first);
        hammer.OnActorCreated(second);
        var hints = new AIHints();
        hammer.AddAIHints(0, first, default, hints);
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones.All(zone => zone.activation == Now.AddSeconds(12.5)), "both shadows constrain movement before the late cast");
        Check(hints.PredictedDamage.Count == 1 && hammer.ActiveAOEs(0, first).ToArray().All(aoe => aoe.Color == Colors.Danger),
            "simultaneous shadows are equally bright and share one mitigation cue");
        var lateCast = Cast(9676, second.Position);
        var helper = Actor(3, 0x18D6, second.Position);
        hammer.OnCastStarted(helper, lateCast);
        var aoes = hammer.ActiveAOEs(0, first);
        Check(aoes.Length == 2 && aoes[0].ActorID == second.InstanceID && aoes[0].Activation == module.CastFinishAt(lateCast),
            "the late helper cast reconciles and reorders its shadow without adding a duplicate");
        Check(aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.AOE, "a later impact remains shaded");
        hints = new();
        hammer.AddAIHints(0, first, default, hints);
        Check(hints.ForbiddenZones.Count == 2 && hints.PredictedDamage.Count == 2
            && hints.PredictedDamage[1].Activation == Now.AddSeconds(12.5d), "staggered impacts retain both movement and mitigation deadlines");
        hammer.OnCastFinished(helper, lateCast);
        Check(hammer.ActiveAOEs(0, first) is var remaining && remaining.Length == 1 && remaining[0].ActorID == first.InstanceID
            && remaining[0].Color == Colors.Danger, "hammer resolution preserves the other shadow and advances it to bright");
        hammer.OnActorDestroyed(first);
        Check(hammer.ActiveAOEs(0, first).Length == 0, "despawning a shadow clears its prediction");
        hammer.OnActorCreated(first);
        hammer.OnActorDeath(Actor(4, 0x1F83, module.Center));
        Check(hammer.ActiveAOEs(0, first).IsEmpty, "boss death clears hammer shadows before their later despawns");

        hammer.OnActorCreated(first);
        hammer.OnActorCreated(second);
        foreach (var (elapsed, stage, lead) in new[] { (5.6116d, 0x00100020u, 7.05d), (8.6208d, 0x00400080u, 4.05d), (11.5639d, 0x01000200u, 1.1d) })
        {
            world.Frame = new(Now.AddSeconds(elapsed), 0, 0, 0, 0, 2);
            hammer.OnActorEAnim(first, stage);
            var updated = hammer.ActiveAOEs(0, first).ToArray();
            Check(updated[0].ActorID == second.InstanceID && updated[0].Activation == Now.AddSeconds(12.5d)
                && updated[1].ActorID == first.InstanceID && updated[1].Activation == world.FutureTime(lead),
                "each recorded animation stage retimes only its own shadow");
            if (stage == 0x00100020u)
            {
                var player = world.Party.Player()!;
                hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
                hammer.AddAIHints(0, player, default, hints);
                var context = new NavigationDecision.Context();
                var route = NavigationDecision.Build(context, world.CurrentTime, hints, player, forbiddenZoneCushion: 0.5f);
                var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
                Check(route.Destination != null && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < 12.5d - elapsed
                    && hints.ForbiddenZones.All(zone => zone.shapeDistance.Distance(endpoint) > 0.5f),
                    "the native route reaches a position clear of both independently timed landings before either resolves");
            }
        }
        world.Frame = new(Now.AddSeconds(11.9279d), 0, 0, 0, 0, 3);
        hammer.OnActorEAnim(first, 0x00400080u);
        Check(hammer.ActiveAOEs(0, first)[1].Activation == Now.AddSeconds(11.5639d).AddSeconds(1.1d),
            "a stale animation cannot delay an advanced shadow");
        var actual = Cast(9676, first.Position);
        actual.TotalTime = 0.7f;
        actual.ElapsedTime = 0.0369061f;
        hammer.OnCastStarted(helper, actual);
        hammer.OnActorEAnim(first, 0x01000200u);
        hammer.OnActorDestroyed(first);
        hints = new();
        hammer.AddAIHints(0, first, default, hints);
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones[1].activation == module.CastFinishAt(actual)
            && hints.PredictedDamage.Count == 2 && hints.PredictedDamage[1].Activation == module.CastFinishAt(actual),
            "a confirmed hammer's movement and mitigation deadline survive visual removal and late animations");
        hammer.OnEventCast(helper, Effect(9676));
        hammer.OnCastFinished(helper, actual);
        Check(hammer.ActiveAOEs(0, first) is var afterImpact && afterImpact.Length == 1 && afterImpact[0].ActorID == second.InstanceID,
            "the observed effect and following cast end retire only the confirmed hammer");
    }

    private static void RabanastreRockCutter()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var center = new WPos(-320, -46);
        var module = Module(prefix + "A12Hashmal", center);
        var world = module.WorldState;
        var cleave = Component<Cleave>(module, prefix + "RockCutter");
        var boss = Actor(1, 0x1F83, center);
        var tank = AddPlayer(2, 0, new(-320, -39));
        var ally = AddPlayer(3, 1, new(-320, -40));
        var distant = AddPlayer(4, 2, new(-301, -46));
        var cast = Cast(9687, center);
        cast.TargetID = tank.InstanceID;
        cleave.OnCastStarted(boss, cast);
        var hints = new AIHints();
        cleave.AddAIHints(1, ally, default, hints);
        var deadline = module.CastFinishAt(cast);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].activation == deadline && hints.ForbiddenZones[0].shapeDistance.Contains(ally.Position),
            "Rock Cutter warns a nearby ally with the cast deadline");
        Check(!hints.ForbiddenZones[0].shapeDistance.Contains(distant.Position)
            && !hints.ForbiddenZones[0].shapeDistance.Contains(new(-320, -52)), "the Rock Cutter forecast leaves room out of reach and behind Hashmal");
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Type == AIHints.PredictedDamageType.Tankbuster && hints.PredictedDamage[0].Players[0],
            "Rock Cutter publishes mitigation for its actual recipient");
        hints = new();
        cleave.AddAIHints(0, tank, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(tank.Position),
            "a nearby ally constrains tank aim and a distant ally cannot");

        tank.PosRot = new(-313, 10, -46, 0);
        hints = new();
        cleave.AddAIHints(1, ally, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(new(-313, -46))
            && !hints.ForbiddenZones[0].shapeDistance.Contains(ally.Position), "the cone turns as the cast target moves");
        cleave.OnCastFinished(boss, cast);
        Check(cleave.OriginsAndTargets().Count == 0, "an early interruption removes Rock Cutter");
        boss.TargetID = tank.InstanceID;
        cast.TargetID = boss.InstanceID;
        cleave.OnCastStarted(boss, cast);
        Check(cleave.OriginsAndTargets().Single().target == tank, "a self-targeted cast falls back to Hashmal's target");
        world.Frame = new(deadline, 0, 0, 0, 0, 2);
        cleave.OnCastFinished(boss, cast);
        Check(cleave.OriginsAndTargets().Count == 1, "near normal cast completion Rock Cutter persists to the effect");
        cleave.OnEventCast(boss, Effect(9687));
        Check(cleave.OriginsAndTargets().Count == 0, "the resolved cleave disappears immediately");

        boss.PosRot = new(-320.02753f, 10.02817f, -46.00598f, 3.085314f);
        tank.PosRot = new(-319.7223f, 10.02817f, -51.438232f, -0.06140828f);
        ally.PosRot = new(-319.69183f, 10f, -42.038635f, 0);
        WPos[] recordedAllies = [new(-317.3114f, -39.261536f), new(-314.59528f, -42.954163f), new(-312.67267f, -45.63977f),
            new(-314.40353f, -52.708717f), new(-317.8302f, -49.729187f), new(-318.83728f, -51.499268f), new(-321.06506f, -53.330322f),
            new(-320.72937f, -48.477966f), new(-326.3142f, -47.501343f), new(-321.61957f, -41.512623f), new(-320.82092f, -39.292053f),
            new(-320.27167f, -40.45172f)];
        for (var i = 0; i < recordedAllies.Length; ++i)
            AddPlayer((ulong)(20 + i), 3 + i, recordedAllies[i]);
        cast.TargetID = tank.InstanceID;
        cast.TotalTime = 3.7f;
        cast.ElapsedTime = 0.0263472f;
        cleave.OnCastStarted(boss, cast);
        var text = new BossComponent.TextHints();
        cleave.AddHints(0, tank, text);
        hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        var overlap = new SDCircle(boss.Position + 6f * 260f.Degrees().ToDirection(), 2f);
        hints.AddForbiddenZone(overlap, world.CurrentTime);
        cleave.AddAIHints(0, tank, default, hints);
        var score = hints.GoalZones.Single();
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, world.CurrentTime, hints, tank, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(text.Count == 1 && hints.ForbiddenZones.Count == 1 && hints.PredictedDamage.Count == 1
            && route.Destination != null && overlap.Distance(endpoint) > 0.5f && score(endpoint) > score(tank.Position),
            "with the recorded allies covering every angle, native navigation favors fewer hits while avoiding a concurrent hazard");

        Actor AddPlayer(ulong id, int slot, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
                new(position.X, 10, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Actors.Find(id)!;
        }
    }

    private static void RabanastreExtremeEdge()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now.AddSeconds(-8.1576), 0, 0, 0, 0, 1) };
        var boss = Actor(1, 0x1F83, new(-320, -46));
        boss.IsTargetable = false;
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A12Hashmal", true)!, world, boss)!;
        var edge = Component<GenericAOEs>(module, prefix + "ExtremeEdge");
        var towers = Component<GenericAOEs>(module, prefix + "Towerfall");
        var jagged = Component<GenericAOEs>(module, prefix + "JaggedEdge");
        var west = Actor(2, 0x1EA7DF, new(-320.0005f, -57));
        var east = Actor(3, 0x1EA7DF, new(-310.474f, -40.5f));
        west.PosRot.W = 1.8928173f;
        east.PosRot.W = -1.4955103f;
        towers.OnActorCreated(west);
        towers.OnActorCreated(east);
        world.Frame = new(Now, 0, 0, 0, 0, 2);
        var player = Actor(4, 0, new(-302.3f, -32.27f), player: true);
        edge.OnActorPlayActionTimelineEvent(boss, 7747);
        Check(Hints(edge).GoalZones.Count == 0, "the shared arrival animation cannot start an Extreme Edge warning");
        edge.OnActorPlayActionTimelineEvent(boss, 7737);
        var global = new BossComponent.GlobalHints();
        edge.AddGlobalHints(player, global);
        var hints = Hints(towers, edge);
        Check(global.Count == 1 && hints.GoalZones.Count == 1 && hints.ForbiddenZones.Count == 2
            && edge.ActiveAOEs(0, player).IsEmpty, "departure supplies staging before either burning side is known");
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && hints.GoalZones[0](endpoint) == 8f
            && route.TimeToGoal < 2.29f && hints.ForbiddenZones.All(zone => zone.shapeDistance.Distance(endpoint) > 0.5f),
            "the recorded starting position can reach the staging band without crossing the impending tower hits");

        hints = Hints(edge);
        var blockedMiddle = new SDRect(module.Center, 90f.Degrees(), 32f, 32f, 5f);
        hints.AddForbiddenZone(blockedMiddle, Now);
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && blockedMiddle.Distance(endpoint) > 0f
            && hints.GoalZones[0](endpoint) > hints.GoalZones[0](player.Position) && route.TimeToGoal < 2.29f,
            $"blocked staging band from {player.Position} to {endpoint}, route {route.Destination}, clearance {blockedMiddle.Distance(endpoint):f2}, time {route.TimeToGoal:f2}, score {hints.GoalZones[0](endpoint):f2}");

        edge.OnActorTargetable(boss);
        Check(Hints(edge).GoalZones.Count == 0, "retargetability clears a cancelled departure");
        edge.OnActorPlayActionTimelineEvent(boss, 7737);
        world.Frame = new(Now.AddSeconds(3.1), 0, 0, 0, 0, 3);
        Check(Hints(edge).GoalZones.Count == 0, "a departure without a cast expires");

        world.Frame = new(Now, 0, 0, 0, 0, 4);
        player.PosRot.X = -318.788f;
        player.PosRot.Z = -51.035f;
        edge.OnActorPlayActionTimelineEvent(boss, 7737);
        var dash = Cast(9680, new(-290.2724f, -34.01239f));
        dash.TotalTime = 5.7f;
        dash.ElapsedTime = 2.42f;
        dash.Rotation = (-1.5704849f).Radians();
        edge.OnCastStarted(boss, dash);
        foreach (var (id, pos) in new (ulong, WPos)[] { (5, new(-324.51f, -50.49f)), (6, new(-324.73f, -45.67f)), (7, new(-306.26f, -35.78f)) })
        {
            var cast = Cast(9677, pos);
            cast.TotalTime = 3f;
            jagged.OnCastStarted(Actor(id, 0x18D6, pos), cast);
        }
        hints = Hints(edge, jagged);
        Check(hints.GoalZones.Count == 0 && hints.ForbiddenZones.Count == 4
            && edge.ActiveAOEs(0, player)[0].Check(player.Position), "the confirmed lane replaces staging while preserving overlapping Jagged Edge deadlines");
        CheckRabanastreRoute(module, player, hints, "east-to-west Extreme Edge with the first Jagged Edge wave");
        edge.OnEventCast(boss, Effect(9680));
        Check(Hints(edge).ForbiddenZones.Count == 0, "the dash hit clears the lane without waiting for return");

        var reverse = Cast(9678, new(-349.752f, -58.03009f));
        reverse.Rotation = 1.5703886f.Radians();
        edge.OnCastStarted(boss, reverse);
        var lane = edge.ActiveAOEs(0, player)[0];
        Check(lane.Check(new(-320, -50)) && !lane.Check(new(-320, -34)), "the opposite variant uses the supplied cast side");
        edge.OnCastFinished(boss, reverse);
        Check(Hints(edge).ForbiddenZones.Count == 0, "an interrupted alternate cast leaves no lane");

        AIHints Hints(params BossComponent[] components)
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            foreach (var component in components)
                component.AddAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
    }

    private static void RabanastreHashmalWaves()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(1, 0x1F83, new(-320, -46));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A12Hashmal", true)!, world, boss)!;
        var jagged = Component<GenericAOEs>(module, prefix + "JaggedEdge");
        var first = Actor(2, 0x18D6, new(-310, -46));
        var second = Actor(3, 0x18D6, new(-320, -38));
        var next = Actor(4, 0x18D6, new(-330, -46));
        var early1 = Cast(9677, first.Position);
        var early2 = Cast(9677, second.Position);
        var late = Cast(9677, next.Position);
        late.TotalTime += 2f;
        jagged.OnCastStarted(next, late);
        jagged.OnCastStarted(first, early1);
        jagged.OnCastStarted(second, early2);
        var aoes = jagged.ActiveAOEs(0, boss);
        Check(aoes.Length == 3 && aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.Danger && aoes[2].Color == Colors.AOE,
            "Jagged Edge highlights simultaneous hits and shades the later wave regardless of insertion order");
        var hints = new AIHints();
        jagged.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 3 && hints.ForbiddenZones[2].activation == module.CastFinishAt(late), "shading retains the later wave's AI constraint");
        jagged.OnEventCast(first, Effect(9677));
        Check(jagged.ActiveAOEs(0, boss).Length == 2 && jagged.ActiveAOEs(0, boss)[0].ActorID == second.InstanceID,
            "a Jagged Edge impact clears only its own puddle");
        jagged.OnCastFinished(first, early1);
        jagged.OnCastFinished(second, early2);
        Check(jagged.ActiveAOEs(0, boss)[0].Color == Colors.Danger, "the next Jagged Edge wave becomes bright as the previous wave resolves");
        jagged.OnActorDestroyed(next);
        Check(jagged.ActiveAOEs(0, boss).IsEmpty, "a lost helper clears its pending Jagged Edge puddle");

        var sanction = Component<RaidwideInstant>(module, prefix + "Sanction");
        var raidwides = Component<RaidwideCasts>(module, prefix + "HashmalRaidwides");
        var tower = Cast(9665, module.Center);
        module.WorldState.Execute(new ActorState.OpCreate(5, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
            new(-320, 10, -46, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        module.WorldState.Execute(new PartyState.OpModify(0, new(0, 5, false)));
        boss.CastInfo = tower;
        raidwides.OnCastStarted(boss, tower);
        sanction.OnCastStarted(boss, tower);
        Check(sanction.Activation == module.CastFinishAt(tower, 3.1d), "Control Tower predicts the separate Sanction raidwide from cast start");
        hints = new();
        raidwides.AddAIHints(0, boss, default, hints);
        sanction.AddAIHints(0, boss, default, hints);
        Check(hints.PredictedDamage.Count == 2 && hints.PredictedDamage[0].Activation == module.CastFinishAt(tower)
            && hints.PredictedDamage[1].Activation == sanction.Activation, "native mitigation sees two separate raidwide deadlines");
        raidwides.OnCastFinished(boss, tower);
        sanction.OnCastFinished(boss, tower);
        Check(sanction.Activation == default, "interrupted Control Tower removes Sanction");
        foreach (var action in new uint[] { 9667, 9987 })
        {
            sanction.OnCastStarted(boss, tower);
            sanction.OnEventCast(boss, Effect(action));
            Check(sanction.Activation == default, "both Sanction actions clear the raidwide forecast");
        }

        var rocks = Component<GenericStackSpread>(module, prefix + "FallingRock");
        WPos[] markers = [new(-308.7968f, -29.251526f), new(-312.79468f, -55.74121f), new(-320.57678f, -37.491394f),
            new(-309.3156f, -25.345215f), new(-336.23254f, -46.341675f), new(-331.19708f, -56.534668f)];
        for (var i = 0; i < markers.Length; ++i)
        {
            var id = (ulong)(20 + i);
            var position = markers[i];
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
                new(position.X, 10, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(1 + i, new(0, id, false)));
            rocks.OnEventIcon(world.Actors.Find(id)!, 120, 0);
        }
        var marked = world.Actors.Find(20)!;
        rocks.OnEventIcon(marked, 120, 0);
        rocks.OnEventIcon(world.Actors.Find(5)!, 230, 0);
        Check(rocks.Spreads.Count == 6 && rocks.Spreads.All(spread => spread.Activation == Now.AddSeconds(5.75d)),
            "six Falling Rock icons warn immediately, without duplicating a refreshed mark or matching Rock Cutter's icon");
        hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        rocks.AddAIHints(1, marked, default, hints);
        Check(hints.ForbiddenZones.Count == 6 && hints.ForbiddenZones.Any(zone => zone.shapeDistance.Contains(marked.Position)),
            "a marked player has timed constraints for both other marks and unmarked party members");
        hints.Normalize();
        CheckRabanastreRoute(module, marked, hints, "Falling Rock marker");
        world.Frame = new(Now.AddSeconds(0.8757d), 0, 0, 0, 0, 2);
        var impact = Cast(9683, marked.Position);
        impact.TotalTime = 4.7f;
        impact.ElapsedTime = 0.02463f;
        impact.TargetID = marked.InstanceID;
        rocks.OnCastStarted(first, impact);
        Check(rocks.Spreads.Count == 6 && rocks.Spreads.Single(spread => spread.Target == marked).Activation == module.CastFinishAt(impact)
            && rocks.Spreads.Count(spread => spread.Activation == Now.AddSeconds(5.75d)) == 5,
            "the helper cast replaces only its own icon deadline");
        rocks.OnCastFinished(first, impact);
        Check(rocks.Spreads.Count == 5 && !rocks.IsSpreadTarget(marked), "one interrupted spread leaves the other marks in place");
        world.Frame = new(Now.AddSeconds(7d), 0, 0, 0, 0, 3);
        rocks.Update();
        Check(rocks.Spreads.Count == 0, "marks without a cast expire");
    }

    private static void RabanastreCommandTowerImpacts()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var module = Module(prefix + "A12Hashmal", new(-320, -46));
        var world = module.WorldState;
        world.Execute(new ActorState.OpCreate(1, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
            new(-320, 10, -61, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 1, false)));
        var player = world.Actors.Find(1)!;
        var helper = Actor(2, 0x18D6, module.Center);
        var visual = Actor(3, 0x1EA1A1, module.Center);
        var tower = Actor(4, 0x1F86, module.Center);
        var impacts = Component<CastCounter>(module, prefix + "CommandTowerImpacts");
        var earthShaker = Component<GenericAOEs>(module, prefix + "EarthShaker");
        impacts.OnActorEAnim(visual, 0x00040008);
        Check(Read().PredictedDamage.Count == 0, "an unrelated object with the shared OID cannot start tower raidwides");
        visual.LayoutID = 7075821;
        impacts.OnActorEAnim(visual, 0x00040008);
        var hints = Read();
        Check(hints.PredictedDamage.Count == 2 && hints.PredictedDamage[0].Activation == Now.AddSeconds(14.9)
            && hints.PredictedDamage[1].Activation == Now.AddSeconds(19.9)
            && hints.PredictedDamage.All(hit => hit.Players[0]) && hints.ForbiddenZones.Count == 0,
            "the platform cue publishes two separate raidwide timings and no false dodge zone");

        var earlyCone = Cast(9672, module.Center);
        earlyCone.Rotation = 180f.Degrees();
        earlyCone.TotalTime = 4.7f;
        var lateCone = Cast(9672, module.Center);
        lateCone.TotalTime = 6.7f;
        var secondHelper = Actor(5, 0x18D6, module.Center);
        earthShaker.OnCastStarted(secondHelper, lateCone);
        earthShaker.OnCastStarted(helper, earlyCone);
        var aoes = earthShaker.ActiveAOEs(0, player);
        Check(aoes.Length == 2 && aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.AOE,
            "Earth Shaker radar distinguishes the current and upcoming cone waves");
        hints = Read();
        earthShaker.AddAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 2 && hints.ForbiddenZones.Count == 2
            && hints.ForbiddenZones[0].shapeDistance.Contains(player.Position),
            "Earth Shaker dodge constraints remain independent of both Impact raidwide predictions");

        earthShaker.OnCastFinished(helper, earlyCone);
        earthShaker.OnCastFinished(secondHelper, lateCone);
        var origin = new WPos(-320.02747f, -46.00598f);
        var earlyHelpers = new List<Actor>();
        ulong sourceId = 10;
        foreach (var (angle, elapsed) in new (float, float)[] { (-60f, 2.02f), (60f, 2.02f), (180f, 2.02f), (0f, 0f), (120f, 0f), (-120f, 0f) })
        {
            var source = Actor(sourceId++, 0x18D6, origin);
            var cast = Cast(9672, origin);
            cast.Rotation = angle.Degrees();
            cast.TotalTime = 4.7f;
            cast.ElapsedTime = elapsed;
            earthShaker.OnCastStarted(source, cast);
            if (elapsed > 0f)
                earlyHelpers.Add(source);
        }

        player.PosRot = (origin + new WDir(0f, 24f)).ToVec4(10f);
        var staging = new AIHints { PathfindMapCenter = module.Center, PathfindMapBounds = module.Bounds };
        earthShaker.AddAIHints(0, player, default, staging);
        Check(staging.ForbiddenZones.Count == 9 && staging.ForbiddenZones.Skip(6).Any(zone => zone.shapeDistance.Contains(player.Position))
            && staging.ForbiddenZones.Take(3).All(zone => !zone.shapeDistance.Contains(player.Position)),
            "the recorded alternate cone directions identify a deep part of the current gap that needs early movement");

        var stagingConstraints = staging.ForbiddenZones.Take(3).Concat(staging.ForbiddenZones.Skip(6)).ToArray();
        staging.Normalize();
        var stagingContext = new NavigationDecision.Context();
        var stagingRoute = NavigationDecision.Build(stagingContext, Now, staging, player, forbiddenZoneCushion: 0.5f);
        var stagingPoint = stagingContext.ThetaStar.CellCenter(stagingContext.ThetaStar.BestIndex());
        Check(stagingRoute.Destination != null && module.Arena.InBounds(stagingPoint)
            && stagingConstraints.All(zone => !zone.shapeDistance.Contains(stagingPoint))
            && stagingRoute.TimeToGoal < 2.98f, $"native movement stages toward the next gap before the first volley, endpoint {stagingPoint}");

        foreach (var source in earlyHelpers)
            earthShaker.OnEventCast(source, Effect(9672));
        player.PosRot = stagingPoint.ToVec4(10f);
        world.Frame = new(Now.AddSeconds(2.98d), 0, 0, 0, 0, 2);
        var exit = new AIHints { PathfindMapCenter = module.Center, PathfindMapBounds = module.Bounds };
        earthShaker.AddAIHints(0, player, default, exit);
        var exitRoute = NavigationDecision.Build(stagingContext, world.CurrentTime, exit, player, forbiddenZoneCushion: 0.5f);
        var exitPoint = stagingContext.ThetaStar.CellCenter(stagingContext.ThetaStar.BestIndex());
        Check(exit.ForbiddenZones.Count == 3 && exitRoute.Destination != null && module.Arena.InBounds(exitPoint)
            && exit.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(exitPoint))
            && exitRoute.TimeToGoal < 2.02f, $"native movement crosses into the next gap after the first hit, endpoint {exitPoint}");

        world.Frame = new(Now.AddSeconds(15.05), 0, 0, 0, 0, 2);
        impacts.OnActorEAnim(visual, 0x00400080);
        Check(Read().PredictedDamage[0].Activation == world.CurrentTime, "the first strike animation reconciles its forecast");
        var first = new ActorCastEvent(new(ActionType.Spell, 9671), 0, 0, 0, default, 4996, 0, default);
        impacts.OnEventCast(helper, first);
        impacts.OnEventCast(helper, first);
        Check(Read().PredictedDamage.Count == 1, "the first hit and its duplicate retire only the first raidwide");
        world.Frame = new(Now.AddSeconds(20.11), 0, 0, 0, 0, 3);
        impacts.OnActorEAnim(visual, 0x01000200);
        Check(Read().PredictedDamage.Single().Activation == world.CurrentTime, "the second strike animation reconciles the remaining raidwide");
        impacts.OnEventCast(helper, new(new(ActionType.Spell, 9671), 0, 0, 0, default, 5079, 0, default));
        Check(Read().PredictedDamage.Count == 0, "the second hit clears the sequence");
        impacts.OnActorEAnim(visual, 0x04000800);
        impacts.OnActorEAnim(visual, 0x00100020);
        Check(Read().PredictedDamage.Count == 2, "the later opening cue can bootstrap a new tower phase");
        impacts.OnActorDeath(tower);
        Check(Read().PredictedDamage.Count == 0, "early tower death cancels future Impacts");

        AIHints Read()
        {
            var result = new AIHints();
            impacts.AddAIHints(0, player, default, result);
            return result;
        }
    }

    private static void RabanastreTrample()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var module = Module(prefix + "A13Rofocale", new(106, -191));
        var trample = Component<GenericAOEs>(module, prefix + "Trample");
        var caster = Actor(1, 0x1FAF, new(100, -200));
        var helper = Actor(2, 0x18D6, caster.Position);
        var cast = Cast(9846, caster.Position);
        cast.Rotation = 90f.Degrees();
        trample.OnCastStarted(caster, cast);
        var aoes = trample.ActiveAOEs(0, caster);
        Check(aoes.Length == 1 && aoes[0].Color == Colors.Danger && aoes[0].Check(new(108, -200)) && !aoes[0].Check(new(108, -194)),
            "the opening lane follows the cast rotation and is the only filled hazard");
        var hints = new AIHints();
        trample.AddAIHints(0, caster, default, hints);
        var deadline = aoes[0].Activation.AddSeconds(-0.25);
        Check(hints.ForbiddenZones.Count == 4 && hints.ForbiddenZones[1].activation == deadline
            && hints.ForbiddenZones[2].activation == aoes[0].Activation.AddSeconds(0.95)
            && hints.ForbiddenZones[3].activation == deadline,
            "AI gets separate front and rear deadlines while keeping the crossing clear");
        var exterior = new WPos(100, -187);
        Check(module.Bounds.Contains(exterior - module.Center) && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(exterior)),
            "the rotated full-sequence route leaves reachable exterior in bounds");
        Check(hints.ForbiddenZones[1].shapeDistance.Contains(new(114, -210)) && hints.ForbiddenZones[2].shapeDistance.Contains(new(86, -210)),
            "both loops constrain routing well outside the first pulse");
        var frontGap = new WPos(114.1f, -200f);
        var rearGap = new WPos(85.9f, -200f);
        Check(hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(frontGap) && !zone.shapeDistance.Contains(rearGap))
            && hints.ForbiddenZones[1].shapeDistance.Contains(frontGap + new WDir(0f, 2f)),
            "the two conservative inner gaps remain clear of both loops and the crossing");
        trample.OnCastFinished(caster, cast);
        Check(trample.ActiveAOEs(0, caster).IsEmpty, "an interrupted Trample clears its prediction");
        hints = new();
        trample.AddAIHints(0, caster, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "interruptions also retire the full-sequence envelope");
        trample.OnCastStarted(caster, cast);
        cast.EventHappened = true;
        trample.OnCastFinished(caster, cast);
        Check(trample.ActiveAOEs(0, caster).Length == 1, "the opening lane persists after the visual resolves");
        trample.OnEventCast(helper, Effect(9847));
        Check(trample.ActiveAOEs(0, caster).IsEmpty, "the first helper pulse clears the opening lane");
        var global = new BossComponent.GlobalHints();
        trample.AddGlobalHints(caster, global);
        Check(global.Count == 1, "the figure-eight warning remains after the opening lane resolves");
        hints = new();
        trample.AddAIHints(0, caster, default, hints);
        Check(hints.ForbiddenZones.Count == 3, "both loops and crossing survive the first impact");
        trample.OnEventCast(helper, Effect(9851));
        hints = new();
        trample.AddAIHints(0, caster, default, hints);
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones[0].activation == Now
            && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(new(114, -210))),
            "the observed midpoint advances the rear and retires only the front loop");
        trample.OnEventCast(helper, Effect(9855));
        global.Clear();
        trample.AddGlobalHints(caster, global);
        Check(global.Count == 0, "the final Trample pulse clears the warning");
        hints = new();
        trample.AddAIHints(0, caster, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "the final Trample pulse also clears AI constraints");

        caster.PosRot = new(92.0272f, -694f, -209.2775f, 3.0346911f);
        var recorded = Cast(9846, caster.Position);
        recorded.Rotation = caster.Rotation;
        recorded.TotalTime = 4.2f;
        recorded.ElapsedTime = 0.016839f;
        trample.OnCastStarted(caster, recorded);
        var rearHit = new WPos(94.79268f, -207.4265f);
        var edgeHit = new WPos(97.68023f, -214.77817f);
        hints = new();
        trample.AddAIHints(0, caster, default, hints);
        var opening = trample.ActiveAOEs(0, caster)[0];
        Check(opening.Check(rearHit) && opening.Check(edgeHit)
            && hints.ForbiddenZones[0].activation == module.CastFinishAt(recorded)
            && hints.ForbiddenZones[0].shapeDistance.Contains(rearHit) && hints.ForbiddenZones[0].shapeDistance.Contains(edgeHit),
            "the first bright warning and its native deadline cover the observed rear and lateral opening hits");
    }

    private static void RabanastreTrampleRoute()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        foreach (var (origin, facing, elapsed, start) in new[]
        {
            (new WPos(105.1194f, -200.6714f), 1.0326245f, 0.039877f, new WPos(111.0703f, -197.3145f)),
            (new WPos(92.0272f, -209.2775f), 3.0346911f, 0.016839f, new WPos(80.9796f, -204.1810f)),
        })
        {
            var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
            var boss = Actor(1, 0x1FAF, origin);
            var player = Actor(2, 0, start, true);
            using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
            var trample = Component<GenericAOEs>(module, prefix + "Trample");
            var cast = Cast(9846, origin);
            cast.Rotation = new Angle(facing);
            cast.ElapsedTime = elapsed;
            cast.TotalTime = 4.2f;
            trample.OnCastStarted(boss, cast);
            var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            trample.AddAIHints(0, player, default, hints);
            hints.Normalize();
            CheckRabanastreRoute(module, player, hints, "Trample");
            var rearGap = origin - 14.1f * new Angle(facing).ToDirection();
            if (module.Arena.InBounds(rearGap))
            {
                player.PosRot = (rearGap + new WDir(1.75f, 0f)).ToVec4();
                var context = new NavigationDecision.Context();
                var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
                var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
                Check(route.Destination != null && endpoint.InCircle(rearGap, 1.5f)
                    && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(endpoint))
                    && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < cast.NPCRemainingTime,
                    $"native Trample routing can use the nearby interior gap, endpoint {endpoint}");
            }
        }
    }

    private static void RabanastreCryOfVictory()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var origin = new WPos(92.02722f, -209.27747f);
        var boss = Actor(1, 0x1FAF, origin);
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
        var cry = Component<GenericAOEs>(module, prefix + "CryOfVictory");
        var trample = Component<GenericAOEs>(module, prefix + "Trample");
        var helper = Actor(3, 0x18D6, origin);
        var player = Actor(2, 0, new(89.188965f, -196.03265f), player: true);
        var charge = Cast(9846, origin);
        charge.TotalTime = 4.2f;
        charge.Rotation = 3.0346911f.Radians();
        cry.OnCastStarted(boss, charge);
        Check(Hints().GoalZones.Count == 0, "opening Trample does not predict the late-phase Cry of Victory");

        cry.OnEventCast(helper, Effect(9867));
        cry.OnCastStarted(boss, charge);
        cry.OnCastFinished(boss, charge);
        Check(Hints().GoalZones.Count == 0, "interrupted Trample drops its follow-up cue");
        cry.OnCastStarted(boss, charge);
        trample.OnCastStarted(boss, charge);
        var preparing = Hints();
        var global = new BossComponent.GlobalHints();
        cry.AddGlobalHints(player, global);
        Check(global.Count == 1 && preparing.GoalZones.Count == 1 && preparing.ForbiddenZones.Count == 4
            && cry.ActiveAOEs(0, player).IsEmpty, "late Trample gives advance positioning while keeping its loops and crossing binding");

        world.Frame = new(Now.AddSeconds(7.09), 0, 0, 0, 0, 2);
        trample.OnEventCast(helper, Effect(9855));
        var context = new NavigationDecision.Context();
        var staging = NavigationDecision.Build(context, world.CurrentTime, Hints(), player, forbiddenZoneCushion: 0.5f);
        Check(staging.Destination is { } && (staging.Destination.Value - origin).Length() < (player.Position - origin).Length(),
            "the confirmed return point gives an inward route while Rofocale turns");
        var move = staging.Destination!.Value - player.Position;
        var afterStaging = player.Position + move.Normalized() * Math.Min(move.Length(), 1.2495f * 6f);
        world.Frame = new(world.CurrentTime.AddSeconds(1.2495), 0, 0, 0, 0, 3);
        var cone = Cast(10060, origin);
        cone.TotalTime = 2.7f;
        cone.ElapsedTime = 0.02325f;
        cone.Rotation = (-0.2129872f).Radians();
        cry.OnCastStarted(boss, cone);
        var active = Hints();
        Check(active.GoalZones.Count == 0 && active.ForbiddenZones.Count == 1 && cry.ActiveAOEs(0, player)[0].Check(player.Position),
            "the cast replaces staging with the actual cone and its deadline");
        var fromRest = NavigationDecision.Build(context, world.CurrentTime, active, player, forbiddenZoneCushion: 0.5f);
        player.PosRot = afterStaging.ToVec4();
        var dodge = NavigationDecision.Build(context, world.CurrentTime, active, player, forbiddenZoneCushion: 0.5f);
        var safe = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(fromRest.LeewaySeconds < 0f && dodge.Destination != null && dodge.LeewaySeconds >= 0f
            && module.Arena.InBounds(safe) && active.ForbiddenZones[0].shapeDistance.Distance(safe) > 0.5f,
            "using the recorded turn window makes the native cone dodge reachable from a recorded Trample position");
        cry.OnEventCast(boss, Effect(10060));
        Check(Hints().ForbiddenZones.Count == 0, "the follow-up cone retires at impact");

        cry.OnEventCast(boss, Effect(9844));
        Check(Hints().GoalZones.Count == 1, "a resolved Chariot starts staging without guessing the cone facing");
        cry.OnActorDeath(boss);
        cry.OnCastStarted(boss, charge);
        Check(Hints().GoalZones.Count == 0, "boss death resets the preparation and late-phase state");
        cry.OnEventCast(helper, Effect(9876));
        cry.OnCastStarted(boss, charge);
        Check(Hints().GoalZones.Count == 1, "alternate Heavenly Subjugation also enables the late Trample follow-up");

        AIHints Hints()
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            trample.AddAIHints(0, player, default, result);
            cry.AddAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
    }

    private static void RabanastreOverlapRoutes()
    {
        const string alliance = "Stormblood.Alliance.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        using var mateus = CreateModule("A11Mateus", 0x1F9B, new(-320, 240));
        var blizzard = Component<GenericAOEs>(mateus, alliance + "A11Mateus.BlizzardIV");
        WPos[] impacts = [new(-320.03f, 264.97f), new(-340.02f, 219.99f), new(-320.06f, 214.98f),
            new(-300.01f, 219.99f), new(-320.03f, 239.98f), new(-295.00f, 239.98f)];
        for (var i = 0; i < impacts.Length; ++i)
            AddCast(blizzard, (ulong)(10 + i), 9790, impacts[i], 9.7f, 0.01887f);
        CheckOverlap(mateus, new(-311.007f, 238.449f), 6, "six simultaneous Blizzard IV markers", blizzard);

        using var hashmal = CreateModule("A12Hashmal", 0x1F83, new(-320, -46));
        var hammer = Component<GenericAOEs>(hashmal, alliance + "A12Hashmal.EarthHammer");
        hammer.OnActorCreated(Actor(70, 0x1EA7E1, new(-304.444f, -61.5563f)));
        hammer.OnActorCreated(Actor(71, 0x1EA7E1, new(-320.0005f, -58f)));
        hammer.OnActorCreated(Actor(72, 0x1EA7E1, new(-308f, -46f)));
        CheckOverlap(hashmal, new(-314.218f, -54.285f), 3, "three recorded Earth Hammer shadows", hammer);
        CheckOverlap(hashmal, new(-334.742f, -51.337f), 3, "three recorded Earth Hammer shadows from the west", hammer);
        var towers = Component<GenericAOEs>(hashmal, alliance + "A12Hashmal.Towerfall");
        var jagged = Component<GenericAOEs>(hashmal, alliance + "A12Hashmal.JaggedEdge");
        world.Frame = new(Now.AddSeconds(-6.803), 0, 0, 0, 0, 1);
        var tower1 = Actor(20, 0x1EA7DF, new(-320, -57));
        tower1.PosRot.W = -0.32918763f;
        var tower2 = Actor(21, 0x1EA7DF, new(-310.474f, -40.5f));
        tower2.PosRot.W = -1.6201482f;
        towers.OnActorCreated(tower1);
        towers.OnActorCreated(tower2);
        world.Frame = new(Now, 0, 0, 0, 0, 2);
        AddCast(jagged, 22, 9677, new(-318.624f, -30.442f), 3f, 0.02346f);
        AddCast(jagged, 23, 9677, new(-306.905f, -55.253f), 3f, 0.02346f);
        AddCast(jagged, 24, 9677, new(-300.954f, -57.115f), 3f, 0.02346f);
        CheckOverlap(hashmal, new(-302.04f, -55.933f), 5, "double Towerfall and Jagged Edge", towers, jagged);

        // Exercise the sheet-backed wide cast using the origin of a recorded
        // narrow tower and a synthetic eastward orientation.
        var wide = Component<GenericAOEs>(hashmal, alliance + "A12Hashmal.WideTowerfall");
        var wideHelper = Actor(25, 0x18D6, tower1.Position);
        var wideCast = Cast(9674, wideHelper.Position);
        wideCast.TotalTime = 9f - ActorCastInfo.NPCFinishDelay;
        wideCast.Rotation = 90f.Degrees();
        wide.OnCastStarted(wideHelper, wideCast);
        var lane = wide.ActiveAOEs(0, hashmal.PrimaryActor)[0];
        Check(lane.Activation == Now.AddSeconds(9) && lane.Check(new(-307, -64.5f)) && !lane.Check(new(-307, -65.5f)),
            "the wide Towerfall publishes its nine-second deadline and sixteen-yalm lane");
        CheckOverlap(hashmal, new(-302.04f, -55.933f), 6, "wide Towerfall with the recorded Towerfall and Jagged Edge overlap", towers, jagged, wide);
        wide.OnEventCast(wideHelper, Effect(9668));
        Check(wide.ActiveAOEs(0, hashmal.PrimaryActor).Length == 1, "the narrow impact does not retire the wide cast");
        wide.OnEventCast(wideHelper, Effect(9674));
        Check(wide.ActiveAOEs(0, hashmal.PrimaryActor).IsEmpty && towers.ActiveAOEs(0, hashmal.PrimaryActor).Length == 2
            && jagged.ActiveAOEs(0, hashmal.PrimaryActor).Length == 3, "the wide impact leaves independent tower and puddle warnings intact");
        wide.OnCastStarted(wideHelper, wideCast);
        wide.OnActorDeath(hashmal.PrimaryActor);
        Check(wide.ActiveAOEs(0, hashmal.PrimaryActor).IsEmpty, "the wide cast clears on encounter death");

        using var argath = CreateModule("A14Argath", 0x1F97, new(106, -386));
        var tiles = Component<GenericAOEs>(argath, alliance + "A14Argath.BleedingTiles");
        var rays = Component<GenericAOEs>(argath, alliance + "A14Argath.Heartless");
        WPos[] tilePositions = [new(122, -386), new(106, -402), new(106, -370), new(90, -386)];
        world.Frame = new(Now.AddSeconds(-11.05), 0, 0, 0, 0, 3);
        for (var i = 0; i < tilePositions.Length; ++i)
            tiles.OnActorEAnim(Actor((ulong)(30 + i), 0x1EA7ED, tilePositions[i]), 0x00040008);
        world.Frame = new(Now, 0, 0, 0, 0, 4);
        (WPos origin, float angle)[] casts = [
            (new(127.398f, -416.068f), 0), (new(122.240f, -410.910f), 90),
            (new(114.672f, -397.482f), 0), (new(119.799f, -392.355f), -90),
            (new(132.555f, -410.910f), -90), (new(109.514f, -392.355f), 90),
            (new(127.398f, -405.783f), 180), (new(114.672f, -387.198f), 180)];
        for (var i = 0; i < casts.Length; ++i)
            AddCast(rays, (ulong)(40 + i), 9778, casts[i].origin, 3.7f, 0.01667f, casts[i].angle);
        CheckOverlap(argath, new(106.144f, -388.856f), 12, "Heartless crosses with four live bleeding tiles", tiles, rays);

        // Coldblood 426080 is already casting when the eight rays start at
        // 427185. Its impact follows them by 2.68 seconds.
        var cold = Component<GenericAOEs>(argath, alliance + "A14Argath.ColdbloodProximity");
        var coldOrigin = new WPos(85.98462f, -386.0075f);
        AddCast(cold, 50, 9767, coldOrigin, 7.7f, 1.3427f);
        var observer = Actor(60, 0, new(104f, -388f), true);
        var words = new BossComponent.GlobalHints();
        var local = new BossComponent.TextHints();
        cold.AddGlobalHints(observer, words);
        cold.AddHints(0, observer, local);
        Check(cold.ActiveAOEs(0, observer)[0].Color == Colors.AOE && rays.ActiveAOEs(0, observer)[0].Color == Colors.Danger
            && words.Count == 1 && local.Count == 0, "the later Coldblood marker stays shaded while the sword crosses take precedence");
        CheckOverlap(argath, new(106.144f, -388.856f), 13, "Heartless, four live bleeding tiles and Coldblood", tiles, rays, cold);
        for (var i = 0; i < casts.Length; ++i)
            rays.OnEventCast(Actor((ulong)(40 + i), 0x1F98, casts[i].origin), Effect(9778));
        cold.AddHints(0, observer, local);
        Check(rays.ActiveAOEs(0, observer).IsEmpty && cold.ActiveAOEs(0, observer)[0].Color == Colors.Danger && local.Count == 1,
            "the individual Heartless hits release the first wave and advance the Coldblood marker");

        BossModule CreateModule(string name, uint oid, WPos center)
            => (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + alliance + name + "." + name, true)!, world, Actor(1, oid, center))!;

        static void AddCast(BossComponent component, ulong id, uint action, WPos origin, float total, float elapsed, float angle = 0)
        {
            var cast = Cast(action, origin);
            cast.TotalTime = total;
            cast.ElapsedTime = elapsed;
            cast.Rotation = angle.Degrees();
            component.OnCastStarted(Actor(id, 0x18D6, origin), cast);
        }

        static void CheckOverlap(BossModule module, WPos start, int hazardCount, string name, params BossComponent[] components)
        {
            var player = Actor(2, 0, start, true);
            var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            foreach (var component in components)
                component.AddAIHints(0, player, default, hints);
            Check(hints.ForbiddenZones.Count == hazardCount && hints.ForbiddenZones.Any(z => z.shapeDistance.Contains(start)),
                name + " presents every hazard and starts inside one");
            hints.Normalize();
            CheckRabanastreRoute(module, player, hints, name);
        }
    }

    private static void CheckRabanastreRoute(BossModule module, Actor player, AIHints hints, string name)
    {
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        var lastDeadline = (float)(hints.ForbiddenZones.Max(z => z.activation) - Now).TotalSeconds;
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && route.LeewaySeconds >= 0f
            && route.TimeToGoal + NavigationDecision.ActivationTimeCushion <= lastDeadline
            && hints.ForbiddenZones.All(z => z.shapeDistance.Distance(endpoint) > 0.5f),
            $"{name} reaches safety from {player.Position}, destination {endpoint}, travel {route.TimeToGoal:f2}s, leeway {route.LeewaySeconds:f2}s");
    }

    private static void RabanastreUnrelenting()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var origin = new WPos(105.97388f, -394.00323f);
        var boss = Actor(1, 0x1F97, origin);
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A14Argath", true)!, world, boss)!;
        var dread = Component<CastHint>(module, prefix + "GnawingDread");
        var fan = Component<GenericAOEs>(module, prefix + "Unrelenting");
        var player = Actor(2, 0, new(120.6f, -397.3f), player: true);
        player.Statuses[0] = new(1422, 0x2D0, Now.AddSeconds(2.29d), 0);
        var visual = Cast(9771, origin);
        visual.TotalTime = 2.2f;
        fan.OnCastStarted(boss, visual);
        var global = new BossComponent.GlobalHints();
        fan.AddGlobalHints(player, global);
        Check(global.Count == 1 && fan.ActiveAOEs(0, player).IsEmpty,
            "the visual cast gives a warning before the helper cones establish the actual lanes");

        float[] directions = [2.3152435f, -2.9206963f, 1.2680938f, 2.8388183f, 1.7916687f];
        var helpers = new Actor[directions.Length];
        for (var i = 0; i < directions.Length; ++i)
        {
            helpers[i] = Actor((ulong)(3 + i), 0x18D6, origin);
            var cast = Cast(9772, origin);
            cast.TotalTime = 2.7f;
            cast.Rotation = directions[i].Radians();
            fan.OnCastStarted(helpers[i], cast);
        }

        var hints = Hints();
        Check(hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Misdirection && hints.ForbiddenZones.Count == 5
            && hints.ForbiddenZones.Select(zone => zone.activation).Distinct().Count() == 1
            && hints.GoalZones.Count == 1,
            "all five simultaneous cones remain active with an extra clearance preference for the arrow");
        var text = new BossComponent.TextHints();
        fan.AddHints(0, player, text);
        Check(text.Any(hint => hint.Item1.Contains("arrow")), "a player in a cone gets an arrow-specific response");
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && hints.GoalZones[0](endpoint) == 6f
            && route.TimeToGoal + NavigationDecision.ActivationTimeCushion <= 3f
            && hints.ForbiddenZones.All(zone => zone.shapeDistance.Distance(endpoint) >= 1f),
            "native navigation can reach a clear gap with extra room before the fan resolves");

        player.PosRot.X = 119.279785f;
        player.PosRot.Z = -401.05292f;
        hints = Hints();
        Check(hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(player.Position)) && hints.GoalZones.Count == 0,
            "a recorded safe gap does not trigger elective movement with the arrow");
        fan.OnEventCast(helpers[3], Effect(9772));
        Check(fan.ActiveAOEs(0, player).Length == 4, "one helper's impact retires only its own cone");
        fan.OnActorDestroyed(helpers[1]);
        Check(fan.ActiveAOEs(0, player).Length == 3, "helper removal cancels only its cone");
        fan.OnActorDeath(boss);
        global.Clear();
        fan.AddGlobalHints(player, global);
        Check(fan.ActiveAOEs(0, player).IsEmpty && global.Count == 0, "boss death clears the fan and its warning");

        AIHints Hints()
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            dread.AddAIHints(0, player, default, result);
            fan.AddAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
    }

    private static void RabanastreGnawingDread()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(1, 0x1F97, new(105.97388f, -394.00323f));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A14Argath", true)!, world, boss)!;
        var dread = Component<CastHint>(module, prefix + "GnawingDread");
        var departure = Component<BossComponent>(module, prefix + "ColdbloodDeparture");
        var cold = Component<GenericAOEs>(module, prefix + "ColdbloodDonut");
        var player = Actor(2, 0, new(97.71f, -380.26f), player: true);
        var dreadCast = Cast(9761, boss.Position);
        dreadCast.TotalTime = 4.7f;
        dreadCast.ElapsedTime = 0.01985f;
        dread.OnCastStarted(boss, dreadCast);
        var hints = Hints();
        var global = new BossComponent.GlobalHints();
        dread.AddGlobalHints(player, global);
        Check(global.Count == 1 && hints.GoalZones.Count == 1 && hints.ForbiddenZones.Count == 0
            && hints.ImminentSpecialMode == default,
            "Gnawing Dread stages inward before the status without guessing the next attack's origin");
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var staging = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(staging) && hints.GoalZones[0](staging) > 0
            && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < dreadCast.NPCRemainingTime,
            "the native planner reaches staging from a fresh recorded position before misdirection");

        dread.OnCastFinished(boss, dreadCast);
        Check(Hints().GoalZones.Count == 0, "a finished or interrupted Gnawing Dread cast clears staging");
        dread.OnCastStarted(boss, dreadCast);
        dread.OnActorDestroyed(boss);
        Check(Hints().GoalZones.Count == 0, "caster removal clears the staging cue");

        var nearRim = Actor(4, 0, new(124, -400), player: true);
        departure.OnEventCast(nearRim, Effect(9765));
        Check(Hints(nearRim).GoalZones.Count == 0, "only Argath's departure can announce Coldblood");
        departure.OnEventCast(boss, Effect(9765));
        hints = Hints(nearRim);
        global.Clear();
        departure.AddGlobalHints(nearRim, global);
        route = NavigationDecision.Build(context, Now, hints, nearRim, forbiddenZoneCushion: 0.5f);
        var inward = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(global.Count == 1 && hints.GoalZones.Count == 1 && hints.ForbiddenZones.Count == 0
            && route.Destination != null && hints.GoalZones[0](inward) > 0 && route.TimeToGoal < 2.02f,
            "the shared departure gives an attainable rim buffer before the cast without guessing the variant");
        Check(Hints(player).GoalZones.Count == 0, "the departure leaves an already central player in place");
        departure.OnCastStarted(boss, Cast(9767, new(85.98462f, -386.0075f)));
        Check(Hints(nearRim).GoalZones.Count == 0, "the confirmed proximity cast retires the generic departure goal");
        departure.OnEventCast(boss, Effect(9765));
        departure.OnActorTargetable(boss);
        Check(Hints(nearRim).GoalZones.Count == 0, "an aborted departure clears on Argath's return");
        departure.OnEventCast(boss, Effect(9765));
        world.Frame = new(Now.AddSeconds(2.8), 0, 0, 0, 0, 2);
        Check(Hints(nearRim).GoalZones.Count == 0, "a departure with no follow-up expires");

        world.Frame = new(Now.AddSeconds(9.18), 0, 0, 0, 0, 2);
        player.PosRot.X = staging.X;
        player.PosRot.Z = staging.Z;
        player.Statuses[0] = new(1422, 0x2D0, Now.AddSeconds(18.83), 0);
        Check(Hints().ImminentSpecialMode.mode == AIHints.SpecialMode.Misdirection,
            "an already active debuff enables the native arrow handling after a late module activation");
        Check(Hints(Actor(3, 0, player.Position, player: true)).ImminentSpecialMode == default,
            "only a player with the debuff uses the arrow movement mode");

        var center = new WPos(105.97388f, -406.02734f);
        var donutCast = Cast(9766, center);
        donutCast.TotalTime = 11.7f;
        donutCast.ElapsedTime = 0.01833f;
        nearRim.Statuses[0] = player.Statuses[0];
        departure.OnEventCast(boss, Effect(9765));
        Check(Hints(nearRim).GoalZones.Count == 0 && Hints(nearRim).ImminentSpecialMode.mode == AIHints.SpecialMode.Misdirection,
            "the early cue does not spend an arrow turn on an unknown direction");
        departure.OnCastStarted(boss, donutCast);
        global.Clear();
        departure.AddGlobalHints(player, global);
        Check(global.Count == 0, "the confirmed donut replaces the shared departure warning");
        cold.OnCastStarted(boss, donutCast);
        hints = Hints();
        var text = new BossComponent.TextHints();
        cold.AddHints(0, player, text);
        Check(text.Any(hint => hint.Item1.Contains("arrow")) && hints.GoalZones.Single()(center) > hints.GoalZones[0](player.Position)
            && hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(player.Position),
            "Coldblood explains the arrow and publishes the real safe circle and cast deadline");
        route = NavigationDecision.Build(context, world.CurrentTime, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && endpoint.InCircle(center, 4.5f)
            && route.LeewaySeconds >= 0f && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < donutCast.NPCRemainingTime,
            "staging permits an in-bounds native route to the observed Coldblood opening");

        var recordedSafe = new WPos(103.37976f, -402.7619f);
        player.PosRot = new(recordedSafe.X, -679, recordedSafe.Z, 0);
        hints = Hints();
        route = NavigationDecision.Build(context, world.CurrentTime, hints, player, forbiddenZoneCushion: 0.5f);
        Check(hints.GoalZones.Single()(recordedSafe) == hints.GoalZones[0](center)
            && (route.Destination == null || route.Destination.Value.InCircle(recordedSafe, MathF.Sqrt(2f) * module.Bounds.MapResolution)),
            $"the buffered interior needs at most an adjacent grid adjustment, destination {route.Destination}, start {recordedSafe}");
        player.PosRot.X = staging.X;
        player.PosRot.Z = staging.Z;

        world.Frame = new(Now.AddSeconds(18.84), 0, 0, 0, 0, 3);
        text.Clear();
        cold.AddHints(0, player, text);
        Check(Hints().ImminentSpecialMode == default && text.Any(hint => hint.Item1.Contains("safe circle") && !hint.Item1.Contains("arrow")),
            "arrow expiration restores normal movement while Coldblood is still casting");
        cold.OnCastFinished(boss, donutCast);
        hints = Hints();
        Check(hints.GoalZones.Count == 0 && hints.ForbiddenZones.Count == 0, "resolved Coldblood clears its geometry and guidance");

        AIHints Hints(Actor? target = null)
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            dread.AddAIHints(0, target ?? player, default, result);
            departure.AddAIHints(0, target ?? player, default, result);
            cold.AddAIHints(0, target ?? player, default, result);
            return result;
        }
    }

    private static void RabanastreCommands()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var module = Module(prefix + "A14Argath", new(106, -386));
        var boss = Actor(1, 0x1F97, module.Center);
        var helper = Actor(2, 0x18D6, module.Center);
        var player = Actor(3, 0, new(106, -390), player: true);
        var unassigned = Actor(4, 0, player.Position, player: true);
        var activation = Now.AddSeconds(5);
        module.PrimaryActor = boss;
        var command = Component<GenericGaze>(module, prefix + "DivineCommandment");
        player.Statuses[0] = new(1425, 0, activation, 0);
        Check(command.ActiveEyes(0, player).IsEmpty, "an unknown mask cannot assign a direction");
        command.OnEventCast(boss, Effect(9754));
        var global = new BossComponent.GlobalHints();
        command.AddGlobalHints(unassigned, global);
        Check(global.Single().Contains("Mask of Truth") && command.ActiveEyes(1, unassigned).IsEmpty,
            "the confirmed mask warns before the player's command without assigning a gaze");
        var eyes = command.ActiveEyes(0, player);
        Check(eyes.Length == 1 && !eyes[0].Inverted && eyes[0].Activation == activation, "the truth mask publishes a timed look-away assignment");
        var towardBoss = player.AngleTo(boss);
        player.PosRot.W = (towardBoss + 86.3f.Degrees()).Rad;
        var text = new BossComponent.TextHints();
        command.AddHints(0, player, text);
        var gaze = CommandHints();
        Check(GenericGaze.HitByEye(player, eyes[0]) && text.Count == 1 && text[0].Item2
            && gaze.ForbiddenDirections.Single().halfWidth == 90f.Degrees()
            && !GenericGaze.HitByEye(player, new GenericGaze.Eye(boss.Position)),
            "the recorded side-on failure warns through the radar, text and AI while ordinary gazes retain their default angle");
        module.WorldState.Frame = new(activation.AddSeconds(-0.25d), 0, 0, 0, 0, 2);
        var rotation = new SmartRotationTweak(module.WorldState, gaze).GetSafeRotation(player.Rotation, null, default);
        Check(rotation is { } safe && (safe - towardBoss).Normalized().Abs().Deg > 90f,
            "native facing rotates from the recorded unsafe angle into the rear half-plane");
        module.WorldState.Frame = new(Now, 0, 0, 0, 0, 1);
        command.OnEventCast(boss, Effect(9753));
        eyes = command.ActiveEyes(0, player);
        Check(eyes.Length == 1 && eyes[0].Inverted && CommandHints().ForbiddenDirections.Single().halfWidth == 135f.Degrees(),
            "the lie mask still requires facing within 45 degrees of Argath");
        player.Statuses[0] = new(1424, 0, activation, 0);
        var hints = new AIHints();
        command.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenDirections.Count == 0 && hints.ImminentSpecialMode.mode == AIHints.SpecialMode.Pyretic && hints.ImminentSpecialMode.activation == activation,
            "the reversed movement command stops actions and movement for the assigned player");
        text.Clear();
        command.AddHints(0, player, text);
        Check(text.Count == 1 && text[0] is ("Stop moving and attacking in 4.0s.", false),
            "the stop order advertises when to stop while there is still time to move");
        module.WorldState.Frame = new(activation.AddSeconds(-1d), 0, 0, 0, 0, 2);
        text.Clear();
        command.AddHints(0, player, text);
        Check(text.Count == 1 && text[0] is ("Stop moving and attacking until The Word!", true),
            "the stop order becomes urgent at the native hold boundary");
        command.OnEventCast(helper, Effect(9376));
        hints = new();
        command.AddAIHints(0, player, default, hints);
        Check(hints.ImminentSpecialMode == default, "the Word clears the resolved mask");

        module.WorldState.Frame = new(Now, 0, 0, 0, 0, 1);
        command.OnEventCast(boss, Effect(9754));
        hints = new();
        command.AddAIHints(0, player, default, hints);
        text.Clear();
        command.AddHints(0, player, text);
        Check(hints.ForbiddenZones.Count == 0 && text.Count == 1 && !text[0].Item2,
            "the movement command gives advance notice without forcing an early departure");
        module.WorldState.Frame = new(activation.AddSeconds(-1.25d), 0, 0, 0, 0, 2);
        command.AddAIHints(0, player, default, hints);
        text.Clear();
        command.AddHints(0, player, text);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Distance(player.Position) < 0 && hints.ForcedMovement == null
            && text.Count == 1 && text[0] is ("Keep moving until The Word!", true),
            "the move warning becomes urgent when the AI starts requesting a hazard-aware step");
        hints.PathfindMapBounds = module.Bounds;
        hints.PathfindMapCenter = module.Center;
        var concurrent = new SDCircle(player.Position + new WDir(1.75f, 0), 1f);
        hints.AddForbiddenZone(concurrent, module.WorldState.CurrentTime);
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, module.WorldState.CurrentTime, hints, player, forbiddenZoneCushion: 0.5f);
        var step = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(step)
            && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(step)),
            $"the movement order still finds a step that avoids a simultaneous hazard, endpoint {step}, first step {route.Destination}");
        var oldPosition = player.Position;
        player.PosRot.X += 2;
        hints = new();
        command.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Distance(oldPosition) > 0, "the move request advances after the player steps");
        command.OnEventCast(helper, Effect(9376));
        hints = new();
        command.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "resolution stops requesting movement");

        module.WorldState.Frame = new(activation.AddSeconds(-1), 0, 0, 0, 0, 2);
        command.OnEventCast(boss, Effect(9753));
        LoseOrder(1424, activation, activation.AddSeconds(-0.9));
        Check(CommandHints().ImminentSpecialMode == default, "an early cancellation releases the hold immediately");

        foreach (var (mask, status) in new (uint Mask, uint Status)[] { (9753, 1424), (9754, 1424), (9753, 1425), (9754, 1425) })
        {
            module.WorldState.Frame = new(activation.AddSeconds(-1), 0, 0, 0, 0, 3);
            command.OnEventCast(boss, Effect(mask));
            LoseOrder(status, activation, activation.AddSeconds(0.08));
            var late = CommandHints();
            Check(status == 1425 ? late.ForbiddenDirections.Count == 1
                : mask == 9753 ? late.ImminentSpecialMode.mode == AIHints.SpecialMode.Pyretic
                : late.ForbiddenZones.Count == 1, "the recorded expiry gap preserves the assigned response");
            command.OnEventCast(helper, Effect(9376));
            late = CommandHints();
            Check(late.ForbiddenDirections.Count == 0 && late.ForbiddenZones.Count == 0 && late.ImminentSpecialMode == default,
                "The Word immediately releases the retained order");
        }

        module.WorldState.Frame = new(activation.AddSeconds(-1), 0, 0, 0, 0, 4);
        command.OnEventCast(boss, Effect(9753));
        LoseOrder(1425, activation, activation.AddSeconds(0.08));
        module.WorldState.Frame = new(activation.AddSeconds(0.31), 0, 0, 0, 0, 5);
        Check(CommandHints().ForbiddenDirections.Count == 0, "a missing effect cannot retain the order beyond the grace period");
        player.Statuses[0] = new(1425, 0, activation.AddSeconds(4), 0);
        command.OnActorDeath(boss);
        Check(CommandHints().ForbiddenDirections.Count == 0, "boss death clears the mask even if a status remains");

        void LoseOrder(uint status, DateTime expires, DateTime observedAt)
        {
            module.WorldState.Frame = new(observedAt, 0, 0, 0, 0, 3);
            var previous = new ActorStatus(status, 0, expires, 0);
            command.OnStatusLose(player, ref previous);
            player.Statuses[0] = default;
        }

        AIHints CommandHints()
        {
            var result = new AIHints();
            command.AddAIHints(0, player, default, result);
            return result;
        }
    }

    private static void RabanastreIceSpiral()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var module = Module(prefix + "A11Mateus", new(-320, 240));
        var boss = Actor(1, 0x1F9B, module.Center);
        module.PrimaryActor = boss;
        var spiral = Component<GenericAOEs>(module, prefix + "IceSpiral");
        var water = Component<GenericTowers>(module, prefix + "IceBubbleBlizzardIIITowers");
        var orb = Actor(2, 0x1F9E, module.Center);
        spiral.OnEventCast(boss, Effect(9780));
        spiral.OnActorCreated(orb);
        var aoes = spiral.ActiveAOEs(0, boss);
        Check(aoes.Length == 20 && aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.AOE, "a new spiral starts at step zero with a shaded preview");
        var activation = aoes[1].Activation;
        spiral.OnEventCast(boss, Effect(9780));
        Check(spiral.ActiveAOEs(0, boss)[0].Activation == activation && spiral.ActiveAOEs(0, boss)[0].Color == Colors.Danger, "the next spiral pulse becomes bright");
        spiral.OnCastStarted(boss, Cast(9781, boss.Position));
        Check(spiral.ActiveAOEs(0, boss).IsEmpty, "Rebind clears the spiral even if the pulse sequence was incomplete");

        var skater = Actor(3, 0x1F9D, new(-320, 251.8f));
        spiral.OnActorCreated(skater);
        spiral.OnActorCreated(orb);
        spiral.Update();
        var moved = new WPos(-320f, 241.8f) + 10.28f * (-0.24f).Radians().ToDirection();
        skater.PosRot.X = moved.X;
        skater.PosRot.Z = moved.Z;
        module.WorldState.Frame = new(Now.AddSeconds(0.4), 0, 0, 0, 0, 1);
        spiral.OnEventCast(boss, Effect(9780));
        spiral.Update();
        aoes = spiral.ActiveAOEs(0, boss);
        Check(aoes.Length == 5 && aoes[0].Origin.X < moved.X && aoes[0].Origin.Z > 245 && aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.AOE,
            "live skater motion anchors the next bright pulse ahead of the actor and shades later pulses");
        Check(aoes[0].Activation == module.WorldState.CurrentTime.AddSeconds(0.23d), "the observed pulse resets the live forecast clock");
        var hints = new AIHints();
        spiral.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 8, "the AI retains the full timed short forecast when the radar shows five");
        module.WorldState.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        spiral.Update();
        Check(spiral.ActiveAOEs(0, boss).IsEmpty, "stale skating telemetry does not reactivate the old sampled lane");
        var staleSpout = Actor(60, 0x1EA1A1, new(-319.485f, 242.37f));
        var staleSoaker = Actor(61, 0, staleSpout.Position, true);
        water.OnActorEAnim(staleSpout, 0x00040008);
        hints = new();
        water.AddAIHints(0, staleSoaker, default, hints);
        Check(hints.GoalZones.Count == 0, "a stale live lane still screens the spout against the sampled passage");
        water.OnActorDestroyed(staleSpout);
        spiral.OnActorDestroyed(skater);
        Check(spiral.ActiveAOEs(0, boss).IsEmpty, "skater despawn retires the forecast");

        skater = Actor(4, 0x1F9D, new(-320, 251.8f));
        spiral.OnActorCreated(skater);
        spiral.OnEventCast(boss, Effect(9780));
        spiral.Update();
        skater.PosRot.X = moved.X;
        skater.PosRot.Z = moved.Z;
        module.WorldState.Frame = new(Now.AddSeconds(1.4), 0, 0, 0, 0, 1);
        spiral.Update();
        Check(spiral.ActiveAOEs(0, boss).Length == 5, "an observed pulse and skater motion bootstrap the forecast after all water orbs are gone");

        spiral.OnActorDestroyed(skater);
        module.WorldState.Frame = new(Now, 0, 0, 0, 0, 1);
        var earlyOrb = Actor(5, 0x1F9E, new(-319.708f, 241.343f));
        var spout = Actor(6, 0x1EA1A1, new(-348, 268));
        var player = Actor(7, 0, new(-327.708f, 241.343f), true);
        spiral.OnActorCreated(earlyOrb);
        var grazedSpout = Actor(62, 0x1EA1A1, earlyOrb.Position + new WDir(0, -3.89f));
        var grazedSoaker = Actor(63, 0, grazedSpout.Position, true);
        water.OnActorEAnim(grazedSpout, 0x00040008);
        module.WorldState.Frame = new(Now.AddSeconds(6.1d), 0, 0, 0, 0, 2);
        hints = new();
        water.AddAIHints(0, grazedSoaker, default, hints);
        Check(hints.GoalZones.Count == 0, "a two-yalm ice pulse grazing the spout edge releases its occupant before the pulse arrives");
        water.OnActorDestroyed(grazedSpout);
        module.WorldState.Frame = new(Now, 0, 0, 0, 0, 1);
        water.OnActorCreated(earlyOrb);
        water.OnActorCreated(earlyOrb);
        water.OnActorEAnim(spout, 0x00040008);
        Check(water.Towers.Count == 2, "duplicate orb creation does not duplicate the objective");

        hints = new();
        var text = new BossComponent.TextHints();
        water.AddAIHints(0, player, default, hints);
        water.AddHints(0, player, text);
        Check(hints.GoalZones.Single()(earlyOrb.Position) > hints.GoalZones[0](spout.Position)
            && text.Single().Item1.Contains("water orb"), "the nearby orb is preferred while there is time to reach and leave it");

        module.WorldState.Frame = new(Now.AddSeconds(5), 0, 0, 0, 0, 2);
        hints = new();
        text.Clear();
        water.AddAIHints(0, player, default, hints);
        water.AddHints(0, player, text);
        Check(hints.GoalZones.Single()(spout.Position) > hints.GoalZones[0](earlyOrb.Position)
            && text.Single().Item1.Contains("three players"), "the spout replaces an orb that is too far away to reach and leave before the skater");

        player.PosRot.X = spout.Position.X;
        player.PosRot.Z = spout.Position.Z;
        text.Clear();
        water.AddHints(0, player, text);
        Check(text.Single().Item1.StartsWith("Stay"), "the spout asks a player already inside to hold their place");
        water.OnActorDeath(earlyOrb);
        Check(water.Towers.Single().ActorID == spout.InstanceID, "an orb's death removes only that orb");
        water.OnActorEAnim(spout, 0x00400080);
        Check(water.Towers.Count == 0, "the recorded spout completion animation retires the objective");

        // The recorded west and south spouts are both live. Already occupied
        // places count toward the west spout, leaving others to fill the south.
        var westSpout = Actor(30, 0x1EA1A1, new(-346.2f, 238f));
        var southSpout = Actor(31, 0x1EA1A1, new(-320.9355f, 265.5193f));
        var spareOrb = Actor(32, 0x1F9E, new(-312, 265));
        water.OnActorEAnim(westSpout, 0x00040008);
        water.OnActorEAnim(southSpout, 0x00040008);
        water.OnActorCreated(spareOrb);
        var occupiedWestA = AddSoaker(0, westSpout.Position);
        var occupiedWestB = AddSoaker(1, westSpout.Position + new WDir(1f, 0f));
        var fillWest = AddSoaker(2, westSpout.Position + new WDir(3f, 0f));
        var fillSouthA = AddSoaker(3, southSpout.Position + new WDir(3f, 0f));
        var fillSouthB = AddSoaker(4, southSpout.Position + new WDir(4f, 0f));
        var fillSouthC = AddSoaker(5, southSpout.Position + new WDir(5f, 0f));
        var spare = AddSoaker(6, southSpout.Position + new WDir(7f, 0f));
        Check(Goal(0, occupiedWestA)(westSpout.Position) == 12f && Goal(1, occupiedWestB)(westSpout.Position) == 12f
            && Goal(2, fillWest)(westSpout.Position) == 12f,
            "two existing occupants reserve their places and only one new player is sent to the west spout");
        Check(Goal(3, fillSouthA)(southSpout.Position) == 12f && Goal(4, fillSouthB)(southSpout.Position) == 12f
            && Goal(5, fillSouthC)(southSpout.Position) == 12f && Goal(6, spare)(spareOrb.Position) == 12f,
            "the south spout reserves three players and leaves the spare player free to pop the orb");
        water.OnActorEAnim(westSpout, 0x00400080);
        Check(water.Towers.Count == 2 && Goal(3, fillSouthA)(southSpout.Position) == 12f,
            "completing the west spout does not clear the simultaneous south assignment");
        water.OnCastStarted(boss, Cast(9781, boss.Position));
        Check(water.Towers.Count == 0, "Rebind clears every remaining assignment");

        Actor AddSoaker(int slot, WPos position)
        {
            var id = (ulong)(slot + 40);
            module.WorldState.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
                new(position.X, 1.23f, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            module.WorldState.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return module.WorldState.Actors.Find(id)!;
        }

        Func<WPos, float> Goal(int slot, Actor soaker)
        {
            var result = new AIHints();
            water.AddAIHints(slot, soaker, default, result);
            return result.GoalZones.Single();
        }
    }

    private static void RabanastreHeavenlySubjugation()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var center = new WPos(106, -191);
        var boss = Actor(1, 0x1FAF, center);
        boss.PosRot.Y = -694;
        boss.IsTargetable = false;
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
        var demons = Component<BossComponent>(module, prefix + "Archaeodemons");
        var ultimate = Component<RaidwideCast>(module, prefix + "HeavenlySubjugation");
        world.Execute(new ActorState.OpCreate(2, 0, 0, 0, "", 0, ActorType.Player, Class.WHM, 70,
            new(120, -694, -184, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 2, false)));
        world.Party.SetAllianceGroups(0, 1, 2);
        var player = world.Actors.Find(2)!;
        var west = AddDemon(3, 92.15f, -183f);
        var east = AddDemon(4, 119.85f, -183.02f);
        var targets = Targets();
        Check(targets.PotentialTargets[0].Priority > targets.PotentialTargets[1].Priority
            && targets.GoalZones.Single()(west.Position) > targets.GoalZones[0](east.Position),
            "the recorded west prison stays assigned to A even when its player starts near the east prison");
        var westWall = Actor(6, 0x1EA1A1, west.Position);
        westWall.LayoutID = 7082097;
        var eastWall = Actor(7, 0x1EA1A1, east.Position);
        eastWall.LayoutID = 7082095;
        demons.OnActorEAnim(westWall, 0x00040008);
        demons.OnActorEAnim(eastWall, 0x00040008);
        targets = Targets();
        Check(targets.ForbiddenZones.Count == 2 && targets.ForbiddenZones.All(z => z.activation == Now.AddSeconds(8.75d))
            && !targets.ForbiddenZones[0].shapeDistance.Contains(east.Position)
            && targets.ForbiddenZones[0].shapeDistance.Contains(east.Position + new WDir(0, 9.4f))
            && !targets.ForbiddenZones[1].shapeDistance.Contains(west.Position)
            && targets.ForbiddenZones[1].shapeDistance.Contains(east.Position),
            "the preparation animation forecasts the other prison's wall and requires entry only to the assigned prison");
        CheckRabanastreRoute(module, player, targets, "Archaeodemon entry before the wall closes");
        demons.OnActorEAnim(westWall, 0x00100020);
        demons.OnActorEAnim(eastWall, 0x00100020);
        targets = Targets();
        Check(targets.ForbiddenZones.Count == 2 && targets.PotentialTargets[0].Priority == AIHints.Enemy.PriorityInvincible
            && targets.PotentialTargets[1].Priority == 4,
            "a player caught inside a different closed prison helps the reachable add");
        var wall = targets.ForbiddenZones[0].shapeDistance;
        Check(wall.Contains(west.Position + new WDir(0, 9.4f)) && !wall.Contains(west.Position)
            && !wall.Contains(west.Position + new WDir(0, 10.4f)),
            "the active wall is a hexagonal band with walkable floor on either side");
        targets.GoalZones.Add(AIHints.GoalProximity(center, 28f, 100f));
        var prisonContext = new NavigationDecision.Context();
        NavigationDecision.Build(prisonContext, Now, targets, player, forbiddenZoneCushion: 0.5f);
        var prisonEndpoint = prisonContext.ThetaStar.CellCenter(prisonContext.ThetaStar.BestIndex());
        Check(prisonEndpoint.InCircle(east.Position, 10f), "native navigation cannot route to an exterior goal through the closed wall");
        demons.OnActorEAnim(eastWall, 0x00010040);
        Check(Targets().ForbiddenZones.Count == 1, "one reopening animation releases only its own wall");
        demons.OnActorDestroyed(westWall);
        Check(Targets().ForbiddenZones.Count == 0, "wall removal clears the remaining constraint");
        demons.OnActorEAnim(westWall, 0x00040008);
        world.Frame = new(Now.AddSeconds(10), 0, 0, 0, 0, 2);
        demons.Update();
        Check(Targets().ForbiddenZones.Count == 0, "a missing close event expires the entry forecast");
        world.Frame = new(Now, 0, 0, 0, 0, 1);

        var north = new WPos(106, -206.89f);
        var northDemon = AddDemon(9, 105.97388f, -206.89716f);
        var northWall = Actor(8, 0x1EA1A1, north);
        northWall.LayoutID = 7082094;
        demons.OnActorEAnim(westWall, 0x00040008);
        demons.OnActorEAnim(northWall, 0x00040008);
        demons.OnActorEAnim(eastWall, 0x00040008);
        world.Party.SetAllianceGroups(1, 0, 2);
        var orbit = Component<GenericAOEs>(module, prefix + "AetherialAcceleration");
        world.Frame = new(Now.AddSeconds(6.111d), 0, 0, 0, 0, 2);
        orbit.OnActorUntargetable(boss);
        world.Frame = new(Now.AddSeconds(8.044d), 0, 0, 0, 0, 3);
        boss.PosRot = new(115.65938f, -694f, -198.26904f, 2.48721f);
        orbit.Update();
        player.PosRot = new(106.92586f, -694f, -208.41316f, 0);
        targets = Targets(northDemon);
        orbit.AddAIHints(0, player, default, targets);
        targets.Normalize();
        var overlapContext = new NavigationDecision.Context();
        var overlapRoute = NavigationDecision.Build(overlapContext, world.CurrentTime, targets, player, forbiddenZoneCushion: 0.5f);
        var overlapEndpoint = overlapContext.ThetaStar.CellCenter(overlapContext.ThetaStar.BestIndex());
        Check(targets.PotentialTargets[0].Actor == northDemon && targets.PotentialTargets[0].Priority == 4
            && targets.ForbiddenZones.Count == 10 && targets.ForbiddenZones.Count(z => z.activation == Now.AddSeconds(8.75d)) == 3
            && targets.ForbiddenZones[3].shapeDistance.Contains(player.Position)
            && overlapRoute.Destination != null && overlapRoute.LeewaySeconds >= 0f && module.Arena.InBounds(overlapEndpoint)
            && targets.ForbiddenZones.All(z => z.shapeDistance.Distance(overlapEndpoint) > 0.5f),
            "native navigation finds a safe route inside the closing north prison while the circling charge is predicted");
        demons.OnActorDestroyed(northWall);
        demons.OnActorDestroyed(eastWall);
        demons.OnActorDestroyed(westWall);
        Check(Targets().ForbiddenZones.Count == 0, "removing the objects clears all pending wall and entry forecasts");
        world.Party.SetAllianceGroups(0, 1, 2);
        world.Frame = new(Now, 0, 0, 0, 0, 1);

        var karma = Component<GenericAOEs>(module, prefix + "Karma");
        player.Class = Class.PLD;
        player.PosRot = new(100.08386f, -694, -185.33894f, 0);
        west.TargetID = player.InstanceID;
        demons.OnActorEAnim(westWall, 0x00100020);
        targets = Targets();
        var outward = (west.Position - center).Normalized();
        var aim = west.Position + outward * (west.HitboxRadius + 0.5f);
        Check(targets.PotentialTargets[0].ShouldBeTanked && targets.PotentialTargets[0].DesiredRotation is { } facing
            && facing.ToDirection().Dot(outward) > 0.99f && targets.GoalZones.Single()(aim) == 8f,
            "the tank already holding the add receives an outward facing goal before Karma locks");
        var circle = new SDCircle(aim, 1.5f);
        targets.AddForbiddenZone(circle, Now);
        NavigationDecision.Build(prisonContext, Now, targets, player, forbiddenZoneCushion: 0.5f);
        prisonEndpoint = prisonContext.ThetaStar.CellCenter(prisonContext.ThetaStar.BestIndex());
        Check(prisonEndpoint.InCircle(west.Position, 8.7f) && circle.Distance(prisonEndpoint) > 0.5f,
            "a concurrent hazard takes priority over the tank's outward facing preference");
        var karmaCast = Cast(9842, west.Position);
        karmaCast.TotalTime = 2.7f;
        karmaCast.Rotation = 106.07f.Degrees();
        west.CastInfo = karmaCast;
        karma.OnCastStarted(west, karmaCast);
        targets = Targets();
        karma.AddAIHints(0, player, default, targets);
        Check(targets.PotentialTargets[0].DesiredRotation == null && targets.ForbiddenZones.Count == 2
            && targets.ForbiddenZones[^1].shapeDistance.Contains(player.Position),
            "Karma retains the recorded facing and requires the tank to dodge the locked cone");
        Check(!targets.PotentialTargets[0].ShouldBeInterrupted, "a cast without the client interrupt flag does not request an interrupt");
        karmaCast.Interruptible = true;
        east.CastInfo = Cast(9843, east.Position);
        east.CastInfo.Interruptible = true;
        demons.OnActorEAnim(eastWall, 0x00100020);
        targets = Targets();
        karma.AddAIHints(0, player, default, targets);
        Check(targets.PotentialTargets[0].ShouldBeInterrupted && !targets.PotentialTargets[1].ShouldBeInterrupted
            && targets.ForbiddenZones.Count == 3 && targets.ForbiddenZones[^1].shapeDistance.Contains(player.Position),
            "an interruptible cast in the player's prison requests the action without dropping its dodge constraint or reaching across walls");
        karmaCast.EventHappened = true;
        Check(!Targets().PotentialTargets[0].ShouldBeInterrupted, "a resolved cast cannot keep requesting an interrupt");
        demons.OnActorEAnim(eastWall, 0x00010040);
        karma.OnCastStarted(east, Cast(9842, east.Position));
        karma.OnActorDeath(west);
        Check(karma.ActiveAOEs(0, player).Length == 1 && karma.ActiveAOEs(0, player)[0].ActorID == east.InstanceID,
            "an Archaeodemon's death retires its cone independently");
        player.Class = Class.WHM;
        west.IsDead = true;
        targets = Targets();
        Check(targets.GoalZones.Count == 0 && targets.PotentialTargets.Single().Priority == AIHints.Enemy.PriorityInvincible,
            "after a kill, an open destination across the still-closed source wall cannot be selected");

        demons.OnActorEAnim(eastWall, 0x00100020);
        demons.OnActorEAnim(westWall, 0x00010040);
        targets = Targets();
        Check(targets.GoalZones.Count == 0 && targets.PotentialTargets.Single().Priority == AIHints.Enemy.PriorityInvincible,
            "opening the source wall does not let the AI approach or interrupt an add behind another closed wall");

        demons.OnActorEAnim(eastWall, 0x00010040);
        targets = Targets();
        Check(targets.GoalZones.Count == 1 && targets.PotentialTargets.Single() is { Priority: 4, ShouldBeInterrupted: true },
            "once both sides are open, the other targetable add can be approached and interrupted before it gains local enmity");
        var fallbackRoute = NavigationDecision.Build(prisonContext, Now, targets, player, forbiddenZoneCushion: 0.5f);
        prisonEndpoint = prisonContext.ThetaStar.CellCenter(prisonContext.ThetaStar.BestIndex());
        Check(fallbackRoute.Destination != null && module.Arena.InBounds(prisonEndpoint) && prisonEndpoint.InCircle(east.Position, 6f),
            "the native planner reaches the open fallback prison after the source wall releases");
        world.Party.SetAllianceGroups(-1, -1, -1);
        Check(Targets().GoalZones.Count == 1, "with no alliance labels the AI can approach the nearest live prison");

        ultimate.OnActorModelStateChange(boss, 20, 0, 0);
        ultimate.OnActorModelStateChange(boss, 0, 0, 0);
        Check(Damage().PredictedDamage.Count == 0, "a ground-level animation reset cannot forecast the ultimate");
        ultimate.OnActorModelStateChange(boss, 20, 0, 0);
        world.Frame = new(Now.AddSeconds(16.26), 0, 0, 0, 0, 2);
        boss.PosRot.Y = -663.5f;
        ultimate.OnActorModelStateChange(boss, 0, 0, 0);
        var damage = Damage().PredictedDamage;
        Check(damage.Count == 1 && damage[0].Players[0] && damage[0].Activation == world.FutureTime(4.7),
            "the observed launch and elevated apex give one early raidwide prediction");
        Check(Damage().GoalZones.Count == 0, "the apex alone cannot establish the direction of the charge");

        world.Frame = new(Now.AddSeconds(17.65), 0, 0, 0, 0, 3);
        var cast = Cast(9866, new(63.85901f, -149.76727f));
        cast.Rotation = 2.3454444f.Radians();
        cast.TotalTime = 3.2f;
        boss.CastInfo = cast;
        ultimate.OnCastStarted(boss, cast);
        var sideHints = Damage();
        damage = sideHints.PredictedDamage;
        Check(damage.Count == 1 && damage[0].Activation == module.CastFinishAt(cast),
            "the actual visual cast replaces the provisional deadline");
        var direction = cast.Rotation.ToDirection();
        var lateral = direction.OrthoL();
        var axis = cast.LocXZ + direction * (center - cast.LocXZ).Dot(direction);
        Check(sideHints.GoalZones.Single()(axis) < sideHints.GoalZones[0](axis + 10f * lateral)
            && MathF.Abs(sideHints.GoalZones[0](axis + 10f * lateral) - sideHints.GoalZones[0](axis - 10f * lateral)) < 0.01f
            && sideHints.ForbiddenZones.Count == 0, "the recorded cast gives a bilateral preference without assuming a safe damage boundary");
        player.PosRot = new(axis.X, -694, axis.Z, 0);
        sideHints.PathfindMapBounds = module.Bounds;
        sideHints.PathfindMapCenter = module.Center;
        var blockedSide = new SDCircle(axis + 14f * lateral, 11f);
        sideHints.AddForbiddenZone(blockedSide, world.CurrentTime);
        var sideContext = new NavigationDecision.Context();
        var sideRoute = NavigationDecision.Build(sideContext, world.CurrentTime, sideHints, player, forbiddenZoneCushion: 0.5f);
        var sideEndpoint = sideContext.ThetaStar.CellCenter(sideContext.ThetaStar.BestIndex());
        Check(sideRoute.Destination != null && module.Arena.InBounds(sideEndpoint) && blockedSide.Distance(sideEndpoint) > 0.5f
            && (sideEndpoint - axis).Dot(lateral) < -8f, "native navigation chooses the other side when a concurrent hazard blocks the first");
        world.Frame = new(Now.AddSeconds(21.13), 0, 0, 0, 0, 4);
        ultimate.OnEventCast(boss, Effect(9866));
        ultimate.OnCastFinished(boss, cast);
        damage = Damage().PredictedDamage;
        Check(damage.Count == 1 && damage[0].Activation == world.FutureTime(0.07d),
            "visual resolution preserves the short window to the helper impact");
        ultimate.OnEventCast(Actor(5, 0x18D6, center), Effect(9867));
        Check(Damage().PredictedDamage.Count == 0 && ultimate.NumCasts == 1, "the helper impact retires the warning");
        ultimate.OnCastStarted(boss, cast);
        ultimate.OnCastFinished(boss, cast);
        Check(Damage().PredictedDamage.Count == 0 && Damage().GoalZones.Count == 0,
            "an interrupted visual cast clears its side preference without creating a helper prediction");
        ultimate.OnCastStarted(boss, cast);
        ultimate.OnEventCast(boss, Effect(9866));
        Check(Damage().PredictedDamage.Count == 1, "the visual forecasts a hit before the alternate resolution");
        ultimate.OnEventCast(Actor(5, 0x18D6, center), Effect(9876));
        Check(Damage().PredictedDamage.Count == 0 && ultimate.NumCasts == 2,
            "the alternate damage action retires the pending prediction and counts the resolved mechanic");

        Actor AddDemon(ulong id, float x, float z)
        {
            world.Execute(new ActorState.OpCreate(id, 0x1FC3, 0, 0, "", 0, ActorType.Enemy, Class.None, 70,
                new(x, -694, z, 0), 1.95f, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }

        AIHints Targets(Actor? additional = null)
        {
            var hints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = center };
            if (!west.IsDead)
                hints.PotentialTargets.Add(new(west, AIHints.Enemy.PriorityUndesirable, false));
            hints.PotentialTargets.Add(new(east, AIHints.Enemy.PriorityUndesirable, false));
            if (additional != null)
                hints.PotentialTargets.Add(new(additional, AIHints.Enemy.PriorityUndesirable, false));
            demons.AddAIHints(0, player, default, hints);
            return hints;
        }

        AIHints Damage()
        {
            var hints = new AIHints();
            ultimate.AddAIHints(0, player, default, hints);
            return hints;
        }
    }

    private static void RabanastreChariot()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var center = new WPos(106, -191);
        var origin = new WPos(112.19946f, -197.77222f);
        var start = new WPos(88.64608f, -213.96637f);
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(1, 0x1FAF, origin);
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
        var chariot = Component<GenericBaitAway>(module, prefix + "Chariot");
        var marked = AddPlayer(2, 0, start);
        var toward = (start - origin).Normalized();
        var peer = AddPlayer(3, 1, origin + 17f * toward);
        var cast = Cast(9844, start);
        cast.TargetID = marked.InstanceID;
        cast.TotalTime = 2.7f;
        cast.ElapsedTime = 0.0226162f;
        chariot.OnEventIcon(marked, 381, marked.InstanceID);
        Check(chariot.CurrentBaits.Count == 0, "an unrelated mark does not forecast Chariot");
        chariot.OnEventIcon(marked, 23, marked.InstanceID);
        chariot.OnEventIcon(marked, 23, marked.InstanceID);
        Check(chariot.CurrentBaits.Single().Activation == Now.AddSeconds(7.8), "the Chariot icon forecasts one lane with a conservative deadline");

        var targetHints = new AIHints { PathfindMapCenter = center, PathfindMapBounds = module.Bounds };
        chariot.AddAIHints(0, marked, default, targetHints);
        var goal = targetHints.GoalZones.Single();
        Check(goal(origin + 22f * toward) > goal(start) && goal(origin + 13f * toward) > goal(origin + 22f * toward)
            && targetHints.ForbiddenZones.Count == 0 && targetHints.PredictedDamage.Single().Players[0],
            "the marked player gets credit for reachable shortening beyond twelve yalms and can end the charge before an ally");
        var hazard = new SDCircle(origin + 20f * toward, 2f);
        targetHints.AddForbiddenZone(hazard, Now);
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, targetHints, marked, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination is { } step && module.Arena.InBounds(step) && (step - origin).Length() < (start - origin).Length()
            && hazard.Distance(endpoint) > 0.5f,
            "the native route takes an inward step while preserving the concurrent hazard");

        var allyHints = new AIHints();
        chariot.AddAIHints(1, peer, default, allyHints);
        Check(allyHints.ForbiddenZones.Single().activation == Now.AddSeconds(7.8)
            && allyHints.ForbiddenZones[0].shapeDistance.Contains(peer.Position),
            "an unmarked ally gets the predicted lane before the cast");
        world.Frame = new(Now.AddSeconds(5.25), 0, 0, 0, 0, 2);
        chariot.OnCastStarted(boss, cast);
        allyHints.Clear();
        chariot.AddAIHints(1, peer, default, allyHints);
        Check(allyHints.ForbiddenZones.Single().activation == module.CastFinishAt(cast)
            && allyHints.ForbiddenZones[0].shapeDistance.Contains(peer.Position),
            "the cast replaces the forecast with one confirmed charge lane");
        marked.PosRot = (origin + 12f * toward).ToVec4();
        chariot.Update();
        allyHints.Clear();
        chariot.AddAIHints(1, peer, default, allyHints);
        Check(!allyHints.ForbiddenZones.Single().shapeDistance.Contains(peer.Position), "shortening the live charge releases the ally beyond the endpoint");
        chariot.OnEventCast(boss, Effect(9844));
        chariot.OnCastFinished(boss, cast);
        Check(chariot.CurrentBaits.Count == 0, "impact and a duplicate finish leave no Chariot guidance");
        chariot.OnCastStarted(boss, cast);
        chariot.OnActorDeath(marked);
        Check(chariot.CurrentBaits.Count == 0, "losing the charge target clears its bait");
        chariot.OnEventIcon(marked, 23, marked.InstanceID);
        world.Frame = new(Now.AddSeconds(12), 0, 0, 0, 0, 3);
        chariot.Update();
        Check(chariot.CurrentBaits.Count == 0, "a Chariot mark without a cast expires");

        Actor AddPlayer(ulong id, int slot, WPos position)
        {
            module.WorldState.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
                position.ToVec4(), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            module.WorldState.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return module.WorldState.Actors.Find(id)!;
        }
    }

    private static void RabanastreCrushHelm()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1FAF, new(106, -191));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
        var mechanic = Component<BossComponent>(module, prefix + "CrushHelm");
        var firstTank = AddTank(1, 0);
        var actualTank = AddTank(2, 1);
        boss.TargetID = firstTank.InstanceID;
        var cast = Cast(9856, boss.Position);
        cast.TargetID = boss.InstanceID;
        cast.TotalTime = 2.7f;
        mechanic.OnEventIcon(actualTank, 381, actualTank.InstanceID);
        SetTime(0.09);
        mechanic.OnCastStarted(boss, cast);
        Check(Hints().PredictedDamage is { Count: 5 } marked && marked.All(hit => hit.Players[1] && !hit.Players[0]),
            "the near-simultaneous Crush Helm mark overrides the boss's current target");
        mechanic.OnCastFinished(boss, cast);
        mechanic.OnEventIcon(actualTank, 381, actualTank.InstanceID);
        SetTime(0.4);
        mechanic.OnCastStarted(boss, cast);
        var hints = Hints();
        Check(hints.PredictedDamage.Count == 5 && hints.PredictedDamage.All(hit => hit.Players[0] && !hit.Players[1])
            && hints.PredictedDamage[^1].Type == AIHints.PredictedDamageType.Tankbuster
            && hints.PredictedDamage.Take(4).All(hit => hit.Type == AIHints.PredictedDamageType.None),
            "a stale mark falls back to the current target with separate predictions and mitigation for the finisher");
        mechanic.OnEventIcon(actualTank, 381, actualTank.InstanceID);
        Check(Hints().PredictedDamage.All(hit => hit.Players[1] && !hit.Players[0]),
            "a mark delivered after cast start corrects the pending mitigation recipient");
        mechanic.OnCastFinished(boss, cast);
        Check(Hints().PredictedDamage.Count == 0, "interrupting the visual clears the entire sequence");
        mechanic.OnCastStarted(boss, cast);
        SetTime(2.986);
        mechanic.OnEventCast(boss, Effect(9856));
        mechanic.OnCastFinished(boss, cast);
        hints = Hints();
        Check(hints.PredictedDamage.Count == 5 && hints.PredictedDamage[0].Activation == world.FutureTime(0.8),
            "the visual action rebases the five forecast times");

        var firstHelper = Actor(78, 0x18D6, boss.Position);
        var firstHit = new ActorCastEvent(new(ActionType.Spell, 9857), boss.InstanceID, 0, 1, default, 1, 0, default);
        firstHit.Targets.Add(new(actualTank.InstanceID, default));
        SetTime(3.900);
        mechanic.OnEventCast(firstHelper, firstHit);
        mechanic.OnEventCast(firstHelper, firstHit);
        hints = Hints();
        Check(hints.PredictedDamage.Count == 4 && hints.PredictedDamage.All(hit => !hit.Players[0] && hit.Players[1]),
            "the first observed helper retires one hit and corrects the recipient without counting duplicates");
        foreach (var helper in new ulong[] { 77, 79, 75 })
            mechanic.OnEventCast(Actor(helper, 0x18D6, boss.Position), firstHit);
        Check(Hints().PredictedDamage.Count == 1, "the four distinct helpers leave only the finisher");

        Check(Utils.StatusIsRemovable(200), "the game sheet confirms Physical Vulnerability Up is removable");
        actualTank.Statuses[0] = new(200, 4, Now.AddSeconds(8), firstHelper.InstanceID);
        firstTank.Statuses[0] = new(200, 3, Now.AddSeconds(8), firstHelper.InstanceID);
        Check(Hints().ShouldCleanse[1] && !Hints().ShouldCleanse[0], "only a party member with four stacks gets the Crush Helm cleanse hint");
        mechanic.OnEventCast(boss, Effect(9858));
        Check(Hints().PredictedDamage.Count == 0 && Hints().ShouldCleanse[1], "the finisher clears predictions while the removable debuff remains active");
        SetTime(8.1);
        Check(!Hints().ShouldCleanse[1], "expired vulnerability does not request a cleanse");
        mechanic.OnCastStarted(boss, cast);
        mechanic.OnActorDestroyed(boss);
        Check(Hints().PredictedDamage.Count == 0, "boss removal clears the next pull's pending sequence");

        void SetTime(double seconds) => world.Frame = new(Now.AddSeconds(seconds), 0, 0, 0, 0, 1);
        AIHints Hints()
        {
            var result = new AIHints();
            mechanic.AddAIHints(0, firstTank, default, result);
            return result;
        }
        Actor AddTank(ulong id, int slot)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.WAR, 70,
                new(106, 0, -191, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Actors.Find(id)!;
        }
    }

    private static void RabanastreDarkCircles()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var module = Module(prefix + "A13Rofocale", new(106, -191));
        module.Arena.Bounds = new ArenaBoundsCircle(29.5f);
        var mechanic = Component<BossComponent>(module, prefix + "DarkGeas");
        var player = Actor(1, 0, module.Center, player: true);
        var boss = Actor(8, 0x1FAF, new(104.6311f, -202.34991f));
        module.PrimaryActor = boss;
        var cast = Cast(9864, boss.Position);
        cast.TotalTime = 2.7f;
        cast.ElapsedTime = 0.0212308f;
        player.PosRot = new(99.26416f, 0, -204.77289f, 0);
        mechanic.OnCastStarted(boss, cast);
        var preparation = new AIHints() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        mechanic.AddAIHints(0, player, default, preparation);
        var plannedCircle = new WPos(103.2217f, -213.1518f);
        var prepContext = new NavigationDecision.Context();
        var prepRoute = NavigationDecision.Build(prepContext, Now, preparation, player, forbiddenZoneCushion: 0.5f);
        var prepEndpoint = prepContext.ThetaStar.CellCenter(prepContext.ThetaStar.BestIndex());
        Check(preparation.InteractWithTarget == null && preparation.ForbiddenZones.Count == 0
            && prepRoute.Destination != null && prepEndpoint.InCircle(plannedCircle, 2f)
            && prepRoute.TimeToGoal < cast.NPCRemainingTime,
            "Dark Geas starts moving toward a confirmed layout position during the observed cast without interacting");
        mechanic.OnCastFinished(boss, cast);
        preparation = new();
        mechanic.AddAIHints(0, player, default, preparation);
        Check(preparation.GoalZones.Count == 0, "an interrupted cast clears layout preparation without an effect");
        mechanic.OnCastStarted(boss, cast);
        mechanic.OnEventCast(boss, Effect(9864));
        mechanic.OnCastFinished(boss, cast);
        module.WorldState.Frame = new(Now.AddSeconds(0.44d), 0, 0, 0, 0, 2);
        mechanic.AddAIHints(0, player, default, preparation);
        Check(preparation.GoalZones.Single()(plannedCircle) == 8f && preparation.InteractWithTarget == null,
            "a resolved cast retains the layout goal through the recorded delay without interacting");
        module.WorldState.Frame = new(Now.AddSeconds(0.75d), 0, 0, 0, 0, 3);
        preparation = new();
        mechanic.AddAIHints(0, player, default, preparation);
        Check(preparation.GoalZones.Count == 0, "the staging goal expires if no circles become targetable");
        module.WorldState.Frame = new(Now, 0, 0, 0, 0, 1);
        mechanic.OnCastStarted(boss, cast);
        mechanic.OnEventCast(boss, Effect(9864));
        mechanic.OnActorDestroyed(boss);
        mechanic.AddAIHints(0, player, default, preparation);
        Check(preparation.GoalZones.Count == 0, "boss removal clears the post-effect staging window on reset");

        player.PosRot = new(module.Center.X, 0, module.Center.Z, 0);
        void AddObject(ulong id, uint oid, float x, bool targetable)
            => module.WorldState.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.EventObj, Class.None, 0, new(x, 0, -191, 0), 0.5f, default, targetable, false, 0, 0, 0));
        AddObject(2, 0x1EA873, 109, false);
        mechanic.OnCastStarted(boss, cast);
        mechanic.OnEventCast(boss, Effect(9864));
        mechanic.OnCastFinished(boss, cast);
        AddObject(3, 0x1EA87D, 111, true);
        mechanic.OnActorCreated(module.WorldState.Actors.Find(3)!);
        module.WorldState.Actors.Find(3)!.IsTargetable = false;
        preparation = new();
        mechanic.AddAIHints(0, player, default, preparation);
        Check(preparation.GoalZones.Count == 0, "a live circle ends staging permanently for that cast");
        module.WorldState.Actors.Find(3)!.IsTargetable = true;
        AddObject(4, 0x1EA7F4, 96, true);
        AddObject(5, 0x1EA872, 107, true);
        AddObject(6, 0x1EA87A, 136.5f, true);
        mechanic.OnCastStarted(boss, cast);
        var hints = new AIHints();
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Count == 1
            && hints.GoalZones[0](new(111, -191)) == 100f && hints.GoalZones[0](new(96, -191)) == 100f
            && hints.GoalZones[0](new(109, -191)) == 0f && hints.GoalZones[0](new(107, -191)) == 0f
            && hints.GoalZones[0](new(136.5f, -191)) == 0f,
            "actual eligible circles replace cast preparation without committing to a remote interaction");
        mechanic.OnCastFinished(boss, cast);
        var global = new BossComponent.GlobalHints();
        mechanic.AddGlobalHints(player, global);
        Check(global.Count == 1, "live circles keep the interaction instruction visible after the cast");

        hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        hints.AddForbiddenZone(new SDCircle(new(111, -191), 2f), Now);
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Single()(new(111, -191)) == 0f
            && hints.GoalZones[0](new(96, -191)) == 100f && hints.ForbiddenZones.Count == 1,
            "an unsafe circle yields to another one while retaining the hazard");

        hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        hints.AddForbiddenZone(new SDRect(new(108.5f, -191), 0f.Degrees(), 32f, 32f, 0.8f), Now);
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Single()(new(111, -191)) == 100f,
            "a safe endpoint stays a candidate even though an active lane blocks the route to it");
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination is { } waypoint && module.Arena.InBounds(waypoint)
            && endpoint.InCircle(new(96, -191), 1f)
            && hints.ForbiddenZones[0].shapeDistance.Distance(waypoint) > 0.5f,
            "native navigation selects the farther reachable circle when a lane cuts off the nearest one");
        player.PosRot = new(endpoint.X, 0, endpoint.Z, 0);
        hints = new();
        hints.AddForbiddenZone(new SDRect(new(108.5f, -191), 0f.Degrees(), 32f, 32f, 0.8f), Now);
        mechanic.AddAIHints(0, player, default, hints);
        var interaction = typeof(AIHints).GetField("PriorityInteraction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Check(hints.InteractWithTarget?.InstanceID == 4 && interaction.GetValue(hints) == hints.InteractWithTarget,
            "arriving at the native route endpoint gives the reachable circle the next free interaction window");
        var sprint = ActionDefinitions.IDSprint;
        var cooldowns = module.WorldState.Client.Cooldowns;
        hints.ActionsToExecute.Push(sprint, player, ActionQueue.Priority.VeryHigh);
        Check(hints.ActionsToExecute.FindBest(module.WorldState, player, cooldowns, 0, hints, 0, false).Action == sprint
            && !hints.ActionsToExecute.FindBest(module.WorldState, player, cooldowns, 0, hints, 0, false, manualOnly: true).Action,
            "a pending required interaction reserves the next free window from automatic actions");
        hints.ActionsToExecute.Push(sprint, player, ActionQueue.Priority.ManualOGCD, manual: true);
        Check(hints.ActionsToExecute.FindBest(module.WorldState, player, cooldowns, 0, hints, 0, false, manualOnly: true).Manual,
            "a player-issued action still wins during a priority interaction");
        hints.Clear();
        Check(hints.InteractWithTarget == null && interaction.GetValue(hints) == null,
            "clearing frame hints drops the interaction target and priority together");

        player.PosRot = new(110.5f, 0, -191, 0);
        hints = new();
        hints.AddForbiddenZone(new SDCircle(player.Position, 2f), Now.AddSeconds(0.5));
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget == null, "an imminent hazard leaves the native movement controller free to dodge");
        hints = new();
        hints.AddForbiddenZone(new SDCircle(player.Position, 2f), DateTime.MaxValue);
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Single()(new(111, -191)) == 0f
            && hints.GoalZones[0](new(96, -191)) == 100f,
            "an untimed hazard blocks both interaction and approach while keeping other circles available");
        hints = new();
        hints.AddForbiddenZone(new SDCircle(new(111, -191), 2f), Now.AddSeconds(10));
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget?.InstanceID == 3, "a sufficiently late hazard permits the nearby interaction");

        boss.IsDead = true;
        hints = new();
        global.Clear();
        mechanic.AddAIHints(0, player, default, hints);
        mechanic.AddGlobalHints(player, global);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Count == 0 && global.Count == 0,
            "a targetable circle lingering after boss death cannot publish movement or interaction");
        boss.IsDead = false;

        module.WorldState.Actors.Find(3)!.IsTargetable = false;
        hints = new();
        mechanic.AddAIHints(0, player, default, hints);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Single()(new(96, -191)) == 100f,
            "Dark Geas advances to the next available circle after a successful interaction or disappearance");
        module.WorldState.Actors.Find(4)!.IsTargetable = false;
        hints = new();
        global.Clear();
        mechanic.AddAIHints(0, player, default, hints);
        mechanic.AddGlobalHints(player, global);
        Check(hints.InteractWithTarget == null && hints.GoalZones.Count == 0 && global.Count == 0,
            "unavailable and out-of-bounds objects leave no interaction, approach or warning");
    }

    private static void RabanastreEmbraceBind()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1FAF, new(106, -191));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A13Rofocale", true)!, world, boss)!;
        var embrace = Component<GenericAOEs>(module, prefix + "Embrace");
        var healer = AddMember(1, 0, Class.WHM);
        var bound = AddMember(2, 1, Class.WAR);
        var otherAlliance = AddMember(3, 8, Class.DRG);
        bound.Statuses[0] = otherAlliance.Statuses[0] = new(564, 0, Now.AddSeconds(8), 10);
        Check(Utils.StatusIsRemovable(564), "the game sheet permits Esuna for the recorded Embrace bind");
        var hints = new AIHints();
        embrace.AddAIHints(0, healer, default, hints);
        Check(hints.ShouldCleanse[1] && !hints.ShouldCleanse[0] && !hints.ShouldCleanse[8],
            "Embrace asks native healer AI to cleanse a bound party member");
        world.Frame = new(Now.AddSeconds(8.1), 0, 0, 0, 0, 2);
        hints = new();
        embrace.AddAIHints(0, healer, default, hints);
        Check(hints.ShouldCleanse == default, "an expired bind does not keep the cleanse request active");

        Actor AddMember(ulong id, int slot, Class job)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, job, 70,
                new(106, 0, -191, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Actors.Find(id)!;
        }
    }

    private static void RabanastreMaverickPreviews()
    {
        const string prefix = "Stormblood.Alliance.A13Rofocale.";
        var module = Module(prefix + "A13Rofocale", new(106, -191));
        module.Arena.Bounds = new ArenaBoundsCircle(29.5f);
        var maverick = Component<GenericAOEs>(module, prefix + "Maverick");
        var clone = Actor(1, 0x2026, new(135.55f, -190.9631f));
        clone.PosRot.W = -1.5704849f;
        var boss = Actor(2, 0x1FAF, module.Center);
        module.PrimaryActor = boss;
        boss.IsTargetable = false;
        maverick.OnActorPlayActionTimelineEvent(boss, 7747);
        Check(maverick.ActiveAOEs(0, boss).IsEmpty, "a timeline on a boss still inside the arena cannot invent a Maverick lane");
        maverick.OnActorPlayActionTimelineEvent(clone, 7747);
        maverick.OnActorPlayActionTimelineEvent(clone, 7747);
        var aoes = maverick.ActiveAOEs(0, boss);
        Check(aoes.Length == 1 && aoes[0].Color == Colors.AOE && aoes[0].Activation == Now.AddSeconds(6.5),
            "a confirmed edge pose provides one shaded preview at the early deadline");
        var hints = new AIHints();
        maverick.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(module.Center),
            "the early lane already constrains AI movement");
        boss.PosRot = new(91.203f, 0, -165.331f, 2.6278253f);
        Check(maverick.ActiveAOEs(0, boss).Length == 2, "the boss preview starts after a matching edge move");
        var cast = Cast(10022, new(135.546f, -190.967f));
        cast.Rotation = clone.Rotation;
        maverick.OnCastStarted(clone, cast);
        aoes = maverick.ActiveAOEs(0, boss);
        Check(aoes.Length == 2 && aoes[0].ActorID == clone.InstanceID && aoes[0].Color == Colors.Danger
            && aoes[0].Activation == module.CastFinishAt(cast), "an alternate cast reconciles the clone's preview and leaves the other lane intact");
        maverick.OnActorTargetable(boss);
        Check(maverick.ActiveAOEs(0, boss).Length == 1, "a returning boss retires its preview without clearing a live clone cast");
        maverick.OnActorDestroyed(clone);
        Check(maverick.ActiveAOEs(0, boss).IsEmpty, "the caster despawn clears its confirmed lane");

        maverick.OnActorPlayActionTimelineEvent(clone, 7747);
        var quickCast = Cast(10036, clone.Position);
        quickCast.Rotation = clone.Rotation;
        quickCast.TotalTime = 0.7f;
        maverick.OnCastStarted(clone, quickCast);
        aoes = maverick.ActiveAOEs(0, boss);
        hints = new();
        maverick.AddAIHints(0, boss, default, hints);
        Check(aoes.Length == 1 && aoes[0].Color == Colors.Danger && aoes[0].Activation == module.CastFinishAt(quickCast)
            && hints.ForbiddenZones.Single().activation == module.CastFinishAt(quickCast)
            && hints.ForbiddenZones[0].shapeDistance.Contains(new(110f, -184f))
            && !hints.ForbiddenZones[0].shapeDistance.Contains(new(110f, -182f)),
            "the short Maverick variant reconciles its preview with the actual cast deadline and sixteen-yalm lane");
        maverick.OnEventCast(clone, Effect(10036));
        Check(maverick.ActiveAOEs(0, boss).IsEmpty, "the short variant's effect clears its warning immediately");

        // The five poses are from the early recorded wave. Subsets exercise alternate
        // counts and the Circle overlap is synthetic, without asserting a later layout.
        (WPos Origin, float Facing)[] lanes =
        [
            (new(106.004395f, -161.45569f), -3.1413052f),
            (new(91.203125f, -165.33148f), 2.6185925f),
            (new(131.57849f, -176.22644f), -2.0944431f),
            (new(120.74463f, -165.48407f), -2.6174426f),
            (new(135.5459f, -190.96667f), -1.5709642f),
        ];
        var player = Actor(3, 0, new(111.702736f, -193.48715f), player: true);
        for (var count = 3; count <= 5; ++count)
        {
            var casters = AddWave(count, 0.03094f);
            var wave = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            maverick.AddAIHints(0, player, default, wave);
            Check(wave.ForbiddenZones.Count == count && maverick.ActiveAOEs(0, player).ToArray().All(aoe => aoe.Color == Colors.Danger),
                "all simultaneous lanes remain bright and constrain native movement regardless of lane count");
            wave.Normalize();
            CheckRabanastreRoute(module, player, wave, $"{count}-lane Maverick");
            maverick.OnEventCast(casters[0], Effect(9865));
            Check(maverick.ActiveAOEs(0, player).Length == count - 1, "a resolved lane retires without clearing the others");
            maverick.OnActorDeath(boss);
            Check(maverick.ActiveAOEs(0, player).IsEmpty, "the boss's death clears the entire Maverick wave");
        }

        var dark = Component<CastHint>(module, prefix + "DarkGeas");
        var unsafeCircle = new WPos(130.9999f, -195.3280f);
        var safeCircle = new WPos(124.7983f, -204.2808f);
        AddCircle(20, 0x1EA873, unsafeCircle);
        AddCircle(21, 0x1EA874, safeCircle);
        AddWave(5, 4.05f);
        player.PosRot = new(unsafeCircle.X, -694, unsafeCircle.Z, 0);
        var overlap = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        maverick.AddAIHints(0, player, default, overlap);
        dark.AddAIHints(0, player, default, overlap);
        Check(overlap.InteractWithTarget == null && overlap.GoalZones.Single()(unsafeCircle) == 0f
            && overlap.GoalZones[0](safeCircle) == 100f && overlap.ForbiddenZones.Count == 5,
            "Dark Geas defers a nearby interaction in the imminent wave and leaves the safe circle available");
        maverick.OnActorDeath(boss);

        Actor[] AddWave(int count, float elapsed)
        {
            var casters = new Actor[count];
            for (var i = 0; i < casters.Length; ++i)
            {
                var lane = lanes[i];
                var caster = casters[i] = Actor((ulong)(10 + i), 0x2026, lane.Origin);
                var charge = Cast(9865, lane.Origin);
                charge.Rotation = lane.Facing.Radians();
                charge.TotalTime = 5.7f;
                charge.ElapsedTime = elapsed;
                maverick.OnCastStarted(caster, charge);
            }
            return casters;
        }

        void AddCircle(ulong id, uint oid, WPos pos)
            => module.WorldState.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.EventObj, Class.None, 0,
                new(pos.X, -694, pos.Z, 0), 0.5f, default, true, false, 0, 0, 0));
    }

    private static void RabanastreSwordRays()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var module = Module(prefix + "A14Argath", new(106, -386));
        var player = Actor(1, 0, new(125, -385), player: true);
        var bait = Component<BossComponent>(module, prefix + "JudgmentBlade");
        var heartless = Component<GenericAOEs>(module, prefix + "Heartless");
        bait.OnEventIcon(player, 125, 0);
        var hints = new AIHints();
        bait.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 1 && hints.GoalZones[0](module.Center) > hints.GoalZones[0](player.Position), "sword bait guidance starts at the player's icon");
        var sword = Actor(2, 0x1F98, module.Center);
        bait.OnActorCreated(sword);
        heartless.OnActorCreated(sword);
        var aoes = heartless.ActiveAOEs(0, player);
        Check(aoes.Length == 1 && aoes[0].Activation == Now.AddSeconds(6.5d) && aoes[0].Origin.AlmostEqual(module.Center - new WDir(0, 5.15f), 0.01f), "sword helper spawn predicts the backed-up ray before its visible cast");
        hints = new();
        bait.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 0, "the bait ends once the sword is placed");
        var cast = Cast(9778, module.Center - new WDir(0, 5.1f));
        heartless.OnCastStarted(sword, cast);
        aoes = heartless.ActiveAOEs(0, player);
        Check(aoes.Length == 1 && aoes[0].Origin == cast.LocXZ && aoes[0].Activation == module.CastFinishAt(cast), "the ray reconciles with the actual cast and retains one constraint");
    }

    private static void RabanastreBoulderAndOrbit()
    {
        const string hashmal = "Stormblood.Alliance.A12Hashmal.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var hashmalBoss = Actor(100, 0x1F83, new(-320, -46));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + hashmal + "A12Hashmal", true)!, world, hashmalBoss)!;
        var boulder = Component<GenericStackSpread>(module, hashmal + "FallingBoulder");
        var dust = Component<RaidwideCast>(module, hashmal + "ToDust");
        var caster = Actor(1, 0x18D6, module.Center);
        world.Execute(new ActorState.OpCreate(2, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70, new(-320, 10, -46, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(9, new(0, 2, false)));
        var cast = Cast(9682, module.Center);
        cast.TargetID = 2;
        boulder.OnCastStarted(caster, cast);
        Check(boulder.Stacks.Count == 1 && boulder.Stacks[0].ForbiddenPlayers[0] && !boulder.Stacks[0].ForbiddenPlayers[8] && boulder.Stacks[0].ForbiddenPlayers[16],
            "Falling Boulder groups by party membership without requiring display labels");
        boulder.OnCastFinished(caster, cast);
        Check(boulder.Stacks.Count == 0, "Falling Boulder clears a cancelled cast");

        var carrierB = world.Actors.Find(2)!;
        var carrierA = AddStackPlayer(20, 0);
        var followerA = AddStackPlayer(21, 1);
        var followerB = AddStackPlayer(22, 10);
        var carrierC = AddStackPlayer(24, 16);
        var followerC = AddStackPlayer(25, 17);
        cast.TotalTime = 4.7f;
        var castA = Cast(9682, module.Center);
        castA.TargetID = carrierA.InstanceID;
        castA.TotalTime = 4.7f;
        castA.ElapsedTime = 0.2f;
        var otherCaster = Actor(23, 0x18D6, module.Center);
        var castC = Cast(9682, module.Center);
        castC.TargetID = carrierC.InstanceID;
        castC.TotalTime = 4.7f;
        var thirdCaster = Actor(26, 0x18D6, module.Center);
        boulder.OnCastStarted(caster, cast);
        boulder.OnCastStarted(caster, cast);
        boulder.OnCastStarted(otherCaster, castA);
        boulder.OnCastStarted(thirdCaster, castC);
        Check(boulder.Stacks.Count == 3, "one marker per alliance survives duplicate cast notifications");
        world.Party.SetAllianceGroups(0, 1, 2);
        var westSphere = Actor(30, 0x1F84, new(-334.5f, -57f));
        var southSphere = Actor(31, 0x1F84, new(-320f, -26f));
        var eastSphere = Actor(32, 0x1F84, new(-306f, -57.5f));
        Actor[] spheres = [westSphere, southSphere, eastSphere];
        var dustCast = Cast(9673, southSphere.Position);
        dustCast.TotalTime = 19.7f;
        southSphere.CastInfo = dustCast;
        dust.OnCastStarted(southSphere, dustCast);
        foreach (var (slot, member, sphere) in new[] { (0, carrierA, westSphere), (1, followerA, westSphere),
            (9, carrierB, southSphere), (10, followerB, southSphere), (16, carrierC, eastSphere), (17, followerC, eastSphere) })
        {
            var stackHints = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            stackHints.AddForbiddenZone(new SDCircle(new(-320, -26), 4f), Now);
            foreach (var target in spheres)
                stackHints.PotentialTargets.Add(new(target, AIHints.Enemy.PriorityUndesirable, false));
            module.CalculateAIHints(slot, member, default, stackHints);
            var shared = stackHints.PredictedDamage.Where(hit => hit.Type == AIHints.PredictedDamageType.Shared).ToArray();
            Check(stackHints.ForbiddenZones.Count == 3 && stackHints.PredictedDamage.Count == 4 && stackHints.GoalZones.Count == 2
                && shared.Single(hit => hit.Players[0]).Activation == module.CastFinishAt(castA)
                && shared.Single(hit => hit.Players[9]).Activation == module.CastFinishAt(cast)
                && shared.Single(hit => hit.Players[16]).Activation == module.CastFinishAt(castC)
                && shared.Count(hit => hit.Players[slot]) == 1,
                "each stack preserves its own party and mitigation deadline alongside To Dust and foreign avoidance");
            Check(stackHints.PotentialTargets.Single(enemy => enemy.Priority == 4).Actor == sphere,
                "the simultaneous movement mechanic preserves the party's Sand Sphere assignment");
            stackHints.Normalize();
            var context = new NavigationDecision.Context();
            var route = NavigationDecision.Build(context, Now, stackHints, member, forbiddenZoneCushion: 0.5f);
            var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
            var distanceToSphere = (endpoint - sphere.Position).LengthSq();
            Check(route.Destination != null && module.Arena.InBounds(endpoint)
                && distanceToSphere < (module.Center - sphere.Position).LengthSq()
                && spheres.All(target => target == sphere || distanceToSphere < (endpoint - target.Position).LengthSq())
                && stackHints.ForbiddenZones.All(zone => zone.shapeDistance.Distance(endpoint) > 0.5f)
                && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < 5f,
                $"Falling Boulder slot {slot} has a timely, hazard-aware route into its party's sector");
        }
        carrierA.PosRot = new(-334.5f, 10, -57f, 0);
        followerA.PosRot = carrierA.PosRot;
        carrierB.PosRot = new(-320f, 10, -26f, 0);
        followerB.PosRot = carrierB.PosRot;
        carrierC.PosRot = new(-306f, 10, -57.5f, 0);
        followerC.PosRot = carrierC.PosRot;
        var settled = new AIHints();
        boulder.AddAIHints(10, followerB, default, settled);
        Check(settled.ForbiddenZones.Count == 3 && settled.ForbiddenZones.Any(zone => zone.shapeDistance.Contains(module.Center))
            && settled.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(carrierB.Position)),
            "once separated, the follower resumes gathering with its own carrier");
        boulder.OnActorDeath(carrierA);
        Check(boulder.Stacks.Count == 2 && boulder.Stacks.All(stack => stack.Target != carrierA), "losing one carrier leaves the other stacks active");
        boulder.OnCastFinished(thirdCaster, castC);
        Check(boulder.Stacks.Single().Target == carrierB, "the northeast stack can finish independently of the southern stack");
        boulder.OnCastFinished(caster, cast);
        var after = new AIHints();
        module.CalculateAIHints(10, followerB, default, after);
        Check(after.ForbiddenZones.Count == 0 && after.PredictedDamage.Count == 1
            && after.PredictedDamage[0].Activation == module.CastFinishAt(dustCast),
            "when the stacks clear, the concurrent To Dust deadline stays active");

        Actor AddStackPlayer(ulong id, int slot)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
                new(-320, 10, -46, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Actors.Find(id)!;
        }

        const string rofocale = "Stormblood.Alliance.A13Rofocale.";
        var center = new WPos(106, -191);
        var boss = Actor(3, 0x1FAF, center);
        using var rofocaleModule = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + rofocale + "A13Rofocale", true)!, world, boss)!;
        var orbit = Component<GenericAOEs>(rofocaleModule, rofocale + "AetherialAcceleration");
        var text = new BossComponent.TextHints();
        orbit.OnActorUntargetable(boss);
        orbit.AddHints(0, boss, text);
        Check(text.Count == 0, "an ordinary disappearance does not warn about the prison charge");
        rofocaleModule.Enemies(0x1FC3).Add(Actor(4, 0x1FC3, new(106, -207)));
        orbit.OnActorUntargetable(boss);
        orbit.AddHints(0, boss, text);
        Check(text.Count == 1 && orbit.ActiveAOEs(0, boss).IsEmpty, "prison entry gets immediate guidance before the opening lane is identified");
        boss.PosRot = new(115, 0, -201, 0);
        orbit.Update();
        var aoes = orbit.ActiveAOEs(0, boss);
        Check(aoes.Length == 3 && aoes[0].Activation == Now.AddSeconds(3.4d) && aoes[0].Origin.Z < -210f, "the approach provides a timed opening before the first hit");
        var hints = new AIHints();
        orbit.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 7 && hints.ForbiddenZones[0].activation == Now.AddSeconds(3.4d), "the opening is actionable for native AI");
        var firstRearHit = new WPos(106.95044f, -209.12494f);
        var firstFrontHit = new WPos(104.52177f, -209.85414f);
        Check(aoes[0].Check(firstRearHit) && aoes[0].Check(firstFrontHit)
            && hints.ForbiddenZones[0].shapeDistance.Contains(firstRearHit) && hints.ForbiddenZones[0].shapeDistance.Contains(firstFrontHit),
            "the opening radar and native hazard cover the recorded damage on both sides of the pulse origin");
        boss.PosRot = new(115.4f, 0, -207.5f, 0);
        orbit.OnActorModelStateChange(boss, 19, 0, 0);
        Check(orbit.ActiveAOEs(0, boss)[0].Activation == Now.AddSeconds(0.38d), "the launch state reconciles the opening deadline");
        var pulse = new ActorCastEvent(new ActionID(ActionType.Spell, 9868u), 0, 0, 0, new(106.31f, -694, -210.93f), 1, 0, (-96.15f).Degrees());
        orbit.OnEventCast(caster, pulse);
        aoes = orbit.ActiveAOEs(0, caster);
        Check(aoes.Length == 3 && aoes[0].Origin.X < 106f && aoes[0].Color == Colors.Danger && aoes[1].Color == Colors.AOE,
            "orbit preview advances west from the first north-side pulse and shades later pulses");
        hints = new AIHints();
        orbit.AddAIHints(0, caster, default, hints);
        Check(hints.ForbiddenZones.Count > aoes.Length && hints.ForbiddenZones[0].activation == Now.AddSeconds(0.29d),
            "the AI receives additional timed orbit predictions beyond the radar window");
        var next = new ActorCastEvent(pulse.Action, 0, 0, 0, new(100.12f, -694, -210.2f), 1, 0, (-81.3f).Degrees());
        var firstPrediction = aoes[0].Origin;
        orbit.OnEventCast(caster, next);
        var advanced = orbit.ActiveAOEs(0, caster)[0].Origin;
        Check(advanced != firstPrediction, "the orbit advances across the angle wrap");
        orbit.OnEventCast(caster, pulse);
        Check(orbit.ActiveAOEs(0, caster)[0].Origin == advanced, "an out-of-order pulse cannot rewind the orbit");
        orbit.OnActorModelStateChange(boss, 20, 0, 0);
        orbit.OnEventCast(caster, next);
        Check(orbit.ActiveAOEs(0, caster).IsEmpty, "the final model state clears the orbit and ignores trailing effects");
        orbit.OnActorUntargetable(boss);
        orbit.OnEventCast(caster, pulse);
        orbit.OnCastStarted(caster, Cast(9866, center));
        Check(orbit.ActiveAOEs(0, caster).IsEmpty, "Heavenly Subjugation clears the circling charge forecast");
    }

    private static void RabanastreBleedingTiles()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var center = new WPos(106, -386);
        var boss = Actor(1, 0x1F97, center);
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A14Argath", true)!, world, boss)!;
        var tiles = Component<GenericAOEs>(module, prefix + "BleedingTiles");
        var north = Actor(2, 0x1EA7ED, new(106, -402));
        var east = Actor(3, 0x1EA7ED, new(122, -386));
        var south = Actor(4, 0x1EA7ED, new(106, -370));
        var west = Actor(5, 0x1EA7ED, new(90, -386));
        var placeholder = Actor(6, 0x1EA7ED, new(101.2131f, -388.5336f));
        var cast = Cast(9769, center);
        boss.CastInfo = cast;
        tiles.OnActorCreated(placeholder);
        tiles.OnActorCreated(north);
        tiles.OnActorCreated(north);
        tiles.OnActorCreated(east);
        tiles.OnActorCreated(south);
        tiles.OnActorCreated(west);
        var aoes = tiles.ActiveAOEs(0, boss);
        Check(aoes.Length == 4 && aoes[0].Activation == Now.AddSeconds(2.8), "only grid tiles provide early floor hazards and duplicate spawns are idempotent");
        Check(aoes[0].Check(new(111.614f, -396.385f)) && !aoes[0].Check(new(112f, -396.385f)), "the north square includes the observed bleed position with a small boundary margin");
        var hints = new AIHints();
        tiles.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 4 && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(center)),
            "the simultaneous squares publish native constraints without blocking the sword bait at center");
        tiles.OnActorEAnim(east, 0x00040008);
        Check(tiles.ActiveAOEs(0, boss)[1].Activation == Now && tiles.ActiveAOEs(0, boss)[1].Color == Colors.Danger, "tile animation reconciles only that square as live");
        tiles.OnActorEAnim(east, 0x00010040);
        tiles.OnActorDestroyed(east);
        Check(tiles.ActiveAOEs(0, boss).Length == 3, "tile fade and destruction cannot retire the other squares");
        tiles.OnCastFinished(boss, cast);
        Check(tiles.ActiveAOEs(0, boss).IsEmpty, "a cancelled Judgment Blade clears the tile predictions");
        boss.CastInfo = null;
        var corner = Actor(7, 0x1EA7ED, new(122, -370));
        tiles.OnActorCreated(corner);
        Check(tiles.ActiveAOEs(0, boss)[0].Activation == Now, "late attachment treats a real corner tile as immediately active");
    }

    private static void RabanastreAddsAndRim()
    {
        const string mateus = "Stormblood.Alliance.A11Mateus.";
        var module = Module(mateus + "A11Mateus", new(-320, 240));
        var guards = Component<BossComponent>(module, mateus + "AzureGuards");
        var player = Actor(1, 0, module.Center, player: true);
        var west = Actor(2, 0x1FA5, new(-335, 240));
        var east = Actor(3, 0x1FA5, new(-305, 240));
        west.IsTargetable = east.IsTargetable = true;
        west.TargetID = player.InstanceID;
        guards.OnActorCreated(west);
        guards.OnActorCreated(east);
        var spawn = west.Position;
        west.PosRot.X += 10;
        var hints = new AIHints();
        hints.PotentialTargets.Add(new(west, 0, false));
        hints.PotentialTargets.Add(new(east, 0, false));
        guards.AddAIHints(0, player, default, hints);
        Check(hints.PotentialTargets[0].DesiredPosition == spawn && hints.PotentialTargets[0].Priority > hints.PotentialTargets[1].Priority,
            "a tank keeps its assigned guard at the separate spawn point");

        const string argath = "Stormblood.Alliance.A14Argath.";
        module = Module(argath + "A14Argath", new(106, -386));
        var shade = Actor(4, 0x1F99, module.Center);
        Set(module, "RelevantEnemies", new Dictionary<uint, List<Actor>> { [0x1F99] = [shade] });
        var royalBlood = Component<BossComponent>(module, argath + "RoyalBlood");
        shade.TargetID = player.InstanceID;
        hints = new();
        royalBlood.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 0, "the temporary spawn target alone is not a Shade assignment");
        shade.Tether = new(17, player.InstanceID);
        royalBlood.OnTethered(shade, shade.Tether);
        hints = new();
        royalBlood.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 1, "only a player tethered to a live Shade gets the grouping goal");
        shade.Tether = new(1, 9);
        hints = new();
        royalBlood.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 1, "a Shade-to-Shade tether preserves the grouping goal while the Shade still follows its assigned player");
        shade.TargetID = 9;
        hints = new();
        royalBlood.AddAIHints(0, player, default, hints);
        Check(hints.GoalZones.Count == 0, "losing the matching attack target retires the old grouping goal");

        module.WorldState.Execute(new ActorState.OpCreate(9, 0, 0, 0, "", 0, ActorType.Player, Class.PLD, 70,
            new(106, 0, -386, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        var victim = module.WorldState.Actors.Find(9)!;
        var attack = Effect(870);
        attack.Targets.Add(new(victim.InstanceID, default));
        royalBlood.OnEventCast(shade, attack);
        royalBlood.AddAIHints(0, victim, default, hints);
        Check(hints.GoalZones.Count == 1, "an actual Shade attack recovers the recipient when the player tether was missed");
        shade.IsDead = true;
        royalBlood.OnActorDeath(shade);
        hints = new();
        royalBlood.AddAIHints(0, victim, default, hints);
        Check(hints.GoalZones.Count == 0, "the Shade's death retires the recipient and movement goal");

        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var center = new WPos(106, -386);
        var boss = Actor(5, 0x1F97, center);
        using var arenaModule = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + argath + "A14Argath", true)!, world, boss)!;
        arenaModule.StateMachine.Start(Now);
        var rim = arenaModule.Components.Single(component => component.GetType().Name == "LethalRim");
        var westRim = new WPos(80.47755f, -385.30975f);
        var inactive = new byte[] { 0, 1, 0, 8, 0, 0, 34, 2, 127, 8, 0, 0 };
        var active = new byte[] { 0, 1, 0, 8, 0, 0, 34, 2, 127, 8, 2, 2 };
        var reset = new byte[] { 0, 1, 0, 8, 0, 0, 34, 2, 127, 1, 1, 1 };
        player.PosRot = new(westRim.X, 0, westRim.Z, 0);
        Check(!arenaModule.Arena.InBounds(westRim), "late attachment starts with the smaller navigation bounds");
        rim.OnLegacyMapEffect(1, 0, [0, 1, 0, 8, 0, 0, 33, 2, 127, 8, 0, 0]);
        Check(!arenaModule.Arena.InBounds(westRim), "an unrelated map packet cannot open the rim");
        rim.OnLegacyMapEffect(1, 0, inactive);
        Check(arenaModule.Arena.InBounds(westRim) && !arenaModule.Arena.InBounds(center + new WDir(26.5f, 0)),
            "the explicit inactive state opens the verified floor while retaining an inset from the floor seam");

        var global = new BossComponent.GlobalHints();
        var ultima = Cast(9760, center);
        ultima.TotalTime = 2.2f;
        ultima.ElapsedTime = 0.0201654f;
        rim.OnCastStarted(boss, ultima);
        hints = new() { PathfindMapBounds = arenaModule.Bounds, PathfindMapCenter = center };
        rim.AddAIHints(0, player, default, hints);
        rim.AddGlobalHints(player, global);
        Check(global.Single().Contains("Move inward") && hints.ForbiddenZones.Single().activation == arenaModule.CastFinishAt(ultima, -0.25d)
            && hints.ForbiddenZones[0].shapeDistance.Distance(westRim) < 0f && arenaModule.Arena.InBounds(westRim),
            "the cast warns and publishes an inward deadline before the hard bounds change");
        CheckRabanastreRoute(arenaModule, player, hints, "Dark Ultima rim closure");
        rim.OnCastFinished(boss, ultima);
        hints = new();
        rim.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0 && arenaModule.Arena.InBounds(westRim), "interruption retires the forecast and preserves the verified floor");

        rim.OnCastStarted(boss, ultima);
        rim.OnEventCast(boss, Effect(9760));
        Check(!arenaModule.Arena.InBounds(westRim), "the Dark Ultima hit constrains navigation before the observed map confirmation");
        rim.OnLegacyMapEffect(1, 0, active);
        rim.OnLegacyMapEffect(1, 0, inactive);
        global.Clear();
        rim.AddGlobalHints(player, global);
        Check(global.Count == 1 && !arenaModule.Arena.InBounds(westRim), "the active state cannot be reopened by an inactive mask update");
        rim.OnLegacyMapEffect(1, 0, reset);
        global.Clear();
        rim.AddGlobalHints(player, global);
        Check(global.Count == 0 && !arenaModule.Arena.InBounds(westRim), "the observed reset restores conservative defaults and clears the warning");
        rim.OnLegacyMapEffect(1, 0, inactive);
        arenaModule.StateMachine.Reset();
        Check(!arenaModule.Arena.InBounds(westRim), "phase exit restores the conservative bounds even without the reset packet");
    }

    private static void RabanastreArgathAdds()
    {
        const string prefix = "Stormblood.Alliance.A14Argath.";
        var module = Module(prefix + "A14Argath", new(106, -386));
        module.Arena.Bounds = new ArenaBoundsSquare(26f);
        var world = module.WorldState;
        world.Execute(new ActorState.OpCreate(1, 0, 0, 0, "", 0, ActorType.Player, Class.WAR, 70,
            new(106, 1, -386, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 1, false)));
        var player = world.Actors.Find(1)!;
        var boss = Actor(2, 0x1F97, module.Center);
        module.PrimaryActor = boss;
        var emptySoul = Component<RaidwideCast>(module, prefix + "EmptySoul");
        var raidwides = Component<RaidwideCasts>(module, prefix + "Raidwides");
        var first = Actor(3, 0x1F9A, new(101.2f, -387.58f));
        var last = Actor(4, 0x1F9A, new(93.55f, -391.24f));
        first.IsTargetable = last.IsTargetable = false;
        var shade = Actor(5, 0x1F99, module.Center);
        var blood = Component<CastHint>(module, prefix + "RoyalBlood");
        Set(module, "RelevantEnemies", new Dictionary<uint, List<Actor>> { [0x1F99] = [] });
        var bloodCast = Cast(9758, new(85.98462f, -386.0075f));
        bloodCast.TotalTime = 4.7f;
        bloodCast.ElapsedTime = 0.020528f;
        player.PosRot = new(115.42336f, 1, -372.1393f, 0);
        blood.OnCastStarted(boss, bloodCast);
        var bloodContext = new NavigationDecision.Context();
        var earlyBlood = BloodHints();
        var bloodRoute = NavigationDecision.Build(bloodContext, Now, earlyBlood, player, forbiddenZoneCushion: 0.5f);
        var bloodEndpoint = bloodContext.ThetaStar.CellCenter(bloodContext.ThetaStar.BestIndex());
        Check(earlyBlood.ForbiddenZones.Count == 1 && bloodRoute.Destination != null && bloodEndpoint.InCircle(module.Center, 6f)
            && earlyBlood.ForbiddenZones[0].shapeDistance.Distance(bloodEndpoint) > 0.5f && bloodRoute.TimeToGoal < bloodCast.NPCRemainingTime,
            "an unassigned player can stage during Royal Blood without crossing a concurrent hazard");
        blood.OnCastFinished(boss, bloodCast);
        Check(BloodHints().GoalZones.Count == 0, "an interrupted Royal Blood cancels staging");
        blood.OnCastStarted(boss, bloodCast);
        world.Frame = new(Now.AddSeconds(5.0224), 0, 0, 0, 0, 2);
        blood.OnEventCast(boss, Effect(9758));
        blood.OnCastFinished(boss, bloodCast);
        Check(BloodHints().GoalZones.Count == 1, "the successful summon bridges the gap before Shade spawn");
        world.Frame = new(Now.AddSeconds(5.31), 0, 0, 0, 0, 3);
        shade.IsTargetable = false;
        module.Enemies(0x1F99).Add(shade);
        blood.OnActorCreated(shade);
        world.Frame = new(Now.AddSeconds(8), 0, 0, 0, 0, 4);
        Check(BloodHints().GoalZones.Count == 1, "untargetable Shades keep the preparation active after the spawn estimate expires");
        shade.IsTargetable = true;
        Check(BloodHints().GoalZones.Count == 0, "a targetable Shade releases the pre-tether preparation");
        world.Frame = new(Now, 0, 0, 0, 0, 1);

        AIHints BloodHints()
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            result.AddForbiddenZone(new SDCircle(module.Center + new WDir(5f, 5f), 2f), world.CurrentTime);
            blood.AddAIHints(0, player, default, result);
            return result;
        }

        var summon = Cast(9759, boss.Position);
        emptySoul.OnCastStarted(boss, summon);
        var global = new BossComponent.GlobalHints();
        emptySoul.AddGlobalHints(player, global);
        Check(global.Any(hint => hint.Contains("gauge")) && Hints().PredictedDamage.Count == 0,
            "Empty Soul gives the add instruction during the summon without predicting damage too early");
        emptySoul.OnCastFinished(boss, summon);
        foreach (var shard in new[] { first, last })
            emptySoul.OnActorCreated(shard);
        global.Clear();
        emptySoul.AddGlobalHints(player, global);
        Check(global.Count == 1, "the add instruction persists after the summon");
        player.PosRot = new(87.846f, 1, -389.242f, 0);
        var approach = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        approach.AddForbiddenZone(new SDCircle(last.Position, 5f), Now);
        emptySoul.AddAIHints(0, player, default, approach);
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, approach, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && endpoint.InCircle(first.Position, 4f)
            && approach.ForbiddenZones[0].shapeDistance.Distance(endpoint) > 0.5f && route.TimeToGoal < 2.57f,
            "the native AI can stage at a reachable Shard before targetability even if a hazard blocks the nearer Shard");
        first.IsTargetable = last.IsTargetable = true;
        Check(Hints().GoalZones.Count == 0, "untargetable staging ends once the Shards can be attacked");
        var priority = module.GetType().GetMethod("CalculateModuleAIHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var targeting = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        targeting.AddForbiddenZone(new SDCircle(last.Position, 5.5f), Now);
        targeting.PotentialTargets.AddRange([new(first, AIHints.Enemy.PriorityUndesirable, false), new(last, 0, false), new(boss, 0, false)]);
        priority.Invoke(module, [0, player, default(PartyRolesConfig.Assignment), targeting]);
        Check(targeting.PotentialTargets[0].Priority > targeting.PotentialTargets[2].Priority && targeting.GoalZones.Single()(first.Position) == 6f,
            "the targetable Shard replaces staging with an attack approach even before it acquires local enmity");
        route = NavigationDecision.Build(context, Now, targeting, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && endpoint.InCircle(first.Position, 4.5f)
            && targeting.ForbiddenZones[0].shapeDistance.Distance(endpoint) > 0.5f,
            "the AI can approach another targetable Shard when the nearer Shard is covered by an active hazard");
        player.PosRot.X = endpoint.X;
        player.PosRot.Z = endpoint.Z;
        targeting.GoalZones.Clear();
        priority.Invoke(module, [0, player, default(PartyRolesConfig.Assignment), targeting]);
        Check(targeting.PotentialTargets[0].Priority > targeting.PotentialTargets[1].Priority,
            "reaching attack range makes that Shard win native target selection");

        var ordinary = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
        ordinary.PotentialTargets.AddRange([new(shade, AIHints.Enemy.PriorityUndesirable, false), new(boss, 0, false)]);
        priority.Invoke(module, [0, player, default(PartyRolesConfig.Assignment), ordinary]);
        Check(ordinary.PotentialTargets[0].Priority > ordinary.PotentialTargets[1].Priority && ordinary.GoalZones.Single()(shade.Position) == 6f,
            "an ordinary targetable Shade already gets attack priority and a native approach before Unholy Sacrifice");
        shade.CastInfo = Cast(9776, shade.Position);
        raidwides.OnCastStarted(shade, shade.CastInfo);
        targeting = new();
        targeting.PotentialTargets.AddRange([new(shade, AIHints.Enemy.PriorityUndesirable, false), new(first, 0, false), new(last, AIHints.Enemy.PriorityInvincible, false), new(boss, 0, false)]);
        priority.Invoke(module, [0, player, default(PartyRolesConfig.Assignment), targeting]);
        Check(targeting.PotentialTargets[0].Priority > targeting.PotentialTargets[1].Priority
            && targeting.PotentialTargets[1].Priority > targeting.PotentialTargets[3].Priority
            && targeting.PotentialTargets[2].Priority == AIHints.Enemy.PriorityInvincible
            && targeting.GoalZones.Single()(shade.Position) == 6f && targeting.GoalZones[0](last.Position) == 0f,
            "the Shade about to explode takes approach priority and immune targets remain excluded");
        Check(Hints().PredictedDamage.Count == 1, "an active sacrifice cast still publishes raidwide damage");
        raidwides.OnActorDeath(shade);
        Check(Hints().PredictedDamage.Count == 0, "killing the casting Shade cancels its predicted damage");

        emptySoul.OnActorDeath(first);
        emptySoul.OnActorDestroyed(first);
        Check(Hints().PredictedDamage.Count == 0, "Dark Ultima remains inactive while a Shard survives");
        emptySoul.OnActorDeath(last);
        var hints = Hints();
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Players[0]
            && hints.PredictedDamage[0].Activation == Now.AddSeconds(5.25) && hints.GoalZones.Count == 1,
            "the final kill gives mitigation and a soft healing regroup before the ultimate cast");

        world.Frame = new(Now.AddSeconds(3.107), 0, 0, 0, 0, 1);
        boss.CastInfo = Cast(9760, boss.Position);
        boss.CastInfo.TotalTime = 2.2f;
        emptySoul.OnCastStarted(boss, boss.CastInfo);
        hints = Hints();
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == module.CastFinishAt(boss.CastInfo)
            && hints.PredictedDamage[0].Activation != Now.AddSeconds(5.25),
            "the confirmed cast replaces the early prediction with one authoritative deadline");
        emptySoul.OnEventCast(boss, Effect(9760));
        Check(Hints().PredictedDamage.Count == 0 && Hints().GoalZones.Count == 0, "the effect retires mitigation and regrouping");

        emptySoul.OnCastStarted(boss, summon);
        foreach (var shard in new[] { first, last })
            emptySoul.OnActorCreated(shard);
        emptySoul.OnActorDestroyed(first);
        emptySoul.OnActorDeath(last);
        Check(Hints().PredictedDamage.Count == 0, "despawning a living Shard cannot trigger a last-kill prediction");
        emptySoul.OnCastStarted(boss, boss.CastInfo);
        Check(Hints().PredictedDamage.Count == 1, "the actual ultimate cast still works when the wave was not observed in full");

        AIHints Hints()
        {
            var result = new AIHints();
            raidwides.AddAIHints(0, player, default, result);
            emptySoul.AddAIHints(0, player, default, result);
            return result;
        }
    }

    private static void RabanastreFrostwave()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var module = Module(prefix + "A11Mateus", new(-320, 240));
        module.Arena.Bounds = new ArenaBoundsSquare(29.5f);
        var world = module.WorldState;
        world.Execute(new ActorState.OpCreate(1, 0, 0, 0, "", 0, ActorType.Player, Class.WAR, 70,
            new(-320, 1, 240, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 1, false)));
        world.Party.SetAllianceGroups(0, 1, 2);
        var player = world.Actors.Find(1)!;
        var boss = Actor(2, 0x1F9B, module.Center);
        var guards = Component<RaidwideInstant>(module, prefix + "AzureGuards");
        var west = Actor(3, 0x1FA5, new(-337, 249));
        var north = Actor(4, 0x1FA5, new(-320, 214));
        var east = Actor(5, 0x1FA5, new(-300, 253));
        foreach (var guard in new[] { west, north, east })
            guards.OnActorCreated(guard);
        Check(Select(0) == west && Select(8) == north && Select(16) == east,
            "targetable guards without local enmity split by alliance even when all groups start at the same place");
        Check(Select(0, west) != west, "an ineligible assigned guard does not prevent selecting a live fallback");
        var context = new NavigationDecision.Context();
        var pickup = GuardHints(0);
        var approach = NavigationDecision.Build(context, Now, pickup, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(approach.Destination != null && pickup.GoalZones.Single()(endpoint) > 0f
            && endpoint.InCircle(west.Position, 3f + west.HitboxRadius),
            "the native movement owner can reach an unclaimed guard from outside melee range");

        var northSpawn = north.Position;
        north.TargetID = player.InstanceID;
        north.PosRot.Z = 231f;
        var separation = GuardHints(0);
        separation.AddForbiddenZone(new SDCircle(new(-320f, 226f), 2f), Now);
        var pullRoute = NavigationDecision.Build(context, Now, separation, player, forbiddenZoneCushion: 0.5f);
        var tankEndpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(pullRoute.Destination != null && module.Arena.InBounds(tankEndpoint)
            && tankEndpoint.Z < northSpawn.Z && separation.ForbiddenZones.All(zone => zone.shapeDistance.Distance(tankEndpoint) > 0.5f),
            "the tank can lead a displaced guard back toward its spawn without walking off the arena or into a concurrent hazard");
        north.TargetID = 0;
        north.PosRot.Z = northSpawn.Z;
        east.TargetID = player.InstanceID;
        Check(Select(0) == east, "the local tank retains an already engaged guard");

        west.IsDead = north.IsDead = true;
        guards.OnActorDeath(north);
        Check(guards.Activation == default, "a surviving guard keeps Frostwave inactive");
        var lastGuard = new BossComponent.TextHints();
        guards.AddHints(0, player, lastGuard);
        Check(lastGuard.Any(hint => hint.Item1.Contains("Frostwave")), "the last surviving guard warns players before the final kill");
        east.IsDead = true;
        guards.OnActorDeath(east);
        var hints = new AIHints();
        guards.AddAIHints(0, player, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Players[0]
            && hints.PredictedDamage[0].Activation == Now.AddSeconds(1d)
            && hints.GoalZones.Single()(module.Center) > hints.GoalZones[0](east.Position) && hints.ForbiddenZones.Count == 0,
            "the final kill gives the AI an imminent raidwide and a nonmandatory healing regroup");
        guards.OnEventCast(boss, Effect(9793));
        Check(guards.Activation == default, "the ultimate effect clears the Frostwave window");

        west.IsDead = north.IsDead = east.IsDead = false;
        foreach (var guard in new[] { west, north, east })
            guards.OnActorCreated(guard);
        guards.OnActorDestroyed(west);
        north.IsDead = east.IsDead = true;
        guards.OnActorDeath(east);
        Check(guards.Activation == default, "a live despawn during reset cannot look like a completed guard wave");

        Actor Select(int slot, Actor? ineligible = null)
            => GuardHints(slot, ineligible).PotentialTargets.Single(enemy => enemy.Priority == 6).Actor;

        AIHints GuardHints(int slot, Actor? ineligible = null)
        {
            var targets = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            foreach (var guard in new[] { west, north, east })
                targets.PotentialTargets.Add(new(guard, guard == ineligible ? AIHints.Enemy.PriorityInvincible : AIHints.Enemy.PriorityUndesirable, false));
            guards.AddAIHints(slot, player, default, targets);
            return targets;
        }
    }

    private static void RabanastreAquaSpheres()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1F9B, new(-320.02747f, 229.99927f));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A11Mateus", true)!, world, boss)!;
        world.Party.SetAllianceGroups(1, 0, 2);
        world.Execute(new ActorState.OpCreate(1, 0, 0, 0, "", 0, ActorType.Player, Class.WAR, 70,
            new(-295.76526f, 1.23f, 250.24942f, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        var player = world.Actors.Find(1)!;
        var component = Component<BossComponent>(module, prefix + "AquaSpheres");
        var unbind = Cast(9779, boss.Position);
        unbind.TotalTime = 4.7f;
        var north = new WPos(-319.9964f, 213.9915f);
        var west = new WPos(-336.7781f, 249.2836f);
        var east = new WPos(-299.7633f, 252.8481f);
        component.OnCastStarted(boss, unbind);
        var hints = Hints();
        var context = new NavigationDecision.Context();
        var stage = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(hints.GoalZones.Single()(north) > 3.9f && stage.Destination != null && endpoint.InCircle(north, 4f)
            && stage.TimeToGoal < 11.93f, "Unbind gives a safe, attainable approach to the recorded sector before the sphere can be attacked");
        component.OnCastFinished(boss, unbind);
        Check(Hints().GoalZones.Count == 0, "interrupting Unbind drops the Aqua Sphere preview");

        component.OnCastStarted(boss, unbind);
        unbind.EventHappened = true;
        world.Frame = new(Now.AddSeconds(4.975), 0, 0, 0, 0, 2);
        component.OnEventCast(boss, Effect(9779));
        component.OnCastFinished(boss, unbind);
        world.Frame = new(Now.AddSeconds(11.845), 0, 0, 0, 0, 3);
        var first = AddSphere(2, north, false);
        var second = AddSphere(3, west, false);
        var third = AddSphere(4, east, false);
        Check(Hints(first, second, third).GoalZones.Single()(north) > 3.9f, "the summon preserves staging until a sphere becomes targetable");
        world.Frame = new(Now.AddSeconds(11.93324d), 0, 0, 0, 0, 4);
        world.Execute(new ActorState.OpTargetable(first.InstanceID, true));
        world.Execute(new ActorState.OpTargetable(second.InstanceID, true));
        world.Party.SetAllianceGroups(2, 0, 1);
        var text = new BossComponent.TextHints();
        component.AddHints(0, player, text);
        var waitingEast = Hints(first, second, third);
        Check(waitingEast.GoalZones.Single()(east) > 3.9f && waitingEast.PriorityTargetsSpan.Length == 0
            && text.Single().Item1.StartsWith("Move toward"),
            "the recorded delay keeps the southeast alliance staged while north and west spheres are targetable");
        world.Frame = new(Now.AddSeconds(12.10678d), 0, 0, 0, 0, 5);
        world.Execute(new ActorState.OpTargetable(third.InstanceID, true));
        Check(Hints(first, second, third).PriorityTargetsSpan[0].Actor == third
            && Hints(first, second, third).GoalZones.Single()(east) == 6f,
            "the southeast sphere replaces only its own staging marker when it becomes targetable");
        world.Party.SetAllianceGroups(1, 0, 2);
        var active = Hints(first, second, third);
        text.Clear();
        component.AddHints(0, player, text);
        Check(active.PriorityTargetsSpan[0].Actor == first && active.GoalZones.Single()(north) == 6f
            && active.GoalZones[0](west) == 0f && text.Any(hint => hint.Item1.Contains("Aqua Sphere")),
            "the targetable sphere replaces preparation with alliance attack range and a player cue");
        var pillar = Actor(5, 0x1FA0, north + new WDir(0, 8));
        var urgent = Hints(first, second, third, pillar);
        Check(urgent.PriorityTargetsSpan[0].Actor == pillar && urgent.GoalZones.Single()(pillar.Position) == 8f
            && urgent.GoalZones[0](west) == 0f, "the Blizzard III approach replaces competing Aqua Sphere movement");
        first.IsDead = true;
        Check(Hints(first, second, third).GoalZones.Single()(west) == 6f,
            "the AI can help another alliance once its own sphere dies");

        Actor AddSphere(ulong id, WPos position, bool targetable)
        {
            world.Execute(new ActorState.OpCreate(id, 0x1F9F, 0, 0, "", 0, ActorType.Enemy, Class.None, 70,
                position.ToVec4(1.23f), 1.8f, new(100, 100, 0, 0, 0), targetable, true, 0, 0, 0));
            return world.Actors.Find(id)!;
        }

        AIHints Hints(params Actor[] candidates)
        {
            var result = new AIHints();
            result.AddForbiddenZone(new SDCircle(new(-305f, 232f), 3f), world.CurrentTime);
            foreach (var enemy in candidates)
                result.PotentialTargets.Add(new(enemy, AIHints.Enemy.PriorityUndesirable, false));
            module.CalculateAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
    }

    private static void RabanastreMateusPhaseResponses()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var center = new WPos(-320, 240);
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        world.Execute(new ActorState.OpCreate(1, 0x1F9B, 0, 0, "", 0, ActorType.Enemy, Class.None, 70,
            new(-320, 1, 240, 0), 4.5f, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
        world.Execute(new ActorState.OpCreate(2, 0, 0, 0, "", 0, ActorType.Player, Class.None, 70,
            new(-320, 1, 246, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
        world.Execute(new PartyState.OpModify(0, new(0, 2, false)));
        world.Execute(new ActorState.OpTarget(1, 2));
        var boss = world.Actors.Find(1)!;
        var tank = world.Actors.Find(2)!;
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A11Mateus", true)!, world, boss)!;
        var cleave = Component<Cleave>(module, prefix + "FlashFreeze");
        var skater = Actor(3, 0x1F9D, center);
        Check(cleave.OriginsAndTargets().Single().target == tank, "Flash-freeze tracks Mateus before the skate starts");
        cleave.OnActorCreated(skater);
        cleave.OnActorDestroyed(skater);
        Check(cleave.OriginsAndTargets().Single().target == tank, "Totema's despawn does not turn off Mateus's own cleave");
        boss.CastInfo = Cast(9789, center);
        Check(cleave.OriginsAndTargets().Count == 0, "an active boss cast suppresses the between-cast cleave");
        boss.CastInfo = null;
        Check(cleave.OriginsAndTargets().Count == 1, "Flash-freeze resumes after the boss cast");
        world.Execute(new ActorState.OpTargetable(1, false));
        Check(cleave.OriginsAndTargets().Count == 0, "an untargetable Mateus does not forecast the cleave");
        world.Execute(new ActorState.OpTargetable(1, true));

        var blizzard = Component<GenericAOEs>(module, prefix + "BlizzardIV");
        var helper = Actor(4, 0x1FA4, new(-320, 215));
        var impact = Cast(9790, new(-320, 265));
        var dualcast = Cast(9788, center);
        dualcast.TotalTime = 4.7f;
        blizzard.OnCastStarted(boss, dualcast);
        var hints = new AIHints();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == module.CastFinishAt(dualcast, 14.8d)
            && hints.ForbiddenZones.Count == 0, "Dualcast gives an early warning without guessing an impact location");
        blizzard.OnCastFinished(boss, dualcast);
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 0, "interrupting Dualcast removes its unconfirmed prediction");
        blizzard.OnCastStarted(boss, dualcast);
        blizzard.OnEventCast(boss, Effect(9788));
        blizzard.OnCastFinished(boss, dualcast);
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == Now.AddSeconds(14.8d),
            "the successful Dualcast effect keeps the warning active between casts");
        blizzard.OnEventCast(boss, Effect(9787));
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == Now.AddSeconds(11.8d)
            && hints.ForbiddenZones.Count == 0, "the summon warns healers early and leaves positions unrestricted until the real cast");
        blizzard.OnCastStarted(helper, impact);
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 1 && hints.PredictedDamage[0].Activation == module.CastFinishAt(impact)
            && hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(impact.LocXZ)
            && !hints.ForbiddenZones[0].shapeDistance.Contains(helper.Position), "the actual cast replaces the estimate and uses the impact position rather than the helper spawn");
        blizzard.OnCastFinished(helper, impact);
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.PredictedDamage.Count == 0 && hints.ForbiddenZones.Count == 0, "the early estimate does not reappear after the real cast");
        var secondHelper = Actor(9, 0x1FA4, new(-320, 225));
        var laterHelper = Actor(13, 0x1FA4, center);
        var laterCast = Cast(9790, center);
        var simultaneous = Cast(9790, new(-340.02f, 219.99f));
        simultaneous.ElapsedTime = 4.96f;
        impact.ElapsedTime = 5f;
        // Deliver the later volley first to exercise chronological ordering.
        blizzard.OnCastStarted(laterHelper, laterCast);
        blizzard.OnCastStarted(helper, impact);
        blizzard.OnCastStarted(secondHelper, simultaneous);
        var preview = blizzard.ActiveAOEs(0, tank);
        Check(preview.Length == 3 && preview[0].Color == Colors.Danger && preview[1].Color == Colors.Danger
            && preview[2].Color == Colors.AOE, "the first volley stays bright while the second is shaded");
        hints = new();
        blizzard.AddAIHints(0, tank, default, hints);
        Check(hints.ForbiddenZones.Count == 3 && hints.PredictedDamage.Select(hit => hit.Activation)
            .SequenceEqual([module.CastFinishAt(impact), module.CastFinishAt(laterCast)]),
            "the AI keeps all three hazards and one independent mitigation deadline per volley");
        var blizzardText = new BossComponent.TextHints();
        blizzard.AddHints(0, tank, blizzardText);
        Check(blizzardText.Count == 0, "a shaded later marker alone does not produce an immediate dodge warning");
        blizzard.OnEventCast(helper, Effect(9790));
        Check(blizzard.ActiveAOEs(0, tank).Length == 2 && blizzard.ActiveAOEs(0, tank)[1].Color == Colors.AOE,
            "one resolving marker does not end its volley or brighten the later marker");
        blizzard.OnEventCast(secondHelper, Effect(9790));
        blizzard.AddHints(0, tank, blizzardText);
        Check(blizzard.ActiveAOEs(0, tank).Length == 1 && blizzard.ActiveAOEs(0, tank)[0].Color == Colors.Danger
            && blizzardText.Count == 1, "the later volley advances when the last earlier marker resolves");
        blizzard.OnCastFinished(laterHelper, laterCast);

        blizzard.OnCastStarted(helper, impact);
        blizzard.OnCastStarted(secondHelper, Cast(9790, center));
        blizzard.OnActorDestroyed(helper);
        Check(blizzard.ActiveAOEs(0, tank) is var remaining && remaining.Length == 1 && remaining[0].ActorID == secondHelper.InstanceID,
            "a despawned marker retires only its own hazard");
        blizzard.OnActorDestroyed(boss);
        Check(blizzard.ActiveAOEs(0, tank).IsEmpty, "Mateus's despawn clears remaining Blizzard IV markers");

        var north = new AIHints.Enemy(Actor(5, 0x1F9F, new(-320, 214)), 0, false);
        var west = new AIHints.Enemy(Actor(6, 0x1F9F, new(-337, 249)), 0, false);
        var east = new AIHints.Enemy(Actor(7, 0x1F9F, new(-300, 253)), 0, false);
        world.Execute(new ActorState.OpCreate(8, 0x1FA0, 0, 0, "", 0, ActorType.Enemy, Class.None, 70,
            new(-320, 1, 221.2f, 0), 1.5f, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
        var pillar = world.Actors.Find(8)!;
        var blizzardAdd = new AIHints.Enemy(pillar, AIHints.Enemy.PriorityUndesirable, false);
        var invinciblePillar = new AIHints.Enemy(Actor(9, 0x1FA0, center), AIHints.Enemy.PriorityInvincible, false);
        var passiveOther = new AIHints.Enemy(Actor(10, 0x2062, center), AIHints.Enemy.PriorityUndesirable, false);
        var azer = new AIHints.Enemy(Actor(14, 0x1F9C, center), AIHints.Enemy.PriorityUndesirable, false);
        var excludedAzer = new AIHints.Enemy(Actor(15, 0x1F9C, center), AIHints.Enemy.PriorityInvincible, false);
        var toad = new AIHints.Enemy(Actor(11, 0x1FA1, center), AIHints.Enemy.PriorityUndesirable, false);
        var untargetableToad = new AIHints.Enemy(Actor(12, 0x1FA1, center), AIHints.Enemy.PriorityUndesirable, false);
        untargetableToad.Actor.IsTargetable = false;
        var priority = module.GetType().GetMethod("CalculateModuleAIHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        hints.PotentialTargets.AddRange([north, west, east, blizzardAdd, invinciblePillar, passiveOther, toad, untargetableToad, azer, excludedAzer]);
        priority.Invoke(module, [0, tank, default(PartyRolesConfig.Assignment), hints]);
        Check(north.Priority == west.Priority && west.Priority == east.Priority, "missing alliance labels preserve equal sphere priority");
        Check(blizzardAdd.Priority > north.Priority && invinciblePillar.Priority == AIHints.Enemy.PriorityInvincible
            && passiveOther.Priority == AIHints.Enemy.PriorityUndesirable,
            "the passive Blizzard III pillar takes priority without bypassing invulnerability or pulling other passive adds");
        Check(toad.Priority > 0 && untargetableToad.Priority == AIHints.Enemy.PriorityUndesirable,
            "the AI can initiate a targetable Flume Toad to create an air bubble without pulling untargetable actors");
        Check(azer.Priority > 0 && azer.Priority < north.Priority && excludedAzer.Priority == AIHints.Enemy.PriorityInvincible,
            "a passive Ice Azer can be attacked without overriding ineligibility or outranking a sphere");
        var azerBlizzard = Component<SingleTargetCast>(module, prefix + "IceAzerBlizzard");
        var azerCast = Cast(9784, center);
        azerCast.TargetID = tank.InstanceID;
        azer.Actor.CastInfo = azerCast;
        azerBlizzard.OnCastStarted(azer.Actor, azerCast);
        var damage = new AIHints();
        azerBlizzard.AddAIHints(0, tank, default, damage);
        Check(damage.PredictedDamage.Single() is var blizzardHit && blizzardHit.Players[0]
            && blizzardHit.Type == AIHints.PredictedDamageType.None && blizzardHit.Activation == module.CastFinishAt(azerCast),
            "Ice Azer Blizzard forecasts ordinary damage only to the actual recipient");
        azerBlizzard.OnActorDeath(azer.Actor);
        damage.Clear();
        azerBlizzard.AddAIHints(0, tank, default, damage);
        Check(damage.PredictedDamage.Count == 0, "the Ice Azer death clears the pending Blizzard prediction");
        world.Party.SetAllianceGroups(1, 0, 2);
        priority.Invoke(module, [0, tank, default(PartyRolesConfig.Assignment), hints]);
        Check(blizzardAdd.Priority > north.Priority && north.Priority > west.Priority && west.Priority == east.Priority,
            "Alliance B prioritizes the northern sphere while a spawned Blizzard III still takes precedence");
        priority.Invoke(module, [8, tank, default(PartyRolesConfig.Assignment), hints]);
        Check(west.Priority > north.Priority && west.Priority > east.Priority, "Alliance A prioritizes the southwest sphere");
        priority.Invoke(module, [16, tank, default(PartyRolesConfig.Assignment), hints]);
        Check(east.Priority > north.Priority && east.Priority > west.Priority, "Alliance C prioritizes the southeast sphere");

        var whisper = Component<CastHint>(module, prefix + "WhiteWhisper");
        var text = new BossComponent.TextHints();
        tank.Tether = new(8, pillar.InstanceID);
        whisper.AddHints(0, tank, text);
        Check(text.Count == 1, "the recorded player-to-pillar tether gives personal guidance before the cast");
        tank.Class = Class.WAR;
        var alternate = Actor(14, 0x1FA0, new(-337f, 244f));
        var pillarRoute = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = center };
        pillarRoute.AddForbiddenZone(new SDCircle(pillar.Position, 6f), Now);
        pillarRoute.PotentialTargets.AddRange([new(pillar, AIHints.Enemy.PriorityUndesirable, false),
            new(alternate, AIHints.Enemy.PriorityUndesirable, false), invinciblePillar]);
        priority.Invoke(module, [0, tank, default(PartyRolesConfig.Assignment), pillarRoute]);
        Check(pillarRoute.GoalZones.Single()(pillar.Position) == 10f && pillarRoute.GoalZones[0](alternate.Position) == 8f
            && pillarRoute.GoalZones[0](center) == 0f, "the tethered pillar gets a preference while a second eligible pillar stays available");
        var routeContext = new NavigationDecision.Context();
        var route = NavigationDecision.Build(routeContext, Now, pillarRoute, tank, forbiddenZoneCushion: 0.5f);
        var destination = routeContext.ThetaStar.CellCenter(routeContext.ThetaStar.BestIndex());
        Check(route.Destination != null && destination.InCircle(alternate.Position, 3f + alternate.HitboxRadius)
            && pillarRoute.ForbiddenZones[0].shapeDistance.Distance(destination) > 0.5f,
            "native navigation reaches a safe alternative when the tethered pillar is covered by a hazard");
        tank.PosRot = destination.ToVec4();
        priority.Invoke(module, [0, tank, default(PartyRolesConfig.Assignment), pillarRoute]);
        Check(pillarRoute.PotentialTargets[1].Priority > pillarRoute.PotentialTargets[0].Priority,
            "the accessible pillar takes targeting priority when the AI arrives in attack range");
        var cast = Cast(10030, pillar.Position);
        cast.TotalTime = 14.7f;
        pillar.CastInfo = cast;
        whisper.OnCastStarted(pillar, cast);
        var global = new BossComponent.GlobalHints();
        whisper.AddGlobalHints(tank, global);
        Check(global.Count == 1 && global[0].Contains($"{cast.NPCRemainingTime:f1}s"), "White Whisper displays its live cast countdown");
        pillar.IsDead = true;
        whisper.OnActorDeath(pillar);
        text.Clear();
        whisper.AddHints(0, tank, text);
        Check(!whisper.Active && text.Count == 0, "pillar death clears cast and tether guidance before the later untether");
    }

    private static void RabanastreBlizzardSphere()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1F9B, new(-320, 240));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A11Mateus", true)!, world, boss)!;
        var kite = Component<BossComponent>(module, prefix + "BlizzardSphere");
        var chill = Component<GenericAOEs>(module, prefix + "Chill");
        var player = AddActor(1, 0, ActorType.Player, new(-320, 240));
        var ally = AddActor(2, 0, ActorType.Player, new(-320, 235));
        var sphere = AddActor(3, 0x1FA6, ActorType.Enemy, new(-320, 247));
        var unrelated = AddActor(4, 0x1FA0, ActorType.Enemy, new(-323, 245));
        world.Execute(new PartyState.OpModify(0, new(0, 1, false)));
        world.Execute(new PartyState.OpModify(1, new(0, 2, false)));
        unrelated.Tether = new(8, player.InstanceID);
        Check(Hints(player).ForbiddenZones.Count == 0, "another mechanic's tether 8 does not start sphere kiting");

        sphere.Tether = new(8, player.InstanceID);
        var hints = Hints(player);
        var raidward = new WPos(-320, 230);
        var clear = new WPos(-307, 250);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Contains(player.Position)
            && hints.GoalZones.Single()(clear) > hints.GoalZones[0](raidward),
            "the live sphere tether imposes spacing and prefers an aim away from the boss and alliance before the cast");
        Check(Hints(ally).GoalZones.Count == 0 && Hints(ally).ForbiddenZones.Count == 0,
            "other players receive no speculative hazard from a tether before Chill starts");

        hints.AddForbiddenZone(new SDCircle(new(-330, 242), 4f), Now);
        hints.Normalize();
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination is { } step && module.Arena.InBounds(step)
            && hints.ForbiddenZones[0].shapeDistance.Distance(step) > hints.ForbiddenZones[0].shapeDistance.Distance(player.Position)
            && !hints.ForbiddenZones[1].shapeDistance.Contains(step),
            $"urgent recovery takes an in-bounds step away from the sphere, destination {route.Destination}, endpoint {endpoint}");
        player.PosRot.Z = raidward.Z;
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && module.Arena.InBounds(endpoint) && hints.GoalZones[0](endpoint) > 0
            && hints.ForbiddenZones.All(zone => zone.shapeDistance.Distance(endpoint) > 0.5f),
            "from safe spacing the native AI finds a clear aim without returning to either hazard");
        player.PosRot.Z = module.Center.Z;

        var cast = Cast(9798, sphere.Position);
        cast.Rotation = sphere.AngleTo(player);
        cast.TotalTime = 4.7f;
        sphere.CastInfo = cast;
        chill.OnCastStarted(sphere, cast);
        hints = Hints(player);
        Check(hints.GoalZones.Count == 0 && hints.ForbiddenZones.Count == 2 && Hints(ally).ForbiddenZones.Count == 1,
            "the actual Chill cone replaces soft aiming guidance and applies to every player");
        chill.OnCastFinished(sphere, cast);
        sphere.CastInfo = null;
        sphere.Tether = new(8, ally.InstanceID);
        Check(Hints(player).ForbiddenZones.Count == 0 && Hints(ally).ForbiddenZones.Count == 1,
            "the live tether immediately transfers the response when its target changes");
        sphere.IsDead = true;
        Check(Hints(ally).ForbiddenZones.Count == 0, "a retired sphere clears spacing and aiming guidance");

        Actor AddActor(ulong id, uint oid, ActorType type, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, type, Class.None, 70,
                new(position.X, 0, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, type == ActorType.Player, 0, 0, 0));
            return world.Actors.Find(id)!;
        }

        AIHints Hints(Actor actor)
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            chill.AddAIHints(0, actor, default, result);
            kite.AddAIHints(0, actor, default, result);
            return result;
        }
    }

    private static void RabanastreAirBubbles()
    {
        const string prefix = "Stormblood.Alliance.A11Mateus.";
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, 0x1F9B, new(-320, 240));
        using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod." + prefix + "A11Mateus", true)!, world, boss)!;
        var froth = Component<GenericAOEs>(module, prefix + "Froth");
        var player = AddPlayer(1, module.Center);
        player.Statuses[0] = new(1429, 6, default, 0);
        var text = new BossComponent.TextHints();
        froth.AddHints(0, player, text);
        Check(text.Any(hint => hint.Item1.Contains("Flume Toad")), "an urgent Breathless stack gives a player cue even before bubbles exist");
        var nearToad = Actor(20, 0x1FA1, module.Center + new WDir(12f, 0f));
        var reachableToad = Actor(21, 0x1FA1, module.Center + new WDir(-21f, 0f));
        var hiddenToad = Actor(22, 0x1FA1, module.Center);
        hiddenToad.IsTargetable = false;
        var pillar = Actor(23, 0x1FA0, module.Center + new WDir(0f, 10f));
        var noAir = PhaseHints();
        var pickupContext = new NavigationDecision.Context();
        var toadRoute = NavigationDecision.Build(pickupContext, Now, noAir, player, forbiddenZoneCushion: 0.5f);
        var attack = pickupContext.ThetaStar.CellCenter(pickupContext.ThetaStar.BestIndex());
        Check(noAir.PotentialTargets[0].Priority > noAir.PotentialTargets.Single(enemy => enemy.Actor == pillar).Priority
            && noAir.PotentialTargets.Single(enemy => enemy.Actor == hiddenToad).Priority == AIHints.Enemy.PriorityUndesirable,
            "urgent recovery prioritizes eligible air sources without choosing a hidden toad or another add");
        Check(toadRoute.Destination != null && attack.InCircle(reachableToad.Position, 3f + reachableToad.HitboxRadius)
            && noAir.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(attack)),
            "native navigation can reach the farther air source when a lane blocks the nearest one");
        player.PosRot = attack.ToVec4();
        player.TargetID = nearToad.InstanceID;
        Check(PhaseHints().PriorityTargetsSpan[0].Actor == reachableToad,
            "once in range, the reachable toad takes precedence over the previous target");
        player.PosRot = module.Center.ToVec4();
        player.TargetID = 0;
        var first = Actor(2, 0x1FA3, player.Position);
        var second = Actor(3, 0x1FA3, player.Position);
        froth.OnActorCreated(first);
        froth.OnActorCreated(first);
        froth.OnActorCreated(second);
        Check(froth.ActiveAOEs(0, player).Length == 2, "air bubbles are keyed by actor identity and duplicate spawn is idempotent");
        var withAir = PhaseHints();
        Check(withAir.PotentialTargets[0].Actor == pillar && withAir.GoalZones.Single()(player.Position) == 8f,
            "a live bubble replaces the urgent toad objective with direct air recovery");
        froth.OnActorDestroyed(first);
        var aoes = froth.ActiveAOEs(0, player);
        Check(aoes.Length == 1 && aoes[0].ActorID == second.InstanceID, "consuming one air bubble cannot clear a different bubble at the same point");

        froth.OnActorDestroyed(second);
        var snowpierce = Component<BaitAwayChargeCast>(module, prefix + "Snowpierce");
        var bubble = Actor(4, 0x1FA3, module.Center + new WDir(10, 0));
        froth.OnActorCreated(bubble);
        AddPlayer(5, bubble.Position);
        var otherTarget = AddPlayer(6, module.Center + new WDir(20, 10));
        var icicle = Actor(7, 0x1FA2, module.Center - new WDir(20, 0));
        var otherIcicle = Actor(8, 0x1FA2, icicle.Position + new WDir(0, 10));
        var cast = Cast(9792, player.Position);
        cast.TargetID = player.InstanceID;
        var otherCast = Cast(9792, otherTarget.Position);
        otherCast.TargetID = otherTarget.InstanceID;
        snowpierce.OnCastStarted(icicle, cast);
        snowpierce.OnCastStarted(otherIcicle, otherCast);
        player.Statuses[0] = new(1429, 6, default, 0);
        var hints = Hints();
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones[0].shapeDistance.Contains(otherTarget.Position)
            && !hints.ForbiddenZones.Any(zone => zone.shapeDistance.Contains(bubble.Position))
            && hints.PredictedDamage.Any(damage => damage.Players[0]),
            "air-first guidance keeps other charge lanes and damage predictions without blocking the required bubble");
        var context = new NavigationDecision.Context();
        var route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        var endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && route.LeewaySeconds >= 0f && endpoint.InCircle(bubble.Position, 1.4f),
            "native navigation can actually reach the air bubble before the estimated deadline during Snowpierce");

        bubble.HitboxRadius = 0.7f;
        var resized = froth.ActiveAOEs(0, player)[0];
        hints = Hints();
        Check(resized.Shape is AOEShapeCircle { Radius: 0.7f }
            && hints.ForbiddenZones[^1].shapeDistance.Contains(bubble.Position + new WDir(0.9f, 0)),
            "a shrinking bubble immediately updates both the radar and the recovery target");
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && route.LeewaySeconds >= 0f && endpoint.InCircle(bubble.Position, 0.7f),
            "the native path reaches the smaller bubble instead of stopping in its old footprint");
        bubble.HitboxRadius = 1.4f;

        world.Execute(new ActorState.OpStatus(player.InstanceID, 0, new(1429, 6, default, 0)));
        var originalDeadline = Hints().ForbiddenZones[^1].activation;
        world.Frame = new(Now.AddSeconds(2), 0, 0, 0, 0, 2);
        world.Execute(new ActorState.OpStatus(player.InstanceID, 0, new(1429, 6, Now.AddSeconds(30), 0)));
        Check(Hints().ForbiddenZones[^1].activation == originalDeadline && originalDeadline == Now.AddSeconds(4),
            "a refreshed Breathless observation cannot indefinitely defer the recovery deadline");
        world.Execute(new ActorState.OpStatus(player.InstanceID, 0, default));
        world.Execute(new ActorState.OpStatus(player.InstanceID, 0, new(1429, 5, default, 0)));
        Check(Hints().ForbiddenZones[^1].activation == Now.AddSeconds(7), "successful recovery clears the previous Breathless deadline");
        world.Execute(new ActorState.OpStatus(player.InstanceID, 0, default));
        world.Frame = new(Now, 0, 0, 0, 0, 1);

        player.Statuses[0] = default;
        hints = Hints();
        var bubbleProtection = hints.ForbiddenZones[^1];
        Check(hints.ForbiddenZones.Count == 4
            && bubbleProtection.shapeDistance.Contains(bubble.Position) && !bubbleProtection.shapeDistance.Contains(player.Position),
            "after breathing, the AI protects the bubble while leaving a short charge on the same line legal");
        player.PosRot.X = bubble.Position.X;
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && route.LeewaySeconds >= 0f && module.Arena.InBounds(endpoint)
            && hints.ForbiddenZones.All(zone => !zone.shapeDistance.Contains(endpoint)),
            "after the stack clears at the bubble, native navigation reaches a legal charge aim");
        player.PosRot.X = module.Center.X;

        snowpierce.OnCastFinished(icicle, cast);
        cast.TotalTime = 1.6f;
        snowpierce.OnCastStarted(icicle, cast);
        player.Statuses[0] = new(1429, 5, default, 0);
        Check(Hints().ForbiddenZones[^1].shapeDistance.Contains(bubble.Position),
            "an imminent charge baits first when the estimated air window leaves time to reach the bubble afterward");
        player.Statuses[0] = new(1429, 9, default, 0);
        Check(!Hints().ForbiddenZones.Any(zone => zone.shapeDistance.Contains(bubble.Position)),
            "near-lethal Breathless keeps the air destination available even when Snowpierce is imminent");
        snowpierce.OnActorDestroyed(icicle);
        Check(Hints().ForbiddenZones.Count == 2, "an interrupted icicle removes its bait while leaving air recovery and the other line active");

        var alternate = Actor(9, 0x1FA3, module.Center + new WDir(10, -7));
        froth.OnActorCreated(alternate);
        snowpierce.OnCastFinished(otherIcicle, otherCast);
        otherIcicle.PosRot.Z = otherTarget.PosRot.Z = bubble.Position.Z;
        otherCast.ElapsedTime = 8.4f;
        snowpierce.OnCastStarted(otherIcicle, otherCast);
        player.Statuses[0] = new(1429, 6, default, 0);
        hints = Hints();
        hints.Normalize();
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(route.Destination != null && route.LeewaySeconds >= 0f && endpoint.InCircle(alternate.Position, 1f),
            "the native AI can reach another air bubble when the earlier charge blocks the first");

        cast.TotalTime = 1.9f;
        snowpierce.OnCastStarted(icicle, cast);
        player.Statuses[0] = new(1429, 5, default, 0);
        hints = Hints();
        Check(hints.GoalZones.Single()(alternate.Position) == 8f && hints.GoalZones[0](bubble.Position) == 0f
            && hints.ForbiddenZones[^1].shapeDistance.Contains(bubble.Position),
            "an earlier competing charge removes the near bubble from the timing decision and the AI gets air first at the alternate");
        snowpierce.OnCastFinished(icicle, cast);
        froth.OnActorDestroyed(alternate);

        player.Statuses[0] = default;
        var newIcicle = Actor(10, 0x1FA2, icicle.Position);
        var unavoidableBubble = Actor(11, 0x1FA3, newIcicle.Position);
        froth.OnActorCreated(unavoidableBubble);
        cast.TotalTime = 10f;
        snowpierce.OnCastStarted(newIcicle, cast);
        hints = Hints();
        Check(hints.ForbiddenZones[^1].shapeDistance.Contains(bubble.Position)
            && !hints.ForbiddenZones[^1].shapeDistance.Contains(player.Position),
            "an unavoidable source bubble cannot block all movement or stop the AI from protecting another bubble");
        froth.OnActorDestroyed(bubble);
        Check(Hints().ForbiddenZones.Count == hints.ForbiddenZones.Count - 1,
            "a source bubble by itself publishes no impossible aiming constraint");

        snowpierce.OnCastFinished(otherIcicle, otherCast);
        snowpierce.OnCastFinished(newIcicle, cast);
        newIcicle.PosRot = (module.Center + new WDir(-6f, 0)).ToVec4();
        unavoidableBubble.PosRot = newIcicle.PosRot;
        cast.TotalTime = 1.9f;
        snowpierce.OnCastStarted(newIcicle, cast);
        player.Statuses[0] = new(1429, 5, default, 0);
        hints = Hints();
        route = NavigationDecision.Build(context, Now, hints, player, forbiddenZoneCushion: 0.5f);
        endpoint = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
        Check(hints.ForbiddenZones.Count == 1 && hints.GoalZones.Single()(unavoidableBubble.Position) == 8f
            && route.Destination != null && endpoint.InCircle(unavoidableBubble.Position, 1f)
            && route.TimeToGoal + NavigationDecision.ActivationTimeCushion < module.CastFinishAt(cast).Subtract(Now).TotalSeconds,
            "native recovery reaches the only bubble before the marked player's own charge can destroy it");

        var survivor = Actor(12, 0x1FA3, module.Center + new WDir(0, 7f));
        froth.OnActorCreated(survivor);
        hints = Hints();
        Check(hints.GoalZones.Count == 0 && hints.ForbiddenZones[^1].shapeDistance.Contains(survivor.Position),
            "a different bubble that survives and remains reachable restores the option to aim first");

        Actor AddPlayer(ulong id, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, ActorType.Player, Class.WAR, 70,
                new(position.X, 0, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(id == 1 ? 0 : (int)id, new(0, id, false)));
            return world.Actors.Find(id)!;
        }

        AIHints Hints()
        {
            var result = new AIHints { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            snowpierce.AddAIHints(0, player, default, result);
            froth.AddAIHints(0, player, default, result);
            return result;
        }

        AIHints PhaseHints()
        {
            var result = new AIHints();
            foreach (var enemy in new[] { nearToad, reachableToad, hiddenToad, pillar })
                result.PotentialTargets.Add(new(enemy, AIHints.Enemy.PriorityUndesirable, false));
            result.AddForbiddenZone(new SDRect(module.Center + new WDir(8f, 0f), 0f.Degrees(), 30f, 30f, 1f), Now);
            module.CalculateAIHints(0, player, default, result);
            result.Normalize();
            return result;
        }
    }
}
