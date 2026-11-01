using BossMod;
using BossMod.AI;
using BossMod.Autorotation;
using System.Text.Json;

public static partial class Checks
{
    private static void MaterialSettings()
    {
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
}
