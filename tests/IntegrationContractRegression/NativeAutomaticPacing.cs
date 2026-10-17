using BossMod;
using BossMod.Pathfinding;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BossMod.Integration;

static partial class Checks
{
    public class OfflinePacingSignatureService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.ReturnType == typeof(nint) ? (nint)0 : targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    private static void NativeAutomaticPacing()
    {
        Service.Config._nodes.TryAdd(typeof(ActionTweaksConfig), new ActionTweaksConfig());
        // MovementOverride's type initializer resolves a signature even for a managed fixture.
        // Supply no pointer, hook or live game call; this fixture never enters its unsafe detour.
        var scanner = typeof(Service).GetProperty("SigScanner")!;
        if (scanner.GetValue(null) == null) scanner.SetValue(null, DispatchProxy.Create(scanner.PropertyType, typeof(OfflinePacingSignatureService)));
        var a = typeof(AIHints).Assembly;
        Type Type(string name) => a.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object obj, string name, object? value) => obj.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        var coordinator = Blank("Integration.IntegrationCoordinator");
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 100L), 1000L);
        Set(coordinator, "Session", session);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var registration = New("Integration.IntegrationSession+Registration", 1, instance, "pace-native", 2000);
        var registered = Call(session, "Register", registration, (Action<string>)(_ => { }))!;
        var owner = (string)Property(registered, "OwnerToken");
        Call(session, "BeginFrame", "fixture", true);
        Func<object, string> snapshot = c => IntegrationSession.Serialize(c);
        Call(session, "Publish", "hints-ready", snapshot);
        Call(session, "Publish", "movement-proposed", snapshot);
        var context = session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        var frame = (long)Property(context, "FrameId");
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        world.Frame = new(new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc), 0, 1, 0.016f, 0.016f, 1);
        world.CurrentZone = 1; world.CurrentCFCID = 1;
        var actors = new ActorState(); var party = new PartyState(actors);
        Set(world, nameof(WorldState.Party), party);
        var client = (ClientState)RuntimeHelpers.GetUninitializedObject(typeof(ClientState)); client.MoveSpeed = 6;
        Set(world, nameof(WorldState.Client), client);
        var player = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 42; player.HitboxRadius = 0.1f; player.PendingKnockbacks = [];
        party.Members[0] = new(42, 1, false);
        ((Actor?[])party.GetType().GetField("_actors", InternalInstance)!.GetValue(party)!)[0] = player;
        var manager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
        Set(manager, nameof(BossModuleManager.WorldState), world);
        var hints = new AIHints { PathfindMapBounds = new ArenaBoundsSquare(8), ForcedMovement = new Vector3(2, 0, 0) };
        var movement = (MovementOverride)RuntimeHelpers.GetUninitializedObject(typeof(MovementOverride));
        Set(movement, "_navmeshPathIsRunning", new bool[1]);
        var gate = New("Integration.IntegrationPacing", (object?)null);
        var inputs = New("Integration.IntegrationInputObservation");
        Set(movement, "IntegrationPacing", gate); Set(movement, "IntegrationInput", inputs);
        var ai = Blank("AI.AIManager"); var controller = Blank("AI.AIController"); Set(ai, "Controller", controller);
        var amex = (ActionManagerEx)RuntimeHelpers.GetUninitializedObject(typeof(ActionManagerEx));
        var rotation = (BossMod.Autorotation.RotationModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossMod.Autorotation.RotationModuleManager));
        foreach (var field in new (string, object)[] { ("bossmod", manager), ("hints", hints), ("movement", movement), ("ai", ai), ("amex", amex), ("rotation", rotation) })
            Set(coordinator, "<" + field.Item1 + ">P", field.Item2);
        var normalType = Type("Autorotation.MiscAI.NormalMovement");
        var previousNormal = normalType.GetField("Instance")!.GetValue(null);
        var normal = RuntimeHelpers.GetUninitializedObject(normalType);
        normalType.GetField("Instance")!.SetValue(null, normal);
        try
        {
            object Refresh()
            {
                Call(gate, "Clear"); Set(coordinator, "_pendingPace", null); Set(coordinator, "_paceAttempts", 0);
                var stamp = Stopwatch.GetTimestamp(); Set(world, "LiveFrameObservedTimestamp", stamp);
                var map = new Map(); map.InitGrid(1, default, 16, 16);
                var raster = New("Pathfinding.NavigationRaster", map, null, new byte[256]);
                var identity = New("Pathfinding.NavigationIdentity", 101L, instance, frame, world.CurrentTime, DateTimeOffset.UtcNow,
                    stamp, default(WPos), false, 6f, 0f, null, player.InstanceID, world.CurrentZone, world.CurrentCFCID);
                var mapSource = New("Pathfinding.NavigationMapSource", hints.PathfindMapBounds, default(WPos), null, default(Bitmap.Region));
                var score = Enum.Parse(Type("Pathfinding.ThetaStar+Score"), "SafeMaxPrio");
                var decision = new NavigationDecision { Destination = new(2, 0), NextWaypoint = new(2, 0), LeewaySeconds = 5, TimeToGoal = 1f / 3 };
                var built = New("Pathfinding.NavigationPublication", identity, raster, new WPos[] { default, new(2, 0) }, true, false, score, decision, false, mapSource);
                var publication = Call(built, "Publish")!;
                Set(normal, "<PublishedNavigation>k__BackingField", publication);
                var authority = New("Integration.IntegrationMovementDecision", instance, frame, "bmr-normal-movement", player.InstanceID,
                    world.CurrentTime, stamp, default(WPos), new WPos(2, 0), new Vector3(2, 0, 0), publication);
                Set(hints, "IntegrationMovementDecision", authority);
                var scope = (string)Call(coordinator, "ControlScope")!;
                Set(coordinator, "_controlObservation", New("Integration.IntegrationCoordinator+ControlObservation", scope, true, stamp));
                return authority;
            }
            string Request(float scale = 0.5f, int leaseMs = 100, long generation = 101) => IntegrationSession.Serialize(new
            { context, ownerToken = owner, generation, scale, leaseMs });
            JsonDocument Submit(string? request = null) => JsonDocument.Parse((string)Call(coordinator, "RequestPace", request ?? Request())!);
            var authority = Refresh();
            var nativeRequest = New("Integration.IntegrationCoordinator+PaceRequest", context, owner, 101L, 0.5f, 100);
            _ = Call(coordinator, "ValidatePace", nativeRequest, authority); // Warm native distance/JIT work, no mutation.
            Refresh();
            using (var response = Submit()) Check(response.RootElement.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean(),
                "native complete route accepts an out-of-combat pace: " + response.RootElement.GetRawText());
            using (var conflict = Submit()) Check(!conflict.RootElement.GetProperty("available").GetBoolean(), "only one accepted pace can own a frame");
            Call(coordinator, "FinalizePace");
            movement.DesiredDirection = hints.ForcedMovement;
            Call(inputs, "Assign", instance, frame, movement.DesiredDirection);
            Call(coordinator, "ArmPace");
            var assignment = inputs.GetType().GetProperty("CurrentAssignment", InternalInstance)!.GetValue(inputs);
            object Apply() => Call(gate, "Apply", assignment, movement.DesiredDirection, Vector2.Zero, Vector2.UnitX, Vector2.UnitX, true, true, false, false, false)!;
            Check((float)Property(Apply(), "Scale") == 0.5f, "native endpoint proof reaches the actual native input gate only after matching assignment");
            amex.MoveMightInterruptCast = true;
            Check((float)Property(Apply(), "Scale") == 1, "a cast queued after authorization revokes input pacing without waiting for another draw");
            amex.MoveMightInterruptCast = false;
            using (var cleared = JsonDocument.Parse((string)Call(coordinator, "ClearPace", IntegrationSession.Serialize(new { schemaVersion = 1, instanceGeneration = instance, ownerToken = owner }))!))
                Check(cleared.RootElement.GetProperty("accepted").GetBoolean() && (float)Property(Apply(), "Scale") == 1,
                    "native owner cleanup restores input without writing saved configuration");
            Refresh();
            using (var accepted = Submit()) Check(accepted.RootElement.GetProperty("accepted").GetBoolean(), "death fixture obtains a fresh native proof");
            Call(coordinator, "FinalizePace"); Call(coordinator, "ArmPace");
            player.IsDead = true;
            Call(coordinator, "UpdateControls", (object?)null);
            Check((float)Property(Apply(), "Scale") == 1, "independent native framework death observation revokes input without another stage callback");
            player.IsDead = false;

            foreach (var invalid in new[] { Request(leaseMs: 101), Request(leaseMs: 0), Request(scale: 1.1f), Request(scale: 0.3f) })
            {
                Refresh(); using var result = Submit(invalid);
                Check(!result.RootElement.GetProperty("available").GetBoolean(), "native pace rejects invalid lease or speed-increasing scale");
            }
            Refresh(); using (var generation = Submit(Request(generation: 99)))
                Check(!generation.RootElement.GetProperty("available").GetBoolean(), "native pace rejects a mismatched navigation generation");
            Refresh(); Set(session, "<CanContribute>k__BackingField", false);
            using (var failedFrame = Submit()) Check(!failedFrame.RootElement.GetProperty("available").GetBoolean(), "a failed/disabled contribution frame cannot authorize input");
            Set(session, "<CanContribute>k__BackingField", true);
            player.InCombat = true; Refresh();
            using (var combat = Submit()) Check(!combat.RootElement.GetProperty("available").GetBoolean(), "native combat authority excludes optional travel pacing");
            player.InCombat = false; amex.MoveMightInterruptCast = true; Refresh();
            using (var cast = Submit()) Check(!cast.RootElement.GetProperty("available").GetBoolean(), "a newly queued/live cast prevents pacing");
            amex.MoveMightInterruptCast = false; hints.HoldCasts = true; Refresh();
            using (var hold = Submit()) Check(!hold.RootElement.GetProperty("available").GetBoolean(), "native cast hold pressure prevents pacing");
            hints.HoldCasts = false; hints.MaxCastTime = 0; Refresh();
            using (var pressure = Submit()) Check(!pressure.RootElement.GetProperty("available").GetBoolean(), "native cast-departure limits remain authoritative");
            hints.MaxCastTime = float.MaxValue;
            var ally = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor)); ally.InstanceID = 43; ally.InCombat = true;
            party.Members[1] = new(43, 2, false);
            ((Actor?[])party.GetType().GetField("_actors", InternalInstance)!.GetValue(party)!)[1] = ally;
            Refresh(); using (var partyCombat = Submit()) Check(!partyCombat.RootElement.GetProperty("available").GetBoolean(), "a party member in combat excludes optional travel pacing");
            ally.InCombat = false;
            Refresh(); Set(world, "LiveFrameObservedTimestamp", Stopwatch.GetTimestamp() - Stopwatch.Frequency);
            using (var stale = Submit()) Check(!stale.RootElement.GetProperty("available").GetBoolean(), "stale world observations cannot authorize input pacing");
            Refresh(); hints.TemporaryObstacles.Add(new SDCircle(new(1, 0), 0.2f));
            using (var obstacle = Submit()) Check(!obstacle.RootElement.GetProperty("available").GetBoolean(), "current physical/temporary constraints revalidate the complete route");
            hints.TemporaryObstacles.Clear(); Refresh(); hints.ForbiddenZones.Add((new SDCircle(default, 0.5f), world.FutureTime(1.4), 0));
            using (var deadline = Submit()) Check(!deadline.RootElement.GetProperty("available").GetBoolean(), "a zero-progress lease cannot consume a required warning escape deadline");
            hints.ForbiddenZones.Clear(); Refresh();
            using (var accepted = Submit()) Check(accepted.RootElement.GetProperty("accepted").GetBoolean(), "constraint-change fixture obtains a pending proof");
            hints.TemporaryObstacles.Add(new SDCircle(new(1, 0), 0.2f));
            Call(coordinator, "FinalizePace");
            Check(gate.GetType().GetProperty("Current", InternalInstance)!.GetValue(gate) == null,
                "a later callback's new native obstacle prevents a pending proof from reaching input");
            hints.TemporaryObstacles.Clear(); Refresh();
            using (var accepted = Submit()) Check(accepted.RootElement.GetProperty("accepted").GetBoolean(), "a new fixture can acquire a fresh native proof");
            hints.ForcedMovement = Vector3.UnitZ;
            Call(coordinator, "FinalizePace");
            Check(gate.GetType().GetProperty("Current", InternalInstance)!.GetValue(gate) == null,
                "a later native/controller direction change revokes pending pacing before input application");
            // The distinct hold contract may operate during combat, but native route,
            // cast-pressure, authored constraints and input authority remain mandatory.
            hints.ForcedMovement = new Vector3(2, 0, 0); player.InCombat = true;
            string HoldRequest(int leaseMs = 100, long generation = 101) => IntegrationSession.Serialize(new
            { context, ownerToken = owner, generation, leaseMs });
            JsonDocument Hold(string? request = null) => JsonDocument.Parse((string)Call(coordinator, "RequestDepartureHold", request ?? HoldRequest())!);
            bool Accepted(JsonDocument response) => response.RootElement.TryGetProperty("accepted", out var ok) && ok.GetBoolean();
            void ArmHold()
            {
                Call(coordinator, "FinalizePace"); movement.DesiredDirection = hints.ForcedMovement;
                Call(inputs, "Assign", instance, frame, movement.DesiredDirection); Call(coordinator, "ArmPace");
                assignment = inputs.GetType().GetProperty("CurrentAssignment", InternalInstance)!.GetValue(inputs);
            }
            Refresh(); hints.AddForbiddenZone(new SDCircle(default, 0.2f), world.FutureTime(3));
            var holdHints = hints.ForbiddenZones.ToArray(); var heldDirection = hints.ForcedMovement;
            using (var accepted = Hold()) Check(Accepted(accepted) && accepted.RootElement.GetProperty("scale").GetSingle() == 0
                && !accepted.RootElement.GetProperty("castPermissionGranted").GetBoolean(), "combat hold acknowledges pending zero-input permission without cast approval: " + accepted.RootElement.GetRawText());
            Check((float)Property(Apply(), "Scale") == 1, "pending hold cannot suppress input before a native matching assignment");
            Check(holdHints.SequenceEqual(hints.ForbiddenZones) && hints.ForcedMovement == heldDirection,
                "departure hold never removes hazards or overwrites native forced direction");
            ArmHold();
            Check((float)Property(Apply(), "Scale") == 0 && (Vector2)Property(Apply(), "Output") == default,
                "route-validated native hold suppresses only the exact automatic input assignment");
            amex.MoveMightInterruptCast = true;
            Check((float)Property(Apply(), "Scale") == 0, "a newly queued cast does not itself invalidate its safe departure hold");
            amex.MoveMightInterruptCast = false;
            var manual = Call(gate, "Apply", assignment, movement.DesiredDirection, Vector2.UnitY, Vector2.UnitX, Vector2.UnitX,
                true, true, false, false, false)!;
            Check((float)Property(manual, "Scale") == 1 && (Vector2)Property(manual, "Output") == Vector2.UnitX,
                "manual input immediately revokes a hold without changing that input");
            hints.ForbiddenZones.Clear();
            foreach (var invalid in new[] { HoldRequest(0), HoldRequest(101), HoldRequest(generation: 99),
                HoldRequest().Replace("\"leaseMs\":100", "\"leaseMs\":100,\"scale\":0"),
                HoldRequest().Replace("movement-proposed", "hints-ready") })
            {
                Refresh(); using var rejected = Hold(invalid);
                Check(!Accepted(rejected), "hold rejects invalid lease/generation/stage or arbitrary scale fields");
            }
            foreach (var flag in new[] { "ForceCancelCastMechanic", "ForceCancelCastOther", "HoldCasts", "WantJump" })
            {
                Refresh(); var field = typeof(AIHints).GetField(flag)!; field.SetValue(hints, true);
                using (var rejected = Hold()) Check(!Accepted(rejected), "native cast/special movement pressure prevents hold: " + flag);
                field.SetValue(hints, false);
            }
            Refresh(); hints.NoMovementAllowsManualInput = true;
            hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, world.CurrentTime, world.FutureTime(5));
            using (var paused = Hold()) Check(!Accepted(paused), "native automatic pause rejects departure holds");
            hints.NoMovementAllowsManualInput = false; hints.ImminentSpecialMode = default;
            hints.MaxCastTime = 0; Refresh();
            using (var urgentCast = Hold()) Check(!Accepted(urgentCast), "native cast-departure deadline cannot be relaxed by a hold");
            hints.MaxCastTime = float.MaxValue; Refresh();
            hints.AddForbiddenZone(new SDCircle(default, 0.2f), world.FutureTime(0.1));
            using (var urgentHazard = Hold()) Check(!Accepted(urgentHazard), "imminent warning at the held footprint forbids zero progress");
            hints.ForbiddenZones.Clear(); Refresh();
            hints.TemporaryObstacles.Add(new SDIntersection([new SDCircle(new(20, 0), 1)]));
            using (var unknown = Hold()) Check(!Accepted(unknown), "unknown obstacle distance semantics never permit holding");
            hints.TemporaryObstacles.Clear(); Refresh();
            using (var accepted = Hold()) Check(Accepted(accepted), "fresh combat route reacquires a bounded hold");
            hints.ForceCancelCastMechanic = true; Call(coordinator, "FinalizePace");
            Check(gate.GetType().GetProperty("Current", InternalInstance)!.GetValue(gate) == null,
                "a later callback's native cast-cancel requirement revokes a pending hold");
            hints.ForceCancelCastMechanic = false; Refresh();
            using (var accepted = Hold()) Check(Accepted(accepted), "new safe route prepares hold cleanup fixture");
            ArmHold();
            Set(world, nameof(WorldState.Actors), actors);
            using var holdEffects = (IDisposable)New("Integration.IntegrationActionEffects", world);
            Set(coordinator, "_effects", holdEffects);
            Call(coordinator, "BeginWorldUpdate");
            Check((float)Property(Apply(), "Scale") == 1, "native frame retirement restores input before any new world/hint mutation");
            using var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!);
            Check(capabilities.RootElement.GetProperty("families").GetProperty("cast-departure-hold").GetInt32() == 1,
                "departure holds are discoverable independently of travel pacing and general movement preferences");
        }
        finally { normalType.GetField("Instance")!.SetValue(null, previousNormal); }
    }
}
