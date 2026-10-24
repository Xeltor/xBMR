using BossMod.CombatAssistance.Config;

namespace BossMod.AI;

// Keep the upstream window identity and native visibility setting. Material styling
// belongs to the window scope, while the content lives in combat assistance.
sealed class AIManagementWindow : CombatAssistance.UI.ConfigWindow
{
    private static readonly AIConfig Config = Service.Config.Get<AIConfig>();
    private readonly EventSubscriptions _subscriptions;

    public AIManagementWindow(AIManager manager, AssistanceSettingsSession settings) : base(manager, settings)
    {
        _subscriptions = new(Config.Modified.ExecuteAndSubscribe(() => IsOpen = Config.DrawUI));
    }

    public void SetVisible(bool visible)
    {
        if (Config.DrawUI == visible) return;
        Config.DrawUI = visible;
        Config.Modified.Fire();
    }

    public void UpdateTitle() => WindowName = "Combat assistance###AI debug window";

    public override void OnClose()
    {
        base.OnClose();
        SetVisible(false);
    }

    protected override void Dispose(bool disposing)
    {
        SaveWindowPosition();
        _subscriptions.Dispose();
        base.Dispose(disposing);
    }
}
