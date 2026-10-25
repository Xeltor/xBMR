namespace BossMod.AI;

// Enable-time correction of hidden legacy controls only. Keep harmless fallback
// values, situational opt-outs, encounter safety and manual handoff independent.
internal static class AIActivationSettings
{
    internal static bool Normalize(AIConfig config)
    {
        var changed = config.ForbidMovement || config.ForbidActions || config.ManualTarget
            || config.AIAutorotPresetName != null || !config.FollowDuringCombat
            || !config.FollowDuringActiveBossModule || !config.FollowTarget
            || !config.UseCombatPositioningRange || config.MinDistance != 0
            || config.MoveDelay != 0 || config.DisableObstacleMaps;
        config.ForbidMovement = false;
        config.ForbidActions = false;
        config.ManualTarget = false;
        config.AIAutorotPresetName = null;
        config.FollowDuringCombat = true;
        config.FollowDuringActiveBossModule = true;
        config.FollowTarget = true;
        config.UseCombatPositioningRange = true;
        config.MinDistance = 0;
        config.MoveDelay = 0;
        config.DisableObstacleMaps = false;
        return changed;
    }
}
