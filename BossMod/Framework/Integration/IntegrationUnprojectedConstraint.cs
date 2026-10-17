using System.Reflection;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

// Reviewed native utility bodies keep every landing envelope/AOE/direct avoid rule.
// Only their displacement parameters change, on an independent frozen root.
internal static class IntegrationUnprojectedConstraint
{
    private static readonly HashSet<Type> Reviewed =
    [
        typeof(SDInCircleAwayFromOriginPlusIntersectAOECircles),
        typeof(SDKnockbackFixedDirectionAgainstSafewalls),
        typeof(SDKnockbackFixedDirectionAgainstSafewallsPlusRectAOE),
        typeof(SDKnockbackFixedDirectionIntoCircle),
        typeof(SDKnockbackInAABBRectAwayFromOrigin),
        typeof(SDKnockbackInAABBRectAwayFromOriginPlusAOERects),
        typeof(SDKnockbackInAABBRectAwayFromOriginPlusIntersectAOECircles),
        typeof(SDKnockbackInAABBRectFixedDirection),
        typeof(SDKnockbackInAABBRectFixedDirectionPlusAOECircle),
        typeof(SDKnockbackInAABBRectFixedDirectionPlusAOECircles),
        typeof(SDKnockbackInAABBRectLeftRightAlongXAxis),
        typeof(SDKnockbackInAABBRectLeftRightAlongZAxis),
        typeof(SDKnockbackInAABBRectLeftRightAlongZAxisPlusAOERects),
        typeof(SDKnockbackInAABBSquareAwayFromOrigin),
        typeof(SDKnockbackInAABBSquareAwayFromOriginIntoCircle),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusAOECircles),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusAOECirclesMixedRadii),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusAOECirclesMixedRadiiPlusAvoidShape),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusAOERects),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusIntersectAOECircles),
        typeof(SDKnockbackInAABBSquareAwayFromOriginPlusRectAOE),
        typeof(SDKnockbackInAABBSquareFixedDirection),
        typeof(SDKnockbackInAABBSquareFixedDirectionIntoCircle),
        typeof(SDKnockbackInAABBSquareFixedDirectionPlusAOECircle),
        typeof(SDKnockbackInAABBSquareFixedDirectionPlusAOECircles),
        typeof(SDKnockbackInAABBSquareFixedDirectionPlusMixedAOEs),
        typeof(SDKnockbackInAABBSquareLeftRightAlongXAxisPlusAOECircles),
        typeof(SDKnockbackInAABBSquareLeftRightAlongXAxisTowardsGoal),
        typeof(SDKnockbackInAABBSquareLeftRightAlongZAxisTowardsGoal),
        typeof(SDKnockbackInCircleAwayFromOrigin),
        typeof(SDKnockbackInCircleAwayFromOriginIntoCircle),
        typeof(SDKnockbackInCircleAwayFromOriginIntoDirection),
        typeof(SDKnockbackInCircleAwayFromOriginMixedAOEs),
        typeof(SDKnockbackInCircleAwayFromOriginMulti2),
        typeof(SDKnockbackInCircleAwayFromOriginMulti3),
        typeof(SDKnockbackInCircleAwayFromOriginPlusAOECircles),
        typeof(SDKnockbackInCircleAwayFromOriginPlusAOERects),
        typeof(SDKnockbackInCircleAwayFromOriginPlusMixedAOEsPlusSingleCircleIntersection),
        typeof(SDKnockbackInCircleFixedDirection),
        typeof(SDKnockbackInCircleFixedDirectionAndAwayFromOrigin),
        typeof(SDKnockbackInCircleLeftRightAlongXAxis),
        typeof(SDKnockbackInCircleLeftRightAlongZAxis),
        typeof(SDKnockbackInComplexPolygonAwayFromOrigin),
        typeof(SDKnockbackInComplexPolygonAwayFromOriginMixedAOEs),
        typeof(SDKnockbackInComplexPolygonAwayFromOriginPlusAOEAABBSquares),
        typeof(SDKnockbackInComplexPolygonAwayFromOriginPlusAOECircles),
        typeof(SDKnockbackInComplexPolygonAwayFromOriginPlusIntersectionTest),
        typeof(SDKnockbackInComplexPolygonFixedDirection),
        typeof(SDKnockbackInSquareFixedDirection),
        typeof(SDKnockbackTowardsOriginPlusAOECirclesPlusAABBSquareIntersection),
        typeof(SDKnockbackWithWallsAwayFromOriginMultiAimIntoDonuts),
    ];
    internal static ShapeDistance Create(ShapeDistance frozen)
    {
        var type = frozen.GetType();
        if (!Reviewed.Contains(type)) throw new InvalidOperationException("native-landing-view-not-reviewed");
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        // Reaching a wall is a preparation requirement, not a danger at the
        // already displaced position. Preserve only the independently harmful
        // rectangle in the compound variant.
        if (type == typeof(SDKnockbackFixedDirectionAgainstSafewalls)) return new SDUnion([]);
        if (type == typeof(SDKnockbackFixedDirectionAgainstSafewallsPlusRectAOE))
        {
            T Field<T>(string name) => (T)fields.Single(f => f.Name == name).GetValue(frozen)!;
            return new SDRect(Field<WPos>("rectOrigin"), Field<WDir>("rectDirection"), Field<float>("lengthFront"), 0, Field<float>("halfWidth"));
        }
        var copy = (ShapeDistance)RuntimeHelpers.GetUninitializedObject(type);
        var scalar = fields.Any(f => f.Name == "distance" && f.FieldType == typeof(float));
        var changed = 0;
        foreach (var field in fields)
        {
            object? value = field.GetValue(frozen);
            if (field.Name == "distance" && field.FieldType == typeof(float)) { value = 0f; ++changed; }
            if ((field.Name is "dir1" or "dir2" || field.Name == "direction" && (!scalar || type.Name.Contains("FixedDirection", StringComparison.Ordinal)))
                && field.FieldType == typeof(WDir)) { value = default(WDir); ++changed; }
            field.SetValue(copy, value);
        }
        if (changed == 0) throw new InvalidOperationException("native-landing-displacement-parameter-missing");
        return copy;
    }
}
