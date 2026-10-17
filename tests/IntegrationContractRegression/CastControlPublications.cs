using BossMod;
using BossMod.Components;
using BossMod.Integration;
using IntegrationExample;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class FailedCastDistance(bool invalidNumber) : ShapeDistance
    {
        public override float Distance(in WPos p) => invalidNumber ? float.NaN : throw new InvalidOperationException("fixture");
    }

    private static void CastControlPublications()
    {
        var native = typeof(AIHints).Assembly;
        var capture = native.GetType("BossMod.Integration.IntegrationCastControl", true)!.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)!;
        var contextType = native.GetType("BossMod.Integration.IntegrationSession+Context", true)!;
        var context = Activator.CreateInstance(contextType, [1, "cast-fixture", 12L, "hints-ready"])!;
        var now = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        var hints = new AIHints();
        var caster = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        caster.InstanceID = 0xABC; caster.IsTargetable = true;
        caster.PendingHPDifferences = []; caster.HPMP = new(1000, 1000, 0, 0, 0);
        ActorCastInfo Cast() => new() { Action = new(ActionType.Spell, 500), TargetID = 0x123, TotalTime = 5, ElapsedTime = 1, Interruptible = true };
        caster.CastInfo = Cast();
        hints.PotentialTargets.Add(new(caster, 0, false) { ShouldBeInterrupted = true, ShouldBeStunned = true });
        JsonDocument Read(WPos[]? points = null, Func<long>? clock = null) => JsonDocument.Parse(IntegrationSession.Serialize(
            capture.Invoke(null, [hints, now, points ?? [default, new WPos(20, 0)], context, clock ?? (() => 0L), 1000L])!));
        var distance = new SDCircle(default, 3);
        var activation = now.AddSeconds(4.3);
        hints.AddForbiddenZoneFromCast(distance, activation, caster.InstanceID, caster.CastInfo);
        hints.AddForbiddenZone(distance, activation); // Identical timing/shape is not a cast link.
        hints.AddForbiddenZone(distance, activation, caster.InstanceID); // Same caster is not a cast link.
        using var first = Read();
        var root = first.RootElement;
        var cast = root.GetProperty("targets")[0].GetProperty("cast");
        var castId = cast.GetProperty("castId").GetString();
        Check(castId != null && root.GetProperty("zones")[0].GetProperty("castId").GetString() == castId,
            "a producer-recorded zone and current cast share their actual native cast identity");
        // A tuple copied with exactly the same shape object/source/activation retains the original
        // producer's evidence. Test uncorrelated cases with distinct native shape instances below.
        Check(cast.GetProperty("remainingSeconds").GetSingle() == caster.CastInfo.NPCRemainingTime
            && cast.GetProperty("reportedRemainingSeconds").GetSingle() == 4
            && cast.GetProperty("deadlineTicks").GetString() == now.AddSeconds(caster.CastInfo.NPCRemainingTime).Ticks.ToString(),
            "native cast estimate includes the NPC finish delay while preserving the unadjusted remaining time");
        Check(cast.GetProperty("deadlineKind").GetString() == "Utc" && root.GetProperty("worldTime").GetProperty("ticks").GetString() == now.Ticks.ToString(),
            "deadlines and publication time use the same explicit world clock");
        Check(root.GetProperty("zones")[0].GetProperty("distances")[0].GetSingle() <= 0
            && root.GetProperty("zones")[0].GetProperty("distances")[1].GetSingle() > 0,
            "batched point containment uses native distance evaluation without reconstructing the shape");
        Check(root.GetProperty("complete").GetBoolean() && !root.GetProperty("castThreatCoverageComplete").GetBoolean(),
            "complete current enumeration never claims complete cast-threat or encounter coverage");
        using (var repeated = Read()) Check(repeated.RootElement.GetProperty("targets")[0].GetProperty("cast").GetProperty("castId").GetString() == castId,
            "cast identity is stable across observations of the same native cast");

        hints.ForbiddenZones.Clear();
        hints.AddForbiddenZone(new SDCircle(default, 3), activation);
        hints.AddForbiddenZone(new SDCircle(default, 3), activation, caster.InstanceID);
        using (var unlinked = Read())
        {
            var zones = unlinked.RootElement.GetProperty("zones");
            Check(zones[0].GetProperty("correlation").GetString() == "unknown" && zones[0].GetProperty("castId").ValueKind == JsonValueKind.Null,
                "matching a unique current caster/deadline cannot invent unknown zone ownership");
            Check(zones[1].GetProperty("correlation").GetString() == "source-actor-only" && zones[1].GetProperty("castId").ValueKind == JsonValueKind.Null,
                "explicit caster identity alone cannot establish which cast owns a zone");
        }

        hints.ForbiddenZones.Clear();
        hints.AddForbiddenZoneFromCast(distance, activation, caster.InstanceID, caster.CastInfo);
        caster.CastInfo = Cast(); // Same action/target/deadline, different native cast instance.
        using (var replacement = Read())
        {
            Check(replacement.RootElement.GetProperty("targets")[0].GetProperty("cast").GetProperty("castId").GetString() != castId,
                "a restarted identical cast receives a different identity");
            Check(replacement.RootElement.GetProperty("zones")[0].GetProperty("castId").GetString() == castId,
                "a retained old constraint is not silently relabeled as the caster's newer cast");
        }
        caster.CastInfo = null;
        using (var cancelled = Read()) Check(cancelled.RootElement.GetProperty("targets")[0].GetProperty("cast").ValueKind == JsonValueKind.Null
            && cancelled.RootElement.GetProperty("zones")[0].GetProperty("castId").GetString() == castId,
            "cast cancellation clears current cast evidence without rewriting a retained constraint's provenance");

        hints.Clear();
        hints.AddForbiddenZone(distance, activation, caster.InstanceID);
        using (var cleared = Read()) Check(cleared.RootElement.GetProperty("zones")[0].GetProperty("castId").ValueKind == JsonValueKind.Null,
            "frame reset clears provenance even when a shape/source/time tuple is reused");
        hints.Clear();
        var a = Cast(); var b = Cast();
        hints.AddForbiddenZoneFromCast(distance, activation, caster.InstanceID, a);
        hints.AddForbiddenZoneFromCast(distance, activation, caster.InstanceID, b);
        using (var ambiguous = Read()) Check(ambiguous.RootElement.GetProperty("zones").EnumerateArray().All(z => z.GetProperty("castId").ValueKind == JsonValueKind.Null),
            "conflicting cast provenance for an indistinguishable tuple fails unknown rather than picking an owner");
        hints.Clear();
        hints.AddForbiddenZoneFromCast(distance, activation, caster.InstanceID, a);
        a.Action = new(ActionType.Spell, 501);
        using (var changed = Read()) Check(changed.RootElement.GetProperty("zones")[0].GetProperty("castId").ValueKind == JsonValueKind.Null,
            "mutating the source cast identity invalidates recorded provenance");

        hints.Clear();
        caster.CastInfo = new() { TotalTime = float.NaN, ElapsedTime = 1, Action = new(ActionType.Spell, 500) };
        hints.PotentialTargets.Add(new(caster, 0, false));
        using (var invalid = Read()) Check(invalid.RootElement.GetProperty("targets")[0].GetProperty("cast").GetProperty("deadlineTicks").ValueKind == JsonValueKind.Null,
            "invalid native timing remains unknown instead of producing an interrupt deadline");
        hints.AddForbiddenZone(new FailedCastDistance(false)); hints.AddForbiddenZone(new FailedCastDistance(true));
        using (var failed = Read()) Check(!failed.RootElement.GetProperty("pointQueriesComplete").GetBoolean()
            && failed.RootElement.GetProperty("zones").EnumerateArray().All(z => z.GetProperty("distances").EnumerateArray().All(v => v.ValueKind == JsonValueKind.Null)),
            "throwing and nonfinite native shape evaluations cannot become outside/safe point evidence");
        long budgetClock = 0;
        using (var exhausted = Read(clock: () => budgetClock += 5)) Check(!exhausted.RootElement.GetProperty("complete").GetBoolean()
            && exhausted.RootElement.GetProperty("evaluations").GetInt32() == 0, "cooperative time exhaustion returns incomplete without evaluating further shapes");
        hints.Clear();
        for (var i = 0; i < 129; ++i) hints.AddForbiddenZoneFromCast(new SDCircle(default, 3), activation, caster.InstanceID, b);
        using (var truncated = Read([default])) Check(!truncated.RootElement.GetProperty("complete").GetBoolean()
            && truncated.RootElement.GetProperty("zones").GetArrayLength() == 128 && truncated.RootElement.GetProperty("evaluations").GetInt32() == 128,
            "native zone enumeration and cast provenance have explicit bounded truncation");
        hints.Clear();
        for (var i = 0; i < 101; ++i) hints.PotentialTargets.Add(new(caster, 0, false));
        using (var targets = Read()) Check(!targets.RootElement.GetProperty("complete").GetBoolean() && targets.RootElement.GetProperty("targets").GetArrayLength() == 100,
            "native target overflow is bounded and marked incomplete");

        NativeCastProducerFixtures(Read, hints, caster, Cast);
        NativeCastQueryRejections(native);
        CastControlConsumerFixture(Read, hints, caster, Cast, now);
    }

    private static void NativeCastProducerFixtures(Func<WPos[]?, Func<long>?, JsonDocument> read, AIHints hints, Actor caster, Func<ActorCastInfo> cast)
    {
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        var module = (MechanicsModule)RuntimeHelpers.GetUninitializedObject(typeof(MechanicsModule));
        typeof(BossModule).GetField(nameof(BossModule.WorldState))!.SetValue(module, world);
        typeof(BossModule).GetField(nameof(BossModule.Arena))!.SetValue(module, new MiniArena(default, new ArenaBoundsSquare(10)));
        typeof(BossModule).GetField("_actorArenaProjectionLayers", InternalInstance)!.SetValue(module, new Dictionary<ulong, int>());
        foreach (var component in new GenericAOEs[] { new SimpleAOEs(module, 500, 3), new ChargeAOEs(module, 500, 3),
            new SimpleAOEGroups(module, [500], 3), new SimpleChargeAOEGroups(module, [500], 3) })
        {
            hints.Clear(); caster.CastInfo = cast();
            hints.PotentialTargets.Add(new(caster, 0, false));
            component.OnCastStarted(caster, caster.CastInfo);
            component.AddAIHints(0, caster, default, hints);
            using var publication = read([default], () => 0);
            var root = publication.RootElement;
            Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].Source == caster.InstanceID,
                $"{component.GetType().Name} retains its actual source actor through native AI hints");
            Check(root.GetProperty("zones")[0].GetProperty("castId").GetString() == root.GetProperty("targets")[0].GetProperty("cast").GetProperty("castId").GetString(),
                $"{component.GetType().Name} records cast provenance at the native cast-start producer");
            component.OnCastFinished(caster, caster.CastInfo);
            hints.Clear(); component.AddAIHints(0, caster, default, hints);
            Check(hints.ForbiddenZones.Count == 0, $"{component.GetType().Name} cancellation/reset does not retain a zone or provenance");
        }
    }

    private static void NativeCastQueryRejections(Assembly native)
    {
        var coordinatorType = native.GetType("BossMod.Integration.IntegrationCoordinator", true)!;
        var coordinator = RuntimeHelpers.GetUninitializedObject(coordinatorType);
        using var session = (IDisposable)Activator.CreateInstance(native.GetType("BossMod.Integration.IntegrationSession", true)!, BindingFlags.Instance | BindingFlags.NonPublic,
            null, [(Func<long>)(() => 100L), 1000L], null)!;
        coordinatorType.GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        session.GetType().GetMethod("BeginFrame", InternalInstance)!.Invoke(session, ["cast-fixture", true]);
        var query = coordinatorType.GetMethod("GetCastControl", InternalInstance)!;
        var context = session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        using (var capabilities = JsonDocument.Parse((string)coordinatorType.GetMethod("Capabilities", InternalInstance)!.Invoke(coordinator, null)!))
            Check(capabilities.RootElement.GetProperty("families").GetProperty("cast-control-observation").GetInt32() == 1
                && capabilities.RootElement.GetProperty("families").GetProperty("target-policy").GetInt32() == 1,
                "cast observation is independently discoverable while the existing target family remains compatible");
        foreach (var points in new object[] { Array.Empty<float[]>(), Enumerable.Repeat(new float[] { 0, 0 }, 17).ToArray(),
            new float[][] { null! }, new[] { new float[] { 0 } }, new[] { new float[] { 0, 0, 0 } } })
        {
            using var result = JsonDocument.Parse((string)query.Invoke(coordinator, [IntegrationSession.Serialize(new { context, points })])!);
            Check(!result.RootElement.GetProperty("available").GetBoolean(), "invalid cast query batches reject before accessing absent game/manager state");
        }
        using var stale = JsonDocument.Parse((string)query.Invoke(coordinator, ["{\"context\":{\"schemaVersion\":99}}"] )!);
        Check(!stale.RootElement.GetProperty("available").GetBoolean(), "cast endpoint rejects incompatible/stale contexts before native access");
        var valid = IntegrationSession.Serialize(new { context });
        using (var offThread = JsonDocument.Parse(Task.Run(() => (string)query.Invoke(coordinator, [valid])!).GetAwaiter().GetResult()))
            Check(!offThread.RootElement.GetProperty("available").GetBoolean(), "cast endpoint cannot read native state from a consumer worker thread");
        using (var extra = JsonDocument.Parse((string)query.Invoke(coordinator, [IntegrationSession.Serialize(new { context, invented = true })])!))
            Check(!extra.RootElement.GetProperty("available").GetBoolean(), "unknown cast request fields cannot silently acquire unintended semantics");

        var manager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(new ActorState()));
        typeof(BossModuleManager).GetField(nameof(BossModuleManager.WorldState))!.SetValue(manager, world);
        coordinatorType.GetField("<bossmod>P", InternalInstance)!.SetValue(coordinator, manager);
        using (var absent = JsonDocument.Parse((string)query.Invoke(coordinator, [valid])!))
            Check(!absent.RootElement.GetProperty("available").GetBoolean() && absent.RootElement.GetProperty("reason").GetString() == "missing-player",
                "valid context without a native player is unavailable, never an empty safe encounter");
        var player = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 1; player.PosRot = new(2, 3, 4, 0);
        world.Party.Members[0] = new(1, 1, false);
        ((Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!)[0] = player;
        var hints = new AIHints();
        var caster = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        caster.InstanceID = 2; caster.CastInfo = new() { Action = new(ActionType.Spell, 500), TotalTime = 5, ElapsedTime = 1, Interruptible = true };
        caster.PendingHPDifferences = [];
        caster.HPMP = new(1000, 1000, 0, 0, 0); caster.IsTargetable = true;
        hints.PotentialTargets.Add(new(caster, 0, false) { ShouldBeInterrupted = true });
        hints.AddForbiddenZoneFromCast(new SDCircle(new(2, 4), 3), world.FutureTime(4), caster.InstanceID, caster.CastInfo);
        coordinatorType.GetField("<hints>P", InternalInstance)!.SetValue(coordinator, hints);
        using var endpoint = JsonDocument.Parse((string)query.Invoke(coordinator, [valid])!);
        var endpointRoot = endpoint.RootElement;
        Check(endpointRoot.GetProperty("available").GetBoolean() && endpointRoot.GetProperty("points")[0][0].GetSingle() == 2
            && endpointRoot.GetProperty("points")[0][1].GetSingle() == 4,
            "the actual native endpoint defaults to the player's horizontal point in the same context");
        Check(endpointRoot.GetProperty("zones")[0].GetProperty("distances")[0].GetSingle() <= 0,
            "actual endpoint publication retains the producer-linked point witness");
        var authored = coordinatorType.GetField("_authoredTargetIntent", InternalInstance)!;
        authored.SetValue(coordinator, Array.CreateInstance(authored.FieldType.GetElementType()!, 0));
        using var oldTargets = JsonDocument.Parse((string)coordinatorType.GetMethod("GetTargets", InternalInstance)!.Invoke(coordinator, [IntegrationSession.Serialize(context)])!);
        Check(oldTargets.RootElement.GetProperty("available").GetBoolean(), "native targets fixture must be queryable: " + oldTargets.RootElement.GetRawText());
        var oldCast = oldTargets.RootElement.GetProperty("targets")[0].GetProperty("cast");
        Check(oldTargets.RootElement.GetProperty("available").GetBoolean() && oldCast.GetProperty("castId").GetString()
            == endpointRoot.GetProperty("targets")[0].GetProperty("cast").GetProperty("castId").GetString()
            && oldCast.GetProperty("remainingSeconds").GetSingle() == caster.CastInfo.NPCRemainingTime
            && oldCast.GetProperty("actionId").GetUInt32() == 500 && oldCast.GetProperty("interruptible").GetBoolean(),
            "existing target observation preserves its cast fields and shares exact identity with the new endpoint");
    }

    private static void CastControlConsumerFixture(Func<WPos[]?, Func<long>?, JsonDocument> read, AIHints hints, Actor caster, Func<ActorCastInfo> makeCast, DateTime now)
    {
        hints.Clear(); caster.CastInfo = makeCast();
        hints.PotentialTargets.Add(new(caster, 0, false) { ShouldBeInterrupted = true, ShouldBeStunned = true });
        hints.AddForbiddenZoneFromCast(new SDCircle(default, 3), now.AddSeconds(4.3), caster.InstanceID, caster.CastInfo);
        using var context = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"cast-fixture\",\"frameId\":12,\"stage\":\"hints-ready\"}");
        var calls = 0;
        var example = new BossModV1CastControl(request =>
        {
            ++calls;
            using var sent = JsonDocument.Parse(request);
            var point = sent.RootElement.GetProperty("points")[0];
            using var result = read([new(point[0].GetSingle(), point[1].GetSingle())], () => 0);
            return result.RootElement.GetRawText();
        });
        var inside = example.Read(context.RootElement, Vector2.Zero);
        Check(inside.Available && !inside.ThreatCoverageComplete && inside.PublishedThreats.Length == 1
            && inside.PublishedThreats[0].InterruptRequested && inside.PublishedThreats[0].StunRequested,
            "compilechecked consumer joins exact cast/actor/action/point evidence while retaining unknown overall coverage");
        Check(example.Read(context.RootElement, new(20, 0)).PublishedThreats.Length == 0,
            "consumer does not claim a native point outside the zone is a linked threat");
        var oldCast = caster.CastInfo; caster.CastInfo = makeCast();
        Check(example.Read(context.RootElement, default).PublishedThreats.Length == 0, "consumer rejects an earlier cast's zone even when a new cast has identical timing/action");
        caster.CastInfo = oldCast; caster.CastInfo.EventHappened = true;
        Check(example.Read(context.RootElement, default).PublishedThreats.Length == 0, "consumer rejects casts whose action event already happened");
        caster.CastInfo.EventHappened = false; caster.CastInfo.Interruptible = false;
        var stunOnly = example.Read(context.RootElement, default);
        Check(stunOnly.PublishedThreats.Length == 1 && !stunOnly.PublishedThreats[0].InterruptRequested && stunOnly.PublishedThreats[0].StunRequested,
            "native uninterruptibility does not invent interrupt permission or imply stun immunity");
        var before = calls;
        Check(!example.Read(context.RootElement, new(float.NaN, 0)).Available && calls == before, "consumer rejects nonfinite points before IPC");
        using var wrong = JsonDocument.Parse("{\"schemaVersion\":1,\"instanceGeneration\":\"other\",\"frameId\":12,\"stage\":\"hints-ready\"}");
        Check(!example.Read(wrong.RootElement, default).Available, "consumer rejects a mismatched echoed context");
        caster.IsTargetable = false;
        Check(example.Read(context.RootElement, default).PublishedThreats.Length == 0, "consumer respects native untargetability even with an interrupt/stun request");
        caster.IsTargetable = true; caster.HPMP = new(0, 1000, 0, 0, 0);
        Check(example.Read(context.RootElement, default).PublishedThreats.Length == 0, "consumer does not turn a pending-dead native target into an action recommendation");
    }
}
