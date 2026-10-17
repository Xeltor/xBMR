using System.Diagnostics;

namespace BossMod.Integration;

// A copied native controller outcome. It is independent of the older asynchronous
// search publication and never authorizes a route or physical movement.
internal sealed record IntegrationControllerDecision(string Id, string InstanceGeneration, long FrameId, string Owner,
    ulong ActorId, DateTime WorldTime, long CapturedTimestamp, Vector3 Origin, Vector3? Destination, Vector3? Direction,
    bool WroteMovement, string Reason, long? NavigationGeneration, long? NavigationFrameId,
    string? NavigationInstance, Vector2? NavigationDestination)
{
    internal bool Matches(string instance, long frame, ulong actorId, Vector3? direction)
        => WroteMovement && InstanceGeneration == instance && FrameId == frame && ActorId == actorId && Direction == direction
            && (direction == null || float.IsFinite(direction.Value.X) && float.IsFinite(direction.Value.Y) && float.IsFinite(direction.Value.Z));

    internal double AgeSeconds => (Stopwatch.GetTimestamp() - CapturedTimestamp) / (double)Stopwatch.Frequency;
}
