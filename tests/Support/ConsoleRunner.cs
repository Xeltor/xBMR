using System.Reflection;

// Managed helper failures must still fail the command, without reaching the runtime's
// unhandled-exception abort path (which creates desktop crash reports on Linux).
internal static class ConsoleRunner
{
    public static int Run(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (Exception error)
        {
            while (error is TargetInvocationException { InnerException: { } inner })
                error = inner;
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
