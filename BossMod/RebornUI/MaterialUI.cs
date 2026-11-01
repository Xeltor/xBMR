using BossMod.Autorotation;
using Dalamud.Interface;
using RebornMaterial;
using System.IO;

namespace BossMod.RebornUI;

internal static class MaterialUI
{
    private static SettingsWindow? _settings;
    private static readonly List<FoldHost> _folds = [];

    public static void Start(WorldState ws, RotationDatabase rotationDB, RotationModuleManager rotation, CombatAssistance.UI.AssistanceSettingsPage assistance, DirectoryInfo replayDir)
    {
        try
        {
            StartCore(ws, rotationDB, rotation, assistance, replayDir);
        }
        catch (Exception ex)
        {
            Service.Logger.Error(ex, "[RebornUI] Failed to start; using the classic interface instead");
            Stop();
        }
    }

    private static void StartCore(WorldState ws, RotationDatabase rotationDB, RotationModuleManager rotation, CombatAssistance.UI.AssistanceSettingsPage assistance, DirectoryInfo replayDir)
    {
        // Plugin owns the shared Material initialization, frame update and disposal.
        _settings = new(Service.Config, ws, replayDir, rotationDB, assistance);

        var rotationHost = FoldHost.Wrap<UIRotationWindow>("Autorotation", new(400f, 400f), new() { MinimumSize = new(260f, 140f), MaximumSize = new(5000f, 5000f) },
            () => RotationStatus(rotation), ["Edit autorotation presets"]);
        if (rotationHost != null)
        {
            _folds.Add(rotationHost);
        }
    }

    public static bool OpenSettings(string page = "")
    {
        if (_settings == null)
        {
            return false;
        }

        _settings.Open(SettingsWindow.PageForClassicTab(page));
        return true;
    }

    public static bool OpenEncounterSettings(BossModuleRegistry.Info info, WorldState ws)
    {
        if (_settings == null)
        {
            return false;
        }

        _ = new EncounterSettingsWindow(info, ws);
        return true;
    }

    public static void Stop()
    {
        foreach (var fold in _folds)
        {
            Try(fold.Dispose);
        }
        _folds.Clear();
        if (_settings != null)
        {
            Try(_settings.Dispose);
            _settings = null;
        }
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Service.Logger.Error(ex, "[RebornUI] Cleanup failed");
        }
    }

    private static (string, Vector4?) RotationStatus(RotationModuleManager rotation)
    {
        var preset = rotation.Preset;
        return preset == RotationModuleManager.ForceDisable ? ("Force-disabled", M3.Scheme.Error)
            : preset != null ? ($"Running {preset.Name}", M3.Severity(M3Severity.Success))
            : ("No preset active", null);
    }
}
