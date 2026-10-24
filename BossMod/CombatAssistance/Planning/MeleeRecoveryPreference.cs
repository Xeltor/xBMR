namespace BossMod.CombatAssistance.Planning;

// A melee ranged fallback is evidence of lost melee access, not a requirement
// to stop and deliver that fallback before restoring access. Real attacks,
// healing recipients and authored movement constraints retain their own goals.
internal static class MeleeRecoveryPreference
{
    internal static uint FallbackAction(Class job) => job switch
    {
        Class.LNC or Class.DRG => 90,
        Class.ROG or Class.NIN => 2247,
        Class.SAM => 7486,
        Class.RPR => 24386,
        Class.VPR => 34632,
        _ => 0
    };

    internal static bool IsFallback(Class job, uint action) => action != 0 && action == FallbackAction(job);

    internal static Positional DesiredPositional(Positional selected, float executesIn, float bypassUntil)
        => bypassUntil > Math.Max(0, executesIn) ? Positional.Any : selected;

    internal static PlanningAction? ForPlanning(Class job, PlanningAction? action)
        => action != null && IsFallback(job, action.Id) ? null : action;
}
