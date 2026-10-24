using BossMod.AI;
using BossMod.CombatAssistance.Config;
// Material layout and controls adapted from XelsCombatAI 4a0f7968; native settings ownership.
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using RebornMaterial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

internal partial class ConfigWindow
{
    private static float HintSlotWidth => 28f * M3.Scale;
    private static float HintLaneWidth => HintSlotWidth * 2;
    private static float ResetGap => M3.Space2;

    private static M3RowInfo BeginControlRow(string label, Vector2 valueSize, bool enabled,
        string? tooltip, string? disabledTooltip, string? risk = null, bool isHeader = false)
    {
        // Every row reserves the same hint and reset lanes, including rows without help or risk.
        // Optional metadata must never move a value control or its reset button.
        var row = M3SettingRow.Begin(label, null,
            new Vector2(HintLaneWidth + valueSize.X + ResetGap + M3Widgets.IconButtonSize,
                Math.Max(valueSize.Y, M3Widgets.IconButtonSize)),
            labelColor: isHeader ? MaterialTheme.Primary : null, disabled: !enabled);
        var hintsY = row.ControlPosition.Y + (Math.Max(valueSize.Y, M3Widgets.IconButtonSize) - ImGui.GetFrameHeight()) * 0.5f;
        if (risk != null)
        {
            ImGui.SetCursorScreenPos(new Vector2(row.ControlPosition.X
                + (HintSlotWidth - M3Widgets.HelpMarkerWidth(FontAwesomeIcon.SkullCrossbones)) * 0.5f, hintsY));
            M3Widgets.HelpMarker(risk, FontAwesomeIcon.SkullCrossbones, M3.Scheme.Error);
        }
        if (tooltip != null || disabledTooltip != null)
        {
            ImGui.SetCursorScreenPos(new Vector2(row.ControlPosition.X + HintSlotWidth
                + (HintSlotWidth - M3Widgets.HelpMarkerWidth(FontAwesomeIcon.QuestionCircle)) * 0.5f, hintsY));
            M3Widgets.HelpMarker(string.Join("\n\n", System.Linq.Enumerable.Where(new[] { tooltip, disabledTooltip },
                value => !string.IsNullOrEmpty(value))), FontAwesomeIcon.QuestionCircle);
        }
        ImGui.SetCursorScreenPos(row.ControlPosition + new Vector2(HintLaneWidth,
            (Math.Max(valueSize.Y, M3Widgets.IconButtonSize) - valueSize.Y) * 0.5f));
        return row;
    }

    private void EndControlRow(in M3RowInfo row, Vector2 valueSize, string defaultValue, Action reset, bool isDefault)
    {
        ImGui.SetCursorScreenPos(row.ControlPosition + new Vector2(HintLaneWidth + valueSize.X + ResetGap,
            Math.Max(0, valueSize.Y - M3Widgets.IconButtonSize) * 0.5f));
        if (M3Widgets.IconButton("reset", FontAwesomeIcon.Undo, $"Restore default: {defaultValue}", enabled: !isDefault))
        {
            reset();
            this.CommitChange();
        }
        M3SettingRow.End(row);
    }

    private void Checkbox(string label, Func<bool> read, bool defaultValue, Action<bool> setter, string? tooltip = null,
        string? disabledTooltip = null, FontAwesomeIcon? icon = null, string? iconTooltip = null, bool isHeader = false)
    {
        if (!this.sectionVisible) return;
        using var id = ImRaii.PushId(label);
        var enabled = this.disabledDepth == 0 && disabledTooltip == null;
        var controlSize = M3Widgets.SwitchSize();
        var row = BeginControlRow(label, controlSize, enabled, tooltip, disabledTooltip, iconTooltip, isHeader);
        var value = read();
        if (M3Widgets.Switch("value", ref value, enabled))
        {
            setter(value);
            this.CommitChange();
        }
        this.EndControlRow(row, controlSize, defaultValue ? "On" : "Off", () => setter(defaultValue), read() == defaultValue);
    }

    private void DrawToggleSectionHeader(string title, Func<bool> read, bool defaultValue, Action<bool> setter, string? tooltip = null,
        FontAwesomeIcon? icon = null, string? iconTooltip = null, string? disabledTooltip = null)
    {
        if (!this.sectionVisible) return;
        this.BeginSettingsCard(title, toggle: true);
        this.Checkbox(title, read, defaultValue, setter, tooltip, disabledTooltip, icon, iconTooltip, isHeader: true);
    }

    private void JobCheckbox(string label, Func<bool> read, bool defaultValue, Action<bool> setter, string? disabledTooltip, string? tooltip = null)
    {
        if (!this.sectionVisible) return;
        label = label switch
        {
            "PLD" => "Paladin",
            "WAR" => "Warrior",
            "DRK" => "Dark Knight",
            "GNB" => "Gunbreaker",
            "MNK" => "Monk",
            "DRG" => "Dragoon",
            "NIN" => "Ninja",
            "SMN" => "Summoner",
            "BLU" => "Blue Mage",
            "SAM" => "Samurai",
            "RPR" => "Reaper",
            "VPR" => "Viper",
            "BRD" => "Bard",
            "DNC" => "Dancer",
            "BST" => "Beastmaster",
            "WHM" => "White Mage",
            "BLM" => "Black Mage",
            "RDM" => "Red Mage",
            "SGE" => "Sage",
            "PCT" => "Pictomancer",
            _ => label,
        };
        if (this.jobGridOpen) ImGui.TableNextColumn();
        this.Checkbox(label, read, defaultValue, setter, tooltip, disabledTooltip);
    }

    private void Combo<T>(string label, Func<T> read, T defaultValue, Action<T> setter, string? tooltip = null,
        Func<T, string>? displayName = null, string? disabledTooltip = null) where T : struct, Enum
    {
        if (!this.sectionVisible) return;
        using var id = ImRaii.PushId(label);
        var name = displayName ?? (option => option.ToString());
        var enabled = this.disabledDepth == 0 && disabledTooltip == null;
        var width = Math.Min(240 * M3.Scale, Math.Max(120 * M3.Scale,
            ImGui.GetContentRegionAvail().X * 0.4f));
        var row = BeginControlRow(label, new Vector2(width, M3Widgets.ComboHeight), enabled, tooltip, disabledTooltip);
        var selected = read();
        if (M3Widgets.Combo("value", ref selected, width, option => name(option)
            + (EqualityComparer<T>.Default.Equals(option, defaultValue) ? " (default)" : string.Empty), enabled: enabled))
        {
            setter(selected);
            this.CommitChange();
        }
        this.EndControlRow(row, new Vector2(width, M3Widgets.ComboHeight), name(defaultValue), () => setter(defaultValue), EqualityComparer<T>.Default.Equals(read(), defaultValue));
    }

    private void SliderFloat(string label, Func<float> read, float defaultValue, float min, float max, Action<float> setter,
        string format = "%.1f", string? tooltip = null, string? disabledTooltip = null)
    {
        if (!this.sectionVisible) return;
        using var id = ImRaii.PushId(label);
        var enabled = this.disabledDepth == 0 && disabledTooltip == null;
        var width = Math.Min(200 * M3.Scale, Math.Max(110 * M3.Scale, ImGui.GetContentRegionAvail().X * 0.3f));
        var numberWidth = 90 * M3.Scale;
        var valueWidth = width + numberWidth + M3.Space1;
        var row = BeginControlRow(label, new Vector2(valueWidth, M3Widgets.NumberFieldHeight), enabled, tooltip, disabledTooltip);
        var value = read();
        var changed = M3Widgets.Slider("##slider", ref value, min, max, string.Empty, width, enabled: enabled);
        ImGui.SetCursorScreenPos(row.ControlPosition + new Vector2(HintLaneWidth + width + M3.Space1, 0));
        changed |= M3Widgets.NumberField("number", ref value, numberWidth, min: min, max: max,
            format: format.Contains(".2", StringComparison.Ordinal) ? "%.2f" : "%.1f", enabled: enabled);
        if (changed && float.IsFinite(value))
        {
            setter(Math.Clamp(value, min, max));
            this.CommitChange();
        }
        this.EndControlRow(row, new Vector2(valueWidth, M3Widgets.NumberFieldHeight), FormatValue(defaultValue, format), () => setter(defaultValue), Math.Abs(read() - defaultValue) < 0.001f);
    }

    private void SliderInt(string label, Func<int> read, int defaultValue, int min, int max, Action<int> setter, string? disabledTooltip = null)
    {
        if (!this.sectionVisible) return;
        using var id = ImRaii.PushId(label);
        var enabled = this.disabledDepth == 0 && disabledTooltip == null;
        var width = 150 * M3.Scale;
        var row = BeginControlRow(label, new Vector2(width, M3Widgets.NumberFieldHeight), enabled, null, disabledTooltip);
        var value = read();
        if (M3Widgets.NumberField("value", ref value, width, step: 1, min: min, max: max, enabled: enabled))
        {
            setter(Math.Clamp(value, min, max));
            this.CommitChange();
        }
        this.EndControlRow(row, new Vector2(width, M3Widgets.NumberFieldHeight), defaultValue.ToString(CultureInfo.InvariantCulture), () => setter(defaultValue), read() == defaultValue);
    }

    private static string FormatValue(float value, string format)
        => value.ToString(format.Contains(".2", StringComparison.Ordinal) ? "F2" : "F1", CultureInfo.InvariantCulture)
            + (format.EndsWith('x') ? "x" : format.EndsWith('s') ? "s" : string.Empty);
}
