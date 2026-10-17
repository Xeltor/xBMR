using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BossMod.Integration;

// Freeze the native pure geometry data graph, not a delegate to mutable encounter state.
// Native SD utility types contain geometry/value fields; actors, services and callbacks never cross.
internal sealed class IntegrationShapeSnapshot(IntegrationRoutes.Budget budget) : IDisposable
{
    private const int MaxObjects = 8192, MaxElements = 262144;
    private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _shapePath = new(ReferenceEqualityComparer.Instance);
    private int _objects, _elements;
    private readonly List<IDisposable> _owned = [];
    internal string? FailureReason { get; private set; }
    internal IDisposable[] TakeOwned() { var result = _owned.ToArray(); _owned.Clear(); return result; }
    public void Dispose() { foreach (var item in _owned) item.Dispose(); _owned.Clear(); }
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();
    private static readonly ConcurrentDictionary<Type, bool> PlainValues = new();
    private static FieldInfo[] DataFields(Type type) => Fields.GetOrAdd(type, static t =>
    {
        var result = new List<FieldInfo>();
        for (; t != null && t != typeof(object); t = t.BaseType!)
            result.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return [.. result];
    });
    private static bool Plain(Type type) => PlainValues.GetOrAdd(type, static t => t != typeof(nint) && t != typeof(nuint) && !t.IsPointer
        && (t.IsPrimitive || t.IsEnum || t.IsValueType && DataFields(t).All(f => Plain(f.FieldType))));
    private static bool Geometry(Type type)
    {
        if (type.Assembly == typeof(ShapeDistance).Assembly && type.Namespace == "BossMod")
        {
            if (type.Name.StartsWith("SD", StringComparison.Ordinal) || type.Name.StartsWith("AOEShape", StringComparison.Ordinal)
                || type.Name is "RelSimplifiedComplexPolygon" or "RelPolygonWithHoles" or "PolygonBoundaryIndex2D" or "SpatialIndex") return true;
            if (type.IsNested && type.DeclaringType != null) return Geometry(type.DeclaringType);
        }
        if (type.Namespace == "Clipper2Lib") return true;
        if (type.Namespace == "System.Collections.Generic")
            return type.Name.StartsWith("List`", StringComparison.Ordinal) || type.Name.StartsWith("Dictionary`", StringComparison.Ordinal)
                || type.Name.Contains("Comparer", StringComparison.Ordinal) || type.IsNested && type.DeclaringType != null && Geometry(type.DeclaringType);
        return false;
    }

    // Structural equality is a proof, not scalar sampling. Opaque buffers must share identity.
    internal static bool SameNativeGeometry(object? left, object? right, IntegrationRoutes.Budget budget, int depth = 0)
    {
        if (!budget.Step() || depth > 32) return false;
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.GetType() != right.GetType()) return false;
        var type = left.GetType();
        if (type.IsPointer || type == typeof(nint) || type == typeof(nuint) || left is Delegate) return false;
        if (Plain(type) || left is string) return left.Equals(right);
        if (left is Array a && right is Array b)
        {
            if (a.Rank != 1 || b.Rank != 1 || a.Length != b.Length || a.Length > MaxElements) return false;
            for (var i = 0; i < a.Length; ++i)
                if (!SameNativeGeometry(a.GetValue(i), b.GetValue(i), budget, depth + 1)) return false;
            return true;
        }
        if (!Geometry(type) || type.Name == "PolygonBoundaryIndex2D") return false;
        return DataFields(type).All(f => SameNativeGeometry(f.GetValue(left), f.GetValue(right), budget, depth + 1));
    }

    internal bool Matches(object? current, object? frozen, IntegrationRoutes.Budget checkBudget, int depth = 0)
    {
        if (!checkBudget.Step() || depth > 32) return false;
        if (ReferenceEquals(current, frozen)) return true;
        if (current == null || frozen == null || current.GetType() != frozen.GetType()) return false;
        var type = current.GetType();
        if (Plain(type) || current is string) return current.Equals(frozen);
        if (type.FullName == "BossMod.PolygonBoundaryIndex2D")
            return _copies.TryGetValue(current, out var copied) && ReferenceEquals(copied, frozen)
                && type.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(current) is false
                && Equals(type.GetProperty("SnapshotEntryCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(current),
                    type.GetProperty("SnapshotEntryCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(frozen));
        if (current is Array a && frozen is Array b)
        {
            if (a.Rank != 1 || b.Rank != 1 || a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; ++i) if (!Matches(a.GetValue(i), b.GetValue(i), checkBudget, depth + 1)) return false;
            return true;
        }
        if (!Geometry(type) || type.IsPointer || current is Delegate) return false;
        return DataFields(type).All(f => Matches(f.GetValue(current), f.GetValue(frozen), checkBudget, depth + 1));
    }

    internal ShapeDistance? Freeze(ShapeDistance source)
    {
        try { return Copy(source, 0) as ShapeDistance; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or MemberAccessException or NotSupportedException or TargetInvocationException)
        { FailureReason = error.InnerException?.Message ?? error.Message; return null; }
    }

    private object? Copy(object? source, int depth)
    {
        if (!budget.Step() || depth > 32 || ++_objects > MaxObjects) throw new InvalidOperationException("geometry-copy-work-limit");
        if (source == null || source is string) return source;
        var type = source.GetType();
        if (type.IsPointer || type.IsByRefLike || type == typeof(nint) || type == typeof(nuint) || source is Delegate)
            throw new InvalidOperationException("geometry-has-runtime-reference");
        if (Plain(type)) return source; // boxed value copy; no mutable reference field
        if (source is ShapeDistance && _shapePath.Contains(source)) throw new InvalidOperationException("cyclic-native-distance-graph");
        if (!type.IsValueType && _copies.TryGetValue(source, out var existing)) return existing;
        if (type.Assembly == typeof(ShapeDistance).Assembly && type.FullName == "BossMod.PolygonBoundaryIndex2D")
        {
            var count = (int)type.GetProperty("SnapshotEntryCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
            if (count < 0 || (_elements += checked(count * 10)) > MaxElements) throw new InvalidOperationException("polygon-query-elements");
            var owned = type.GetMethod("CloneForReadOnlyQuery", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, [Math.Min(16384, MaxElements / 10)])!;
            _owned.Add((IDisposable)owned); _copies.Add(source, owned); return owned;
        }
        if (source is Array array)
        {
            if (array.Rank != 1 || array.GetLowerBound(0) != 0 || (_elements += array.Length) > MaxElements)
                throw new InvalidOperationException("geometry-array-work-limit");
            var element = type.GetElementType()!;
            if (Plain(element))
            {
                var values = array.Clone(); _copies.Add(source, values); return values;
            }
            var copy = Array.CreateInstance(element, array.Length); _copies.Add(source, copy);
            for (var i = 0; i < array.Length; ++i) copy.SetValue(Copy(array.GetValue(i), depth + 1), i);
            return copy;
        }
        if (!type.IsValueType && !Geometry(type)) throw new InvalidOperationException("geometry-data-type-not-reviewed");
        var result = RuntimeHelpers.GetUninitializedObject(type);
        if (!type.IsValueType) _copies.Add(source, result);
        var shape = source is ShapeDistance;
        if (shape) _shapePath.Add(source);
        try
        {
            foreach (var field in DataFields(type)) field.SetValue(result, Copy(field.GetValue(source), depth + 1));
        }
        finally { if (shape) _shapePath.Remove(source); }
        return result;
    }
}
