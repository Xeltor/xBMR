using BossMod;

static partial class Regression
{
    private const string Griaule = "Shadowbringers.Dungeon.D02DohnMheg.D022Griaule.";

    private static void GriauleTethers()
    {
        var module = Module(Griaule + "D022Griaule", default);
        var world = module.WorldState;
        var boss = Actor(1, 0x98E, default);
        boss.HitboxRadius = 3.18f;
        Set(module, "PrimaryActor", boss);
        var player = AddPlayer(2, 0, ActorType.Player, new(5, 0));
        var companion = AddPlayer(3, 1, ActorType.Buddy, new(6, 0));
        var otherPlayer = AddPlayer(4, 2, ActorType.Player, new(6, 0));
        var east = Actor(40, 0xEFB, new(20, 0));
        var south = Actor(30, 0xEFB, new(0, 20));
        var feeding = Component<BossComponent>(module, Griaule + "FeedingTime");
        feeding.OnActorCreated(east);
        feeding.OnActorCreated(south);
        Check(Hints(player).ForbiddenZones.Count == 0, "saplings without tethers do not restrict movement");
        feeding.OnTethered(south, new(84, boss.InstanceID));
        feeding.OnTethered(east, new(84, boss.InstanceID));
        var lane = Hints(player).ForbiddenZones.Single();
        Check(lane.Source == east.InstanceID && lane.activation == Now.AddSeconds(10.9)
            && lane.shapeDistance.Distance(new(8, 0)) > 0 && lane.shapeDistance.Distance(new(0, 8)) < 0,
            "player gets a timed corridor on the closest free beam despite reverse tether order");
        Check(Hints(otherPlayer).ForbiddenZones.Single().Source == south.InstanceID,
            "the next player gets a distinct beam while Trust members choose their own");
        feeding.OnUntethered(east, new(84, boss.InstanceID));
        feeding.OnTethered(east, new(84, companion.InstanceID));
        Check(Hints(player).ForbiddenZones.Single().Source == south.InstanceID
            && Hints(otherPlayer).ForbiddenZones.Count == 0, "a Trust intercept frees the player to move to the other beam");
        player.Statuses[0] = new(1153, 0, Now, 0);
        Check(Hints(player).ForbiddenZones.Count == 0, "Fodder does not request movement while the player is bound by Coiling Ivy");
        player.Statuses[0] = default;
        Check(Hints(player).ForbiddenZones.Single().Source == south.InstanceID,
            "the free beam is actionable immediately after the root breaks");
        feeding.OnUntethered(south, new(84, boss.InstanceID));
        feeding.OnTethered(south, new(84, player.InstanceID));
        Check(Hints(player).ForbiddenZones.Single().Source == south.InstanceID,
            "the player holds their intercepted beam until it resolves");
        feeding.OnEventCast(south, Effect(8899));
        feeding.OnTethered(south, new(84, boss.InstanceID));
        Check(Hints(player).ForbiddenZones.Count == 0, "the resolved sapling cannot pull the player back");
        feeding.OnActorDestroyed(east);
        var nextWave = Actor(30, 0xEFB, new(-20, 0));
        feeding.OnActorCreated(nextWave);
        feeding.OnTethered(nextWave, new(84, boss.InstanceID));
        Check(Hints(player).ForbiddenZones.Single().shapeDistance.Distance(new(-8, 0)) > 0,
            "a new wave with a reused ID gets its own lane");
        feeding.OnActorDestroyed(nextWave);
        Check(Hints(player).ForbiddenZones.Count == 0, "despawn removes the outstanding lane");

        Actor AddPlayer(ulong id, int slot, ActorType type, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, 0, 0, 0, "", 0, type, Class.None, 73,
                new(position.X, 0, position.Z, 0), 0.5f, new(100, 100, 0, 0, 0), true, true, 0, 0, 0));
            world.Execute(new PartyState.OpModify(slot, new(0, id, false)));
            return world.Party[slot]!;
        }

        AIHints Hints(Actor actor)
        {
            var hints = new AIHints();
            feeding.AddAIHints(world.Party.FindSlot(actor.InstanceID), actor, default, hints);
            return hints;
        }
    }

    private static void GriauleRoots()
    {
        var module = Module(Griaule + "D022Griaule", default);
        var player = Actor(1, 0, new(8, 0), true);
        var own = Actor(2, 0xF08, new(8.03f, 0));
        var other = Actor(3, 0xF08, new(0, 8));
        var dormant = Actor(4, 0xF08, new(8, 0));
        dormant.IsTargetable = false;
        var ivy = Component<BossComponent>(module, Griaule + "CoilingIvy");

        player.Statuses[0] = new(1153, 0, Now.AddSeconds(20), 0);
        Check(Priorities() == (2, 1, 0, true), "fettered players prioritize the nearby root over the other live root and dormant objects");
        player.Statuses[0] = default;
        Check(Priorities() == (1, 1, 0, false), "free players can help with any remaining root");
        own.IsDead = true;
        player.Statuses[0] = new(1153, 0, Now.AddSeconds(20), 0);
        Check(Priorities() == (0, 1, 0, false), "dead roots stop competing for targets immediately");

        (int, int, int, bool) Priorities()
        {
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(own, 0, false));
            hints.PotentialTargets.Add(new(other, 0, false));
            hints.PotentialTargets.Add(new(dormant, 0, false));
            ivy.AddAIHints(0, player, default, hints);
            var bound = player.FindStatus(1153) != null;
            Check(hints.AutomaticMovementPaused == bound && hints.ForbidDashes == bound && !hints.SpecialModeBlocksManualMovement,
                "only a live Fetter pauses automatic movement, with manual input preserved");
            return (hints.PotentialTargets[0].Priority, hints.PotentialTargets[1].Priority, hints.PotentialTargets[2].Priority,
                hints.PotentialTargets[0].ShouldBeTargeted);
        }
    }
}
