using System.Reflection;
using System.Runtime.Loader;

return ConsoleRunner.Run(() =>
{
    if (args.Length != 4)
        throw new ArgumentException("Pass the built xBMR DLL, Dalamud dependency directory, game directory, and installed Questionable DLL.");
    var assembly = Path.GetFullPath(args[0]);
    AssemblyLoadContext.Default.Resolving += (_, name) => new[] { Path.GetDirectoryName(assembly)!, Path.GetFullPath(args[1]) }
        .Select(dir => Path.Combine(dir, name.Name + ".dll")).Where(File.Exists)
        .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).FirstOrDefault();
    AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
    Assembly.GetExecutingAssembly().GetType("Checks", true)!.GetMethod("Run")!.Invoke(null,
        [Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3])]);
});
