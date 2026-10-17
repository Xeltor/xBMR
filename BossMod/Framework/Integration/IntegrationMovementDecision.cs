using BossMod.Pathfinding;

namespace BossMod.Integration;

// Written by the native controller after range/cast corrections, not inferred from an old raster.
internal sealed record IntegrationMovementDecision(string InstanceGeneration, long FrameId, string Owner,
    ulong ActorId, DateTime WorldTime, long CapturedTimestamp, WPos Origin, WPos Destination, Vector3 Direction,
    NavigationPublication Navigation);
