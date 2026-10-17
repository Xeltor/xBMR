# Promathia: ring deadlines, tower commitment and Heavy puddles

The ring-origin behavior described in this first repair is superseded by the
[movement-overlap correction](promathia-ring-overlap.md). Freezing the deadline
was necessary; freezing the ring centre introduced a regression when the boss
repositioned for Pestilent Penance.

Development follow-up on 2026-10-14 to
[the False Genesis and Empty Seed repair](promathia-false-genesis.md).
The earlier changes remain present. Release metadata, module maturity, settings,
IPC and assembly naming are unchanged. Unrelated concurrent Al Zahbi and Shinryu
working-tree changes are preserved and included in the full development build.

## Changed mechanic contracts

**Wheel of Impregnability (`50321` / `50323`)** previously returned an activation
of `WorldState.FutureTime(10.5)` every time its AOEs were queried. The drawing
identified the dangerous circle, but its navigation deadline never approached.
It now freezes a forecast at visual cast start, using the native corrected cast
finish plus 10.6 seconds. The 13-yalm circle and origin remain fixed for that
instance, survive the successful boss visual's finish, and retire on the actual
helper hit. Duplicate reads/starts cannot push the deadline forward. Interrupted
visuals clear their own instance; an old finish cannot clear a newer cast.

**Bastion of Twilight (`50322` / `50324`)** had the same rolling deadline and
incorrectly declared Wheel's action as its watched action. Apply the same fixed
forecast/retirement contract to its existing 8–50 donut, using its own helper ID.
The delay is an observed nominal forecast, not a measured server snapshot.

**Infernal Deliverance towers (`50335`)** previously offered a timed union of
all available towers. That permits uptime excursions out of the current soak or
movement toward a different tower. The repair selects an available tower using
current position and occupancy, keeps that choice stable, and adds an immediate
stay-inside constraint once the actor enters. Moving outside does not release
that commitment. Entering another eligible tower manually updates the choice.
Existing 4-yalm geometry and 1–8 occupancy policy are preserved; no fixed alliance
assignment is invented. Actual tower actions retire their own predictions and
commitments, independently of neighbouring towers. The existing cast-finish
cancellation, source destruction and phase-replacement paths clear owned state.
The subsequent `50565` 8-yalm, 5-second cast AOEs are preserved.

**Empty Salvation puddles** now have a registered component for event objects
`0x1EC097`. The same OID includes a central linking visual; it is excluded by its
centre placement. The six outer object positions supply the pool origins.
Spawn forecasts use the Salvation cast finish plus 0.8 seconds; actual activation
animation supersedes that forecast. Ordinary navigation receives immediate
avoidance constraints and the map displays each persistent pool.

For a required tower, only its chosen concentric puddle is exempt from avoidance;
the other pools remain risky. This accepts the unavoidable Heavy needed to soak
without creating contradictory soak/avoid constraints. All pools remain drawn.
The exception disappears as soon as that tower resolves. Expansion animation
changes the relevant puddles to the existing helper's 8-yalm footprint; the later
small animation restores the nominal 4-yalm footprint. Fade removes a hazard
before object destruction. Link animations preserve it. Duplicate creation,
recreation, stale destruction, new Salvation, interrupted Salvation and False
Genesis cleanup have explicit per-instance handling.

The **4-yalm small Heavy footprint is provisional**, based on the concentric
4-yalm tower, recorded Heavy entries and existing encounter layout. The expanded
8-yalm footprint agrees with the follow-up action range and an observed Heavy
entry 7.25 yalms from a pool centre during expansion. Neither establishes the
exact server boundary. No extra arbitrary radius margin is added. The repair
uses encounter-local constraints; shared pathfinding, rotations and XCAI source
are unchanged. No forced movement bypasses BMR safety or manual control.

## Evidence and uncertainty

Six distinct originals were reviewed; the first two informed implementation,
the other four relevant windows were inspected after source/build freeze. These
originals were previously used for other encounter work and are not pristine
whole-recording holdouts. All bounded action/context query pages are exhausted.

| Use | Recording ID | Original SHA-256 | First Wheel visual / helper sequences | First tower start / action sequences |
| --- | --- | --- | --- | --- |
| Design | `216d4cfe585ec3ef6501bf9b10b940c76a207a26a6551fd872a54e7dd10e4318` | `eaf65885e8c4dc128618d6fdd8633504243f643b2370df157c7da99794b048db` | `975435 / 985624` | `1155756 / 1161423` |
| Design | `6c85fbd4c236802850758b3dfc4c44c258c691b1eca2fe29d33329c016330ec0` | `fa629e1f29c71a94fb27654d626b9b20e697172eb3805b2b6196428a41c8bf90` | `1020074 / 1031176` | `1229450 / 1236109` |
| Post-freeze evaluation | `bbedd3081c1583ad3a5c34c3f8319e0ef56001267c9774196ba1da49fcfb27bb` | `6d02e9c89109687c97d8bf9d7398d178099a4b45b2b06d4c009f55b072100dec` | `1083678 / 1093429` | `1248966 / 1254301` |
| Post-freeze evaluation | `d20a982a413d1f48cdb840c23acde4713ca9a9218d4239d60b426b0e6648f743` | `d736ff6630d21d5e22b6e1dc574bf0f86088b4d5662581e7414631ed105f97b5` | `1044276 / 1055165` | `1242251 / 1248897` |
| Post-freeze evaluation | `e208c64b10ea036c15fcd066bbae657c41e0b8c810e9c28720659c340555541f` | `c97cef48d62d3d4da612911409967e9cdf7dbd475b062d6f8e7eb834777c31f2` | `920421 / 928845` | `1111451 / 1117450` |
| Post-freeze evaluation | `7c2d1362d4ed6e90c39c281b9941fb2bb8192f6320821a54cdec9bbd04b93984` | `df9f1b2b316191f3215a584098f85277cfc5b4fd652d9a93a2fdbcf81a037ccf` | `1002427 / 1010648` | `1203711 / 1210164` |

There are **28 ring visuals and 27 observed helper hits** across these sources.
The remaining Wheel starts at sequence `1189081` in recording `e208c64b10...`;
Promathia dies at sequence `1200662`, tick `14487313851`, before the forecasted
helper. The missing hit is retained as a phase-exit case, not excluded or turned
into an invented cancellation timer. Source destruction is sequence `1206844`.

For example, the first design recording's first Wheel begins at tick
`12680041567`, sequence `975435`; its helper arrives at tick `12806469681`,
sequence `985624`. Its corrected nominal forecast is 65.1 ms before that observed
hit. At the before-hit native sample, the baseline still claims **10.5 seconds
remaining**, while the candidate retains its original deadline. Successful
visual-finish samples retain the future circle/donut until helper resolution.
Across the six originals, cast observations vary: the frozen forecasts are
approximately 0.698 seconds early through 0.056 seconds late against client
helper delivery. This is reported without retuning to the evaluation sources;
it does not calibrate server snapshot time or last-safe melee departure.

First-design puddle creation sequences `941217`–`941222` are the six outer pools;
central object `941216` is excluded. Outer activation animations are
`945090`–`945095`. A representative puddle (`806`) fades at `971185` and is only
later destroyed at `974686`. A tower pool (`934`) expands at `1161543`, returns
to small at `1166553`, fades at `1183385`, and is destroyed at `1186742`.
The native callback composes animation state as `(p1 << 16) | p2`:
`0x00010002` small/active, `0x04000800` expanded, `0x00011000` small again,
`0x00040008` removed. Linked-puddle damage patterns retain existing components;
their exact Heavy boundaries are not newly certified.

Action data matching the two design recordings supports Wheel radius 13, tower radius 4
and follow-up radius 8. Local EObj/ExportedSG inspection resolves the puddle's
scene asset `sgvf_z6r3_b3492.sgb`, SHA-256
`e9294d2d43a3491d12278a0cc9c895b8ba8c714b518bb94a745c8946d0261c0c`.
Read-only inspection of its AVFX meshes/curves exposes several different visual
models and transforms; these were **not** substituted for a verified hitbox.
The local game version is `2026.10.06.0000.0000`, newer than the recordings and
matching archived Action datasets. Asset applicability and composed visual
edges remain separate evidence gaps. No raw game assets or private originals
are committed, published or uploaded.

## Validation

- Full current-working-tree Release build succeeds, with zero errors and the six
  existing obsolete-gauge warnings. The source generator registers the new pool
  component and all existing module/state/component factories.
- **38 new synthetic assertions** cover fixed ring deadlines, duplicate reads and
  starts, visual/helper retirement, interruption, individual tower commitment,
  an uptime excursion, selective pool exemptions, expansion/fade, recreation and
  phase reset. The complete native suite passes **1,251 assertions**, including
  the prior Promathia, Shinryu and concurrently added Al Zahbi groups. The saved
  baseline fails the new ring contract and lacks the pool component. Synthetic
  geometry/logic probes do not verify physical edges.
- **172 paired native replay samples** across all six closed originals are available,
  with zero module errors, skipped native operation kinds, unsupported outputs or
  missing samples. Other components' exported AOEs match at all 172 boundaries.
  **83 ring forecasts** keep their original deadlines; **28 visual finishes**
  preserve the forecast and **27 helper hits** retire it. Twelve pool samples
  expose six circles each; five recorded inside-tower samples have the immediate
  hold constraint. Both sampled False Genesis starts preserve the earlier arrival
  constraint and contain no stale pools. Baseline/candidate numeric projections
  are identical. Original hashes are checked before and after each serialized
  read. The missing helper after boss death remains an explicit phase-exit case.
  Detailed results are saved in
  `<isolated validation output>`.
- The five Python release/version tests, metadata validator and whitespace check
  pass. No version bump, tag, feed change, installed-directory copy or manual
  plugin reload is performed.

Frozen main module SHA-256:
`1891733de3a49c1c47450716f7caed9d2b789797f9a67106c664085beec9b4b2`.
Frozen state activation SHA-256:
`5d9776a887c8d80e3fa91ce01a650205ee8dce07405229260acb017a2e10add4`.
Baseline assembly SHA-256:
`1518243fc09199de095c22dd5e683e70a17726c0a27e68f1d3f55dd1ef724309`.
Candidate assembly SHA-256:
`51d0b09dfaf93f461866530bbf1e5f5418a30b4936780c5721fcdbdfed766f3b`.
This candidate includes the unrelated current working-tree changes; only the
owned Promathia paths and test registration were edited for this repair.
Requests, reports, sanitized event citations and private asset inspection are
under `<isolated validation output>`.

## Remaining acceptance

The native runner has complete original decoding and numeric projections, but
its aggregate export remains incomplete: collision, physical boundaries,
continuous routes, server snapshots and live controller execution are unverified.
Its optional navigation fixtures use a supplied speed of 6 yalms/second, not a
measured Heavy movement speed. They do not prove timely physical escape while
slowed. Mid-fight reload cannot recover omitted historical animation callbacks;
created-object fallback is provisional until the next observed transition.

Personal live duty testing is optional under the current module maturity
policy. These checks do not establish live melee departure, controller tower
commitment, physical escape while Heavy or the exact small-pool edge. The known
ring/cleave defect was subsequently fixed and validated in the linked overlap
correction. Physical uncertainty is retained as a limitation; it is not by itself
a demand for a personal playthrough or every hypothetical cancellation variant.
The current Contributed label is inherited provenance, not a new Verified
acceptance of the entire encounter.
