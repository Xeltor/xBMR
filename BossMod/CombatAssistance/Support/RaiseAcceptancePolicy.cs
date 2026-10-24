using System;

namespace BossMod.CombatAssistance.Support;

internal readonly record struct RaiseOfferIdentity(nint Revive, uint Raiser, nint Dialog, uint DialogId);

// One dispatch per continuous pending-Raise episode, even if the UI stays open
// while the server processes acceptance. UI readiness does not reset the latch.
internal sealed class RaiseAcceptancePolicy(Func<int>? chooseDelay = null)
{
    private readonly Func<int> chooseDelay = chooseDelay ?? (() => 0);
    public static bool IsPendingRaiseStatus(uint statusId) => statusId is 148 or 1140;
    public int RemainingDelayMilliseconds { get; private set; }

    private uint playerId;
    private uint territoryId;
    private bool attempted;
    private RaiseOfferIdentity waitingOffer;
    private float lastTimeLeftSeconds;
    private long readyAtMilliseconds;
    private bool waiting;

    public bool TryBeginAttempt(bool enabled, bool dead, bool pendingRaise, bool offerReady,
        uint player, uint territory, RaiseOfferIdentity offer, long nowMilliseconds, float timeLeftSeconds)
    {
        if (player != this.playerId || territory != this.territoryId)
        {
            this.playerId = player;
            this.territoryId = territory;
            this.attempted = false;
            this.waiting = false;
        }

        if (!dead || !pendingRaise || player == 0)
        {
            this.attempted = false;
            this.waiting = false;
            this.RemainingDelayMilliseconds = 0;
            return false;
        }

        if (!enabled || !offerReady || this.attempted)
        {
            this.waiting = false;
            this.RemainingDelayMilliseconds = 0;
            return false;
        }

        if (!this.waiting || this.waitingOffer != offer ||
            timeLeftSeconds > this.lastTimeLeftSeconds + 0.25f)
        {
            this.waitingOffer = offer;
            this.readyAtMilliseconds = nowMilliseconds + this.chooseDelay();
            this.waiting = true;
        }
        this.lastTimeLeftSeconds = timeLeftSeconds;

        this.RemainingDelayMilliseconds = (int)Math.Max(0, this.readyAtMilliseconds - nowMilliseconds);
        // Do not lose an offer that is already close to expiring when first seen.
        if (this.RemainingDelayMilliseconds > 0 && timeLeftSeconds > 2f)
        {
            return false;
        }

        this.attempted = true;
        this.waiting = false;
        this.RemainingDelayMilliseconds = 0;
        return true;
    }
}
