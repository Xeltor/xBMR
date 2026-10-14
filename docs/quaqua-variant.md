# Quaqua — partial Variant Aloalo Island support

Implemented on 2026-10-14 in `V3Aloalo/V31Quaqua`. This is Variant duty 961,
territory 1176, boss OID `0x40BA`, NameID 12527. It is **WIP**, not verified or
complete encounter support. The default minimum maturity is Contributed, so
testing this module requires enabling WIP modules in xBMR's module settings.
No live settings were changed.

## Scope and arena

The implementation covers the observed route 1 thunder pull: Made Magic,
three axes, three donuts, the later mixed five-sphere wave, Rout, Violet Storm,
and Cloud to Ground. A global hint identifies partial coverage. Hammers,
fair-skies Scalding Waves, middle-path rotating crosses/poison pools, and
right-path orbs/statue gazes remain unsupported. Only the observed Rout order
(edges then middle) is covered by recording evidence; middle-first is untested.
The code follows helper-preview order rather than hardcoding either route.

Arena bounds are established immediately and feed native AI pathfinding, not
just drawing. The center is captured once from the primary actor when the module
is constructed; in the reviewed recording this is `(-538, 94)`. It does not follow
the boss's dashes. The circle radius of 20 is an upstream **reference estimate**,
not an independently calibrated safe boundary. Player trajectories were not used
to define a safe perimeter. Holes, the exact DoT boundary, alternate-path arena
geometry, and creation of the module while the boss is away from center remain
unverified. No phase-dependent boundary change is implemented.

Map effect index 60 changes to `0x00020001` at sequence 57969 after the first
Made Magic, and returns to `0x00080004` at 76489 when the boss dies. These are
observed environment states, not measurements of the ring's dimensions.
The fixed reference circle persists throughout the pull; native death-phase
exit clears components, and a new module starts with empty mechanic state.

## Evidence and mechanic contracts

The user's written mechanics supplied branch meanings. The supplemental
[upstream example](https://github.com/awgil/ffxiv_bossmod/blob/fff7f6e98541fcd6b23d7c2ccbac88e1fa4c9c75/BossMod.Modules/Endwalker/Variant/V03Aloalo/V031Quaqua.cs)
is BSD-3-Clause source at `fff7f6e98541fcd6b23d7c2ccbac88e1fa4c9c75`, with
contributor attribution retained. Its IDs and shapes were reviewed against
the current fork API and recorded events; its timers and helper matching were
not treated as authoritative. The module was implemented against this fork's
span-based AOE API and generated factories.

One original was used for authoring and regression, with no independent holdout:

- Recording: `e53c95eb53aeb608fd8faf27f6f0d0e96e1625a2f410d57aeb419b2d1931a939`.
- Original SHA-256: `fe00ca7749705151bfc05c0dd357ad76b8ffe23054ad173c65b3f7fcb5a0dc31`.
- Retained revision: `38ba4d245d4848180c06638143a23e1922e546206a31c64a7df5b0d77e6c9a99`.
- Writer xBMR `7.5.6.35`, game `2026.10.06.0000.0000`, archive parser MVID
  `52cc4dc7-8b32-46be-8b49-2f03899b31c4`.
- Complete schema-3 projection: 35,124 selected observations, 80,117 decoded
  raw entries. Reviewed inventory domain is inclusive sequences 52567–78000.
  All five inventory pages were exhausted with a fixed revision and filters.

| Mechanic | Cited sequences | Prediction, resolution and limits |
| --- | --- | --- |
| Made Magic `35732` | starts 57215, 59196, 72323; impacts 57895, 59784, 72808 | Native cast raidwide prediction. One prediction per cast includes the player and companion slot 24. |
| Axes/donuts `35722/35723` | tethers 58101–58103, 60012–60014, 73017–73022; casts 59100–59102, 61155–61157, 74113–74117; impacts 59407–59411, 61579–61583, 74582–74591 | Circle R14; donut inner R5, outer R18. Tethers 256/257 forecast at visual cast finish +4.8s; actual sphere casts replace the prediction with their own location/deadline. Each sphere retires on its own hit, cancellation or destruction. |
| Rout `35729/35730` | primary start 63435; preview starts 63455, 63647, 63731, 63859; impacts 64065, 64304, 64627, 64999 | Helper `35731` supplies ordered origins/facings. Its earlier finish does not retire a damaging dash. Use the damaging action's 45-by-16 reference rectangle, not the preview's 40-by-10 fields. First deadline is the primary cast finish; later forecasts use 1.6s cadence, corrected after each actual dash. Only the next lane is risky for AI; later lanes remain visual previews. Timing/order alternatives remain unverified. |
| Violet Storm `35733` | start 66428, impact 67073, end 67074 | Cast-position cone R32, half-angle 60 degrees; native cast lifecycle. |
| Cloud to Ground `35740/35741` | first starts 68932–68933, 69319–69320; first hits 69412–69414, 69791–69793; final hits 71495–71497 | Four independent R6 lines, seven pulses per line, 6-yalm advance. First-to-second delay about 3.1s; later delay about 2.1s. Actual casts resynchronize origins/deadlines. Match a continuation to its predicted position, because helpers 35/36 finish lines originally started by 33/34. Do not remove a line merely because its next center lies outside the arena. |

The first sphere tethers arrive **before** the boss's visual-cast callback; the
fifth mixed-wave tether arrives **after** it. The visual clears its tethers before
the spheres cast. Initialization therefore scans already-present outgoing orb
tethers, accepts later ones, and retains predictions across normal untethering.
An orb's cast is authoritative over an earlier tether forecast. Duplicate
effects cannot consume another orb or dash, and callback state is keyed by
actor object/cast identity rather than a reused numeric alias alone.

The complete exaflare wave has 28 hits. Helpers 33/34 each deliver six, helpers
35/36 each deliver eight: the latter pair is reused for the final two hits of the
former pair's lines. Treating each caster as one immutable line leaves stale
hazards. At the second Howl, the boss dies before any new first-hit impact;
this is a truncated mechanic sequence, not evidence of a different pulse count.

Action/Omen evidence in the archive is pinned to the older game
`2026.09.15.0000.0000`. The regression executable separately reads the currently
installed `2026.10.06.0000.0000` Action rows and confirms the eight relevant
cast-type/range/width tuples. Donut inner radius and cone angle also have the
upstream/written reference basis. These remain reference geometry, not measured
server hit boundaries or hitbox-intersection semantics.

## Validation

- Full current-tree Release build: SDK 10.0.401, compatible Dalamud API 15
  development dependencies; zero errors and six existing obsolete-gauge warnings.
- `tests/QuaquaRegression`: 63 assertions covering real generated registration,
  phase activation, arena-to-AI wiring, fixed center, fresh-pull cleanup, tether
  ordering, duplicate starts/effects, individual retirement, cancellation,
  helper handoff, shape boundaries and mixed-wave safe pockets. These callback
  fixtures are synthetic; expected pockets use the written rules/reference
  dimensions, not candidate-generated ground truth.
- Closed-original native replay: 89 available samples across two bounded
  requests. Zero module errors, missing samples, skipped operation kinds or
  unsupported output kinds. Offline game assets exactly match the recording.
  The original file's SHA-256 was checked before and after replay.
- All 28 completed-wave exaflare impacts have an imminent prediction at the
  corresponding recorded cast location (within 0.05 yalms) and cast-finish
  observation (within 0.1s; observed residual 0.00037–0.02163s). Sphere waves
  retire individually, all exaflares are gone after 71498, and the later mixed
  wave leaves no stale constraints after 74592.
- Native lifecycle deactivates at timestamp-group completion after 76504 with
  zero components, including the second wave interrupted by boss death.
- Three native navigation fixtures export the arena stencil and actual next
  steps. Axes and mixed spheres have positive module leeway. **Rout navigation
  remains inconclusive:** the recorded actor at the four-preview sample is
  23.42 yalms from center, outside the provisional R20 bound, and native leeway
  is negative. Reducing active avoidance to the next dash does not resolve
  that arena/route uncertainty. No full continuous path or live XCAI execution
  was validated.

The frozen implementation-coverage review exhausts all five pages under contract
`c54d32a1e6e21dabd9c1a224fc514e93172a22d83cf40c0cb77e600f68bfcf08`.
It records 162 observations declared handled, 92 declared ignored, and 510
unmapped contextual observations. These are event counts, not mechanic coverage
percentages. Unmapped context includes player actions/statuses, scene and actor
lifecycles, model/timeline signals and environment changes; it is preserved in
the private report, not silently classified as implemented.

Eleven timing cases submitted to archive validation fall within the supplied
client-observation intervals. The formal result remains **inconclusive**:
there is no held-out source, the candidate parser MVID differs from the archive
parser, and client observations are not server snapshots. No numeric projection
equivalence is claimed: the current runner's schema-1 fingerprint excludes the
nine party rows retained in schema 3, although the native replay applies original
party operations. Archive HP/MP and other documented omissions remain explicit.

## Frozen build and local testing

Module-source manifest SHA-256:
`0bfad668958dbb564effc4fdada48a3c4f24c9769a537253c6e5d900d941b28e`.
This identifies the four Quaqua source files, not unrelated working-tree edits.
The full build includes all current local changes.

DLL SHA-256: `fb77f568732e6e3c16f8a7439e7a0292cb7e6b210ba6d369b3d33c8f04dc25c6`.
DLL MVID: `3cdc9e0a-f627-4f41-9d08-1e2cf78a87b5`.
Native harness SHA-256:
`bac58848af5311272aa34635d9b798c3f35d96fa750bbcf17442e19607ca1102`.

Private evidence and reports are under `/tmp/xbmr-quaqua/`: `evidence.json`,
`accepted-source-manifest.json`, `accepted-0-report.json`,
`accepted-1-report.json`, `accepted-coverage.json`, `timing-validation.json`,
and the build/test logs. These temporary paths are not durable publication
artifacts. Original paths and identifying replay contents are not committed.

```sh
DALAMUD_HOME=/home/xeltor/.xlcore/dalamud/Hooks/dev \
  /home/xeltor/.local/share/xbmr-dotnet/dotnet build \
  tests/QuaquaRegression/QuaquaRegression.csproj -c Release
/home/xeltor/.local/share/xbmr-dotnet/dotnet \
  tests/QuaquaRegression/bin/Release/net10.0/QuaquaRegression.dll \
  /home/xeltor/Projects/xBMR/BossMod/bin/Release/BossModReborn.dll \
  /home/xeltor/.xlcore/dalamud/Hooks/dev \
  '/home/xeltor/Games/steam/debian-installation/steamapps/common/FINAL FANTASY XIV Online/game'
```

The development plugin watches the normal Release output. No installed plugin
directory was modified, no manual reload was triggered, and no release version,
tag or feed was changed. Build/replay proof is separate from live acceptance.
Next required evidence is physical arena calibration, representative live
route 1 testing and independent recordings of the currently uncovered variants.
