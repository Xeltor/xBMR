using BossMod.Autorotation;
using System.Globalization;

namespace BossMod.Integration;

internal sealed class IntegrationStrategy(Preset.ModuleSettings module, int track, string key, Func<bool> isLive) : IntegrationLeases.IResource
{
    internal sealed record Value(string ValueText, string Target = "Automatic", int TargetParam = 0);
    public string Key => key;
    public int MaxLeaseMs => 2000;
    private StrategyConfig Config => module.Definition.Configs[track];
    private static string F(float value) => float.IsNaN(value) ? "default" : value.ToString("R", CultureInfo.InvariantCulture);

    private string Format(StrategyValue value)
    {
        var text = value switch
        {
            StrategyValueTrack v when Config is StrategyConfigTrack c && (uint)v.Option < (uint)c.Options.Count => c.Options[v.Option].InternalName,
            StrategyValueFloat v => F(v.Value),
            StrategyValueInt v => v.Value.ToString(CultureInfo.InvariantCulture),
            _ => "unknown"
        };
        return IntegrationSession.Serialize(new
        {
            valueText = text,
            target = value is StrategyValueTrack t ? t.Target.ToString() : null,
            targetParam = value is StrategyValueTrack p ? (int?)p.TargetParam : null,
            priority = value is StrategyValueTrack pr ? F(pr.PriorityOverride) : null,
            offset1 = value is StrategyValueTrack x ? F(x.Offset1) : null,
            offset2 = value is StrategyValueTrack z ? F(z.Offset2) : null,
            expireIn = F(value.ExpireIn)
        });
    }

    public IntegrationLeases.State Read()
    {
        var state = module.ReadOwnedStrategy(track);
        return new(state.Available && state.Value != null ? Format(state.Value) : "unavailable", state.Revision, state.LeaseId);
    }

    internal bool TryValue(Value? request, StrategyValue current, out StrategyValue? value)
    {
        value = null;
        if (request == null || request.ValueText == null || request.ValueText.Length > 128
            || request.Target is not ("Automatic" or "EnemyByOID") || request.Target == "Automatic" && request.TargetParam != 0
            || request.Target == "EnemyByOID" && request.TargetParam == 0)
            return false;
        switch (Config)
        {
            case StrategyConfigTrack config:
                var option = config.Options.FindIndex(o => o.InternalName == request.ValueText);
                if (option < 0 || !Allows(request.ValueText, current)
                    || request.Target == "EnemyByOID" && (config.Options[option].SupportedTargets & ActionTargets.Hostile) == 0)
                    return false;
                value = (current as StrategyValueTrack ?? new()) with
                {
                    Option = option,
                    Target = request.Target == "Automatic" ? StrategyTarget.Automatic : StrategyTarget.EnemyByOID,
                    TargetParam = request.TargetParam,
                    Offset1 = 0,
                    Offset2 = 0
                };
                return true;
            case StrategyConfigFloat config when request.Target == "Automatic":
                if (!float.TryParse(request.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
                    || !float.IsFinite(f) || f < config.MinValue || f > config.MaxValue)
                    return false;
                value = new StrategyValueFloat { Value = f };
                return true;
            case StrategyConfigInt config when request.Target == "Automatic":
                if (!long.TryParse(request.ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                    || i < config.MinValue || i > config.MaxValue)
                    return false;
                value = new StrategyValueInt { Value = i };
                return true;
            default:
                return false;
        }
    }

    internal bool Allows(string option, StrategyValue current)
    {
        if (module.Type != typeof(Autorotation.MiscAI.NormalMovement))
            return true;
        return Config.InternalName switch
        {
            "Destination" => option is "None" or "Pathfind",
            "Range" => option is "Any" or "MaxRange" or "GreedAutomatic" or "Drag",
            "Cast" => option is "Leeway" or "DropMove" or "DropInstants",
            "SpecialModes" => option == "Automatic",
            "DelayMovement" or "DodgeDelayMovement" => option == "None",
            "ForbiddenZoneCushion" => current is StrategyValueTrack previous && Config is StrategyConfigTrack config
                && config.Options.FindIndex(o => o.InternalName == option) >= previous.Option,
            _ => true
        };
    }

    public IntegrationLeases.Write Apply(string leaseId, IntegrationLeases.State expected, string json)
    {
        var state = module.ReadOwnedStrategy(track);
        if (!state.Available || state.Value == null || state.Revision != expected.Revision || Format(state.Value) != expected.Value
            || !TryValue(IntegrationSession.Parse<Value>(json), state.Value, out var value))
            return new(false, "changed-or-unsupported-strategy", state.Revision);
        if (module.TransientSettings.Any(s => s.Track == track))
            return new(false, "legacy-transient-active", state.Revision);
        var accepted = module.SetOwnedStrategy(track, leaseId, state.Revision, value!, isLive);
        var after = module.ReadOwnedStrategy(track);
        return new(accepted && after.LeaseId == leaseId, accepted ? "applied" : "strategy-ownership-changed", after.Revision);
    }

    public bool Renew(string leaseId, long revision, int leaseMs) => module.RenewOwnedStrategy(track, leaseId, revision, leaseMs);
    public bool Release(string leaseId, long revision) => module.ReleaseOwnedStrategy(track, leaseId, revision);
}
