# Eden's Gate: Resurrection (normal)

Implemented on 2026-10-14 in `Shadowbringers/Raid/E01EdenPrime`. Territory 849,
CFC 653, primary OID `0x2499`, BNpcName 8345. Savage is deliberately a different
encounter. Maturity remains **WIP** because of the material gaps below.

## Written-guide review

Read before implementation:

- [Normal raid guide](https://ffxiv.consolegameswiki.com/wiki/Eden%27s_Gate:_Resurrection),
  especially Phases & Abilities. It identifies the lethal edge, two-hit buster,
  orb drops, tank lines, donut, corner attack, three-part Delta Attack,
  beam/ground-circle overlap, scene changes and add intermission.
- [Pocket Guide, normal](https://ffxivguide.akurosia.org/shb/edens-gate-resurrection.html).
  Its mechanic prose is placeholder WIP; the listed action IDs were useful
  search aids, not independent geometry or timing evidence. Its broad role tags
  do not reliably describe AOE shape.

No mechanical disagreement with the normal guide was established. Neither guide
calibrates server snapshots, proximity falloff, or exact playable dimensions.
The guide's front-facing Pure Light response agrees with the observed corner
origin and Action-sheet rectangle, rather than a guessed cone.

## Evidence and arena contract

Development archive recording
`b4248652c2b9b5bf2a9e85f5985d48cbc069a0b6b581f9ace7543adfaca4968b`,
source digest `6b5d9ea9ce55b7977df33b829fac51b1d2c0ee1b4f48532d17430217b92d8f80`,
retained revision `34c4b59e414166f06f997b80a63de3d6f3ab2a64b82d4ae16efc8f2b5892ffdd`,
game `2026.09.15.0000.0000`, schema 1. Also reviewed schema-3 archive recording
`dedc30912090101413f4d8858db94df2105831af706a871b7f52a008786754a5`,
source digest `200b60514847ef5cf88e45d7d6c64b46db1bb13eb1909e69a804bda50e4466cb`,
game `2026.10.06.0000.0000`. Both report complete decoding, with explicit omitted
operations. Schema 1/3 lack HPMP; positions and cast/action delivery do not prove
server snapshots. All reviewed sources are disclosed as development evidence;
there is no pristine final independent holdout.

The closed local original used for native replay has digest
`de6468b9f136501506f2e4555bfdb9946433a07f68fdf895b335bb70a7602dba`.
It differs from both archive digests: archive sequences/aliases must not be used
as its replay anchors. The initial mismatched-anchor report was retained as an
unavailable-state trial, then corrected using a private decode of this original.
Its relationship to the same-day archive source is not established as independent.
No private filenames or identities are included here.

Offline installed assets are `2026.10.06.0000.0000`. TerritoryType's typed Bg link
selects `bg/ex3/01_nvt_n4/goe/n4g1`. The collision index SHA256 is
`0d8a4d8af2c4235b89bc6243a0e921685162ade71a9e8981661ebd6814087d3e`.
Its three entries are tr1717, tr1718 and tr1817. All node/polygon counts matched
the bounded binary decode. In tr1717 (SHA256
`a8188a8825cfcc70336ef64e49f9ed9668de9ed469e42ef8516c8ac7e1d3f2f0`),
eight upward supporting triangles of material `0x7004` cover a square from
X/Z 80.499917 to 119.5, total projected area 1521.00162. Sixteen perimeter
triangles of material `0x27004` surround it out to 79.9/120.1. These support a
center `(100,100)` and half width 19.5 independently of player positions.
The material difference is supporting evidence, not an independently decoded
server death rule. The guide establishes the lethal perimeter qualitatively.

The boundary SGB `sgbg_n4g1_a1_gmc01.sgb` (SHA256
`52e474be93e1de3fb941972d307184f4a1c4202bec2519340888a20559088a0c`)
is placed at `(100,0,100)`, unit scale, and has four edge lights at local ±19.8
plus `b1593deth1_u.avfx`. The layout's square trigger at the same center has
scale `(20,3,20)`; its scale alone was not used as a floor measurement.
No holes occur in the supporting square. The module preserves these bounds
through Dimensional Shift, Fragor Maximus, the add phase and the return. The
available recordings show scene changes on this same platform; physical
phase-dependent collider enablement has not been independently measured.

The collision reader follows the pinned [terrain extraction contract](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneExtractor.cs)
and [streamed collision structures](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Collider.cs).
Raw proprietary assets and identifying originals remain private.

## Handling and timing

| Mechanic | Implementation contract |
| --- | --- |
| Eden's Flare | Donut 5–60, boss cast location, available from cast start. |
| Pure Light | Forward rectangle 60, half width 30; current cast origin/facing handles each corner. |
| Delta Attack | Helper Blizzard circle 10; two Thunder rectangles 40, half width 5; Fire target spreads 5. Each helper retires independently. |
| Pure Beam | Each helper cast supplies a rectangle 60, half width 2, locked cast origin/rotation. All concurrent casts are retained. |
| Paradise Lost | Each helper supplies a fixed cast-location circle 6. Independent lifecycle from beams and Pure Light. |
| Vice of Apathy | Icon 28 immediately marks spreads 6; action 17640 removes matching target. Predicted delay 6.1s from icon. Black-hole OID `0x1EAE20` contributes persistent obstacles of radius 5 while present and not event state 7. |
| Vice of Vanity | Icon 172 immediately marks two boss-to-tank rectangles 100, half width 3. Predicted delay 6.6s. Visual completion retains both baits until both distinct helpers resolve. |
| Spear / Heavensunder | First cast predicts both tank hits. Second activation is first cast finish +1.46s; actual Heavensunder removes it. Interrupted first casts clear the second prediction. |
| Raidwide casts | Eden's Gravity, Dimensional Shift, Fragor Maximus and each Guardian's Mana Burst supply damage predictions. |
| Guardians | Prioritize targetable adds. Untargetable creation supplies a conservative landing forecast; actual Paradisal Dive casts refine it. North/south one-yalm arrival regions implement the guide's positioning strategy, not calibrated damage-falloff boundaries. A bounded nine-yalm front-half policy keeps non-targets behind active Guardians; this is not a measured Mana Slice cone. |
| Eternal Breath | Arena scene animations provide initial/later cinematic damage estimates; actual Eden Primeval Stasis refines timing. Helper Eternal Breath or encounter completion clears it. These newer cue offsets still need original-replay and live acceptance. |

Dimensions are Action/Omen-sheet-supported models corroborated by the guide's
mechanic families, not calibrated physical hitbox acceptance. Sheet evidence
dataset SHA256 is `6f98b919501d005d944c4840e714df456143dd4cb3a60285f4463a666637d86b`
for the September build. The October replay is a separate applicability check;
identical historical assets are not assumed. Flare's omen is `gl_sircle_6005at1`.

September sequence citations: Spear 4111 → hit 5628 → Heavensunder 6031;
Apathy icons 9386–9390 → visual start 9413 → helper hits 11680–11683;
Vanity icons 17874/17876 → visual start 17892 → visual action 19844 →
helper hits 20145/20163; Pure Light 26889 → hit 30536 → end 30537;
Delta helper starts 35274–35284 → hits 38119–38139; beams begin 46716–46719,
ground circles 48475–48479, overlapping corner cast 52139. These are recorded
client observations, not last-safe server deadlines.

All cast-driven hazards use native NPC cast correction (+0.3s), with no extra
greed delay. Icon delays are conservative estimates relative to observed hits:
Apathy ~6.146s and Vanity ~6.857–6.908s in the September source. Exact aim lock,
server snapshot and last-safe departure remain unmeasured. Predictions become
visible immediately at their reliable cast/icon cue and carry future activation
to both AI consumers; they do not impose an extra immediate dodge rule.

Cancellation/reset handling includes native cast retirement, cancelled visual
assignment clearing, actor-reference checks against recreation, separate helper
completion, and a fresh component set for a new module instance. The native
dispatcher does not send player destruction callbacks, so stored assignments
are also cleaned in Update. No movement controller or manual-control override
was added.

## Earlier native validation and current regression scope

- Current working-tree Release build passed with SDK 10.0.401 and API 15 dev
  assemblies: zero errors, six existing obsolete-gauge warnings.
- The earlier `tests/EdenPrimeRegression` run passed 47 synthetic assertions,
  covering generated registration and the then-current 19 components, four corner rotations, danger/safe probes,
  arena bounds through scene transitions, concurrent hazards, duplicate icons,
  cancellation, actor recreation, two buster hits and fresh-pull state.
- That earlier candidate's native replay has 26 available samples, complete export, zero module errors,
  zero missing samples or skipped operations, matching offline game version.
  Checked early warning, action/end separation, per-helper retirement, overlaps,
  add targeting and the second tank hit. Two native navigation fixtures returned
  finite in-bounds destinations with positive modeled leeway; this is a next-step
  diagnostic, not a complete physical route or live XCAI proof.
- Nine revision-bound archive actor inventories were exhausted and mapped with
  explicit ignored/unresolved observations. This is scoped action coverage, not
  exhaustive status/environment or alternate-pattern acceptance.

The earlier native summary in `/tmp/xbmr-eden-20261014/validation-summary.json`
explicitly checks 19 components. Its source manifest contains only
`E01EdenPrime.cs` and `ViceAndVirtue.cs`; it does not include `Intermission.cs`.
The later `pass2/` directory contains typed sheet and TMB asset research, not a
new native validation report. These historical results must not be attributed to
the three subsequently added intermission components.

The current source activates 21 components, including `ParadisalDive`,
`GuardianRearSafety` and `EternalBreath`. The regression suite now expects 21 and
adds 29 targeted assertions. All 76 assertions passed against the combined
7.5.6.35.4 Release build, alongside all other repository encounter suites. The
full current working-tree build passed with zero errors and six existing warnings.
The new cases drive native actor creation, casts, targetability, scene animation,
effects and destruction. They cover simultaneous landing deadlines, conservative
mid-phase bootstrap followed by an actual-cast refinement, cancellation and
arrival cleanup; Guardian rear-policy bounds, target exemption, current-cast
suppression and destruction; and initial/later cinematic cues, actual Stasis
refinement, effect/completion/source-loss cleanup and reset. They explicitly keep
positioning policy separate from physical damage-boundary acceptance.

Earlier 19-component module-source manifest SHA256:
`a1a9436dd386c7c9a42dbe817dfc62a81913f0f28b5b2206ee75d846a2eb7246`.
Earlier 19-component candidate DLL SHA256:
`14cfee7eab2b44e87b633a76178baee863568db3fc9367038a4b3fbd6d947e32`.
Private evidence, requests, failed/successful reports, asset facts and preserved
original are under `/tmp/xbmr-eden-20261014/`.

Remaining material gaps: calibrated Paradisal Dive damage falloff, actual travel
feasibility into the authored arrival regions, Mana Slice's physical cone angle,
and original-replay/live validation of the newer landing/rear-policy/cinematic
predictions. The current arrival regions and rear envelope are conservative
positioning policies, not independently measured damage eligibility.
Eternal Breath currently uses initial scene+48.5s, Stasis+43.4s and later
scene+18.5s estimates. Historical event-object animations cannot be recovered on
mid-cinematic bootstrap; source-backed loss is cleaned, but an event-object-only
cue with missing later resolution has no bounded stale expiry. Primeval Stasis
transition timing and Sunder Pressure puddle effect timing also remain unverified.
Live BMR/XCAI behavior, server snapshots, full physical boundaries, late-load
icon recovery and unobserved wipes/alternate overlaps are unverified. WIP is
retained for these specific omissions; build/replay success does not promote it.
This work is included in the 7.5.6.35.4 source release. No installed-plugin copy
or manual reload was made.
