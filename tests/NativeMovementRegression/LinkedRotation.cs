using BossMod.CombatAssistance.Runtime;
using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private sealed record OverrideFixture(byte? HostileType, bool? TargetFreely, bool? AutoOffAfterCombat, bool? FriendlyPartyNpcHealRaise);
    private static class ModeDataFixture
    {
        public static OverrideFixture? IpcOverrides { get; set; }
        public static int? TargetingTypeOverride { get; set; }
    }

    private static void LinkedRotation()
    {
        var player = (BossMod.Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BossMod.Actor));
        var mayActivate = CombatAssistanceAuthority.CanAct(player, true, false, false, false, false);
        Check(mayActivate && !CombatAssistanceAuthority.CanAct(null, true, false, false, false, false)
            && !CombatAssistanceAuthority.CanAct(player, false, false, false, false, false)
            && !CombatAssistanceAuthority.CanAct(player, true, true, false, false, false)
            && !CombatAssistanceAuthority.CanAct(player, true, false, true, false, false)
            && !CombatAssistanceAuthority.CanAct(player, true, false, false, true, false)
            && !CombatAssistanceAuthority.CanAct(player, true, false, false, false, true),
            "Linked rotation has independent player, mount/flight, cutscene, PvP and loading eligibility rather than movement ownership.");
        player.IsDead = true;
        Check(!CombatAssistanceAuthority.CanAct(player, true, false, false, false, false), "A dead player cannot start linked rotation.");
        player.IsDead = false;
        player.IsDestroyed = true;
        Check(!CombatAssistanceAuthority.CanAct(player, true, false, false, false, false), "A stale player cannot start linked rotation.");
        player.IsDestroyed = false;
        player.MountId = 1;
        Check(!CombatAssistanceAuthority.CanAct(player, true, false, false, false, false), "A mounted player cannot start linked rotation.");
        player.MountId = 0;
        RsrMode? steeringMode = RsrMode.Off;
        var steeringOwner = new LinkedRotationOwner(() => steeringMode, next => { steeringMode = next; return true; });
        steeringOwner.Update(true, true, true, !CombatAssistanceAuthority.CanAct(player, true, false, false, false, false));
        Check(steeringMode == RsrMode.Henched && steeringOwner.Owned,
            "Explicitly active native AI can start linked RSR without acquiring movement or camera control.");
        steeringOwner.Release();
        Check(steeringMode == RsrMode.Off, "Stopping native AI still releases its linked rotation mode.");
        steeringMode = RsrMode.Off;
        steeringOwner.Update(true, true, true, !CombatAssistanceAuthority.CanAct(player, true, false, false, false, true));
        Check(steeringMode == RsrMode.Off, "Zoning defers linked activation.");
        steeringOwner.Update(true, true, true, !CombatAssistanceAuthority.CanAct(player, true, false, false, false, false));
        Check(steeringMode == RsrMode.Henched, "Linked activation retries once the player becomes available after zoning.");
        foreach (var input in new (bool Forbidden, bool Paused, bool Requested, bool Forced, bool Facing)[]
        {
            (Forbidden: true, Paused: false, Requested: false, Forced: false, Facing: false),
            (false, true, false, false, false), (false, false, true, false, false),
            (false, false, false, true, false), (false, false, false, false, true)
        })
            Check(!CombatAssistanceAuthority.CanSteer(input.Forbidden, input.Paused, input.Requested, input.Forced, input.Facing)
                && CombatAssistanceAuthority.CanAct(player, true, false, false, false, false),
                "Movement opt-outs, movement-only pauses, manual keys and manual facing revoke steering without revoking combat or target authority.");
        Check(CombatAssistanceAuthority.CanSteer(false, false, false, false, false),
            "Automatic steering remains available after manual input ends.");
        var configured = new OverrideFixture(2, true, false, true);
        ModeDataFixture.IpcOverrides = configured;
        ModeDataFixture.TargetingTypeOverride = 7;
        var pause = new RsrModeOptions(typeof(ModeDataFixture));
        ModeDataFixture.IpcOverrides = null;
        ModeDataFixture.TargetingTypeOverride = null;
        pause.RestoreOwnWrite(RsrMode.Off, RsrHostilePolicy.TargetsHaveTarget);
        Check(ReferenceEquals(ModeDataFixture.IpcOverrides, configured) && ModeDataFixture.TargetingTypeOverride == 7,
            "Temporary Off preserves existing Boolean and AutoDuty targeting overrides.");
        var resume = new RsrModeOptions(typeof(ModeDataFixture));
        ModeDataFixture.IpcOverrides = new(1, null, null, null);
        ModeDataFixture.TargetingTypeOverride = null;
        resume.RestoreOwnWrite(RsrMode.AutoDuty, RsrHostilePolicy.TargetsHaveTarget);
        Check(ReferenceEquals(ModeDataFixture.IpcOverrides, configured) && ModeDataFixture.TargetingTypeOverride == 7,
            "Returning to the prior mode restores the exact original override object.");
        var foreign = new OverrideFixture(0, false, false, false);
        ModeDataFixture.IpcOverrides = foreign;
        ModeDataFixture.TargetingTypeOverride = 4;
        resume.RestoreOwnWrite(RsrMode.AutoDuty, RsrHostilePolicy.TargetsHaveTarget);
        Check(ReferenceEquals(ModeDataFixture.IpcOverrides, foreign) && ModeDataFixture.TargetingTypeOverride == 4,
            "A foreign override written during a mode operation is preserved.");
        ModeDataFixture.IpcOverrides = null;
        ModeDataFixture.TargetingTypeOverride = null;
        var noOverride = new RsrModeOptions(typeof(ModeDataFixture));
        ModeDataFixture.IpcOverrides = new(1, null, null, null);
        noOverride.RestoreOwnWrite(RsrMode.Henched, RsrHostilePolicy.TargetsHaveTarget);
        Check(ModeDataFixture.IpcOverrides == null, "A native activation does not accidentally pin an unset targeting preference.");
        RsrMode? mode = RsrMode.Off;
        var writes = 0;
        var owner = new LinkedRotationOwner(() => mode, next => { ++writes; mode = next; return true; });
        owner.Update(true, true, false, false);
        Check(writes == 0 && !owner.Owned, "Imported enabled/link preferences cannot independently start AI or rotation.");
        owner.Update(true, true, true, true);
        Check(writes == 0, "Manual handoff cannot activate linked rotation.");
        owner.Update(true, true, true, false);
        Check(mode == RsrMode.Henched && owner.Owned && writes == 1, "Native activation can start linked RSR in its existing mode contract.");
        owner.Update(true, true, true, false);
        Check(writes == 1, "Repeated active frames do not repeat mode writes.");
        mode = RsrMode.AutoDuty;
        owner.Release();
        Check(mode == RsrMode.AutoDuty && !owner.Owned && writes == 1, "A foreign or user mode change wins over restoration.");
        mode = RsrMode.Off;
        owner.Update(true, true, true, false);
        owner.Update(true, false, true, false);
        Check(mode == RsrMode.Off && !owner.Owned && writes == 3, "Disabling the link restores only its own mode once.");
        mode = null;
        owner.Update(true, true, true, false);
        Check(writes == 3, "Unknown external state never authorizes an activation.");
    }
}
