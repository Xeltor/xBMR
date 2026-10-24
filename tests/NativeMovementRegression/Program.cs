using System.Reflection;
using System.Runtime.Loader;

if (args.Length is not (2 or 3))
    throw new ArgumentException("Pass the built xBMR DLL, matching Dalamud dependency directory, and optionally the installed RSR directory for a read-only contract check.");
var assembly = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) => new[] { Path.GetDirectoryName(assembly)!, Path.GetFullPath(args[1]) }
    .Select(dir => Path.Combine(dir, name.Name + ".dll")).Where(File.Exists)
    .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).FirstOrDefault();
var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
Console.WriteLine($"Native module provenance: {loaded.GetName().Version}; MVID={loaded.ManifestModule.ModuleVersionId}");
var checks = Assembly.GetExecutingAssembly().GetType("Checks", true)!;
if (args.Length == 3)
    checks.GetMethod("InstalledRsrContracts")!.Invoke(null, [args[2]]);
else
    checks.GetMethod("Run")!.Invoke(null, null);
