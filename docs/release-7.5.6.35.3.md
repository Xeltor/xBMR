# xBMR 7.5.6.35.3

This release keeps upstream base `7.5.6.35` and increments the fork revision to
`3`. Dalamud install version: `7.5.6.3503`.

- Add partial Variant Aloalo Island support for Quaqua and Ketuduke, including
  native arena constraints, predicted hazards and explicit route coverage hints.
  See [Quaqua](quaqua-variant.md) and [Ketuduke](ketuduke-variant.md) for supported
  mechanics and remaining geometry, route and timing validation.
- Correct Al Zahbi wave-one activation, Leaping Cleave knockback constraints,
  Dance to Dust tracking and interrupt hints. See the
  [Al Zahbi evidence](al-zahbi-corrections.md).
- Correct Promathia's False Genesis platform preparation and floor transitions,
  Empty Seed knockback constraints, ring deadlines, tower assignments and puddle
  tracking. See [platform corrections](promathia-false-genesis.md) and
  [rings, towers and puddles](promathia-rings-towers-puddles.md).
- Correct Shinryu's Twilight floor avoidance and hazard retirement while
  preserving the existing arena geometry. See the
  [Shinryu evidence](shinryu-floor-corrections.md).
- Include encounter regression harnesses and document arena-first development,
  early warnings, timing requirements and the local build workflow.

The BossModReborn assembly name, settings paths and IPC remain compatible.
Original licence and third-party notices are retained.

Release validation: the pinned .NET 10.0.401 Release build passed with six
existing obsolete-gauge warnings and no errors. All five Python version tests,
1,251 native module assertions, 63 Ketuduke assertions and 63 Quaqua assertions
passed against the rebuilt development DLL.

Build and synthetic regression results establish source/build validation only.
The encounter reports describe earlier replay evidence and its limitations;
this release does not establish physical arena calibration, server snapshot
timing, complete route coverage or live xBMR/XCAI gameplay acceptance. Quaqua
remains WIP, and both new Aloalo modules display their partial coverage in game.
