using BossMod.AI;
using BossMod.CombatAssistance.Config;

namespace BossMod.CombatAssistance.UI;

internal sealed record PluginConnectionRow(string Name, string Status, bool NeedsAttention = false);

internal partial class AssistanceSettingsPage
{
    private AIConfig NativeConfig => Service.Config.Get<AIConfig>();
    private static readonly AIConfig NativeDefaults = new();

    private void NativeChange(Action change)
    {
        change();
        this.NativeConfig.Modified.Fire();
    }

    internal void OpenFallback() => _ = new FallbackWindow(this);

    // Used only when the main settings host failed to initialize; shares all content.
    private sealed class FallbackWindow(AssistanceSettingsPage page)
        : RebornUI.RebornWindow("Automovement###BMRConfigAutomovement", true, new(980, 680))
    {
        protected override RebornMaterial.M3Density Density => SettingsDensity;

        public override void PreOpenCheck()
        {
            if (!page.NativeConfig.DrawUI) IsOpen = false;
        }

        public override void Draw() => page.Draw();

        public override void OnClose()
        {
            page.OnHidden();
            if (page.NativeConfig.DrawUI)
                page.NativeChange(() => page.NativeConfig.DrawUI = false);
            base.OnClose();
        }
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
