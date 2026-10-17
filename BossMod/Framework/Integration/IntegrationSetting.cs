using System.Globalization;

namespace BossMod.Integration;

internal sealed class IntegrationSetting(ConfigRoot root, ConfigNode node, ConfigFieldMetadata field,
    string[] allowedValues, int maxLeaseMs, bool synchronousOnly) : IntegrationLeases.IResource
{
    internal readonly string Node = node.GetType().FullName!;
    internal readonly string Field = field.Name;
    internal readonly string[] AllowedValues = allowedValues;
    internal readonly bool SynchronousOnly = synchronousOnly;
    public string Key => Node + "." + Field;
    public int MaxLeaseMs => maxLeaseMs;
    private static string Format(object? value) => value switch
    {
        bool b => b ? "true" : "false",
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "unavailable"
    };

    public IntegrationLeases.State Read()
    {
        var state = root.ReadOwnedField(node, field);
        return new(Format(state.Value), state.Revision, state.LeaseId);
    }

    public IntegrationLeases.Write Apply(string leaseId, IntegrationLeases.State expected, string value)
    {
        if (!AllowedValues.Contains(value, StringComparer.Ordinal))
            return new(false, "unsupported-setting-value", expected.Revision);
        object parsed = field.FieldType == typeof(bool) ? bool.Parse(value) : field.FieldType.IsEnum
            ? Enum.Parse(field.FieldType, value, ignoreCase: false) : throw new NotSupportedException("Unsupported setting type.");
        var native = root.ReadOwnedField(node, field);
        if (native.Revision != expected.Revision || Format(native.Value) != expected.Value)
            return new(false, "changed-setting", native.Revision);
        var result = root.SetOwnedField(node, field, leaseId, native.Revision, native.Value, parsed);
        return new(result.Accepted, result.Reason, result.Revision);
    }

    public bool Release(string leaseId, long revision) => root.ReleaseOwnedField(node, field, leaseId, revision);

    internal static IntegrationSetting[] Supported(ConfigRoot root)
    {
        IntegrationSetting Setting<T>(string name, string[] values, int duration, bool scoped = true) where T : ConfigNode
            => new(root, root.Get<T>(), GeneratedConfigMetadata.Get<T>().FieldsByName[name], values, duration, scoped);
        return
        [
            Setting<AI.AIConfig>(nameof(AI.AIConfig.DisableObstacleMaps), ["false"], 2000, false),
            Setting<RPRConfig>(nameof(RPRConfig.AlignDashToCamera), ["false"], 250),
            Setting<DNCConfig>(nameof(DNCConfig.AlignDashToCamera), ["false"], 250),
            Setting<WHMConfig>(nameof(WHMConfig.AlignDashToCamera), ["false"], 250),
            Setting<PCTConfig>(nameof(PCTConfig.AlignDashToCamera), ["false"], 250),
            Setting<DRGConfig>(nameof(DRGConfig.ElusiveJump), ["Default"], 250),
            Setting<ClassShared.SharedActionsConfig>(nameof(ClassShared.SharedActionsConfig.AlignDashToCamera), ["false"], 250)
        ];
    }
}
