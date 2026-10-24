using BossMod.CombatAssistance.Support;

internal static class PartyRangedMitigationTests
{
    public static void Run()
    {
        var now = DateTime.UtcNow;
        var hit = now.AddSeconds(5);
        foreach (var status in new uint[] { 1826, 1934, 1951, 2177 })
        {
            PartyMitigationEffect[] effects = [new(1, 10, status, 6), new(2, 10, status, 6)];
            Checks.Check(PartyRangedMitigationPolicy.Covered(now, hit, [1, 2], effects),
                "each non-stacking ranged defense covers the same party hit");
            Checks.Check(!PartyRangedMitigationPolicy.Covered(now, hit, [1, 2, 3], effects),
                "an uncovered party member retains native mitigation");
            Checks.Check(!PartyRangedMitigationPolicy.Covered(now, now.AddSeconds(6), [1, 2], effects),
                "protection must survive the hit with a timing margin");
        }
        foreach (var effect in new PartyMitigationEffect[]
        {
            new(1, 10, 1193, 10), new(1, 10, 1951, float.NaN), new(1, 10, 1951, float.PositiveInfinity),
            new(1, 10, 1951, -10), new(1, 0, 1951, 10), new(2, 10, 1951, 10),
        })
            Checks.Check(!PartyRangedMitigationPolicy.Covered(now, hit, [1], [effect]),
                "complementary debuffs, invalid durations and wrong recipients cannot spend ranged protection");
        Checks.Check(!PartyRangedMitigationPolicy.Covered(now, hit, [], []), "empty coverage fails open");
        Checks.Check(!PartyRangedMitigationPolicy.Covered(now, now, [1], [new(1, 10, 1951, 10)]),
            "expired forecast cannot preserve a cooldown");
        foreach (var (job, action) in new (uint, uint)[] { (23, 7405), (31, 16889), (38, 16012) })
            Checks.Check(PartyRangedMitigationPolicy.ActionForJob(job).Action == action,
                "ranged jobs use their verified native party defense");
        Checks.Check(PartyRangedMitigationPolicy.ActionForJob(19).Action == 0,
            "tank mitigation remains a separate complementary family");
        Checks.Check(!PartyRangedMitigationPolicy.Unlocked(23, 61) && PartyRangedMitigationPolicy.Unlocked(23, 62) &&
            !PartyRangedMitigationPolicy.Unlocked(31, 55) && PartyRangedMitigationPolicy.Unlocked(38, 56),
            "level-synced peers must have unlocked their party defense");
    }
}
