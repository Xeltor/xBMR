namespace BossMod.CombatAssistance.Planning;

internal static class NativeSagePackApproach
{
    // A not-yet-started ranged filler must not reserve every GCD while a moving
    // pack gets farther away. Actual casts remain protected by native unlock;
    // healing and instant actions stay in RSR's evidenced sequence.
    internal static PlanningAction? ForPlanning(Class job, PlanningAction? action, PlanningRange? pack, WPos origin)
        => job == Class.SGE && pack?.NeedsRecovery(origin) == true && action is { CastSeconds: > 0 }
            && (SGE.AID)action.Id is SGE.AID.Dosis or SGE.AID.DosisII or SGE.AID.DosisIII ? null : action;
}
