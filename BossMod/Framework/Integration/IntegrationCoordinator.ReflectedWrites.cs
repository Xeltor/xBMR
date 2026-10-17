using System.Reflection;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    internal sealed record FrameWriteRequest(IntegrationSession.Context Context, string OwnerToken);
    internal sealed record IngressWriteRequest(IntegrationSession.Context Context, string OwnerToken,
        string ControllerDecisionId, float[]? Destination);

    internal string ApplyFateFrame(string json)
    {
        var request = IntegrationSession.Parse<FrameWriteRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready"
            || !Session.IsOwnerActive(request.OwnerToken) || !Session.CanContribute || !ControlsAllowed()
            || World.Party.Player() is not { InCombat: true } actor
            || bossmod.ActiveModule is not OpenWorldFate module || module.StateMachine.ActivePhase == null
            || !ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds) || hints.PathfindMapArenaProjectionLayer != null)
            return Unavailable("invalid-owned-fate-frame-source");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (module.GetType().GetMethod("UpdateModule", flags)?.DeclaringType != typeof(SimpleBossModule)
            || module.GetType().GetMethod("CalculateModuleAIHints", flags)?.DeclaringType != typeof(SimpleBossModule))
            return Unavailable("authored-frame-retained");
        var original = hints.PathfindMapCenter;
        var (entry, bitmap) = module.Obstacles.Find(new(original.X, actor.PosRot.Y, original.Z));
        if (entry == null || bitmap == null || !ReferenceEquals(bitmap, hints.PathfindMapObstacles.Bitmap)
            || bitmap.PixelSize != hints.PathfindMapBounds.MapResolution)
            return Unavailable("matching-terrain-unavailable");
        var alignment = AlignOwnedFateFrame(original, entry.Origin, bitmap.PixelSize, hints.PathfindMapBounds.Radius);
        if (alignment == null) return Unavailable("invalid-terrain-frame");
        var (center, x, z, half) = alignment.Value;
        hints.PathfindMapCenter = center;
        hints.PathfindMapObstacles = new(bitmap, new(x - half, z - half, x + half, z + half));
        return IntegrationSession.Serialize(new { accepted = true, context = request.Context, originalCenter = P(original),
            center = P(center), obstacleOrigin = P(entry.Origin), resolution = bitmap.PixelSize,
            window = new { left = x - half, top = z - half, right = x + half, bottom = z + half }, geometryOnly = true });
    }

    // Exact prior consumer arithmetic, including truncation rather than rounding.
    internal static (WPos Center, int X, int Z, int Half)? AlignOwnedFateFrame(WPos center, WPos origin, float resolution, float radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Z) || !float.IsFinite(origin.X) || !float.IsFinite(origin.Z)
            || !float.IsFinite(resolution) || resolution <= 0 || !float.IsFinite(radius) || radius <= 0) return null;
        var local = (center - origin) / resolution; var half = radius / resolution;
        if (!float.IsFinite(local.X) || !float.IsFinite(local.Z) || Math.Abs(local.X) > 1_000_000 || Math.Abs(local.Z) > 1_000_000
            || !float.IsFinite(half) || half > 4096) return null;
        var x = (int)local.X; var z = (int)local.Z;
        return (origin + new WDir(x, z) * resolution, x, z, (int)half);
    }

    private Vector3? SuggestedIngressDestination()
    {
        if (bossmod.ActiveModule is not { } module || World.Party.Player() is not { } actor || module.Arena.InBounds(actor.Position)) return null;
        var boundary = module.Arena.ClampToBounds(actor.Position); WPos? best = null; var distance = float.MaxValue;
        for (var i = 0; i < 8; ++i)
        {
            var angle = i * MathF.PI / 4; var candidate = boundary + new WDir(MathF.Cos(angle), MathF.Sin(angle)) * 0.75f;
            var d = (candidate - actor.Position).LengthSq();
            if (module.Arena.InBounds(candidate) && d < distance) { best = candidate; distance = d; }
        }
        return best?.ToVec3(actor.PosRot.Y);
    }

    internal string ApplyDutyIngress(string json)
    {
        var request = IntegrationSession.Parse<IngressWriteRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "movement-proposed"
            || !Session.IsOwnerActive(request.OwnerToken) || !Session.CanContribute
            || request.Destination != null && (request.Destination.Length != 3 || request.Destination.Any(v => !float.IsFinite(v))))
            return Unavailable("invalid-owned-ingress-request");
        var actor = World.Party.Player(); var source = hints.IntegrationControllerDecision; var module = bossmod.ActiveModule;
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "movement-proposed"
            || !Session.IsOwnerActive(request.OwnerToken) || !Session.CanContribute || !ControlsAllowed() || actor == null
            || _encounterMovement != null || module?.StateMachine.ActivePhase == null
            || module.Info.Category is not (BossModuleInfo.Category.Dungeon or BossModuleInfo.Category.VariantCriterion)
            || module.Arena.InBounds(actor.Position) || source == null || source.Id != request.ControllerDecisionId
            || source.Owner != "bmr-normal-movement" || source.Reason != "native-ingress-without-route-proof"
            || !source.Matches(Session.InstanceGeneration, Session.FrameId, actor.InstanceID, hints.ForcedMovement))
            return Unavailable("invalid-owned-ingress-source");
        Vector3? destination = null; var reason = "consumer-withheld";
        if (request.Destination is { Length: 3 } p && p.All(float.IsFinite)
            && actor.CastInfo == null && !amex.MoveMightInterruptCast && hints.MaxCastTime == float.MaxValue
            && hints.ForbiddenDirections.Count == 0 && hints.ImminentSpecialMode == default)
        {
            var target = new Vector3(p[0], p[1], p[2]);
            if (Vector3.Distance(actor.PosRot.XYZ(), target) <= 40 && Math.Abs(target.Y - actor.PosRot.Y) < 0.01f
                && module.Arena.InBounds(new(target.X, target.Z)) && SuggestedIngressDestination() is { } suggested
                && Vector3.DistanceSquared(target, suggested) < 0.01f
                && ActionPredicate.IsDashSafe(actor.Position, new(target.X, target.Z), hints) && IngressClearance(actor, target))
            { destination = target; reason = "validated-original-entry"; }
            else reason = "entry-clearance-unverified";
        }
        hints.ForcedMovement = destination is { } entry ? entry - actor.PosRot.XYZ() : Vector3.Zero;
        return IntegrationSession.Serialize(new { accepted = true, context = request.Context, request.ControllerDecisionId,
            applied = destination != null, withheld = destination == null, status = destination == null ? "withheld" : "applied",
            reason, destination = V(destination) });
    }

    private unsafe bool IngressClearance(Actor actor, Vector3 destination)
    {
        var delta = destination - actor.PosRot.XYZ();
        if (ObservePhysicalStep(actor, delta).Status != "clear") return false;
        var framework = Framework.Instance(); var collision = framework == null ? null : framework->BGCollisionModule;
        if (collision == null || collision->SceneManager == null) return false;
        var sphere = collision->SceneManager->StreamingSphere;
        if (Vector3.Distance(destination, new(sphere.X, sphere.Y, sphere.Z)) + actor.HitboxRadius >= sphere.W) return false;
        var length = delta.Length(); if (length < 0.01f) return false;
        foreach (var height in new[] { 0.25f, 1.5f })
            if (BGCollisionModule.RaycastMaterialFilter(actor.PosRot.XYZ() + Vector3.UnitY * height, delta / length, out _, length)) return false;
        return true;
    }
}
