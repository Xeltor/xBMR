# Isgebind: inactive Frost Breath cone

Repair date: 2026-10-17. Normal Stone Vigil, territory 1042/CFC 11, boss OID
`0x5AF`. The maintainer reported that the inactive front cone blocked both BMR
AI and XCAI. This material movement defect reopened the module for investigation.

## Guidance and cause

Read before implementation:

- [Current normal guide](https://www.icy-veins.com/ffxiv/the-stone-vigil-dungeon-guide):
  Frost Breath is a casted tank-facing cone; its other boss mechanics identify
  the post-6.1 normal variant.
- [Thonky](https://www.thonky.com/final-fantasy-xiv/duties/the-stone-vigil) and
  [Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/The_Stone_Vigil):
  face the boss away from the party and avoid the actual frontal attack. This
  positioning advice does not establish a continuously damaging idle cone.
  Thonky's persistent ice-puddle details are legacy behavior and were not used.
- [Official 6.1 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/c73cd284013587066d8f9e697fab1db9f007372c):
  relevant normal-dungeon mechanic adjustments; no geometry/timing measurements.

The module activated both `FrostBreath : SimpleAOEs` and
`FrostBreathCleave : Cleave(activeWhileCasting: false)`. The latter followed the
boss's target while idle and supplied an immediate forbidden zone because its
`NextExpected` was never assigned. It also produced an idle warning/outline.
The actual cast already had its own timed cone.

Remove only that permanent cleave component/activation and its unused shape
field. Keep the cast-start Frost Breath 1022 cone (nominal radius 27, half-angle
60 degrees), activation deadline and cast-end/cancellation behavior. Both AIs
consume the resulting module forbidden zones: no XCAI-specific exception, forced
movement, generic cleave change or weakening of other encounter hazards is used.
The radar now shows Frost Breath during its cast rather than presenting an idle
outline as a continuing threat.

## Recording and timing boundaries

Original SHA-256:
`782dc65383ea7eda1190d05d55fe8b28c80bca905115b8c90277bf3a35d44416`,
2,395,324 bytes, game `2026.10.06.0000.0000`. It decodes through EOF with
172,401 raw operations and 110,255 retained schema-2 events in 108 parts. It is
an authoring/regression source, not an independent holdout. The preceding local
file is an adjacent dungeon segment, not a second Isgebind clear. Original
recordings and detailed receipts remain in private evidence storage.

Isgebind is recording-local actor 71. All five observed Frost Breaths have cast
starts at sequences 145925, 151217, 155459, 158107 and 165355; matching cast ends
are 146014, 151297, 155542, 158179 and 165429. The observed cast-start-to-action
interval is about 0.97–0.98 seconds. The existing native NPC correction and
captured cast deadline are retained; damage/action delivery is not proof of the
server snapshot or last-safe crossing time.

The roughly 9.15-second Rime Wreath-to-breath interval in this recording is not
used to invent a prediction timer. Crossing is no longer forbidden solely
because the boss could eventually breathe. An actual new cast, ice, Cauterize,
Touchdown or arena constraint can still make a route unsafe. Physical cone
boundaries, controller response and live crossing/survival are not established
by this repair's offline validation.

## Acceptance

Reopening is limited to the false idle constraint. Restore Verified when the
focused checks reproduce the old defect, confirm its absence between attacks,
preserve all five actual casts and adjacent mechanics, and the final current-tree
build passes. Existing arena and unrelated mechanics retain their prior
acceptance; absent personal live testing alone is not a WIP gate. Validation
counts and the final DLL identity are recorded below.

The focused fixture reproduces the old idle barrier: baseline mode passes its
35 checks, while normal repaired-behavior mode rejects the old DLL at the idle
constraint assertion. The repaired exact current-tree DLL passes all 35 checks,
including actual-cast geometry/deadline, bootstrap, cancellation, reset, death,
destruction and adjacent raidwide/ice/flight/landing behavior.

The final Release build passes with zero errors and six existing obsolete-gauge
warnings. DLL SHA-256:
`0eb0ab89a61f545bf51425981b7ad89285fa166d7ee18b4984dca16149fa1c54`;
module source SHA-256:
`4def13f13614babd3718663a3c6d7e29f65dfa0a699faea6637d33e3197dbf5b`.
A fresh out-of-combat check preceded the build. Dalamud automatically finished
loading it at `2026-10-17T12:55:29.428+02:00`; no manual reload or
installed-directory copy was used.

The native comparison retains 34 recording-derived samples. The baseline has
extra idle constraints in fourteen samples, including all immediate before/after
breath samples. Actual Frost Breath casts already have one timed constraint;
removing the permanent cleave preserves it. The source expectation for unchanged
Touchdown initially omitted `ArenaCenter.Quantized()`. Its three control failures
occurred in both baseline and candidate. Those v1 contracts/failures are
preserved; v2 derives the center from the existing float-grid quantization
formula, without changing hazard tolerances, dimensions or module behavior.
V2 baseline passes 492 assertions. Both the isolated repaired candidate and the
exact final DLL pass all 614 assertions across 34 native samples, with zero
missing samples, module errors, unsupported outputs or unapplied operations.
The final DLL MVID is `2d2cb9b5-897f-40b6-ade5-bed01ec830ef`. All five actual
breaths preserve their timed constraints; fourteen baseline samples with extra
idle constraints now have none. Detailed source-formula correction and exact
final-build replay receipts remain private.

The false idle constraint is repaired and focused validation supports restoring
Verified. This does not certify a newly measured physical cone, last-safe crossing
time, or live BMR AI/XCAI movement; those proof levels remain separate.
