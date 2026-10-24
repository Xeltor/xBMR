using Dalamud.Plugin.Services;

namespace BossMod.CombatAssistance.Support;

// Framework-thread current state. RSR owns eligible action targets; BMR owns party,
// encounter damage and observed action effects. Nothing here is sent to a worker.
internal sealed class NativeSupportContext(WorldState world, AIHints hints, IObjectTable objects, IPluginLog log)
{
    internal WorldState World => world;
    internal IObjectTable ObjectTable => objects;
    internal IPluginLog Log => log;
    internal bool Mounted => world.Party.Player()?.MountId != 0;
    internal bool InCombat => world.Party.Player()?.InCombat == true;
    internal Actor[] Party => world.Party.WithoutSlot(includeDead: true, excludeAlliance: true);
    internal bool IsParty(ulong id) => Party.Any(a => a.InstanceID == id);
    internal bool PartyVisible => world.Party.Members.Take(PartyState.MaxPartySize)
        .Where(m => m.IsValid()).All(m => world.Actors.Find(m.InstanceId) is { IsDestroyed: false });

    internal bool IncomingDamage(ulong actorId)
    {
        var slot = world.Party.FindSlot(actorId);
        return slot >= 0 && hints.PredictedDamage.Any(d => d.Players[slot]
            && d.Activation > world.CurrentTime && d.Activation <= world.FutureTime(10));
    }

    internal DateTime? MitigationDeadline()
    {
        var upcoming = hints.PredictedDamage.Where(d => d.Activation > world.CurrentTime
            && d.Activation <= world.FutureTime(10) && d.Players.Any()).Select(d => d.Activation).ToArray();
        return upcoming.Length == 0 ? null : DateTime.UtcNow + (upcoming.Min() - world.CurrentTime);
    }
}
