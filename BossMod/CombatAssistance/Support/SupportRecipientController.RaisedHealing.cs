using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BossMod.CombatAssistance.Support;

internal sealed partial class SupportRecipientController
{
    private readonly RaisedHealingPolicy raisedHealing = new();
    private bool raisedHealingSubscribed;
    private bool raisedHealingFaulted;
    public SupportRecipientSnapshot? RaisedHealingSnapshot { get; private set; }

    private bool RaisedHealingEnabled => this.config.Enabled && this.config.HealRaisedNonPartyPlayers &&
        this.services.ObjectTable.LocalPlayer?.ClassJob.RowId is 24 or 28 or 33 or 40;

    private IDisposable? raisedSubscription;
    private void SubscribeRaisedHealing()
    {
        this.raisedSubscription = this.services.World.Actors.CastEvent.Subscribe(this.ObserveRaisedPlayer);
        this.raisedHealingSubscribed = true;
    }

    private void UnsubscribeRaisedHealing()
    {
        this.raisedSubscription?.Dispose();
        this.raisedSubscription = null;
        this.raisedHealingSubscribed = false;
    }

    private void ObserveRaisedPlayer(Actor source, ActorCastEvent effect)
    {
        if (source.InstanceID != this.services.World.Party.Player()?.InstanceID || !effect.IsSpell()) return;
        if (!this.RaisedHealingEnabled || !this.allowed() || effect.Action.ID is not (125 or 173 or 3603 or 24287)) return;
        foreach (var target in effect.Targets)
            if (ConfirmsRaise(target.Effects)
                && this.services.World.Actors.Find(target.ID) is { Type: ActorType.Player, IsDead: true }
                && !this.services.IsParty(target.ID))
                this.raisedHealing.ObserveRaise(target.ID, DateTime.UtcNow);
    }

    private static bool ConfirmsRaise(ActionEffects effects)
    {
        foreach (var effect in effects.AsSpan())
            if (effect.Type == ActionEffectType.ApplyStatusEffectTarget && effect.Value is 148 or 1140) return true;
        return false;
    }

    private void RefreshRaisedHealing()
    {
        if (this.RaisedHealingSnapshot is { } snapshot && DateTime.UtcNow - snapshot.ObservedAt > TimeSpan.FromMilliseconds(750))
            this.RaisedHealingSnapshot = null;
        if (!this.RaisedHealingEnabled || this.services.ObjectTable.LocalPlayer is not { IsDead: false } ||
            this.services.Mounted)
        {
            this.raisedHealing.Clear();
            this.RaisedHealingSnapshot = null;
            return;
        }
        foreach (var id in this.raisedHealing.Actors)
        {
            var actor = this.services.World.Actors.Find(id);
            this.raisedHealing.Observe(id, actor is { IsDestroyed: false }, actor?.IsDead ?? true,
                this.services.IsParty(id), actor?.HPMP.CurHP ?? 0, actor?.HPMP.MaxHP ?? 0, DateTime.UtcNow);
        }
        if (this.RaisedHealingSnapshot is { } selected && !this.raisedHealing.Actors.Contains(selected.Preferred))
            this.RaisedHealingSnapshot = null;
    }

    private bool TryOfferRaisedHealing(object?[] arguments, Assembly basic)
    {
        if (this.raisedHealingFaulted) return false;
        try { return this.OfferRaisedHealing(arguments, basic); }
        catch (Exception ex)
        {
            this.raisedHealingFaulted = true;
            this.raisedHealing.Clear();
            this.RaisedHealingSnapshot = null;
            this.services.Log.Verbose(ex, "Post-resurrection healing unavailable; preserving native rotation and other support.");
            return false;
        }
    }

    private bool OfferRaisedHealing(object?[] arguments, Assembly basic)
    {
        if (!this.inScope || !this.RaisedHealingEnabled || !this.raisedHealingSubscribed || !this.allowed() ||
            this.actionInterface?.GetProperty("ActionPreview", StaticFlags)?.GetValue(null) is not false ||
            this.services.ObjectTable.LocalPlayer is not { IsDead: false, IsCasting: false } player ||
            this.dataCenter?.GetMethod("IsActivatedIPC", StaticFlags)?.Invoke(null, null) is not true ||
            this.dataCenter.GetProperty("IsTargetOnly", StaticFlags)?.GetValue(null) is not false ||
            this.dataCenter.GetProperty("CurrentInterceptedAction", StaticFlags)?.GetValue(null) != null ||
            RsrCommandPolicy.HasExplicitCommand(this.dataCenter.GetProperty("CommandStatus", StaticFlags)?.GetValue(null) as Enum,
                this.dataCenter.GetProperty("SpecialType", StaticFlags)?.GetValue(null) as Enum)) return false;
        if (this.raisedHealing.Actors.Length == 0) return false;
        // These encounter statuses disable ordinary party healing in RSR's
        // healing selectors. The general-GCD fallback must respect them too.
        var statusType = basic.GetType("RotationSolver.Basic.Data.StatusID", true)!;
        var scalebound = Convert.ToUInt32(Enum.Parse(statusType, "Scalebound"));
        var shackled = Convert.ToUInt32(Enum.Parse(statusType, "ShackledHealing"));
        if (player.StatusList.Any(status => status.StatusId == scalebound || status.StatusId == shackled)) return false;
        var actions = player.ClassJob.RowId switch
        {
            24 => new (string Name, uint Id)[] { ("RegenPvE", 137), ("CureIiPvE", 135) },
            28 => [("PhysickPvE", 190)],
            33 => [("AspectedBeneficPvE", 3595), ("BeneficIiPvE", 3610)],
            40 => [("DiagnosisPvE", 24284)],
            _ => throw new InvalidOperationException("Unsupported post-resurrection healer"),
        };
        foreach (var actorId in this.raisedHealing.Actors)
        {
            if (this.services.ObjectTable.SearchById(actorId) is not IPlayerCharacter { IsDead: false, IsTargetable: true } recipient || !recipient.IsValid() || recipient.Address == 0 ||
                !this.raisedHealing.Observe(actorId, true, false,
                    this.services.IsParty(actorId),
                    recipient.CurrentHp, recipient.MaxHp, DateTime.UtcNow)) continue;
            foreach (var (name, expectedId) in actions)
            {
                var action = this.rotation!.GetType().GetProperty(name, InstanceFlags)!.GetValue(this.rotation)!;
                if (action.GetType().GetProperty("AdjustedID", InstanceFlags)?.GetValue(action) is not uint id || id != expectedId)
                    continue;
                var binding = new RsrSupportTargetBinding(action, this.actionInterface!,
                    (eligible, _) => eligible.FirstOrDefault(actor => actor.GameObjectId == recipient.GameObjectId), _ => { }, false);
                // The ordinary friendly range cache contains party members only.
                // Restore this scoped single-player replacement before another selector.
                using var scope = new RsrExternalHealTargetScope(basic, this.dataCenter, action, recipient);
                if (!scope.Eligible) continue;
                var parameters = this.canUse!.GetParameters();
                var check = new object?[parameters.Length];
                for (var i = 1; i < parameters.Length; i++) check[i] = parameters[i].DefaultValue;
                if (binding.Invoke(this.canUse, check) is not true) continue;
                arguments[0] = check[0];
                this.RaisedHealingSnapshot = new("Raise recovery", 0, recipient.GameObjectId, recipient.Position, false,
                    $"{name} offered for your raised non-party player; execution unconfirmed", DateTime.UtcNow);
                return true;
            }
        }
        return false;
    }
}
