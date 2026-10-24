using BossMod.CombatAssistance.Support;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void TankRecovery()
    {
        var now = DateTime.UtcNow;
        var player = new TankRecoveryMember(1, true, false, 100, 100, true, Stance: true, Target: 10);
        var cotank = new TankRecoveryMember(2, true, false, 100, 100, true, Stance: true, Target: 10);
        var healer = new TankRecoveryMember(3, false, false, 100, 100);
        var held = new TankRecoveryEnemy(10, 2, 1, true, false, false, true, 5);
        var policy = new TankRecoveryPolicy();
        Check(policy.Observe(now, 1, [held], [player, cotank, healer], true, true, true, (_, _) => true).IsEmpty,
            "another living tank keeps ownership without an authored swap");
        var swap = held with { PreferProvoke = true };
        Check(policy.Observe(now, 1, [swap], [player, cotank, healer], true, false, false, (_, _) => true).Single().Kind == TankRecoveryKind.AuthoredSwap,
            "exact authored swap overrides inferred co-tank ownership");
        Check(policy.Observe(now, 1, [swap with { ShouldTank = false }], [player, cotank, healer], true, true, true, (_, _) => true).IsEmpty,
            "native assignment to another tank cannot be outweighed by a swap preference");
        Check(policy.Observe(now, 1, [swap with { Priority = -1 }], [player, cotank, healer], true, true, true, (_, _) => true).IsEmpty,
            "pointless or forbidden target cannot become a recovery candidate");
        Check(policy.Observe(now, 1, [swap with { Victim = 1 }], [player, cotank, healer], true, true, true, (_, _) => true).IsEmpty,
            "already owned target does not request another Provoke");
        Check(policy.Observe(now, 1, [swap], [player, cotank, healer], false, false, false, (_, _) => true).IsEmpty,
            "saved opt-outs leave the native rotation in control");
        policy.Reset();
        var loose = held with { Victim = 3, KnownEncounter = false };
        Check(policy.Observe(now, 1, [loose], [player, healer], false, false, true, (_, _) => true).IsEmpty,
            "a target flicker does not establish loose aggro");
        Check(policy.Observe(now.AddSeconds(1), 1, [loose], [player, healer], false, false, true, (_, _) => true).IsEmpty,
            "ordinary loose aggro retains its observation interval");
        Check(policy.Observe(now.AddSeconds(1.5), 1, [loose], [player, healer], false, false, true, (_, _) => true).Single().Kind == TankRecoveryKind.LooseEnemy,
            "sustained party aggro is an independent recovery reason");
        Check(policy.Observe(now.AddSeconds(2), 1, [loose with { KnownEncounter = true }], [player, healer], false, false, true, (_, _) => true).IsEmpty,
            "known boss targeting a healer is not inferred to need Provoke");
        Check(policy.Observe(now.AddSeconds(2), 1, [loose with { Casting = true }], [player, healer], false, false, true, (_, _) => true).IsEmpty,
            "cast targets do not establish auto-attack ownership");
        Check(policy.Observe(now.AddSeconds(3), 1, [loose], [player, healer with { Dead = true, HP = 0 }], false, false, true, (_, _) => true).IsEmpty,
            "a dead victim does not justify ordinary trash recovery");
        policy.Reset();
        var critical = healer with { HP = 20 };
        policy.Observe(now, 1, [loose], [player, critical], false, false, true, (_, _) => true);
        Check(policy.Observe(now.AddSeconds(0.5), 1, [loose], [player, critical], false, false, true, (_, _) => true).Single().Urgency == 2,
            "critical party victim uses the retained half-second confirmation");
        policy.Reset();
        policy.Observe(now, 1, [held], [player, cotank, healer], false, true, false, (_, _) => true);
        var deadTank = cotank with { Dead = true, HP = 0 };
        Check(policy.Observe(now.AddSeconds(0.1), 1, [held with { Victim = 3 }], [player, deadTank, healer], false, true, false, (_, _) => true).Single().Kind == TankRecoveryKind.TankDeath,
            "observed owner death allows the elected tank to recover a known encounter");
        Check(policy.Observe(now.AddSeconds(0.2), 1, [held with { Victim = 3 }], [player, healer], false, true, false, (_, _) => true).IsEmpty,
            "missing owner is not evidence of death");
        Check(policy.Observe(now.AddSeconds(9), 1, [held with { Victim = 3 }], [player, deadTank, healer], false, true, false, (_, _) => true).IsEmpty,
            "old owner history cannot authorize a later unrelated recovery");
        policy.Reset();
        var backup = player with { Id = 4 };
        policy.Observe(now, 4, [held], [player, backup, cotank, healer], false, true, false, (_, _) => true);
        Check(policy.Observe(now.AddSeconds(0.1), 4, [held with { Victim = 3 }], [player, backup, deadTank, healer], false, true, false, (_, _) => true).IsEmpty,
            "second candidate gives the ready first tank a recovery turn");
        Check(policy.Observe(now.AddSeconds(0.75), 4, [held with { Victim = 3 }], [player, backup, deadTank, healer], false, true, false, (_, _) => true).Length == 1,
            "an unavailable first tank does not indefinitely block a backup");
        Check(policy.Observe(now.AddSeconds(0.8), 4, [held with { Victim = 3 }], [player, backup, deadTank, healer], false, true, false, (p, _) => p.Id != 4).IsEmpty,
            "tank election includes actual Provoke range");
        policy.Reset();
        Check(policy.Observe(now.AddSeconds(1), 4, [held with { Victim = 3 }], [player, backup, deadTank, healer], false, true, false, (_, _) => true).IsEmpty,
            "manual handoff or logout discards prior owner-death authority");
        Check(TankRecoveryPolicy.CanRetarget(3, 2, 0) && !TankRecoveryPolicy.CanRetarget(7, 2, 0)
            && TankRecoveryPolicy.CanRetarget(7, 2, 2), "held pack does not get dragged toward an ordinary loose enemy");

        var attack = new RsrActionEvidence(100, 10, default, [10], false, false, 1, 20, 0, 0, 0, 2.5f, 0.1f, 0, 0.6f);
        Check(NativeTankRecovery.PreservesCurrentAction(attack, false, 100, 10), "exact selected ranged opportunity may deliver to its original target");
        Check(!NativeTankRecovery.PreservesCurrentAction(attack, false, 100, 11), "aggro score cannot replace the current target");
        Check(!NativeTankRecovery.PreservesCurrentAction(attack with { Friendly = true }, false, 100, 10), "tank recovery cannot replace a selected heal");
        Check(!NativeTankRecovery.PreservesCurrentAction(attack with { AffectedRecipients = [10, 11] }, false, 100, 10),
            "another evidenced recipient cannot disappear behind a recovery score");
        Check(!NativeTankRecovery.PreservesCurrentAction(null, false, 100, 10), "unavailable action evidence is not an empty GCD");
        Check(NativeTankRecovery.PreservesCurrentAction(null, true, 100, 10), "observed absence of a selected GCD permits ranged recovery");
    }
}
