using BossMod;
using BossMod.CombatAssistance.Support;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void TankStance()
    {
        var policy = new TankStancePolicy();
        var now = DateTime.UtcNow;
        Check(policy.Evaluate(now, false, true, false, 2, 3, 0.9f) == true,
            "holding an enemy preserves tank responsibility over co-tank threat");
        Check(policy.Evaluate(now, false, false, true, 2, 3, 0.9f) == true,
            "native tank assignment takes precedence before the swap is executed");
        Check(policy.Evaluate(now, true, false, false, 2, 3, 0.9f) == null,
            "co-tank ownership must settle before dropping stance");
        Check(policy.Evaluate(now.AddSeconds(3), true, false, false, 2, 3, 0.9f) == false,
            "established co-tank lead is protected at high relative enmity");
        Check(policy.Evaluate(now.AddSeconds(4), false, false, false, 2, 3, 0.7f) == null,
            "middle enmity band retains the current stance");
        Check(policy.Evaluate(now.AddSeconds(5), false, false, false, 2, 3, 0.5f) == true,
            "low backup enmity enables stance without an immediate ownership flip");
        Check(policy.Evaluate(now.AddSeconds(6), true, false, false, 2, 3, null) == null,
            "missing threat cannot undo a stance enabled by another controller");
        Check(policy.Evaluate(now.AddSeconds(6), true, false, false, 2, 4, 0.9f) == null,
            "changed holder restarts ownership observation");
        Check(policy.Evaluate(now.AddSeconds(5), true, false, false, 2, 4, 0.9f) == null,
            "world-time rewind cannot reuse settled ownership");
        policy.Reset();
        Check(policy.Evaluate(now.AddSeconds(10), true, false, false, 2, 4, 0.9f) == null,
            "logout death or manual handoff clears ownership history");
        var hate = new ClientState.HateInfo(2, [new(1, 500), new(3, 1000)]);
        Check(TankStancePolicy.RelativeThreat(hate, 2, 1, 3) == 0.5f, "native enmity uses exact target and actor identities");
        Check(TankStancePolicy.RelativeThreat(hate, 4, 1, 3) == null, "previous target's enmity is not reusable");
        Check(TankStancePolicy.RelativeThreat(hate, 2, 1, 5) == null, "missing co-tank is not zero enmity");
        Check(TankStancePolicy.RelativeThreat(new(2, [new(1, 1), new(3, 0)]), 2, 1, 3) == null,
            "zero holder enmity cannot authorize a release");
        Check(TankStancePolicy.RelativeThreat(new(2, [new(1, 1001), new(3, 1000)]), 2, 1, 3) == null,
            "inconsistent ownership reading yields instead of assuming a settled target");
        Check(TankStancePolicy.RelativeThreat(new(2, [new(1, 1), new(1, 2), new(3, 1000)]), 2, 1, 3) == null,
            "duplicate identity is not a valid native threat sample");
        Check(RsrTankStanceSelection.Property(Class.DRG) == null && RsrTankStanceSelection.Property(Class.PLD) == "IronWillPvE"
            && NativeTankStance.Release(Class.GNB).ID == 32068, "native job definitions preserve exact stance actions");
    }
}
