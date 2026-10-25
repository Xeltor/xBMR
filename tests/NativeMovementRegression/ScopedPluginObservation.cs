using BossMod.CombatAssistance.Integrations;

public static partial class Checks
{
    private static void ScopedPluginObservation()
    {
        var reads = 0;
        var current = new object();
        var observations = new ScopedObservation<object>(() => { ++reads; return current; });
        using (observations.Begin())
        {
            for (var i = 0; i < 32; ++i) Check(ReferenceEquals(observations.Read(), current), "Same synchronous observation shares only the current inventory.");
            Check(reads == 1, "Repeated integration services capture plugin inventory once within a synchronous update.");
            current = new();
            using (observations.Begin()) Check(ReferenceEquals(observations.Read(), current), "A nested callback observes new inventory immediately.");
            var after = new object(); current = after;
            Check(ReferenceEquals(observations.Read(), after), "Nested scope disposal invalidates the parent rather than restoring older plugin state.");
        }
        current = new();
        Check(ReferenceEquals(observations.Read(), current), "No plugin inventory snapshot survives outside its synchronous observation.");
        using (observations.Begin())
        {
            var rejected = Task.Run(() =>
            {
                try { using var invalid = observations.Begin(); return false; }
                catch (InvalidOperationException) { return true; }
            }).GetAwaiter().GetResult();
            Check(rejected, "A worker cannot take ownership of the framework's active observation scope.");
        }
    }
}
