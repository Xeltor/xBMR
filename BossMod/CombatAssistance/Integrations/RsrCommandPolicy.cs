namespace BossMod.CombatAssistance.Integrations;

// RSR 7.5.7.6 adds automatic Burst to CommandStatus, even with no user command.
// Preserve explicit command priority without mistaking that automatic bit for
// manual authority. Unknown contracts remain conservative.
internal static class RsrCommandPolicy
{
    internal static bool HasExplicitCommand(Enum? status, Enum? special, bool queuedAction = false)
    {
        if (queuedAction || status == null || special == null || !Enum.IsDefined(special.GetType(), special)) return true;
        var bits = Convert.ToUInt64(status);
        if (bits == 0) return false;
        if (special.ToString() == "Burst") return true;
        if (!Enum.TryParse(status.GetType(), "Burst", out var burst) || burst is not Enum automaticBurst) return true;
        return (bits & ~Convert.ToUInt64(automaticBurst)) != 0;
    }
}
