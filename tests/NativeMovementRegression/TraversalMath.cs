using BossMod;
using BossMod.Pathfinding;
using System.Diagnostics;

public static partial class Checks
{
    private static void TraversalMath()
    {
        var random = new Random(72231);
        foreach (var resolution in new[] { .35f, 1f, 2f })
        foreach (var rotation in new[] { 0f, .731f })
        {
            var map = new Map();
            map.InitGrid(resolution, new(286.02194f, -627.9452f), 24, 20, rotation.Radians());
            for (var scene = 0; scene < 6; ++scene)
            {
                for (var i = 0; i < 480; ++i)
                    map.PixelMaxG[i] = scene switch
                    {
                        0 => float.MaxValue,
                        1 => 20 + random.NextSingle(),
                        _ => random.Next(12) switch { 0 => -1, 1 => float.NaN, 2 => float.PositiveInfinity,
                            < 6 => random.NextSingle() * 12, _ => float.MaxValue }
                    };
                for (var sample = 0; sample < 1000; ++sample)
                {
                    WPos Position() => map.GridToWorld(random.Next(24), random.Next(20),
                        sample % 4 == 0 ? 0 : random.NextSingle(), sample % 3 == 0 ? 0 : random.NextSingle());
                    var a = Position();
                    var b = sample % 8 == 0 ? a : Position();
                    var radius = sample % 9 == 0 ? 4 * resolution : sample % 3 == 0 ? 0 : random.NextSingle() * resolution;
                    var elapsed = sample % 7 == 0 ? 0 : random.NextSingle() * 4;
                    var duration = sample % 5 == 0 ? 0 : random.NextSingle() * 8;
                    foreach (var physical in new[] { false, true })
                    {
                        var expected = RectangleTraversalReference.Check(map, a, b, elapsed, duration, radius, out var expectedSlack, physical);
                        var actual = TimedTraversal.Check(map, a, b, elapsed, duration, radius, out var slack, physical);
                        Check(actual == expected && slack == expectedSlack,
                            $"Interval traversal retains exact rectangle verdict and slack: scene={scene}, sample={sample}, resolution={resolution}, rotation={rotation}.");
                    }
                }
            }
        }
        // Probe every cell separately so missing a corner/edge touch cannot be
        // masked by an earlier obstacle. Also test one-ULP deadline boundaries.
        var corners = Grid(8, 8);
        foreach (var (first, last) in new[] { (0, 63), (7, 56), (63, 0), (56, 7) })
        foreach (var radius in new[] { 0f, .49f })
        for (var cell = 0; cell < 64; ++cell)
        {
            var a = Point(corners, first); var b = Point(corners, last);
            foreach (var limit in new[] { -1f, 0, .5f, MathF.BitDecrement(.5f), MathF.BitIncrement(.5f), 1f })
            {
                corners.PixelMaxG[cell] = limit;
                var expected = RectangleTraversalReference.Check(corners, a, b, 0, 1, radius, out var expectedSlack);
                var actual = TimedTraversal.Check(corners, a, b, 0, 1, radius, out var slack);
                Check(actual == expected && slack == expectedSlack, "Every touched boundary cell and strict deadline agrees with the rectangle reference.");
            }
            corners.PixelMaxG[cell] = float.MaxValue;
        }
        // An independent geometric assertion: the main diagonal touches each
        // corner-adjacent cell, while a cell two columns away cannot intersect.
        corners.PixelMaxG[1] = -1;
        Check(!TimedTraversal.Check(corners, Point(corners, 0), Point(corners, 63), 0, 1, 0, out _), "Inclusive diagonal corner contact is retained.");
        corners.PixelMaxG[1] = float.MaxValue; corners.PixelMaxG[2] = -1;
        Check(TimedTraversal.Check(corners, Point(corners, 0), Point(corners, 63), 0, 1, 0, out _), "A distant cell in the bounding rectangle is not on the diagonal.");

        var broad = Grid(160, 160);
        Array.Fill(broad.PixelMaxG, 20);
        foreach (var (name, a, b, radius) in new[]
        {
            ("adjacent", Point(broad, 161), Point(broad, 322), .25f),
            ("short diagonal", Point(broad, 161), Point(broad, 966), .25f),
            ("30-cell dash", Point(broad, 161), Point(broad, 4991), .25f),
            ("78-cell diagonal", Point(broad, 161), Point(broad, 12719), .25f),
            ("pooled 150-cell diagonal", Point(broad, 161), Point(broad, 24311), .25f),
            ("wide footprint", Point(broad, 805), Point(broad, 12719), 3f),
            ("horizontal", Point(broad, 161), Point(broad, 239), .25f)
        })
        {
            double Measure(bool sweep)
            {
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 2000; ++i)
                {
                    var ok = sweep ? TimedTraversal.Check(broad, a, b, i % 3, 1, radius, out _)
                        : RectangleTraversalReference.Check(broad, a, b, i % 3, 1, radius, out _);
                    if (!ok) throw new InvalidOperationException("Benchmark route unexpectedly blocked.");
                }
                return watch.Elapsed.TotalMilliseconds;
            }
            Measure(false); Measure(true);
            var reference = new List<double>(); var optimized = new List<double>();
            for (var repeat = 0; repeat < 5; ++repeat)
                if (repeat % 2 == 0) { reference.Add(Measure(false)); optimized.Add(Measure(true)); }
                else { optimized.Add(Measure(true)); reference.Add(Measure(false)); }
            reference.Sort(); optimized.Sort();
            Console.WriteLine($"Traversal math {name}: rectangle={reference[2]:F3}ms interval={optimized[2]:F3}ms per 2000 checks; synthetic only.");
        }
    }
}
