using BossMod.Autorotation;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private sealed class PresetIdentity { internal readonly string Id = Guid.NewGuid().ToString("N"); }
    private readonly ConditionalWeakTable<Preset, PresetIdentity> _strategyPresets = new();

    internal sealed record StrategyQuery(IntegrationSession.Context Context, string PresetName, string Module, string Track);
    internal sealed record StrategyRequest(IntegrationSession.Context Context, string OwnerToken, string PresetName, string PresetId,
        string Module, string Track, long ExpectedRevision, string ExpectedValue, IntegrationStrategy.Value Value, int LeaseMs, string? LeaseId = null);

    private bool StrategyScopeLive(string owner, string scope, Preset preset, string name)
    {
        var observation = System.Threading.Volatile.Read(ref _controlObservation);
        return Session.IsOwnerActive(owner) && ReferenceEquals(rotation.Preset, preset) && preset.Name == name
            && observation is { Allowed: true } && observation.Scope == scope
            && (System.Diagnostics.Stopwatch.GetTimestamp() - observation.Timestamp) / (double)System.Diagnostics.Stopwatch.Frequency < 0.25;
    }

    private (Preset Preset, Preset.ModuleSettings Module, int Track, string Id)? ResolveStrategy(string presetName, string moduleName, string trackName)
    {
        if (string.IsNullOrWhiteSpace(presetName) || string.IsNullOrWhiteSpace(moduleName) || string.IsNullOrWhiteSpace(trackName))
            return null;
        var preset = rotation.Database.Presets.FindPresetByName(presetName);
        if (preset == null || !ReferenceEquals(rotation.Preset, preset) || preset.Modules.Count > 64)
            return null;
        var modules = preset.Modules.Where(m => m.Type.FullName == moduleName).Take(2).ToArray();
        if (modules.Length != 1 || modules[0].Definition.Configs.Count > 128)
            return null;
        var module = modules[0];
        var track = module.Definition.Configs.FindIndex(c => c.InternalName == trackName);
        if (track < 0 || module.Definition.Configs[track] is StrategyConfigTrack c && c.Options.Count > 128)
            return null;
        return (preset, module, track, _strategyPresets.GetOrCreateValue(preset).Id);
    }

    internal string GetStrategy(string json)
    {
        var request = IntegrationSession.Parse<StrategyQuery>(json);
        if (request == null || !Session.Matches(request.Context))
            return Unavailable("invalid-strategy-context");
        var source = ResolveStrategy(request.PresetName, request.Module, request.Track);
        if (source == null)
            return Unavailable("strategy-not-in-active-preset-or-limit");
        var (preset, module, track, id) = source.Value;
        var config = module.Definition.Configs[track];
        var native = module.ReadOwnedStrategy(track);
        if (!native.Available || native.Value == null)
            return Unavailable("unavailable-native-strategy");
        var resource = new IntegrationStrategy(module, track, $"strategy:{id}:{request.Module}:{request.Track}", () => false);
        var state = resource.Read();
        return IntegrationSession.Serialize(new
        {
            available = true, context = request.Context, request.PresetName, presetId = id, request.Module, request.Track,
            state.Value, state.Revision, leased = state.LeaseId != null, resourceKey = resource.Key,
            legacyTransientActive = module.TransientSettings.Any(s => s.Track == track),
            kind = config is StrategyConfigTrack ? "option" : config is StrategyConfigFloat ? "float" : config is StrategyConfigInt ? "int" : "unsupported",
            options = config is StrategyConfigTrack options ? options.Options.Select(o => new { valueText = o.InternalName,
                allowed = resource.Allows(o.InternalName, native.Value), canTargetEnemyOid = (o.SupportedTargets & ActionTargets.Hostile) != 0 }).ToArray() : null,
            minimum = config switch { StrategyConfigFloat f => f.MinValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                StrategyConfigInt i => i.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => null },
            maximum = config switch { StrategyConfigFloat f => f.MaxValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                StrategyConfigInt i => i.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => null },
            maxLeaseMs = resource.MaxLeaseMs,
            targetKinds = config is StrategyConfigTrack ? new[] { "Automatic", "EnemyByOID" } : new[] { "Automatic" },
            modifierPolicy = "owned value is ignored while a native modifier key is held",
            restoration = "remove own runtime layer; never write saved or legacy transient values"
        });
    }

    internal string ApplyStrategy(string json)
    {
        var request = IntegrationSession.Parse<StrategyRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready" || !Session.CanContribute
            || !ControlsAllowed() || !Session.IsOwnerActive(request.OwnerToken) || _leases == null)
            return Unavailable("invalid-strategy-owner-or-context");
        var source = ResolveStrategy(request.PresetName, request.Module, request.Track);
        if (source == null || source.Value.Id != request.PresetId)
            return Unavailable("changed-or-unavailable-preset");
        var scope = ControlScope();
        var (preset, module, track, id) = source.Value;
        var resource = new IntegrationStrategy(module, track, $"strategy:{id}:{request.Module}:{request.Track}",
            () => StrategyScopeLive(request.OwnerToken, scope, preset, request.PresetName));
        _leases.Sweep(scope, true);
        return IntegrationSession.Serialize(_leases.Apply(request.OwnerToken, request.LeaseId, scope, resource,
            request.ExpectedRevision, request.ExpectedValue, IntegrationSession.Serialize(request.Value), request.LeaseMs));
    }
}
