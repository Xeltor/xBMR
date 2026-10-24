using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void Formation()
    {
        var memory = new NativeFormationPreference();
        var now = DateTime.UtcNow;
        var id = ((ushort)1, 1ul, Class.DRG, 20ul);
        var position = new WPos(0, -5);
        Check(memory.Observe(id, [1, 2], now, position, default, default, true, false) == null,
            "formation does not request an initial cosmetic walk");
        memory.Observe(id, [1, 2], now.AddSeconds(0.6), position, default, default, true, false);
        var during = memory.Observe(id, [1, 2], now.AddSeconds(0.8), position, default, default, false, true);
        Check(during?.Anchor == position && during.Value(position) > during.Value(new(0, 5)),
            "remembered useful formation participates before the first mechanic move");
        var after = memory.Observe(id, [1, 2], now.AddSeconds(1), new(5, 0), default, default, false, false);
        Check(after?.Anchor == position, "mechanic exit does not overwrite the original useful formation");
        var changed = memory.Observe(id, [1, 3], now.AddSeconds(1.2), position, default, default, true, true);
        Check(changed?.Anchor == null && changed?.Lane != null, "party changes reset memory and use deterministic lanes");
        memory.Reset();
        Check(memory.Observe(id, [1, 2], now.AddSeconds(1.3), position, default, default, true, false) == null,
            "manual handoff releases formation preference");
        var first = new NativeFormationPreference().Observe(id, [1, 2], now, position, default, default, false, true);
        var second = new NativeFormationPreference().Observe((id.Item1, 2ul, id.Item3, id.Item4), [1, 2], now, position, default, default, false, true);
        Check(first?.Lane != second?.Lane, "party members receive complementary stable lanes without random variation");
    }
}
