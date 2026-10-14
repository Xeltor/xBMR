using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 3)
    throw new ArgumentException("Pass the built BossModReborn DLL, Dalamud dependency directory, and game directory.");
var assembly = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) => new[] { Path.GetDirectoryName(assembly)!, Path.GetFullPath(args[1]) }
    .Select(dir => Path.Combine(dir, name.Name + ".dll")).Where(File.Exists)
    .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).FirstOrDefault();
AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
Assembly.GetExecutingAssembly().GetType("Regression", true)!.GetMethod("Run")!.Invoke(null, [Path.GetFullPath(args[2])]);
