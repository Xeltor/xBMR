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
            var native = new BossMod.AI.AIConfig();
            using (var oldConfig = System.Text.Json.JsonDocument.Parse("""
                {"FollowSlot":3,"AIAutorotPresetName":"old","ForbidActions":true,"ManualTarget":true,
                 "FocusTargetMaster":true,"UseCombatPositioningRange":false,"MaxDistanceToTarget":20,
                 "MinDistance":10,"MoveDelay":5,"DisableObstacleMaps":true,"AutoAFK":true,
                 "ForbidMovement":true,"ForbidAIMovementMounted":true}
                """))
                native.Deserialize(oldConfig.RootElement, new System.Text.Json.JsonSerializerOptions());
            Check(native.FollowSlot == BossMod.PartyState.PlayerSlot && native.AIAutorotPresetName == "old"
                && native.ForbidActions && native.ManualTarget && native.FocusTargetMaster
                && native.ForbidMovement && native.ForbidAIMovementMounted && native.MoveDelay == 5,
                "Loading alone does not activate AI or change hidden control settings.");
            native.FollowDuringCombat = false;
            native.FollowDuringActiveBossModule = false;
            native.FollowTarget = false;
            native.EchoToChat = false;
            native.FollowOutOfCombat = true;
            Check(BossMod.AI.AIActivationSettings.Normalize(native)
                && native.UseCombatPositioningRange && native.MinDistance == 0
                && native.MoveDelay == 0 && !native.DisableObstacleMaps && !native.ForbidMovement
                && !native.ForbidActions && !native.ManualTarget && native.AIAutorotPresetName == null
                && native.FollowDuringCombat && native.FollowDuringActiveBossModule && native.FollowTarget,
                "Enabling AI automatically corrects hidden controls that prevent managed operation.");
            Check(!native.EchoToChat && native.FollowOutOfCombat && native.FocusTargetMaster
                && native.AutoAFK && native.ForbidAIMovementMounted && native.MaxDistanceToTarget == 20
                && !BossMod.AI.AIActivationSettings.Normalize(native)
                && !BossMod.AI.AIActivationSettings.Normalize(new()),
                "Correction preserves unrelated preferences and is idempotent.");
            var managedFallbacks = new BossMod.AI.AIConfig
            {
                MaxDistanceToTarget = 3, MaxDistanceToSlot = 0, PreferredDistance = 1,
                FocusTargetMaster = true, ForbidAIMovementMounted = true, AutoAFK = true
            };
            Check(!BossMod.AI.AIActivationSettings.Normalize(managedFallbacks),
                "Runtime-managed/fallback distances and situational opt-outs need no correction.");
            managedFallbacks.ForbidMovement = true;
            Check(BossMod.AI.AIActivationSettings.Normalize(managedFallbacks)
                && !managedFallbacks.ForbidMovement && managedFallbacks.MaxDistanceToTarget == 3
                && managedFallbacks.MaxDistanceToSlot == 0 && managedFallbacks.PreferredDistance == 1
                && managedFallbacks.AutoAFK && managedFallbacks.ForbidAIMovementMounted,
                "Correcting a movement block preserves managed fallback values and situational opt-outs.");
            var replayConfig = new BossMod.ReplayManagementConfig();
            Check(!replayConfig.ShareFightRecordings && replayConfig.ImportFightSharingPreference(true)
                && replayConfig.ShareFightRecordings && replayConfig.FightSharingPreferenceMigrated,
                "Replay sharing imports the existing opt-in once without enabling it by default.");
            replayConfig.ShareFightRecordings = false;
            Check(!replayConfig.ImportFightSharingPreference(true) && !replayConfig.ShareFightRecordings,
                "A new replay-sharing opt-out cannot be overwritten by the old AI preference.");
            var explicitReplayChoice = new BossMod.ReplayManagementConfig();
            explicitReplayChoice.ShareFightRecordings = true;
            explicitReplayChoice.Modified.Fire();
            explicitReplayChoice.ShareFightRecordings = false;
            explicitReplayChoice.Modified.Fire();
            Check(!explicitReplayChoice.ImportFightSharingPreference(true) && !explicitReplayChoice.ShareFightRecordings,
                "A direct replay setting choice wins over a pending legacy import.");
            var replayOptOut = new BossMod.ReplayManagementConfig();
            Check(replayOptOut.ImportFightSharingPreference(false) && !replayOptOut.ShareFightRecordings,
                "Moving sharing preserves an existing opt-out.");
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
                Check(SpinWait.SpinUntil(() => { session.Poll(); return session.MayEdit; }, 5000), "Reconciliation is durable before assistance becomes available.");
                Check(!session.MayAssist, "Recovery and saved Enabled values do not activate AI.");
                session.Active = true;
                Check(session.MayAssist, "Explicit native AI activation enables assistance after recovery.");
                session.Active = false;
                Check(!session.MayAssist, "Stopping native AI revokes assistance without waiting for persistence.");
                session.Active = true;
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
