# Recorded ARR quest battles: later MSQ coverage

These five modules are **WIP**. They add encounter-specific cast warnings and timed AI hazards to the retained solo duties. The previously existing Lord of the Inferno quest script is preserved. No trial, dungeon, or other Grand Company variant is substituted for these battles.

## Written guidance read before implementation

All guides below were read on 2026-10-17, specifically their **Solo Duty** sections.

| Solo duty | Written guide and relevant findings | Cross-check against retained encounter |
| --- | --- | --- |
| Lord of the Inferno | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/lord-of-the-inferno): eliminate bruisers before rangers, respond to dangerous halberdiers, and leave Highcant to the Flames. This is the ambush before the Ifrit trial. | Territory 275/CFC 339 contains the named Amalj'aa and Flame allies. Several simultaneous Devastate casts and hostile Highcant Blizzard II casts appear. Friendly Flame Thaumaturge Blizzard II must be excluded. |
| The Company You Keep (Maelstrom) | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/the-company-you-keep-maelstrom): ranged soldiers before bestiarii and decurion; eliminate the new funditors before the Vanguard and keep its line attacks away from allies. | Territory 279/CFC 342 has the Storm party, both soldier groups, and Vanguard casts. Recorded Cermet Drill and Drill Cannons have different lengths, so both are handled. Hostile Fire II is a target-following splash. |
| Brotherly Love | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/brotherly-love): prioritize Sigh when it appears, then remaining Snarls, Screech and Budnapper. Ignore the morbol while avoiding its frontal attack. | Territory 303/CFC 350 contains both waves and the Milkroot Morbol. Bad Breath and Vine Probe have separate shapes. Snarl Fire II and Blizzard II are hostile; Papalymo's shared spells are friendly. |
| Nouveau Riche | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/nouveau-riche): finish each reinforcement group before decurion and Laurentius, switching back to new adds. | Territory 232/CFC 311 contains Laurentius, the allied Redbellies and 3rd Cohort enemies. Laurentius actually dies before the last decurion Rive, so anchoring the encounter on Laurentius caused an early unload and was rejected. The stable allied Redbelly Gutter anchors the final implementation. |
| Big Trouble in Little Ala Mhigo | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/big-trouble-in-little-ala-mhigo): lancers, pugilists, drake, tempered reinforcements, then Lavafang. | Territory 306/CFC 353 contains those waves and the protected Ala Mhigan party. Devastate, Burning Cyclone, Gutripper, Heartstopper and Lavafang's Fire II are distinguished. |

The guides agree with the observed encounter identities and reinforcement structure. They establish response context, not exact attack dimensions, server snapshots, physical arena borders, or last-safe movement times. Their suggested kill order is implemented as relative priorities, with existing negative target restrictions preserved. Highcant and the morbol receive the native non-target priority rather than attracting automatic attacks. Enemies already attacking the player receive the documented defensive preference where appropriate.

## Identity and evidence

The original recordings are closed native BMR v31 sources, all written with xBMR 7.5.6.3508 and game version `2026.10.06.0000.0000`. Each source reached EOF. Every recording used here is **training evidence**; projections of these sources are not independent holdouts.

| Territory / CFC | Quest row | Primary anchor OID | Original SHA-256 |
| --- | --- | --- | --- |
| 275 / 339 | 65879 | `0x3C3`, Flame Sergeant | `8835ea77cc854f61ccd22044e4b53084459a32972ad4cbc3818d6452310bf50d` |
| 279 / 342 | 66217 | `0x406`, Storm Marauder | `89f0051830d867083164220857f21148434388ad4537a9599cecd105fcafdc6a` |
| 303 / 350 | 66260 | `0x633`, Papalymo | `e370f3927c5e3e01f322da65828d734c4a2452b2608ca1df906ce06a5645c4cb` |
| 232 / 311 | 66276 | `0x66E`, allied Redbelly Gutter | `de7f80b0a2be662323e368b0e3ea49d399829acae286aaf8cbcfe5d57d36c312` |
| 306 / 353 | 66318 | `0x7BE`, Gundobald | `95311c186e111f8fd798a78269cb4e888484736491aea71ad7267ac0242bdcb7` |

The Nouveau Riche QuestBattle row contains an invalid Quest reference, `854246`. Exact-name lookup in the matching game sheets identifies ARR Quest `66276`, internal ID `GaiUsa308_00740`; the other same-name Quest `69398`, `XxaUsa308_03862`, is a different quest.

Private originals, native actor identities, Action/Omen rows, and relevant LGB summaries are retained under `<private evidence location>`. The `zone-*-facts.json` and `zone-*-layouts.json` files bind extracted facts to the game version and original digest, with asset hashes. Numeric projections are under `<isolated validation output>`, `05`, `08`, `09`, and `10`.

## Arena investigation and remaining material gap

Arena research preceded implementation. Matching-version territory layouts were inspected, including the recorded actors' native LayoutIDs and QuestBattle references. Lord of the Inferno has the `QB_ManFst209_001` layer with multiple CollisionBox and LineVFX records. Brotherly Love has `QB_GAIUSA201_001`, including a barrier near `(-251.77, -1.01)`. The other three extractions did not resolve a complete applicable barrier layer. Empty extraction results are not evidence that the duty has no barriers.

These partial layout records do not establish complete walkable polygons, holes, lethal edges, or phase-dependent floor eligibility. A collision-box transform alone is insufficient to infer its complete world collision semantics. The available vnavmesh caches could be deserialized during the wider investigation, but their exact game/filter bindings were not verified. Player tracks were not used as safe-floor measurements.

The modules therefore use **provisional fixed planning envelopes**, chosen around source-supported encounter landmarks and spawn groups. These circles are computational caps, not claims of circular terrain or measured safe floors:

| Duty | Fixed center (X, Z) | Planning radius |
| --- | --- | --- |
| Lord of the Inferno | `(-338, -238)` | 32 |
| The Company You Keep (Maelstrom) | `(369, -352)` | 45 |
| Brotherly Love | `(-277, -37)` | 42 |
| Nouveau Riche | `(44, 6)` | 42 |
| Big Trouble in Little Ala Mhigo | `(35, -345)` | 38 |

These bounds constrain native planning and enable the existing obstacle-map facility; no obstacle bitmap, physical route, or lethal-edge correctness is claimed. The frame remains fixed across waves. Three-dimensional arena-border drawing is disabled to avoid presenting these caps as measured physical walls. Resolving the terrain contract remains required before maturity promotion.

## Mechanic contracts

The matching Action rows give the dimensions below. Rectangles and cones use the native recorded caster hitbox as an explicit range extension; rectangle full widths become half widths, and omen full angles become half angles. Circles use the sheet radius. These are supported initial models, still lacking independent physical boundary probes.

| Action | Model | Observed hostile source |
| --- | --- | --- |
| Devastate 490 | Front length `3 + caster radius`, full width 3 | Inferno bruisers/rangers; Ala Mhigo pugilists |
| Blizzard II 146 | Caster-location circle, radius 5 | Highcant; Company imaginifer; Brotherly Snarls |
| Fire II 147 | Target-following splash, radius 5 | Company/Nouveau imaginifers; Brotherly Snarls; Lavafang |
| Cermet Drill 526 | Front length `6 + caster radius`, full width 5 | Company Vanguard |
| Drill Cannons 525 | Front length `30 + caster radius`, full width 5 | Company Vanguard |
| Bad Breath 604 | Radius `12 + caster radius`, full angle 120 degrees | Milkroot Morbol |
| Vine Probe 606 | Front length `6 + caster radius`, full width 8 | Milkroot Morbol |
| Heartstopper 866 | Front length `3 + caster radius`, full width 3 | Nouveau bestiarii/Laurentius; tempered pikemen |
| Overpower 720 | Radius `6 + caster radius`, full angle 90 degrees | Nouveau decurion |
| Rive 1135 | Front length `30 + caster radius`, full width 2 | Nouveau decurion |
| Burning Cyclone 517 | Radius `6 + caster radius`, full angle 120 degrees | Battle Drake |
| Gutripper 865 | Front length `3 + caster radius`, full width 3 | Ala Mhigan conscripts |

Fixed-location/directional hazards use the cast's locked location and rotation. Current hostile casts are read independently, so activation with an existing cast, concurrent casters, cancellation, death, destruction and actor recreation cannot leave old hazards behind. Corrected activation deadlines are captured at the first valid cue and keyed by source actor and cast object, so expiry of the raw cast timer does not discard the final native 0.3-second correction. Shared friendly spell IDs are excluded by current allegiance and encounter OID. Fire II uses the native `BaitAwayCast` target-following model rather than a fixed circle that would invite an impossible dodge of a player's own targeted attack. It supplies timed avoidance/bait guidance for the correct party member, including recorded NPC party members. Added source/target cleanup and duplicate-start deduplication prevent stale baits.

The first reliable directional/action cue in the inspected evidence is cast start. No earlier status, target change, or scripted timer was established as a reliable predictor of which attack and facing will occur. Warnings therefore appear as soon as that cast exists; no speculative rotation is fitted to one recording. Activation uses native `CastFinishAt`, preserving its NPC `+0.3s` correction. Typical corrected nominal cast lengths are 2.0s for Blizzard II and Drill Cannons, 2.5s for the short frontal attacks, and 3.0s for Bad Breath and Fire II. These are **nominal completion deadlines, not verified snapshots or last-safe departure times**. No additional greed window or late-dodge optimization is introduced.

Each current independent cast is drawn and supplied to AI. Future repetitions are not painted across the arena. Fire II uses the normal outline for a player's own bait and filled danger for another target; resolved/cancelled casts disappear immediately. This keeps the radar focused on current simultaneous responses. Live radar readability and controller response remain unverified.

Stable ally anchors preserve component lifetime across individual enemy deaths and reinforcement gaps. Leaving the territory/CFC resets the module; ally-anchor death clears the phase on failure. Native replay confirmed why Nouveau must remain active after Laurentius dies: death at source sequence `15488` precedes decurion Rive at `17559`.

## Validation

The isolated full candidate build succeeded using the pinned SDK and API 15 development dependencies. It produced six existing obsolete gauge warnings and no errors. The watched plugin output was not replaced by this candidate. The isolated assembly SHA-256 is `053b03294512562d1b863d95172da6e6eb1418ddfdbcb40af2aa0a4537bd7747`.

`tests/ArrLaterQuestRegression` passes **391 synthetic checks** across registration, existing/concurrent casts, all authored dimensions, timed AI hazards, friendly spell exclusion, priority preservation including `-1` and `-4`, reinforcement gaps, cancellation, death, destruction, recreation, target-following splash movement, duplicate callbacks, and own-bait behavior. These are implementation regressions, not physical encounter evidence.

Native replay requests/reports and independent numeric expectations are retained under `<private evidence location>`. Their samples use exact raw-operation or raw timestamp-group boundaries from the original decoder, rather than assuming that the last numeric projection event is the last native operation in a timestamp group. Each request declares every source as training evidence. Actor-specific Fire II probes distinguish the target from another recorded party member and include target movement where present.

All **158 native samples** export completely, with **2,122 independent numeric assertions**, no missing samples, no skipped native operations, no module errors, and exact offline game-version matches. Assertions cover independently reconstructed active caster sets, cast-origin/facing and sheet dimensions, corrected activation deadlines (within 2ms), AI boundary probes, explicit target-priority fixtures, and moving Fire II target probes. The final combined working-tree build is reported in the task's coverage summary. Build/registration, offline mechanics checks, physical geometry/timing, and live xBMR/XCAI acceptance remain distinct. Full terrain validation, independent source/variant coverage, measured last-safe timing, visual validation, and live controller behavior are still open; none of these modules is promoted from WIP on build/replay success.
