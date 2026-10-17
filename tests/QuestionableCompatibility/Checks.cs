using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using BossMod;
using BossMod.Autorotation;

static class Checks
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    public static void Run(string dalamudDirectory, string gameDirectory, string questionableDll)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        Configuration(dalamudDirectory);
        Presets(questionableDll);
        Console.WriteLine($"{assertions} Questionable compatibility assertions passed; offline IPC and preset validation, no live gameplay proof.");
    }

    private static void Configuration(string dalamudDirectory)
    {
        var config = new ConfigRoot();
        config.Initialize();
        var execute = typeof(Service).Assembly.GetType("BossMod.IPCConfiguration", true)!.GetMethod("Execute")!;
        var configuration = (ConfigRoot c, IReadOnlyList<string> a, bool s) => (List<string>)execute.Invoke(null, [c, a, s])!;

        // Use Dalamud's actual argument/return conversion, including the older List subscriber.
        var dalamud = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dalamudDirectory, "Dalamud.dll"));
        var channelType = dalamud.GetType("Dalamud.Plugin.Ipc.Internal.CallGateChannel", true)!;
        var channel = Activator.CreateInstance(channelType, ["BossMod.Configuration"])!;
        channelType.GetProperty("Func")!.SetValue(channel,
            (Func<IReadOnlyList<string>, bool, List<string>>)((a, s) => configuration(config, a, s)));
        var invoke = channelType.GetMethod("InvokeFunc", BindingFlags.Instance | BindingFlags.NonPublic)!;
        T Call<T>(IReadOnlyList<string> a, bool save = false)
            => (T)invoke.MakeGenericMethod(typeof(T)).Invoke(channel, [new object[] { a, save }])!;

        config.Get<ZoneModuleConfig>().EnableQuestBattles = false;
        Check(Call<IReadOnlyList<string>>(["cfg", "ZoneModuleConfig", "EnableQuestBattles"]).SequenceEqual(["False"]), "Questionable prefixed read returns the original value");
        var modified = 0;
        using var subscription = config.Modified.Subscribe(() => ++modified);
        Check(Call<IReadOnlyList<string>>(["ZoneModuleConfig", "EnableQuestBattles", "true"]).Count == 0, "VBM collection expression accepted");
        Check(config.Get<ZoneModuleConfig>().EnableQuestBattles && modified == 0, "save=false changes only runtime config");
        Check(Call<List<string>>(new List<string> { "ZoneModuleConfig", "EnableQuestBattles" }).SequenceEqual(["True"]), "legacy BMR List arguments and results still work");
        Check(Call<List<string>>(new List<string> { "CFG", "ZoneModuleConfig", "EnableQuestBattles", "false" }, true).Count == 0, "case-insensitive prefix supports writes");
        Check(!config.Get<ZoneModuleConfig>().EnableQuestBattles && modified == 1, "saved writes retain config notification semantics");
        Check(Call<IReadOnlyList<string>>(["cfg"]).Count > 0, "incomplete request returns a diagnostic");
        Check(Call<IReadOnlyList<string>>(["cfg", "MissingNode", "MissingField"]).Count > 0, "unknown setting returns a diagnostic");
        Check(!config.Get<ZoneModuleConfig>().EnableQuestBattles, "invalid requests leave settings unchanged");
    }

    private static void Presets(string questionableDll)
    {
        // Read the user's real installed presets, rather than maintaining a copy that can drift.
        var questionable = AssemblyLoadContext.Default.LoadFromAssemblyPath(questionableDll);
        var diagnostics = new List<string>();
        Service.LogHandlerDebug = diagnostics.Add;
        foreach (var name in new[] { "Overworld", "QuestBattle", "NormalMovement" })
        {
            diagnostics.Clear();
            using var stream = questionable.GetManifestResourceStream("Questionable.Controller.CombatModules.BossModPreset." + name)
                ?? throw new InvalidOperationException($"Questionable preset resource missing: {name}");
            using var reader = new StreamReader(stream);
            JsonNode node = new JsonArray(JsonNode.Parse(reader.ReadToEnd()));
            foreach (var converter in PlanPresetConverter.PresetSchema.Converters)
                node = converter(node, 0, new FileInfo("<Questionable compatibility test>"));
            var expected = node.AsArray()[0]!;
            var preset = expected.Deserialize<Preset>(Serialization.BuildSerializationOptions())!;
            Check(diagnostics.Count == 0, $"{name} imports without silently dropped modules/options: {string.Join("; ", diagnostics)}");
            Check(preset.Modules.Count == expected["Modules"]!.AsObject().Count, $"{name} keeps every module");
            foreach (var module in preset.Modules)
            {
                var entries = expected["Modules"]![module.Type.FullName!]!.AsArray();
                Check(module.SerializedSettings.Count == entries.Count, $"{name} keeps every setting for {module.Type.Name}");
            }
            var roundtrip = JsonSerializer.SerializeToNode(preset, Serialization.BuildSerializationOptions())!;
            Check(JsonNode.DeepEquals(expected, roundtrip), $"{name} round-trips every module, track, option and target unchanged");
        }
        Service.LogHandlerDebug = null;
    }
}
