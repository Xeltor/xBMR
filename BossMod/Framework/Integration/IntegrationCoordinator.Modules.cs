namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private BossModuleRegistry.Info[]? _moduleCatalog;
    internal sealed record ModuleQuery(IntegrationSession.Context Context, int Offset = 0, int Count = 64);
    internal sealed record ModuleControlQuery(IntegrationSession.Context Context, string Identity);
    internal sealed record ModuleControlRequest(IntegrationSession.Context Context, string OwnerToken, string Identity,
        long ExpectedRevision, bool? ExpectedEnabled, bool? Enabled, int LeaseMs, string? LeaseId = null, string? Reason = null);

    private BossModuleRegistry.Info? ResolveModuleIdentity(string? identity)
    {
        if (identity == null || identity.Length > 512) return null;
        var separator = identity.IndexOf(':');
        if (separator < 1 || !uint.TryParse(identity.AsSpan(0, separator), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var oid)) return null;
        var info = BossModuleRegistry.FindByOID(oid);
        return info != null && info.Maturity != BossModuleInfo.Maturity.Dummy
            && identity == $"{info.PrimaryActorOID:X}:{info.ModuleType.FullName}" ? info : null;
    }

    private bool CanDisableModule(BossModuleRegistry.Info info)
    {
        if (World.Party.Player() is not { InCombat: false } || bossmod.LoadedModules.Count + bossmod.PendingModules.Count > 1024)
            return false;
        return bossmod.LoadedModules.Concat(bossmod.PendingModules).Where(m => m.PrimaryActor.OID == info.PrimaryActorOID)
            .All(m => m.GetType() == info.ModuleType && m.CanTemporarilyDisable());
    }

    private bool ModuleControlLive(string owner, string scope)
    {
        var observation = System.Threading.Volatile.Read(ref _controlObservation);
        return Session.IsOwnerActive(owner) && observation is { Allowed: true } && observation.Scope == scope
            && (System.Diagnostics.Stopwatch.GetTimestamp() - observation.Timestamp) / (double)System.Diagnostics.Stopwatch.Frequency < 0.25;
    }

    internal string GetModuleEnablement(string json)
    {
        var request = IntegrationSession.Parse<ModuleControlQuery>(json);
        if (request == null || !Session.Matches(request.Context)) return Unavailable("invalid-module-control-context");
        var info = ResolveModuleIdentity(request.Identity);
        if (info == null) return Unavailable("unknown-module-identity");
        try
        {
            var config = BossModuleManager.Config;
            var state = config.ObserveModuleEnablement(info.PrimaryActorOID);
            return state == null ? Unavailable("module-watch-limit") : IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.Identity, state.SavedEnabled, state.Enabled, state.Revision,
                state.UserRevision, leased = state.LeaseId != null, maxLeaseMs = 2000,
                meetsMinimumMaturity = info.Maturity >= config.MinMaturity,
                canTemporarilyDisable = CanDisableModule(info),
                lifetime = "runtime layer only; active temporarily-enabled native instances retain coverage until natural reset/end; explicit user changes win"
            });
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Module control observation failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("module-control-observation-unavailable");
        }
    }

    internal string ApplyModuleEnablement(string json)
    {
        var request = IntegrationSession.Parse<ModuleControlRequest>(json);
        if (request == null || request.ExpectedEnabled == null || request.Enabled == null || !Session.Matches(request.Context)
            || request.Context.Stage != "hints-ready" || !Session.CanContribute || !ControlsAllowed()
            || !Session.IsOwnerActive(request.OwnerToken) || _leases == null)
            return Unavailable("invalid-module-control-owner-or-context");
        var info = ResolveModuleIdentity(request.Identity);
        var config = BossModuleManager.Config;
        if (info == null || info.Maturity < config.MinMaturity) return Unavailable("unknown-module-or-maturity-gate");
        if (request.Enabled.Value && !config.IsModuleConfiguredEnabled(info.PrimaryActorOID) && request.Reason != "user-requested-temporary-enable")
            return Unavailable("saved-module-opt-out-requires-explicit-enable-intent");
        try
        {
            if (!request.Enabled.Value && !CanDisableModule(info)) return Unavailable("module-active-pull-pending-or-player-in-combat");
            var state = config.ObserveModuleEnablement(info.PrimaryActorOID);
            if (state == null) return Unavailable("module-watch-limit");
            var scope = ControlScope();
            var resource = new IntegrationModuleEnablement(config, info.PrimaryActorOID, () => ModuleControlLive(request.OwnerToken, scope));
            _leases.Sweep(scope, true);
            return IntegrationSession.Serialize(_leases.Apply(request.OwnerToken, request.LeaseId, scope, resource,
                request.ExpectedRevision, request.ExpectedEnabled.Value ? "true" : "false", request.Enabled.Value ? "true" : "false", request.LeaseMs));
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Module control failed: {error.GetType().Name}: {error.Message}");
            return Unavailable("module-control-unavailable");
        }
    }

    internal string GetModules(string json)
    {
        var request = IntegrationSession.Parse<ModuleQuery>(json);
        if (request == null || !Session.Matches(request.Context) || request.Offset < 0 || request.Count is < 1 or > 128)
            return Unavailable("invalid-module-catalog-context-or-page");
        var catalog = _moduleCatalog ??= BossModuleRegistry.RegisteredModules.Values.OrderBy(i => i.PrimaryActorOID).ToArray();
        if (request.Offset > catalog.Length || bossmod.LoadedModules.Count + bossmod.PendingModules.Count > 1024)
            return Unavailable("module-page-or-observation-limit");
        var config = BossModuleManager.Config;
        var loaded = bossmod.LoadedModules.GroupBy(m => m.Info?.PrimaryActorOID ?? m.PrimaryActor.OID).ToDictionary(g => g.Key, g => g.ToArray());
        var pending = bossmod.PendingModules.GroupBy(m => m.Info?.PrimaryActorOID ?? m.PrimaryActor.OID).ToDictionary(g => g.Key, g => g.Count());
        var modules = catalog.Skip(request.Offset).Take(request.Count).Select(info =>
        {
            var enabled = config.IsModuleConfiguredEnabled(info.PrimaryActorOID);
            var effectiveEnabled = config.IsModuleEnabled(info.PrimaryActorOID);
            var instances = loaded.GetValueOrDefault(info.PrimaryActorOID) ?? [];
            return new
            {
                identity = info.PrimaryActorOID.ToString("X") + ":" + info.ModuleType.FullName,
                primaryActorOid = info.PrimaryActorOID.ToString("X"), module = info.ModuleType.FullName,
                maturity = info.Maturity.ToString(), configuredEnabled = enabled, effectiveEnabled,
                minimumMaturity = config.MinMaturity.ToString(), meetsMinimumMaturity = info.Maturity >= config.MinMaturity,
                eligibleByConfiguration = enabled && info.Maturity >= config.MinMaturity,
                eligibleToLoad = effectiveEnabled && info.Maturity >= config.MinMaturity,
                expansion = info.Expansion.ToString(), category = info.Category.ToString(), info.GroupID, info.NameID,
                selected = bossmod.ActiveModule?.GetType() == info.ModuleType,
                loadedActors = instances.Take(16).Select(m => new { actorId = m.PrimaryActor.InstanceID.ToString("X"),
                    activePhase = m.StateMachine.ActivePhaseIndex, positioningActive = m.StateMachine.ActiveState != null,
                    retainedAfterLease = bossmod.RetainedAfterModuleLease(m) }).ToArray(),
                loadedActorsComplete = instances.Length <= 16, pendingInstances = pending.GetValueOrDefault(info.PrimaryActorOID)
            };
        }).ToArray();
        return IntegrationSession.Serialize(new
        {
            available = true, context = request.Context, catalogVersion = 1, total = catalog.Length, request.Offset,
            nextOffset = request.Offset + modules.Length < catalog.Length ? (int?)(request.Offset + modules.Length) : null,
            modules, enablementLeasesSupported = true,
            meaning = "registered identity, configured/runtime eligibility and native loaded/active retention are distinct; no maturity bypass"
        });
    }
}
