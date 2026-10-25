namespace BossMod.CombatAssistance.Targeting;

internal static class FateTargetPolicy
{
    internal static bool Matches(uint actorFate, uint activeFate, bool synced, byte progress)
        => synced && activeFate != 0 && actorFate == activeFate && progress < 100;

    // Target acquisition must be possible before the first attack establishes
    // player/party combat. This grants only the targeting gate; normal action,
    // manual handoff, range and authored target restrictions still apply.
    internal static bool HasAcquisition(AIHints hints, uint activeFate, bool synced, byte progress)
        => hints.PotentialTargets.Any(enemy => enemy.Priority >= 0
            && enemy.RsrTargetDecision is { MayAcquire: true }
            && enemy.Actor is { IsTargetable: true, IsDead: false, IsDestroyed: false, PendingDead: false } actor
            && Matches(actor.FateID, activeFate, synced, progress));
}
