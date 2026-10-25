using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void ResourceArithmetic()
    {
        var exact = true;
        for (var slot = 0; slot < 16; slot++)
            for (var a = 0; a < 16; a++)
                for (var b = 0; b < 16; b++)
                {
                    var rest = 0xAAAAAAAAAAAAAAAAul & ~(15ul << (slot * 4));
                    exact &= MovementPlanningContext.NoFewerResources(rest | ((ulong)a << (slot * 4)),
                        rest | ((ulong)b << (slot * 4))) == (a >= b);
                }
        Check(exact, "packed resource comparison preserves every charge pair in every slot without cross-slot borrowing");
        var random = new Random(81357);
        var bytes = new byte[16];
        for (var sample = 0; sample < 10000; sample++)
        {
            random.NextBytes(bytes);
            var a = BitConverter.ToUInt64(bytes);
            var b = BitConverter.ToUInt64(bytes, 8);
            exact &= MovementPlanningContext.NoFewerResources(a, b) == Reference(a, b);
            exact &= MovementPlanningContext.NoFewerResources(a, a) && MovementPlanningContext.NoFewerResources(a, 0);
        }
        Check(exact, "packed resource comparison equals the independent scalar reference for mixed inventories and fast paths");

        // Equal/dominating inventories are common in label comparisons. Measure
        // the exact primitive separately; this is not a whole-search speed claim.
        var pairs = new (ulong A, ulong B)[4096];
        for (var i = 0; i < pairs.Length; i++)
        {
            random.NextBytes(bytes);
            var a = BitConverter.ToUInt64(bytes) & 0x7777777777777777ul;
            pairs[i] = (i % 3) switch
            {
                0 => (a, a),
                1 => (a + 0x1111111111111111ul, a),
                _ => (a, a + 0x1111111111111111ul),
            };
        }
        _ = Measure(Reference);
        _ = Measure(MovementPlanningContext.NoFewerResources);
        var scalar = Measure(Reference);
        var packed = Measure(MovementPlanningContext.NoFewerResources);
        Check(scalar.Count == packed.Count, "resource arithmetic benchmark preserves all scalar outcomes");
        Console.WriteLine($"Resource comparison, 1,048,576 equal/dominating/missing-charge pairs: scalar={scalar.Milliseconds:F3}ms packed={packed.Milliseconds:F3}ms; primitive only, not game FPS.");

        (double Milliseconds, long Count) Measure(Func<ulong, ulong, bool> compare)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            long count = 0;
            for (var repeat = 0; repeat < 256; repeat++)
                foreach (var pair in pairs)
                    if (compare(pair.A, pair.B)) count++;
            return (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, count);
        }

        static bool Reference(ulong a, ulong b)
        {
            for (var slot = 0; slot < 16; slot++)
                if (((a >> (slot * 4)) & 15) < ((b >> (slot * 4)) & 15)) return false;
            return true;
        }
    }
}
