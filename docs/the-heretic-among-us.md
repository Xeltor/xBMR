# The Heretic among Us

Implementation date: 2026-10-17. ARR level-40 MSQ solo battle, Quest 66477,
QuestBattle 61, territory 302, CFC 344. This is the Guillaime/Drillemont battle
outside Snowcloak, not the Snowcloak dungeon or an Ishgard trial.

## Evidence and written guidance

Read before design:

- [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/the-heretic-among-us):
  human and dragon forms, reinforcement priority, eastern ramp, and moving out
  of Electric Velitation.
- [Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/The_Heretic_among_Us):
  transformation, ally protection, reinforcements and failure on Drillemont's
  death. Later reinforcement counts differ from Thonky; do not fit a fixed wave
  schedule or approximate HP thresholds to those descriptions.
- [Official quest entry](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/bcd612f8b59/):
  quest identity. Historical comments are context, not current geometry/timing
  evidence.

The closed original SHA-256 is
`b847619ae20535b050a4c596b38a481d7f13c56aea70262953a8a3d6cf49a533`,
269,768 bytes, game `2026.10.06.0000.0000`. It decodes through EOF with 18,090
raw operations. Private schema-2 projection retains 9,305 events in ten parts.
The already-indexed archive schema-3 recording is
`fb6fcba8e2684fd3247e8980defba5ea9d52b5f13cea4bd3423512ef72dec575`
(8,998 events, one complete part); it is the same original, not another attempt.
Schema 3 omits HP/MP, and both projections omit ten legacy map operations; the
private native inspection preserves those raw payloads and actor ally flags.

All available evidence was inspected for authoring: no independent holdout is
claimed. EOF does not establish complete recorder coverage. The earlier CFC 343
recording belongs to a different quest and supplies no second Heretic attempt.
Private originals, typed sheet/layout facts, requests, checks and build identities
are under `~/.local/share/xbmr-evidence/heretic-among-us-20261017/`.

## Arena first, with explicit remaining limits

Matching QuestBattle/Level rows and the background's `QB_GaiUsc004_001` layout
identify the combat placements and an entrance barrier. Layer 92268 contains
CollisionBox 4256785 at `(-797.8951,224.5105,33.18368)`, rotation zero, scale
`(1,18.22341,27.59255)`, plus a line VFX. The `planevent.lgb` SHA-256 is
`9e1e8768fdc3577bbc41e3d567202ab429dee36ef6fc6260ee6231014a07a604`.
That single barrier does not establish a complete floor polygon.

The module therefore remains WIP. Its authored planning envelope is a rectangle
centered at `(-860,0)`, half-width 60 and half-height 40, containing all thirteen
scripted combat/friendly spawn origins. This is **not a measured playable-area
boundary**. It constrains the pathfinding map, allows existing terrain obstacle
maps and suppresses the 3D border so it is not presented as a physical wall.
Obstacle-map availability is runtime-dependent; an empty obstacle map does not
make the entire rectangle walkable.

Hounds approach via an elevated ramp at approximately Y247–248, while much of
the fight is around Y228. Complete cliffs, holes, floor collision and ramp
connectivity remain unverified. No bounds were inferred from where the player
stood, no fictitious teleporter/height transition was added, and no claim of
continuous safe travel is made. Stronger terrain evidence is required before
this envelope can be accepted as accurate full-arena movement support.

## Module lifetime and target policy

Drillemont OID `0x6D6` (NameID 2085) is the stable friendly primary; he can be
untargetable. The module activates only in territory 302/CFC 344 when a known
enemy is in combat. It survives the human form's removal, the transformation,
enemy-free gaps and the dragon's death while reinforcements remain. Drillemont's
death/destruction ends the phase; leaving the exact duty resets the module.
There is no guessed victory timer or HP-driven phase schedule.

The recording replaces human Guillaime `0x6D2` (actor 20) with dragon Guillaime
`0x6D3` (actor 24): human untargetable at sequence 6477, dragon created at 6543,
human destroyed at 6833. Dragon death at 15069 does not end the encounter:
Wyvern actor 25 survives until 17588. Allied conjurer/archer deaths likewise do
not end Drillemont's module. All surviving reinforcements remain independently
eligible.

Eligible Wyrmhounds `0x6D4` and Wyverns `0x6D5` receive priority 3; either boss
form receives priority 1. Negative restrictions, unrelated targets, allies,
untargetable actors and dead actors retain their existing priorities. No stun,
interrupt, invulnerability or cleansing behavior is inferred from an action name.
The module overrides the default-target fallback to return null: the protected
friendly primary must never become an automatic fallback attack target.

## Attack and timing contracts

| Source/action | Warning and nominal geometry | Removal |
| --- | --- | --- |
| Wyrmhound / Body Slam 497 | Cast-start circle, range 3 + caster radius (recorded 5.2 total) | Matching cast end/cancel, death/destruction |
| Wyrmhound / Stone Gaze 499 | Cast-start cone, range 6 + radius (8.2), half-angle 45 degrees from matching fan090 omen | Same |
| Wyvern / Collapse 1136 | Cast-start cone, range 8 + radius (10.88), half-angle 45 degrees from matching fan090 omen | Same |
| Dragon / Electric Velitation 738 | Fixed cast-location circle, radius 5; no caster-radius addition | Same |
| Dragon / Spirit Dart 1164 | Incoming single-target damage hint; timed prediction for an actual party slot | Same |
| Human / Ruin 31085 | Incoming single-target damage hint; timed prediction for an actual party slot | Same |

Shape dimensions are nominal matching Action/Omen facts, not independently
measured physical hit boundaries. Stone Gaze is treated as a frontal cone;
the name does not establish that looking away makes its area safe. Spirit Dart
is not Electric Velitation: the latter is action 738 with a ground target.

Only exact hostile source/action pairs create hazards. Spell origin and facing
are captured when the cast starts. Each cast's native corrected deadline is
captured once; duplicate callbacks and later reads during the raw timer's final
clamped tail do not slide it. Concurrent hounds retain independent hazards.
Components bootstrap from current casts and handle replacement, cancellation,
death, destruction and actor recreation. Targeted damage predictions exclude
absent/dead targets and NPCs without party slots; those NPC casts still receive
the incoming-damage hint rather than an invented all-party damage mask.

Rotten Meat 1163 warns of reinforcements from the human cast; Dragon's Blood
1162 warns of transformation. No future spawn time or fixed wave count is
invented. Instant Virus 169, Ruin II 31086, Lunge 737 and autoattack 870 were
inventoried, but lack a reliable advance cast cue in this recording; they receive
no fabricated timed prediction. Physical snapshots and last-safe departure times
remain uncalibrated; native cast deadlines are not server snapshot proof.

The radar shows current simultaneous casts and removes them individually. It
does not draw speculative later waves or timed patterns. Every shown risky AOE
also supplies its deadline to the AI. Vertical hit eligibility and route safety
across the ramp remain separate from this planar hazard model.

## Validation and maturity

Build, generated registration, focused synthetic lifecycle/geometry checks and
original-recording native replay are separate proof levels. Validation receipts
record the exact built DLL, source digest, game/dependency identities, frozen
expectations and available/unavailable samples. They do not execute live movement
or actions. WIP is retained for the material full-floor/ramp and physical timing
gaps above; no live gameplay acceptance is claimed.

The final current-tree Release build passes with zero errors and six existing
obsolete-gauge warnings. DLL SHA-256:
`762947e596510e8a378ffa6da138b3cc8ce0fcd86372cd32e8207df917d8250a`;
MVID `3267d04e-65f4-4836-96c8-2d4242072f00`; module source SHA-256
`9aa373e1db217e2205e7efa72824f2d44bdc5c4c3342b675f96256173e164727`.
It began after a fresh out-of-combat check and automatically finished loading
at `2026-10-17T12:34:06.736+02:00`. No installed-directory copy or manual reload
was used.

That exact DLL passes 153 focused synthetic checks and 1,077 independent
recording-derived assertions across 64 native samples. All sixteen recorded AOE
casts are checked at start and end; all five targeted casts are checked with
their recorded party membership. Drillemont, the player and the conjurer occupy
slots 24, 0 and 25 respectively: predictions use those individual bits, not an
all-party mask. Transformation, remaining adds after dragon death, independent
caster cleanup and target exclusions are covered. The native export is complete
with no missing samples, module errors, unsupported outputs or unapplied raw
operations, using exact matching game data. Here “independent” describes the
expected-value derivation, not a separate uninspected recording.

The revision-bound archive mapping audit exhausts all 325 selected cast/action
events in the reviewed combat interval (sequences 91–17588), across 63 token
groups. It records 46 cast observations with declared handling, 275 ignored
observations (friendly/player actions, ordinary autoattacks, and resolutions
whose guidance retires at cast end), and four unresolved instant observations
(Virus, Lunge and Ruin II). Mapping review contract SHA-256:
`7767380d3ba4fa4bcecd488f82ccb871dd5b8ee3b66456799513e6c0d652b212`.
These are observation counts, not a coverage percentage or proof of exhaustive
mechanic handling. Detailed declarations and omissions remain in the private
receipts.
