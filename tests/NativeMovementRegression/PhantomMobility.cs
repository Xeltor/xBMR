using BossMod;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void PhantomMobility()
    {
        var rotation = new object();
        var duty = new PhantomDuty();
        foreach (var id in NativePhantomMobility.ActionIds)
            Check(ReferenceEquals(RsrNativeMobilitySelection.SelectionOwner(id, rotation, duty), duty)
                && RsrNativeMobilitySelection.SelectionOwner(id, rotation, null) == null,
                "Phantom movement belongs to the current duty rotation and cannot fall back to the job rotation.");
        Check(ReferenceEquals(RsrNativeMobilitySelection.SelectionOwner(37008, rotation, duty), rotation),
            "Ordinary job movement keeps its own RSR action owner.");
        var actions = RsrNativeMobilitySelection.DutyActions(duty, typeof(PhantomActionId), typeof(IPhantomAction)).ToArray();
        Check(actions.Length == 5 && actions.Contains(duty.OccultSprintPvE) && actions.Contains(duty.PhantomKickPvE),
            "Phantom movement settings are discovered independently of regular job actions and duty-slot lists.");
        Check(RsrNativeMobilitySelection.DutyActions(new PartialPhantomDuty(), typeof(PhantomActionId), typeof(IPhantomAction)).Count() == 1,
            "A broken or missing Phantom getter cannot discard the remaining configured actions.");
        var original = duty.OccultSprintPvE;
        using (var lease = RsrOriginalActionSelectionLease.Acquire(duty, nameof(PhantomDuty.OccultSprintPvE), (_, _, _) => false))
        {
            lease.Resume();
            Check(lease.Owned && !duty.OccultSprintPvE.CanUse(out _), "Duty movement uses the same sole-owner selector lease.");
            Check(ReferenceEquals(RsrNativeMobilitySelection.DutyActions(duty, typeof(PhantomActionId), typeof(IPhantomAction)).Last(), duty.OccultSprintPvE)
                && ReferenceEquals(duty.OccultSprintPvE.Config, original.Config) && !duty.OccultSprintPvE.Config.SavedEnabled,
                "A duty lease forwards the original saved opt-out instead of a force-enabled action value.");
        }
        Check(ReferenceEquals(duty.OccultSprintPvE, original) && duty.OccultSprintPvE.CanUse(out _),
            "Releasing duty movement restores the original action selector.");

        var player = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.Statuses = new ActorStatus[60];
        var now = DateTime.UtcNow;
        foreach (var id in NativePhantomMobility.ActionIds)
        {
            player.Statuses[0] = new(NativePhantomMobility.RequiredStatus(id), 0, now.AddSeconds(5), 1);
            Check(NativePhantomMobility.Available(id, 1346, player, now)
                && NativePhantomMobility.Available(id, 1252, player, now) == (id != 49078)
                && !NativePhantomMobility.Available(id, 1, player, now)
                && !NativePhantomMobility.Available(id, 1346, player, now.AddSeconds(5)),
                "Phantom movement requires the correct active job, territory and unexpired availability window.");
            player.Statuses[0] = new(4362, 0, now.AddSeconds(5), 1);
            Check(!NativePhantomMobility.Available(id, 1346, player, now), "Another Phantom job cannot grant movement eligibility.");
        }
        var settings = new AssistancePreferences { UseGapCloser = true, UsePhantomGapClosers = true, GapCloserWHM = false };
        Check(!NativeMobilityCapture.EnabledBySettings(new(41595, MobilityMechanic.FixedTowardTarget, 15, Phantom: true), settings, Class.WHM)
            && !NativeMobilityCapture.SprintEnabled(41646, settings, Class.WHM), "Phantom movement retains the regular job's saved opt-out.");
        settings.GapCloserWHM = true;
        settings.UsePhantomGapClosers = false;
        Check(!NativeMobilityCapture.EnabledBySettings(new(41600, MobilityMechanic.Directional, 15, Phantom: true), settings, Class.WHM),
            "The Phantom feature opt-out remains authoritative after duty discovery.");
        Check(NativeMobilityCapture.SprintForecast(41646, new(6)) == (12, 10)
            && NativeMobilityCapture.SprintForecast(41646, new(0, 6.6f, 30)) == null
            && NativeMobilityCapture.SprintForecast(41646, new(3)) == null,
            "Occult Sprint retains its finite boost and never invents an unknown or slowed baseline.");
    }

    private enum PhantomActionId : uint { PhantomKickPvE = 41595, OccultFeatherfootPvE = 41600, StepForthPvE = 49078, SteadfastStancePvE = 46604, OccultSprintPvE = 41646 }
    public sealed class PhantomConfig { private bool _isEnable = false; public bool SavedEnabled => _isEnable; }
    public interface IPhantomAction { PhantomConfig Config { get; } bool CanUse(out IPhantomAction? selected); }
    private sealed class PhantomAction : IPhantomAction
    {
        public PhantomConfig Config { get; } = new();
        public bool CanUse(out IPhantomAction? selected) { selected = this; return true; }
    }
    private sealed class PartialPhantomDuty
    {
        public IPhantomAction PhantomKickPvE => throw new InvalidOperationException("changed getter");
        public IPhantomAction OccultSprintPvE { get; } = new PhantomAction();
    }
    private sealed class PhantomDuty
    {
        private readonly Lazy<IPhantomAction> _OccultSprintPvECreator = new(() => new PhantomAction());
        public IPhantomAction OccultSprintPvE => _OccultSprintPvECreator.Value;
        public IPhantomAction PhantomKickPvE { get; } = new PhantomAction();
        public IPhantomAction OccultFeatherfootPvE { get; } = new PhantomAction();
        public IPhantomAction StepForthPvE { get; } = new PhantomAction();
        public IPhantomAction SteadfastStancePvE { get; } = new PhantomAction();
    }
}
