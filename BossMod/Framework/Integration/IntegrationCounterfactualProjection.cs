using BossMod.Components;
using System.Globalization;

namespace BossMod.Integration;

// Native hypothetical geometry only. Never clones/mutates actors or hints and
// never converts missing independent-hazard/recovery evidence into safety.
internal static class IntegrationCounterfactualProjection
{
    internal const int MaxPoses = 4;
    internal sealed record Pose(float[] Position, float Facing);
    internal sealed record Step(float[] From, float[] To, float? Facing, bool NativeDestinationUnsafe,
        string NativeDestinationContext, string? ActivationTicks, string? EndTicks, int? SourceIndex,
        int? ArenaProjectionLayer, bool? RestrictToArenaProjectionLayer);
    internal sealed record Result(float[] Position, float Facing, string Status, Step[] Movements);
    private static string? T(DateTime value) => value == default ? null : value.Ticks.ToString(CultureInfo.InvariantCulture);
    private static float[] P(WPos value) => [value.X, value.Z];
    internal static bool Supports(BossComponent component) => component is GenericForcedMarch
        || component is GenericKnockback { SupportsCounterfactualProjection: true };

    internal static Result? Project(BossComponent component, Actor actor, int slot, Pose pose, IntegrationRoutes.Budget budget)
    {
        if (slot < 0 || slot >= PartyState.MaxAllies || pose.Position is not { Length: 2 }
            || !float.IsFinite(actor.Position.X) || !float.IsFinite(actor.Position.Z) || !float.IsFinite(actor.PosRot.Y)
            || !float.IsFinite(actor.Rotation.Rad) || !float.IsFinite(actor.HitboxRadius) || actor.HitboxRadius < 0
            || pose.Position.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000)
            || !float.IsFinite(pose.Facing) || Math.Abs(pose.Facing) > 2 * MathF.PI || !budget.Step()) return null;
        var position = new WPos(pose.Position[0], pose.Position[1]);
        if ((position - actor.Position).LengthSq() > 256 * 256) return null;
        var steps = new List<Step>();
        if (component is GenericKnockback knockback)
        {
            var projected = knockback.ProjectFromPosition(slot, actor, position);
            if (projected == null || projected.Count > 32) return null;
            var context = knockback.GetType().GetMethod(nameof(GenericKnockback.DestinationUnsafe), [typeof(int), typeof(Actor), typeof(WPos)])?.DeclaringType == typeof(GenericKnockback)
                ? "native-base-arena-rule" : "native-override-with-actual-actor-context";
            foreach (var move in projected)
            {
                if (!budget.Step()) return null;
                steps.Add(new(P(move.From), P(move.To), null, knockback.DestinationUnsafe(slot, actor, move.To), context,
                    T(move.Activation), null, move.SourceIndex, move.ArenaProjectionLayer, move.RestrictToArenaProjectionLayer));
            }
        }
        else if (component is GenericForcedMarch march)
        {
            var projected = march.ProjectFromPose(actor, position, new(pose.Facing));
            if (projected == null || projected.Count > 33) return null;
            var context = march.GetType().GetMethod(nameof(GenericForcedMarch.DestinationUnsafe), [typeof(int), typeof(Actor), typeof(WPos)])?.DeclaringType == typeof(GenericForcedMarch)
                ? "native-base-arena-rule" : "native-override-with-actual-actor-context";
            foreach (var move in projected)
            {
                if (!budget.Step()) return null;
                steps.Add(new(P(move.From), P(move.To), move.Direction.Rad, march.DestinationUnsafe(slot, actor, move.To), context,
                    T(move.Activation), T(move.End), move.PendingIndex, march.ArenaProjectionLayer, march.RestrictToArenaProjectionLayer));
            }
        }
        else return null;
        if (!budget.Step() || steps.Any(s => s.From.Concat(s.To).Any(v => !float.IsFinite(v))
            || s.Facing is { } facing && !float.IsFinite(facing))) return null;
        return new([.. pose.Position], pose.Facing, steps.Count == 0 ? "no-published-displacement" : "projected", [.. steps]);
    }
}
