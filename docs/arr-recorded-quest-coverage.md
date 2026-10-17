# Recorded ARR quest instance coverage

Follow-up on 2026-10-17: [The Lominsan Way](the-lominsan-way.md) adds objective
selection, strict wrong-target exclusions, Rive cast handling and paralysis
guidance for the newly recorded territory 307 / CFC 354. It is the fifteenth
distinct duty, with sixteen encounter registrations including Guardian's two
anchors. The audit counts and artifact below describe the earlier fourteen-duty
snapshot; the follow-up note records its own evidence and validation limits.

A later pass on the same date uses two newly indexed Lominsan segments to
correct Rive to its observed action 1299, add incoming Paralyze and bomb
warnings, and request native cleansing while paralysis is present. That pass
builds and reloads successfully; 90 focused checks and 790 native assertions
pass against its final DLL. The two new segments appear to be one contest
split by reload, so they do not establish independent-trial acceptance. See
the encounter note for source identities, exact validation limits and the
corrected barrier-asset provenance. The historical audit below is unchanged.

Audit date: 2026-10-17. Scope is the retained, closed solo-duty recordings from
the current character's ARR progress, not every ARR quest or every duty ever
entered. Fifteen originals cover fourteen distinct instanced territories; the
two lindwurm attempts belong to the same duty. Every original decodes to EOF
and reports game version `2026.10.06.0000.0000`, matching the inspected game
data. All fifteen sources are authoring inputs, with no independent holdout.

Before this work, only the lindwurm rescue had an encounter module. Two other
duties had small quest automation scripts; those scripts did not supply the
encounter hazard coverage below and are preserved.

| Quest and battle | Territory / CFC | Added coverage | Implementation notes |
| --- | --- | --- | --- |
| Spirithold Broken | 225 / 297 | Clay Golem cone and circle | [Early MSQ](arr-early-quest-modules.md) |
| Leia's Legacy | 227 / 299 | Heartstopper, healer priority, surviving accomplices | [Early MSQ](arr-early-quest-modules.md) |
| To Guard a Guardian | 226 / 298 | Tree defense and separate mage battle, friendly-cast filtering | [Early MSQ](arr-early-quest-modules.md) |
| Into a Copper Hell, Ul'dah battle | 274 / 338 | Three bodyguards and target priorities | [Early MSQ](arr-early-quest-modules.md) |
| Lord of the Inferno, Amalj'aa ambush | 275 / 339 | Devastate, enemy Blizzard II, ally protection priorities | [Later MSQ](arr-later-quest-modules.md) |
| The Company You Keep (Maelstrom) | 279 / 342 | Vanguard lines, enemy Blizzard II, ranged soldier priorities | [Later MSQ](arr-later-quest-modules.md) |
| Brotherly Love | 303 / 350 | Enemy magic, morbol cone/line, healer priority | [Later MSQ](arr-later-quest-modules.md) |
| Nouveau Riche | 232 / 311 | Heartstopper, Overpower, Rive, reinforcement lifecycle | [Later MSQ](arr-later-quest-modules.md) |
| Big Trouble in Little Ala Mhigo | 306 / 353 | Melee lines, drake cone, ally protection priorities | [Later MSQ](arr-later-quest-modules.md) |
| A Dangerous Proposition | 237 / 307 | Wolf attacks and alpha priority | [Lancer](arr-lancer-quest-modules.md) |
| Lance of Destiny, guild training | 238 / 308 | Three sequential opponents, Heartstopper | [Lancer](arr-lancer-quest-modules.md) |
| Lance of Destiny, Kamapuaa | 290 / 355 | Reckless Charge, Water II, sprite priorities | [Lancer](arr-lancer-quest-modules.md) |
| Proof of Might, lindwurms | 239 / 309 | Frost Breath, Thibain protection and failure lifecycle | [Lindwurms](proof-of-might-lindwurms.md) |
| Proof of Might, Foulques | 240 / 310 | Heartstopper, Fire II, Ring of Frost and persistent ice | [Lancer](arr-lancer-quest-modules.md) |

The existing `QuestBattle` scripts for A Dangerous Proposition and Lord of the
Inferno remain separate from these encounter modules. The latter covers the
solo ambush before Ifrit, not the Bowl of Embers trial. Into a Copper Hell
covers the post-dungeon street fight, not Copperbell Mines. The two Lance of
Destiny instances and the two Proof of Might instances have separate modules.
To Guard a Guardian additionally needs two encounter anchors because the
cutscene destroys the first battle's allied anchor.

## Acceptance limits

These additions remain **WIP**. They provide cast warnings and native AI
constraints from the earliest established cue, target priorities and recorded
phase handling. Their fixed planning envelopes constrain the native
pathfinding map, but they are not accepted physical floor polygons. Layout
barriers provide partial evidence in some duties; natural edges, obstacles,
slopes and collision coverage still need verification. Invented 3D floor borders
are suppressed. Obstacle maps are permitted where available; permitting a map
does not establish that a matching map exists.

Cached navigation meshes were inspected privately as research. Their current
game-version and active layout-filter bindings could not be established, so
they were not treated as verified floor geometry. Player movement was not used
to infer safe arena boundaries.

Cast finish predictions retain BMR's existing NPC correction. Successful
recording-derived predictions or nominal shape probes do not calibrate server
snapshots, last-safe departure, collision, travel routes or live AI behavior.
Persistent frost uses a documented nominal avoidance policy until its contact
boundary is calibrated. Each encounter note records its written guide URLs,
recording identities, tested behavior and remaining gaps.

Private originals, requests and native reports are retained under
`<private evidence location>`; the earlier lindwurm
reports are under `proof-of-might-20261017/` beside it. Raw recordings, player
identities and extracted game assets are not included in the source changes.
No release or installed-plugin replacement is part of this work.

## Combined validation

The final current working tree builds successfully with SDK 10.0.401 and
API-15 development assemblies: zero errors and six existing obsolete-gauge
warnings. The build includes the unrelated local changes already present in
the checkout. The generated registry contains fifteen encounter registrations
across all fourteen recorded duties; Guardian accounts for the extra
registration. The thirteen previously uncovered duties now have modules.

| Group | Distinct duties | Synthetic assertions | Recording-derived assertions |
| --- | --- | --- | --- |
| Lindwurm rescue | 1 | 16 | 701 |
| Early MSQ | 4 | 34 | 601 |
| Later MSQ | 5 | 391 | 2,122 |
| Other Lancer battles | 4 | 37 | 1,172 |
| Total | 14 | 478 | 4,596 |

All 478 synthetic assertions also pass against the final watched DLL. Native
replay uses full compatible candidate builds with the same encounter source;
it reports zero module errors, unapplied raw operations or game-version
mismatches. Early and later MSQ exports are complete. Lancer and lindwurm
requests deliberately include seven samples after their modules unload; those
outputs are unavailable and are not counted as successful mechanic samples.
Lifecycle callbacks independently confirm component cleanup. All sources
remain authoring evidence, without untouched independent evaluation pulls.

The tests cover simultaneous and already-running casts, cancellation,
death/destruction/recreation, reinforcement gaps, target-following splashes,
negative target restrictions, the Guardian cutscene and the guild's sequential
opponents. Later-MSQ deadlines are captured once per actor/cast generation;
tail-frame checks prevent the native NPC correction from shifting the deadline
when a raw timer reaches zero. Native party-state inspection confirms protected
allies occupy NPC party slots, so their attackers receive ordinary eligible
priorities while hidden reserves retain their passive restrictions.

The existing Python suite completes eight tests, with one optional test skipped.
Owned source/test/document files pass whitespace checks. These results do not
close the physical arena, Power Wave angle/early-cue, persistent ice contact,
calibrated movement timing or live controller gaps described in the notes.

Final artifact:

- Path: `BossMod/bin/Release/BossModReborn.dll`.
- SHA-256: `d0fd5b1c0d3e4b17d1e8ba974143c48924da8077776767cacc157adc624cb468`.
- Assembly MVID: `f26111e6-31ed-41d1-b80d-f9b2d4db353b`.
- Build began after a fresh out-of-combat observation at 09:28:50 Amsterdam.
  A further observation at 09:29:00 also showed normal conditions. The watcher
  finished loading the development plugin at 09:29:15. No manual reload was
  requested. This is runtime reload proof, not live encounter acceptance.

Combined logs, source hashes, registry output and updated lindwurm candidate
receipts are preserved privately under `arr-solo-20261017/integration/`.
Per-group native requests, expectations and checkers remain in `early-arr/`,
`later-msq-validation/` and `lancer-validation/` beside it. Numeric authoring
projections are retained under `projections/` so temporary-directory cleanup
does not remove the checker inputs.
