using System.Diagnostics;
using System.Threading;

namespace BossMod.Integration;

// Copies native input results without exposing a hook, pointer or mutable game/controller object.
internal sealed class IntegrationInputObservation
{
    internal sealed record Assignment(string InstanceGeneration, long FrameId, Vector3? Direction);
    internal sealed record Sample(long Generation, DateTimeOffset ObservedUtc, long ObservedTimestamp, string Kind,
        Vector2 UserDirection, Vector2 ActualDirection, Vector2 OutputDirection, bool FollowPath, bool Misdirection,
        Assignment? MatchingAssignment);
    private Assignment? _assignment;
    private Sample? _latest;
    private long _generation;
    internal Sample? Latest => Volatile.Read(ref _latest);
    internal void Assign(string instance, long frame, Vector3? direction) => Volatile.Write(ref _assignment, new(instance, frame, direction));
    internal void Observe(string kind, Vector3? currentDirection, Vector2 user, Vector2 actual, Vector2 output, bool follow, bool misdirection)
    {
        var assignment = Volatile.Read(ref _assignment);
        if (assignment?.Direction != currentDirection)
            assignment = null;
        Volatile.Write(ref _latest, new(Interlocked.Increment(ref _generation), DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            kind, user, actual, output, follow, misdirection, assignment));
    }
}
