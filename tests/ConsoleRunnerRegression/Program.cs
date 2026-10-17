using System.Reflection;

return ConsoleRunner.Run(() =>
{
    switch (args.Single())
    {
        case "success":
            Console.WriteLine("checks passed");
            break;
        case "assertion":
            throw new InvalidOperationException("regression assertion failed");
        case "bootstrap":
            Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "missing-dependency.dll"));
            break;
        case "reflection":
            typeof(Fixture).GetMethod(nameof(Fixture.Outer))!.Invoke(null, null);
            break;
        default:
            throw new ArgumentException("unknown fixture mode");
    }
});

static class Fixture
{
    public static void Outer() => typeof(Fixture).GetMethod(nameof(Inner))!.Invoke(null, null);
    // Match the row-lookup exception recorded in the 2026-10-17 09:06 helper abort.
    public static void Inner() => throw new ArgumentOutOfRangeException("rowId", 854246, "Missing Quest row");
}
