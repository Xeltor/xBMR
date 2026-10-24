using BossMod.CombatAssistance.Config;
using System.Text.Json;

public static partial class Checks
{
    private static void SettingsImport()
    {
        var root = Path.Combine(Path.GetTempPath(), "native-assistance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var legacy = Path.Combine(root, "XelsCombatAI.json");
            var directory = Path.Combine(root, "native");
            var store = new AssistanceSettingsStore(directory, legacy);
            const string original = """
                {"Version":30,"Enabled":false,"ManageMovement":false,"ManagePositionals":false,"GapCloserRDM":false,
                 "FixBmrStackSeparation":false,"FixBmrForcedMarchHeading":false,"CombatCamera":true,"ShareFightRecordings":true,
                 "PreferredForbiddenZoneDistance":99,"CameraPanningSpeed":-5,"DecisionOverlayDensity":99,
                 "DisabledEncounterFixes":["saved-opt-out"],"StartRotationSolverAtFateOpener":true,"TrackDungeonParty":true,
                 "UseLearnedKnockbackFallback":true,"AcknowledgedPatchNotes":["old-update"],"PatchNotesBaselineVersion":"3.1.7"}
                """;
            File.WriteAllText(legacy, original);
            var loaded = store.Load(true);
            Check(loaded.State == AssistanceSettingsState.StandaloneLoaded && !loaded.MayAssist && !File.Exists(store.FilePath),
                "Standalone coexistence defers the first import and cannot activate assistance.");
            loaded = store.Load(false);
            var preferences = loaded.Document!.Preferences;
            Check(loaded.State == AssistanceSettingsState.Ready && !loaded.MayAssist && !preferences.Enabled
                && !preferences.ManageMovement && !preferences.ManagePositionals && !preferences.GapCloserRDM
                && !preferences.FixBmrStackSeparation && !preferences.FixBmrForcedMarchHeading,
                "Saved feature and job opt-outs survive the one-time import.");
            Check(preferences.CombatCamera && preferences.ShareFightRecordings && preferences.DisabledEncounterFixes.Contains("saved-opt-out"),
                "Camera, fight sharing and saved compatibility choices survive import.");
            Check(preferences.PreferredForbiddenZoneDistance == 3 && preferences.CameraPanningSpeed == 0.25f
                && preferences.DecisionOverlayDensity == OverlayDensity.Normal, "Legacy clamping runs before committing settings.");
            Check(File.ReadAllText(legacy) == original && loaded.Document.LegacyImport is { Version: 30, Sha256.Length: 64 },
                "Import records source identity while retaining the byte-identical rollback file.");
            using (var json = JsonDocument.Parse(File.ReadAllText(store.FilePath)))
            {
                Check(!json.RootElement.GetProperty("Preferences").TryGetProperty("AcknowledgedPatchNotes", out _)
                    && !json.RootElement.GetProperty("Preferences").TryGetProperty("PatchNotesBaselineVersion", out _),
                    "Retired update-log state is not imported or persisted; the original legacy file is preserved.");
                Check(!json.RootElement.GetProperty("Preferences").TryGetProperty("StartRotationSolverAtFateOpener", out _),
                    "Excluded ToF behavior is not persisted in native preferences.");
                Check(!json.RootElement.GetProperty("Preferences").TryGetProperty("TrackDungeonParty", out _)
                    && typeof(AssistancePreferences).GetProperty("TrackDungeonParty") == null,
                    "Retired between-pull following is discarded even when enabled in legacy settings.");
                Check(!json.RootElement.GetProperty("Preferences").TryGetProperty("UseLearnedKnockbackFallback", out _)
                    && typeof(AssistancePreferences).GetProperty("UseLearnedKnockbackFallback") == null,
                    "Retired unknown-knockback prediction is discarded even when enabled in legacy settings.");
            }
            var integrated = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(store.FilePath));
            integrated["Preferences"]!["TrackDungeonParty"] = true;
            integrated["Preferences"]!["UseLearnedKnockbackFallback"] = true;
            File.WriteAllText(store.FilePath, integrated.ToString());
            loaded = store.Load(false);
            Check(!File.ReadAllText(store.FilePath).Contains("TrackDungeonParty", StringComparison.Ordinal),
                "Repeated startup also retires the field from existing integrated settings without activating following.");
            Check(!File.ReadAllText(store.FilePath).Contains("UseLearnedKnockbackFallback", StringComparison.Ordinal),
                "Repeated startup also retires the unknown-knockback setting while preserving the original standalone file.");
            var receipt = (loaded.Document ?? throw new InvalidOperationException(loaded.Detail)).LegacyImport;
            File.WriteAllText(legacy, "{\"Version\":30,\"Enabled\":true,\"GapCloserRDM\":true}");
            loaded = store.Load(false);
            Check(!loaded.Document!.Preferences.Enabled && !loaded.Document.Preferences.GapCloserRDM && loaded.Document.LegacyImport == receipt,
                "Existing integrated choices take precedence over subsequent legacy changes.");
            var saved = File.ReadAllText(store.FilePath);
            loaded = store.Load(true, _ => throw new InvalidOperationException("Must not reconcile while standalone is loaded."));
            Check(loaded.State == AssistanceSettingsState.StandaloneLoaded && File.ReadAllText(store.FilePath) == saved,
                "Coexistence does not mutate existing settings or restore another plugin's journal.");

            var recoveryStore = new AssistanceSettingsStore(Path.Combine(root, "recovery"), legacy);
            File.WriteAllText(legacy, """
                {"Version":30,"Enabled":true,"BmrHallwayModulesToRestore":[16858],
                 "BmrHallwayRestoreUserRevisions":{"16858":7},"BmrHallwayRestoreGeneration":"old-provider"}
                """);
            loaded = recoveryStore.Load(false);
            Check(loaded.State == AssistanceSettingsState.RecoveryPending && !loaded.MayAssist && File.Exists(recoveryStore.FilePath),
                "Persistent restoration journal is durably transferred before its old owner is retired.");
            File.WriteAllText(legacy, "{\"Version\":30}");
            loaded = recoveryStore.Load(false, config =>
            {
                Check(config.BmrHallwayRestoreUserRevisions[16858] == 7 && config.BmrHallwayRestoreGeneration == "old-provider",
                    "Interrupted import resumes the original recovery evidence without reimporting legacy settings.");
                return false;
            });
            Check(loaded.State == AssistanceSettingsState.RecoveryPending, "Unverifiable restoration cannot silently become ready.");
            loaded = recoveryStore.Load(false, config => { config.BmrHallwayModulesToRestore.Clear(); return true; });
            Check(loaded.MayAssist && loaded.Document!.Preferences.BmrHallwayRestoreGeneration == null,
                "Completed journal reconciliation enables assistance without changing native AI activation.");
            loaded.Document!.Preferences.BmrHallwayModulesToRestore.Add(16858);
            loaded.Document.Preferences.BmrHallwayRestoreUserRevisions[16858] = 8;
            loaded.Document.Preferences.ResetAll();
            Check(loaded.Document.Preferences.BmrHallwayModulesToRestore.SequenceEqual([16858u]), "Reset retains outstanding recovery journals.");

            var oldStore = new AssistanceSettingsStore(Path.Combine(root, "old"), legacy);
            File.WriteAllText(legacy, "{\"Version\":12,\"ManageTrueNorthInRsr\":false,\"ManageHealerAoePositioning\":false}");
            loaded = oldStore.Load(false);
            Check(loaded.Document is { Preferences.Version: 30 } && !loaded.Document.Preferences.ManageTrueNorth
                && !loaded.Document.Preferences.ManageHealerCoverageZone && !loaded.Document.Preferences.Enabled,
                "Original private JSON aliases and version migrations run before import; absent legacy master switch remains disabled.");
            var invalidStore = new AssistanceSettingsStore(Path.Combine(root, "invalid"), legacy);
            File.WriteAllText(legacy, "{\"Version\":999}");
            Check(invalidStore.Load(false).State == AssistanceSettingsState.Unavailable && !File.Exists(invalidStore.FilePath),
                "A future legacy version is retained without downgrading or committing defaults.");
            File.WriteAllText(store.FilePath, "{\"FormatVersion\":999}");
            Check(store.Load(false).State == AssistanceSettingsState.Unavailable && File.ReadAllText(store.FilePath) == "{\"FormatVersion\":999}",
                "Unsupported integrated settings are never replaced by a legacy import.");

            var defaults = new AssistanceSettingsStore(Path.Combine(root, "defaults"), Path.Combine(root, "absent.json")).Load(false);
            Check(defaults.MayAssist && defaults.Document!.LegacyImport == null, "Fresh defaults enable assistance but do not own native activation.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
