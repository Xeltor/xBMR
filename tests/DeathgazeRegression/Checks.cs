using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A31DeathgazeHollow;

static partial class Checks
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Actor(ulong id, uint oid, WPos pos, bool player = false)
        => new(id, oid, 0, 0, "", 0, player ? ActorType.Player : ActorType.Enemy, Class.None, 60,
            new Vector4(pos.X, 2.87f, pos.Z, 0), hitboxRadius: 0.5f, hpmp: new(100, 100, 0, 100, 100));
    private static ActorCastInfo Cast(uint action, WPos position, float duration = 8.7f, Angle rotation = default)
        => new() { Action = new(ActionType.Spell, action), Location = new(position.X, 2.87f, position.Z), TotalTime = duration, Rotation = rotation };
    private static ActorCastEvent Hit(uint action, ulong target = 0)
        => new(new(ActionType.Spell, action), target, 0, 0, default, 0, 0, default);
    private static T Component<T>(BossModule module, string name) where T : BossComponent
        => (T)module.Components.Single(c => c.GetType().Name == name);
    private static A31DeathgazeHollow Module()
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = Actor(100, (uint)OID.Boss, new(299.5f, 439.66f));
        var module = new A31DeathgazeHollow(world, boss);
        module.StateMachine.Start(Now);
        return module;
    }
    private static AIHints Hints(BossComponent component, Actor player)
    {
        var hints = new AIHints();
        component.AddAIHints(0, player, default, hints);
        return hints;
    }
    private static void Party(BossModule module, params Actor[] players)
    {
        foreach (var player in players)
            module.WorldState.Actors.Actors.Add(player.InstanceID, player);
        for (var i = 0; i < players.Length; ++i)
            module.WorldState.Execute(new PartyState.OpModify(i == 2 ? 16 : i, new(0, players[i].InstanceID, false)));
    }

    private static Actor Create(BossModule module, Actor actor)
    {
        module.WorldState.Execute(new ActorState.OpCreate(actor.InstanceID, actor.OID, actor.SpawnIndex, actor.LayoutID, actor.Name, actor.NameID,
            actor.Type, actor.Class, actor.Level, actor.PosRot, actor.HitboxRadius, actor.HPMP, actor.IsTargetable, actor.IsAlly, actor.OwnerID, actor.FateID, actor.Renderflags));
        return module.WorldState.Actors.Find(actor.InstanceID)!;
    }

    private static void Animate(BossModule module, Actor actor, ushort storedState, ushort immediateMask)
        => module.WorldState.Execute(new ActorState.OpEventObjectAnimation(actor.InstanceID, storedState, immediateMask));

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        RegistrationAndAOEs();
        Spreads();
        Pulls();
        HelperKnockback();
        IceAlignment();
        FloorStrikeLifecycle();
        DeckGeometry();
        ShipWallLifecycle();
        RadialKnockback();
        SpritePriorities();
        Console.WriteLine($"{assertions} Deathgaze assertions passed (synthetic callbacks; no physical or live gameplay proof).");
    }

    private static Actor Seal(BossModule module, ulong id, uint oid, WPos position)
    {
        var actor = Actor(id, oid, position);
        actor.Type = ActorType.EventObj;
        return Create(module, actor); // facing0 intentionally differs from the bound SGB rotation
    }

    private static void DeckGeometry()
    {
        using var module = Module();
        Check(module.Arena.InBounds(new(299, 410)), "recovered deck contains its interior");
        foreach (var point in new WPos[] { new(269.04f, 410), new(328.97f, 410), new(300, 395.04f), new(300, 424.29f) })
            Check(module.Arena.InBounds(point), "deck accepts independently measured inside edge probe " + point);
        foreach (var point in new WPos[] { new(269, 410), new(329.02f, 410), new(300, 394.98f), new(300, 424.35f), new(329.5f, 424.8f) })
            Check(!module.Arena.InBounds(point), "deck rejects lethal edge/old rectangular sliver " + point);
        var map = new BossMod.Pathfinding.Map();
        module.Arena.Bounds.PathfindMap(map, module.Center);
        var middle = map.WorldToGrid(new(299, 410));
        var oldSouthSliver = map.WorldToGrid(new(299, 424.8f));
        Check(map.InBounds(middle.x, middle.y) && map.PixelMaxG[map.GridToIndex(middle)] >= 0,
            "recovered deck bounds populate a walkable AI map interior");
        Check(!map.InBounds(oldSouthSliver.x, oldSouthSliver.y) || map.PixelMaxG[map.GridToIndex(oldSouthSliver)] < 0,
            "old rectangular south sliver is outside the pathfinding grid or impassable, not only hidden in the radar");
    }

    private static void ShipWallLifecycle()
    {
        using var module = Module();
        var player = Actor(1, 0, new(315, 409.6f), true);
        var walls = Component<BossComponent>(module, "ShipWalls");
        Check(Hints(walls, player).TemporaryObstacles.Count == 0, "unobserved purple seals do not create physical obstacles");
        var east = Seal(module, 10, 2002872, new(329.6231f, 409.6169f));
        var west = Seal(module, 11, 2007602, new(268.5849f, 409.653f));
        Check(Hints(walls, player).TemporaryObstacles.Count == 2, "native creation with EventState0 enables both exact purple seals");
        var obstacle = Hints(walls, player).TemporaryObstacles.Single(o => o.Contains(east.Position));
        Check(obstacle.Contains(east.Position + new WDir(0.74f, 14)) && !obstacle.Contains(east.Position + new WDir(0.77f, 14)),
            "seal uses authored rotated0.75y narrow axis despite actor facing0");
        Check(obstacle.Contains(east.Position + new WDir(0, 14.99f)) && !obstacle.Contains(east.Position + new WDir(0, 15.01f)),
            "seal uses authored15y long axis independently of actor hitbox");
        module.WorldState.Execute(new ActorState.OpEventState(east.InstanceID, 7));
        Check(Hints(walls, player).TemporaryObstacles.Count == 1, "native EventState7 withdraws the east stopping wall immediately");
        module.WorldState.Execute(new ActorState.OpEventState(west.InstanceID, 3));
        Check(Hints(walls, player).TemporaryObstacles.Count == 0, "unknown native wall state cannot advertise solid geometry");
        module.WorldState.Execute(new ActorState.OpEventState(east.InstanceID, 0));
        module.WorldState.Execute(new ActorState.OpRenderflags(east.InstanceID, 1));
        Check(Hints(walls, player).TemporaryObstacles.Count == 0, "hidden seal withdraws physical stopping geometry");
        module.WorldState.Execute(new ActorState.OpRenderflags(east.InstanceID, 0));
        module.StateMachine.Reset();
        module.StateMachine.Start(Now);
        walls = Component<BossComponent>(module, "ShipWalls");
        Check(Hints(walls, player).TemporaryObstacles.Count == 1, "reset derives walls from retained native state0/state3 rather than stale component history");
        module.WorldState.Execute(new ActorState.OpDestroy(east.InstanceID));
        Check(Hints(walls, player).TemporaryObstacles.Count == 0, "native seal destruction removes its physical obstacle");
        Seal(module, 12, 2002872, new(300, 410));
        Seal(module, 13, 2007603, new(329.6231f, 409.6169f));
        Check(Hints(walls, player).TemporaryObstacles.Count == 0, "wrong position and wrong raw object ID cannot become ship seals");
    }

    private static void RadialKnockback()
    {
        using var module = Module();
        var origin = new WPos(298.97f, 409.6f);
        var player = Actor(1, 0, new(315, 409.6f), true);
        Party(module, player);
        var helper = Create(module, Actor(43, (uint)OID.Helper, origin));
        var radial = Component<SimpleKnockbacks>(module, "VoidAeroIIIKnockback");
        var cast = Cast(7310, origin, 5.7f);
        module.WorldState.Execute(new ActorState.OpCastInfo(helper.InstanceID, cast));
        radial.OnCastStarted(helper, cast);
        Check(radial.Casters.Count == 1, "duplicate radial helper cue cannot double displacement or AI deadlines");
        Check(Hints(radial, player).ForbiddenZones.Count == 0, "missing seal state cannot invent a whole-arena restriction");
        var east = Seal(module, 10, 2002872, new(329.6231f, 409.6169f));
        var west = Seal(module, 11, 2007602, new(268.5849f, 409.653f));
        var zone = Hints(radial, player).ForbiddenZones.Single();
        Check(zone.activation == module.CastFinishAt(cast).AddSeconds(-0.3), "radial AI receives helper deadline and response margin");
        var eastLanding = radial.CalculateMovements(0, player).Single().to;
        Check(eastLanding.X > 328.85f && eastLanding.X < 328.90f && module.Arena.InBounds(eastLanding),
            "east radial knockback stops at measured near seal face inside the actual deck");
        Check(!zone.shapeDistance.Contains(player.Position), "east short-end seal supplies a usable safe arrival");
        player.PosRot = new(282, 2.87f, origin.Z, 0);
        var westLanding = radial.CalculateMovements(0, player).Single().to;
        Check(westLanding.X > 269.31f && westLanding.X < 269.36f && module.Arena.InBounds(westLanding)
            && !Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position),
            "west short-end seal supplies an independent safe arrival");
        foreach (var z in new[] { 400f, 418f })
        {
            player.PosRot = new(origin.X, 2.87f, z, 0);
            var movement = radial.CalculateMovements(0, player).Single();
            Check(Math.Abs((movement.to - movement.from).Length() - 20) < 0.01f && !module.Arena.InBounds(movement.to),
                "open long-side movement retains full20y travel instead of clamping to the deck edge " + z);
            Check(Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "open long-side arrival is forbidden " + z);
        }
        Check(Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(origin), "undefined radial direction at exact origin is conservative");
        player.PosRot = new(315, 2.87f, origin.Z, 0);
        var bolt = Component<SimpleAOEs>(module, "BoltOfDarkness3");
        var boltHelper = Actor(44, (uint)OID.Helper, new(319.48f, 394.6f));
        var overlappingBolt = Cast(7315, boltHelper.Position, 6.5f);
        bolt.OnCastStarted(boltHelper, overlappingBolt);
        Check(Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position)
            && radial.DestinationUnsafe(0, player, eastLanding), "Bolt resolving0.8s after radial knockback rejects its otherwise safe seal landing");
        Check(!Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(new(282, origin.Z)), "overlapping east Bolt preserves the west seal alternative");
        bolt.OnCastFinished(boltHelper, overlappingBolt);
        Check(!Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "cancelled Bolt restores the otherwise safe east landing");
        var laterBolt = Cast(7315, boltHelper.Position, 8.7f);
        bolt.OnCastStarted(boltHelper, laterBolt);
        Check(!Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "unrelated later Bolt does not invent an immediate arrival overlap");
        bolt.OnCastFinished(boltHelper, laterBolt);
        var tornado = Create(module, Actor(58, (uint)OID.FierceWind, eastLanding));
        Check(Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "persistent tornado at a seal rejects that arrival");
        module.WorldState.Execute(new ActorState.OpDead(tornado.InstanceID, true));
        var immunity = new ActorStatus((uint)BossMod.WAR.SID.ArmsLength, 0, Now.AddSeconds(20), player.InstanceID);
        module.WorldState.Execute(new ActorState.OpStatus(player.InstanceID, 0, immunity));
        var text = new BossComponent.TextHints();
        radial.AddHints(0, player, text);
        Check(Hints(radial, player).ForbiddenZones.Count == 0 && radial.CalculateMovements(0, player).Count == 0 && text.Count == 0,
            "native active immunity bypasses radial alignment, arrows and personal warning");
        module.WorldState.Execute(new ActorState.OpStatus(player.InstanceID, 0,
            new((uint)BossMod.WAR.SID.ArmsLength, 0, Now.AddSeconds(5), player.InstanceID)));
        Check(Hints(radial, player).ForbiddenZones.Count == 1, "immunity expiring before helper activation cannot suppress the arrival constraint");
        module.WorldState.Execute(new ActorState.OpStatus(player.InstanceID, 0, default));
        module.WorldState.Execute(new ActorState.OpEventState(east.InstanceID, 7));
        Check(Hints(radial, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "disabled east seal withdraws its safe radial alignment while west remains available");
        module.WorldState.Execute(new ActorState.OpEventState(west.InstanceID, 7));
        Check(Hints(radial, player).ForbiddenZones.Count == 0, "both inactive seals withdraw unsupported solid-wall AI guidance");
        module.WorldState.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(radial.Casters.Count == 0 && Hints(radial, player).ForbiddenZones.Count == 0, "native helper cancellation clears radial lifecycle");
        radial.OnCastStarted(helper, cast);
        module.WorldState.Execute(new ActorState.OpDestroy(helper.InstanceID));
        Check(radial.Casters.Count == 0, "native helper destruction clears even a cue without retained CastInfo");
    }

    private static void RegistrationAndAOEs()
    {
        using var module = Module();
        Check(module.Components.Count == 16, "all generated component factories activate at pull");
        var player = Actor(1, 0, new(299.5f, 410), true);
        var helper = Actor(43, (uint)OID.Helper, new(319.48f, 394.6f));
        var bolt = Component<SimpleAOEs>(module, "BoltOfDarkness3");
        var cast = Cast(7315, helper.Position, 6.5f);
        bolt.OnCastStarted(helper, cast);
        var zones = Hints(bolt, player).ForbiddenZones;
        Check(zones.Count == 1, "previously disabled Bolt of Darkness supplies AI hazard from helper cast start");
        Check(zones[0].activation == module.CastFinishAt(cast), "Bolt retains helper deadline");
        Check(zones[0].shapeDistance.Contains(new(319.48f, 410)), "east Bolt pattern forbids its recorded column");
        Check(!zones[0].shapeDistance.Contains(new(289, 410)), "opposite column remains usable");
        bolt.OnCastFinished(helper, cast);
        Check(Hints(bolt, player).ForbiddenZones.Count == 0, "cancelled/completed Bolt removes its hazard");
        var aero = Component<SimpleAOEs>(module, "VoidAeroIIIAOE");
        helper.PosRot = new(298.97f, 2.87f, 409.6f, 0);
        var aeroCast = Cast(7309, helper.Position, 5.7f);
        aero.OnCastStarted(helper, aeroCast);
        zones = Hints(aero, player).ForbiddenZones;
        Check(zones.Count == 1 && zones[0].shapeDistance.Contains(helper.Position), "Aero III centre damage is no longer omitted");
        Check(!zones[0].shapeDistance.Contains(helper.Position + new WDir(10.1f, 0)), "Aero III does not inflate the radius10 sheet dimension");
        aero.OnCastFinished(helper, aeroCast);
        Check(Hints(aero, player).ForbiddenZones.Count == 0, "Aero III cleanup removes damage hazard");
        var blizzard = Component<SimpleAOEs>(module, "VoidBlizzardIIIAOE");
        var blizzardCast = Cast(7306, helper.Position, 2.7f, 180.Degrees());
        blizzard.OnCastStarted(helper, blizzardCast);
        zones = Hints(blizzard, player).ForbiddenZones;
        var direction = blizzardCast.Rotation.ToDirection();
        Check(zones.Count == 1 && zones[0].shapeDistance.Contains(helper.Position + 40.49f * direction), "Blizzard fan includes the conventional40+0.5y range");
        Check(!zones[0].shapeDistance.Contains(helper.Position + 40.51f * direction), "Blizzard fan no longer forbids unsupported range40.5-60");
        blizzard.OnCastFinished(helper, blizzardCast);
        Check(Hints(blizzard, player).ForbiddenZones.Count == 0, "Blizzard cast cancellation clears its fan");
        var wind = Actor(58, (uint)OID.FierceWind, new(296.07f, 404.28f));
        module.WorldState.Actors.Actors.Add(wind.InstanceID, wind);
        var biting = Component<Voidzone>(module, "BitingWind");
        var obstacles = Hints(biting, player).TemporaryObstacles;
        Check(obstacles.Count == 1 && obstacles[0].Contains(wind.Position), "existing tornado is an AI obstacle");
        Check(!obstacles[0].Contains(wind.Position + new WDir(4.1f, 0)), "tornado uses sheet radius4 without extra geometry");
        wind.IsDead = true;
        Check(Hints(biting, player).TemporaryObstacles.Count == 0, "dead tornado does not leave stale obstacle");
    }

    private static void Spreads()
    {
        using var module = Module();
        var first = Actor(1, 0, new(300, 410), true);
        var second = Actor(2, 0, new(301, 410), true);
        var otherAlliance = Actor(3, 0, new(320, 410), true);
        Party(module, first, second, otherAlliance);
        var component = Component<UniformStackSpread>(module, "VoidAeroII");
        component.OnEventIcon(first, 70, first.InstanceID);
        component.OnEventIcon(otherAlliance, 70, otherAlliance.InstanceID);
        component.OnEventIcon(first, 70, first.InstanceID);
        Check(component.Spreads.Count == 2, "actor assignment retains other-alliance target and deduplicates icons");
        Check(component.Spreads.All(s => s.Activation == Now.AddSeconds(5.5)), "spread deadline starts at the earliest icon cue");
        var zones = Hints(component, second).ForbiddenZones;
        Check(zones.Any(z => z.shapeDistance.Contains(first.Position)), "unmarked nearby player receives an AI spread exclusion");
        zones = Hints(component, first).ForbiddenZones;
        Check(zones.Any(z => z.shapeDistance.Contains(second.Position)), "marked player receives AI instructions to spread from unmarked players");
        component.OnEventCast(module.PrimaryActor, Hit(7308, 999));
        Check(component.Spreads.Count == 2, "unmatched action does not erase an unrelated assignment");
        component.OnEventCast(module.PrimaryActor, Hit(7308, first.InstanceID));
        Check(component.Spreads.Count == 1 && component.Spreads[0].Target == otherAlliance, "each helper hit retires only its target");
        otherAlliance.IsDead = true;
        component.Update();
        Check(component.Spreads.Count == 0, "target death clears stale spread");
        otherAlliance.IsDead = false;
        component.OnEventIcon(otherAlliance, 70, otherAlliance.InstanceID);
        module.WorldState.Actors.Actors[otherAlliance.InstanceID] = Actor(otherAlliance.InstanceID, 0, new(310, 410), true);
        component.Update();
        Check(component.Spreads.Count == 0, "recreated actor does not inherit prior lifecycle assignment");
        component.OnEventIcon(first, 70, first.InstanceID);
        module.WorldState.Frame = new(Now.AddSeconds(7.6), 0, 0, 0, 0, 1);
        component.Update();
        Check(component.Spreads.Count == 0, "missing helper resolution cannot retain icon indefinitely");
        component.OnEventIcon(first, 70, first.InstanceID);
        Check(component.Spreads.Count == 1 && component.Spreads[0].Activation == Now.AddSeconds(13.1), "later repetition receives a fresh deadline after stale cleanup");
    }

    private static void Pulls()
    {
        using var module = Module();
        var player = Actor(1, 0, new(300, 410), true);
        Party(module, player);
        var helper = Actor(51, (uint)OID.Helper, new(299.5f, 409.6f));
        var component = Component<GenericKnockback>(module, "VoidDeathPull");
        var cast = Cast(7312, helper.Position);
        component.OnCastStarted(helper, cast);
        component.OnCastStarted(helper, cast);
        var sources = component.ActiveKnockbacks(0, player);
        Check(sources.Length == 2, "no-cast pulls become visible from lethal-helper cue and duplicate starts are idempotent");
        Check(sources[0].Distance == 40 && sources[1].Distance == 15, "pull distances match linked displacement rows57/64");
        Check(sources[0].Activation == module.CastFinishAt(cast).AddSeconds(-8), "first pull is predicted1s into9s cast");
        Check(sources[1].Activation == module.CastFinishAt(cast).AddSeconds(-3), "second pull is predicted6s into9s cast");
        var zones = Hints(component, player).ForbiddenZones;
        Check(zones.Count == 1 && zones[0].activation == sources[1].Activation.AddSeconds(-0.3), "AI receives second-pull arrival deadline");
        Check(zones[0].shapeDistance.Contains(helper.Position + new WDir(24.9f, 0)), "second pull cannot deposit player inside lethal centre");
        Check(!zones[0].shapeDistance.Contains(helper.Position + new WDir(25.1f, 0)), "supported escape space is not unnecessarily forbidden");
        var immunity = new ActorStatus((uint)BossMod.WAR.SID.ArmsLength, 0, Now.AddSeconds(20), player.InstanceID);
        component.OnStatusGain(player, ref immunity);
        Check(Hints(component, player).ForbiddenZones.Count == 0, "active knockback immunity preserves manual/AI uptime");
        component.OnStatusLose(player, ref immunity);
        component.OnEventCast(Actor(50, (uint)OID.Helper, helper.Position), Hit(7313));
        sources = component.ActiveKnockbacks(0, player);
        Check(sources.Length == 1 && sources[0].Distance == 15, "first action removes first pull only");
        component.OnEventCast(helper, Hit(7314));
        Check(component.ActiveKnockbacks(0, player).Length == 0 && Hints(component, player).ForbiddenZones.Count == 0, "second pull resolution clears preparation");
        component.OnCastStarted(helper, cast);
        component.OnCastFinished(helper, cast);
        Check(component.ActiveKnockbacks(0, player).Length == 0 && Hints(component, player).ForbiddenZones.Count == 0, "cast cancellation clears both predictions");
        component.OnCastStarted(helper, cast);
        component.OnActorDestroyed(helper);
        Check(component.ActiveKnockbacks(0, player).Length == 0 && Hints(component, player).ForbiddenZones.Count == 0, "helper destruction clears pull lifecycle");
    }

    private static void HelperKnockback()
    {
        using var module = Module();
        var player = Actor(1, 0, new(300, 410), true);
        Party(module, player);
        var helper = Actor(43, (uint)OID.Helper, new(299.5f, 394.6f));
        var component = Component<SimpleKnockbacks>(module, "VoidAeroIV");
        component.OnCastStarted(module.PrimaryActor, Cast(7292, module.PrimaryActor.Position, 2.2f));
        Check(component.Casters.Count == 0, "boss visual does not create an earlier unsafe expiry");
        foreach (var rotation in new[] { default(Angle), 180.Degrees() })
        {
            var cast = Cast(7311, helper.Position, 2.7f, rotation);
            component.OnCastStarted(helper, cast);
            var source = component.Casters.Single();
            Check(source.Kind == GenericKnockback.Kind.DirForward && source.Distance == 37, "helper uses source-forward37y displacement");
            Check(source.Activation == module.CastFinishAt(cast), "helper timing survives the earlier boss visual finish");
            component.OnCastFinished(module.PrimaryActor, Cast(7292, module.PrimaryActor.Position, 2.2f));
            Check(component.Casters.Count == 1, "unrelated boss cast finish does not remove helper");
            var movements = component.CalculateMovements(0, player);
            Check(movements.Count == 1 && Math.Abs((movements[0].to - movements[0].from).Length() - 37) < 0.01f, "open edge is not silently clamped as a solid wall");
            Check(Hints(component, player).ForbiddenZones.Count == 0, "missing ice state does not forbid the entire arena");
            var text = new BossComponent.TextHints();
            component.AddHints(0, player, text);
            Check(text.Any(t => t.Item1.Contains("ice block")), "ice alignment instruction remains actionable for manual response");
            component.OnCastFinished(helper, cast);
            Check(component.Casters.Count == 0, "helper cancellation/removal clears knockback");
        }
    }

    private static void FloorStrikeLifecycle()
    {
        using var module = Module();
        var player = Actor(1, 0, new(321.4979f, 417.1275f), true);
        Party(module, player);
        var floor = Component<GenericAOEs>(module, "FloorStrikes");
        // Exact raw create59: actor29/OID2007616, placed southern square at (321.4979,417.1275).
        var tile = Actor(29, 2007616, player.Position);
        tile.Type = ActorType.EventObj;
        tile.HitboxRadius = 2;
        tile.PosRot.W = -4.7922134e-05f; // native actor facing differs from the authored SGB half-turn
        tile = Create(module, tile);
        Check(tile.OID == 2007616 && tile.InstanceID == 29, "floor fixture pins exact decimal native tile identity");
        Animate(module, tile, 4, 8);
        Check(floor.ActiveAOEs(0, player).Length == 0, "prepared dark tile is not prematurely marked as a lethal strike");
        Animate(module, tile, 16, 32);
        var aoe = floor.ActiveAOEs(0, player)[0];
        Check(aoe.Activation == Now.AddSeconds(4.1) && aoe.ActorID == tile.InstanceID,
            "native16/32 supplies an early per-tile deadline with documented response margin");
        Check(aoe.Rotation == default, "square uses authored world axes, not native actor facing");
        Check(aoe.Check(tile.Position + new WDir(7.49f, 7.49f)) && !aoe.Check(tile.Position + new WDir(7.51f, 0)),
            "forecast preserves independently measured15x15 footprint");
        var zones = Hints(floor, player).ForbiddenZones;
        Check(zones.Count == 1 && zones[0].activation == aoe.Activation && zones[0].shapeDistance.Contains(player.Position),
            "early tile forecast reaches native AI timing and geometry");
        var personal = new BossComponent.TextHints();
        floor.AddHints(0, player, personal);
        Check(personal.Count != 0, "occupied flashing tile supplies actionable player warning");
        module.WorldState.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        Animate(module, tile, 16, 32);
        Check(floor.ActiveAOEs(0, player).Length == 1 && floor.ActiveAOEs(0, player)[0].Activation == aoe.Activation,
            "duplicate tile warning preserves first deadline and one forecast");
        floor.OnEventCast(module.PrimaryActor, Hit(7299));
        floor.OnCastFinished(module.PrimaryActor, Cast(7299, module.PrimaryActor.Position));
        Check(floor.ActiveAOEs(0, player).Length == 1, "earlier boss action/cast finish cannot resolve the later tile strike");
        Animate(module, tile, 4, 16);
        Check(floor.ActiveAOEs(0, player).Length == 0 && Hints(floor, player).ForbiddenZones.Count == 0,
            "same tile4/16 resolves one-shot forecast without retaining a guessed persistent collider zone");
        Animate(module, tile, 16, 32);
        Animate(module, tile, 1, 2);
        Check(floor.ActiveAOEs(0, player).Length == 0, "explicit tile reset clears pending strike");
        Animate(module, tile, 16, 32);
        Animate(module, tile, 0x1234, 0x5678);
        Check(floor.ActiveAOEs(0, player).Length == 0, "unknown floor state withdraws unsupported prediction");
        Animate(module, tile, 32, 16);
        Check(floor.ActiveAOEs(0, player).Length == 0, "reversed native packing cannot activate a tile forecast");
        var unrelated = Create(module, Actor(700, 2007608, tile.Position));
        Animate(module, unrelated, 16, 32);
        Check(floor.ActiveAOEs(0, player).Length == 0, "adjacent iceOID cannot enter strict floor range");
        Animate(module, tile, 16, 32);
        var old = tile;
        var replacementTile = Actor(tile.InstanceID, tile.OID, tile.Position);
        replacementTile.Type = ActorType.EventObj;
        tile = Create(module, replacementTile);
        Check(floor.ActiveAOEs(0, player).Length == 0, "new actor generation cannot inherit stale floor prediction");
        Animate(module, tile, 16, 32);
        floor.OnActorEAnim(old, 0x00040010);
        floor.OnActorDestroyed(old);
        Check(floor.ActiveAOEs(0, player).Length == 1, "old generation resolution/destruction cannot remove replacement prediction");
        module.WorldState.Frame = new(Now.AddSeconds(7.2), 0, 0, 0, 0, 1);
        floor.Update();
        var global = new BossComponent.GlobalHints();
        floor.AddGlobalHints(player, global);
        Check(floor.ActiveAOEs(0, player).Length == 0 && global.Count != 0,
            "missing resolution retires stale forecast with explicit lost-evidence warning, not a claimed safe expiry");
        Animate(module, tile, 1, 2);
        global = new();
        floor.AddGlobalHints(player, global);
        Check(global.Count == 0, "verified floor reset clears lost-evidence warning");
        Animate(module, tile, 16, 32);
        module.WorldState.Execute(new ActorState.OpDestroy(tile.InstanceID));
        Check(floor.ActiveAOEs(0, player).Length == 0, "native tile destruction clears prediction");

        // Both attempts use this same tile identity set; the first attempt reaches all eight before wiping.
        var all = new List<Actor>();
        for (var i = 0; i < 8; ++i)
        {
            var t = Actor((ulong)(800 + i), (uint)(2007609 + i), new(276.4979f + 15 * (i % 4), 402.1275f + 15 * (i / 4)));
            t.Type = ActorType.EventObj;
            all.Add(Create(module, t));
            Animate(module, all[^1], 16, 32);
        }
        Check(floor.ActiveAOEs(0, player).Length == 8 && Hints(floor, player).ForbiddenZones.Count == 8,
            "observed eight-tile wipe pattern retains every forecast rather than inventing a safe tile");
        foreach (var t in all)
            Check(floor.ActiveAOEs(0, player).ToArray().Any(a => a.Check(t.Position)), "each exact native floorOID has its own footprint");
        foreach (var t in all)
            Animate(module, t, 1, 2);
        Animate(module, all[7], 16, 32);
        Animate(module, all[2], 16, 32);
        Check(floor.ActiveAOEs(0, player).Length == 2, "post-wipe second pull starts fresh two-tile pattern");
        Animate(module, all[7], 4, 16);
        Check(floor.ActiveAOEs(0, player).Length == 1 && floor.ActiveAOEs(0, player)[0].ActorID == all[2].InstanceID,
            "one tile resolution cannot prematurely retire another tile's forecast");
    }

    private static void IceAlignment()
    {
        using var module = Module();
        var player = Actor(1, 0, new(305, 410), true);
        Party(module, player);
        // Exact raw native create seq63: actor33, EObj2007607, (305,2.87,420.6), hitbox2.
        var ice = Actor(33, 2007607, new(305, 420.6f));
        ice.Type = ActorType.EventObj;
        ice.HitboxRadius = 2;
        ice = Create(module, ice);
        Check(ice.InstanceID == 33 && ice.OID == 2007607 && ice.Type == ActorType.EventObj && ice.HitboxRadius == 2,
            "typed native ice fixture matches recorded create seq63, including decimal EObj row2007607");
        var blocks = Component<BossComponent>(module, "IceBlocks");
        var aero = Component<SimpleKnockbacks>(module, "VoidAeroIV");
        var helper = Actor(43, (uint)OID.Helper, new(299.5f, 394.6f));
        var cast = Cast(7311, helper.Position, 2.7f);
        aero.OnCastStarted(helper, cast);
        aero.OnCastStarted(helper, cast);
        Check(aero.Casters.Count == 1, "duplicate helper start does not duplicate displacement or AI deadlines");
        Animate(module, ice, 4, 8);
        Animate(module, ice, 4, 8);
        var obstacles = Hints(blocks, player).TemporaryObstacles;
        Check(obstacles.Count == 1, "duplicate ice activation retains one physical obstacle");
        Check(obstacles[0].Contains(new(308.99f, 420.6f)) && !obstacles[0].Contains(new(309.01f, 420.6f)), "ice collider uses asset4y half-width, independent of actor hitbox");
        var sources = aero.ActiveKnockbacks(0, player);
        Check(sources.Length == 1 && sources[0].SafeWalls.Length == 4, "active collision box supplies four physical faces");
        var movement = aero.CalculateMovements(0, player).Single();
        Check(Math.Abs(movement.to.Z - 416.599f) < 0.01f, "north wind stops at the near ice face rather than the open edge");
        var zones = Hints(aero, player).ForbiddenZones;
        Check(zones.Count == 1 && zones[0].activation == module.CastFinishAt(cast).AddSeconds(-0.3), "known ice supplies helper-timed arrival constraint with response margin");
        Check(!zones[0].shapeDistance.Contains(player.Position), "ray through known block is a valid alignment");
        Check(!zones[0].shapeDistance.Contains(new(308.49f, 410)) && zones[0].shapeDistance.Contains(new(308.51f, 410)), "AI retains player-radius corner margin without changing physical dimensions");
        Check(zones[0].shapeDistance.Contains(new(310, 410)), "ray beside block cannot be advertised as safe");
        Check(obstacles[0].Contains(new(305, 423)), "z423 is inside the measured ice box, not beyond it");
        var nearFace = sources[0].SafeWalls.Single(w => Math.Abs(w.Vertex1.Z - 416.6f) < 0.01f && Math.Abs(w.Vertex2.Z - 416.6f) < 0.01f);
        Check(Intersect.RaySegment(new WPos(305, 410), new WDir(0, 1), nearFace.Vertex1, nearFace.Vertex2) < 37, "forward ray intersects near face before displacement ends");
        Check(Intersect.RaySegment(new WPos(305, 424.9f), new WDir(0, 1), nearFace.Vertex1, nearFace.Vertex2) == float.MaxValue, "negative ray intersection is rejected");
        Check(zones[0].shapeDistance.Contains(new(305, 424.9f)), "block entirely behind player cannot stop forward knockback");
        // The enemy cache was already queried above. Native creation must update that existing cache.
        var tornado = Create(module, Actor(58, (uint)OID.FierceWind, new(305, 415)));
        Check(Hints(aero, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "known persistent tornado makes aligned landing unsafe");
        tornado.IsDead = true;
        var immunity = new ActorStatus((uint)BossMod.WAR.SID.ArmsLength, 0, Now.AddSeconds(20), player.InstanceID);
        aero.OnStatusGain(player, ref immunity);
        Check(Hints(aero, player).ForbiddenZones.Count == 0, "immunity bypasses ice alignment arrival restriction");
        aero.OnStatusLose(player, ref immunity);
        Animate(module, ice, 1, 2);
        Check(Hints(blocks, player).TemporaryObstacles.Count == 0 && Hints(aero, player).ForbiddenZones.Count == 0, "ice disable clears obstacle and alignment together");
        Animate(module, ice, 4, 8);
        var replacement = Create(module, Actor(ice.InstanceID, ice.OID, ice.Position));
        blocks.Update();
        Check(Hints(blocks, player).TemporaryObstacles.Count == 0, "recreated ice actor does not inherit old enabled collider");
        Animate(module, replacement, 4, 8);
        blocks.OnActorDestroyed(ice);
        Check(Hints(blocks, player).TemporaryObstacles.Count == 1, "old generation destruction cannot remove newly activated replacement ice");
        Animate(module, replacement, 0x1234, 0x5678);
        Check(Hints(blocks, player).TemporaryObstacles.Count == 0 && Hints(aero, player).ForbiddenZones.Count == 0, "unknown ice state withdraws unproved stopping geometry");
        ice = Create(module, Actor(ice.InstanceID, ice.OID, ice.Position));
        Animate(module, ice, 4, 8);
        Animate(module, ice, 8, 4);
        Check(Hints(blocks, player).TemporaryObstacles.Count == 0, "swapped native animation parameters cannot enable collision geometry");
        Animate(module, ice, 4, 8);
        module.WorldState.Execute(new ActorState.OpDestroy(ice.InstanceID));
        Check(Hints(blocks, player).TemporaryObstacles.Count == 0, "destroyed ice removes collision faces");
        aero.OnCastFinished(helper, cast);

        // Opposite wind direction uses the same asset dimensions and state lifecycle.
        // Exact opposite-side raw native create seq57: actor27, EObj2007605, (319,2.87,398.6), hitbox2.
        ice = Actor(27, 2007605, new(319, 398.6f));
        ice.Type = ActorType.EventObj;
        ice.HitboxRadius = 2;
        ice = Create(module, ice);
        Check(ice.InstanceID == 27 && ice.OID == 2007605 && ice.Type == ActorType.EventObj && ice.HitboxRadius == 2,
            "opposite-side typed fixture matches recorded create seq57");
        Animate(module, ice, 4, 8);
        player.PosRot = new(319, 2.87f, 410, 0);
        var south = Cast(7311, new(299.5f, 424.6f), 2.7f, 180.Degrees());
        aero.OnCastStarted(helper, south);
        movement = aero.CalculateMovements(0, player).Single();
        Check(Math.Abs(movement.to.Z - 402.601f) < 0.01f, "south wind stops at opposite near ice face");
        Check(!Hints(aero, player).ForbiddenZones.Single().shapeDistance.Contains(player.Position), "single-block opposite pattern retains safe alignment");
        aero.OnCastFinished(helper, south);
        Check(Hints(aero, player).ForbiddenZones.Count == 0, "cancelled helper removes alignment deadline while ice remains physical");
        aero.OnCastStarted(helper, south);
        aero.OnActorDestroyed(helper);
        Check(aero.Casters.Count == 0 && Hints(aero, player).ForbiddenZones.Count == 0, "destroyed helper cannot retain a stale alignment deadline");
    }
}
