using BossMod.AI;
using BossMod.CombatAssistance.Config;

namespace BossMod.CombatAssistance.UI;

internal sealed record PluginConnectionRow(string Name, string Status, bool NeedsAttention = false);

internal partial class ConfigWindow
{
    private AIConfig NativeConfig => Service.Config.Get<AIConfig>();
    private static readonly AIConfig NativeDefaults = new();

    private void NativeChange(Action change)
    {
        change();
        this.NativeConfig.Modified.Fire();
    }

    private static string BuildDiagnosticText(AIManager manager, AssistanceSettingsSession settings)
    {
        var plan = manager.Autorot.MovementPlanning.CurrentPlan;
        return $"xBMR combat assistance\nSettings: {settings.Current.State}\n{settings.Current.Detail}\n"
            + $"Following: {manager.Beh != null}\nPlanner: {plan?.Status.ToString() ?? "idle"}\n"
            + $"Destination: {plan?.UsefulDestination}\nBound: {plan?.Bound}\n"
            + $"Evidence: {plan?.Evidence}\nFailure: {manager.Autorot.MovementPlanning.LastFailure}";
    }
}
