using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Linq;
using System.Reflection;

namespace BossMod.CombatAssistance.Support;

internal sealed partial class SupportRecipientController
{
    private RsrMitigationSelectionLease? mitigationLease;
    private RsrMitigationDutyScope? mitigationScope;
    public Func<DateTime?>? MitigationDeadline { get; set; }
    public string MitigationStatus { get; private set; } = "inactive";
    private bool MitigationEnabled => this.config.PreserveCoveredRangedMitigation &&
        this.services.ObjectTable.LocalPlayer?.ClassJob.RowId is 23 or 31 or 38;

    private void AcquireMitigationSequencing(Assembly basic, object current)
    {
        if (!this.MitigationEnabled) return;
        var job = this.services.ObjectTable.LocalPlayer!.ClassJob.RowId;
        var (property, id, _) = PartyRangedMitigationPolicy.ActionForJob(job);
        try
        {
            var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            this.mitigationScope = new(center.GetProperty("CurrentDutyRotation", StaticFlags)!);
            this.mitigationLease = RsrMitigationSelectionLease.Acquire(current, property, id, this.DeferMitigation,
                ex => this.services.Log.Verbose(ex, "Mitigation sequencing retained native RSR selection"));
            this.MitigationStatus = "native defense selection ready";
        }
        catch (Exception ex)
        {
            this.ResetMitigationSequencing();
            this.services.Log.Verbose(ex, "Mitigation sequencing contract unavailable; native RSR selection retained");
        }
    }

    private void RefreshMitigationSequencing()
    {
        if (!this.MitigationEnabled || !this.allowed() || !this.services.InCombat)
            this.MitigationStatus = "inactive";
    }

    private void EnterMitigationSequencing()
    {
        if (this.MitigationEnabled) this.mitigationScope?.Enter();
    }
    private void ExitMitigationSequencing() => this.mitigationScope?.Exit();
    private void ResetMitigationSequencing()
    {
        this.mitigationScope?.Exit(); this.mitigationScope = null;
        this.mitigationLease?.Dispose(); this.mitigationLease = null;
        this.MitigationStatus = "inactive";
    }

    private unsafe bool DeferMitigation(object action)
    {
        this.RefreshMitigationSequencing();
        var now = DateTime.UtcNow;
        var manager = ActionManager.Instance();
        var center = this.dataCenter;
        if (!this.inScope || this.mitigationScope?.NativeDefense != true || !this.MitigationEnabled || !this.allowed() ||
            !this.services.InCombat ||
            this.services.ObjectTable.LocalPlayer is not { IsDead: false, IsCasting: false } player ||
            manager == null || !float.IsFinite(manager->AnimationLock) || manager->AnimationLock > 0 ||
            manager->AreaTargetingActionId != 0 || manager->AreaTargetingSpellId != 0 || manager->ActionQueued ||
            center?.GetMethod("IsActivatedIPC", StaticFlags)?.Invoke(null, null) is not true ||
            center.GetProperty("IsTargetOnly", StaticFlags)?.GetValue(null) is not false ||
            center.GetProperty("CurrentInterceptedAction", StaticFlags)?.GetValue(null) != null ||
            RsrCommandPolicy.HasExplicitCommand(center.GetProperty("CommandStatus", StaticFlags)?.GetValue(null) as Enum,
                center.GetProperty("SpecialType", StaticFlags)?.GetValue(null) as Enum) ||
            this.MitigationDeadline?.Invoke() is not { } deadline || (deadline - now).TotalMilliseconds is <= 0 or > 10000)
        {
            return false;
        }
        var selection = action.GetType().GetProperty("Target", InstanceFlags)?.GetValue(action);
        return this.DeferRangedMitigation(selection, player, now, deadline);
    }
}
