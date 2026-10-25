using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Targeting;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void RsrTargetSettings()
    {
        var authority = new RsrTargetAuthority(true, true, true, false, false);
        RsrTargetRules Rules(Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool> allowed,
            Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool> named,
            Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool> specific)
            => RsrTargetRules.Capture(authority, allowed, named, specific)!;
        RsrTargetAuthorityIntegration();
        // The provider owns the option predicates. Exercise the integration's
        // handling of their results without manufacturing live Dalamud actors.
        foreach (var option in new[] { "enemy parts", "FATE", "Forlorn", "hunt/relic/leve", "quest" })
        {
            var enabled = false;
            var reader = Rules(_ => true, _ => false, _ => enabled);
            var disabled = reader.Observe(null!);
            enabled = true;
            var selected = reader.Observe(null!);
            enabled = false;
            Check(disabled.EligiblePriority(-3) == -3 && selected.EligiblePriority(-3) == 0
                && reader.Observe(null!).EligiblePriority(-3) == -3,
                $"Live {option} preference grants and revokes specific acquisition without a generic engage override.");
            Check(selected.PreferredPriority(0, true) == 0 && selected.PreferredPriority(3, true) == 3
                && selected.PreferredPriority(-4, false) == -4 && selected.PreferredPriority(-2, false) == -2,
                $"{option} cannot override BMR priorities, assignments or independent safety restrictions.");
        }
        foreach (var option in new[] { "only FATE enemies", "focus Forlorns", "other players' quest enemies" })
        {
            var excluded = false;
            var reader = Rules(_ => !excluded, _ => true, _ => true);
            Check(reader.Observe(null!).MayAcquire, $"{option}: the allowed candidate remains eligible.");
            excluded = true;
            var blocked = reader.Observe(null!);
            Check(!blocked.MayAcquire && blocked.Preference == 0 && blocked.EligiblePriority(3) == AIHints.Enemy.PriorityForbidden,
                $"{option}: RSR's exclusion also forbids native selection, movement targeting and collateral, despite preference.");
        }
        var unknown = Rules(_ => throw new InvalidOperationException(), _ => true, _ => true).Observe(null!);
        Check(unknown.Permission == RsrTargetPermission.Unavailable && !unknown.MayAcquire, "A failed external observation cannot authorize a pull.");
        Check(Rules(_ => true, _ => true, _ => true).Observe(null!).Preference == 2
            && Rules(_ => true, _ => false, _ => true).Observe(null!).Preference == 1,
            "Named RSR priority remains above its ordinary specific preferences, below authored BMR targets.");
        var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        var moduleEnemy = new AIHints.Enemy(actor, 1, false);
        var preferredEnemy = new AIHints.Enemy(actor, 0, false) { RsrTargetDecision = new(RsrTargetPermission.Allowed, 2) };
        var hints = new AIHints();
        hints.PotentialTargets.AddRange([moduleEnemy, preferredEnemy]);
        Check(RsrTargetRules.ApplyPriorities(hints) && preferredEnemy.Priority == 0 && !preferredEnemy.RsrTargetPreference,
            "Even the smallest positive BMR priority stays above named RSR preference; integer ranks cannot collide.");
        moduleEnemy.Priority = 0;
        moduleEnemy.ShouldBeTargeted = true;
        Check(RsrTargetRules.ApplyPriorities(hints) && preferredEnemy.Priority == 0,
            "An explicit priority-zero BMR assignment also prevents RSR preference promotion.");
        hints.PotentialTargets.Remove(moduleEnemy);
        Check(!RsrTargetRules.ApplyPriorities(hints) && preferredEnemy.Priority == 2 && preferredEnemy.RsrTargetPreference,
            "Without authored targeting, RSR preferences are applied and their origin remains explicit.");
        preferredEnemy.Priority = 5;
        preferredEnemy.RsrTargetDecision = new(RsrTargetPermission.Excluded, 2);
        RsrTargetRules.ApplyPriorities(hints);
        Check(preferredEnemy.Priority == AIHints.Enemy.PriorityForbidden && !preferredEnemy.RsrTargetPreference,
            "A hard RSR user exclusion is retained after module writers and clears assisted preference provenance.");
        var player = new ExecutionActor(1, default, 0, .5f, default);
        var ordinary = new OffensiveTargetActor(new(2, new(3, 0), 0, .5f, default), 0, true);
        var specific = new OffensiveTargetActor(new(3, new(4, 0), 0, .5f, default), 1, true, RsrPreferred: true);
        var frame = new OffensiveTargetFrame(player, 2, 0, false, true, false, 0, [ordinary, specific]);
        Check(frame.PriorityAcquisition(3) == 3 && (frame with { RetainSelection = true }).PriorityAcquisition(3) == 0
            && (frame with { Acquire = false }).PriorityAcquisition(3) == 0,
            "Specific bootstrap respects manual handoff and native acquisition opt-outs.");
        var module = ordinary with { Priority = 3 };
        Check((frame with { Actors = [module, specific] }).Acquisition == 2
            && (frame with { Forced = 2 }).PriorityAcquisition(3) == 0,
            "BMR module targeting wins over both RSR preference and acquisition.");
        foreach (var policy in Enum.GetValues<RsrHostilePolicy>())
        {
            bool Allows(bool target = false, bool solo = false, bool duty = false, bool smart = false)
                => RsrFallbackTargeting.AllowsEngagement(policy, false, target, solo, duty, smart);
            Check(RsrFallbackTargeting.AllowsEngagement(policy, true, false, false, false, false),
                "RSR fallback can defend against enemies targeting the player or their pet.");
            Check(Allows() == (policy == RsrHostilePolicy.AllTargetsCanAttack),
                $"{policy}: restrictive engagement does not become attack-everything in Henched.");
            if (policy == RsrHostilePolicy.SoloDeepDungeonSmart)
                Check(!Allows(true) && Allows(smart: true), "Solo deep-dungeon fallback uses the original RSR smart predicate.");
            else
                Check(Allows(true), $"{policy}: previously engaged targets remain eligible.");
            if (policy == RsrHostilePolicy.AllTargetsWhenSolo)
                Check(Allows(solo: true) && !Allows(), "Solo-only fallback checks actual party composition.");
            if (policy == RsrHostilePolicy.AllTargetsWhenSoloInDuty)
                Check(Allows(solo: true, duty: true) && !Allows(solo: true) && !Allows(duty: true),
                    "Solo-in-duty fallback requires both conditions.");
        }
        RsrFallbackTargeting Fallback(bool nearest) => new(RsrHostilePolicy.AllTargetsCanAttack, 1, true, false,
            nearest, _ => false, () => Array.Empty<object>(), (_, _, _) => false);
        Check(!Fallback(false).CanAcquire(null!) && Fallback(true).CanAcquire(null!),
            "Without a usable original action, the optional nearest-target fallback must be explicitly enabled.");
        var actionReads = 0;
        var cached = new RsrFallbackTargeting(RsrHostilePolicy.AllTargetsCanAttack, 1, true, false, false,
            _ => false, () => { ++actionReads; return Array.Empty<object>(); }, (_, _, _) => false);
        Check(!cached.CanAcquire(null!) && !cached.CanAcquire(null!) && actionReads == 1,
            "Fallback readiness is captured once per framework snapshot rather than rescanned for every passive candidate.");
    }

    private static void RsrTargetAuthorityIntegration()
    {
        var henched = new RsrTargetAuthority(true, true, true, false, false);
        var validityCalls = 0;
        (AIHints Hints, AIHints.Enemy Enemy, OffensiveTargetFrame Frame) Pipeline(RsrTargetAuthority authority,
            bool assigned, bool specific = false, bool excluded = false, bool unavailable = false)
        {
            var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = 10;
            actor.FateID = specific ? 123u : 0;
            var wrapper = Character(10);
            var engage = new RsrFallbackTargeting(RsrHostilePolicy.TargetsHaveTarget, 1, true, false, false,
                _ => false, () => Array.Empty<object>(), (_, _, _) => false);
            Check(!engage.Allows(wrapper), "The passive integration fixture is rejected by actual restrictive fallback engagement.");
            var rules = RsrTargetRules.Capture(authority, c =>
                {
                    ++validityCalls;
                    if (unavailable) throw new InvalidOperationException("unavailable provider");
                    // Model the owning RSR predicate's actual authority branch:
                    // Off/Auto reject the passive candidate on engagement, while
                    // Henched bypasses engagement without bypassing exclusions.
                    return !excluded && (authority.Henched == true || engage.Allows(c) || specific);
                }, _ => false, _ => specific, engage);
            // This is the same initial-enemy constructor and post-module hint
            // pass used by AIHintsBuilder, followed by real normalized selection.
            var enemy = RsrTargetRules.CreateEnemy(actor, AIHints.Enemy.PriorityUndesirable, false, rules?.Observe(wrapper),
                actor.FateID == 0 || FateTargetPolicy.Matches(actor.FateID, 123, true, 50));
            if (assigned) enemy.ShouldBeTargeted = true;
            var hints = new AIHints();
            hints.PotentialTargets.Add(enemy);
            RsrTargetRules.ApplyPriorities(hints);
            hints.Normalize();
            var frame = new OffensiveTargetFrame(new(1, default, 0, .5f, default), 0, 0, false, true, false, 0,
                [new(new(10, new(3, 0), 0, .5f, default), enemy.Priority, enemy.Priority >= 0,
                    ForbiddenCollateral: enemy.Priority <= AIHints.Enemy.PriorityUndesirable,
                    RsrPreferred: enemy.RsrTargetPreference)]);
            return (hints, enemy, frame);
        }

        foreach (var authority in new[]
        {
            new RsrTargetAuthority(false, false, false, false, false), // Off retains CurrentRotation.
            henched with { Active = false }, // A stale mode flag is not active authority.
            new RsrTargetAuthority(true, false, false, false, false), // Auto owns its own targets.
            new RsrTargetAuthority(true, false, true, false, false), // Explicit Manual.
            henched with { TargetOnly = true }, henched with { PvP = true }, henched with { Active = null }
        })
        {
            var before = validityCalls;
            var assigned = Pipeline(authority, true);
            Check(assigned.Enemy.RsrTargetDecision == null && validityCalls == before
                && assigned.Enemy.Priority == 0 && assigned.Hints.PriorityTargetsSpan.Length == 1
                && assigned.Frame.MissingTargetAcquisition(false) == 10,
                "Inactive/non-delegated RSR cannot evaluate its engagement predicate or veto a BMR-assigned passive target, despite a retained rotation.");
            var ordinary = Pipeline(authority, false, specific: true);
            Check(ordinary.Enemy.Priority == AIHints.Enemy.PriorityUndesirable
                && ordinary.Hints.PriorityTargetsSpan.IsEmpty && ordinary.Frame.Acquisition == 0,
                "Without delegated authority, loaded RSR preference does not pull passive targets for native BMR.");
        }
        var bmr = Pipeline(henched, true);
        Check(bmr.Enemy.RsrTargetDecision is { Permission: RsrTargetPermission.Allowed, EngagementAllowed: false }
            && bmr.Enemy.Priority == 0 && bmr.Hints.PriorityTargetsSpan.Length == 1 && bmr.Frame.Acquisition == 10,
            "Active Henched keeps restrictive general engagement separate from permission for a BMR-authored passive target.");
        var unassigned = Pipeline(henched, false);
        Check(unassigned.Enemy.RsrTargetDecision is { Permission: RsrTargetPermission.Allowed, EngagementAllowed: false }
            && unassigned.Enemy.Priority == AIHints.Enemy.PriorityUndesirable
            && unassigned.Hints.PriorityTargetsSpan.IsEmpty && unassigned.Frame.Acquisition == 0,
            "An ordinary unassigned passive target still fails restrictive fallback engagement without becoming hard-forbidden.");
        var blocked = Pipeline(henched, true, specific: true, excluded: true);
        Check(blocked.Enemy.RsrTargetDecision is { Permission: RsrTargetPermission.Excluded }
            && blocked.Enemy.Priority == AIHints.Enemy.PriorityForbidden && blocked.Hints.PriorityTargetsSpan.IsEmpty
            && blocked.Frame.Acquisition == 0,
            "Genuine exclusions remain effective for hints, assigned selection and acquisition in active Henched.");
        var fate = Pipeline(henched, false, specific: true);
        Check(fate.Enemy.RsrTargetPreference && fate.Enemy.Priority == 1 && fate.Hints.PriorityTargetsSpan.Length == 1
            && fate.Frame.PriorityAcquisition(10) == 10,
            "Henched's specific FATE preference still admits and prioritizes a passive matching target despite restrictive generic engagement.");
        var unknown = Pipeline(henched, true, unavailable: true);
        Check(unknown.Enemy.RsrTargetDecision is { Permission: RsrTargetPermission.Unavailable }
            && unknown.Enemy.Priority == 0 && unknown.Frame.Acquisition == 10,
            "Failed observation is unavailable evidence, not a fabricated hard exclusion overriding BMR assignment.");
    }
}
