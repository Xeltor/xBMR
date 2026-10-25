using System.Diagnostics;
using System.Threading;

namespace BossMod.Pathfinding;

// Immutable native raster used by direct route validation. No serialized window limits.
internal sealed class NavigationRaster
{
    internal readonly float Resolution;
    internal readonly int Width;
    internal readonly int Height;
    internal readonly WPos Center;
    internal readonly Angle Rotation;
    private readonly float[] _timeLimits;
    private readonly byte[] _blocks;


    internal NavigationRaster(Map map, float[]? unrelaxedLimits, byte[] blocks)
    {
        var count = checked(map.Width * map.Height);
        if (count <= 0 || blocks.Length != count)
            throw new ArgumentException("Unsupported raster dimensions.");
        Resolution = map.Resolution;
        Width = map.Width;
        Height = map.Height;
        Center = map.Center;
        Rotation = map.Rotation;
        _timeLimits = (unrelaxedLimits ?? map.PixelMaxG).AsSpan(0, count).ToArray();
        _blocks = blocks.ToArray();
    }

    internal float LimitAt(int x, int y) => _timeLimits[y * Width + x];
    internal byte BlockAt(int x, int y) => _blocks[y * Width + x];
    internal Vector2 WorldToGrid(WPos point)
    {
        var localZ = Rotation.ToDirection() / Resolution;
        var offset = point - Center;
        return new((Width >> 1) + offset.Dot(localZ.OrthoL()), (Height >> 1) + offset.Dot(localZ));
    }
}

internal sealed record NavigationIdentity(long Generation, DateTime WorldTime,
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
    internal readonly bool NativeDestinationSafe;
    internal readonly WPos? Destination;
    internal readonly WPos? NextWaypoint;
    internal readonly WPos Endpoint;
    internal readonly float TimeToGoal;
    internal readonly float LeewaySeconds;
    internal readonly bool HasTeleporters;
    internal readonly NavigationMapSource? MapSource;
    // Independent timed-warning assessment of the complete recovery witness.
    // Positive counts are least-bad recovery, never a complete safety proof.
    internal int? RecoveryExposureCount { get; init; }
    internal bool RecoveryHoldForFewerExposures { get; init; }

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
        NativeDestinationSafe = score is ThetaStar.Score.UltimatelySafe or >= ThetaStar.Score.Safe;
        Destination = decision.Destination;
        NextWaypoint = decision.NextWaypoint;
        Endpoint = witness.Length > 0 ? witness[^1] : source.Origin;
        TimeToGoal = decision.TimeToGoal;
        LeewaySeconds = decision.LeewaySeconds;
        HasTeleporters = hasTeleporters;
        MapSource = mapSource;
    }

    private NavigationPublication(NavigationPublication built, bool publish = true)
    {
        Source = built.Source;
        BuildCompletedUtc = built.BuildCompletedUtc;
        PublishedUtc = publish ? DateTimeOffset.UtcNow : built.PublishedUtc;
        Raster = built.Raster;
        _witness = built._witness;
        WitnessComplete = built.WitnessComplete;
        NativeRelaxedWarnings = built.NativeRelaxedWarnings;
        NativeScore = built.NativeScore;
        NativeMaxGoalReached = built.NativeMaxGoalReached;
        NativeDestinationSafe = built.NativeDestinationSafe;
        Destination = built.Destination;
        NextWaypoint = built.NextWaypoint;
        Endpoint = built.Endpoint;
        TimeToGoal = built.TimeToGoal;
        LeewaySeconds = built.LeewaySeconds;
        HasTeleporters = built.HasTeleporters;
        MapSource = built.MapSource;
        RecoveryExposureCount = built.RecoveryExposureCount;
        RecoveryHoldForFewerExposures = built.RecoveryHoldForFewerExposures;
    }

    internal NavigationPublication Publish() => PublishedUtc != null ? this : new(this);
    internal NavigationPublication WithRecoveryExposure(int count) => new(this, false) { RecoveryExposureCount = count };
    internal WPos[] CopyWitness() => _witness.ToArray();
    internal double AgeSeconds => (Stopwatch.GetTimestamp() - Source.CapturedTimestamp) / (double)Stopwatch.Frequency;
}
