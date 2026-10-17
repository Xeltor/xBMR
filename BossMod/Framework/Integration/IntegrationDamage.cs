using System.Globalization;

namespace BossMod.Integration;

// Copy identity and forecasts together on the native frame thread. A party member can
// have a known instance ID while its actor is absent from the local object table.
internal static class IntegrationDamage
{
    internal const int MaxPredictions = 128;
    internal sealed record PartySlot(int Slot, string? ActorId, bool ActorLoaded);
    internal sealed record Prediction(string ActivationTicks, float? SecondsFromWorldTime, string Type,
        string RecipientSlotMask, string[] RecipientActorIds, bool RecipientsComplete);
    internal sealed record Publication(bool Available, IntegrationSession.Context Context, bool Complete,
        string WorldTimeTicks, string WorldTimeKind, PartySlot[] PartySlots, Prediction[] Predictions);

    internal static Publication Capture(PartyState party, IReadOnlyList<AIHints.DamagePrediction> predictions,
        DateTime worldTime, IntegrationSession.Context context)
    {
        var slots = party.Members.Select((m, slot) => new PartySlot(slot,
            m.InstanceId == 0 ? null : m.InstanceId.ToString("X"), m.InstanceId != 0 && party[slot]?.InstanceID == m.InstanceId)).ToArray();
        var copied = predictions.Take(MaxPredictions).Select(p =>
        {
            var recipients = slots.Where(s => p.Players[s.Slot]).ToArray();
            var seconds = (float)(p.Activation - worldTime).TotalSeconds;
            return new Prediction(p.Activation.Ticks.ToString(CultureInfo.InvariantCulture), float.IsFinite(seconds) ? seconds : null,
                p.Type.ToString(), p.Players.Raw.ToString("X"), recipients.Where(s => s.ActorId != null).Select(s => s.ActorId!).ToArray(),
                recipients.All(s => s.ActorId != null));
        }).ToArray();
        return new(true, context, predictions.Count <= MaxPredictions, worldTime.Ticks.ToString(CultureInfo.InvariantCulture),
            worldTime.Kind.ToString(), slots, copied);
    }
}
