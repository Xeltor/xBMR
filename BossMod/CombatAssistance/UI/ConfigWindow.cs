using BossMod.AI;
using BossMod.CombatAssistance.Config;
// Material layout and controls adapted from XelsCombatAI 4a0f7968; native settings ownership.
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RebornMaterial;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

internal partial class ConfigWindow : UIWindow
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
    private static readonly FontAwesomeIcon[] PageIcons =
    [FontAwesomeIcon.Cog, FontAwesomeIcon.Walking, FontAwesomeIcon.Users, FontAwesomeIcon.Crosshairs,
        FontAwesomeIcon.UserShield, FontAwesomeIcon.Bolt, FontAwesomeIcon.Wrench, FontAwesomeIcon.Stethoscope];
    private ConfigPage selectedPage;
    private readonly Dictionary<ConfigPage, int> selectedTabs = [];
    private readonly HashSet<string> displayedAvailability = [];
    private readonly List<M3NavItem> navigation = [];
    private MaterialTheme.Scope theme;
    private MaterialTheme.CardScope? settingsCard;
    private DateTime nextAvailabilityRefresh;
    private string? currentDependencyWarning;
    private int ActiveTab => this.selectedTabs.GetValueOrDefault(this.selectedPage);

    private string[] TabNames => this.selectedPage switch
    {
        ConfigPage.Movement => ["Control", "Knockbacks", "Positioning"],
        ConfigPage.JobSpecific => ["Party support", "Black Mage", "Pictomancer", "Red Mage", "Tanks"],
        ConfigPage.BmrFixes => ["General fixes"],
        ConfigPage.Troubleshooting => ["Connections", "BMR settings", "Overlay & help"],
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
    private Vector2? lastVisiblePosition;
    private bool jobGridOpen;

    public Func<IReadOnlyList<PluginConnectionRow>>? PluginConnections { get; set; }
    public Func<string?>? RotationSolverAvailability { get; set; }
    public Func<string?>? SupportRecipientStatus { get; set; }
    public Func<string?>? ActionPredictionAvailability { get; set; }

    internal ConfigWindow(AIManager manager, AssistanceSettingsSession settings)
        : base("Combat assistance###AI debug window", false, new Vector2(1180, 820),
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.manager = manager;
        this.settings = settings;
        this.save = settings.Save;
        this.resetRuntimeState = () =>
        {
            manager.Autorot.MovementPlanning.Release("normal");
            manager.Autorot.MovementPlanning.Release("follow");
        };
        this.setEnabled = enabled => this.config.Enabled = enabled;
        this.debugState = () => BuildDiagnosticText(manager, settings);
        this.dependencyWarning = () => settings.MayEdit ? null : settings.Current.Detail;
        this.trueNorthWarning = () => this.rotationSolverUnavailable;
        this.manageTrueNorthEnabled = () => { };
        this.copyToClipboard = text => ImGui.SetClipboardText(text);
        this.Size = new Vector2(1180, 820);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(940, 650), MaximumSize = new Vector2(float.MaxValue) };
        this.RespectCloseHotkey = true;
        this.AllowPinning = false;
        this.AllowClickthrough = false;
    }

    public override void PreDraw() => this.theme = MaterialTheme.Push();

    public override void PostDraw()
    {
        this.theme.Dispose();
        this.theme = default;
    }

    public override void OnOpen()
    {
        this.confirmResetAll = false;
        if (this.config.SettingsWindowPosition is { } position && float.IsFinite(position.X) && float.IsFinite(position.Y))
        {
            this.Position = position;
            this.PositionCondition = ImGuiCond.Appearing;
        }
    }

    public override void OnClose() => this.SaveWindowPosition();

    internal void SaveWindowPosition()
    {
        if (this.lastVisiblePosition is { } position && this.config.SettingsWindowPosition != position)
        {
            this.config.SettingsWindowPosition = position;
            this.save();
        }
    }

    public override void Draw()
    {
        this.lastVisiblePosition = ImGui.GetWindowPos();
        if (DateTime.UtcNow >= this.nextAvailabilityRefresh)
        {
            this.rotationSolverUnavailable = this.RotationSolverAvailability?.Invoke();
            this.currentDependencyWarning = this.dependencyWarning();
            this.nextAvailabilityRefresh = DateTime.UtcNow.AddSeconds(1);
        }
        this.DrawAppBar();
        var bodyHeight = Math.Max(1, ImGui.GetContentRegionAvail().Y);
        var sidebarWidth = 208f * M3.Scale;
        using (var sidebar = ImRaii.Child("navigation", new Vector2(sidebarWidth, bodyHeight), false))
        {
            if (sidebar)
            {
                this.navigation.Clear();
                for (var i = 0; i < PageNames.Length; i++)
                    this.navigation.Add(new M3NavItem(((ConfigPage)i).ToString(), PageNames[i], PageIcons[i],
                        this.selectedPage == (ConfigPage)i, PageDescriptions[i], Accent: MaterialTheme.Primary, SeparatorAfter: i == 5));
                var clicked = M3Navigation.Draw("pages", this.navigation, expanded: true);
                if (clicked != null && Enum.TryParse<ConfigPage>(clicked, out var page))
                    this.SelectPage(page);
            }
        }
        ImGui.SameLine();
        using (var body = ImRaii.Child("page", new Vector2(0, bodyHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (body)
            {
                M3Text.Draw(PageNames[(int)this.selectedPage], M3TextStyle.Heading);
                ImGui.SameLine(0, M3.Space2);
                M3Widgets.HelpMarker(PageDescriptions[(int)this.selectedPage], FontAwesomeIcon.QuestionCircle);
                if (this.currentDependencyWarning != null)
                    M3Text.Draw($"Waiting for: {this.currentDependencyWarning}", M3TextStyle.Warning, wrap: true);
                var names = this.TabNames;
                if (names.Length > 0)
                {
                    var tabs = new M3Tab[names.Length];
                    for (var i = 0; i < names.Length; i++) tabs[i] = new M3Tab(names[i]);
                    var selected = M3Navigation.Tabs("tabs", tabs, this.ActiveTab, secondary: true);
                    if (selected >= 0 && selected < names.Length && selected != this.ActiveTab)
                    {
                        this.selectedTabs[this.selectedPage] = selected;
                        this.confirmResetAll = false;
                        this.resetScroll = true;
                    }
                }
                ImGui.Dummy(new Vector2(0, M3.Space3));
                using var content = ImRaii.Child("settings", Vector2.Zero, false);
                if (content)
                {
                    if (this.resetScroll) ImGui.SetScrollY(0);
                    this.resetScroll = false;
                    this.displayedAvailability.Clear();
                    this.disabledDepth = 0;
                    this.sectionVisible = true;
                    using var pageId = ImRaii.PushId(this.selectedPage.ToString());
                    try
                    {
                        if (this.selectedPage == ConfigPage.General) this.DrawNativeGeneral();
                        if (this.selectedPage == ConfigPage.Movement && this.ActiveTab == 0) this.DrawNativeMovement();
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
                    finally
                    {
                        this.EndSettingsCard();
                    }
                }
            }
        }
    }

    private void DrawAppBar()
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var actionSize = M3Widgets.WindowActionsSize(0);
        _ = M3Widgets.WindowActions("window-actions", new Vector2(origin.X + width, origin.Y),
            [], out var closed, fill: MaterialTheme.RaisedSurface);
        if (closed) this.IsOpen = false;
        ImGui.SetCursorScreenPos(origin + new Vector2(M3.Space2,
            Math.Max(0, actionSize.Y - M3Text.Measure("xBMR · Combat assistance", M3TextStyle.Title).Y) * 0.5f));
        M3Text.Draw("xBMR · Combat assistance", M3TextStyle.Title);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + actionSize.Y + M3.Space1));
        M3Widgets.Divider(2);
    }

    private void BeginSettingsCard(string title, bool toggle = false)
    {
        this.EndSettingsCard();
        this.settingsCard = MaterialTheme.BeginCard("section-" + title, toggle ? null : title);
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
            using var card = MaterialTheme.BeginCard(connection.Name, connection.Name);
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
            "vnavmesh" => "Checks whether movement destinations can be reached.",
            "Upcoming actions" => "Predicts the next attack for range and positionals.",
            "Automatic positioning" => "Provides movement guidance during combat.",
            "Placed ground effects" => "Tracks friendly ground effects for positioning.",
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

    private void DrawBossModCompatibility()
    {
        if (!this.sectionVisible) return;
        this.DrawSectionHeader("Movement settings");
        this.Text("Movement and action restrictions are shared with /bmr. Native presets keep their own strategies.");
        this.Text(this.settings.Current.Detail);
    }

    private enum ConfigPage { General, Movement, AoeAndTrash, Positionals, JobSpecific, Dashes, BmrFixes, Troubleshooting }
}
