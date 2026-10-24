using Dalamud.Plugin;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BossMod.CombatAssistance.Integrations;

internal static class ReflectionObjectSearch
{
    private static readonly ConditionalWeakTable<IDalamudPluginInterface, ScopedObservation<IExposedPlugin[]>> Inventories = new();
    private static readonly ConditionalWeakTable<Type, DiscoveryFields> Fields = new();
    private sealed record DiscoveryFields(FieldInfo? Owner, FieldInfo? Instance);

    internal static ScopedObservation<IExposedPlugin[]>.Scope BeginObservation(IDalamudPluginInterface pluginInterface)
        => Inventory(pluginInterface).Begin();

    private static ScopedObservation<IExposedPlugin[]> Inventory(IDalamudPluginInterface pluginInterface)
        => Inventories.GetValue(pluginInterface, static current => new(() => current.InstalledPlugins.ToArray()));

    internal static IExposedPlugin[] ReadInstalledPlugins(IDalamudPluginInterface pluginInterface)
        => Inventory(pluginInterface).Read();

    public static object? FindLoadedPlugin(IDalamudPluginInterface pluginInterface, string typeFullName, params ReadOnlySpan<string> pluginNames)
        => Resolve(pluginInterface, typeFullName, out _, pluginNames);

    public static string ConnectionStatus(IDalamudPluginInterface pluginInterface, string typeFullName, params ReadOnlySpan<string> pluginNames)
    {
        Resolve(pluginInterface, typeFullName, out var status, pluginNames);
        return status;
    }

    private static object? Resolve(IDalamudPluginInterface pluginInterface, string typeFullName, out string status, ReadOnlySpan<string> pluginNames)
    {
        status = "Not installed";
        if (typeFullName.StartsWith("BossMod.", StringComparison.Ordinal))
        {
            status = "xBMR services are accessed directly.";
            return null;
        }
        try
        {
            var installed = ReadInstalledPlugins(pluginInterface);
            foreach (var plugin in installed)
            {
                if (!MatchesName(plugin, pluginNames)) continue;
                if (!plugin.IsLoaded)
                {
                    status = "Not loaded (disabled or changing state). Check the plugin installer.";
                    continue;
                }
                var instance = ReadLoadedInstance(plugin, typeFullName);
                status = instance == null
                    ? "Link unavailable: loaded plugin instance or Dalamud discovery contract changed."
                    : "Loaded; instance link available";
                return instance;
            }
        }
        catch (Exception)
        {
            // Plugin inventory can change during reload. Never search outside the named plugin.
            status = "Plugin inventory unavailable; waiting for the next check.";
        }
        return null;
    }

    private static bool MatchesName(IExposedPlugin plugin, ReadOnlySpan<string> names)
    {
        foreach (var name in names)
            if (string.Equals(plugin.InternalName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(plugin.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static object? ReadLoadedInstance(IExposedPlugin plugin, string typeFullName)
    {
        if (typeFullName.StartsWith("BossMod.", StringComparison.Ordinal) || !plugin.IsLoaded) return null;
        var owner = ReadLocalPlugin(plugin);
        var instanceField = owner == null ? null : Fields.GetValue(owner.GetType(), DiscoverFields).Instance;
        if (instanceField?.FieldType != typeof(object)) return null;
        var instance = instanceField.GetValue(owner);
        return plugin.IsLoaded && instance?.GetType().FullName == typeFullName ? instance : null;
    }

    internal static object? ReadLocalPlugin(IExposedPlugin plugin)
    {
        // Verified against installed Dalamud 15.0.3.4: ExposedPlugin -> LocalPlugin -> instance.
        // Read only these fields. Arbitrary getters, Tasks and enumerators must never be traversed.
        return Fields.GetValue(plugin.GetType(), DiscoverFields).Owner?.GetValue(plugin);
    }

    private static DiscoveryFields DiscoverFields(Type type)
    {
        var exposedType = typeof(IDalamudPluginInterface).Assembly.GetType("Dalamud.Plugin.ExposedPlugin");
        var localType = typeof(IDalamudPluginInterface).Assembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin");
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        if (type == localType) return new(null, type.GetField("instance", Flags));
        if (exposedType == null || localType == null || type != exposedType)
            return new(null, null);
        var ownerFields = exposedType.GetFields(Flags).Where(field => field.FieldType == localType).ToArray();
        return new(ownerFields.Length == 1 ? ownerFields[0] : null, null);
    }
}
