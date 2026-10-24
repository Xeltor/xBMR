using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Runtime;
using Dalamud.Plugin;
using System.IO;
using System.Threading;

public static partial class Checks
{
    private static void LegacyRetirement()
    {
        LegacyXcaiInstaller.VerifyContract(typeof(IDalamudPluginInterface).Assembly);
        var now = DateTime.UtcNow;
        var operations = new RetirementOperations();
        var owner = new LegacyXcaiRetirement(operations);
        owner.Update(false, true, now);
        Check(owner.BlocksAssistance && operations.Events.Count == 0, "Retirement waits for login and gates settings import.");
        owner.Update(true, false, now.AddSeconds(1));
        Check(operations.Events.Count == 0, "Combat postpones removal without asking the user.");
        owner.Update(true, true, now.AddSeconds(2));
        Check(operations.Events.SequenceEqual(["unload"]) && owner.BlocksAssistance,
            "Safe mandatory unload begins without a notice, acknowledgement or choice.");
        owner.Update(true, true, now.AddSeconds(3));
        Check(operations.Events.Count == 1, "A running unload is never duplicated or waited on by the frame.");
        operations.Completion.SetResult();
        owner.Update(true, false, now.AddSeconds(4));
        Check(operations.Events.Count == 1 && owner.BlocksAssistance, "Combat starting during unload postpones installer removal.");
        owner.Update(true, true, now.AddSeconds(5));
        Check(operations.Events.SequenceEqual(["unload", "remove"]) && !owner.BlocksAssistance
            && operations.Observations.Last() == false,
            "Completed removal releases settings import and publishes completion for the modal.");
        owner.DisposeAsync().AsTask().GetAwaiter().GetResult();

        operations = new() { AlreadyUnloaded = true };
        owner = new(operations);
        owner.Update(true, true, now);
        Check(!owner.BlocksAssistance && operations.Events.SequenceEqual(["unload", "remove"]),
            "Disabled installations are removed immediately when safe, without loading or user acknowledgement.");
        owner.DisposeAsync().AsTask().GetAwaiter().GetResult();

        operations = new();
        owner = new(operations);
        owner.Update(true, true, now);
        operations.Identity = new object();
        operations.Completion.SetResult();
        owner.Update(true, true, now.AddSeconds(1));
        Check(operations.Events.SequenceEqual(["unload", "unload", "remove"]) && ReferenceEquals(operations.Removed, operations.Identity),
            "An identity replacement is independently unloaded before removal rather than trusting stale completion.");
        owner.DisposeAsync().AsTask().GetAwaiter().GetResult();

        operations = new();
        owner = new(operations);
        owner.Update(true, true, now);
        operations.Completion.SetException(new InvalidOperationException("unload failed"));
        owner.Update(true, true, now.AddSeconds(1));
        owner.Update(true, true, now.AddSeconds(2));
        Check(owner.BlocksAssistance && operations.Events.SequenceEqual(["unload", "failure"])
            && operations.Observations.All(value => value),
            "Unload failure blocks import and never publishes successful removal; retries back off.");
        owner.DisposeAsync().AsTask().GetAwaiter().GetResult();

        operations = new();
        owner = new(operations);
        owner.Update(true, true, now);
        var shutdown = owner.DisposeAsync().AsTask();
        Check(!shutdown.IsCompleted, "Owner shutdown awaits a started legacy unload asynchronously.");
        operations.Completion.SetResult();
        shutdown.GetAwaiter().GetResult();
        owner.Update(true, true, now.AddSeconds(1));
        Check(operations.Removed == null, "Retirement performs no removal after its owner has stopped.");
        LegacyRetirementNotice();
    }

    private static void LegacyRetirementNotice()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xbmr-retirement-" + Guid.NewGuid().ToString("N"));
        var failures = new List<Exception>();
        void Ready(LegacyXcaiNotice notice, Func<bool> condition)
        {
            Check(SpinWait.SpinUntil(() => { notice.Poll(); return condition(); }, 5000), "Notice IO finishes within the bounded test wait.");
        }
        try
        {
            var notice = new LegacyXcaiNotice(directory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(false);
            notice.Acknowledge();
            Check(!notice.ShouldShow, "Users without XCAI do not receive a migration popup.");
            notice.ObserveInstallations(true);
            Ready(notice, () => notice.State == LegacyXcaiNoticeState.RemovalPending);
            Check(!notice.ShouldShow, "Detection and pending removal cannot show a completed-removal popup.");
            notice.Acknowledge();
            Check(notice.State == LegacyXcaiNoticeState.RemovalPending, "Acknowledgement cannot authorize or shortcut removal.");
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();

            notice = new(directory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(false);
            Ready(notice, () => notice.ShouldShow);
            Check(notice.State == LegacyXcaiNoticeState.Removed, "A persisted pending migration becomes a completion popup only after installer absence is verified.");
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();

            notice = new(directory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(false);
            Ready(notice, () => notice.ShouldShow);
            Check(notice.ShouldShow, "An unseen or unacknowledged completion popup survives restart and has no timeout.");
            notice.Acknowledge();
            Check(!notice.ShouldShow && notice.State == LegacyXcaiNoticeState.Acknowledged, "Got it dismisses only the completed-migration notice.");
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();

            notice = new(directory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(true);
            Ready(notice, () => notice.State == LegacyXcaiNoticeState.Acknowledged);
            notice.ObserveInstallations(false);
            Check(!notice.ShouldShow, "Acknowledgement survives startup and subsequent detection/removal without another popup.");
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Check(failures.Count == 0, "Notice receipts persist without IO failures.");

            var fastDirectory = Path.Combine(directory, "fast-removal");
            notice = new(fastDirectory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(true);
            notice.ObserveInstallations(false);
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();
            notice = new(fastDirectory, error => { lock (failures) failures.Add(error); });
            notice.ObserveInstallations(false);
            Ready(notice, () => notice.ShouldShow);
            Check(notice.ShouldShow, "Fast removal followed by immediate unload still preserves the completion popup across startup.");
            notice.Acknowledge();
            notice.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class RetirementOperations : ILegacyXcaiRetirementOperations
    {
        internal object Identity = new();
        internal object? Removed;
        internal bool AlreadyUnloaded;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<string> Events = [];
        internal readonly List<bool> Observations = [];
        public object[] FindInstallations() => ReferenceEquals(Removed, Identity) ? [] : [Identity];
        public void ObserveInstallations(bool installed) => Observations.Add(installed);
        public Task Unload(object installation)
        {
            if (!ReferenceEquals(installation, Identity)) throw new InvalidOperationException("stale identity");
            Events.Add("unload");
            return AlreadyUnloaded ? Task.CompletedTask : Completion.Task;
        }
        public void RemoveUnloaded(object installation)
        {
            if (!ReferenceEquals(installation, Identity) || !(AlreadyUnloaded || Completion.Task.IsCompletedSuccessfully))
                throw new InvalidOperationException("unload must complete for this identity");
            Events.Add("remove");
            Removed = installation;
        }
        public void ReportFailure(Exception error) => Events.Add("failure");
    }
}
