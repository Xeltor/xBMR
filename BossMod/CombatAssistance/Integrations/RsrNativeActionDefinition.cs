namespace BossMod.CombatAssistance.Integrations;

// This upstream base models Manafication's ranged swords through the original
// sword definitions (ActionQueue.CanExecute adjusts range while the status is
// active). RSR 7.5.7.6 can select the separate adjusted action IDs. Borrow only
// that known family's native metadata; actual identity, cast timing, range and
// recipients still come from the exact selected action and current game data.
internal static class RsrNativeActionDefinition
{
    internal static ActionDefinition? Find(uint selected, Func<ActionID, ActionDefinition?> lookup)
    {
        var exact = lookup(new(ActionType.Spell, selected));
        if (exact != null) return exact;
        var family = selected switch { 45960 => 7527u, 45961 => 7528u, 45962 => 7529u, _ => 0u };
        return family != 0 ? lookup(new(ActionType.Spell, family)) : null;
    }
}
