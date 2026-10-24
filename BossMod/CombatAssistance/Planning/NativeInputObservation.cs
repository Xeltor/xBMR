using System.Diagnostics;
using System.Threading;

namespace BossMod.CombatAssistance.Planning;

internal sealed record MovementOwnerObservation(string Owner, ulong ActorId, DateTime WorldTime, Vector3 Origin,
    Vector3? Destination, Vector3? Direction, bool WroteMovement, string Reason, long? NavigationGeneration);

// One current native input observation. No callback transport, consumer identity,
// pacing proof, renewal or history buffer participates in input application.
internal sealed class NativeInputObservation
{
    internal sealed record Sample(long Timestamp, Vector2 User, Vector2 Actual, Vector2 Output,
        bool Automatic, bool FollowingNavmesh, bool Misdirection, float UserTurn);
    private Sample? _latest;
    internal Sample? Latest => Volatile.Read(ref _latest);
    internal bool ManualFacing => Latest is { } sample && sample.UserTurn != 0
        && (Stopwatch.GetTimestamp() - sample.Timestamp) / (double)Stopwatch.Frequency is >= 0 and < 0.25;
    internal void Observe(Vector2 user, Vector2 actual, Vector2 output, bool automatic, bool follow, bool misdirection, float userTurn)
        => Volatile.Write(ref _latest, new(Stopwatch.GetTimestamp(), user, actual, output, automatic, follow, misdirection, userTurn));
}
