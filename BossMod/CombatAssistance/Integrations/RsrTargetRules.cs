using Dalamud.Game.ClientState.Objects.Types;

namespace BossMod.CombatAssistance.Integrations;

// Framework-thread observation only. Store the resulting values in native hints,
// never these live provider delegates in a worker input.
internal sealed class RsrTargetRules
{
    private readonly Func<IBattleChara, bool> _validWhileHenched;
    private readonly Func<IBattleChara, bool> _named;
    private readonly Func<IBattleChara, bool> _specific;
    private readonly RsrFallbackTargeting? _fallback;

    private RsrTargetRules(Func<IBattleChara, bool> validWhileHenched,
        Func<IBattleChara, bool> named, Func<IBattleChara, bool> specific, RsrFallbackTargeting? fallback)
    {
        _validWhileHenched = validWhileHenched; _named = named; _specific = specific; _fallback = fallback;
    }

    internal static RsrTargetRules? Capture(RsrTargetAuthority authority, Func<IBattleChara, bool> validWhileHenched,
        Func<IBattleChara, bool> named, Func<IBattleChara, bool> specific, RsrFallbackTargeting? fallback = null)
        => authority.DelegatesNativeTargeting ? new(validWhileHenched, named, specific, fallback) : null;

    internal static AIHints.Enemy CreateEnemy(Actor actor, int priority, bool tank, RsrTargetDecision? decision,
        bool fateEligible = true)
        => new(actor, decision?.EligiblePriority(priority, fateEligible) ?? priority, tank) { RsrTargetDecision = decision };

    internal RsrTargetDecision Observe(IBattleChara actor)
    {
        try
        {
            // Only active Henched can reach this reader: its IsAttackable path
            // bypasses general engagement. A rejection here is an exclusion;
            // restrictive engagement is captured independently by the fallback.
            if (!_validWhileHenched(actor)) return new(RsrTargetPermission.Excluded, 0);
            return new(RsrTargetPermission.Allowed, _named(actor) ? 2 : _specific(actor) ? 1 : 0, _fallback?.Allows(actor) == true);
        }
        catch { return new(RsrTargetPermission.Unavailable, 0); }
    }
    internal bool CanAcquireFallback(IBattleChara actor) => _fallback?.CanAcquire(actor) == true;

    internal static bool ApplyPriorities(AIHints hints)
    {
        var authored = hints.AuthoritativeTarget != null
            || hints.PotentialTargets.Any(e => e.Priority > 0 || e.ExplicitTargetRequirement == true);
        foreach (var enemy in hints.PotentialTargets)
        {
            if (enemy.RsrTargetDecision is not { } decision) continue;
            if (decision.Permission == RsrTargetPermission.Excluded)
            {
                // Preference never overrides hard user exclusions, including
                // for movement/AoE. Module priority is still above preference.
                enemy.ForcePriority(AIHints.Enemy.PriorityForbidden);
                continue;
            }
            var priority = decision.PreferredPriority(enemy.Priority, authored);
            if (priority == enemy.Priority) continue;
            enemy.Priority = priority;
            enemy.RsrTargetPreference = true;
        }
        return authored;
    }
}

// A loaded rotation is not authority. Off can retain CurrentRotation, and Auto
// owns its own selection. Only explicit active Henched delegates targets here.
internal readonly record struct RsrTargetAuthority(bool? Active, bool? Henched, bool? Manual, bool? TargetOnly, bool? PvP)
{
    internal bool DelegatesNativeTargeting => Active == true && Henched == true && Manual == true
        && TargetOnly == false && PvP == false;
}

internal enum RsrTargetPermission { Unavailable, Allowed, Excluded }

internal readonly record struct RsrTargetDecision(RsrTargetPermission Permission, int Preference, bool EngagementAllowed = false)
{
    internal bool MayAcquire => Permission == RsrTargetPermission.Allowed && Preference > 0;

    internal int EligiblePriority(int priority, bool fateEligible = true)
        => Permission == RsrTargetPermission.Excluded ? AIHints.Enemy.PriorityForbidden
            : priority == AIHints.Enemy.PriorityUndesirable && MayAcquire && fateEligible ? 0 : priority;

    internal int PreferredPriority(int priority, bool authored)
        => Permission == RsrTargetPermission.Allowed && !authored && priority == 0 ? Preference : priority;
}
