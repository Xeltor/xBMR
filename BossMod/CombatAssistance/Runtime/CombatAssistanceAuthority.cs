namespace BossMod.CombatAssistance.Runtime;

// Shared player availability is independent of the authority to steer them.
internal static class CombatAssistanceAuthority
{
    internal static bool CanAct(Actor? player, bool localPlayerMatches, bool flying,
        bool cutscene, bool pvp, bool loading)
        => player is { IsDead: false, IsDestroyed: false, MountId: 0 } && localPlayerMatches
            && !flying && !cutscene && !pvp && !loading;

    internal static bool CanSteer(bool forbidden, bool paused, bool requested, bool forceUnblocked, bool manualFacing)
        => !forbidden && !paused && !requested && !forceUnblocked && !manualFacing;
}
