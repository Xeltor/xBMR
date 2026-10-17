using BossMod;
using BossMod.Integration;

static partial class Checks
{
    private static void DamagePublications()
    {
        var party = new PartyState(new ActorState());
        party.Members[0] = new(100, 0xAA, false);
        party.Members[1] = new(101, 0xBB, false);
        party.Members[63] = new(0, 0xCC, false);
        var time = new DateTime(2026, 10, 17, 9, 45, 0, DateTimeKind.Utc);
        var context = new IntegrationSession.Context(1, "damage-test", 1, "hints-ready");
        var forecasts = new List<AIHints.DamagePrediction>
        {
            new(new(3), time.AddSeconds(1), AIHints.PredictedDamageType.Raidwide),
            new(new(1UL << 63), time.AddSeconds(1.5), AIHints.PredictedDamageType.None),
            new(new(1UL << 2), time.AddSeconds(2), AIHints.PredictedDamageType.Raidwide)
        };
        var first = IntegrationDamage.Capture(party, forecasts, time, context);
        Check(first.Complete && first.Predictions.Length == 3, "overlapping forecasts retain every bounded entry");
        Check(first.Predictions[0].RecipientActorIds.SequenceEqual(new[] { "AA", "BB" }) && first.Predictions[0].RecipientsComplete,
            "known recipient identities survive absent local actors");
        Check(!first.PartySlots[0].ActorLoaded && first.PartySlots[0].ActorId == "AA", "identity and local actor availability are separate");
        Check(first.Predictions[1].RecipientActorIds.Single() == "CC" && first.Predictions[1].RecipientSlotMask == "8000000000000000",
            "all 64 native party/NPC slots retain exact mask semantics");
        Check(!first.Predictions[2].RecipientsComplete && first.Predictions[2].RecipientActorIds.Length == 0,
            "unresolved recipient slots cannot become complete empty recipient lists");
        Check(first.WorldTimeTicks == time.Ticks.ToString() && first.Predictions[0].SecondsFromWorldTime == 1
            && first.Predictions[1].ActivationTicks == time.AddSeconds(1.5).Ticks.ToString(), "overlap deadlines share the exact world clock");
        party.Members[0] = new(102, 0xDD, false);
        forecasts.Clear();
        forecasts.Add(new(new(1), time.AddSeconds(3)));
        var second = IntegrationDamage.Capture(party, forecasts, time.AddSeconds(0.5), context with { FrameId = 2 });
        Check(second.Predictions[0].RecipientActorIds.Single() == "DD" && second.Predictions[0].SecondsFromWorldTime == 2.5f,
            "new observation maps changed slots with its own clock and frame");
        Check(first.PartySlots[0].ActorId == "AA" && first.Predictions[0].RecipientActorIds[0] == "AA" && first.Predictions.Length == 3,
            "later party/list changes cannot rewrite a retained publication");
        for (var i = 0; i < 128; ++i) forecasts.Add(new(new(1), time));
        var truncated = IntegrationDamage.Capture(party, forecasts, time, context);
        Check(!truncated.Complete && truncated.Predictions.Length == 128, "truncation is bounded and explicit");
        Check(IntegrationDamage.Capture(party, [], time, context).Complete, "complete empty native forecast remains distinguishable from truncation");
    }
}
