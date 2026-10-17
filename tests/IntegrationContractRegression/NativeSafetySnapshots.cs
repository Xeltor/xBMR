using BossMod;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private static void NativeSafetySnapshots()
    {
        var (world, actor, module) = ProjectionFixture();
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object o, string name, object? value) => o.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(o, value);
        object? Call(object o, string name, params object?[] args) => o.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(o, args);
        long clock = 1;
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => clock), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var owner = (string)Property(Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "native-safety", 2000), (Action<string>)(_ => { }))!, "OwnerToken");
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(8) };
        var manager = Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session);
        Set(coordinator, "<bossmod>P", manager); Set(coordinator, "<hints>P", hints); Set(coordinator, "<movement>P", Blank("MovementOverride"));
        Set(coordinator, "<rotation>P", Blank("Autorotation.RotationModuleManager")); Set(coordinator, "_safetyGate", new object());
        var jobs = coordinator.GetType().GetField("_safetyJobs", InternalInstance)!; jobs.SetValue(coordinator, Activator.CreateInstance(jobs.FieldType));
        var routeJobs = coordinator.GetType().GetField("_safetyRoutes", InternalInstance)!; routeJobs.SetValue(coordinator, Activator.CreateInstance(routeJobs.FieldType));
        var constraintJobs = coordinator.GetType().GetField("_constraintJobs", InternalInstance)!; constraintJobs.SetValue(coordinator, Activator.CreateInstance(constraintJobs.FieldType));
        var client = (ClientState)Blank("ClientState"); client.MoveSpeed = 6; Set(world, nameof(WorldState.Client), client);
        actor.PosRot = new(0, 0, 0, 0); actor.HitboxRadius = 0.5f; actor.PendingKnockbacks = [];
        var component = new LandingKnockback(module); component.Casters.Add(new(default, 5, world.FutureTime(3))); module.Components.Add(component);
        string Id() => (string)Type("Integration.IntegrationMechanics").GetMethod("ComponentId", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [component])!;
        object Context() => session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        void Frame(ShapeDistance[] shapes, bool separate = true)
        {
            hints.Clear(); hints.PathfindMapBounds = new ArenaBoundsSquare(8);
            Set(hints, "ProjectionSourcesRequested", true); component.Shapes = shapes;
            module.CalculateAIHints(0, actor, default, hints); hints.Normalize();
            Call(session, "BeginFrame", (string)Call(coordinator, "ControlScope")!, true);
            Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c)));
            Set(hints, "ProjectionSourcesContext", Context());
        }
        string Start(bool omit = true) => (string)Call(coordinator, "StartSafetySnapshot", IntegrationSession.Serialize(new
            { context = Context(), ownerToken = owner, omitComponentIds = omit ? new[] { Id() } : [] }))!;
        string Poll(string id) => (string)Call(coordinator, "PollSafetySnapshot", IntegrationSession.Serialize(new { context = Context(), ownerToken = owner, snapshotId = id }))!;
        string Window(string id, string kind) => (string)Call(coordinator, "GetSafetyWindow", IntegrationSession.Serialize(new
            { context = Context(), ownerToken = owner, snapshotId = id, kind, left = 0, top = 0, width = 40, height = 40 }))!;
        string Capture(ShapeDistance[] shapes, bool omit = true)
        {
            for (var attempt = 0; attempt < 5; ++attempt)
            {
                Frame(shapes); using var receipt = JsonDocument.Parse(Start(omit));
                if (receipt.RootElement.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean())
                {
                    var id = receipt.RootElement.GetProperty("snapshotId").GetString()!;
                    var until = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < until)
                    {
                        using var polled = JsonDocument.Parse(Poll(id));
                        if (polled.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ready") return id;
                        if (polled.RootElement.TryGetProperty("available", out var available) && !available.GetBoolean()) throw new InvalidOperationException(polled.RootElement.ToString());
                        Thread.Sleep(1);
                    }
                    throw new InvalidOperationException("native safety worker did not finish bounded fixture");
                }
            }
            throw new InvalidOperationException("native safety capture never passed its first-use budget");
        }
        byte[] Blocks(string id, string kind) { using var json = JsonDocument.Parse(Window(id, kind)); return json.RootElement.GetProperty("window").GetProperty("blocks").GetBytesFromBase64(); }
        string StartRoute(string id, string kind, float[] target, float[]? origin = null, string? deadline = null)
        {
            using var receipt = JsonDocument.Parse((string)Call(coordinator, "StartSafetyRoute", IntegrationSession.Serialize(new
            { context = Context(), ownerToken = owner, snapshotId = id, kind, origin, destination = target, mustArriveByTicks = deadline }))!);
            Check(receipt.RootElement.GetProperty("accepted").GetBoolean(), "native strict route starts from its owned immutable snapshot");
            return receipt.RootElement.GetProperty("routeId").GetString()!;
        }
        JsonDocument Route(string id, string route)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until)
            {
                var result = JsonDocument.Parse((string)Call(coordinator, "PollSafetyRoute", IntegrationSession.Serialize(new
                    { context = Context(), ownerToken = owner, snapshotId = id, routeId = route }))!);
                if (!result.RootElement.TryGetProperty("status", out var status) || status.GetString() != "pending") return result;
                result.Dispose(); Thread.Sleep(1);
            }
            throw new InvalidOperationException("native strict route fixture exceeded worker wait");
        }
        void Clear() => Call(session, "Clear", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0));
        try
        {
            var projected = new SDKnockbackInAABBSquareAwayFromOrigin(default, default, 5, 7);
            var independent = new SDCircle(new(0, 3), 1);
            var id = Capture([new SDUnion([projected, independent])]);
            using (var ready = JsonDocument.Parse(Poll(id))) Check(ready.RootElement.GetProperty("omittedProjectionRegions").GetArrayLength() == 1
                && ready.RootElement.GetProperty("strictUnion").GetBoolean(), "native strict snapshot separates only exact producer-attributed preparation leaf and retains independent union sibling");
            using (var metadata = JsonDocument.Parse(Poll(id))) Check(metadata.RootElement.GetProperty("embeddedLandingConstraintsRetained").GetBoolean(),
                "corrected snapshot explicitly certifies embedded native landing predicates remain binding");
            var recovery = StartRoute(id, "recovery", [6, 0], [5, 0]);
            using (var route = Route(id, recovery)) Check(route.RootElement.GetProperty("status").GetString() == "clear"
                && route.RootElement.GetProperty("witnessComplete").GetBoolean() && route.RootElement.GetProperty("hypotheticalOrigin").GetBoolean(),
                "native strict recovery returns complete conditional ThetaStar witness rather than reconstructing displacement");
            var preparation = StartRoute(id, "preparation", [1, 0]);
            using (var route = Route(id, preparation)) Check(route.RootElement.GetProperty("status").GetString() == "clear"
                && !route.RootElement.GetProperty("hypotheticalOrigin").GetBoolean() && route.RootElement.GetProperty("doesNotAuthorizeMovementOrActions").GetBoolean(),
                "native preparation witness uses actual actor origin and preserves action/control ownership: " + route.RootElement);
            var late = StartRoute(id, "preparation", [1, 0], deadline: world.FutureTime(0.5).Ticks.ToString());
            using (var route = Route(id, late)) Check(route.RootElement.GetProperty("status").GetString() == "blocked"
                && route.RootElement.GetProperty("validation").GetProperty("reason").GetString() == "native-route-arrival-deadline",
                "native route cannot claim preparation arrival after its conservative deadline");
            var prep = Blocks(id, "preparation"); var land = Blocks(id, "landing");
            Check(prep[20 * 40 + 30] != 0 && land[20 * 40 + 30] == 0, "own preparation rule remains binding for starts and is not applied a second time to landing positions");
            Check(land[26 * 40 + 20] != 0, "independent same-component union hazard remains binding on the landing raster");
            Clear();
            id = Capture([new SDCircle(default, 2)], omit: false);
            var escape = StartRoute(id, "preparation", [5, 0]);
            using (var route = Route(id, escape)) Check(route.RootElement.GetProperty("status").GetString() == "clear",
                "future native warning permits complete preparation escape before unchanged source deadline: " + route.RootElement);
            Clear();
            var embedded = new SDKnockbackInAABBSquareAwayFromOriginPlusAOECirclesMixedRadiiPlusAvoidShape(default, default, 5, 9,
                [(new WPos(3, 0), 1f)], 1, new SDCircle(new(-3, 0), 1));
            id = Capture([embedded]); land = Blocks(id, "landing");
            Check(land[20 * 40 + 26] != 0 && land[20 * 40 + 14] != 0,
                "native unprojected view preserves embedded current-position circles and direct avoid predicate while suppressing only displacement");
            Check(embedded.Distance(new(3, 0)) > 0, "embedded-circle regression is clear under projected preparation but dangerous at the actual landing point");
            Clear();
            id = Capture([new SDKnockbackFixedDirectionAgainstSafewalls(new(1, 0), [], 5, 0)]);
            Check(Blocks(id, "landing").Contains((byte)0), "pure safe-wall preparation is not retained as an all-danger landing rule");
            Clear();
            id = Capture([new SDKnockbackFixedDirectionAgainstSafewallsPlusRectAOE(new(1, 0), [], 5, 0, default, new(1, 0), 3, 1)]);
            land = Blocks(id, "landing");
            Check(land[20 * 40 + 22] != 0 && land[20 * 40 + 14] == 0,
                "compound safe-wall landing view retains exactly its independent rectangle without requiring a second wall arrival");
            hints.ForbiddenZones.Add((new SDCircle(default, 1), world.FutureTime(4), 0));
            using (var changed = JsonDocument.Parse(Poll(id))) Check(!changed.RootElement.GetProperty("available").GetBoolean(), "changed native constraint publication permanently retires copied safety snapshot");
            id = Capture([new SDCircle(default, 100)], omit: false);
            using (var ready = JsonDocument.Parse(Poll(id))) Check(ready.RootElement.GetProperty("nativePreparationRelaxationPrevented").GetBoolean(), "native all-danger fallback is explicitly recorded and prevented from becoming safe evidence");
            Check(!Blocks(id, "preparation").Contains((byte)0), "strict all-danger map exposes no reopened fallback cells");
            Clear(); using (var cleared = JsonDocument.Parse(Poll(id))) Check(!cleared.RootElement.GetProperty("available").GetBoolean(), "owner clear retires pending/ready safety snapshots and native query buffers");
            id = Capture([new SDCircle(default, float.NaN)], omit: false);
            Check(!Blocks(id, "landing").Contains((byte)0), "NaN distance or invalid native row culling cannot become strict cell clearance");
            var diagnostic = (Task<string>)Call(coordinator, "CheckSafetyBackend", IntegrationSession.Serialize(new { schemaVersion = 1, instanceGeneration = instance }))!;
            using (var result = JsonDocument.Parse(diagnostic.GetAwaiter().GetResult()))
                Check(result.RootElement.GetProperty("diagnosticOnly").GetBoolean() && result.RootElement.GetProperty("completed").GetBoolean()
                    && !result.RootElement.GetProperty("liveSourceValidated").GetBoolean() && !result.RootElement.GetProperty("liveProofIdsIssued").GetBoolean(),
                    "in-process native backend diagnostic validates isolated worker/search data without issuing live source authority");
            var controlDiagnostic = (Task<string>)Call(coordinator, "CheckControlBackend", IntegrationSession.Serialize(new { schemaVersion = 1, instanceGeneration = instance }))!;
            using (var result = JsonDocument.Parse(controlDiagnostic.GetAwaiter().GetResult()))
                Check(result.RootElement.GetProperty("completed").GetBoolean() && !result.RootElement.GetProperty("inputsWritten").GetBoolean()
                    && result.RootElement.GetProperty("tests").EnumerateObject().All(p => p.Value.GetBoolean()),
                    "loaded native control backend runs isolated ownership/timing/input/classifier fixtures without actual game input: " + result.RootElement);
            clock += 251; using (var expired = JsonDocument.Parse(Poll(id))) Check(!expired.RootElement.GetProperty("available").GetBoolean(), "snapshot source deadline is original native frame time, never refreshed by polling");
            id = Capture([projected]);
            Check(Task.Run(() => { using var wrong = JsonDocument.Parse(Poll(id)); return !wrong.RootElement.GetProperty("available").GetBoolean(); }).GetAwaiter().GetResult(), "safety polling still requires the exact current native callback thread");
            using (var window = JsonDocument.Parse((string)Call(coordinator, "GetSafetyWindow", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = id, kind = "landing", left = 0, top = 0, width = 128, height = 128 }))!))
                Check(!window.RootElement.GetProperty("available").GetBoolean(), "safety window request cannot copy an oversized raster region");
            Clear(); id = Capture([projected]); component.StopAtWall = true;
            using (var changed = JsonDocument.Parse(Poll(id))) Check(!changed.RootElement.GetProperty("available").GetBoolean(), "native wall-clipping policy change retires source even with unchanged published zones");
            component.StopAtWall = false;
            id = Capture([projected]); component.ImmuneUntil(world.FutureTime(10));
            using (var changed = JsonDocument.Parse(Poll(id))) Check(!changed.RootElement.GetProperty("available").GetBoolean(), "native immunity change retires source even with unchanged custom preparation zones");
            component.ImmuneUntil(default);
            Clear(); module.Components.Clear();
            owner = (string)Property(Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "owned-constraint", 2000), (Action<string>)(_ => { }))!, "OwnerToken");
            string Constraint(string parent, Func<Vector2, float> sdf) => (string)Call(coordinator, "SubmitConstraint", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = parent, sourceKind = "learned", activationTicks = world.FutureTime(3).Ticks.ToString(), conservativeLipschitz = 1 }), sdf)!;
            JsonDocument WaitConstraint(string constraint)
            {
                var until = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < until)
                {
                    var response = JsonDocument.Parse((string)Call(coordinator, "PollConstraint", IntegrationSession.Serialize(new
                        { context = Context(), ownerToken = owner, constraintId = constraint }))!);
                    if (!response.RootElement.TryGetProperty("status", out var state) || state.GetString() != "pending") return response;
                    response.Dispose(); Thread.Sleep(1);
                }
                throw new InvalidOperationException("owned constraint worker timeout");
            }
            id = Capture([], omit: false);
            using (var started = JsonDocument.Parse(Constraint(id, p => p.X + 2)))
            {
                Check(started.RootElement.GetProperty("accepted").GetBoolean(), "detached learned SDF starts only after complete native displacement absence");
                using var admitted = WaitConstraint(started.RootElement.GetProperty("constraintId").GetString()!);
                Check(admitted.RootElement.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean(),
                    "immutable hard overlay admitted only with combined strict clear footprint: " + admitted.RootElement);
            }
            var nativeContext = new BossMod.Pathfinding.NavigationDecision.Context();
            _ = BossMod.Pathfinding.NavigationDecision.Build(nativeContext, world.CurrentTime, hints, actor);
            var warned = nativeContext.Map.WorldToGrid(new(-3, 0));
            var limit = nativeContext.Map.PixelMaxG[nativeContext.Map.GridToIndex(warned)];
            Check(limit > 0 && limit < float.MaxValue, "future owned preparation mask retains native timed traversal rather than becoming an immediate physical wall");
            _ = BossMod.Pathfinding.NavigationDecision.Build(nativeContext, world.FutureTime(4), hints, actor);
            Check(nativeContext.Map.PixelMaxG[nativeContext.Map.GridToIndex(warned)] == 0,
                "native owned raster never least-danger relaxes its activated warning cells");
            actor.PosRot.X = 0.1f;
            var owned = ((ShapeDistance shapeDistance, DateTime activation, ulong Source)[])Call(hints, "CaptureIntegrationWarnings")!;
            Check(owned.Length == 1, "world-anchored learned mask permits normal horizontal progress inside unchanged native source");

            Check(owned.Length == 1 && owned[0].shapeDistance.Contains(new(-3, 0)) && !owned[0].shapeDistance.Contains(new(3, 0))
                && hints.TemporaryObstacles.Count == 0 && hints.ForbiddenZones.Count == 0,
                "owned mask constrains native obstacle capture while preserving original authored lists");
            hints.ForbiddenZones.Add((new SDCircle(new(8, 0), 1), world.FutureTime(2), 0));
            Check((((ShapeDistance shapeDistance, DateTime activation, ulong Source)[])Call(hints, "CaptureIntegrationWarnings")!).Length == 1 && (bool)Call(session, "IsOwnerActive", owner)!,
                "final native source mutation withdraws optional overlay while keeping read-only observer registration alive");
            foreach (var invalid in new Func<Vector2, float>[] { _ => -1, _ => float.NaN, _ => throw new InvalidOperationException("consumer") })
            {
                clock += 150;
                id = Capture([], omit: false);
                using var started = JsonDocument.Parse(Constraint(id, invalid));
                Check(started.RootElement.GetProperty("accepted").GetBoolean(), "bounded worker accepts detached callback for evaluation, not immediate mutation");
                using var rejected = WaitConstraint(started.RootElement.GetProperty("constraintId").GetString()!);
                Check(!rejected.RootElement.GetProperty("available").GetBoolean() && (bool)Call(session, "IsOwnerActive", owner)!,
                    "all-danger/nonfinite/throwing optional constraints cannot strand movement or disconnect the observer");
            }
            clock += 150; Frame([]); id = Capture([], omit: false);
            using (var segments = JsonDocument.Parse((string)Call(coordinator, "EvaluateSafetySegments", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = id, kind = "landing", routes = new float[][][] { [[0, 0], [2, 0]] },
                  model = new { radius = 0.5f, speed = 10, departureDelaySeconds = 0, additionalTimeMarginSeconds = 0.25f } }))!))
                Check(segments.RootElement.GetProperty("results")[0].GetProperty("status").GetString() == "clear"
                    && !segments.RootElement.GetProperty("detourSearchPerformed").GetBoolean() && !segments.RootElement.GetProperty("forcedPhysicsVerified").GetBoolean(),
                    "snapshot-bound exact segment evaluator never substitutes a walking detour or claims forced physics");
            var cells = Enumerable.Range(10, 20).SelectMany(y => Enumerable.Range(10, 20).Select(x => y * 40 + x)).ToArray();
            using (var submitted = JsonDocument.Parse((string)Call(coordinator, "SubmitConstraintCells", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = id, sourceKind = "learned", activationTicks = world.FutureTime(3).Ticks.ToString(), allowedCellIndices = cells }))!))
            {
                Check(submitted.RootElement.GetProperty("accepted").GetBoolean(), "explicit native-cell mask transport needs no false binary Lipschitz claim");
                using var admitted = WaitConstraint(submitted.RootElement.GetProperty("constraintId").GetString()!);
                Check(admitted.RootElement.TryGetProperty("accepted", out var ok) && ok.GetBoolean(), "native-cell mask has a full actual native-rasterized clear footprint");
            }
            Check((bool)Call(session, "IsOwnerActive", owner)!, "normal mask admission preserves observer registration");
            clock += 251; id = Capture([], omit: false);
            clock += 100; Frame([]);
            using (var submitted = JsonDocument.Parse((string)Call(coordinator, "SubmitConstraintCells", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = id, sourceKind = "learned", activationTicks = world.FutureTime(3).Ticks.ToString(), allowedCellIndices = cells }))!))
            {
                using var admitted = WaitConstraint(submitted.RootElement.GetProperty("constraintId").GetString()!);
                Check(admitted.RootElement.TryGetProperty("accepted", out var ok) && ok.GetBoolean(), "fresh mask can use a still-live older original snapshot");
            }
            Call(session, "Submit", New("Integration.IntegrationSession+GoalRequest", Context(), owner, "combat"), (Func<Vector2, float>)(_ => 4));
            var paired = (Func<Vector2, float>[])Call(session, "CaptureIndividualGoalLeaves")!;
            Check(paired.Length == 1 && paired[0](default) == 4, "native admission enables only its freshly paired goal leaves");
            clock += 151;
            Check(paired[0](default) == 0 && (bool)Call(session, "IsOwnerActive", owner)!,
                "later mask submission cannot renew original snapshot expiry or disconnect the observer");
            id = Capture([], omit: false);
            using (var submitted = JsonDocument.Parse((string)Call(coordinator, "SubmitConstraintCells", IntegrationSession.Serialize(new
                { context = Context(), ownerToken = owner, snapshotId = id, sourceKind = "learned", activationTicks = world.FutureTime(-0.5).Ticks.ToString(),
                  landingUntilTicks = world.FutureTime(0.5).Ticks.ToString(), allowedCellIndices = cells }))!))
            {
                Check(submitted.RootElement.GetProperty("accepted").GetBoolean(), "learned continuation retains original past activation with an explicit still-live bounded landing end");
                using var admitted = WaitConstraint(submitted.RootElement.GetProperty("constraintId").GetString()!);
                Check(admitted.RootElement.TryGetProperty("accepted", out var ok) && ok.GetBoolean()
                    && admitted.RootElement.GetProperty("activationTicks").GetString() == world.FutureTime(-0.5).Ticks.ToString(),
                    "post-activation mask remains nonrelaxed without renewing event clocks");
            }
            var savedTime = world.Frame.Timestamp; world.Frame.Timestamp = savedTime.AddSeconds(0.6);
            var afterLanding = ((ShapeDistance shapeDistance, DateTime activation, ulong Source)[])Call(hints, "CaptureIntegrationWarnings")!;
            Check(afterLanding.Length == 0 && (bool)Call(session, "IsOwnerActive", owner)!, "original learned landing deadline withdraws the contribution without revoking its observer");
            world.Frame.Timestamp = savedTime;
        }
        finally { Call(coordinator, "DisposeSafetySnapshots"); }

    }
}
