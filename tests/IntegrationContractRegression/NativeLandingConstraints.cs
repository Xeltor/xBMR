using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

static partial class Checks
{
    private class LandingKnockback(BossModule module) : SimpleKnockbacks(module, 0, 5)
    {
        internal ShapeDistance[] Shapes = [];
        internal void ImmuneUntil(DateTime expiry) => PlayerImmunes[0].RoleBuffExpire = expiry;
        public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        {
            if (Casters.Count != 0)
                foreach (var shape in Shapes) hints.AddForbiddenZone(shape, Casters[0].Activation);
        }
    }
    private sealed class LandingOverride(BossModule module) : LandingKnockback(module)
    {
        public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => false;
    }
    private sealed class RewriteLandingHints(BossModule module, Action<AIHints> rewrite) : BossComponent(module)
    {
        public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) => rewrite(hints);
    }

    private static unsafe void NativeLandingConstraints()
    {
        var (world, actor, module) = ProjectionFixture();
        actor.HPMP = new(100, 100, 0, 0, 0); actor.PendingHPDifferences = []; actor.PendingKnockbacks = [];
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object obj, string field, object? value) => obj.GetType().GetField(field, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string method, params object?[] args) => obj.GetType().GetMethod(method, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        Set(module, nameof(BossModule.StateMachine), new StateMachine([new(new(), "fixture")]) { ActivePhaseIndex = 0 });
        Set(world, nameof(WorldState.Actors), new ActorState());
        using var effects = (IDisposable)New("Integration.IntegrationActionEffects", world);
        long clock = 1;
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => clock), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        object Register() => Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "landing-native", 2000), (Action<string>)(_ => { }))!;
        var owner = (string)Property(Register(), "OwnerToken");
        var hints = new AIHints();
        var manager = (BossModuleManager)Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var movement = (MovementOverride)Blank("MovementOverride");
        Set(movement, "IntegrationInput", New("Integration.IntegrationInputObservation"));
        Set(movement, "IntegrationPacing", New("Integration.IntegrationPacing", (object?)null));
        var ai = Blank("AI.AIManager"); Set(ai, "Controller", Blank("AI.AIController"));
        var amex = (ActionManagerEx)Blank("ActionManagerEx");
        var pointer = typeof(ActionManagerEx).GetField("_inst", InternalInstance)!;
        var memory = NativeMemory.AllocZeroed((nuint)Marshal.SizeOf(pointer.FieldType.GetElementType()!));
        try
        {
            pointer.SetValue(amex, Pointer.Box(memory, pointer.FieldType));
            var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session); Set(coordinator, "_effects", effects);
            foreach (var field in new (string, object)[] { ("bossmod", manager), ("zonemod", Blank("ZoneModuleManager")), ("hints", hints),
                ("rotation", Blank("Autorotation.RotationModuleManager")), ("amex", amex), ("movement", movement), ("ai", ai) })
                Set(coordinator, "<" + field.Item1 + ">P", field.Item2);
            var kb = new LandingKnockback(module);
            string Context() => IntegrationSession.Serialize(session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!);
            string Id() => (string)Type("Integration.IntegrationMechanics").GetMethod("ComponentId", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [kb])!;
            ShapeDistance Pure(float limit = 9) => new SDKnockbackInAABBSquareAwayFromOrigin(default, default, 5, limit);
            void Build(Action? configure = null)
            {
                Call(coordinator, "BeginWorldUpdate"); hints.Clear();
                module.Components.Clear(); module.Components.Add(kb);
                kb.Casters.Clear(); kb.Casters.Add(new(default, 5, world.FutureTime(3)));
                kb.Shapes = [Pure()]; kb.StopAtWall = false; kb.StopAfterWall = false; kb.ImmuneUntil(default);
                configure?.Invoke();
                module.CalculateAIHints(0, actor, default, hints);
                hints.Normalize();
                Call(coordinator, "HintsReady");
            }
            string Request(string? context = null, string? componentId = null, int count = 1) => IntegrationSession.Serialize(new
            {
                context = JsonSerializer.Deserialize<JsonElement>(context ?? Context()), componentId = componentId ?? Id(),
                poses = Enumerable.Range(0, count).Select(_ => new { position = new[] { 3f, 0f }, facing = 0f }).ToArray()
            });
            JsonDocument Query(string? request = null) => JsonDocument.Parse((string)Call(coordinator, "EvaluateKnockbackLandings", request ?? Request())!);
            string Status(JsonDocument response) => response.RootElement.GetProperty("observations")[0].GetProperty("currentPublishedConstraintsStatus").GetString()!;
            Build();
            using (var warm = Query()) { } // Native first-use JIT is not query permission.
            Build();
            using (var warm = Query()) { }
            Build();
            var before = hints.ForbiddenZones.ToArray(); var actorBefore = actor.PosRot;
            Check(before[0].shapeDistance.Contains(new(8, 0)), "native preparation constraint would incorrectly project the landing a second time");
            using (var clear = Query())
            {
                Check(clear.RootElement.GetProperty("available").GetBoolean() && Status(clear) == "clear", "native landing query separates an owned radial preparation rule and checks its actual landing envelope");
                Check(clear.RootElement.GetProperty("status").GetString() == "unknown" && !clear.RootElement.GetProperty("physicalObstaclesEvaluated").GetBoolean()
                    && !clear.RootElement.GetProperty("preparationReachabilityEvaluated").GetBoolean() && !clear.RootElement.GetProperty("recoveryReachabilityEvaluated").GetBoolean(),
                    "published landing constraint clearance is never complete physical, preparation or recovery safety");
                Check(clear.RootElement.GetProperty("observations")[0].GetProperty("separatedProjectionConstraints").GetInt32() == 1,
                    "the separation reports exact native producer-linked constraint count");
            }
            Check(before.SequenceEqual(hints.ForbiddenZones) && actor.PosRot == actorBefore, "native evaluation neither edits/clones live hints nor changes actor pose");
            var reader = new IntegrationExample.BossModV1LandingConstraints(r => (string)Call(coordinator, "EvaluateKnockbackLandings", r)!);
            var contextElement = JsonSerializer.Deserialize<JsonElement>(Context());
            Check(reader.Inspect(contextElement, Id(), [new(3, 0)], [0]) is [{ Landing.X: 8, CurrentPublishedConstraintsStatus: "clear" }],
                "compiled BCL reader consumes the actual endpoint as limited observations");
            Check(new IntegrationExample.BossModV1LandingConstraints(_ => "{broken").Inspect(contextElement, Id(), [new(3, 0)], [0]) == null,
                "malformed landing evidence stays unavailable in the compiled reader");
            Check(reader.Inspect(contextElement, Id(), [new(3, 0)], []) == null, "compiled reader bounds mismatched pose/facing batches before IPC");

            Build();
            var partyActors = (Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!;
            var replacement = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            replacement.InstanceID = actor.InstanceID; replacement.HPMP = actor.HPMP;
            replacement.PosRot = actor.PosRot; replacement.HitboxRadius = actor.HitboxRadius;
            replacement.PendingHPDifferences = []; replacement.PendingKnockbacks = [];
            partyActors[0] = replacement;
            using (var changedActor = Query()) Check(!changedActor.RootElement.GetProperty("available").GetBoolean(),
                "same-ID actor replacement cannot inherit another actor object's producer capture");
            partyActors[0] = actor;
            var nativeId = actor.InstanceID; actor.InstanceID = 99;
            using (var changedId = Query()) Check(!changedId.RootElement.GetProperty("available").GetBoolean(),
                "retained actor object cannot reuse producer capture after identity changes");
            actor.InstanceID = nativeId;

            Build(); hints.AddForbiddenZone(new SDCircle(new(8, 0), 1), world.FutureTime(3), 42);
            using (var damage = Query()) Check(Status(damage) == "blocked", "independent damage geometry at the landing remains authoritative");
            Build(); hints.TemporaryObstacles.Add(new SDCircle(new(8, 0), 1));
            using (var obstacle = Query()) Check(Status(obstacle) == "blocked", "current temporary obstacles are checked at the landing");
            Build(); hints.AddForbiddenZone(new SDCircle(new(8, 0), 1), world.FutureTime(4));
            using (var margin = Query()) Check(Status(margin) == "blocked", "native activation cushion and additional landing margin retain near-future hazards");
            Build(); hints.AddForbiddenZone(new SDCircle(new(8, 0), 1), world.FutureTime(6));
            using (var future = Query()) Check(Status(future) == "clear" && future.RootElement.GetProperty("observations")[0].GetProperty("laterWarnings").GetInt32() == 1,
                "later warning is explicitly deferred for recovery planning rather than relabeled as expired");
            Build(); hints.AddForbiddenZone(new SDCircle(new(8, 0), 1), world.CurrentTime.AddSeconds(-3));
            using (var past = Query()) Check(Status(past) == "blocked", "past activation never fabricates hazard expiry");
            Build(() => kb.Shapes = [Pure(7)]);
            using (var envelope = Query()) Check(Status(envelope) == "blocked" && envelope.RootElement.GetProperty("observations")[0].GetProperty("reason").GetString() == "own-projection-landing-envelope",
                "separating the preparation transform preserves the producer's stricter landing envelope");
            Build(() => kb.Shapes = [Pure(8.4f)]);
            using (var footprint = Query()) Check(Status(footprint) == "blocked", "actual actor footprint and spatial margin constrain landing clearance");
            Build(() => kb.Shapes = [new SDKnockbackInCircleAwayFromOrigin(default, default, 5, 9)]);
            using (var circle = Query()) Check(Status(circle) == "clear", "native pure circular radial projection is independently supported");
            Build(() => kb.Shapes = [new SDUnion([Pure(9), Pure(7)])]);
            using (var union = Query()) Check(Status(union) == "blocked", "a union of pure preparation rules retains every landing envelope");
            Build(() => kb.Shapes = [new SDUnion([Pure(), new SDCircle(new(20, 20), 1)])]);
            var mixed = hints.ForbiddenZones[0].shapeDistance;
            using (var ambiguous = Query()) Check(!ambiguous.RootElement.GetProperty("available").GetBoolean()
                && ReferenceEquals(mixed, hints.ForbiddenZones[0].shapeDistance), "ambiguous mixed own constraint never discards a real damage/preparation sibling");
            Build(); hints.AddForbiddenZone(new SDUnion([new SDCircle(new(8, 0), 1), new SDCircle(new(20, 20), 1)]), world.FutureTime(3));
            using (var unionDamage = Query()) Check(Status(unionDamage) == "blocked", "independent native unions preserve all damage children");
            Build(); hints.AddForbiddenZone(new SDIntersection([new SDCircle(new(20, 20), 1)]), world.FutureTime(3));
            using (var unsupported = Query()) Check(Status(unsupported) == "unknown", "unsupported independent composites cannot become positive clearance");
            Build(() => module.Components.Add(new RewriteLandingHints(module, h => h.ForbiddenZones[0] = (new SDCircle(new(20, 20), 1), world.FutureTime(3), 0))));
            using (var rewritten = Query()) Check(!rewritten.RootElement.GetProperty("available").GetBoolean(), "later producer rewrites invalidate captured ownership instead of guessing it");
            Build(); hints.ForbiddenZones.Add(hints.ForbiddenZones[0]);
            using (var duplicate = Query()) Check(!duplicate.RootElement.GetProperty("available").GetBoolean(), "duplicate native shape/time/source tuples remain ambiguously attributed");
            Build(); hints.ForbiddenZones[0] = (hints.ForbiddenZones[0].shapeDistance, world.FutureTime(4), 0);
            using (var retimed = Query()) Check(!retimed.RootElement.GetProperty("available").GetBoolean(), "retiming a retained source cannot reuse its previous producer attribution");
            var children = new[] { Pure() };
            Build(() => kb.Shapes = [new SDUnion(children)]); children[0] = Pure(7);
            using (var graphChanged = Query()) Check(!graphChanged.RootElement.GetProperty("available").GetBoolean(), "post-capture union-child mutation invalidates producer attribution");
            var cycleChildren = new ShapeDistance[1]; var cycle = new SDUnion(cycleChildren); cycleChildren[0] = cycle;
            Build(() => kb.Shapes = [cycle]);
            using (var cyclic = Query()) Check(!cyclic.RootElement.GetProperty("available").GetBoolean(), "cyclic native union metadata is bounded without invoking its recursive distance function");
            Build(() => kb.Shapes = [new SDUnion(Enumerable.Range(0, 129).Select(_ => Pure()).ToArray())]);
            using (var oversized = Query()) Check(!oversized.RootElement.GetProperty("available").GetBoolean(), "oversized constraint graphs remain unavailable");
            Build(() => kb.Shapes = [new SDKnockbackInAABBSquareAwayFromOrigin(default, new(1, 0), 5, 9)]);
            using (var mismatch = Query()) Check(!mismatch.RootElement.GetProperty("available").GetBoolean(), "source geometry must match the exact producer-linked native projection");
            Build(() =>
            {
                var other = new LandingKnockback(module) { Shapes = [Pure()] };
                other.Casters.Add(kb.Casters[0]); module.Components.Add(other);
            });
            using (var other = Query()) Check(Status(other) == "unknown", "another component's identical geometry/time is never stripped as this component's preparation rule");
            kb = new LandingOverride(module); Build();
            using (var customRule = Query()) Check(Status(customRule) == "unknown",
                "a native DestinationUnsafe override keeps hypothetical-start context uncertainty explicit");
            kb = new LandingKnockback(module); Build();
            var priorFailure = Context();
            try
            {
                Build(() => module.Components.Add(new RewriteLandingHints(module, _ => throw new InvalidOperationException("native-producer-failure"))));
                Check(false, "native producer fixture should throw");
            }
            catch (InvalidOperationException error) when (error.Message == "native-producer-failure")
            {
                using var failedBuild = Query(Request(context: priorFailure));
                Check(!failedBuild.RootElement.GetProperty("available").GetBoolean(), "metadata collection preserves native exceptions and cannot revive prior context after an interrupted build");
            }
            foreach (var change in new Action[] { () => kb.StopAtWall = true, () => kb.Casters.Add(kb.Casters[0]),
                () => kb.ImmuneUntil(world.FutureTime(5)), () => kb.Shapes = [],
                () => kb.Casters[0] = new(default, 5, world.FutureTime(3), kind: GenericKnockback.Kind.DirForward) })
            {
                Build(change); using var unsupported = Query();
                Check(!unsupported.RootElement.GetProperty("available").GetBoolean(), "unsupported wall/sequence/immunity/publication/kind cannot acquire landing clearance");
            }
            Build(); using (var tooMany = Query(Request(count: 5))) Check(!tooMany.RootElement.GetProperty("available").GetBoolean(), "landing endpoint bounds pose batch sizes");
            using (var incompatible = Query(Request().Replace("\"schemaVersion\":1", "\"schemaVersion\":2"))) Check(!incompatible.RootElement.GetProperty("available").GetBoolean(), "landing endpoint rejects incompatible schemas");
            using (var missing = Query(Request(componentId: "missing"))) Check(!missing.RootElement.GetProperty("available").GetBoolean(), "unknown component cannot inherit similar landing constraints");
            var previous = Context(); Build();
            using (var stale = Query(Request(context: previous))) Check(!stale.RootElement.GetProperty("available").GetBoolean(), "landing constraint captures are specific to one native hint frame");
            Check(Task.Run(() => { using var wrong = Query(); return !wrong.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(), "landing queries require the native callback thread");
            Build(); clock += 251;
            using (var expired = Query()) Check(!expired.RootElement.GetProperty("available").GetBoolean(), "landing observations cannot outlive source frame freshness");
            clock = 1;
            Call(session, "Unregister", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0));
            Build(); using (var noCapture = Query()) Check(!noCapture.RootElement.GetProperty("available").GetBoolean(), "without registered owners producer capture stays disabled and dependent queries remain unavailable");
            using var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!);
            Check(capabilities.RootElement.GetProperty("families").GetProperty("native-knockback-landing-constraints").GetInt32() == 1,
                "native landing constraints are independently discoverable without complete mechanic-safety claims");
        }
        finally { NativeMemory.Free(memory); }
    }
}
