using BossMod.CombatAssistance.Config;

namespace BossMod.CombatAssistance.Runtime;

internal enum LegacyAssistanceCommandKind { Help, Enabled, Toggle, Config, Knockback, RetiredParty, NativeStatus, RecordingStatus }
internal readonly record struct LegacyAssistanceCommand(LegacyAssistanceCommandKind Kind, bool Value = false)
{
    internal static LegacyAssistanceCommand Parse(string arguments)
    {
        var parts = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return new(LegacyAssistanceCommandKind.Toggle);
        if (parts.Length == 1)
            return parts[0].ToLowerInvariant() switch
            {
                "on" => new(LegacyAssistanceCommandKind.Enabled, true),
                "off" => new(LegacyAssistanceCommandKind.Enabled, false),
                "toggle" => new(LegacyAssistanceCommandKind.Toggle),
                "config" => new(LegacyAssistanceCommandKind.Config),
                "check-ipc" => new(LegacyAssistanceCommandKind.NativeStatus),
                "check-recording-ipc" => new(LegacyAssistanceCommandKind.RecordingStatus),
                _ => new(LegacyAssistanceCommandKind.Help)
            };
        if (parts.Length != 2 || !(parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
            return new(LegacyAssistanceCommandKind.Help);
        var value = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
        return parts[0].ToLowerInvariant() switch
        {
            "knockback" => new(LegacyAssistanceCommandKind.Knockback, value),
            "party" => new(LegacyAssistanceCommandKind.RetiredParty, value),
            _ => new(LegacyAssistanceCommandKind.Help)
        };
    }
}

// Compatibility for the old assistance switches, not another AI activation owner.
// Shared preferences and the normal save queue are also used by the Material UI.
internal sealed class LegacyAssistanceCommands(AssistanceSettingsSession settings, Action openConfig,
    Func<string> nativeStatus, Func<string> recordingStatus, Action<string> print)
{
    internal const string Usage = "Usage: /xcai [on|off|toggle|config|knockback on|knockback off|check-ipc|check-recording-ipc]";

    internal void Execute(string arguments)
    {
        var command = LegacyAssistanceCommand.Parse(arguments);
        switch (command.Kind)
        {
            case LegacyAssistanceCommandKind.Config:
                openConfig();
                return;
            case LegacyAssistanceCommandKind.NativeStatus:
                print(nativeStatus());
                return;
            case LegacyAssistanceCommandKind.RecordingStatus:
                print(recordingStatus());
                return;
            case LegacyAssistanceCommandKind.RetiredParty:
                print("Automatic tank following between pulls has been retired. Use /bmrai follow to select a native follow leader.");
                return;
            case LegacyAssistanceCommandKind.Help:
                print(Usage);
                return;
        }
        if (!settings.MayEdit)
        {
            print(settings.Current.Detail);
            return;
        }
        var preferences = settings.Preferences;
        switch (command.Kind)
        {
            case LegacyAssistanceCommandKind.Enabled:
            case LegacyAssistanceCommandKind.Toggle:
                preferences.Enabled = command.Kind == LegacyAssistanceCommandKind.Toggle ? !preferences.Enabled : command.Value;
                print($"Combat assistance: {(preferences.Enabled ? "on" : "off")}. Native AI activation is controlled by /bmrai.");
                break;
            case LegacyAssistanceCommandKind.Knockback:
                preferences.ManageKnockback = command.Value;
                print($"Knockback immunity management: {(preferences.ManageKnockback ? "on" : "off")}");
                break;
        }
        settings.Save();
    }
}
