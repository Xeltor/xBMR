namespace BossMod.Pathfinding;

// A stack is a visible gathering instruction, not merely permission to touch
// its outer edge. This native goal is evaluated before pathfinding; authored
// exclusions, capacity, projection and physical safety still choose legal space.
internal static class NativeStackGathering
{
    // Keep gathering identifiable at the native capture boundary. Its score is
    // still only a preference; an executable route needs independent proof.
    internal sealed class Goal(WPos[] peers, float squaredRadius, WPos? combatAnchor, float combatRadius)
    {
        internal float Evaluate(WPos position)
        {
            var nearby = 0;
            foreach (var peer in peers)
                if ((position - peer).LengthSq() <= squaredRadius) ++nearby;
            // With equally sized groups, a carrier brings the marker toward
            // boss contact rather than taking it to an isolated ranged peer.
            // The contact plateau preserves useful space around the hitbox.
            var contact = nearby > 0 && combatAnchor is { } anchor
                ? .1f / (1f + Math.Max(0, (position - anchor).Length() - combatRadius)) : 0;
            return nearby * 1000f + contact;
        }
    }

    internal static float InteriorRadius(float stackRadius) => Math.Min(2f, stackRadius * 0.5f);

    internal static void AddGoal(AIHints hints, IEnumerable<WPos> eligiblePeers, float radius,
        WPos? combatAnchor = null, float combatRadius = 0)
    {
        if (!float.IsFinite(radius) || radius <= 0) return;
        var peers = eligiblePeers.Where(p => float.IsFinite(p.X) && float.IsFinite(p.Z)).ToArray();
        if (peers.Length == 0) return;
        if (combatAnchor is { } anchor && (!float.IsFinite(anchor.X) || !float.IsFinite(anchor.Z))) combatAnchor = null;
        if (!float.IsFinite(combatRadius) || combatRadius < 0) combatRadius = 0;
        // A flat interior avoids chasing the exact centre every time a party
        // member takes a small step. Among equal gathering spots, native combat
        // goals and the existing continuity/spacing preferences still apply.
        hints.GoalZones.Add(new Goal(peers, radius * radius, combatAnchor, combatRadius).Evaluate);
    }
}
