using BossMod.Components;

namespace BossMod.Pathfinding;

// Native hypothetical geometry only. Never clones/mutates actors or hints and
// never converts missing independent-hazard/recovery evidence into safety.
internal static class NativeDisplacementProjection
{
    internal sealed record Pose(WPos Position, Angle Facing);
    internal sealed record Step(WPos From, WPos To, Angle? Facing, bool NativeDestinationUnsafe,
        string NativeDestinationContext, DateTime? Activation, DateTime? End, int? SourceIndex,
        int? ArenaProjectionLayer, bool? RestrictToArenaProjectionLayer);
    internal sealed record Result(WPos Position, Angle Facing, string Status, ImmutableArray<Step> Movements);
    private static DateTime? Time(DateTime value) => value == default ? null : value;
    private static bool Finite(WPos value) => float.IsFinite(value.X) && float.IsFinite(value.Z);
    internal static bool Supports(BossComponent component) => component is GenericForcedMarch
        || component is GenericKnockback { SupportsCounterfactualProjection: true };

    internal static Result? Project(BossComponent component, Actor actor, int slot, Pose pose, NativeRouteValidation.Budget budget)
    {
        if (slot < 0 || slot >= PartyState.MaxAllies || pose == null
            || !float.IsFinite(actor.Position.X) || !float.IsFinite(actor.Position.Z) || !float.IsFinite(actor.PosRot.Y)
            || !float.IsFinite(actor.Rotation.Rad) || !float.IsFinite(actor.HitboxRadius) || actor.HitboxRadius < 0
            || !Finite(pose.Position)
            || !float.IsFinite(pose.Facing.Rad) || !budget.Step()) return null;
        var position = pose.Position;
        var steps = new List<Step>();
        if (component is GenericKnockback knockback)
        {
            var projected = knockback.ProjectFromPosition(slot, actor, position);
            if (projected == null) return null;
            var context = knockback.GetType().GetMethod(nameof(GenericKnockback.DestinationUnsafe), [typeof(int), typeof(Actor), typeof(WPos)])?.DeclaringType == typeof(GenericKnockback)
                ? "native-base-arena-rule" : "native-override-with-actual-actor-context";
            foreach (var move in projected)
            {
                if (!budget.Step()) return null;
                steps.Add(new(move.From, move.To, null, knockback.DestinationUnsafe(slot, actor, move.To), context,
                    Time(move.Activation), null, move.SourceIndex, move.ArenaProjectionLayer, move.RestrictToArenaProjectionLayer));
            }
        }
        else if (component is GenericForcedMarch march)
        {
            var projected = march.ProjectFromPose(actor, position, pose.Facing);
            if (projected == null) return null;
            var context = march.GetType().GetMethod(nameof(GenericForcedMarch.DestinationUnsafe), [typeof(int), typeof(Actor), typeof(WPos)])?.DeclaringType == typeof(GenericForcedMarch)
                ? "native-base-arena-rule" : "native-override-with-actual-actor-context";
            foreach (var move in projected)
            {
                if (!budget.Step()) return null;
                steps.Add(new(move.From, move.To, move.Direction, march.DestinationUnsafe(slot, actor, move.To), context,
                    Time(move.Activation), Time(move.End), move.PendingIndex, march.ArenaProjectionLayer, march.RestrictToArenaProjectionLayer));
            }
        }
        else return null;
        if (!budget.Step() || steps.Any(s => !Finite(s.From) || !Finite(s.To)
            || s.Facing is { } facing && !float.IsFinite(facing.Rad))) return null;
        return new(pose.Position, pose.Facing, steps.Count == 0 ? "no-published-displacement" : "projected", [.. steps]);
    }
}
