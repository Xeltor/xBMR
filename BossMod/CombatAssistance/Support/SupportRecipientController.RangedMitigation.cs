using Dalamud.Game.ClientState.Objects.Types;

namespace BossMod.CombatAssistance.Support;

internal sealed partial class SupportRecipientController
{
    private bool DeferRangedMitigation(object? selection, IBattleChara player, DateTime now, DateTime deadline)
    {
        this.MitigationStatus = "native ranged mitigation retained";
        if (!player.IsValid() || player.Address == 0
            || selection?.GetType().GetProperty("AffectedTargets")?.GetValue(selection) is not IBattleChara[] affected
            || affected.Length == 0 || affected.Any(a => !a.IsValid() || a.Address == 0)) return false;
        var ids = affected.Select(a => a.GameObjectId).Append(player.GameObjectId).Distinct().ToArray();
        var party = this.services.Party;
        var recipients = ids.Select(id => party.FirstOrDefault(a => a.InstanceID == id)).ToArray();
        if (recipients.Any(a => a == null || a.IsDead)) return false;
        var sources = party.Where(a => a.Level >= 98 || player.Level < 98)
            .Where(a => a.Class is Class.BRD or Class.MCH or Class.DNC).Select(a => a.InstanceID).ToHashSet();
        var effects = recipients.OfType<Actor>().SelectMany(a => a.Statuses.Where(s => sources.Contains(s.SourceID))
            .Select(s => new PartyMitigationEffect(a.InstanceID, s.SourceID, s.ID,
                Math.Max(0, (float)(s.ExpireAt - this.services.World.CurrentTime).TotalSeconds)))).ToArray();
        if (!PartyRangedMitigationPolicy.Covered(now, deadline, ids, effects)) return false;
        this.MitigationStatus = "party ranged protection covers this hit; preserving cooldown";
        return true;
    }
}
