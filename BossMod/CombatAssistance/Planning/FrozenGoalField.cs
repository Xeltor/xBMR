using BossMod.Pathfinding;

namespace BossMod.CombatAssistance.Planning;

// Capture on the framework thread. An opaque native goal is NOT a safety or assignment proof.
// Keep both native aggregate priority and independent leaf scores, with the same four-corner
// conservative sampling as NavigationDecision. No live callback survives this boundary.
internal sealed class FrozenGoalField
{
    private readonly ImmutableArray<float> _aggregate;
    private readonly ImmutableArray<ImmutableArray<float>> _leaves;
    private readonly (int Width, int Height, float Resolution, WPos Center, Angle Rotation) _geometry;
    internal int Cells => _aggregate.Length;
    internal int Goals => _leaves.Length;
    private FrozenGoalField(Map map, float[] aggregate, ImmutableArray<ImmutableArray<float>> leaves)
    {
        _geometry = (map.Width, map.Height, map.Resolution, map.Center, map.Rotation);
        _aggregate = aggregate.ToImmutableArray();
        _leaves = leaves;
    }
    internal float Aggregate(int cell) => _aggregate[cell];
    internal float Leaf(int goal, int cell) => _leaves[goal][cell];

    internal static FrozenGoalField Capture(Map map, IReadOnlyList<Func<WPos, float>> goals)
    {
        var cells = checked(map.Width * map.Height);
        if (cells <= 0)
            throw new ArgumentException("Unsupported native goal field dimensions.");
        var cornerWidth = map.Width + 1;
        var cornerCount = checked(cornerWidth * (map.Height + 1));
        var sums = new float[cornerCount];
        var values = new float[cornerCount];
        var leaves = ImmutableArray.CreateBuilder<ImmutableArray<float>>(goals.Count);
        foreach (var goal in goals)
        {
            for (var y = 0; y <= map.Height; ++y)
                for (var x = 0; x <= map.Width; ++x)
                {
                    var index = y * cornerWidth + x;
                    var value = goal(map.GridToWorld(x, y, 0, 0));
                    if (!float.IsFinite(value) || !float.IsFinite(sums[index] + value))
                        throw new ArgumentException("Native goal returned an invalid value.");
                    sums[index] += value;
                    values[index] = value;
                }
            leaves.Add(CellMinima(values).ToImmutableArray());
        }
        return new(map, CellMinima(sums), leaves.ToImmutable());

        float[] CellMinima(float[] corners)
        {
            var result = new float[cells];
            for (var y = 0; y < map.Height; ++y)
                for (var x = 0; x < map.Width; ++x)
                {
                    var top = y * cornerWidth + x;
                    result[y * map.Width + x] = Math.Min(Math.Min(corners[top], corners[top + 1]),
                        Math.Min(corners[top + cornerWidth], corners[top + cornerWidth + 1]));
                }
            return result;
        }
    }

    internal bool PreservesEachLeaf(int candidate, ReadOnlySpan<int> baselines)
    {
        foreach (var leaf in _leaves)
            foreach (var baseline in baselines)
                if (leaf[candidate] < leaf[baseline])
                    return false;
        return true;
    }

    internal void ApplyNativePriorities(Map map)
    {
        if (_geometry != (map.Width, map.Height, map.Resolution, map.Center, map.Rotation))
            throw new ArgumentException("Native goal field belongs to a different map.");
        map.MaxPriority = float.MinValue;
        for (var cell = 0; cell < Cells; ++cell)
        {
            if (map.PixelMaxG[cell] != float.MaxValue)
                continue;
            map.PixelPriority[cell] = _aggregate[cell];
            map.MaxPriority = Math.Max(map.MaxPriority, _aggregate[cell]);
        }
    }
}
