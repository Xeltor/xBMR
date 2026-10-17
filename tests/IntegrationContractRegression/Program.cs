using System.Reflection;
using System.Runtime.Loader;

try
{
    if (args.Length != 2)
        throw new ArgumentException("Pass the built xBMR DLL and Dalamud dependency directory.");
    var assembly = Path.GetFullPath(args[0]);
    AssemblyLoadContext.Default.Resolving += (_, name) => new[] { Path.GetDirectoryName(assembly)!, Path.GetFullPath(args[1]) }
        .Select(dir => Path.Combine(dir, name.Name + ".dll")).Where(File.Exists)
        .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).FirstOrDefault();
    AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
    Assembly.GetExecutingAssembly().GetType("Checks", true)!.GetMethod("Run")!.Invoke(null, null);
    return 0;
}
catch (Exception error)
{
    while (error is TargetInvocationException { InnerException: { } inner })
        error = inner;
    Console.Error.WriteLine(error);
    return 1;
}
