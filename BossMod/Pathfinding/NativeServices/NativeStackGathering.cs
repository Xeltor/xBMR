namespace BossMod.Pathfinding;

// A stack is a visible gathering instruction, not merely permission to touch
// its outer edge. This native goal is evaluated before pathfinding; authored
// exclusions, capacity, projection and physical safety still choose legal space.
internal static class NativeStackGathering
{
    internal static float InteriorRadius(float stackRadius) => Math.Min(2f, stackRadius * 0.5f);

    internal static void AddGoal(AIHints hints, IEnumerable<WPos> eligiblePeers, float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0) return;
        var peers = eligiblePeers.Where(p => float.IsFinite(p.X) && float.IsFinite(p.Z)).ToArray();
        if (peers.Length == 0) return;
        var squared = radius * radius;
        // A flat interior avoids chasing the exact centre every time a party
        // member takes a small step. Among equal gathering spots, native combat
        // goals and the existing continuity/spacing preferences still apply.
        hints.GoalZones.Add(position =>
        {
            var nearby = 0;
            foreach (var peer in peers)
                if ((position - peer).LengthSq() <= squared) ++nearby;
            return nearby * 1000f;
        });
    }
}
