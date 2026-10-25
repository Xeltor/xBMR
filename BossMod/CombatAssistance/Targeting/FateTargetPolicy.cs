namespace BossMod.CombatAssistance.Targeting;

internal static class FateTargetPolicy
{
    internal static bool Matches(uint actorFate, uint activeFate, bool synced, byte progress)
        => synced && activeFate != 0 && actorFate == activeFate && progress < 100;
}
