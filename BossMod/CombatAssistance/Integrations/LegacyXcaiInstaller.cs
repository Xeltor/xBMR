using Dalamud.Plugin;
using System.IO;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Narrow installer adapter for the one retired internal name. Reflection is
// necessary because Dalamud does not expose uninstall through its public API.
internal sealed class LegacyXcaiInstaller(IDalamudPluginInterface pluginInterface, Runtime.LegacyXcaiNotice notice) : Runtime.ILegacyXcaiRetirementOperations
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string InternalName = "XelsCombatAI";
    private bool _verified;

    internal static void VerifyContract(Assembly assembly)
    {
        var local = assembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin", true)!;
        var manager = assembly.GetType("Dalamud.Plugin.Internal.PluginManager", true)!;
        var disposal = assembly.GetType("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode", true)!;
        _ = Enum.Parse(disposal, "WaitBeforeDispose");
        RequireMethod(local, "UnloadAsync", [disposal], typeof(Task));
        RequireMethod(local, "ScheduleDeletion", [typeof(bool)], typeof(void));
        RequireMethod(manager, "RemovePlugin", [local], typeof(void));
        foreach (var name in new[] { "InternalName", "IsLoaded", "IsDev", "State", "DllFile" })
            _ = RequireProperty(local, name);
        _ = RequireProperty(manager, "InstalledPlugins");
        var dev = assembly.GetType("Dalamud.Plugin.Internal.Types.LocalDevPlugin", true)!;
        foreach (var name in new[] { "StartOnBoot", "AutomaticReload" })
            if (RequireProperty(dev, name) is not { CanWrite: true, PropertyType: var type } || type != typeof(bool))
                throw new MissingMemberException(dev.FullName, name);
        var location = assembly.GetType("Dalamud.Configuration.DevPluginLocationSettings", true)!;
        if (dev.GetField("locationSettings", Flags)?.FieldType != location)
            throw new MissingFieldException(dev.FullName, "locationSettings");
        _ = RequireProperty(location, "Path");
        if (!RequireProperty(location, "IsEnabled").CanWrite)
            throw new MissingMemberException(location.FullName, "IsEnabled");
        RequireMethod(assembly.GetType("Dalamud.Configuration.Internal.DalamudConfiguration", true)!, "QueueSave", [], typeof(void));
        var exposed = assembly.GetType("Dalamud.Plugin.ExposedPlugin", true)!;
        if (exposed.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Count(field => field.FieldType == local) != 1)
            throw new InvalidOperationException("Dalamud's installed-plugin identity contract changed.");
    }

    public object[] FindInstallations()
    {
        var matches = pluginInterface.InstalledPlugins.Where(plugin => plugin.InternalName == InternalName).ToArray();
        if (matches.Length == 0)
            return [];
        if (!_verified)
        {
            VerifyContract(typeof(IDalamudPluginInterface).Assembly);
            _verified = true;
        }
        return matches.Select(plugin => ReflectionObjectSearch.ReadLocalPlugin(plugin)
            ?? throw new InvalidOperationException("Cannot identify the retired XCAI installation.")).ToArray();
    }

    public void ObserveInstallations(bool installed) => notice.ObserveInstallations(installed);

    public Task Unload(object installation)
    {
        RequireCurrent(installation);
        if (Read(installation, "IsDev") is true)
            DisableDevelopmentRegistration(installation);
        if (Read(installation, "State")?.ToString() == "Unloaded" && Read(installation, "IsLoaded") is false)
            return Task.CompletedTask;
        var disposal = installation.GetType().Assembly.GetType("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode", true)!;
        return (Task)(RequireMethod(installation.GetType(), "UnloadAsync", [disposal], typeof(Task))
            .Invoke(installation, [Enum.Parse(disposal, "WaitBeforeDispose")])
            ?? throw new InvalidOperationException("Dalamud did not return an unload completion."));
    }

    public void RemoveUnloaded(object installation)
    {
        RequireCurrent(installation);
        if (Read(installation, "IsLoaded") is not false || Read(installation, "State")?.ToString() != "Unloaded")
            throw new InvalidOperationException("XCAI has not finished unloading; removal will be retried.");
        var assembly = installation.GetType().Assembly;
        var local = assembly.GetType("Dalamud.Plugin.Internal.Types.LocalPlugin", true)!;
        if (Read(installation, "IsDev") is not true)
            RequireMethod(local, "ScheduleDeletion", [typeof(bool)], typeof(void)).Invoke(installation, [true]);
        // The installer removes the registration immediately and cleans scheduled
        // binary files on restart. Configurations, replays and source are untouched.
        var manager = GetService(assembly, "Dalamud.Plugin.Internal.PluginManager");
        RequireMethod(manager.GetType(), "RemovePlugin", [local], typeof(void)).Invoke(manager, [installation]);
    }

    private static void DisableDevelopmentRegistration(object installation)
    {
        var type = installation.GetType();
        RequireProperty(type, "AutomaticReload").SetValue(installation, false);
        RequireProperty(type, "StartOnBoot").SetValue(installation, false);
        var location = type.GetField("locationSettings", Flags)!.GetValue(installation)!;
        // A directory registration can contain other development plugins. Disable
        // only an exact DLL registration; never disable unrelated directory entries.
        if (Read(location, "Path") is string path && Read(installation, "DllFile") is FileInfo dll
            && string.Equals(Path.GetFullPath(path), dll.FullName, StringComparison.OrdinalIgnoreCase))
            RequireProperty(location.GetType(), "IsEnabled").SetValue(location, false);
        var configuration = GetService(type.Assembly, "Dalamud.Configuration.Internal.DalamudConfiguration");
        RequireMethod(configuration.GetType(), "QueueSave", [], typeof(void)).Invoke(configuration, null);
    }

    private void RequireCurrent(object installation)
    {
        if (Read(installation, "InternalName") is not InternalName
            || !FindInstallations().Any(current => ReferenceEquals(current, installation)))
            throw new InvalidOperationException("The XCAI installation changed; stale removal was rejected.");
    }

    public void ReportFailure(Exception error)
    {
        Service.Logger.Warning(error, "Mandatory XCAI retirement could not complete; retrying after 30 seconds. Saved settings are untouched.");
    }

    private static object GetService(Assembly assembly, string typeName) => assembly.GetType("Dalamud.Service`1", true)!
        .MakeGenericType(assembly.GetType(typeName, true)!).GetMethod("Get", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, null) ?? throw new InvalidOperationException("Dalamud installer service is unavailable.");
    private static object? Read(object target, string name) => RequireProperty(target.GetType(), name).GetValue(target);
    private static PropertyInfo RequireProperty(Type type, string name) => type.GetProperty(name, Flags)
        ?? throw new MissingMemberException(type.FullName, name);
    private static MethodInfo RequireMethod(Type type, string name, Type[] arguments, Type result)
    {
        var method = type.GetMethod(name, Flags, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);
        return result.IsAssignableFrom(method.ReturnType) ? method : throw new InvalidOperationException($"Unexpected {name} return contract.");
    }
}
