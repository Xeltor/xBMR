using BossMod;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

static partial class Checks
{
    private static unsafe void NativeHintPublications()
    {
        var (world, actor, _) = ProjectionFixture();
        actor.PendingHPDifferences = []; actor.HPMP = new(100, 100, 0, 0, 0);
        var native = typeof(AIHints).Assembly;
        Type Type(string name) => native.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object obj, string field, object? value) => obj.GetType().GetField(field, BindingFlags.Public | InternalInstance)!.SetValue(obj, value);
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(obj, args);
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => 1), 1000L);
        var hints = new AIHints();
        var manager = (BossModuleManager)Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world);
        var movement = (MovementOverride)Blank("MovementOverride");
        Set(movement, "IntegrationInput", New("Integration.IntegrationInputObservation"));
        Set(movement, "IntegrationPacing", New("Integration.IntegrationPacing", (object?)null));
        var ai = Blank("AI.AIManager"); Set(ai, "Controller", Blank("AI.AIController"));
        var rotation = Blank("Autorotation.RotationModuleManager");
        var zone = Blank("ZoneModuleManager");
        var amex = (ActionManagerEx)Blank("ActionManagerEx");
        var pointer = typeof(ActionManagerEx).GetField("_inst", InternalInstance)!;
        var memory = NativeMemory.AllocZeroed((nuint)Marshal.SizeOf(pointer.FieldType.GetElementType()!));
        try
        {
            pointer.SetValue(amex, Pointer.Box(memory, pointer.FieldType));
            var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session);
            foreach (var field in new (string Name, object Value)[] { ("bossmod", manager), ("zonemod", zone), ("hints", hints),
                ("rotation", rotation), ("amex", amex), ("movement", movement), ("ai", ai) }) Set(coordinator, "<" + field.Name + ">P", field.Value);
            string Context() => IntegrationSession.Serialize(session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!);
            JsonDocument Snapshot() => JsonDocument.Parse((string)Call(coordinator, "GetSnapshot", Context())!);
            JsonDocument Targets() => JsonDocument.Parse((string)Call(coordinator, "GetTargets", Context())!);
            Call(coordinator, "HintsReady");
            using (var empty = Snapshot())
                Check(empty.RootElement.GetProperty("available").GetBoolean() && empty.RootElement.GetProperty("nativeHintGoalCount").GetInt32() == 0
                    && !empty.RootElement.GetProperty("hasNativeHintGoals").GetBoolean() && empty.RootElement.GetProperty("goalZonesEnabled").GetBoolean(),
                    "actual native hints-ready distinguishes enabled empty goals from unknown goal presence");
            hints.GoalZones.Add(_ => 0); hints.ForbidDashes = true;
            Call(coordinator, "HintsReady");
            using (var present = Snapshot())
                Check(present.RootElement.GetProperty("nativeHintGoalCount").GetInt32() == 1 && present.RootElement.GetProperty("hasNativeHintGoals").GetBoolean()
                    && present.RootElement.GetProperty("forbidDashes").GetBoolean()
                    && present.RootElement.GetProperty("nativeHintGoalScope").GetString() == IntegrationHintEvidence.GoalScope,
                    "zero-valued native scorer still publishes exact presence and authored dash restriction");
            hints.GoalZones.Add(_ => 5); // Simulate native rotation adding a later goal.
            Call(session, "Publish", "movement-proposed", (Func<object, string>)(context => IntegrationSession.Serialize(context)));
            using (var later = Snapshot())
                Check(later.RootElement.GetProperty("nativeHintGoalCount").GetInt32() == 1,
                    "later native/consumer goal additions cannot inflate the captured pre-integration prefix");
            hints.GoalZonesEnabled = false;
            using (var disabled = Snapshot())
                Check(disabled.RootElement.GetProperty("nativeHintGoalCount").GetInt32() == 1 && !disabled.RootElement.GetProperty("goalZonesEnabled").GetBoolean(),
                    "goal presence and goal enablement remain independent native observations");
            hints.ForbidDashes = false;
            using (var permittedFlag = Snapshot()) Check(!permittedFlag.RootElement.GetProperty("forbidDashes").GetBoolean(),
                "dash restriction tracks current native hints at the exact requested stage");
            actor.TargetID = actor.InstanceID; actor.AggroPlayer = false;
            hints.PotentialTargets.Add(new(actor, 0, false));
            using (var target = Targets()) Check(!target.RootElement.GetProperty("targets")[0].GetProperty("aggroPlayer").GetBoolean(),
                "targeting the player cannot fabricate native enmity membership");
            actor.TargetID = 999; actor.AggroPlayer = true;
            using (var target = Targets()) Check(target.RootElement.GetProperty("targets")[0].GetProperty("aggroPlayer").GetBoolean()
                && target.RootElement.GetProperty("targets")[0].GetProperty("targetId").GetString() == "3E7",
                "native aggro membership remains true independently of the enemy's current target");
            actor.IsDestroyed = true;
            using (var gone = Targets()) Check(gone.RootElement.GetProperty("targets")[0].GetProperty("aggroPlayer").ValueKind == JsonValueKind.Null,
                "a retained destroyed-actor entry cannot provide current aggro evidence");
            actor.IsDestroyed = false;
            using (var capabilities = JsonDocument.Parse((string)Call(coordinator, "Capabilities")!))
                foreach (var family in new[] { "native-goal-presence", "dash-policy-observation", "target-aggro-observation" })
                    Check(capabilities.RootElement.GetProperty("families").GetProperty(family).GetInt32() == 1,
                        "each requested publication gap has independent capability discovery");
            using (var fresh = Snapshot())
            {
                var before = fresh.RootElement.GetProperty("context").GetRawText();
                Call(session, "BeginFrame", "next", true);
                using var stale = JsonDocument.Parse((string)Call(coordinator, "GetSnapshot", before)!);
                Check(!stale.RootElement.GetProperty("available").GetBoolean(), "old goal/dash observation context cannot become current at the next frame");
            }
            using (var notCaptured = Snapshot()) Check(notCaptured.RootElement.GetProperty("nativeHintGoalCount").ValueKind == JsonValueKind.Null,
                "a frame without a native hint capture preserves unknown instead of inventing an empty list");
        }
        finally { NativeMemory.Free(memory); }
    }
}
