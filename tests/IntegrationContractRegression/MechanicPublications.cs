using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class MechanicsModule(WorldState world, Actor primary) : BossModule(world, primary, default, new ArenaBoundsSquare(10));
    private sealed class PublishedAOEs(BossModule module, GenericAOEs.AOEInstance[] aoes) : GenericAOEs(module)
    {
        public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => aoes;
    }
    private sealed class PublishedStacks(BossModule module) : GenericStackSpread(module, includeDeadTargets: true);
    private sealed class PublishedKnockback(BossModule module) : GenericKnockback(module)
    {
        internal Knockback[] Sources = [];
        public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => Sources;
        public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => pos.X > 2;
        internal void SetImmune(DateTime expiration) => PlayerImmunes[0].RoleBuffExpire = expiration;
    }

    private static void MechanicPublications()
    {
        var described = IntegrationMechanics.Describe(new AOEShapeCone(10, 0.4f.Radians(), 0.2f.Radians(), true));
        Check(described.Status == "known" && described.Kind == "cone" && described.Inverted && described.DirectionOffset == 0.2f,
            "native shape transport retains inversion, direction offset and parameter meanings");
        Check(IntegrationMechanics.Describe(new AOEShapeCircle(float.NaN)).Status == "unknown", "invalid shape parameters cannot become safe geometry");
        Check(IntegrationMechanics.Describe(new AOEShapeTriCone(10, 0.5f.Radians())).Status == "unknown", "unaudited shape stays explicitly unknown");

        // Construct only the native geometry/world needed for generic publications: no game assets,
        // replay, plugin update or live client is involved in this fixture.
        Service.Config._nodes.TryAdd(typeof(BossModuleConfig), new BossModuleConfig());
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(new ActorState()));
        Actor MakeActor(ulong id, bool isDead)
        {
            var value = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            typeof(Actor).GetField(nameof(Actor.InstanceID))!.SetValue(value, id);
            value.IsDead = isDead;
            value.PosRot = new((float)id - 1, 0, 0, 0);
            return value;
        }
        var actor = MakeActor(1, false);
        var dead = MakeActor(2, true);
        world.Party.Members[0] = new(1, 1, false);
        world.Party.Members[1] = new(2, 2, false);
        var actors = (Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!;
        actors[0] = actor; actors[1] = dead;
        var module = (MechanicsModule)RuntimeHelpers.GetUninitializedObject(typeof(MechanicsModule));
        typeof(BossModule).GetField(nameof(BossModule.WorldState))!.SetValue(module, world);
        typeof(BossModule).GetField(nameof(BossModule.Arena))!.SetValue(module, new MiniArena(default, new ArenaBoundsSquare(10)));
        typeof(BossModule).GetField(nameof(BossModule.Components))!.SetValue(module, new List<BossComponent>());
        typeof(BossModule).GetField("_actorArenaProjectionLayers", InternalInstance)!.SetValue(module, new Dictionary<ulong, int>());
        var active = new PublishedAOEs(module, [new(new AOEShapeCircle(3), default, activation: world.FutureTime(4), actorID: 42)]);
        var stack = new PublishedStacks(module);
        stack.Stacks.Add(new(actor, 3, 2, 4, world.FutureTime(5), new(2)));
        module.Components.Add(active); module.Components.Add(stack); module.Components.Add(new BossComponent(module));
        var context = new IntegrationSession.Context(1, "generation", 5, "hints-ready");
        using var json = JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context, new(clock: () => 0, frequency: 1000))));
        var root = json.RootElement;
        Check(root.GetProperty("publishedEntriesComplete").GetBoolean() && !root.GetProperty("encounterForecastComplete").GetBoolean()
            && !root.GetProperty("futurePulsesComplete").GetBoolean(), "complete current generic publication never claims whole encounter/future coverage");
        var entries = root.GetProperty("entries");
        Check(entries[0].GetProperty("sourceActorId").GetString() == "2A" && entries[0].GetProperty("futurePulseCount").ValueKind == JsonValueKind.Null,
            "source actor evidence is retained and unknown later pulses remain unknown");
        Check(entries[1].GetProperty("excludedActorIds")[0].GetString() == "2", "same-world actor mapping includes dead excluded stack recipients");
        Check(!root.GetProperty("components")[2].GetProperty("genericPublicationSupported").GetBoolean(), "unknown component remains explicit coverage gap");
        Check(IntegrationMechanics.ComponentId(active) == IntegrationMechanics.ComponentId(active)
            && IntegrationMechanics.ComponentId(active) != IntegrationMechanics.ComponentId(stack), "component sequence identity lasts only for its native object lifetime");
        var knockback = new PublishedKnockback(module)
        {
            Sources = [new(new(-1, 0), 4, world.FutureTime(4)), new(new(-1, 0), 2, world.FutureTime(5))]
        };
        actor.PosRot = new(0, 0, 0, 0);
        var projection = IntegrationMechanics.ObserveCurrentProjection(knockback, actor, 0)!;
        Check(projection.Length == 2 && projection[0].To[0] == 4 && projection[1].From[0] == 4 && projection[1].To[0] == 6,
            "native projection retains ordered successive movements");
        Check(projection[0].NativeDestinationUnsafe && projection[0].ActivationTicks == world.FutureTime(4).Ticks.ToString()
            && projection[1].ActivationTicks == world.FutureTime(5).Ticks.ToString() && projection[1].SourceIndex == 1,
            "component-specific unsafe behavior and native source timing are retained through ordered projections");
        Check(actor.Position == default && actor.Rotation == default, "projection never mutates the native actor pose");
        knockback.SetImmune(world.FutureTime(6));
        Check(IntegrationMechanics.ObserveCurrentProjection(knockback, actor, 0)!.Length == 0,
            "actual native immunity filters the sequence without cloning component state");
        knockback.SetImmune(world.FutureTime(4.5));
        var filtered = IntegrationMechanics.ObserveCurrentProjection(knockback, actor, 0)!;
        Check(filtered.Length == 1 && filtered[0].SourceIndex == 1 && filtered[0].ActivationTicks == world.FutureTime(5).Ticks.ToString()
            && filtered[0].From[0] == 0 && filtered[0].To[0] == 2,
            "immunity-filtered sequence retains the actual surviving source index and activation rather than zipping unfiltered timestamps");
        var nativeLegacy = knockback.CalculateMovements(0, actor);
        Check(nativeLegacy.Count == filtered.Length && nativeLegacy[0].to.X == filtered[0].To[0],
            "timed projection and existing native hints use the same calculation path");
        var march = new GenericForcedMarch(module);
        march.AddForcedMovement(actor, 0f.Radians(), 1, world.FutureTime(2));
        march.AddForcedMovement(actor, 90f.Degrees(), 2, world.FutureTime(5));
        var marchProjection = IntegrationMechanics.ObserveCurrentProjection(march, actor, 0)!;
        Check(marchProjection.Length == 2 && marchProjection[0].ActivationTicks == world.FutureTime(2).Ticks.ToString()
            && marchProjection[0].EndTicks == world.FutureTime(3).Ticks.ToString() && marchProjection[1].SourceIndex == 1
            && marchProjection[1].EndTicks == world.FutureTime(7).Ticks.ToString(),
            "native pending march projections preserve activation/duration and ordered pending identity");
        march.ActivateForcedMovement(actor, world.FutureTime(1));
        var marching = IntegrationMechanics.ObserveCurrentProjection(march, actor, 0)!;
        Check(marching.Length == 3 && marching[0].SourceIndex == null && marching[0].ActivationTicks == null
            && marching[0].EndTicks == world.FutureTime(1).Ticks.ToString(),
            "an already active march reports the remaining segment from now, separately from pending status activations");
        var legacyMarch = march.ForcedMovements(actor);
        Check(legacyMarch.Count == marching.Length && legacyMarch[^1].to.Z == marching[^1].To[1],
            "march timing metadata cannot diverge from native geometric projection");
        knockback.SetImmune(default);
        knockback.Sources = [new(new(-1, 0), 1)];
        Check(IntegrationMechanics.ObserveCurrentProjection(knockback, actor, 0)![0].ActivationTicks == null,
            "an unscheduled native source cannot acquire an invented activation clock");
        Check(IntegrationMechanics.ObserveCurrentProjection(active, actor, 0) == null
            && IntegrationMechanics.ObserveCurrentProjection(knockback, actor, -1) == null, "unsupported components and missing actor slots remain unavailable");
        module.Components.Clear();
        module.Components.Add(new PublishedAOEs(module, Enumerable.Range(0, 200).Select(_ => new GenericAOEs.AOEInstance(new AOEShapeCircle(1), default)).ToArray()));
        using var bounded = JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context, new(clock: () => 0, frequency: 1000))));
        Check(bounded.RootElement.GetProperty("entries").GetArrayLength() == 128 && !bounded.RootElement.GetProperty("publishedEntriesComplete").GetBoolean(),
            "truncated publication is bounded and cannot be interpreted as complete");
        using var exhausted = JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context,
            new(clock: () => 0, frequency: 1000, evaluations: 0))));
        Check(exhausted.RootElement.GetProperty("entries").GetArrayLength() == 0
            && !exhausted.RootElement.GetProperty("publishedEntriesComplete").GetBoolean(), "exhausted work budget cannot produce a complete empty forecast");
    }
}
