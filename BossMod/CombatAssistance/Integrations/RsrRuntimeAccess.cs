using BossMod.CombatAssistance.Support;
using Dalamud.Plugin;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace BossMod.CombatAssistance.Integrations;

// This is the remaining external boundary. No xBMR reflection is used.
internal sealed class RsrRuntimeAccess(IDalamudPluginInterface plugin)
{
    internal bool TryGetSupportContext([NotNullWhen(true)] out Assembly? assembly, [NotNullWhen(true)] out Assembly? basic, [NotNullWhen(true)] out object? rotation)
    {
        assembly = ReflectionObjectSearch.FindLoadedPlugin(plugin, "RotationSolver.RotationSolverPlugin",
            "RotationSolver", "Rotation Solver Reborn")?.GetType().Assembly;
        basic = assembly == null ? null : ResolveReferencedAssemblyInLoadContext(assembly, "RotationSolver.Basic");
        rotation = basic?.GetType("RotationSolver.Basic.DataCenter")?.GetProperty("CurrentRotation",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        return assembly != null && basic != null && rotation != null;
    }

    internal static Assembly? ResolveReferencedAssemblyInLoadContext(Assembly assembly, string name)
        => AssemblyLoadContext.GetLoadContext(assembly)?.Assemblies.SingleOrDefault(a => a.GetName().Name == name);
    internal static uint Job(object rotation) => Convert.ToUInt32(rotation.GetType().GetProperty("Job",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(rotation) ?? 0);
    internal static SupportAction? Support(uint job) => job switch
    {
        40 => SupportAction.Kardia,
        38 => SupportAction.DancePartner,
        28 => SupportAction.FeyUnion,
        33 => SupportAction.Balance,
        24 or 27 or 35 => SupportAction.Resurrection,
        _ => null
    };
}
