using BossMod.CombatAssistance.Config;
using System.Threading;

public static partial class Checks
{
    private static void SettingsSession()
    {
        var directory = Path.Combine(Path.GetTempPath(), "assistance-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var legacy = Path.Combine(directory, "XelsCombatAI.json");
            var store = new AssistanceSettingsStore(Path.Combine(directory, "integrated"), legacy);
            File.WriteAllText(legacy, """
                {"Version":30,"Enabled":true,"BmrHallwayModulesToRestore":[16858],
                 "BmrHallwayRestoreUserRevisions":{"16858":7},"BmrHallwayRestoreGeneration":"old-provider"}
                """);
            var session = new AssistanceSettingsSession(store);
            try
            {
                session.ObserveStandalone(true);
                Check(!session.MayAssist && !session.MayEdit, "Loaded standalone blocks assistance immediately, before any async result.");
                session.ObserveStandalone(false);
                Check(SpinWait.SpinUntil(() => { session.Poll(); return session.Current.State == AssistanceSettingsState.RecoveryPending; }, 5000),
                    "Standalone unload transfers the legacy journal without enabling assistance early.");
                session.ReconcileRecovery(new Dictionary<uint, bool>());
                Check(!session.MayAssist, "An incomplete native settings observation cannot retire a recovery journal.");
                session.ReconcileRecovery(new Dictionary<uint, bool> { [16858] = false });
                Check(SpinWait.SpinUntil(() => { session.Poll(); return session.MayAssist; }, 5000), "Reconciliation is durable before assistance becomes available.");
                var audit = session.Current.Document!.RecoveryReconciliation.Single();
                Check(audit is { ModuleOid: 16858, SavedEnabled: false, OldGeneration: "old-provider", OldUserRevision: 7 },
                    "Unknown historical ownership preserves the current native choice with an explicit reconciliation audit.");
                Check(session.Preferences.BmrHallwayModulesToRestore.Count == 0 && File.Exists(legacy),
                    "Reconciled journals retain the original rollback configuration.");
                session.Preferences.Enabled = false;
                session.Save();
                session.Preferences.GapCloserRDM = false;
                session.Save();
                session.ObserveStandalone(true);
                Check(!session.MayAssist && !session.MayEdit, "Reloading standalone immediately revokes native assistance and UI writes.");
                session.ObserveStandalone(false);
                Check(SpinWait.SpinUntil(() => { session.Poll(); return session.MayEdit; }, 5000), "Ordered saves complete before a new load.");
                Check(!session.Preferences.Enabled && !session.Preferences.GapCloserRDM && session.Current.Document!.RecoveryReconciliation.Count == 1,
                    "Repeated load preserves the last UI changes and never repeats reconciliation.");
            }
            finally { session.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            Check(!session.MayAssist && !session.MayEdit, "Disposal prevents assistance and later writes.");
            Check(store.Load(false).Document!.RecoveryReconciliation.Count == 1, "Reconciliation audit survives restart.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
