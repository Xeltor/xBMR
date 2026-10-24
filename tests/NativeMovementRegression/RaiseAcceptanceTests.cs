using BossMod.CombatAssistance.Support;
using BossMod.CombatAssistance.Config;



internal static class RaiseAcceptanceTests
{
    public static void OfferLifecycle()
    {
        Checks.Check(RaiseAcceptancePolicy.IsPendingRaiseStatus(148) &&
            RaiseAcceptancePolicy.IsPendingRaiseStatus(1140), "Both pending Raise variants permit offer inspection");
        Checks.Check(!RaiseAcceptancePolicy.IsPendingRaiseStatus(2648) &&
            !RaiseAcceptancePolicy.IsPendingRaiseStatus(0), "Transcendent and absent statuses cannot authorize acceptance");
        var config = new AssistancePreferences();
        Checks.Check(!config.AutoAcceptRaises, "Resurrection acceptance defaults off");
        config.AutoAcceptRaises = true;
        config.ResetAll();
        Checks.Check(!config.AutoAcceptRaises, "Reset restores manual resurrection acceptance");

        var policy = new RaiseAcceptancePolicy(() => 800);
        var firstOffer = new RaiseOfferIdentity((nint)100, 42, (nint)200, 130);
        var secondOffer = new RaiseOfferIdentity((nint)101, 43, (nint)201, 131);
        bool Attempt(bool enabled = true, bool dead = true, bool raise = true, bool ready = true,
            uint player = 42, uint territory = 1346, RaiseOfferIdentity? offer = null,
            long now = 1000, float timeLeft = 50)
            => policy.TryBeginAttempt(enabled, dead, raise, ready, player, territory,
                offer ?? firstOffer, now, timeLeft);

        Checks.Check(!Attempt(enabled: false), "Disabled acceptance does not dispatch");
        Checks.Check(!Attempt(raise: false), "Death return without Raise never dispatches");
        Checks.Check(!Attempt(dead: false), "A living player never accepts an offer");
        Checks.Check(!Attempt(ready: false), "Hidden, unowned or disabled dialogs do not dispatch");
        Checks.Check(!Attempt(), "A fresh eligible offer starts its response delay");
        Checks.Check(policy.RemainingDelayMilliseconds == 800, "Delay is drawn once per offer");
        Checks.Check(!Attempt(now: 1799), "A ready offer waits for its response deadline");
        Checks.Check(Attempt(now: 1800), "The same offer dispatches after the response delay");
        Checks.Check(!Attempt(now: 2000), "Server delay cannot cause repeated acceptance");
        Attempt(ready: false);
        Checks.Check(!Attempt(), "Dialog replacement does not reset an attempted Raise episode");
        Attempt(enabled: false);
        Checks.Check(!Attempt(), "Toggling the option cannot retry a dispatched offer");
        Checks.Check(!Attempt(raise: false), "Expiry cannot accept a newly shown Return dialog");
        Checks.Check(!Attempt(), "A later Raise episode gets its own delay");
        Checks.Check(Attempt(now: 1800), "A later Raise episode may be accepted");
        Checks.Check(!Attempt(raise: false, territory: 1252), "Zoning without Raise never accepts Return");
        Checks.Check(!Attempt(territory: 1252), "A fresh offer in the new territory gets a delay");
        Checks.Check(Attempt(territory: 1252, now: 1800), "A fresh offer in the new territory is independent");
        Checks.Check(!Attempt(player: 0), "Missing player identity never dispatches");

        Checks.Check(!Attempt(offer: firstOffer), "A new pending Raise starts a delay");
        Checks.Check(!Attempt(offer: secondOffer, now: 1700), "A changed offer restarts the delay");
        Checks.Check(!Attempt(offer: secondOffer, now: 2499), "The replacement does not inherit elapsed time");
        Checks.Check(Attempt(offer: secondOffer, now: 2500), "Only the replacement offer dispatches");
        Attempt(raise: false);
        Checks.Check(Attempt(timeLeft: 1.5f), "An almost expired offer bypasses the delay");
        Attempt(raise: false);
        Checks.Check(!Attempt(timeLeft: 30), "A new offer starts its delay");
        Checks.Check(!Attempt(now: 1400, timeLeft: 45), "A renewed offer with reused native pointers restarts its delay");
        Checks.Check(!Attempt(now: 2199, timeLeft: 44), "A renewed offer keeps its own deadline");
        Checks.Check(Attempt(now: 2200, timeLeft: 44), "A renewed offer dispatches after its own delay");
    }
}
