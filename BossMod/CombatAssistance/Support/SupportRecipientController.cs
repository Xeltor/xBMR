using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace BossMod.CombatAssistance.Support;

internal sealed record SupportRecipientSnapshot(string Action, ulong Current, ulong Preferred,
    Vector3? Position, bool Transfer, string Reason, DateTime ObservedAt)
{
    public bool RequestSubmitted { get; init; }
    public bool IncomingDamage { get; init; }
    public bool ComplementaryCoverage { get; init; }
}

internal sealed partial class SupportRecipientController : IDisposable
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly SupportAction[] ManagedActions = Enum.GetValues<SupportAction>();
    private readonly Func<AssistancePreferences> preferences;
    private AssistancePreferences config => this.preferences();
    private readonly NativeSupportContext services;
    private readonly RsrRuntimeAccess reflection;
    private readonly Func<bool> allowed;
    private readonly Dictionary<SupportAction, RsrSupportActionLease> leases = [];
    private readonly SupportAssignment assignment = new();
    private readonly RsrSelectionScopeOwner callbackOwner;
    private RsrSelectionScopeOwner.Registration? callback;
    private RsrSupportDutySlot? dutySlot;
    private object? rotation;
    private Type? actionInterface;
    private Type? dataCenter;
    private Type? priorityConfig;
    private PropertyInfo? unionRemove;
    private object? originalUnionRemove;
    private MethodInfo? canUse;
    private object? releaseAction;
    private RsrSupportActionProxy? releaseProxy;
    private SupportAction jobAction;
    private bool allowTransfer;
    private bool inScope;
    private bool faulted;
    private uint territory;
    private DateTime transferApplicationUntil;
    private DateTime nextProbe;
    private bool wasMounted;
    private DateTime nextFailureLog;

    public SupportRecipientController(Func<AssistancePreferences> preferences, NativeSupportContext services,
        RsrRuntimeAccess reflection, Func<bool> allowed, RsrSelectionScopeOwner callbackOwner)
    {
        this.preferences = preferences;
        this.services = services;
        this.reflection = reflection;
        this.allowed = allowed;
        this.callbackOwner = callbackOwner;
        this.SubscribeRaisedHealing();
    }

    public Func<ulong, bool>? IncomingDamage { get; set; }

    public string Status { get; private set; } = "disabled";
    public string? AvailabilityWarning => this.RaisedHealingEnabled && (!this.raisedHealingSubscribed || this.raisedHealingFaulted)
        ? "Post-resurrection healing unavailable; RSR keeps control."
        : this.Status switch
        {
            "waiting for RSR" => "Requires Rotation Solver Reborn.",
            "Support integration unavailable; RSR keeps control." or "Support integration yielded; RSR keeps control." => this.Status,
            _ => null
        };
    public SupportRecipientSnapshot? Snapshot { get; private set; }

    private bool Enabled => this.config.Enabled && (this.SupportEnabled || this.MitigationEnabled || this.RaisedHealingEnabled);

    private bool SupportEnabled => (this.services.ObjectTable.LocalPlayer?.ClassJob.RowId switch
    {
        40 => this.config.ManageKardiaRecipient,
        38 => this.config.ManageDancePartnerRecipient,
        28 => this.config.ManageFeyUnionRecipient,
        33 => this.config.ManageAstCardRecipients,
        _ => false
    });

    internal void Update()
    {
        if (!this.allowed())
        {
            this.Reset();
            this.Status = "inactive under native activation or manual-control gates";
            return;
        }
        var mounted = this.services.Mounted;
        if (mounted)
        {
            this.Reset();
            this.wasMounted = true;
            this.Status = "inactive while mounted";
            return;
        }
        if (this.wasMounted)
        {
            this.wasMounted = false;
            this.nextProbe = DateTime.MinValue;
        }
        this.RefreshRaisedHealing();
        this.RefreshMitigationSequencing();
        if (this.territory != this.services.World.CurrentZone)
        {
            this.Reset();
            this.territory = this.services.World.CurrentZone;
        }
        if (this.services.ObjectTable.LocalPlayer is not { IsDead: false } live || !live.IsValid() || live.Address == 0)
        {
            this.Reset();
            this.Status = "waiting for an active player";
            return;
        }
        if (!this.Enabled)
        {
            this.Reset();
            this.Status = "disabled";
            return;
        }
        if (DateTime.UtcNow < this.nextProbe) return;
        this.nextProbe = DateTime.UtcNow.AddSeconds(1);
        try
        {
            if (!this.reflection.TryGetSupportContext(out var plugin, out var basic, out var current))
            {
                this.Reset();
                this.Status = "waiting for RSR";
                return;
            }
            if (ReferenceEquals(this.rotation, current) && this.callback?.Owned == true && !this.faulted &&
                (!this.RaisedHealingEnabled || this.dutySlot != null) &&
                (!this.MitigationEnabled || this.mitigationLease != null)) return;
            this.Reset();
            var job = this.services.ObjectTable.LocalPlayer!.ClassJob.RowId;
            if (RsrRuntimeAccess.Job(current!) != job)
            {
                this.Status = "waiting for RSR job change";
                return;
            }
            this.jobAction = RsrRuntimeAccess.Support(job)
                ?? (this.MitigationEnabled ? SupportAction.Resurrection : throw new NotSupportedException("This job has no managed support action"));
            this.rotation = current;
            this.actionInterface = basic!.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            this.dataCenter = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            this.priorityConfig = basic.GetType("RotationSolver.Basic.Configuration.OtherConfiguration", true)!;
            this.canUse = this.actionInterface.GetMethod("CanUse")!;
            var actions = this.jobAction == SupportAction.Balance
                ? new[] { SupportAction.Balance, SupportAction.Spear, SupportAction.Bole, SupportAction.Arrow, SupportAction.Spire, SupportAction.Ewer }
                : this.jobAction == SupportAction.Resurrection ? [] : [this.jobAction];
            foreach (var action in actions)
            {
                RsrSupportTargetBinding? binding = null;
                var lease = RsrSupportActionLease.Acquire(current, PropertyName(action), (original, method, arguments) =>
                {
                    if (this.faulted) return RsrSupportActionProxy.Forward(original, method, arguments);
                    try { return binding!.Invoke(method, arguments); }
                    catch (Exception ex)
                    {
                        this.Failure(ex);
                        // Never replay an execution whose outcome is unknown.
                        return method.Name == "Use" ? false : RsrSupportActionProxy.Forward(original, method, arguments);
                    }
                });
                this.leases.Add(action, lease);
                binding = new(lease.OriginalAction, this.actionInterface, (eligible, execution) => this.Select(action, eligible, execution),
                    id => this.RecordRequested(action, id), action == SupportAction.Kardia,
                    allowOriginalTargetFallback: !SupportRecipientPolicy.IsAssignment(action));
                lease.Suspend();
            }
            this.AcquireMitigationSequencing(basic, current!);
            if (this.jobAction is not (SupportAction.Balance or SupportAction.Resurrection) || this.RaisedHealingEnabled)
            {
                var remainingAbilities = RsrSupportDutySlot.RemainingAbilities(current, this.dataCenter!);
                this.dutySlot = new(this.dataCenter.GetProperty("CurrentDutyRotation", StaticFlags)!,
                    arguments => this.GuardSelection(() => remainingAbilities(arguments)),
                    arguments => this.SupportEnabled && this.jobAction is not (SupportAction.Balance or SupportAction.Resurrection) &&
                        this.GuardSelection(() => this.OfferTransfer(arguments)),
                    arguments => this.TryOfferRaisedHealing(arguments, basic!));
            }
            if (this.jobAction == SupportAction.DancePartner)
                this.releaseAction = current.GetType().GetProperty("EndingPvE", InstanceFlags)!.GetValue(current);
            if (this.jobAction == SupportAction.FeyUnion)
            {
                this.releaseAction = current.GetType().GetProperty("DissolveUnionPvE", InstanceFlags)!.GetValue(current);
                this.unionRemove = current.GetType().GetProperty("AetherpactRemove", InstanceFlags);
                if (this.unionRemove?.PropertyType != typeof(float) || this.unionRemove.SetMethod == null)
                    this.unionRemove = null;
            }
            if (this.releaseAction != null)
            {
                var originalRelease = this.releaseAction;
                this.releaseProxy = (RsrSupportActionProxy)DispatchProxy.Create(this.actionInterface, typeof(RsrSupportActionProxy));
                this.releaseProxy.Initialize(originalRelease, (methodInfo, arguments) =>
                {
                    if (methodInfo.Name == "Use" && !this.GuardSelection(this.ValidateRelease)) return false;
                    var result = RsrSupportActionProxy.Forward(originalRelease, methodInfo, arguments);
                    if (methodInfo.Name == "Use" && result is true)
                    {
                        this.assignment.Expect(0, DateTime.UtcNow);
                        this.transferApplicationUntil = this.Snapshot?.Preferred != 0 ? DateTime.UtcNow.AddSeconds(10) : default;
                    }
                    return result;
                });
                this.releaseProxy.Active = false;
                this.releaseAction = this.releaseProxy;
            }
            this.callback = this.callbackOwner.Register(this.Enter, this.Failure, order: 10);
            this.Status = "recipient ownership ready";
        }
        catch (Exception ex)
        {
            this.Failure(ex);
            this.Reset();
            this.Status = "Support integration unavailable; RSR keeps control.";
        }
    }

    private IDisposable? Enter()
    {
        if (this.faulted || !this.Enabled || !this.allowed() || this.services.ObjectTable.LocalPlayer is not { IsDead: false } player || !player.IsValid() || player.Address == 0 ||
            this.services.World.Party.Player()?.FindStatus(418) != null ||
            this.dataCenter?.GetProperty("IsPvP", StaticFlags)?.GetValue(null) is true ||
            !this.services.PartyVisible)
        {
            this.Status = "Support recipient management paused; RSR keeps control.";
            this.Snapshot = null;
            return null;
        }
        // An explicit named partner is a persistent user preference in RSR.
        if (this.jobAction == SupportAction.DancePartner &&
            this.rotation?.GetType().GetProperty("DancePartnerName", InstanceFlags)?.GetValue(this.rotation) is string partner &&
            !string.IsNullOrWhiteSpace(partner))
        {
            this.Status = "keeping RSR's named dance partner";
            this.Snapshot = null;
            return null;
        }
        if (!ReferenceEquals(this.dataCenter!.GetProperty("CurrentRotation", StaticFlags)!.GetValue(null), this.rotation)) return null;
        this.Status = "Recipient management ready; RSR keeps action timing.";
        this.inScope = true;
        try
        {
            if (this.SupportEnabled) foreach (var lease in this.leases.Values) lease.Resume();
            if (this.releaseProxy != null) this.releaseProxy.Active = this.SupportEnabled;
            if (this.SupportEnabled && this.unionRemove != null)
            {
                this.originalUnionRemove = this.unionRemove.GetValue(this.rotation);
                this.unionRemove.SetValue(this.rotation, 1.1f);
            }
            if (this.SupportEnabled || this.RaisedHealingEnabled) this.dutySlot?.Enter();
            this.EnterMitigationSequencing();
            return new Scope(this.Exit);
        }
        catch { this.Exit(); throw; }
    }

    private void Exit()
    {
        try
        {
            this.ExitMitigationSequencing();
            this.dutySlot?.Exit();
            if (this.unionRemove != null && this.originalUnionRemove != null && this.unionRemove.GetValue(this.rotation) is 1.1f)
                this.unionRemove.SetValue(this.rotation, this.originalUnionRemove);
        }
        finally
        {
            this.originalUnionRemove = null;
            foreach (var lease in this.leases.Values) lease.Suspend();
            if (this.releaseProxy != null) this.releaseProxy.Active = false;
            this.allowTransfer = this.inScope = false;
        }
    }

    private IBattleChara? Select(SupportAction action, IReadOnlyList<IBattleChara> eligible, bool execution)
    {
        if (!ReferenceEquals(this.dataCenter?.GetProperty("CurrentRotation", StaticFlags)?.GetValue(null), this.rotation)) return null;
        var priorityName = action switch
        {
            SupportAction.DancePartner => "DancePartnerPriority",
            SupportAction.Balance => "TheBalancePriority",
            SupportAction.Spear => "TheSpearPriority",
            _ => "KardiaTankPriority"
        };
        var priorities = (this.priorityConfig?.GetField(priorityName, StaticFlags)?.GetValue(null) as IEnumerable)?
            .Cast<object>().Select(Convert.ToUInt32).ToArray() ?? [];
        if (action == SupportAction.FeyUnion)
        {
            var player = this.services.ObjectTable.LocalPlayer!;
            var fairy = this.services.ObjectTable.OfType<IBattleNpc>().FirstOrDefault(actor => actor.IsValid() && actor.Address != 0 && actor.OwnerId == player.EntityId);
            eligible = fairy == null ? [] : eligible.Where(actor => Vector3.Distance(actor.Position, fairy.Position) <= 30).ToArray();
        }
        var observation = NativeSupportParty.Read(this.services, action, eligible, priorities, this.IncomingDamage);
        SupportDecision decision;
        if (SupportRecipientPolicy.IsAssignment(action))
            decision = this.assignment.Evaluate(action, observation.Recipients, observation.Current, DateTime.UtcNow);
        else
        {
            var best = SupportRecipientPolicy.Best(action, observation.Recipients, 0, executableOnly: true);
            decision = new(best?.Id ?? 0, false, "Best available recipient for RSR's current card");
        }
        if (action == SupportAction.FeyUnion && observation.Current != 0 && !this.assignment.ManualOwned &&
            observation.Recipients.Any(member => member.Id == observation.Current &&
                (member.HealingSuppressed || (member.Health >= 0.98f && member.Attackers == 0 && !member.IncomingHit))))
            decision = new(0, true, "End tether with no ongoing healing need");
        if (observation.Recipients.Any(member => member.Id == decision.Recipient && member.IncomingHit) &&
            action is not (SupportAction.DancePartner or SupportAction.Balance or SupportAction.Spear))
            decision = decision with { Reason = decision.Reason + "; BMR predicts incoming damage to this recipient" };
        var complementaryCoverage = action is (SupportAction.FeyUnion or SupportAction.Kardia) && observation.Current == 0 &&
            observation.Recipients.Any(member => member.Id == decision.Recipient && SupportRecipientPolicy.CoverageTieBreak(action, member) == 0 &&
                observation.Recipients.Any(other => other.CanApply && SupportRecipientPolicy.CoverageTieBreak(action, other) > 0 &&
                    SupportRecipientPolicy.Score(action, other) == SupportRecipientPolicy.Score(action, member)));
        if (complementaryCoverage)
            decision = decision with
            {
                Reason = decision.Reason + (action == SupportAction.Kardia
                ? "; complements another Sage's observed Kardion" : "; complements another Scholar's observed fairy tether")
            };
        if (observation.Recipients.Any(member => member.Id == decision.Recipient && member.RefreshingOwnCard))
            decision = decision with { Reason = decision.Reason + "; RSR-approved local card refresh" };
        var selected = eligible.FirstOrDefault(actor => actor.GameObjectId == decision.Recipient);
        this.Snapshot = new(action.ToString(), observation.Current, decision.Recipient,
            observation.Actors.FirstOrDefault(actor => actor.InstanceID == decision.Recipient)?.PosRot.XYZ(),
            decision.Change, decision.Reason, DateTime.UtcNow)
        {
            ComplementaryCoverage = complementaryCoverage,
            IncomingDamage = observation.Recipients.Any(member => member.Id == decision.Recipient && member.IncomingHit) &&
                action is not (SupportAction.DancePartner or SupportAction.Balance or SupportAction.Spear)
        };
        if (SupportRecipientPolicy.IsAssignment(action))
        {
            if (!decision.Change || (observation.Current != 0 && !this.allowTransfer && !execution)) return null;
            // Existing tethers/partners require a separately validated release.
            if (observation.Current != 0 && action is SupportAction.DancePartner or SupportAction.FeyUnion) return null;
            if (action == SupportAction.FeyUnion && selected != null &&
                SupportRecipientPolicy.Score(action, observation.Recipients.First(member => member.Id == selected.GameObjectId)) <= 0) return null;
        }
        return selected;
    }

    private void RecordRequested(SupportAction action, ulong recipient)
    {
        this.assignment.Expect(recipient, DateTime.UtcNow);
        if (this.Snapshot is { } snapshot && snapshot.Action == action.ToString())
        {
            this.Snapshot = snapshot with
            {
                Preferred = recipient,
                Position = this.services.ObjectTable.OfType<IBattleChara>().FirstOrDefault(actor => actor.IsValid() && actor.GameObjectId == recipient)?.Position,
                RequestSubmitted = true,
                IncomingDamage = this.IncomingDamage?.Invoke(recipient) == true &&
                    action is not (SupportAction.DancePartner or SupportAction.Balance or SupportAction.Spear),
                Reason = "Support request submitted; effect unconfirmed",
                ObservedAt = DateTime.UtcNow
            };
        }
    }



    private bool OfferTransfer(object?[] arguments)
    {
        this.allowTransfer = true;
        try
        {
            this.Snapshot = null;
            // Running the proxied check refreshes the decision from current
            // legal candidates even when it rejects an already-active link.
            if (this.TryAction(this.leases[this.jobAction].Action, out var selected))
            {
                // Initial Union use remains in RSR's healing selectors.
                if (this.jobAction != SupportAction.FeyUnion || DateTime.UtcNow <= this.transferApplicationUntil)
                {
                    arguments[1] = selected;
                    return true;
                }
            }
            if (this.releaseAction != null && this.ReleaseReady() &&
                this.TryAction(this.releaseAction, out selected))
            {
                arguments[1] = selected;
                return true;
            }
            return false;
        }
        finally { this.allowTransfer = false; }
    }

    private bool ValidateRelease()
    {
        if (!this.inScope || this.faulted || !this.Enabled || !this.allowed()) return false;
        this.Snapshot = null;
        this.TryAction(this.leases[this.jobAction].Action, out _);
        return this.ReleaseReady();
    }

    private bool ReleaseReady()
    {
        if (this.Snapshot is not { Current: not 0, Transfer: true } || this.assignment.ManualOwned) return false;
        if (this.jobAction == SupportAction.DancePartner)
        {
            var dancer = this.rotation!.GetType().BaseType!;
            if (dancer.GetProperty("IsDancing", StaticFlags)?.GetValue(null) is not false) return false;
            // Never deliberately remove the recipient during burst buffs.
            if (this.services.ObjectTable.LocalPlayer!.StatusList.Any(status => status.StatusId is 1822 or 1825)) return false;
            foreach (var name in new[] { "StandardStepPvE", "TechnicalStepPvE", "DevilmentPvE" })
            {
                var burstAction = this.rotation.GetType().GetProperty(name, InstanceFlags)!.GetValue(this.rotation)!;
                if (burstAction.GetType().GetProperty("EnoughLevel", InstanceFlags)!.GetValue(burstAction) is not true) continue;
                var burstCooldown = burstAction.GetType().GetProperty("Cooldown", InstanceFlags)!.GetValue(burstAction)!;
                if (burstCooldown.GetType().GetMethod("WillHaveOneCharge", InstanceFlags)!.Invoke(burstCooldown, [5f]) is true)
                    return false;
            }
        }
        var application = this.leases[this.jobAction].OriginalAction;
        if (application.GetType().GetProperty("IsEnabled", InstanceFlags)?.GetValue(application) is not true) return false;
        var cooldown = application.GetType().GetProperty("Cooldown", InstanceFlags)!.GetValue(application)!;
        return cooldown.GetType().GetProperty("HasOneCharge", InstanceFlags)!.GetValue(cooldown) is true;
    }

    private bool TryAction(object action, out object? selected)
    {
        var parameters = this.canUse!.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var i = 1; i < parameters.Length; i++) arguments[i] = parameters[i].DefaultValue;
        var result = RsrSupportActionProxy.Forward(action, this.canUse, arguments) is true;
        selected = arguments[0];
        return result;
    }

    private bool GuardSelection(Func<bool> select)
    {
        if (this.faulted) return false;
        try
        {
            return ReferenceEquals(this.dataCenter?.GetProperty("CurrentRotation", StaticFlags)?.GetValue(null), this.rotation) && select();
        }
        catch (Exception ex)
        {
            this.Failure(ex);
            return false;
        }
    }

    private void Failure(Exception ex)
    {
        this.faulted = true;
        this.Status = "Support integration yielded; RSR keeps control.";
        if (DateTime.UtcNow < this.nextFailureLog) return;
        this.nextFailureLog = DateTime.UtcNow.AddSeconds(10);
        this.services.Log.Verbose(ex, "Support recipient reflection failed.");
    }

    private void Reset()
    {
        this.raisedHealing.Clear();
        this.raisedHealingFaulted = false;
        this.RaisedHealingSnapshot = null;
        if (this.inScope) this.Exit();
        this.ResetMitigationSequencing();
        this.callback?.Dispose();
        this.callback = null;
        foreach (var lease in this.leases.Values) lease.Dispose();
        this.leases.Clear();
        this.dutySlot = null;
        this.rotation = this.releaseAction = null;
        this.releaseProxy?.Release();
        this.releaseProxy = null;
        this.unionRemove = null;
        this.Snapshot = null;
        this.assignment.Reset();
        this.transferApplicationUntil = default;
        this.faulted = false;
    }

    public void Dispose()
    {
        this.UnsubscribeRaisedHealing();
        this.Reset();
    }

    private static string PropertyName(SupportAction action) => action switch
    {
        SupportAction.Kardia => "KardiaPvE",
        SupportAction.DancePartner => "ClosedPositionPvE",
        SupportAction.FeyUnion => "AetherpactPvE",
        _ => "The" + action + "PvE"
    };

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
