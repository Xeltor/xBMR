namespace BossMod.Integration;

// Captures the encounter goal prefix separately from rotation/follow combat goals.
// An integration may rank candidates, but cannot make a lower encounter score win.
internal sealed class IntegrationGoalSet(IntegrationSession session, Func<WPos, float>[] encounterGoals)
{
    internal readonly IntegrationSession.Context Context = session.CurrentContext;
    internal bool NavigationPublicationRequested => session.HasActiveOwners;
    internal sealed record Snapshot(Func<WPos, float>[] Encounter, Func<WPos, float>[] Combat,
        Func<WPos, float>[] MechanicPreference, Func<WPos, float>[] Presentation);

    internal Snapshot? Capture(Func<WPos, float>[] native)
    {
        if (encounterGoals.Length > native.Length || !encounterGoals.SequenceEqual(native.Take(encounterGoals.Length)))
            return null; // native producer replaced the goal list; do not guess its priority/ownership
        Func<WPos, float>[] Adapt(string category) => session.CaptureGoals(category)
            .Select(score => (Func<WPos, float>)(p => score(new(p.X, p.Z)))).ToArray();
        var combat = Adapt("combat");
        var utility = Adapt("party-utility");
        var mechanic = Adapt("mechanic-preference");
        var presentation = Adapt("presentation");
        if (combat.Length + utility.Length + mechanic.Length + presentation.Length == 0)
            return null;
        return new(encounterGoals, [.. native.Skip(encounterGoals.Length), .. combat, .. utility], mechanic, presentation);
    }

    internal static void Rasterize(Pathfinding.Map map, Snapshot snapshot)
    {
        // Each tier uses the same conservative four-corner rasterizer as native BMR.
        // Lexicographic ranks preserve encounter assignments first, combat/party value second,
        // mechanic preferences third and presentation last, without arbitrary epsilon weights.
        var count = checked(map.Width * map.Height);
        if (count > 262144)
            return; // bounded work; caller has already rasterized native goals
        var tiers = new[] { snapshot.Encounter, snapshot.Combat, snapshot.MechanicPreference, snapshot.Presentation };
        var values = new float[tiers.Length][];
        var scratch = new Pathfinding.Map();
        scratch.Init(map, map.Center);
        for (var tier = 0; tier < tiers.Length; ++tier)
        {
            Array.Clear(scratch.PixelPriority);
            Pathfinding.NavigationDecision.RasterizeGoalZones(scratch, tiers[tier]);
            values[tier] = scratch.PixelPriority.AsSpan(0, count).ToArray();
        }
        var cells = Enumerable.Range(0, count).Where(i => map.PixelMaxG[i] == float.MaxValue).ToArray();
        int Compare(int a, int b)
        {
            for (var tier = 0; tier < values.Length; ++tier)
            {
                var cmp = values[tier][a].CompareTo(values[tier][b]);
                if (cmp != 0)
                    return cmp;
            }
            return 0;
        }
        Array.Sort(cells, Compare);
        var rank = 0;
        for (var i = 0; i < cells.Length; ++i)
        {
            if (i > 0 && Compare(cells[i - 1], cells[i]) != 0)
                ++rank;
            map.PixelPriority[cells[i]] = rank;
        }
        map.MaxPriority = cells.Length == 0 ? float.MinValue : rank;
    }
}
