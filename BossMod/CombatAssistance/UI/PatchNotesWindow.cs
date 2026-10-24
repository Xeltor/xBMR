using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RebornMaterial;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

using BossMod.CombatAssistance.Runtime;

internal sealed class PatchNotesWindow : UIWindow
{
    private readonly Action<PatchNoteEntry[]> acknowledge;
    private readonly List<M3NavItem> versions = [];
    private PatchNoteEntry[] entries = [];
    private bool acknowledgeOnClose;
    private int selectedEntry;
    private bool resetScroll;
    private MaterialTheme.Scope theme;

    public PatchNotesWindow(Action<PatchNoteEntry[]> acknowledge)
        : base("Combat assistance — Patch notes###BossModCombatAssistancePatchNotes", false, new Vector2(1080, 760), ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.acknowledge = acknowledge;
        this.Size = new Vector2(1080, 760);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(820, 560), MaximumSize = new Vector2(float.MaxValue) };
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.RespectCloseHotkey = true;
    }

    public void Show(PatchNoteEntry[] notes)
    {
        if (this.IsOpen || notes.Length == 0) return;
        this.entries = notes;
        this.selectedEntry = notes.Length - 1;
        this.resetScroll = true;
        this.acknowledgeOnClose = true;
        this.IsOpen = true;
    }

    public void Suspend()
    {
        this.acknowledgeOnClose = false;
        this.IsOpen = false;
    }

    // Observe a title-bar/hotkey close before the framework considers auto-showing pending notes.
    // WindowSystem's OnClose callback can otherwise arrive after that automatic show decision.
    internal void ObserveDismissal()
    {
        if (!this.IsOpen) this.OnClose();
    }

    public override void OnClose()
    {
        if (!this.acknowledgeOnClose) return;
        this.acknowledgeOnClose = false;
        this.acknowledge(this.entries);
    }

    public override void PreDraw() => this.theme = MaterialTheme.Push();

    public override void PostDraw()
    {
        this.theme.Dispose();
        this.theme = default;
    }

    public override void Draw()
    {
        if (this.entries.Length == 0) return;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        _ = M3Widgets.WindowActions("window-actions", new Vector2(origin.X + width, origin.Y), [], out var closed,
            fill: MaterialTheme.RaisedSurface);
        if (closed)
        {
            this.OnClose();
            this.IsOpen = false;
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(M3.Space2, M3.Space1));
        M3Text.Draw("What's new", M3TextStyle.Heading);
        M3Text.Draw("Combat assistance · Updates and imported release history", M3TextStyle.Muted);
        ImGui.Dummy(new Vector2(0, M3.Space3));
        M3Widgets.Divider(4);
        var height = Math.Max(1, ImGui.GetContentRegionAvail().Y - M3Widgets.ButtonHeight - M3.Space3 * 2);
        using (var versionsPane = ImRaii.Child("versions", new Vector2(194 * M3.Scale, height), false))
        {
            if (versionsPane)
            {
                M3Text.Draw("RELEASE HISTORY", M3TextStyle.Label);
                M3Widgets.Divider();
                this.versions.Clear();
                for (var i = this.entries.Length - 1; i >= 0; i--)
                {
                    var version = this.entries[i].Version;
                    this.versions.Add(new M3NavItem(i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        version.StartsWith("Development", StringComparison.Ordinal) ? "Development" : version,
                        FontAwesomeIcon.History, i == this.selectedEntry));
                }
                var clicked = M3Navigation.Draw("history", this.versions, expanded: true);
                if (clicked != null && int.TryParse(clicked, out var index))
                {
                    this.selectedEntry = index;
                    this.resetScroll = true;
                }
            }
        }
        ImGui.SameLine();
        using (var content = ImRaii.Child("changes", new Vector2(0, height), false))
        {
            if (content)
            {
                if (this.resetScroll) ImGui.SetScrollY(0);
                this.resetScroll = false;
                var entry = this.entries[this.selectedEntry];
                M3Text.Draw(entry.Version.StartsWith("Development", StringComparison.Ordinal) ? entry.Version : $"Version {entry.Version}", M3TextStyle.Heading);
                M3Widgets.Pill("change-count", $"{entry.Changes.Length} changes", M3.Scheme.Primary, FontAwesomeIcon.CheckCircle);
                ImGui.Dummy(new Vector2(0, M3.Space3));
                for (var i = 0; i < entry.Changes.Length; i++)
                {
                    using (var card = MaterialTheme.BeginCard($"{entry.Id}-{i}"))
                    {
                        M3Text.Draw(entry.Changes[i], wrap: true);
                    }
                    ImGui.Dummy(new Vector2(0, M3.Space2));
                }
            }
        }
        M3Widgets.Divider(2);
        M3Text.Draw("Close to mark the displayed updates as read.", M3TextStyle.Muted);
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowContentRegionMax().X - 110 * M3.Scale));
        if (M3Widgets.Button("close", "Got it", M3ButtonStyle.Filled, width: 100 * M3.Scale))
        {
            this.OnClose();
            this.IsOpen = false;
        }
    }
}
