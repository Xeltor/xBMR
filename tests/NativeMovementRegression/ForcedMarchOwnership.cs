using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void ForcedMarchOwnership()
    {
        void Require(bool condition, string why) => Check(condition, why);
        Require(!new ForcedMarchTiming(14, 0, 1).ProtectFacing, "Early countdown preserves attacks.");
        Require(new ForcedMarchTiming(1.05, 0, 1).ProtectFacing, "Final turn window protects facing.");
        Require(new ForcedMarchTiming(-0.2, 0, 1).ProtectFacing, "Status transition margin remains protected.");
        Require(!new ForcedMarchTiming(-0.5, 0, 1).ProtectFacing, "Stale pending move cannot hold rotation.");
        Require(new ForcedMarchTiming(10, 2, 1).ProtectFacing, "Active march retains protection before later moves.");
        Require(!new ForcedMarchTiming(10, 0, 1).ProtectFacing, "Separated marches allow attacks between windows.");
        foreach (var original in new[] { RsrMode.Auto, RsrMode.Manual, RsrMode.Henched, RsrMode.AutoDuty, RsrMode.TargetOnly })
        {
            RsrMode? mode = original;
            object? provider = new object();
            var available = true;
            var writes = 0;
            var lease = new ForcedMarchRotationOwner(() => available ? (mode, provider) : (null, null), value => { writes++; mode = value; return true; });
            lease.Tick(true, 1);
            Require(lease.Owned && mode == RsrMode.Off, "Pause owns only Off.");
            lease.Tick(true, 1);
            Require(writes == 1, "Renewal avoids repeated Off commands.");
            available = false;
            lease.Tick(false, 0);
            Require(lease.Owned, "Lost read retains restoration obligation.");
            available = true;
            lease.Tick(false, 0);
            Require(!lease.Owned && mode == original, "Exact previous mode restores after IPC recovers.");
            lease.Tick(true, 2);
            mode = RsrMode.Manual;
            lease.Tick(true, 2);
            Require(!lease.Owned, "External mode change releases control.");
            lease.Tick(true, 2);
            Require(mode == RsrMode.Manual, "Same mechanic cannot reacquire after external change.");
            lease.Tick(false, 0);
            lease.Tick(true, 3);
            provider = new object();
            mode = RsrMode.Auto;
            lease.Tick(true, 3);
            lease.Tick(false, 0);
            Require(mode == RsrMode.Auto, "Provider reload never restores stale provider mode.");
        }
        RsrMode? uncertain = RsrMode.Auto;
        var owner = new object();
        var failRestore = true;
        var uncertainLease = new ForcedMarchRotationOwner(() => (uncertain, owner), value =>
        {
            if (value != RsrMode.Off && failRestore) return false;
            uncertain = value;
            return value != RsrMode.Off;
        });
        uncertainLease.Tick(true, 1);
        uncertainLease.Tick(false, 0);
        Require(uncertainLease.Owned, "Uncertain Off application and failed restore retain obligation.");
        failRestore = false;
        uncertainLease.Tick(false, 0);
        Require(uncertain == RsrMode.Auto && !uncertainLease.Owned, "Restore retry recovers previous mode.");
        uncertainLease.Tick(true, 2);
        uncertainLease.Abandon();
        uncertainLease.Tick(false, 0);
        Require(uncertain == RsrMode.Off && !uncertainLease.Owned, "Explicit linked disable cancels restoration and preserves Off.");
        RsrMode? nestedMode = RsrMode.Off;
        var nestedProvider = typeof(Checks).Assembly;
        var failNestedRestore = false;
        bool NestedWrite(RsrMode value)
        {
            if (failNestedRestore && value == RsrMode.Henched) return false;
            nestedMode = value;
            return true;
        }
        var lifetimeOwner = new LinkedRotationOwner(() => nestedMode, NestedWrite);
        lifetimeOwner.Update(true, true, true, false);
        var nestedPause = new ForcedMarchRotationOwner(() => (nestedMode, nestedProvider), NestedWrite);
        nestedPause.Tick(true, 1);
        failNestedRestore = true;
        nestedPause.Tick(false, 0);
        if (!nestedPause.Owned) lifetimeOwner.Release();
        Require(nestedMode == RsrMode.Off && nestedPause.Owned, "Disable retains the lifetime owner while nested restore is pending.");
        failNestedRestore = false;
        nestedPause.Tick(false, 0);
        lifetimeOwner.Release();
        Require(nestedMode == RsrMode.Off, "Nested disable restores the original pre-enable mode once RSR recovers.");

    }
}
