using BossMod;
using BossMod.Components;

static partial class Regression
{
    private static void SerpentLabyrinth()
    {
        const string prefix = "Dawntrail.Dungeon.D07TenderValley.D074GreatestSerpentOfTural.";
        // Synthetic checks of the existing nominal module contract. These do
        // not simulate the game's arrow transport or its collision eligibility.
        (uint State, WPos Entrance, WPos Cleanse)[] patterns =
        [
            (0x01000080, new(-124, -552), new(-140, -564)),
            (0x04000200, new(-128, -560), new(-120, -544)),
            (0x10000800, new(-132, -548), new(-120, -564)),
            (0x00020001, new(-136, -556), new(-140, -544))
        ];
        foreach (var pattern in patterns)
        {
            var module = Module(prefix + "D074GreatestSerpentOfTural", new(-130, -554));
            module.Arena.Bounds = new ArenaBoundsSquare(12);
            var component = Component<GenericAOEs>(module, prefix + "GreatestLabyrinth");
            var player = Actor(100, 0, module.Arena.Center, true);
            component.OnMapEffect(1, pattern.State);
            var hints = new AIHints();
            component.AddAIHints(0, player, default, hints);
            Check(hints.ForbiddenZones.Count == 2, "each maze pattern publishes its terrain and required-square constraints");
            Check(hints.ForbiddenZones[0].shapeDistance.Distance(player.Position) > 0,
                "maze center remains a legal approach region before the required-square deadline");
            Check(hints.ForbiddenZones[1].shapeDistance.Distance(player.Position) == 0
                && hints.ForbiddenZones[1].activation == Now.AddSeconds(10),
                "maze center cannot satisfy the required-square constraint");
            foreach (var point in new[] { pattern.Entrance, pattern.Cleanse })
                Check(hints.ForbiddenZones.All(z => z.shapeDistance.Distance(point) > 0),
                    "each pattern retains its nominal entrance and cleansing square");
            foreach (var reset in new uint[] { 0x00100004, 0x00200004, 0x00400004, 0x00080004 })
            {
                component.OnMapEffect(1, reset);
                Check(component.ActiveAOEs(0, player).IsEmpty, "maze resolution removes its guidance for each reset variant");
            }
        }
    }
}
