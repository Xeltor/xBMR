using BossMod;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

static partial class Checks
{
    private static unsafe void NativeControllerProvenance()
    {
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        void Set(object obj, string name, object? value) => obj.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        object? Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.GetValue(obj);
        object? Assignment(object inputs) => inputs.GetType().GetProperty("CurrentAssignment", InternalInstance)!.GetValue(inputs);
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 1), 1000L);
        var instance = (string)Field(session, "InstanceGeneration")!;
        Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "controller", 2000), (Action<string>)(_ => { }));
        Call(session, "BeginFrame", "fixture", true);
        var frame = (long)session.GetType().GetProperty("FrameId", InternalInstance)!.GetValue(session)!;
        var world = (WorldState)RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        world.Frame = new(DateTime.UtcNow, 0, 1, 0.016f, 0.016f, 1);
        var actors = new ActorState(); var party = new PartyState(actors);
        Set(world, nameof(WorldState.Party), party);
        var player = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 42;
        party.Members[0] = new(1, 42, false);
        ((Actor?[])Field(party, "_actors")!)[0] = player;
        var hints = new AIHints();
        void AttachGoals() => Set(hints, "IntegrationGoals", New("Integration.IntegrationGoalSet", session, Array.Empty<Func<WPos, float>>()));
        AttachGoals();
        var movement = (MovementOverride)RuntimeHelpers.GetUninitializedObject(typeof(MovementOverride));
        var inputs = New("Integration.IntegrationInputObservation"); Set(movement, "IntegrationInput", inputs);
        var amex = (ActionManagerEx)RuntimeHelpers.GetUninitializedObject(typeof(ActionManagerEx));
        var nativePointer = typeof(ActionManagerEx).GetField("_inst", InternalInstance)!;
        var nativeType = nativePointer.FieldType.GetElementType()!;
        var size = Marshal.SizeOf(nativeType);
        var memory = NativeMemory.AllocZeroed((nuint)size);
        try
        {
            // Owned zeroed fixture memory supplies cast fields only. No game instance,
            // hook, native call or real action execution is involved.
            nativePointer.SetValue(amex, Pointer.Box(memory, nativePointer.FieldType));
            var controller = New("AI.AIController", world, amex, movement);
            var coordinator = RuntimeHelpers.GetUninitializedObject(Type("Integration.IntegrationCoordinator"));
            var manager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
            Set(manager, nameof(BossModuleManager.WorldState), world);
            Set(coordinator, "Session", session);
            Set(coordinator, "<bossmod>P", manager); Set(coordinator, "<hints>P", hints); Set(coordinator, "<movement>P", movement);
            object? navigation = null;
            void Update() => Call(controller, "Update", player, hints, world.CurrentTime, navigation, true);
            object Decision() => Field(hints, "IntegrationControllerDecision")!;
            void Reset() { hints.Clear(); AttachGoals(); movement.UserMove = default; amex.MoveMightInterruptCast = false; Set(controller, "NaviTargetPos", new WPos(12, 0)); }

            Reset(); Update();
            var decision = Decision(); var id = (string)Property(decision, "Id");
            Check(hints.ForcedMovement == new Vector3(12, 0, 0) && (bool)Property(decision, "WroteMovement")
                && (string)Property(decision, "Owner") == "bmr-follow", "actual native follow controller records the direction it wrote");
            Check(Property(decision, "NavigationGeneration") == null, "follow target without a search publication cannot fabricate navigation provenance");
            var identity = New("Pathfinding.NavigationIdentity", 101L, instance, frame - 1, world.CurrentTime.AddMilliseconds(-20), DateTimeOffset.UtcNow,
                System.Diagnostics.Stopwatch.GetTimestamp(), player.Position, false, 6f, 0f, null, player.InstanceID, world.CurrentZone, world.CurrentCFCID);
            navigation = New("Pathfinding.NavigationPublication", identity, null, new WPos[] { default, new(10, 0) }, true, false,
                Enum.Parse(Type("Pathfinding.ThetaStar+Score"), "SafeMaxPrio"), new BossMod.Pathfinding.NavigationDecision { Destination = new(10, 0) }, false, null);
            Reset(); Update(); decision = Decision(); id = (string)Property(decision, "Id");
            Check((long)Property(decision, "NavigationGeneration") == 101 && (long)Property(decision, "NavigationFrameId") == frame - 1
                && (Vector3)Property(decision, "Destination") == new Vector3(12, 0, 0) && (Vector2)Property(decision, "NavigationDestination") == new Vector2(10, 0),
                "final follow target and older two-dimensional search waypoint retain independent identities and geometry");
            using (var observed = JsonDocument.Parse(BossMod.Integration.IntegrationSession.Serialize(Call(coordinator, "ControllerObservation")!)))
                Check(!observed.RootElement.GetProperty("routeSafetyVerified").GetBoolean()
                    && observed.RootElement.GetProperty("navigationDestination").GetArrayLength() == 2,
                    "a recorded controller correction cannot relabel an older search route as currently validated");
            using (var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!))
                Check(capabilities.RootElement.GetProperty("families").GetProperty("controller-input-provenance").GetInt32() == 1,
                    "controller/input provenance is independently advertised by the actual native provider");
            movement.DesiredDirection = hints.EffectiveForcedMovement;
            Call(coordinator, "OverrideAssigned", "native-hints");
            var assigned = Assignment(inputs)!;
            Check((string)Property(assigned, "ControllerDecisionId") == id && (string)Property(assigned, "ControllerOwner") == "bmr-follow",
                "actual native assignment joins only the same-frame controller outcome");
            Call(inputs, "Observe", "walk-input-result", movement.DesiredDirection, Vector2.Zero, Vector2.UnitX, Vector2.UnitX,
                false, false, 1f, null, true, assigned, true);
            using (var sample = JsonDocument.Parse(BossMod.Integration.IntegrationSession.Serialize(Call(coordinator, "InputObservation")!)))
                Check(sample.RootElement.GetProperty("matchingAssignment").GetProperty("controllerDecisionId").GetString() == id
                    && sample.RootElement.GetProperty("automatic").GetBoolean(), "native snapshot preserves actual input-to-assignment identity");
            movement.DesiredDirection = default;
            Call(coordinator, "OverrideAssigned", "native-interaction-suppression");
            var suppressed = Assignment(inputs)!;
            Check(Property(suppressed, "ControllerDecisionId") == null && (string)Property(suppressed, "Reason") == "native-interaction-suppression"
                && (long)Property(suppressed, "Generation") > (long)Property(assigned, "Generation"),
                "interaction suppression creates a distinct assignment without borrowing the walking route");
            movement.DesiredDirection = Vector3.UnitZ;
            Call(coordinator, "ObserveApplicationBoundary");
            Check((string)Property(Assignment(inputs)!, "Reason") == "unattributed-application-boundary"
                && Property(Assignment(inputs)!, "ControllerDecisionId") == null, "changed post-assignment direction is explicitly unattributed");

            Reset(); movement.UserMove = new(1, 0); Update();
            Check(hints.ForcedMovement == null && !(bool)Property(Decision(), "WroteMovement") && (string)Property(Decision(), "Reason") == "manual-input",
                "manual input suppresses native follow without fabricating an assigned controller direction");
            Reset(); amex.MoveMightInterruptCast = true; Update();
            Check(hints.ForcedMovement == null && (string)Property(Decision(), "Reason") == "native-cast-hold",
                "live queued-cast pressure is observed after the native controller's final cast correction");
            Reset(); Set(controller, "NaviTargetPos", null); Update();
            Check((string)Property(Decision(), "Reason") == "no-controller-destination", "native no-destination result remains distinct from a route arrival");
            Reset(); Set(controller, "NaviTargetPos", default(WPos)); Update();
            Check((string)Property(Decision(), "Reason") == "within-destination-tolerance", "native arrival tolerance is observed rather than inferred from an old endpoint");
            Reset(); hints.ForcedMovement = Vector3.UnitZ; Update();
            Check(hints.ForcedMovement == Vector3.UnitZ && !(bool)Property(Decision(), "WroteMovement")
                && (string)Property(Decision(), "Reason") == "other-movement-wins", "authored/other movement retains precedence over follow provenance");
            movement.DesiredDirection = hints.ForcedMovement;
            Call(coordinator, "OverrideAssigned", "native-hints");
            Check(Property(Assignment(inputs)!, "ControllerDecisionId") == null, "a non-writing follow controller cannot claim an unrelated native assignment");
            Reset(); Update(); hints.NoMovementAllowsManualInput = true;
            hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, world.CurrentTime, world.FutureTime(1));
            movement.DesiredDirection = hints.EffectiveForcedMovement;
            Call(coordinator, "OverrideAssigned", "native-hints");
            Check((string)Property(Assignment(inputs)!, "Reason") == "native-movement-pause"
                && Property(Assignment(inputs)!, "ControllerDecisionId") == null, "native pause suppression cannot be mistaken for the preceding follow direction");
            hints.Clear();
            Check(Field(hints, "IntegrationControllerDecision") == null, "new native hints clear controller provenance before the next frame");
        }
        finally { NativeMemory.Free(memory); }
    }
}
