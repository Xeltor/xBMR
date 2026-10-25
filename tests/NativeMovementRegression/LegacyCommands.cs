using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.UI;

public static partial class Checks
{
    private static void LegacyCommands()
    {
        Check(MigrationCommandAlias.TryGetArguments("  /XCAI\tCAMERA   ON ", out var arguments) && arguments == "CAMERA ON",
            "Compatibility dispatch preserves case-insensitive arguments with ordinary whitespace.");
        Check(MigrationCommandAlias.TryGetArguments("/xcai", out arguments) && arguments == ""
            && !MigrationCommandAlias.Matches("/xcai-other on") && !MigrationCommandAlias.Matches("/bmrai on"),
            "Bare legacy toggle is supported without intercepting another command name.");
        foreach (var command in new[] { "/xcai", "/xcai on", "/xcai config", "/xcai camera off", "/xcai unknown" })
        {
            Check(MigrationCommandAlias.CanHandle(-1, true, false, command)
                && !MigrationCommandAlias.CanHandle(0, true, false, command)
                && !MigrationCommandAlias.CanHandle(-1, true, true, command)
                && !MigrationCommandAlias.CanHandle(-1, false, false, command),
                "Every compatibility command yields to an existing handler and standalone ownership, including unknown arguments.");
        }
        foreach (var malformed in new[] { "config extra", "on extra", "camera on", "camera off", "camera toggle", "knockback", "party on extra", "camera on /bmrai on" })
            Check(LegacyAssistanceCommand.Parse(malformed).Kind == LegacyAssistanceCommandKind.Help,
                "Malformed compatibility commands request help without executing a partial command.");

        var root = Path.Combine(Path.GetTempPath(), "native-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new AssistanceSettingsStore(root, Path.Combine(root, "absent.json"));
            var settings = new AssistanceSettingsSession(store);
            var configOpened = 0;
            var nativeReads = 0;
            var recordingReads = 0;
            var messages = new List<string>();
            var commands = new LegacyAssistanceCommands(settings, () => ++configOpened,
                () => { ++nativeReads; return "native current state"; },
                () => { ++recordingReads; return "recording current state"; }, messages.Add, enabled => { settings.Preferences.Enabled = enabled; settings.Active = enabled; }, () => settings.Active);
            try
            {
                commands.Execute("knockback off");
                Check(settings.Preferences.ManageKnockback && messages.Count == 1,
                    "Commands cannot mutate the loading fallback preferences.");
                settings.ObserveStandalone(false);
                Check(SpinWait.SpinUntil(() => { settings.Poll(); return settings.MayEdit; }, 5000),
                    "Compatibility commands use the normal asynchronously loaded native settings.");
                var preferences = settings.Preferences;
                Check(!settings.Active && !settings.MayAssist, "Loading enabled preferences never starts AI.");
                preferences.CombatCamera = true;
                commands.Execute("OFF");
                Check(!preferences.Enabled && settings.MayEdit && !settings.MayAssist,
                    "Legacy off writes the shared assistance master switch and revokes assistance.");
                commands.Execute("");
                Check(preferences.Enabled && settings.MayAssist, "Bare legacy command retains its original toggle behavior.");
                commands.Execute("toggle");
                Check(!preferences.Enabled, "Explicit legacy toggle uses the same shared master switch.");
                commands.Execute("on");
                commands.Execute("KNOCKBACK OFF");
                Check(preferences.Enabled && preferences.CombatCamera && !preferences.ManageKnockback,
                    "Supported feature commands use shared preferences and preserve independently configured camera settings.");
                commands.Execute("camera off");
                Check(preferences.CombatCamera && messages[^1] == LegacyAssistanceCommands.Usage
                    && !LegacyAssistanceCommands.Usage.Contains("camera", StringComparison.Ordinal),
                    "The removed camera subcommand displays help without changing the saved camera preference or advertising itself.");
                commands.Execute("config");
                Check(configOpened == 1, "Legacy config opens the integrated settings.");
                commands.Execute("notes");
                Check(messages[^1] == LegacyAssistanceCommands.Usage && !LegacyAssistanceCommands.Usage.Contains("notes", StringComparison.Ordinal),
                    "The removed notes command displays help without advertising an in-client update log.");
                commands.Execute("check-ipc");
                commands.Execute("check-recording-ipc");
                Check(nativeReads == 1 && recordingReads == 1 && messages.TakeLast(2).SequenceEqual(new[] { "native current state", "recording current state" }),
                    "Historical check commands report current native services without restoring transport probes.");
                foreach (var retired in new[] { "party on", "party off" })
                {
                    commands.Execute(retired);
                    Check(messages[^1].Contains("retired", StringComparison.Ordinal) && preferences.Enabled && preferences.CombatCamera && !preferences.ManageKnockback,
                        "Retired party-follow commands explain their replacement without enabling any behavior.");
                }
                commands.Execute("camera on extra");
                Check(messages[^1] == LegacyAssistanceCommands.Usage, "Invalid arguments display help without mutating saved controls.");
                settings.ObserveStandalone(true);
                commands.Execute("knockback on");
                commands.Execute("off");
                Check(preferences.Enabled && preferences.CombatCamera && !settings.MayAssist,
                    "Standalone coexistence blocks compatibility mutations before any reload finishes.");
            }
            finally { settings.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            var restarted = store.Load(false).Document!.Preferences;
            Check(restarted.Enabled && restarted.CombatCamera && !restarted.ManageKnockback,
                "Compatibility settings persist through the ordinary save queue and restart without another owner.");
        }
        finally { Directory.Delete(root, true); }
    }
}
