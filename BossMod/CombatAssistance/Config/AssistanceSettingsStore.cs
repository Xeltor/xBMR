using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;

namespace BossMod.CombatAssistance.Config;

internal enum AssistanceSettingsState { Ready, StandaloneLoaded, RecoveryPending, Unavailable }
internal sealed record LegacySettingsReceipt(string Sha256, int Version, DateTime ImportedUtc);
internal sealed record LegacyRecoveryReconciliation(uint ModuleOid, bool SavedEnabled, string? OldGeneration, long? OldUserRevision, DateTime ReconciledUtc);
internal sealed class AssistanceSettingsDocument
{
    public int FormatVersion { get; set; } = 1;
    public AssistancePreferences Preferences { get; set; } = new();
    public LegacySettingsReceipt? LegacyImport { get; set; }
    public List<LegacyRecoveryReconciliation> RecoveryReconciliation { get; set; } = [];
}
internal sealed record AssistanceSettingsLoad(AssistanceSettingsState State, AssistanceSettingsDocument? Document, string Detail)
{
    internal bool MayAssist => State == AssistanceSettingsState.Ready && Document?.Preferences.Enabled == true;
}

// Only call during initialization, explicit save, or a standalone-plugin unload transition.
// Native BMR configuration nodes are not serialized here and loading cannot activate AI.
internal sealed class AssistanceSettingsStore(string directory, string legacyFile)
{
    internal string FilePath => Path.Combine(directory, "combat-assistance.json");
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        Formatting = Formatting.Indented,
        Culture = System.Globalization.CultureInfo.InvariantCulture
    };

    internal AssistanceSettingsLoad Load(bool standaloneLoaded, Func<AssistancePreferences, bool>? reconcileRecovery = null)
    {
        try
        {
            var existing = File.Exists(FilePath);
            if (standaloneLoaded && !existing)
                return new(AssistanceSettingsState.StandaloneLoaded, null, "Waiting for standalone XCAI to unload before importing settings.");
            AssistanceSettingsDocument document;
            if (existing)
            {
                var json = JObject.Parse(File.ReadAllText(FilePath));
                if ((int?)json[nameof(AssistanceSettingsDocument.FormatVersion)] != 1 || json[nameof(AssistanceSettingsDocument.Preferences)] is not JObject preferences)
                    return new(AssistanceSettingsState.Unavailable, null, "Unsupported integrated settings format; original file retained.");
                document = json.ToObject<AssistanceSettingsDocument>(JsonSerializer.Create(Settings))
                    ?? throw new JsonSerializationException("Missing integrated settings.");
                Prepare(document.Preferences, (int?)preferences[nameof(AssistancePreferences.Version)] ?? 0);
            }
            else if (File.Exists(legacyFile))
            {
                var bytes = File.ReadAllBytes(legacyFile);
                var json = JObject.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                var version = (int?)json[nameof(AssistancePreferences.Version)] ?? 0;
                var preferences = json.ToObject<AssistancePreferences>(JsonSerializer.Create(Settings))
                    ?? throw new JsonSerializationException("Missing legacy preferences.");
                // Old missing master switch meant disabled. New-install defaults must not replace that opt-out.
                if (json[nameof(AssistancePreferences.Enabled)] == null)
                    preferences.Enabled = false;
                Prepare(preferences, version);
                document = new() { Preferences = preferences, LegacyImport = new(Convert.ToHexStringLower(SHA256.HashData(bytes)), version, DateTime.UtcNow) };
            }
            else
            {
                document = new();
                Prepare(document.Preferences, document.Preferences.Version);
            }
            if (standaloneLoaded)
                return new(AssistanceSettingsState.StandaloneLoaded, document, "Standalone XCAI is loaded; integrated assistance is inactive.");

            // Establish ownership durably before any restoration callback changes native settings.
            // An interrupted migration resumes from this journal and never reimports the legacy file.
            if (!existing)
                Save(document);
            var pending = document.Preferences.BmrHallwayModulesToRestore.Count != 0;
            if (pending)
            {
                if (reconcileRecovery == null)
                    return new(AssistanceSettingsState.RecoveryPending, document, "Legacy restoration journal retained; assistance awaits reconciliation.");
                var reconciled = reconcileRecovery(document.Preferences);
                Save(document); // retain partial progress as well as successful reconciliation
                if (!reconciled)
                    return new(AssistanceSettingsState.RecoveryPending, document, "Legacy restoration journal retained; assistance awaits reconciliation.");
            }
            if (pending && document.Preferences.BmrHallwayModulesToRestore.Count != 0)
                return new(AssistanceSettingsState.RecoveryPending, document, "Recovery callback did not reconcile every journal entry.");
            Save(document);
            return new(AssistanceSettingsState.Ready, document, document.LegacyImport == null ? "Integrated settings ready." : "Legacy settings imported; original retained for rollback.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OverflowException)
        {
            return new(AssistanceSettingsState.Unavailable, null, error.Message);
        }
    }

    private static void Prepare(AssistancePreferences preferences, int version)
    {
        if (version is < 0 or > 30)
            throw new JsonSerializationException("Unsupported preference version; file retained without modification.");
        preferences.Version = version;
        preferences.Migrate();
        preferences.Clamp();
        preferences.BmrHallwayModulesToRestore ??= [];
        preferences.BmrHallwayRestoreUserRevisions ??= [];
        preferences.BmrHallwayModulesToRestore = preferences.BmrHallwayModulesToRestore.Distinct().ToList();
        if (preferences.BmrHallwayModulesToRestore.Count == 0)
        {
            preferences.BmrHallwayRestoreUserRevisions.Clear();
            preferences.BmrHallwayRestoreGeneration = null;
        }
    }

    internal void Save(AssistanceSettingsDocument document)
    {
        if (document.FormatVersion != 1)
            throw new ArgumentException("Unsupported settings format.", nameof(document));
        Prepare(document.Preferences, document.Preferences.Version);
        Directory.CreateDirectory(directory);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(document, Settings));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
