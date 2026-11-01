using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using BossMod;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Runtime;

static partial class Checks
{
    private static void QuestionableControl(string questionableDll)
    {
        var active = false;
        var changes = new List<bool>();
        var control = new QuestionableAiControl(() => active, value => { active = value; changes.Add(value); });
        var preferences = new AssistancePreferences { ManageMovement = false, LinkRotationSolverToggle = false };
        Check(!preferences.QuestionableControlsAi, "Questionable control is opt-in");
        control.Observe(true, true, true);
        Check(!active && !control.OwnsActivation, "Loading during an existing run never activates AI");
        control.Observe(true, true, null);
        control.Observe(true, true, true);
        Check(!active, "Unknown state cannot manufacture a new run");
        control.Observe(true, true, false);
        control.Observe(true, true, true);
        Check(active && control.OwnsActivation && changes.SequenceEqual([true]), "An observed new run starts native AI once");
        control.Observe(true, true, true);
        Check(changes.Count == 1 && control.Start("Questionable - Quest Battles") && changes.Count == 1,
            "Steady running and preset changes do not restart native AI");
        control.ReleaseOwnership();
        control.Observe(true, true, false);
        Check(active && changes.Count == 1, "Questionable stop preserves a manual takeover");
        active = false;
        control.Observe(true, true, true);
        control.Observe(true, true, null);
        Check(!active && changes.SequenceEqual([true, true, false]), "Provider loss stops only its owned activation");
        control.Observe(true, true, false);
        control.Observe(true, true, true);
        control.Observe(false, true, true);
        Check(!active && !control.Start("Questionable"), "Revoking permission stops owned AI and rejects starts");
        control.Observe(true, true, false);
        control.Observe(true, true, true);
        control.Observe(true, false, true);
        Check(!active && !control.Start(), "Settings readiness remains authoritative");
        control.Observe(true, true, true);
        Check(!active, "Recovery does not manufacture a fresh run");
        Check(!control.Start("Other preset"), "Unknown preset requests cannot change native policy");
        Check(!QuestionableAiControl.IsQuestionableCaller(), "Ordinary clients are not attributed to Questionable");
        Check(control.CreatePreset("Questionable", false) && !control.CreatePreset("Questionable", false)
            && control.CreatePreset("Questionable", true) && !control.CreatePreset("Other preset", true), "Virtual preset overwrite and name semantics");
        using (var json = JsonDocument.Parse(control.GetPreset("Questionable")!))
            Check(json.RootElement.GetProperty("Modules").EnumerateObject().Count() == 0, "Virtual preset contains no imported policy");
        Check(control.DeletePreset("Questionable") && control.GetPreset("Questionable") == null
            && !control.DeletePreset("Questionable"), "Virtual preset deletion is isolated");
        Check(!control.BlocksConfiguration(["cfg", "ZoneModuleConfig", "EnableQuestBattles"])
            && control.BlocksConfiguration(["cfg", "ZoneModuleConfig", "EnableQuestBattles", "true"])
            && control.BlocksConfiguration(["ActionTweaksConfig", "PreventMovingWhileCasting", "false"])
            && control.BlocksConfiguration(["NewNode", "NewSetting", "true"]), "Reads retain their contract; all configuration writes are denied");
        Check(control.BlocksCompatibilityCommand("/vbm", ["clear-maps"])
            && !control.BlocksCompatibilityCommand("/bmr", ["clear-maps"]), "Compatibility setup commands stay separate from ordinary commands");
        var saved = JsonSerializer.Serialize(preferences);
        var loaded = JsonSerializer.Deserialize<AssistancePreferences>(saved)!;
        Check(!loaded.ManageMovement && !loaded.LinkRotationSolverToggle && !loaded.QuestionableControlsAi,
            "Existing feature opt-outs and the default permission survive serialization");
        loaded.QuestionableControlsAi = true;
        loaded = JsonSerializer.Deserialize<AssistancePreferences>(JsonSerializer.Serialize(loaded))!;
        Check(loaded.QuestionableControlsAi && !loaded.ManageMovement, "Explicit permission persists independently of feature choices");
        loaded.ResetAll();
        Check(!loaded.QuestionableControlsAi, "Reset revokes external control");
        InstalledQuestionableProtocol(questionableDll);
    }

    private static void InstalledQuestionableProtocol(string questionableDll)
    {
        // Invoke the installed provider's methods with inert subscribers. No game,
        // quest, installed plugin state, preset file or real command is mutated.
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(questionableDll);
        var type = assembly.GetType("Questionable.External.BossModIpc", true)!;
        var instance = RuntimeHelpers.GetUninitializedObject(type);
        var active = false;
        var control = new QuestionableAiControl(() => active, value => active = value);
        control.Observe(true, true, true);
        var native = new ConfigRoot();
        native.Initialize();
        native.Get<ZoneModuleConfig>().EnableQuestBattles = false;
        var presetCalls = 0;
        var blockedCommands = new List<string>();
        var blockedWrites = 0;
        var configType = assembly.GetType("Questionable.Configuration", true)!;
        var config = Activator.CreateInstance(configType, nonPublic: true)!;
        var advanced = configType.GetProperty("Advanced")!.GetValue(config)!;
        advanced.GetType().GetProperty("BMRestoreMovement")!.SetValue(advanced, true);
        var execute = typeof(Service).Assembly.GetType("BossMod.IPCConfiguration", true)!.GetMethod("Execute")!;
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (field.FieldType == configType) { field.SetValue(instance, config); continue; }
            if (!field.FieldType.IsInterface) continue;
            var proxy = DispatchProxy.Create(field.FieldType, typeof(QuestionableSubscriber));
            ((QuestionableSubscriber)proxy).Handle = (method, args) =>
            {
                Check(control.IsCaller, $"Installed Questionable caller is attributable at {field.Name}.{method.Name}");
                if (method.Name == "get_HasFunction") return true;
                if (method.Name == "ProcessCommand")
                {
                    var command = (string)args![0]!;
                    blockedCommands.Add(command);
                    if (command == "/vbmai off") control.Stop();
                    else Check(control.BlocksCompatibilityCommand(command.Split(' ')[0], command.Split(' ').Skip(1).ToArray()), "Questionable setup command cannot change native state");
                    return true;
                }
                if (method.Name != "InvokeFunc") throw new InvalidOperationException(method.Name);
                switch (field.Name)
                {
                    case "_getPreset": return control.GetPreset((string)args![0]!);
                    case "_createPreset":
                        using (var payload = JsonDocument.Parse((string)args![0]!))
                            return control.CreatePreset(payload.RootElement.GetProperty("Name").GetString()!, (bool)args[1]!);
                    case "_deletePreset": return control.DeletePreset((string)args![0]!);
                    case "_getActivePreset": return control.ActivePreset;
                    case "_setPreset": ++presetCalls; return control.Start((string)args![0]!);
                    case "_clearPreset": return control.Stop();
                    case "_configuration":
                        var request = (IReadOnlyList<string>)args![0]!;
                        if (control.BlocksConfiguration(request)) { ++blockedWrites; return new List<string> { "On/off only" }; }
                        return execute.Invoke(null, new object[] { native, request, args[1]! });
                    default: throw new InvalidOperationException(field.Name);
                }
            };
            field.SetValue(instance, proxy);
        }
        var presetType = type.GetNestedType("EPreset")!;
        object Preset(string value) => Enum.Parse(presetType, value);
        void Invoke(string name, params object[] args) => type.GetMethods().Single(m => m.Name == name
            && m.GetParameters().Length == args.Length && m.GetParameters().Zip(args).All(x => x.First.ParameterType.IsInstanceOfType(x.Second))).Invoke(instance, args);
        Invoke("AddAllPresets", true);
        Check(!active, "Questionable preset preparation alone cannot activate AI");
        Invoke("SetActivePreset", Preset("Overworld"));
        Check(active && control.OwnsActivation && control.ActivePreset == "Questionable", "Installed combat start enables native AI without importing its preset");
        Invoke("SetPresetForSoloDuty", Preset("QuestBattle"));
        Check(active && control.ActivePreset == "Questionable - Quest Battles" && blockedWrites == 1
            && !native.Get<ZoneModuleConfig>().EnableQuestBattles, "Installed solo-duty start preserves xBMR configuration");
        Invoke("SetActivePreset", Preset("NormalMovement"));
        Check(active && control.ActivePreset == "Questionable - Normal Movement" && presetCalls == 3,
            "Installed normal-movement transition remains an on request, with native movement policy");
        Invoke("Cleanup");
        Check(!active && control.ActivePreset == null && blockedCommands.Any(c => c.Contains("EnableQuestBattles false")),
            "Installed cleanup stops owned AI and cannot restore foreign configuration values");
        active = true;
        Invoke("Cleanup");
        Check(active, "Installed cleanup preserves AI started manually");
        control.Observe(false, true, true);
        Invoke("SetPresetForSoloDuty", Preset("QuestBattle"));
        Check(active && !control.OwnsActivation && control.ActivePreset == null && blockedWrites == 2
            && !native.Get<ZoneModuleConfig>().EnableQuestBattles,
            "Revoked permission still isolates Questionable policy and preserves manual activation");
        control.Observe(true, true, true);
        active = false; // The user turns AI off during this same run.
        control.ReleaseOwnership();
        control.Observe(true, true, true);
        Check(!active, "Steady run polling respects a manual off");
        Invoke("SetActivePreset", Preset("Overworld"));
        Check(active && control.OwnsActivation, "A fresh explicit start can request control later in the active run");
        control.Observe(true, true, false);
        Check(!active, "Public run stop releases owned activation");
        Console.WriteLine($"Installed Questionable protocol: {assembly.GetName().Version}; MVID={assembly.ManifestModule.ModuleVersionId}");
    }
}

public class QuestionableSubscriber : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handle = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handle(targetMethod!, args);
}
