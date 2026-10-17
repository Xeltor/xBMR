# Offline inspection helpers and command failures

All encounter regression launchers and `scripts/EncounterInspection` use
`tests/Support/ConsoleRunner.cs`. Successful commands return 0. Managed failures
print the exception and its stack to stderr and return 1, including failures in
argument parsing, dependency loading, and reflected checks. Reflection invocation
wrappers are unwrapped so the original failing check appears first. A failed
assertion still fails the command; this does not suppress system crash reporting
or catch native crashes, stack overflows, or runtime fail-fast termination.

## Missing sheet references

A missing referenced quest row previously raised an unhandled
`TargetInvocationException` wrapping an `ArgumentOutOfRangeException` from
`Lumina.Excel.ExcelSheet<Quest>.GetRow`: parameter `rowId`, actual value `854246`.

The retained inspector uses `TryGetRow` for sheet references. In the installed
data, territory 232 references QuestBattle 13 and quest ID 854246, which is absent
from the Quest sheet. The output preserves that raw ID, with `QuestFound: false`
and `Quest: null`. Missing territory input fails the command. Missing referenced
battle/quest rows are explicit metadata gaps, not silently invented values.
The meaning of the unresolved raw quest reference is not established here.

## Inspect an inventory

Build the helper with the pinned SDK. This only builds the standalone helper;
it does not rebuild or reload the plugin. It reads the existing plugin DLL and
installed game sheets. Inventory JSON is an array with `zone` (territory ID),
`enemy_oids` (an array), and `complete` (recording completeness) on each entry.

```sh
dotnet build \
  scripts/EncounterInspection/EncounterInspection.csproj -c Release
dotnet \
  scripts/EncounterInspection/bin/Release/net10.0/EncounterInspection.dll \
  BossMod/bin/Release/BossModReborn.dll \
  ${DALAMUD_HOME} \
  "$FFXIV_GAME" \
  tests/EncounterInspectionRegression/inventory.json
```

Output is JSON Lines, one row per territory. `Complete` describes the supplied
recordings, not module coverage or correctness. Registered modules and maturity
are inspection metadata and do not prove mechanic coverage or live safety.

## Regression checks

`python -m unittest discover -s tests` builds a dependency-free subprocess fixture
and checks success, assertion failure, missing dependencies, and nested reflection
failure using the recorded row ID. It requires the pinned compatible SDK; set
`XBMR_DOTNET` to the compatible SDK executable if it is not on PATH.

Set `XBMR_GAME_DIRECTORY` to the installed game directory to also run the real
territory-232/missing-Quest-row regression. That check requires a built plugin DLL
and compatible Dalamud assemblies (`DALAMUD_HOME`, defaulting to the local dev
directory). It intentionally detects installed-sheet changes that invalidate the
recorded fixture. Offline command/build success remains separate from in-game
acceptance.

## Validation on 2026-10-17

- All 12 encounter/compatibility regression launchers built; missing arguments
  returned 1 with a diagnostic, without SIGABRT.
- Eight Python tests passed with the installed-sheet regression enabled.
- Proof of Might (15 checks), Questionable compatibility (74 assertions), and
  Tyrant (2,726 assertions) passed using the existing plugin build.
- The retained inspector completed the original inventory of 14 territories;
  the missing quest reference remained explicit. Invalid arguments and missing
  files returned 1. The original temporary inspector was also given the shared
  exception boundary, preserving its three-argument command line.
- This historical checkpoint validated the helpers against an existing plugin
  assembly. It did not validate a full current-tree plugin rebuild or live
  gameplay.
