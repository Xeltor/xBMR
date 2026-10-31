using BossMod.CombatAssistance.Integrations;
using System.Reflection;
using System.Reflection.Emit;

public static partial class Checks
{
    private static void RsrAssemblyLayout()
    {
        static Assembly Provider(string name, bool state)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new(name), AssemblyBuilderAccess.RunAndCollect);
            return assembly.DefineDynamicModule(name)
                .DefineType(state ? "RotationSolver.Basic.DataCenter" : "RsrProvider")
                .CreateType()!.Assembly;
        }

        var unavailable = Provider("RsrWithoutState", false);
        Check(RsrRuntimeAccess.ResolveBasicAssembly(unavailable) == null,
            "A missing RSR state contract remains unavailable.");
        var merged = Provider("RsrMergedState", true);
        Check(RsrRuntimeAccess.ResolveBasicAssembly(merged) == merged,
            "Current RSR resolves Basic types from its provider assembly.");
        var split = Provider("RotationSolver.Basic", true);
        Check(RsrRuntimeAccess.ResolveBasicAssembly(unavailable) == split,
            "Older RSR still resolves its separate Basic assembly in the provider context.");
        Check(RsrRuntimeAccess.ResolveBasicAssembly(merged) == merged,
            "A stale separate Basic assembly cannot replace the current merged provider's state.");
    }
}
