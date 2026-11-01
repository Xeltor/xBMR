using BossMod.AI;
using BossMod.CombatAssistance.Config;
// Material layout and controls adapted from XelsCombatAI 4a0f7968; native settings ownership.
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using RebornMaterial;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

internal partial class AssistanceSettingsPage
{
    public Func<string>? FightSharingStatus { get; set; }

    private static readonly string[] PageNames = ["General", "Movement", "Dungeons & AoE", "Positionals", "Jobs", "Movement abilities", "BMR Fixes", "Troubleshooting"];
    private static readonly string[] JobSections = ["Party support", "Black Mage", "Pictomancer", "Red Mage", "Tanks"];
    private readonly AIManager manager;
    private readonly AssistanceSettingsSession settings;
    private AssistancePreferences config => this.settings.Preferences;
    private readonly AssistancePreferences defaultConfig = new();
    private readonly Action save;
    private readonly Action resetRuntimeState;
    private readonly Action<bool> setEnabled;
    private readonly Func<string> debugState;
    private readonly Func<string?> dependencyWarning;
    private readonly Func<string?> trueNorthWarning;
    private readonly Action manageTrueNorthEnabled;
    private readonly Action<string> copyToClipboard;
    private ConfigPage selectedPage;
    private readonly Dictionary<ConfigPage, int> selectedTabs = [];
    private readonly HashSet<string> displayedAvailability = [];
    private M3Card.Scope? settingsCard;
    private DateTime nextAvailabilityRefresh;
    private string? currentDependencyWarning;
    private int ActiveTab => this.selectedTabs.GetValueOrDefault(this.selectedPage);

    private string[] TabNames => this.selectedPage switch
    {
        ConfigPage.Movement => ["Control", "Knockbacks", "Positioning"],
        ConfigPage.JobSpecific => ["Party support", "Black Mage", "Pictomancer", "Red Mage", "Tanks"],
        ConfigPage.BmrFixes => ["General fixes"],
        ConfigPage.Troubleshooting => ["Connections", "Overlay & help"],
        _ => [],
    };

    private static readonly string[] PageDescriptions =
    [
        "Combat assistance, camera preferences, and chat messages.",
        "Choose how to move, when to yield, and where to stand.",
        "Stay with the party and make each area attack count.",
        "Keep rear and flank access within BossMod's safe space.",
        "Movement and party support tailored to each job.",
        "Choose which jobs may use their movement abilities.",
        "Optional corrections. BossMod still controls encounter safety.",
        "Check connections, resolve conflicts, and inspect decisions.",
    ];
    private string? rotationSolverUnavailable;
    private DateTime copiedDebugStateUntil;
    private bool confirmResetAll;
    private bool resetScroll;
    private bool sectionVisible = true;
    private int disabledDepth;
    private bool jobGridOpen;

    public Func<IReadOnlyList<PluginConnectionRow>>? PluginConnections { get; set; }
    public Func<string?>? RotationSolverAvailability { get; set; }
    public Func<string?>? SupportRecipientStatus { get; set; }
    public Func<string?>? ActionPredictionAvailability { get; set; }

    internal AssistanceSettingsPage(AIManager manager, AssistanceSettingsSession settings)
    {
        this.manager = manager;
        this.settings = settings;
        this.save = settings.Save;
        this.resetRuntimeState = () =>
        {
            manager.Autorot.MovementPlanning.Release("normal");
            manager.Autorot.MovementPlanning.Release("follow");
        };
        this.setEnabled = manager.EnableConfig;
        this.debugState = () => BuildDiagnosticText(manager, settings);
        this.dependencyWarning = () => settings.MayEdit ? null : settings.Current.Detail;
        this.trueNorthWarning = () => this.rotationSolverUnavailable;
        this.manageTrueNorthEnabled = () => { };
        this.copyToClipboard = text => ImGui.SetClipboardText(text);
    }

    internal void OnHidden() => this.confirmResetAll = false;

    internal void Draw()
    {
        using var id = ImRaii.PushId("combat-assistance");
        if (this.resetScroll) ImGui.SetScrollY(0);
        this.resetScroll = false;
        if (DateTime.UtcNow >= this.nextAvailabilityRefresh)
        {
            this.rotationSolverUnavailable = this.RotationSolverAvailability?.Invoke();
            this.currentDependencyWarning = this.dependencyWarning();
            this.nextAvailabilityRefresh = DateTime.UtcNow.AddSeconds(1);
        }
        this.displayedAvailability.Clear();
        this.disabledDepth = 0;
        this.sectionVisible = true;
        try
        {
            using (ImRaii.Disabled(!this.settings.MayEdit))
                this.DrawActivation();
            this.EndSettingsCard();
            if (this.currentDependencyWarning != null)
                M3Widgets.Banner("##waiting", this.currentDependencyWarning, M3Severity.Warning, FontAwesomeIcon.Clock);

            var pages = new M3Tab[PageNames.Length];
            for (var i = 0; i < pages.Length; ++i)
                pages[i] = new(PageNames[i], Tooltip: PageDescriptions[i]);
            var selectedPage = M3Navigation.Tabs("sections", pages, (int)this.selectedPage);
            if (selectedPage >= 0 && selectedPage < pages.Length && selectedPage != (int)this.selectedPage)
                this.SelectPage((ConfigPage)selectedPage);
            ImGui.Dummy(new(0, M3.Space2));
            var names = this.TabNames;
            if (names.Length > 0)
            {
                var tabs = new M3Tab[names.Length];
                for (var i = 0; i < names.Length; ++i) tabs[i] = new(names[i]);
                var selected = M3Navigation.Tabs("subsections", tabs, this.ActiveTab, secondary: true);
                if (selected >= 0 && selected < names.Length && selected != this.ActiveTab)
                {
                    this.selectedTabs[this.selectedPage] = selected;
                    this.confirmResetAll = false;
                    this.resetScroll = true;
                }
                ImGui.Dummy(new(0, M3.Space2));
            }
            using var pageId = ImRaii.PushId(this.selectedPage.ToString());
            using var unavailable = ImRaii.Disabled(!this.settings.MayEdit);
            switch (this.selectedPage)
            {
                case ConfigPage.General: this.DrawGeneralTab(); break;
                case ConfigPage.Movement: this.DrawMovementTab(); break;
                case ConfigPage.AoeAndTrash: this.DrawAoeAndTrashTab(); break;
                case ConfigPage.Positionals: this.DrawPositionalsTab(); break;
                case ConfigPage.JobSpecific: this.DrawJobSpecificTab(); break;
                case ConfigPage.Dashes: this.DrawDashesTab(); break;
                case ConfigPage.BmrFixes: this.DrawBmrFixesTab(); break;
                case ConfigPage.Troubleshooting: this.DrawTroubleshootingTab(); break;
            }
        }
        finally { this.EndSettingsCard(); }
    }

    private void DrawActivation()
    {
        this.DrawToggleSectionHeader("Enable AI", () => this.settings.MayAssist && this.manager.Beh != null, false, this.setEnabled,
            "Starts automatic movement and combat assistance for your character. Turn off to stop both. Manual movement always takes priority.");
        if (this.NativeConfig.FollowSlot != PartyState.PlayerSlot || this.manager.MasterSlot != PartyState.PlayerSlot)
            M3Widgets.Banner("##native-follow", "Native party following is selected. It changes movement priorities and pauses Keep up with dungeon pulls. Use /bmrai follow Slot1 to return to normal combat movement (enables AI).",
                M3Severity.Warning, FontAwesomeIcon.InfoCircle);
    }

    private void BeginSettingsCard(string title, bool toggle = false)
    {
        this.EndSettingsCard();
        this.settingsCard = M3Card.Begin("section-" + title, toggle ? null : title);
    }

    private void EndSettingsCard()
    {
        if (this.settingsCard is { } card)
        {
            card.Dispose();
            this.settingsCard = null;
            ImGui.Dummy(new Vector2(0, M3.Space2));
        }
    }

    private void SelectPage(ConfigPage page)
    {
        this.selectedPage = page;
        this.confirmResetAll = false;
        this.resetScroll = true;
    }

    private void CommitChange()
    {
        this.config.Clamp();
        this.resetRuntimeState();
        this.save();
    }

    private void Text(string text)
    {
        if (this.sectionVisible && !string.IsNullOrEmpty(text)) M3Text.Draw(text, wrap: true);
    }

    private void DynamicText(Func<string> read)
    {
        if (this.sectionVisible) this.Text(read());
    }

    private void Spacing() { if (this.sectionVisible) ImGui.Dummy(new Vector2(0, M3.Space2)); }
    private void BeginDisabled() => this.disabledDepth++;
    private void EndDisabled() => this.disabledDepth--;
    private void BeginJobGrid() => this.jobGridOpen = ImGui.BeginTable("jobs", 2, ImGuiTableFlags.SizingStretchSame);
    private void EndJobGrid()
    {
        if (this.jobGridOpen) ImGui.EndTable();
        this.jobGridOpen = false;
    }

    private void DrawSectionHeader(string title)
    {
        if (!this.sectionVisible) return;
        this.BeginSettingsCard(title);
    }

    private void DrawAvailability(Func<string?> read)
    {
        if (this.sectionVisible && read() is { } reason && this.displayedAvailability.Add(reason))
            M3Text.Draw(reason, M3TextStyle.Warning, wrap: true);
    }

    private void DrawConnections()
    {
        if (!this.sectionVisible) return;
        if (this.PluginConnections?.Invoke() is not { Count: > 0 } rows)
        {
            this.Text("Checking plugin connections...");
            return;
        }
        foreach (var connection in rows)
        {
            using var card = M3Card.Begin(connection.Name, connection.Name);
            M3Text.Draw(connection.Status, connection.NeedsAttention ? M3TextStyle.Warning
                : connection.Status is "Connected" or "Available" ? M3TextStyle.Success : M3TextStyle.Muted);
            ImGui.SameLine(0, M3.Space2);
            M3Widgets.HelpMarker(ConnectionHelp(connection), FontAwesomeIcon.QuestionCircle);
        }
    }

    private static string ConnectionHelp(PluginConnectionRow connection) => connection.NeedsAttention
        ? "Check that the plugin is enabled and up to date. Copy a troubleshooting report if this persists."
        : connection.Name switch
        {
            "RotationSolver Reborn" => "Provides rotation actions and targets for assisted combat.",
            _ => "Copy a troubleshooting report for details.",
        };

    private void BeginJobSection(string title)
    {
        this.EndSettingsCard();
        this.sectionVisible = JobSections[this.ActiveTab] == title;
        this.DrawSectionHeader(title);
    }

    private void Button(string label, Action onClick)
    {
        if (this.sectionVisible && M3Widgets.Button(label, label)) onClick();
    }

    private enum ConfigPage { General, Movement, AoeAndTrash, Positionals, JobSpecific, Dashes, BmrFixes, Troubleshooting }
}
