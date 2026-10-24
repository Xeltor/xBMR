using BossMod.CombatAssistance.Runtime;
using Dalamud.Bindings.ImGui;
using RebornMaterial;

namespace BossMod.CombatAssistance.UI;

// WindowSystem owns the invisible popup host. A real ImGui modal prevents the
// completion notice being lost behind other plugin windows or dismissed by timeout.
internal sealed class LegacyXcaiMigrationWindow : UIWindow
{
    private const string Popup = "XCAI has merged into xBMR###BossModXcaiMigrationComplete";
    private readonly LegacyXcaiNotice _notice;
    private MaterialTheme.Scope _theme;

    internal LegacyXcaiMigrationWindow(LegacyXcaiNotice notice)
        : base("###BossModXcaiMigrationHost", false, Vector2.One,
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings)
    {
        _notice = notice;
        IsOpen = true;
        ForceMainWindow = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        SizeCondition = ImGuiCond.Always;
    }

    public override bool DrawConditions() => Service.ClientState.IsLoggedIn && _notice.ShouldShow;
    public override void PreDraw() => _theme = MaterialTheme.Push();
    public override void PostDraw() => _theme.Dispose();

    public override void Draw()
    {
        if (!ImGui.IsPopupOpen(Popup))
            ImGui.OpenPopup(Popup);
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + viewport.Size * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f));
        ImGui.SetNextWindowSize(new Vector2(570 * M3.Scale, 0), ImGuiCond.Always);
        var open = true;
        if (!ImGui.BeginPopupModal(Popup, ref open,
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
            return;
        try
        {
            M3Text.Draw("XCAI is now part of xBMR", M3TextStyle.Heading);
            ImGui.Dummy(new Vector2(0, M3.Space3));
            M3Text.Draw("Standalone XCAI has been removed from your game.", wrap: true);
            ImGui.Dummy(new Vector2(0, M3.Space2));
            M3Text.Draw("Your saved settings and recordings have been kept. Combat assistance now lives in xBMR.", wrap: true);
            ImGui.Dummy(new Vector2(0, M3.Space2));
            M3Text.Draw("Open /bmrai for settings. Your existing /xcai commands still work.", wrap: true);
            M3Widgets.Divider(3);
            if (M3Widgets.Button("acknowledge-migration", "Got it", M3ButtonStyle.Filled, width: ImGui.GetContentRegionAvail().X))
            {
                _notice.Acknowledge();
                ImGui.CloseCurrentPopup();
            }
        }
        finally { ImGui.EndPopup(); }
        // Closing with Escape is not acknowledgement; the unacknowledged modal
        // reopens. There is no close box, cancellation or keep-installed action.
    }
}
