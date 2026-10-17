using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Diagnostics;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record PhysicalStepRequest(IntegrationSession.Context Context, string OwnerToken, float[] Direction);
    private long _physicalProbeFrame;
    private int _physicalProbeAttempts;
    private IntegrationPhysicalProbe.Result? _lastPhysicalProbe;
    private unsafe IntegrationPhysicalProbe.Result ObservePhysicalStep(Actor actor, Vector3 delta)
    {
        if (_physicalProbeFrame != Session.FrameId) { _physicalProbeFrame = Session.FrameId; _physicalProbeAttempts = 0; }
        if (Service.ClientState?.IsLoggedIn != true) return new("unknown", "native-game-client-unavailable");
        if (++_physicalProbeAttempts > 8 || World.LiveFrameObservedTimestamp <= 0
            || Stopwatch.GetElapsedTime(World.LiveFrameObservedTimestamp).TotalMilliseconds >= 250 || World.Client.Flying || actor.MountId != 0)
            return new("unknown", "native-physical-source-or-work-limit");
        var framework = Framework.Instance();
        var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null || collision->SceneManager->NumScenes is < 1 or > 128 || collision->LoadInProgressCounter != 0)
            return new("unknown", "native-collision-scene-not-ready");
        var sphere = collision->SceneManager->StreamingSphere;
        var from = actor.PosRot.XYZ(); var horizontal = new Vector3(delta.X, 0, delta.Z);
        var length = horizontal.Length(); var target = length > 0 ? from + horizontal / length * Math.Min(length, 1.25f) : from;
        bool Covered(Vector3 p) => float.IsFinite(sphere.W) && sphere.W > actor.HitboxRadius
            && Vector3.Distance(p, new(sphere.X, sphere.Y, sphere.Z)) + actor.HitboxRadius < sphere.W;
        if (!Covered(from) || !Covered(target)) return new("unknown", "native-collision-streaming-coverage-unverified");
        return IntegrationPhysicalProbe.Evaluate(from, delta, actor.HitboxRadius,
            (origin, direction, distance) => BGCollisionModule.RaycastMaterialFilter(origin, direction, out var hit, distance) ? hit.Point : null);
    }
    internal string GetPhysicalStep(string json)
    {
        var request = IntegrationSession.Parse<PhysicalStepRequest>(json);
        if (request == null || !Session.Matches(request.Context) || !Session.IsOwnerActive(request.OwnerToken)
            || request.Direction is not { Length: 2 } || request.Direction.Any(v => !float.IsFinite(v) || Math.Abs(v) > 256)
            || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor)
            return Unavailable("invalid-native-physical-step-request");
        var result = ObservePhysicalStep(actor, new(request.Direction[0], 0, request.Direction[1]));
        return IntegrationSession.Serialize(new { available = true, context = request.Context, actorId = actor.InstanceID.ToString("X"),
            worldTimeTicks = T(World.CurrentTime), result, lookahead = 1.25f, maximumDrop = 2f, sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
            meaning = "read-only loaded native collision scene and five-point floor support observation; unknown floor/streaming is never a proven drop; no movement or action permission" });
    }
}
