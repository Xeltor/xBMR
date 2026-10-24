using System;
using System.Numerics;

namespace BossMod.CombatAssistance.UI;

internal static class OverlayCircleSamples
{
    private static readonly Vector2[] OutlinePoints = Build(64, true);
    private static readonly Vector2[] FillPoints = Build(48, false);
    public static ReadOnlySpan<Vector2> Outline => OutlinePoints;
    public static ReadOnlySpan<Vector2> Fill => FillPoints;

    private static Vector2[] Build(int segments, bool closed)
    {
        var points = new Vector2[segments + (closed ? 1 : 0)];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * MathF.Tau / segments;
            points[i] = new(MathF.Sin(angle), MathF.Cos(angle));
        }
        return points;
    }
}
