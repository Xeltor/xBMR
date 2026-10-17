# Quaqua — partial Variant Aloalo Island support

Implemented on 2026-10-14 in `V3Aloalo/V31Quaqua`. This is Variant duty 961,
territory 1176, boss OID `0x40BA`, NameID 12527. It is **WIP**, not verified or
complete encounter support. The default minimum maturity is Contributed, so
testing this module requires enabling WIP modules in xBMR's module settings.
No live settings were changed.

## Scope and arena

The implementation covers the observed left-path thunder and fair-skies pulls:
Made Magic, three axes, three donuts, the later mixed five-sphere wave, Rout,
Hammer Landing, Violet Storm, Cloud to Ground and Scalding Waves. A global hint
identifies partial coverage. Middle-path rotating crosses/poison pools and
right-path orbs/statue gazes remain unsupported. Only the observed Rout order
(edges then middle) is covered by recording evidence; middle-first is untested.
The code follows helper-preview order rather than hardcoding either route.

Arena bounds are established immediately and feed native AI pathfinding, not
just drawing. The center is the fixed authored placement `(-538, 94)`, including
when the module is created while the boss is dashing. The previous constructor
captured the boss's current position and therefore displaced the entire arena on
a mid-dash load. The nominal circle radius20 is independently corroborated by
the installed perimeter collision mesh described below; player trajectories were
not used to define a safe perimeter. Exact server DoT eligibility and any
route-specific changes not represented by this authored placement remain
qualified. No phase-dependent boundary change is implemented.

### Independent arena evidence and bootstrap correction

The current installed game is `2026.10.06.0000.0000`. Typed TerritoryType1176
selects `ex4/09_ocn_o5/dun/o5d1/level/o5d1`. Its
`bg/ex4/09_ocn_o5/dun/o5d1/level/bg.lgb`, SHA256
`e4c912540afea0cfff1f33e5dd34f723a589440e1b33126d7ef512d6644432e2`,
places CollisionBox10146156 at `(-538,18,94)` with zero rotation and unit scale.
Its exact mesh link resolves to
`bg/ex4/09_ocn_o5/dun/o5d1/collision/o5d1_xx_col01.pcb`, SHA256
`413d18e03ed6b36be86097839f1bf0e6e0864bf4d755d7bf83b97640ec7fc6d6`.

The installed Lumina collision-box reader uses an older layout and returns the
spurious path `9`. The bounded raw reader instead follows the current primary
[FileLayerGroupInstanceCollisionBox contract](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/FileFormat.cs):
OffsetPath is at instance+0x50, relative to the instance. All three placed copies
resolve to the same full path. No guessed filename was used as the link.

All31 nodes and384 triangles match the collision-file header. The mesh is an
annulus whose closed inner boundary has64 vertices at nominal radius20; decoded
unrounded vertex radii are19.999853–20.000206. The outer boundary is approximately
25.4878. Both loops have degree2 at every vertex after disclosed0.001y coordinate
rounding. The ring's authored centre and nominal dimensions are independent of
boss and player positions. Polygon tessellation, collision material overrides,
exact DoT eligibility and enablement are distinct from this geometry result.
The guide qualitatively identifies the damaging perimeter. The radius is retained
at20; no unexplained hazard inflation or arena enlargement was added.

The constructor now uses that fixed centre. Four displaced-bootstrap fixtures
check all sides, immediate AI arena metadata, the opposite playable edge,
excluded outer perimeter and reset after a different displacement. They would
fail under the old constructor. The parent owns compilation and execution of the
updated suite; no new passing test count or final binary hash is claimed here.
Sanitized reproducible asset facts are in `aloalo-arena-geometry.json`; proprietary
raw assets and bounded reader outputs remain private under
`<isolated validation output>`.

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

The initial thunder implementation used this original for authoring and
regression. The later fair-skies original below is also an authoring source;
neither is an independent holdout for the current module.

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

## Melee timing and additional left-path mechanics

The subsequent user recording supplies one complete hammer sequence and two
complete fair-skies waves:

- Recording: `8346f3985f6707c27c31567ef98b774be99648a024d4c1b7fd0e87d7839e8077`.
- Original SHA-256: `317f5cc7de163596122f06a4740dfe4d8660f0d0a6ed0ffe6753f03e5f200211`.
- Retained revision: `ac32a19004870d8216a3cf6295be09ff07e76647fcc24629db2c2c1bc35d7fd9`.
- Same writer, game version and archive parser MVID as the thunder recording.
  Complete schema-3 projection: 24,313 selected observations, 67,451 decoded
  raw entries; the source includes trash before the boss.

| Mechanic | Cited sequences | Prediction, resolution and limits |
| --- | --- | --- |
| Sphere avoidance | early tethers 45492–45495, 47519–47522, 60515–60520; sphere casts 46671–46673, 48569–48571, 61752–61756 | Keep tether previews visible throughout the roughly 12.8-second forecast, but mark them risky only in the final five seconds. Geometry and true impact deadlines are unchanged; actual sphere casts remain authoritative. This avoids exposing distant forecasts to the IPC safety predicate, which treats every forbidden shape as immediately unsafe regardless of activation. |
| Hammer Landing `35725/35726` | hammers visual 49958; numbered status 2056 at 50903, 51104, 51345; first cast 51128; impacts 51948, 52270, 52591 | Beacon OID `0x40E0`, extras 626/627/628 supply order. First cast supplies its origin/deadline; subsequent impacts use the next beacon position and resynchronize at +2.1 seconds. Only the next 20-yalm radial push constrains landing positions. Native immunity handling applies. Individual effects advance once; cancellation and the third impact clear the sequence. The second visual at 66501 is interrupted by boss death and supplies no completed sequence. |
| Scalding Waves `35735/35736` | initial casts 56282–56283 and 64030–64031; initial hits 56876/56878 and 64897/64899; repeats 57404–58821 and 65159–66442 | Initial beams from OID `0x40BE` use a 50-by-8 reference rectangle. Four cascade branches from OID `0x4134` advance four yalms with 50-by-4 rectangles: first repeat +2.8 seconds, then +2.1 seconds. Each inward branch has seven pulses and each outward branch three. Only the next pulse is risky, starting after both initial beams resolve; later pulses remain previews. Helpers are reused, so matching uses due time and position rather than immutable caster ownership. |

The five-second sphere lead preserves a route budget in 243 synthetic native
navigation starts across the center and five radii for axes, donuts and mixed
spheres. Worst travel was 3.684 seconds with the native one-second reserve.
This checks the reference arena and arrangements, not arbitrary layouts,
latency, actual melee uptime or live execution. It does not change the common
IPC safety implementation or XCAI policy.

Action/Omen evidence in the archive is pinned to the older game
`2026.09.15.0000.0000`. The regression executable separately reads the currently
installed `2026.10.06.0000.0000` Action rows and confirms ten relevant
cast-type/range/width tuples, including both Scalding Waves actions. Knockback
row 96 confirms the nominal 20-yalm radial hammer displacement; its row ID also
appears in the recorded knockback effect. Donut inner radius and cone angle have the
upstream/written reference basis. These remain reference geometry, not measured
server hit boundaries or hitbox-intersection semantics.

## Validation

- Full current-tree Release build: SDK 10.0.401, compatible Dalamud API 15
  development dependencies; zero errors and six existing obsolete-gauge warnings.
- `tests/QuaquaRegression`: 590 assertions covering real generated registration,
  phase activation, arena-to-AI wiring, fixed center, fresh-pull cleanup, tether
  ordering, duplicate starts/effects, individual retirement, cancellation,
  helper handoff, shape boundaries, mixed-wave safe pockets, delayed sphere
  avoidance, native route budgets, numbered hammer order and both fair-skies
  branch lengths. These callback
  fixtures are synthetic; expected pockets use the written rules/reference
  dimensions, not candidate-generated ground truth.
- Closed-original native replay: 158 available candidate samples across four bounded
  requests. Zero module errors, missing samples, skipped operation kinds or
  unsupported output kinds. Offline game assets exactly match the recording.
  Both original files' SHA-256 values were checked before and after replay.
  The 69 new-recording samples were also run against the pre-change DLL;
  common AOE geometry and deadlines match exactly, including 33 sphere instances.
- All 44 initial/cascade impacts across both fair-skies waves have an imminent
  prediction matching the recorded cast/helper position within 0.028 yalms and
  the impact observation within 0.059 seconds. Both waves clear fully. Hammer
  constraints advance through all three impacts and clear; 24 non-marginal
  landing probes agree with an independent nominal radial-push calculation.
  This is reference-shape replay evidence, not physical hit or knockback proof.
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

The initial thunder implementation-coverage review exhausts all five pages under contract
`c54d32a1e6e21dabd9c1a224fc514e93172a22d83cf40c0cb77e600f68bfcf08`.
It records 162 observations declared handled, 92 declared ignored, and 510
unmapped contextual observations. These are event counts, not mechanic coverage
percentages. Unmapped context includes player actions/statuses, scene and actor
lifecycles, model/timeline signals and environment changes; it is preserved in
the private report, not silently classified as implemented.

The additional recording was inventoried without restricting to known action
IDs over inclusive sequences 40438–66640. All six pages are exhausted under a
fixed revision. Its current-source handling review uses contract
`3a862b312244ace161a41fc225dacfee2882d65b86e3edf19d1797d648936380`:
122 observations declared handled, 29 explicitly ignored staging/visual/autoattack
observations, and 503 unmapped contextual observations. These declarations are
source-review claims, separate from the replay checks above, and do not certify
full encounter coverage.

Eleven initial thunder timing cases submitted to archive validation fall within the supplied
client-observation intervals. The formal result remains **inconclusive**:
there is no held-out source, the candidate parser MVID differs from the archive
parser, and client observations are not server snapshots. No numeric projection
equivalence is claimed: the current runner's schema-1 fingerprint excludes the
nine party rows retained in schema 3, although the native replay applies original
party operations. The new-recording replay likewise exports schema 1 rather than
claiming full schema-3 projection equivalence. Archive HP/MP and other documented
omissions remain explicit. The earlier coverage/timing contracts do not certify
the newly added mechanics.

Fifteen additional timing cases cover both fair-skies waves and all three hammer
impacts. All fall within the supplied client-observation intervals of ±0.1
seconds. Their formal archive result is also **inconclusive**, because both
recordings are authoring sources, parser MVIDs differ and these observations do
not establish server snapshots. The full 44-impact fair-skies numeric comparison
is retained separately in the private detailed-validation report.

## Frozen build and local testing

Module-source manifest SHA-256:
`5a750b8102837eb223b33b897af6a7e1aa2af126133ec88fc5f6f405055bfc67`.
This identifies the six Quaqua source files, not unrelated working-tree edits.
The full build includes all current local changes.

DLL SHA-256: `0199856b9dccfab587b87fc2afbcec7d97f96c63ade48c2bad3b63293a43b92e`.
DLL MVID: `c4518902-09a3-4e53-9c61-82be46407a14`.
Native harness SHA-256:
`bac58848af5311272aa34635d9b798c3f35d96fa750bbcf17442e19607ca1102`.

Private evidence and reports are under `<isolated validation output>`: `evidence.json`,
`accepted-source-manifest.json`, `accepted-0-report.json`,
`accepted-1-report.json`, `accepted-coverage.json`, `timing-validation.json`,
and the build/test logs. The subsequent build, two-source manifest, paired
new-recording reports, rerun thunder reports and detailed numeric validation
are under `<isolated validation output>`. These temporary paths are not durable
publication artifacts. Original paths and identifying replay contents are not
committed.

```sh
DALAMUD_HOME=${DALAMUD_HOME} \
  dotnet build \
  tests/QuaquaRegression/QuaquaRegression.csproj -c Release
dotnet \
  tests/QuaquaRegression/bin/Release/net10.0/QuaquaRegression.dll \
  BossMod/bin/Release/BossModReborn.dll \
  ${DALAMUD_HOME} \
  "$FFXIV_GAME"
```

The development plugin watches the normal Release output. No installed plugin
directory was modified, no manual reload was triggered, and no release version,
tag or feed was changed. Build/replay proof is separate from live acceptance.
DevBridge reports the development xBMR plugin loaded, but its diagnostic provider
does not export loaded-assembly provenance, so the runtime MVID has not been
matched to this build. The last recorded live fight predates these changes.
Next required evidence is physical arena calibration, representative live
left-path testing and independent recordings of the currently uncovered variants.

## Maturity reassessment after 7.5.6.35.4

Reviewed source base `bd9bf9228` under `docs/module-maturity-policy.md`.
Quaqua remains WIP because the written guide identifies actual middle-path
rotating crosses/poison pools and right-path pursuing orbs/statue gazes which the
module does not handle. These are known material coverage gaps, rather than
hypothetical undiscovered patterns. The arena bootstrap correction closes a
separate concrete defect and adds independent perimeter support.

Personal live play, exact server snapshots, a fixed number of recordings and
pristine holdouts are not automatic promotion requirements. Existing recorded
left-path validation and the new bootstrap checks are relevant evidence; their
limits remain disclosed. The absence of a recorded middle-first Rout alone is
not a new defect: the component follows actual ordered previews rather than a
fixed path. No Verified promotion is made while the explicit route gaps remain.
