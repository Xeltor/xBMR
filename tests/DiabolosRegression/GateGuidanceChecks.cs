using BossMod;

static partial class Checks
{
    private static void GateGuidanceLifecycle()
    {
        var world = World();
        Actor CreateGate(ulong id, uint oid, bool targetable = true)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
                new(-350, 1, -445, 0), 3.2f, new(100, 100, 0, 100, 100), targetable, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        var gate = CreateGate(90, 6411, false); // recorded Diabolic Gate OID0x190B
        var player = Actor(80, 0, new(-350, -445), Class.SGE);
        Member(world, player, 0);
        using var module = Hollow(world);
        var component = Component<BossComponent>(module, "GateGuidance");
        BossComponent.GlobalHints Guidance()
        {
            BossComponent.GlobalHints hints = [];
            component.AddGlobalHints(player, hints);
            return hints;
        }
        Check(Guidance().Single().Contains("forming"),
            "bootstrap recovers an already present unopened Diabolic Gate from its earliest cue");
        component.OnActorCreated(gate);
        Check(Guidance().Count == 1, "duplicate gate creation cannot duplicate group-entry guidance");
        world.Execute(new ActorState.OpTargetable(gate.InstanceID, true));
        Check(Guidance().Single().Contains("Shadow Dahak"),
            "native targetability supplies the manual group-entry instruction without choosing entrants");
        var flow = new ActorCastInfo
        {
            Action = new(ActionType.Spell, 7189), TotalTime = 27.7f, ElapsedTime = 0.0157911f,
            Location = new(-350, 1, -445)
        };
        world.Execute(new ActorState.OpCastInfo(gate.InstanceID, flow));
        Check(Guidance().Count == 2 && Guidance().Any(h => h.Contains("Deep Flow") && h.Contains("28.0s")),
            "actual Deep Flow reports native remaining time including NPC finish delay");
        gate.CastInfo!.ElapsedTime = 8;
        Check(Guidance().Any(h => h.Contains("20.0s")),
            "Deep Flow urgency follows the live cast instead of a timer from gate creation");
        var ai = Hints(module, component, 0, player);
        Check(ai.ForbiddenZones.Count == 0 && ai.GoalZones.Count == 0 && ai.PredictedDamage.Count == 0
            && ai.ForcedMovement == null && ai.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal,
            "manual gate response introduces no guessed footprint, damage deadline, forced movement or entrant assignment");
        world.Execute(new ActorState.OpCastInfo(gate.InstanceID, null));
        Check(Guidance().Count == 1 && Guidance().All(h => !h.Contains("Deep Flow")),
            "native cast cancellation clears only cast urgency while the portal still exists");
        var replacement = CreateGate(gate.InstanceID, 6411);
        component.OnActorDestroyed(gate);
        Check(Guidance().Count == 1 && Guidance().Single().Contains("Shadow Dahak"),
            "late destruction of the old generation cannot hide the replacement gate");
        world.Execute(new ActorState.OpDestroy(replacement.InstanceID));
        Check(Guidance().Count == 0, "native current-generation destruction retires gate guidance immediately");

        var deathgate = CreateGate(91, 6410); // recorded Deathgate OID0x190A
        Check(Guidance().Count == 0, "ordinary Deathgate creation does not imply portal entry");
        world.Execute(new ActorState.OpCastInfo(deathgate.InstanceID, new()
        {
            Action = new(ActionType.Spell, 7188), TotalTime = 8.7f, Location = new(-350, 1, -445)
        }));
        Check(Guidance().Single().Contains("Void Call") && Guidance().Single().Contains("9.0s"),
            "observed Void Call gives summon urgency from its actual native cast");
        world.Execute(new ActorState.OpCastInfo(deathgate.InstanceID, null));
        Check(Guidance().Count == 0, "cancelled Void Call cannot retain a summon countdown");
        CreateGate(deathgate.InstanceID, 0x19A);
        component.OnActorDestroyed(deathgate);
        Check(Guidance().Count == 0, "replacement by an unrelated object clears old gate semantics");

        CreateGate(92, 6411);
        player.PosRot = new(350, -300, -445, 0);
        Check(Guidance().Count == 0, "main-floor gate instructions do not reach portal participants");
        Check(module.CalculateGlobalHints(player).SequenceEqual(["Portal arena: move manually; automatic movement is paused."]),
            "last PortalSafety component keeps its manual instruction through overlapping gate guidance");
        world.Execute(new ActorState.OpDestroy(92));
        using var fresh = Hollow(World());
        var reset = Component<BossComponent>(fresh, "GateGuidance");
        BossComponent.GlobalHints clean = [];
        player.PosRot = new(-350, 1, -445, 0);
        reset.AddGlobalHints(player, clean);
        Check(clean.Count == 0, "fresh module reset cannot retain the previous gate or cast countdown");

        // A module activated after the cast starts receives the current Actor and
        // CastInfo during normal component bootstrap; no fresh creation is needed.
        var bootstrapWorld = World();
        var existing = Actor(93, 6411, new(-350, -445));
        existing.CastInfo = new() { Action = new(ActionType.Spell, 7189), TotalTime = 27.7f, ElapsedTime = 18 };
        bootstrapWorld.Actors.Actors[existing.InstanceID] = existing;
        using var bootstrap = Hollow(bootstrapWorld);
        BossComponent.GlobalHints recovered = [];
        Component<BossComponent>(bootstrap, "GateGuidance").AddGlobalHints(player, recovered);
        Check(recovered.Any(h => h.Contains("Deep Flow") && h.Contains("10.0s")),
            "mid-cast bootstrap recovers current remaining urgency without restarting28s");
    }
}
