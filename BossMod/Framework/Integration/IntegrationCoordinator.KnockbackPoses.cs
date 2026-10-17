using BossMod.Components;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record NativeKnockbackPoses(IntegrationSession.Context Context, string OwnerToken, string ComponentId,
        IntegrationCounterfactualProjection.Pose[] Poses);
    private long _knockbackPoseFrame;
    private int _knockbackPoseAttempts;
    internal string ProjectNativeKnockbackPoses(string json)
    {
        var request = IntegrationSession.Parse<NativeKnockbackPoses>(json);
        if (request == null || !Session.Matches(request.Context) || !Session.IsOwnerActive(request.OwnerToken)
            || request.Poses is not { Length: >= 1 and <= 32 } || World.Party.Player() is not { IsDead: false, IsDestroyed: false } actor
            || !float.IsFinite(actor.HitboxRadius) || actor.HitboxRadius is < 0 or > 5
            || !float.IsFinite(actor.PosRot.X) || !float.IsFinite(actor.PosRot.Y) || !float.IsFinite(actor.PosRot.Z) || !float.IsFinite(actor.PosRot.W) || bossmod.ActiveModule is not { } module || module.Components.Count > 64)
            return Unavailable("invalid-native-knockback-pose-context");
        if (_knockbackPoseFrame != Session.FrameId) { _knockbackPoseFrame = Session.FrameId; _knockbackPoseAttempts = 0; }
        if (++_knockbackPoseAttempts > 8) return Unavailable("native-knockback-pose-work-limit");
        var live = Session.CaptureObservationLiveness(request.OwnerToken);
        if (live == null) return Unavailable("native-knockback-owner-retired");
        try
        {
            var component = module.Components.OfType<GenericKnockback>().FirstOrDefault(c => IntegrationMechanics.ComponentId(c) == request.ComponentId);
            var slot = World.Party.FindSlot(actor.InstanceID);
            if (component == null || slot < 0) return Unavailable("native-knockback-component-unavailable");
            var budget = new IntegrationRoutes.Budget(); var observations = new List<object>(); var sourceEntries = 0; var walls = 0;
            foreach (var pose in request.Poses)
            {
                if (pose == null || pose.Position is not { Length: 2 } || pose.Position.Any(p => !float.IsFinite(p) || Math.Abs(p) > 1_000_000)
                    || !float.IsFinite(pose.Facing) || Math.Abs(pose.Facing) > MathF.Tau) return Unavailable("invalid-native-knockback-pose");
                var position = new WPos(pose.Position[0], pose.Position[1]);
                if ((position - actor.Position).LengthSq() > 256 * 256 || !budget.Step()) return Unavailable("native-knockback-pose-budget");
                var projected = component.ProjectFromActorPose(slot, actor, position, pose.Facing.Radians(), out var sources);
                if (projected == null || !budget.Step()) return Unavailable("native-knockback-projection-incomplete");
                sourceEntries += sources.Length; walls += sources.Sum(s => s.SafeWalls.Length);
                if (sourceEntries > 128 || walls > 512) return Unavailable("native-knockback-publication-limit");
                var copy = actor.CopyForProjection(position, pose.Facing.Radians());
                var steps = new List<object>(); var published = new List<object>();
                for (var i = 0; i < sources.Length; ++i)
                {
                    if (!budget.Step()) return Unavailable("native-knockback-pose-budget");
                    var s = sources[i];
                    published.Add(new
                    {
                        sourceIndex = i, origin = P(s.Origin), s.Distance, direction = s.Direction.Rad, movementKind = s.Kind.ToString(), s.MinDistance,
                        activationTicks = T(s.Activation), activationKnown = s.Activation != default,
                        s.IgnoreImmunes, immuneAtActivation = component.IsImmune(slot, s.Activation),
                        participantApplies = module.ActorMatchesArenaProjectionLayer(copy, s.ArenaProjectionLayer, s.RestrictToArenaProjectionLayer),
                        shape = s.Shape == null ? null : IntegrationMechanics.Describe(s.Shape),
                        shapeAppliesAtStart = s.Shape == null || s.Shape.Check(position, s.Origin, s.Direction),
                        safeWalls = s.SafeWalls.Select(w => new[] { P(w.Vertex1), P(w.Vertex2) }).ToArray(),
                        sourceActorId = s.ActorID == 0 ? null : s.ActorID.ToString("X"), s.ArenaProjectionLayer, s.RestrictToArenaProjectionLayer
                    });
                }
                foreach (var step in projected)
                {
                    if (!budget.Step() || !float.IsFinite(step.From.X) || !float.IsFinite(step.From.Z)
                        || !float.IsFinite(step.To.X) || !float.IsFinite(step.To.Z)) return Unavailable("native-knockback-pose-budget-or-invalid-result");
                    steps.Add(new { from = P(step.From), to = P(step.To), activationTicks = T(step.Activation), step.SourceIndex,
                        step.ArenaProjectionLayer, step.RestrictToArenaProjectionLayer,
                        nativeDestinationUnsafe = component.DestinationUnsafe(slot, copy, step.To) });
                }
                observations.Add(new { pose.Position, pose.Facing, sources = published, movements = steps,
                    nativeStationaryDestinationUnsafe = component.DestinationUnsafe(slot, copy, position),
                    status = steps.Count == 0 ? "no-published-displacement" : "projected" });
            }
            if (!budget.Step() || !Session.Matches(request.Context) || !live()) return Unavailable("native-knockback-pose-retired");
            var response = IntegrationSession.Serialize(new
            {
                available = true, context = request.Context, request.ComponentId, actorId = actor.InstanceID.ToString("X"),
                worldTimeTicks = T(World.CurrentTime), sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
                component.StopAtWall, component.StopAfterWall, component.MaxCasts, observations, complete = true,
                nativeDestinationContext = "hypothetical native actor start position/facing; actual identity, height, status and immunity",
                status = "unknown", independentHazardsEvaluated = false, physicalObstaclesEvaluated = false,
                preparationReachabilityEvaluated = false, recoveryReachabilityEvaluated = false,
                meaning = "exact native custom-source and chained displacement geometry; empty movement never grants immunity; no landing, route, action or input safety approval"
            });
            return response.Length <= 262144 && budget.Step() ? response : Unavailable("native-knockback-response-budget");
        }
        catch (Exception error)
        {
            Service.LogVerbose($"[Integration] Native knockback pose failed: {error.GetType().Name}");
            return Unavailable("native-knockback-pose-failed");
        }
    }
}
