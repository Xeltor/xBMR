using BossMod.CombatAssistance.Config;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;

namespace BossMod.CombatAssistance.Support;

internal sealed unsafe class RaiseAcceptanceController(Func<AssistancePreferences> preferences, WorldState world, Func<bool> allowed,
    IObjectTable objects, ICondition condition, IGameGui gui, IPluginLog log)
{
    private AssistancePreferences config => preferences();
    private readonly RaiseAcceptancePolicy policy = new();
    public string Status { get; private set; } = "not evaluated";
    public string LastOfferStatus { get; private set; } = "no offer observed";
    private string lastLoggedBlocker = string.Empty;

    public void Update()
    {
        var player = objects.LocalPlayer;
        if (player != null && (!player.IsValid() || player.Address == 0)) player = null;
        var dead = player is { IsDead: true, CurrentHp: 0 } && condition[ConditionFlag.Unconscious];
        var pendingRaise = dead && player!.StatusList.Any(status => RaiseAcceptancePolicy.IsPendingRaiseStatus(status.StatusId));
        var enabled = allowed() && config.AutoAcceptRaises &&
            world.Party.Player() is { MountId: 0 } &&
            !condition[ConditionFlag.OccupiedInCutSceneEvent] &&
            !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51];

        // Read and dispatch on the framework thread without scheduling a generic
        // confirmation. A Raise effect alone never selects an arbitrary dialog.
        AddonSelectYesno* addon = null;
        RaiseOfferIdentity offer = default;
        float timeLeftSeconds = 0;
        var blocker = !enabled ? "disabled or transitioning" : !dead
            ? $"death state not ready: dead={player?.IsDead}, hp={player?.CurrentHp}, unconscious={condition[ConditionFlag.Unconscious]}"
            : !pendingRaise ? "pending Raise status absent" : "offer not evaluated";
        var offerReady = enabled && pendingRaise && this.TryGetOfferControl(out addon, out offer, out timeLeftSeconds, out blocker);
        this.Status = offerReady ? "eligible resurrection offer" : blocker;
        if (config.AutoAcceptRaises && dead && pendingRaise && !offerReady && blocker != this.lastLoggedBlocker)
        {
            log.Information($"Resurrection acceptance blocked: {blocker}");
            this.LastOfferStatus = $"{DateTime.UtcNow:O}: blocked: {blocker}";
            this.lastLoggedBlocker = blocker;
        }
        if (!pendingRaise)
        {
            this.lastLoggedBlocker = string.Empty;
        }
        if (!this.policy.TryBeginAttempt(enabled, dead, pendingRaise, offerReady,
                player?.EntityId ?? 0, world.CurrentZone,
                offer, Environment.TickCount64, timeLeftSeconds))
        {
            if (offerReady)
            {
                this.Status = this.policy.RemainingDelayMilliseconds > 0
                    ? $"accepting resurrection offer in {this.policy.RemainingDelayMilliseconds} ms"
                    : "acceptance already attempted during this Raise episode";
            }
            return;
        }

        // Re-resolve immediately before dispatch; an expired/replaced offer or
        // closed dialog consumes this attempt without redirecting it elsewhere.
        if (player == null || !player.IsValid() || player.Address == 0 || !player.StatusList.Any(status => RaiseAcceptancePolicy.IsPendingRaiseStatus(status.StatusId)) ||
            !this.TryGetOfferControl(out var current, out var currentOffer, out _, out _) ||
            current != addon || currentOffer != offer)
        {
            this.Status = "offer changed before dispatch; attempt consumed";
            this.LastOfferStatus = $"{DateTime.UtcNow:O}: {this.Status}";
            return;
        }

        var events = current->YesButton->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;
        current->AtkUnitBase.ReceiveEvent(events->State.EventType, (int)events->Param, events);
        this.Status = "acceptance dispatched; awaiting revival";
        this.LastOfferStatus = $"{DateTime.UtcNow:O}: {this.Status}";
        log.Information("Dispatched acceptance for the current resurrection offer while pending Raise was active.");
    }

    private bool TryGetOfferControl(out AddonSelectYesno* addon, out RaiseOfferIdentity offer,
        out float timeLeftSeconds, out string blocker)
    {
        addon = null;
        offer = default;
        timeLeftSeconds = 0;
        blocker = "revive agent unavailable";
        var agent = AgentRevive.Instance();
        if (agent == null)
        {
            return false;
        }
        blocker = $"native offer not ready: state={agent->State}, timerPositive={agent->ResurrectionTimeLeft > 0}, raiserValid={agent->ResurrectingPlayerId is not (0 or 0xE0000000)}, revivePresent={agent->Revive != null}";
        if (agent->Revive == null || agent->State != ReviveState.Revivable ||
            agent->ResurrectionTimeLeft <= 0 || agent->ResurrectingPlayerId is 0 or 0xE0000000)
        {
            return false;
        }

        var handle = gui.GetAddonByName("SelectYesno", 1);
        blocker = handle.IsNull ? "resurrection dialog absent" : "dialog callback ownership mismatch";
        if (handle.IsNull)
        {
            return false;
        }

        var unit = (AtkUnitBase*)handle.Address;
        var atkModule = RaptureAtkModule.Instance();
        // This shared dialog uses the revive callback or the revive agent's own
        // callback; the agent AddonId can remain zero while the offer is active.
        if (unit->Id == 0 || atkModule == null)
        {
            blocker = "dialog callback mapping unavailable";
            return false;
        }

        if (!atkModule->AddonCallbackMapping.TryGetValue((uint)unit->Id, out var callback, false))
        {
            blocker = "dialog callback entry absent";
            return false;
        }

        if ((nint)callback.EventInterface != (nint)agent->Revive &&
            (nint)callback.EventInterface != (nint)agent)
        {
            blocker = $"dialog callback differs from revive offer: agentOffset={(long)((nint)callback.EventInterface - (nint)agent)}, reviveOffset={(long)((nint)callback.EventInterface - (nint)agent->Revive)}, eventKind={callback.EventKind}";
            return false;
        }

        blocker = "revive dialog is hidden or not ready";
        if (!unit->IsReady || !unit->IsVisible)
        {
            return false;
        }

        var candidate = (AddonSelectYesno*)unit;
        var button = candidate->YesButton;
        blocker = button == null ? "acceptance button absent" : $"acceptance control not ready: enabled={button->IsEnabled}, type={button->GetComponentType()}";
        if (button == null || !button->IsEnabled || button->GetComponentType() != ComponentType.Button ||
            button->AtkComponentBase.OwnerNode == null || !button->AtkResNode->IsVisible() ||
            button->AtkResNode->AtkEventManager.Event == null)
        {
            return false;
        }

        addon = candidate;
        offer = new RaiseOfferIdentity((nint)agent->Revive, (uint)agent->ResurrectingPlayerId,
            (nint)unit, (uint)unit->Id);
        timeLeftSeconds = (float)agent->ResurrectionTimeLeft;
        blocker = "ready";
        return true;
    }
}
