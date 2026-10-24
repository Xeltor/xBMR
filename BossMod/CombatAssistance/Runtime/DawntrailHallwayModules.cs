using System;
using System.Collections.Generic;

namespace BossMod.CombatAssistance.Runtime;

internal readonly record struct HallwayModule(uint ObjectId, string TypeName);

// Audited ordinary pack modules only. Names, hitbox size, and maturity are not classifiers.
internal static class DawntrailHallwayModules
{
    internal static readonly IReadOnlyList<HallwayModule> Entries = Array.AsReadOnly<HallwayModule>([
        new(0x41DA, "BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardAerostat1.D040VanguardAerostat1"),
        new(0x43D4, "BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardAerostat2.D040VanguardAerostat2"),
        new(0x447A, "BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardLeptocyon.D040VanguardLeptocyon"),
        new(0x4479, "BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardSentryR7.D040VanguardSentryR7"),
        new(0x41E2, "BossMod.Dawntrail.Dungeon.D05Origenics.D050OrigenicsAerostat.D050OrigenicsAerostat"),
        new(0x4612, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90ForestBat.D90ForestBat"),
        new(0x46A4, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90ForestWoolback.D90ForestWoolback"),
        new(0x4652, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90RottenResearcher.D90RottenResearcher"),
        new(0x461B, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90SprightlyClayGolem.D90SprightlyClayGolem"),
        new(0x4619, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90SprightlyPhoebad.D90SprightlyPhoebad"),
        new(0x4616, "BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90StationSpecter.D90StationSpecter"),
    ]);
}

