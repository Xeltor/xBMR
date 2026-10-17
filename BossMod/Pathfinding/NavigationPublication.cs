using System.Diagnostics;
using System.Threading;

namespace BossMod.Pathfinding;

// All arrays are private copies. Callers receive copies/JSON, never a worker's writable map.
internal sealed class NavigationRaster
{
    internal const int MaxCells = 262144;
    internal const int MaxWindowCells = 4096;
    internal readonly float Resolution;
    internal readonly int Width;
    internal readonly int Height;
    internal readonly WPos Center;
    internal readonly Angle Rotation;
    private readonly float[] _timeLimits;
    private readonly float[] _priorities;
    private readonly byte[] _blocks;

    internal sealed record Window(int Left, int Top, int Width, int Height, byte[] Blocks, float?[] TimeLimitsSeconds, float?[] GoalPriority);

    internal NavigationRaster(Map map, float[]? unrelaxedLimits, byte[] blocks)
    {
        var count = checked(map.Width * map.Height);
        if (count > MaxCells || count <= 0 || blocks.Length != count)
            throw new ArgumentException("Unsupported raster dimensions.");
        Resolution = map.Resolution;
        Width = map.Width;
        Height = map.Height;
        Center = map.Center;
        Rotation = map.Rotation;
        _timeLimits = (unrelaxedLimits ?? map.PixelMaxG).AsSpan(0, count).ToArray();
        _priorities = map.PixelPriority.AsSpan(0, count).ToArray();
        _blocks = blocks.ToArray();
    }

    internal Window? CopyWindow(int left, int top, int width, int height)
    {
        if (left < 0 || top < 0 || width <= 0 || height <= 0 || (long)width * height > MaxWindowCells
            || (long)left + width > Width || (long)top + height > Height)
            return null;
        var count = width * height;
        var blocks = new byte[count];
        var times = new float?[count];
        var priorities = new float?[count];
        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                var source = (top + y) * Width + left + x;
                var dest = y * width + x;
                blocks[dest] = _blocks[source];
                var time = _timeLimits[source];
                if (!float.IsFinite(time) || blocks[dest] == byte.MaxValue)
                    return null;
                times[dest] = time == float.MaxValue || !float.IsFinite(time) || blocks[dest] != 0 ? null : time;
                var priority = _priorities[source];
                if (!float.IsFinite(priority))
                    return null;
                priorities[dest] = priority == float.MinValue || !float.IsFinite(priority) || blocks[dest] != 0 ? null : priority;
            }
        }
        return new(left, top, width, height, blocks, times, priorities);
    }

    internal byte BlockAt(int x, int y) => _blocks[y * Width + x];
    internal Map? CopyForSearch(float elapsedSeconds, int maxCells)
    {
        var count = checked(Width * Height);
        if (count > maxCells || !float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            return null;
        var map = new Map();
        map.InitGrid(Resolution, Center, Width, Height, Rotation);
        for (var i = 0; i < count; ++i)
        {
            var limit = _timeLimits[i];
            if (!float.IsFinite(limit) || _blocks[i] == byte.MaxValue)
                return null;
            map.PixelMaxG[i] = _blocks[i] != 0 ? -1 : limit == float.MaxValue ? float.MaxValue : limit - elapsedSeconds;
        }
        return map;
    }
    internal Vector2 WorldToGrid(WPos point)
    {
        var localZ = Rotation.ToDirection() / Resolution;
        var offset = point - Center;
        return new((Width >> 1) + offset.Dot(localZ.OrthoL()), (Height >> 1) + offset.Dot(localZ));
    }
}

internal sealed record NavigationIdentity(long Generation, string? IntegrationInstance, long HintGeneration, DateTime WorldTime,
    DateTimeOffset CapturedUtc, long CapturedTimestamp, WPos Origin, bool Casting, float Speed, float AvoidanceBuffer, int? ProjectionLayer,
    ulong ActorId = 0, ushort ZoneId = 0, ushort ContentId = 0);

internal sealed class NavigationPublication
{
    private static long _generation;
    internal static long NextGeneration() => Interlocked.Increment(ref _generation);
    internal readonly NavigationIdentity Source;
    internal readonly DateTimeOffset BuildCompletedUtc;
    internal readonly DateTimeOffset? PublishedUtc;
    internal readonly NavigationRaster? Raster;
    private readonly WPos[] _witness;
    internal readonly bool WitnessComplete;
    internal readonly bool NativeRelaxedWarnings;
    internal readonly string NativeScore;
    internal readonly bool NativeMaxGoalReached;
    internal readonly WPos? Destination;
    internal readonly WPos? NextWaypoint;
    internal readonly WPos Endpoint;
    internal readonly float TimeToGoal;
    internal readonly float LeewaySeconds;
    internal readonly bool HasTeleporters;
    internal readonly NavigationMapSource? MapSource;

    internal bool TryRemainingLeeway(DateTime worldTime, ulong actorId, ushort zoneId, ushort contentId, out float leeway, long? timestamp = null)
    {
        leeway = 0;
        var worldAge = (worldTime - Source.WorldTime).TotalSeconds;
        var age = ((timestamp ?? Stopwatch.GetTimestamp()) - Source.CapturedTimestamp) / (double)Stopwatch.Frequency;
        if (PublishedUtc == null || Source.ActorId != actorId || Source.ZoneId != zoneId || Source.ContentId != contentId
            || worldAge < 0 || worldAge >= 0.25 || age < 0 || age >= 0.25 || !float.IsFinite(LeewaySeconds))
            return false;
        // Retained results carry the original deadline, even if observed repeatedly or the world
        // clock pauses. Never grant a fresh cast window merely because another frame reads it.
        leeway = Math.Max(0, LeewaySeconds - (float)Math.Max(worldAge, age));
        return true;
    }

    internal NavigationPublication(NavigationIdentity source, NavigationRaster? raster, WPos[] witness, bool complete,
        bool relaxed, ThetaStar.Score score, NavigationDecision decision, bool hasTeleporters, NavigationMapSource? mapSource = null)
    {
        Source = source;
        BuildCompletedUtc = DateTimeOffset.UtcNow;
        Raster = raster;
        _witness = witness.ToArray();
        WitnessComplete = complete;
        NativeRelaxedWarnings = relaxed;
        NativeScore = score.ToString();
        NativeMaxGoalReached = score == ThetaStar.Score.SafeMaxPrio;
        Destination = decision.Destination;
        NextWaypoint = decision.NextWaypoint;
        Endpoint = witness.Length > 0 ? witness[^1] : source.Origin;
        TimeToGoal = decision.TimeToGoal;
        LeewaySeconds = decision.LeewaySeconds;
        HasTeleporters = hasTeleporters;
        MapSource = mapSource;
    }

    private NavigationPublication(NavigationPublication built)
    {
        Source = built.Source;
        BuildCompletedUtc = built.BuildCompletedUtc;
        PublishedUtc = DateTimeOffset.UtcNow;
        Raster = built.Raster;
        _witness = built._witness;
        WitnessComplete = built.WitnessComplete;
        NativeRelaxedWarnings = built.NativeRelaxedWarnings;
        NativeScore = built.NativeScore;
        NativeMaxGoalReached = built.NativeMaxGoalReached;
        Destination = built.Destination;
        NextWaypoint = built.NextWaypoint;
        Endpoint = built.Endpoint;
        TimeToGoal = built.TimeToGoal;
        LeewaySeconds = built.LeewaySeconds;
        HasTeleporters = built.HasTeleporters;
        MapSource = built.MapSource;
    }

    internal NavigationPublication Publish() => PublishedUtc != null ? this : new(this);
    internal WPos[] CopyWitness() => _witness.ToArray();
    internal double AgeSeconds => (Stopwatch.GetTimestamp() - Source.CapturedTimestamp) / (double)Stopwatch.Frequency;
    internal bool Matches(string instance, long generation, ulong actor, ushort zone, ushort content, long? now = null)
    {
        var age = ((now ?? Stopwatch.GetTimestamp()) - Source.CapturedTimestamp) / (double)Stopwatch.Frequency;
        return Source.IntegrationInstance == instance && Source.HintGeneration > 0 && Source.Generation == generation
            && Source.ActorId == actor && Source.ZoneId == zone && Source.ContentId == content && PublishedUtc != null && age is >= 0 and < 0.25;
    }
}
