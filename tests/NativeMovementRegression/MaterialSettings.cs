using BossMod;
using BossMod.AI;
using BossMod.Autorotation;
using BossMod.CombatAssistance.UI;
using System.Text.Json;

public static partial class Checks
{
    private static void MaterialSettings()
    {
        AutomovementVisibility();
        var autorotation = GeneratedConfigMetadata.Get<AutorotationConfig>();
        var status = autorotation.FieldsByName[nameof(AutorotationConfig.ShowDTR)];
        Check(status.Radio && !autorotation.FieldsByName[nameof(AutorotationConfig.ShowUI)].Radio,
            "Generated config metadata distinguishes radio choices from ordinary controls.");
        var rotation = new AutorotationConfig();
        status.Setter(rotation, AutorotationConfig.DtrStatus.Icon);
        Check((AutorotationConfig.DtrStatus)status.Getter(rotation)! == AutorotationConfig.DtrStatus.Icon,
            "Material radio metadata keeps the native enum getter and setter.");

        var ai = GeneratedConfigMetadata.Get<AIConfig>();
        Check(ai.DisplayFields.Length == 0 && ai.SerializableFields.Any(field => field.Name == nameof(AIConfig.ForbidMovement)),
            "The general settings renderer preserves hidden native AI controls and their saved values.");
        var native = new AIConfig { ForbidMovement = true, FollowOutOfCombat = false, UseCombatPositioningRange = false };
        var loadedNative = new AIConfig();
        RoundTrip(native, loadedNative);
        Check(loadedNative.ForbidMovement && !loadedNative.FollowOutOfCombat && !loadedNative.UseCombatPositioningRange,
            "Native AI restrictions and combat-range opt-outs survive the settings upgrade.");

        // Resolve through ConfigNode so this suite does not acquire a separate UI-library dependency.
        var interfaceType = typeof(ConfigNode).Assembly.GetType("BossMod.RebornUI.InterfaceConfig", throwOnError: true)!;
        var metadata = GeneratedConfigMetadata.Get(interfaceType);
        var source = (ConfigNode)Activator.CreateInstance(interfaceType)!;
        metadata.FieldsByName["TextScale"].Setter(source, 1.25f);
        metadata.FieldsByName["CompactSettings"].Setter(source, true);
        var restored = (ConfigNode)Activator.CreateInstance(interfaceType)!;
        RoundTrip(source, restored);
        Check((float)metadata.FieldsByName["TextScale"].Getter(restored)! == 1.25f
            && (bool)metadata.FieldsByName["CompactSettings"].Getter(restored)!,
            "The shared Material interface settings round-trip through generated native configuration.");

        static void RoundTrip(ConfigNode source, ConfigNode destination)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                source.Serialize(writer, options);
            using var json = JsonDocument.Parse(stream.ToArray());
            destination.Deserialize(json.RootElement, options);
        }
    }

    private static void AutomovementVisibility()
    {
        var native = new AIConfig { DrawUI = true, ForbidMovement = true, FollowOutOfCombat = false, FollowSlot = 3 };
        var opens = 0;
        var closes = 0;
        var saves = 0;
        using var saved = native.Modified.Subscribe(() => ++saves);
        var visibility = new AutomovementSettingsVisibility(native, () => ++opens, () => ++closes);
        Check(opens == 1 && closes == 0 && saves == 0,
            "Saved AI-menu visibility restores Automovement without rewriting settings.");
        native.Modified.Fire();
        Check(opens == 1 && closes == 0,
            "Unrelated native setting edits do not steal focus or reopen Automovement.");
        visibility.Publish(false);
        Check(!native.DrawUI && closes == 0 && saves == 2,
            "Navigating away publishes hidden state without closing the shared settings window recursively.");
        visibility.Publish(false);
        Check(saves == 2, "Unchanged page visibility does not write settings every frame.");
        native.DrawUI = true;
        native.Modified.Fire();
        Check(opens == 2 && closes == 0, "The native menu command reopens Automovement through the existing visibility setting.");
        native.DrawUI = false;
        native.Modified.Fire();
        Check(opens == 2 && closes == 1, "The native menu command hides the active Automovement page.");
        visibility.Publish(true);
        Check(opens == 2 && native.DrawUI, "Opening the page through main navigation does not recursively reopen it.");
        Check(native.ForbidMovement && !native.FollowOutOfCombat && native.FollowSlot == 3,
            "Opening, hiding and switching settings pages preserve movement restrictions and follow preferences.");
        visibility.Dispose();
        native.DrawUI = false;
        native.Modified.Fire();
        Check(opens == 2 && closes == 1, "Disposing the settings host detaches visibility callbacks before a reload.");
    }
}
