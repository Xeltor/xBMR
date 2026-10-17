using BossMod;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

static partial class Checks
{
    private sealed class ModulePolicyFixture(WorldState world, Actor actor) : BossModule(world, actor, default, new ArenaBoundsSquare(10))
    {
        internal bool PullPending;
        protected override bool CheckPull() => PullPending;
    }

    private static void ModuleEnablementOwnership()
    {
        long now = 100;
        var live = true;
        var owners = new OwnedModuleEnablement(() => now, 1000);
        var state = owners.Observe(1, true, true)!;
        Check(owners.Apply(1, "a", state.Revision, false, () => live), "per-module runtime disable acquired");
        state = owners.Read(1)!;
        Check(state.SavedEnabled && !state.Enabled && owners.Renew(1, "a", state.Revision, 100), "borrowed module value is separate from saved preference");
        Check(!owners.Apply(1, "b", state.Revision, true, () => true) && !owners.Release(1, "b", state.Revision), "another owner cannot steal/release a module");
        var second = owners.Observe(2, true, true)!;
        Check(owners.Apply(2, "b", second.Revision, false, () => true), "different module owners remain independent");
        owners.UserIntent(1, false);
        Check(!owners.Read(1)!.Enabled && !owners.Read(1)!.SavedEnabled && owners.Read(1)!.LeaseId == null
            && !owners.Release(1, "a", state.Revision) && owners.Read(2)!.LeaseId == "b", "later user choice equal to borrowed value wins only for its module");
        var before = owners.Read(1)!.Revision;
        owners.UserIntent(1, true); owners.UserIntent(1, false);
        Check(owners.Read(1)!.Revision > before, "explicit changes away then back invalidate an old acquisition token");
        state = owners.Read(1)!;
        Check(owners.Apply(1, "a", state.Revision, true, () => live), "temporary enable over a disabled configured preference is possible in the backend");
        state = owners.Read(1)!;
        Check(owners.Renew(1, "a", state.Revision, 50), "module backend expiry is independently armed");
        now += 51;
        Check(!owners.Read(1)!.Enabled && owners.Read(1)!.LeaseId == null && !owners.Renew(1, "a", state.Revision, 50),
            "native module read observes expiry and cannot revive an expired override");
        state = owners.Read(1)!;
        owners.Apply(1, "a", state.Revision, true, () => live); live = false;
        Check(!owners.Read(1)!.Enabled, "owner loss restores native preference without an external cleanup callback");
        live = true; state = owners.Read(1)!;
        owners.Apply(1, "a", state.Revision, true, () => throw new InvalidOperationException("probe failed"));
        Check(owners.Read(1)!.LeaseId == null && !owners.Read(1)!.Enabled, "throwing owner probe cannot escape through native eligibility");
        state = owners.Observe(3, true, true)!;
        owners.Apply(3, "a", state.Revision, false, () => true);
        Check(owners.Observe(3, false, false) is { LeaseId: null, SavedEnabled: false, Enabled: false }, "observed raw configuration edit invalidates the borrowed layer");
        var bounded = new OwnedModuleEnablement(() => now, 1000);
        for (uint i = 0; i < 128; ++i)
        {
            var watch = bounded.Observe(i, false, true)!;
            Check(watch != null && bounded.Pin(i), "native retained provenance pins a bounded watch");
        }
        Check(bounded.Observe(129, true, true) == null && bounded.WatchedOids().Length == 128,
            "watch limits cannot evict native active-retention provenance");
        bounded.Unpin(0);
        Check(bounded.Observe(129, true, true) != null && bounded.Read(0) == null, "only unpinned unowned observations may be evicted");
        var many = new OwnedModuleEnablement(() => now, 1000);
        for (uint i = 0; i < 32; ++i)
        {
            var watch = many.Observe(i, true, true)!;
            Check(many.Apply(i, "lease-" + i, watch.Revision, false, () => true), "module layer count accepts its advertised bound");
        }
        var overflow = many.Observe(33, true, true)!;
        Check(!many.Apply(33, "overflow", overflow.Revision, false, () => true), "module layers reject beyond 32 owners");

        NativeModuleEnablement();
        ModuleControlInput();
    }

    private static void NativeModuleEnablement()
    {
        const uint oid = 0xFEED01;
        var config = new BossModuleConfig();
        var configType = typeof(BossModuleConfig);
        object Observe(uint id) => configType.GetMethod("ObserveModuleEnablement", InternalInstance)!.Invoke(config, [id])!;
        var resourceType = configType.Assembly.GetType("BossMod.Integration.IntegrationModuleEnablement", true)!;
        var live = true;
        object Resource(uint id, BossModuleConfig? target = null) => resourceType.GetConstructors(InternalInstance | BindingFlags.Public).Single()
            .Invoke([target ?? config, id, (Func<bool>)(() => live)]);
        object State(object resource) => resourceType.GetMethod("Read")!.Invoke(resource, null)!;
        object Apply(object resource, string value) => resourceType.GetMethod("Apply")!.Invoke(resource, ["fixture", State(resource), value])!;
        bool Release(object resource, long revision) => (bool)resourceType.GetMethod("Release")!.Invoke(resource, ["fixture", revision])!;
        Observe(oid); Observe(oid + 1);
        var resource = Resource(oid);
        var applied = Apply(resource, "false");
        Check((bool)Property(applied, "Accepted") && !config.IsModuleEnabled(oid) && config.DisabledModuleOIDs.Length == 0,
            "actual native module query consumes the runtime layer without editing the saved list");
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) config.Serialize(writer, new());
        var savedJson = Encoding.UTF8.GetString(stream.ToArray());
        using var persisted = JsonDocument.Parse(savedJson);
        Check(persisted.RootElement.GetProperty(nameof(BossModuleConfig.DisabledModuleOIDs)).GetArrayLength() == 0
            && !savedJson.Contains("ModuleEnablementOwners") && !savedJson.Contains("fixture"), "generated native serialization cannot persist a module lease");
        var revision = (long)Property(applied, "Revision");
        config.SetModuleEnabled(oid + 1, false);
        Check(!config.IsModuleEnabled(oid) && !config.IsModuleEnabled(oid + 1) && (string?)Property(State(resource), "LeaseId") == "fixture",
            "an unrelated module save cannot steal or persist a borrowed disable");
        config.SetModuleEnabled(oid, false);
        Check(!Release(resource, revision) && !config.IsModuleEnabled(oid) && config.DisabledModuleOIDs.Contains(oid),
            "actual user choice equal to borrowed value is saved and cleanup cannot overwrite it");
        config.SetModuleEnabled(oid, true); Observe(oid);
        applied = Apply(resource, "false");
        config.SetModulesEnabled([oid], true);
        Check(config.IsModuleEnabled(oid) && (string?)Property(State(resource), "LeaseId") == null,
            "explicit equal-to-saved bulk intent revokes the borrowed module setting");
        applied = Apply(resource, "false");
        using (var unrelated = JsonDocument.Parse("{\"RadarResize\":true}")) config.Deserialize(unrelated.RootElement, new());
        Check(!config.IsModuleEnabled(oid) && (string?)Property(State(resource), "LeaseId") == "fixture", "unrelated partial configuration import preserves independent module ownership");
        using (var partial = JsonDocument.Parse("{\"DisabledModuleOIDs\":[],\"RadarResize\":\"invalid\"}"))
        {
            try { config.Deserialize(partial.RootElement, new()); }
            catch (AggregateException) { }
        }
        Check(config.IsModuleEnabled(oid) && (string?)Property(State(resource), "LeaseId") == null,
            "explicit module-list import revokes ownership even if another imported field fails after partial application");
        applied = Apply(resource, "false");
        config.Deserialize(JsonDocument.Parse("{\"DisabledModuleOIDs\":[]}").RootElement, new());
        Check(config.IsModuleEnabled(oid) && (string?)Property(State(resource), "LeaseId") == null,
            "configuration import revokes owned enablement even if saved values are equal");
        applied = Apply(resource, "false"); live = false;
        Check(config.IsModuleEnabled(oid), "native eligibility recovers on owner loss without a generic lease-store sweep");
        live = true;

        // Exercise native manager eligibility and pending transitions with only the state it
        // needs. There are no game assets, controller or plugin update calls in this fixture.
        var manager = (BossModuleManager)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleManager));
        var nativeConfig = BossModuleManager.Config;
        var oldDisabled = nativeConfig.DisabledModuleOIDs.ToArray();
        var oldMaturity = nativeConfig.MinMaturity;
        var module = (ModulePolicyFixture)RuntimeHelpers.GetUninitializedObject(typeof(ModulePolicyFixture));
        var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        typeof(Actor).GetField(nameof(Actor.OID))!.SetValue(actor, oid);
        var info = (BossModuleRegistry.Info)RuntimeHelpers.GetUninitializedObject(typeof(BossModuleRegistry.Info));
        info.PrimaryActorOID = oid; info.ModuleType = typeof(ModulePolicyFixture); info.Maturity = BossModuleInfo.Maturity.Contributed;
        typeof(BossModule).GetField(nameof(BossModule.Info))!.SetValue(module, info);
        typeof(BossModule).GetField(nameof(BossModule.PrimaryActor))!.SetValue(module, actor);
        typeof(BossModule).GetField(nameof(BossModule.StateMachine))!.SetValue(module, new StateMachine([new(new(), "fixture")]));
        var managerType = typeof(BossModuleManager);
        managerType.GetField("_leasedLoadOrigins", InternalInstance)!.SetValue(manager, new Dictionary<BossModule, long>());
        managerType.GetField(nameof(BossModuleManager.LoadedModules))!.SetValue(manager, new List<BossModule> { module });
        managerType.GetField(nameof(BossModuleManager.PendingModules))!.SetValue(manager, new List<BossModule>());
        var eligible = managerType.GetMethod("ModuleEligible", InternalInstance)!;
        var reconcile = managerType.GetMethod("ReconcileModuleEligibility", InternalInstance)!;
        bool CanDisable() => (bool)typeof(BossModule).GetMethod("CanTemporarilyDisable", InternalInstance)!.Invoke(module, null)!;
        try
        {
            nativeConfig.MinMaturity = BossModuleInfo.Maturity.Contributed;
            nativeConfig.SetModuleEnabled(oid, false);
            configType.GetMethod("ObserveModuleEnablement", InternalInstance)!.Invoke(nativeConfig, [oid]);
            var enableResource = Resource(oid, nativeConfig);
            applied = Apply(enableResource, "true");
            Check((bool)Property(applied, "Accepted") && (bool)eligible.Invoke(manager, [module])!, "native manager accepts a temporary enable within its maturity gate");
            managerType.GetMethod("RememberLeasedLoad", InternalInstance)!.Invoke(manager, [module]);
            Check(CanDisable(), "inactive native module with no pending pull can be temporarily disabled");
            module.PullPending = true;
            Check(!CanDisable(), "native custom pull predicate prevents removing a module about to activate");
            module.PullPending = false;
            actor.InCombat = true;
            Check(!CanDisable(), "a combat actor cannot be disabled before its custom pull predicate catches up");
            actor.InCombat = false;
            module.StateMachine.ActivePhaseIndex = 0;
            Check(!CanDisable(), "active native phase cannot be withdrawn by a new disable request");
            Check(Release(enableResource, (long)Property(applied, "Revision")) && !nativeConfig.IsModuleEnabled(oid)
                && (bool)eligible.Invoke(manager, [module])!, "lease release restores configuration but preserves active native coverage");
            reconcile.Invoke(manager, null);
            Check(manager.LoadedModules.Contains(module) && manager.PendingModules.Count == 0, "native reconciliation retains the active temporarily enabled instance");
            for (uint i = 1; i <= 256; ++i) configType.GetMethod("ObserveModuleEnablement", InternalInstance)!.Invoke(nativeConfig, [oid + i]);
            Check((bool)eligible.Invoke(manager, [module])!, "catalog paging cannot evict provenance and remove active native coverage");
            nativeConfig.SetModuleEnabled(oid, false);
            Check(!(bool)eligible.Invoke(manager, [module])!, "later explicit same-value user disable defeats active retention");
            reconcile.Invoke(manager, null);
            Check(!manager.LoadedModules.Contains(module) && manager.PendingModules.Contains(module), "user intent is applied through the normal native pending transition");
            var origins = (Dictionary<BossModule, long>)managerType.GetField("_leasedLoadOrigins", InternalInstance)!.GetValue(manager)!;
            Check(origins.Count == 0, "native pending transition releases its retention pin");
            configType.GetMethod("ObserveModuleEnablement", InternalInstance)!.Invoke(nativeConfig, [oid]);
            applied = Apply(enableResource, "true");
            manager.PendingModules.Remove(module); manager.LoadedModules.Add(module);
            managerType.GetMethod("RememberLeasedLoad", InternalInstance)!.Invoke(manager, [module]);
            live = false;
            Check(!nativeConfig.IsModuleEnabled(oid) && (bool)eligible.Invoke(manager, [module])!, "owner failure also restores preference without removing active native coverage");
            nativeConfig.MinMaturity = BossModuleInfo.Maturity.Verified;
            Check(!(bool)eligible.Invoke(manager, [module])!, "a user maturity gate cannot be bypassed by active lease retention");
            nativeConfig.MinMaturity = BossModuleInfo.Maturity.Contributed;
            module.StateMachine.ActivePhaseIndex = 1;
            Check(!(bool)eligible.Invoke(manager, [module])!, "ended phases cannot retain an expired temporary enable");
            reconcile.Invoke(manager, null);
            Check(origins.Count == 0 && manager.PendingModules.Contains(module), "natural phase end removes native retention and releases its pin");
        }
        finally
        {
            nativeConfig.DisabledModuleOIDs = oldDisabled; nativeConfig.MinMaturity = oldMaturity;
            configType.GetMethod("RefreshModuleEnablement", InternalInstance)!.Invoke(nativeConfig, null);
        }
    }

    private static void ModuleControlInput()
    {
        var assembly = typeof(BossModuleConfig).Assembly;
        var sessionType = assembly.GetType("BossMod.Integration.IntegrationSession", true)!;
        using var session = (IDisposable)Activator.CreateInstance(sessionType, InternalInstance, null,
            [(Func<long>)(() => 100L), 1000L], null)!;
        sessionType.GetMethod("BeginFrame", InternalInstance)!.Invoke(session, ["fixture", true]);
        var context = sessionType.GetProperty("CurrentContext", InternalInstance)!.GetValue(session)!;
        var coordinatorType = assembly.GetType("BossMod.Integration.IntegrationCoordinator", true)!;
        var coordinator = RuntimeHelpers.GetUninitializedObject(coordinatorType);
        coordinatorType.GetField("Session", InternalInstance)!.SetValue(coordinator, session);
        var apply = coordinatorType.GetMethod("ApplyModuleEnablement", InternalInstance)!;
        // With a genuinely fresh matching native context, omitted booleans must reject before
        // touching game state. This object intentionally has no player/managers/controllers.
        var missingEnabled = JsonSerializer.Serialize(new { context, ownerToken = "fixture", identity = "1:fixture", expectedRevision = 1,
            expectedEnabled = true, leaseMs = 1000 }, BossMod.Integration.IntegrationSession.Json);
        var missingExpected = JsonSerializer.Serialize(new { context, ownerToken = "fixture", identity = "1:fixture", expectedRevision = 1,
            enabled = false, leaseMs = 1000 }, BossMod.Integration.IntegrationSession.Json);
        foreach (var json in new[] { missingEnabled, missingExpected })
        {
            using var response = JsonDocument.Parse((string)apply.Invoke(coordinator, [json])!);
            Check(!response.RootElement.GetProperty("available").GetBoolean(), "omitted module booleans cannot silently request a disable or imply a false comparison value");
        }
    }
}
