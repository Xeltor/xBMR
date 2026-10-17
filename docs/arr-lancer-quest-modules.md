# ARR Lancer solo battles: initial encounter modules

## Scope and maturity

Authored on 2026-10-17 from the maintainer's four closed recordings, matching
game data `2026.10.06.0000.0000`, and written research below. These four modules
remain **WIP**. They add cast warnings, AI hazards and relevant target priorities;
natural terrain perimeters, physical hitboxes, calibrated snapshot timing and
live xBMR/XCAI behavior remain materially unverified. No release is requested.
The separate lindwurm rescue module is outside this change.

| Battle | Territory / CFC | Primary OID | Scope |
| --- | --- | --- | --- |
| A Dangerous Proposition | 237 / 307 | `0x666` friendly Foulques | Alpha wolf and pack |
| Lance of Destiny: guild trial | 238 / 308 | `0x220` first lancer | Three sequential lancers |
| Lance of Destiny: Kamapuaa | 290 / 355 | `0x6CD` Kamapuaa | Boars, reinforcing sprites |
| Proof of Might: Foulques | 240 / 310 | `0x223` Foulques | Duel, souls and persistent ice |

## Written research

Read the correct quest stages before selecting components:

- [A Dangerous Proposition](https://ffxiv.consolegameswiki.com/wiki/A_Dangerous_Proposition):
  the instanced encounter is the wolf pack after the earlier outdoor treant.
  Its objective is killing the alpha. Dialogue warns about paralysis and
  Foulques's interference. Recording action 999 is Nerve Sever; 1089 is the
  friendly NPC's blinding potion. His friendly attacks are not hostile AOEs.
- [Lance of Destiny](https://ffxiv.consolegameswiki.com/wiki/Lance_of_Destiny):
  separates the successive guild opponents from the later Bramble Patch trial.
  The latter includes boar reinforcements and ends with Ywain's intervention;
  killing Kamapuaa is not assumed to be the ending. Neither section provides
  measured attack dimensions or physical arena coordinates.
- [Historical Proof of Might guide](https://ffxiv11.wordpress.com/2014/04/15/ffxiv-proof-of-might-level-30-lancer-quest-guide-tips/):
  distinguishes rescue from duel, describes persistent ice and advises killing
  the three souls. Its cross-class Cure advice is obsolete. Its informal ice
  name differs from matching Action 1316's current name, Ring of Frost.
  The guide establishes neither precise radius nor last-safe departure.
- [2025 official-forum progression report](https://forum.square-enix.com/ffxiv/threads/522740):
  a player reports that stunning Foulques just before his summon can break
  completion. This is a report, not reproduced current-game proof. These
  modules introduce no stun or interrupt requests.

The wiki contains quest text and broad sequencing; no sufficiently detailed
written strategy with measured geometry was found for the wolf/guild/boar
instances. Native recordings and matching typed Action/Omen fields supply the
bounded implementation facts instead.

## Evidence identities

All sources below were used for authoring and regression expectations. There
are no untouched independent evaluation recordings for these four instances.

| Battle | Original SHA-256 |
| --- | --- |
| Wolves | `e69d38771050556db53a0f1bc6a1aeff0cbe6d3d72886cb3bac33f6b955ae744` |
| Guild trial | `77c53a5065f938a8cb37c29045d7656d1ec828c03caac73ecbe66eba762cba9b` |
| Kamapuaa | `9bf6a707cd19fe28fbf15d35bd25c3d9fb9f01d38ab9aff6d06d68a2ed518e07` |
| Foulques duel | `d7ea9435ffcdf27b0eac995c56859ef1e5eec1fe19fc6eaa03e83811455a5322` |

Numeric projections are schema 1 and omit health and other native operations.
Private native replay uses complete closed originals, without claiming missing
schema-1 health data or treating projections as separate trials. Matching local
typed actor/action facts and LGB summaries are retained under
`<private evidence location>`
and `zone-*-layouts.json`. Exact source sequence citations below refer to these
originals, not timestamps fitted into a universal script.

## Mechanic and lifecycle contracts

- Wolves: pack OID `0x599`, radius 1.65, casts Neck Rip 975; alpha `0x21F`,
  radius 2.2, casts Nerve Sever 999. Both matching actions are CastType 4,
  EffectRange 3, full width 2, Omen 2 `general02f`. Rectangles therefore use
  lengths 4.65/5.2 and half width 1. Cast starts create independent locked
  origin/facing predictions, and native cast finish/cancellation removes only
  that caster. Source sequences 6540/6542 are concurrent pack casts. The alpha
  begins its three casts at 6819, 11170 and 17924. Target priorities favor the
  quest's alpha objective over the pack. Friendly Foulques anchors the whole
  encounter, so killing a pack member does not unload its remaining hazards.
  Alpha death at 22355 ends the phase and clears components.
- Guild trial: enemies `0x220`, `0x221`, `0x222` arrive sequentially, at 55,
  1291 and 3042. The first enemy remains the module anchor after its death and
  destruction: this custom phase deliberately does not end on primary death.
  Native registration rejects event NPCs, so Ywain cannot be used as an anchor.
  Completion requires an observed
  final opponent and its death, not the initially empty final-enemy list.
  Heartstopper 866 is CastType 4, range 3+0.5, width 3, Omen 2: rectangle
  length 3.5, half width 1.5. The three recorded casts occur in the second and
  third stages. The first two deaths at 963/2389 leave components active;
  the final death at 6172 clears the phase.
- Kamapuaa: Reckless Charge 334 is a stationary rectangle despite its name:
  matching fields say CastType 4, range 3+R, width 3, no positional displacement.
  Each cast uses its own radius: boss 3 gives length 6; hog 2.4 gives 5.4.
  Warning state supports concurrent hog casts. Water sprite `0x6CF` uses Cure
  120, Water 1120 and self-targeted Water II 1016; fire sprites `0x6D0` use Fire
  141. Priority is water healer, fire caster, hog, boss. These are kill
  priorities, not asserted interrupt/stun susceptibility. Water II uses its
  typed radius 5 and recorded self-target, with cancellation cleanup. Boss
  destruction at 10817, after scripted untargetability, ends the module;
  no boss death or fabricated boss kill is required.
- Duel: Heartstopper 866 also belongs to melee soul `0x5FB`; independent
  per-caster state handles overlap at sequences 8230/8232. Caster soul `0x5FC`
  uses Fire II 147 targeting the player (5610/7408), CastType 2, radius 5.
  `BaitAwayCast` follows that actor target and does not tell the target to dodge
  a fabricated fixed cast-start circle. Souls have priority over Foulques.
  Ring of Frost 1316 is CastType 5, EffectRange 6 with caster radius 0.5,
  Omen 1 `general_1bf`; nominal initial circle is radius 6.5. Three casts at
  3651/4862/9954 precede persistent objects `0x5FD` at 4142/5679/10802.
  A locked cast prediction covers the cast, and an event prediction bridges
  resolution to observed object spawn, without a guidance gap or guessed
  removal timeout. The actual source objects remain while present and active;
  disappearance/event state 7 removes them, phase exit clears everything.
  Recorded object event state is 1; other state transitions are synthetic
  regression coverage, not observed alternatives. Environmental Frostbite 268
  is seen after these spawns, but neither its status source 0 nor object
  hitbox growth 0.5 to 1.5 measures the floor effect. Persistent radius 6.5 is
  explicitly a nominal avoidance policy borrowed from the originating cast;
  it remains physically uncalibrated and blocks maturity promotion.

The ordinary NPC cast correction is 0.3s. Observed 2.2s/2.7s timers therefore
give roughly 2.5s/3s warning deadlines, consistent with action delivery. AI gets
the same predictions at cast observation. Cast finish, action delivery, later
status delivery and server snapshots remain distinct. No verified extra greed
window or guaranteed last-safe departure is claimed. All priority changes skip
negative native priorities, allies, untargetable and dead/destroyed targets.
Manual input, native forbidden targets and other encounter constraints remain
authoritative.

Radar shows active cast hazards and actual persistent ice. There is no invented
future sequence or new arena text. The recorded three ice objects overlap at
the same position; each lifetime remains independent. Live radar readability,
including the ice edge, remains unverified.

## Arena investigation and open material gaps

The modules use fixed **provisional planning rectangles**, with obstacle-map
support enabled and 3D arena borders suppressed. They do constrain native
pathfinding, but are not measured playable floor boundaries and must not be
presented as arena acceptance. No shape is derived from recorded player paths.

| Battle | Planning center | Half dimensions | Evidence limit |
| --- | --- | --- | --- |
| Wolves | `(305, -285)` | `30 x 30` | Central encounter envelope; exact walls/natural floor unknown |
| Guild | `(161.5, -273.5)` | `9 x 6` | West/east quest barriers, other room edges unmeasured |
| Kamapuaa | `(-235, 21)` | `25 x 25` | Central encounter envelope; terrain/entrances unknown |
| Duel | `(-285, 292)` | `24 x 20` | Central encounter envelope; slopes/cliffs/obstacles unknown |

Guild layer `83084`, `QB_ClsLnc004_001`, in
`bg/ffxiv/fst_f1/twn/f1t2/level/planner.lgb` contains west CollisionBox
3818938 at `(150.6744, 15.54595, -274.0349)`, scale `(0.5, 6, 2)`, and east
3935782 at `(172.1159, 19.92509, -273.5756)`, scale `(1, 9.118258, 10.93007)`.
Associated line VFX support entrance barriers, not a complete rectangular
natural-room perimeter. Recorded quest actor layout IDs for the other three
instances did not resolve to suitable quest layers in the inspected direct LGB
dependencies; an unrelated FATE layer near the duel is excluded as floor proof.
Available old navmesh caches lack a verified matching game/filter binding and
are not promoted into physical geometry claims. Full floor, lethal edge, hole,
obstacle, transition and reset mapping remains required before promotion.

## Validation

- Isolated full-source Release build succeeds with SDK 10.0.401, compatible
  API-15 development assemblies, six existing obsolete-gauge warnings and zero
  errors. Generated registration is included. Assembly MVID
  `cf86ce7d-dafa-42ac-a14e-d52892e73da9`; SHA-256
  `486f1a8782fd71be62398f4e86603ad696e0b3c656cfb9e958346222f629ffdc`.
- `tests/ArrLancerRegression` passes **37 synthetic assertions** for generated
  registration, bootstrap of concurrent casts, per-caster dimensions, separate
  cancellation, sequential guild actors after first-anchor destruction,
  scripted Kamapuaa departure, actual actor-targeted Fire II, persistent ice,
  event-to-spawn continuity, object state 7, fresh retry construction, target
  eligibility, priority -4 preservation and absence of speculative control flags.
- Full native replay of the four closed originals passes **1,172
  recording-derived assertions** across 150 available main samples and six
  supplemental explicit target-fixture samples. Checks cover native module
  activation, source-keyed overlapping cast state, locked origins, nominal
  dimensions, corrected activation timestamps, all relevant target classes,
  preserved native forbidden priorities and component cleanup. No raw operation
  was skipped or unapplied, no module error occurred, and all offline game-data
  versions matched exactly.
- Five deliberately requested main samples are after final module unload and
  unavailable, rather than successful empty-hazard results: guild two, boar one,
  duel two. Their lifecycle records separately show zero components on phase
  completion. Those three main reports therefore retain `export_complete=false`;
  wolves and all supplemental target reports are complete within their scope.
- Native guild replay caught and corrected an initial EventNPC-anchor draft:
  the actual registry permits Enemy/EventObj, not EventNPC. The final first-lancer
  anchor remains active through its destruction and all three recorded later
  Heartstopper casts. This is separate from synthetic constructor success.

Private requests, reports, checker and build logs are retained under
`<private evidence location>`.
They preserve failed drafts and final report identities; final main reports are
`{1,6,7,13}-report-final.json`, supplemental target reports are
`{1,7,13}-priorities-report.json`. The assertion script is `verify.py`.

All builds for this subtask use `<isolated validation output>`; they never touch
the development plugin's watched main DLL. Parent integration owns the final
current-tree build and out-of-combat reload check. These results prove bounded
offline behavior; floor geometry, physically calibrated hazards/snapshots,
route safety, live radar delivery and live quest completion remain open.
