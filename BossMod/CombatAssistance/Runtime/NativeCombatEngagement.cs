namespace BossMod.CombatAssistance.Runtime;

internal static class NativeCombatEngagement
{
    // The local flag can drop between waves while a support NPC remains engaged.
    // Reuse BMR's own eligible enemy/party relationships, including Duty Support;
    // unrelated NPC battles and untouched enemies do not authorize assistance.
    internal static bool Active(WorldState world, AIHints hints, Actor player)
    {
        if (player.InCombat) return true;
        foreach (var enemy in hints.PriorityTargetsSpan)
        {
            var actor = enemy.Actor;
            if (actor.InCombat && !actor.IsDead && !actor.IsDestroyed && actor.IsTargetable
                && world.Party.FindSlot(actor.TargetID) is >= 0 and < PartyState.MaxPartySize)
                return true;
        }
        return false;
    }
}
