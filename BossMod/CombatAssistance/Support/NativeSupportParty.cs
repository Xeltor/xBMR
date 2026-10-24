using Dalamud.Game.ClientState.Objects.Types;

namespace BossMod.CombatAssistance.Support;

internal sealed record SupportPartyObservation(Actor[] Actors, SupportRecipient[] Recipients, ulong Current);

internal static class NativeSupportParty
{
    internal static SupportPartyObservation Read(NativeSupportContext context, SupportAction action,
        IReadOnlyList<IBattleChara> eligible, IReadOnlyList<uint> jobPriority, Func<ulong, bool>? incomingDamage)
    {
        if (context.World.Party.Player() is not { IsDestroyed: false } player) return new([], [], 0);
        var actors = context.Party;
        var scholars = actors.Where(a => a.InstanceID != player.InstanceID && a.Class == Class.SCH && !a.IsDead)
            .Select(a => a.InstanceID).ToHashSet();
        var sages = actors.Where(a => a.InstanceID != player.InstanceID && a.Class == Class.SGE && !a.IsDead)
            .Select(a => a.InstanceID).ToHashSet();
        var applicable = eligible.Where(a => a.IsValid() && a.Address != 0).Select(a => a.GameObjectId).ToHashSet();
        var recipients = new List<SupportRecipient>();
        ulong current = 0;
        var expected = action switch
        {
            SupportAction.Kardia => 2605u,
            SupportAction.DancePartner => 1824u,
            SupportAction.FeyUnion => 1223u,
            SupportAction.Balance => 3887u,
            SupportAction.Arrow => 3888u,
            SupportAction.Spear => 3889u,
            SupportAction.Bole => 3890u,
            SupportAction.Ewer => 3891u,
            SupportAction.Spire => 3892u,
            _ => 0u
        };
        foreach (var actor in actors)
        {
            if (actor.IsDestroyed || action == SupportAction.DancePartner && actor == player) continue;
            var role = actor.Role switch
            {
                Role.Tank => SupportRole.Tank,
                Role.Healer => SupportRole.Healer,
                Role.Melee => SupportRole.Melee,
                _ => SupportRole.Ranged
            };
            var priority = jobPriority.Count;
            for (var i = 0; i < jobPriority.Count; ++i)
                if (jobPriority[i] == (uint)actor.Class) { priority = i; break; }
            var canApply = applicable.Contains(actor.InstanceID);
            var otherPartner = false; var otherKardion = false; var otherUnion = false;
            var equivalent = false; var refreshing = false; var multiplier = 1f;
            foreach (var status in actor.Statuses)
            {
                if (status.ID == 0 || status.ExpireAt <= context.World.CurrentTime) continue;
                var owner = context.World.Actors.Find(status.SourceID)?.OwnerID ?? 0;
                var own = status.SourceID == player.InstanceID || owner == player.InstanceID;
                if (status.ID == expected && own && SupportRecipientPolicy.IsAssignment(action)) current = actor.InstanceID;
                otherPartner |= status.ID == 1824 && !own;
                otherKardion |= SupportEffectOwnership.IsOtherKardion(status.ID, status.SourceID, player.InstanceID, sages);
                otherUnion |= SupportEffectOwnership.IsOtherFeyUnion(status.ID, status.SourceID, owner, player.InstanceID, scholars);
                equivalent |= SupportEffectOwnership.BlocksCardRecipient(status.ID, expected, own, canApply);
                refreshing |= status.ID == expected && own && canApply;
                if (status.ID == 43) multiplier = Math.Min(multiplier, 0.75f);
                if (status.ID == 44) multiplier = 0.5f;
            }
            recipients.Add(new(actor.InstanceID, role, !actor.IsDead && actor.HPMP.CurHP > 0, canApply,
                actor.HPMP.MaxHP == 0 ? float.NaN : (float)actor.HPMP.CurHP / actor.HPMP.MaxHP,
                context.World.Actors.Count(a => a.Type == ActorType.Enemy && !a.IsDead && !a.IsAlly && a.InCombat && a.TargetID == actor.InstanceID),
                incomingDamage?.Invoke(actor.InstanceID) == true,
                actor.HPMP.MaxHP == 0 ? 0 : (float)actor.HPMP.Shield / actor.HPMP.MaxHP,
                HasOtherPartner: otherPartner, HasOtherKardion: otherKardion, HasEquivalentCard: equivalent,
                DamageMultiplier: multiplier, JobPriority: priority, HasOtherFeyUnion: otherUnion, RefreshingOwnCard: refreshing));
        }
        return new(actors, recipients.ToArray(), current);
    }
}
