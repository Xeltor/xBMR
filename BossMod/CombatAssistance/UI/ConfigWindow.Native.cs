using BossMod.AI;
using BossMod.CombatAssistance.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using RebornMaterial;

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

    private void DrawNativeGeneral()
    {
        var native = this.NativeConfig;
        this.DrawToggleSectionHeader("Automatic movement", () => this.manager.Beh != null, false,
            active => { if (active) this.manager.SwitchToFollow(native.FollowSlot); else this.manager.SwitchToIdle(); },
            "Starts or stops following with the selected leader and preset. Combat assistance preferences do not start it by themselves.");
        var members = this.manager.WorldState.Party.WithSlot(true);
        var slots = members.ToArray();
        var names = slots.Select(p => p.Item2.Name).ToArray();
        var selected = Array.FindIndex(slots, p => p.Item1 == native.FollowSlot);
        if (names.Length > 0)
            this.NativeChoice("Leader", names, Math.Max(0, selected), index => this.NativeChange(() =>
            {
                native.FollowSlot = slots[index].Item1;
                if (this.manager.Beh != null) this.manager.SwitchToFollow(native.FollowSlot);
            }));
        var presets = this.manager.Autorot.Database.Presets.AllPresets.ToArray();
        var presetNames = new[] { "None" }.Concat(presets.Select(p => p.Name)).ToArray();
        var presetIndex = Array.FindIndex(presets, p => p.Name == native.AIAutorotPresetName) + 1;
        this.NativeChoice("Preset", presetNames, presetIndex,
            index => this.NativeChange(() => this.manager.SetAIPreset(index == 0 ? null : presets[index - 1])));
        this.Checkbox("Forbid actions", () => native.ForbidActions, NativeDefaults.ForbidActions,
            value => this.NativeChange(() => native.ForbidActions = value), "Keeps automated actions disabled while movement remains available.");
        this.Checkbox("Manual targeting", () => native.ManualTarget, NativeDefaults.ManualTarget,
            value => this.NativeChange(() => native.ManualTarget = value));
        this.Checkbox("Focus target leader", () => native.FocusTargetMaster, NativeDefaults.FocusTargetMaster,
            value => this.NativeChange(() => native.FocusTargetMaster = value));
        this.EndSettingsCard();
    }

    private void DrawNativeMovement()
    {
        var native = this.NativeConfig;
        this.DrawSectionHeader("Movement authority");
        this.Checkbox("Forbid movement", () => native.ForbidMovement, NativeDefaults.ForbidMovement,
            value => this.NativeChange(() => native.ForbidMovement = value));
        this.Checkbox("Idle while mounted", () => native.ForbidAIMovementMounted, NativeDefaults.ForbidAIMovementMounted,
            value => this.NativeChange(() => native.ForbidAIMovementMounted = value));
        this.DrawSectionHeader("Following");
        this.Checkbox("Follow during combat", () => native.FollowDuringCombat, NativeDefaults.FollowDuringCombat,
            value => this.NativeChange(() => native.FollowDuringCombat = value));
        this.Checkbox("Follow during active boss modules", () => native.FollowDuringActiveBossModule, NativeDefaults.FollowDuringActiveBossModule,
            value => this.NativeChange(() => native.FollowDuringActiveBossModule = value));
        this.Checkbox("Follow out of combat", () => native.FollowOutOfCombat, NativeDefaults.FollowOutOfCombat,
            value => this.NativeChange(() => native.FollowOutOfCombat = value));
        this.Checkbox("Follow target", () => native.FollowTarget, NativeDefaults.FollowTarget,
            value => this.NativeChange(() => native.FollowTarget = value));
        this.NativeNumber("Distance to leader", native.MaxDistanceToSlot, value => native.MaxDistanceToSlot = value);
        this.Checkbox("Use combat positioning range", () => native.UseCombatPositioningRange, NativeDefaults.UseCombatPositioningRange,
            value => this.NativeChange(() => native.UseCombatPositioningRange = value),
            "Native combat assistance uses the job's reach while RotationSolver is active. The selected action and mechanics determine the useful position. Disable for fixed-distance target following.");
        this.NativeNumber("Fixed distance to target", native.MaxDistanceToTarget, value => native.MaxDistanceToTarget = value);
        this.NativeNumber("Minimum target distance", native.MinDistance, value => native.MinDistance = value);
        this.Combo("Following positional", () => native.DesiredPositional, NativeDefaults.DesiredPositional,
            value => this.NativeChange(() => native.DesiredPositional = value));
        this.Checkbox("Follow RotationSolver positional", () => native.FollowRSRDesiredPositional, NativeDefaults.FollowRSRDesiredPositional,
            value => this.NativeChange(() => native.FollowRSRDesiredPositional = value));
        this.NativeNumber("Following danger-zone cushion", native.PreferredDistance, value => native.PreferredDistance = value);
        this.NativeNumber("Following movement delay", (float)native.MoveDelay, value => native.MoveDelay = value);
        this.Checkbox("Disable obstacle maps", () => native.DisableObstacleMaps, NativeDefaults.DisableObstacleMaps,
            value => this.NativeChange(() => native.DisableObstacleMaps = value),
            "Uses the native movement setting also available in /bmr. Terrain coverage depends on the active arena and map.");
        this.Checkbox("Automatic AFK", () => native.AutoAFK, NativeDefaults.AutoAFK,
            value => this.NativeChange(() => native.AutoAFK = value));
        this.NativeNumber("AFK timer (seconds)", native.AFKModeTimer, value => native.AFKModeTimer = value);
        this.EndSettingsCard();
    }

    private void NativeChoice(string label, string[] names, int selected, Action<int> change)
    {
        using var id = ImRaii.PushId(label);
        var width = Math.Min(280 * M3.Scale, ImGui.GetContentRegionAvail().X * 0.45f);
        var row = BeginControlRow(label, new(width, M3Widgets.ComboHeight), true, null, null);
        if (M3Widgets.Combo("value", ref selected, names, width) && (uint)selected < names.Length)
            change(selected);
        M3SettingRow.End(row);
    }

    private void NativeNumber(string label, float value, Action<float> change)
    {
        using var id = ImRaii.PushId(label);
        var width = 120 * M3.Scale;
        var row = BeginControlRow(label, new(width, M3Widgets.NumberFieldHeight), true, null, null);
        // Preserve the native numeric domain; moving the control must not invent a range cap.
        if (M3Widgets.NumberField("value", ref value, width, min: 0, format: "%.1f") && float.IsFinite(value))
            this.NativeChange(() => change(value));
        M3SettingRow.End(row);
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
