namespace BossMod.CombatAssistance.Planning;

internal enum ComboIntentKind { Continuation, Starter, RotationHold }
internal enum RedMageRotationKind { Other, Reborn, Beiruta }
internal readonly record struct RedMageIntentState(RedMageRotationKind Rotation, uint LastCombo, float ComboRemaining,
    byte ManaStacks, bool Manafication, bool InSingleTargetCombo, bool ContinuationResources, bool StarterResources,
    bool InstantBeforeMelee, bool EnoughForRotationStarter, bool AoePermitted, float HoldRemaining);
internal readonly record struct RedMageIntentChoice(ComboIntentKind Kind, uint Action, uint Fallback, int MinimumTargets, float EndsIn);

// These are observed rotation policy gates, not a generic mana threshold or a
// prediction adapter for native BMR rotations. No action is submitted or credited
// as delivered by this optional preparation objective.
internal static class RedMageIntentPolicy
{
    internal static bool IsRangedFinisher(uint id) => id is 7525 or 7526 or 16530 or 25858 or 45960 or 45961 or 45962;
    // Known ranged mana-generating GCDs do not consume a sword-combo step or
    // its gauge budget. Already-selected sword actions use ordinary execution
    // geometry; Reprise and unknown actions cannot borrow unchanged resources.
    internal static bool CanPrepareAfter(uint id) => id is 7503 or 7505 or 7507 or 7509 or 7510 or 7511 or 7524
        or 16524 or 16525 or 16526 or 25855 or 25856 or 37004 or 37006;

    internal static RedMageIntentChoice? Choose(RedMageIntentState state, IReadOnlySet<uint> available, int aoeMinimum)
    {
        if (state.ManaStacks >= 3) return null;
        var next = state.LastCombo switch
        {
            7527 when !state.Manafication && state.InSingleTargetCombo => 7528u,
            7528 when !state.Manafication && state.InSingleTargetCombo => 7529u,
            7530 => 37002u,
            37002 => 37003u,
            _ => 0u
        };
        if (next != 0 && state.ContinuationResources && float.IsFinite(state.ComboRemaining) && state.ComboRemaining > 0 && available.Contains(next))
            return new(ComboIntentKind.Continuation, next, 0, 1, state.ComboRemaining);
        // A partial/expired combo cannot turn into a fresh starter preparation.
        if (state.ManaStacks != 0 || state.InstantBeforeMelee || !state.StarterResources) return null;
        var kind = state.Rotation switch
        {
            RedMageRotationKind.Reborn when state.EnoughForRotationStarter || state.Manafication => ComboIntentKind.Starter,
            RedMageRotationKind.Beiruta when float.IsFinite(state.HoldRemaining) && state.HoldRemaining > 0 => ComboIntentKind.RotationHold,
            _ => (ComboIntentKind?)null
        };
        if (kind == null) return null;
        var single = !state.Manafication && available.Contains(7527);
        var aoe = state.AoePermitted && available.Contains(7530) && aoeMinimum >= 1;
        var endsIn = kind == ComboIntentKind.RotationHold ? state.HoldRemaining : MovementPlanningContext.MaximumHorizonSeconds;
        return aoe ? new(kind.Value, 7530, single ? 7527u : 0, aoeMinimum, endsIn)
            : single ? new(kind.Value, 7527, 0, 1, endsIn) : null;
    }
}
