# Ketuduke: initial Variant module

Implemented on 2026-10-14 in
`BossMod/Modules/Endwalker/Variant/V3Aloalo/V32Ketuduke/`.
This is Aloalo Island **Variant**, duty 961 / territory 1176, boss OID
`0x4091` / 16529. It does not replace the Criterion/Savage modules, whose primary
OIDs are `0x409C` and `0x40A3`.

## Arena first

The module supplies a square arena centered at X/Z `(-790, -395)` with half-size
20 yalms. `BossModule.CalculateAIHints` passes these bounds to native pathfinding;
they are not merely a radar drawing. They apply from activation on every route,
and a fresh module starts with the same bounds after death/reset.

The guide describes a perimeter damage-over-time zone, not an instant-death
cliff. The initial module excludes that perimeter through the arena bounds;
it does not invent a perimeter activation timer, shrink the arena by a guessed
margin, or claim physical calibration. The center and half-size come from the
separately pinned normal-Variant reference below. Recorded player positions and
passing nominal-edge probes do **not** independently measure the physical edge.
Verify the floor/perimeter alignment in game before calling it accepted safety.

### Independent arena corroboration, 2026-10-14

The installed recording-matching game `2026.10.06.0000.0000` now supplies
independent layout/mesh support for the existing dimensions. Typed
TerritoryType1176 selects `ex4/09_ocn_o5/dun/o5d1/level/o5d1`. BG instance10074749
in `bg.lgb` places `o5d1_a1_bos01` at `(-789.9998,14,-395)`, with zero rotation
and unit scale. Its explicit collision link is
`bg/ex4/09_ocn_o5/dun/o5d1/collision/o5d1_a1_bos01.pcb`, SHA256
`818106965f2b3ccde1ea919d8ee5b89f9f93fa289f113ab39f97493e1b031eaf`.

The complete decode matches241 nodes and1526 triangles. The horizontal
material0x27005 annulus at localY0 has40 triangles, projected area approximately
607.9992y², a closed inner boundary at localX/Z±20 and outer boundsX±23/Z±24.
The boundary loops have16 and24 vertices respectively, all degree2 after
0.001y rounding. Its transformed centre agrees with the module to0.0002y, and
its inner square corroborates the existing half-size20 independently of player
positions. Collision material meaning, server DoT eligibility and phase enablement
remain separate from this static geometry result. No geometry change is needed.
See `aloalo-arena-geometry.json` for sanitized provenance and primary format links;
raw assets/readers remain private under `/tmp/xbmr-aloalo-arena-20261014/`.

The previous suggestion to require a personal in-game perimeter check is
superseded by `docs/module-maturity-policy.md`: available replay/offline evidence
can be sufficient, with material uncertainties evaluated individually.

## Source and evidence

- API and integration: current local xBMR working tree, SDK 10.0.401, Dalamud
  API 15 development assemblies. Existing unrelated local changes are preserved.
- Supplemental reference: [awgil/ffxiv_bossmod V032Ketuduke](https://github.com/awgil/ffxiv_bossmod/blob/40b0abdd35e81517409bdc81446910f943a75a49/BossMod.Modules/Endwalker/Variant/V03Aloalo/V032Ketuduke.cs),
  SHA-256 `e0068c1c5a99db4f807d74f7e3921c9584940b6de04417cd445b63c3b17dfbe1`.
  The repository's BSD-3-Clause licence and Andrew Gilewsky copyright notice
  remain intact. This implementation adapts the mechanic conventions rather
  than copying the incompatible `ModuleInit` / enumerable-AOE API.
- Maintainer-supplied [Aloalo Island guide](https://ffxiv.consolegameswiki.com/wiki/Aloalo_Island#Soul_of_the_South_Seas:_Ketuduke),
  including four route branches. This is descriptive community documentation,
  not a measurement of geometry or server snapshot times.
- Archive Action rows from game `2026.09.15.0000.0000` corroborate nominal circle
  ranges, rectangle length/width fields and action identities. The recordings
  are from `2026.09.01.0000.0000`; historical asset equivalence is unverified.
- All six previously inspected originals are **development/regression inputs**.
  They are not pristine holdouts, and distinct file digests do not establish
  independent parties or trials. Schema projections of one original count once.

The selected schema-3 recording IDs are:

| Recording ID | Declared original SHA-256 |
| --- | --- |
| `a88fd1873cd07a9ffbe7a3b418c25bbf112bda41eeac908f9cb37ab9e9950993` | `e07736606389af510f15fc098873d4f84a51523793aecaf864b78b054c7f302b` |
| `ad1feddcf00998b092c4c445f837d9e5e370efd07caea77d24a09718590597b0` | `1dcf7a74821b5749f3d8283e18d8899c1b927fa83dd7183ead299b8dd4b29b56` |
| `fa3fd6d97e9d034af9c287f84dd04808a6c3de21df3b9d37771d009e8a29b2d0` | `c64bba787dab77eea82f39989609fd2c51077599fcf4530a9da609f3870d9d4d` |
| `3f206416686538821a38248d3106395b141184995eb783a8460fc03f1a3f794d` | `ff4f4df7ca6e320fccdae82bc86dc49f4a4d845afdd2bb74b529199e18c6a78c` |
| `c72727aca8df5798c20f69ca0964499190425ffca4a35fe0134d9b1f2251e4a8` | `34085af07dad6b1acaba0a3b18851b2d55b7edf4ab37290b7dea280a654d405b` |
| `4f66454af9c2106ac7f5a5e3091161d020f022c4bc439e63a89653b615f1bbf8` | `213fd770eefc9e2a5e083844c7c850f13b89d552ad01f9f83434322ae1b5205d` |

The first source's reviewed primary actor is alias 103, created at sequence
120457. Its retained revision is
`69ea2a492c5e5c532618ec4f6dde91772ae09c1348d0d3fddea03ec5903d6972`.
All selected non-movement event pages were exhausted before aggregation; source
aliases are resolved separately for each recording. The primary-only
`implementation_coverage` comparison declares handling/exclusions for 20 token
groups. That declaration does not certify helper coverage or implementation.

## Implemented mechanic contracts

| Mechanic | Trigger, lifetime and guidance | Limits |
| --- | --- | --- |
| Tidal Roar | Visual 35493 forecasts one raidwide impact at native cast finish + 1 second. Helper 35494 retires it. One party mask includes companions without duplicate cumulative predictions. Interrupted visual removes its forecast. | Client helper timing, not a measured server snapshot. |
| Bubble Net | Helper cast 35457 forecasts raidwide damage. Visual cast 35456 suspends static crystal forecasts. | Route-specific Bubble Net variants are not yet implemented. |
| Spring Crystals | Distinct crystal objects carry distinct forecasts. Appearance actions 35450/35451 start a nominal +14-second static deadline. Bubble status 3745 / extra 200 invalidates that wave's static deadline. Directional helper cast 35461 predicts a 20-yalm shift for bubbled crystals and a deadline at wind finish + 3.1 seconds. Actual Saturate casts refine the origin and deadline. Each 35452–35455 hit retires its own crystal only. Destroyed/replaced objects are pruned. | Early forecasts are reference/event-derived estimates. During the bubble-to-wind gap, guidance is deliberately unavailable. Bubble Net cancellation and unobserved route interactions need further evidence. |
| Saturate geometry | Spheres use nominal radius 8. Early rectangle forecasts are centered 76-by-10 lines. Actual rectangular casts use their recorded **front-origin cast location**, which differs from the crystal position by about 38 yalms, with length 76 / half-width 5. | Nominal geometry is not a measured physical hit boundary. |
| Strewn Bubbles | Visual action 35462 anchors activation at +7 seconds. Each marker 0x1EB936 records an original 20-by-10 lane. Helper 35463 matches and retires one lane at a time; moving/disappearing markers do not prematurely erase the trail. | Late activation without the visual anchor has no fabricated forecast. Missing helper and cancellation variants remain gaps. |
| Twintides | Cast IDs choose circle-then-donut or donut-then-circle; no fixed alternation is assumed. Only the current step is dangerous. The first impact exposes the second step at +4.1 seconds; its actual cast refines the deadline/origin. Duplicate first hits cannot consume the second step. Interrupted current cast clears dependent state. | Nominal radii 14 and 8–60 come from reference/Action evidence. Physical boundaries/snapshots remain unverified. |
| Hydrobomb | Each 35490 location-targeted helper cast supplies its own radius-5 warning/deadline. | Future bait locations are not guessed. |
| Hydroblast | Visual cast 35491 supplies a radius-5 tankbuster bait, including party separation. | Reference-based; no complete tankbuster cast occurs in the selected samples. Post-visual helper 35492 timing/eligibility needs a longer capture. |
| Blowing Bubbles | Each Airy Bubble 0x4095 supplies a moving current-position warning; duplicate creation, individual despawn, actor replacement and late component activation preserve object identity. | Radius 5 is a reference avoidance policy, not a calibrated capture radius. This does not forecast a continuous moving interception or prove safe travel. |
| Route 1 Water III | Apa 0x4113 tethers 57/1 identify the assigned player and reported stretch state. ID 57 adds a bounded preference for increasing distance and a text warning; ID 1/unlink stops that preference. Cast 36116 separately predicts damage to the assigned native party slot. Cast finish/unlink preserve successful pending hits; helper 36125/36126 retires only its matching source/recipient. Interrupted cast removes its forecast. Endpoints are checked by object identity even without player-destruction callbacks. | No assumed 20-yalm safe circle, calibrated proximity falloff or enforced arrival deadline. The damage category is neutral; downstream healing/mitigation consumption is unverified. A finish with no matching helper remains unresolved until invalidation/reset. |

For example, source 1 has wind starts 131734 (-X), 142311 (-X with Water III)
and 147247 (+X); rectangular Saturate starts 128030/128031 demonstrate the
front-origin location. Lanes resolve separately at 134931–134934. Apa cast
142060 is followed by visual 143509, unlink 143546, and helper 36126 at 143555.
Those are separate lifecycle boundaries, not one damage snapshot.

## Route coverage and remaining work

All six sources contain Zozone's summon 35470 and Apa's Water III 36116,
consistent with Route 1 (no fishing / do not assist Zozone). The initial module
uses `Contributed`, not `Verified` or `AISupport`, and displays a persistent
global warning that routes 2–4 mechanics are not implemented. It does not change
XCAI, take manual-control ownership or start gameplay.

| Route | Available implementation/evidence |
| --- | --- |
| 1 | Arena, shared mechanics and partial tether/damage guidance; six development originals. Live movement acceptance still pending. |
| 2 | Arena/shared components only. Tidal Wave immunity/bind, Hydrosurge, Wavefoam and Anila transport require recordings and implementation. Shared crystal guidance alone is not safe route support across the persistent midline. |
| 3 | Arena/shared components only. Zaratan-facing, bubble/elevation eligibility and Hundred Lashings require recordings and implementation. |
| 4 | Arena/shared components only. Ogrebon/players bubbled versus grounded, Shock and Updraft require recordings and implementation; capture both eligibility outcomes when available. |

The maintainer is recording routes 2–4. No unattended monitor, route selection,
game action, release or feed publication was created by this change.

### First live feedback

On 2026-10-14 the maintainer reported that the initial module "worked great"
and initially identified the completed run as Route 2. The newly indexed boss
segment `9a4ef7f6893114f0d87cbd4e497395f8f13ff0167930586646c464a395453609`
contains summon 35470 and Water III 36116, with no Route 2 Tidal Wave/Hydrosurge
casts. The maintainer then confirmed they may have taken Route 1 again.
Treat this as positive **maintainer-reported Route 1 feedback**, not Route 2
acceptance or an independently measured arena/route-safety test. The new run is
additional evidence and was not used to tune the frozen implementation above.

## Validation

The full current working tree builds at the stable development output path
`BossMod/bin/Release/BossModReborn.dll`; Dalamud watches that output directly.
No installed plugin binary was copied or manually reloaded.

- 63 synthetic assertions pass: generated registration/activation, nominal
  arena edges reaching AI bounds, native death-phase cleanup/new module,
  both wind directions, front-origin rectangles, duplicate/independent instance
  handling, interrupted casts, both Twintides orders and repeated orders,
  assigned-player forecasts, bootstrap and player identity reuse.
- 17 native replay reports across six originals contain **728 available
  samples**, no module errors, no skipped raw operations and no unsupported
  outputs. Each original's bytes/hash were unchanged after replay.
- 96 actual crystal cast origins and native deadlines agree with recorded
  cast inputs; 192 nominal inside/outside probes agree with AI predicates.
  96 individual crystal and 24 individual lane resolution checks pass.
- 22 Twintides progression, 9 raidwide helper retirement, 6 Water III visual
  persistence and 6 Water III helper retirement checks pass.
- 18 native navigator fixtures use the 80-by-80, 0.5-yalm grid centered on the
  authored arena; all 14 selected destinations stay inside the nominal bounds.
  Four fixtures choose no destination. This does not prove continuous physical
  travel, casting cancellation or live XCAI controller behavior.
- **Preserved discrepancy:** among 96 early-to-actual crystal-center comparisons,
  the largest residual is 0.03376497594 yalms. Twelve exceed the prior trial's
  0.03-yalm diagnostic tolerance. These are recorded discrepancies, not passing
  exact-origin tests; the tolerance was not widened and hazards were not inflated.
- All native reports retain `export_complete: false` because the September 1
  recordings do not match the September 15 offline assets. They are bounded
  native evidence, not full historical or physical encounter reconstruction.
- All sources are development inputs. Pristine holdouts, physical boundary/
  snapshot calibration, real wipe/re-entry, all route branches, moving-bubble
  interception, and actual in-duty xBMR/XCAI acceptance remain pending.

Frozen module source bundle SHA-256:
`44669bc6b43fcfcf9d5cf5c93c964742713fc7bfe5144ca25a2fef23c6c64e51`.
Tested assembly SHA-256:
`0f7668728c748e3f4149bb8a1b8f50769ade511a1b44f2f7611dfb8d7e8edc28`.
The final working-tree build also passed with zero errors, and its watched
development DLL SHA-256 is
`1d8002736d2889bbbf77b2d36e03abc549bf7564430bb600d37c7c5790d66bf5`.
The module's four source files still match the frozen source manifest; unrelated
encounter work can change the aggregate DLL identity. The 63 synthetic Ketuduke
checks pass on this final development DLL too, and the existing module harness
passed 1,251 native assertions. The five Python repository tests also pass.
The aggregate receipt is in `ketuduke-variant-validation.json`. Detailed private
reports/requests, original-to-digest mapping and code manifest remain under
`/tmp/xbmr-ketuduke/`; original recording filenames are not committed.

Run synthetic checks with the pinned SDK after building the plugin:

```sh
/home/xeltor/.local/share/xbmr-dotnet/dotnet build tests/KetudukeVariant/KetudukeVariant.csproj -c Release
/home/xeltor/.local/share/xbmr-dotnet/dotnet tests/KetudukeVariant/bin/Release/net10.0/KetudukeVariant.dll \
  /home/xeltor/Projects/xBMR/BossMod/bin/Release/BossModReborn.dll \
  /home/xeltor/.xlcore/dalamud/Hooks/dev \
  '/home/xeltor/Games/steam/debian-installation/steamapps/common/FINAL FANTASY XIV Online/game'
python tests/KetudukeVariant/verify-replays.py /tmp/xbmr-ketuduke
```

## Maturity reassessment after 7.5.6.35.4

Reviewed source base `bd9bf9228` under `docs/module-maturity-policy.md`. The
previous Contributed label described contribution provenance; it did not mean
all Variant routes were complete. The module is returned to WIP because the
[preferred normal-Variant guide](https://ffxiv.consolegameswiki.com/wiki/Aloalo_Island)
identifies concrete unimplemented responses on routes2–4:

- Route2: unavoidable Tidal Wave, persistent Hydrosurge and player bubbles for
  the Summoned Anila crossing while crystals resolve.
- Route3: select the bubbled Zaratan's frontal half during Updraft/Hundred Lashings.
- Route4: choose ground or a player bubble according to the Ogrebon's bubble
  state during Updraft/Shock.

The current source explicitly advertises those route gaps and lacks their
components. They are actual known omissions affecting safe responses, not
hypothetical undiscovered variants. Route1's prior acceptance evidence and
maintainer feedback remain valid for that covered behavior. No unsupported
route was implemented from guide prose alone in this batch.

The independent static arena corroboration above improves available evidence but
does not close route coverage. Personal live play, exact server snapshots and
pristine holdouts are not automatic promotion gates. No Verified promotion is
made while these known material omissions remain. The parent owns the next
current-tree build/test validation; historical hashes and counts above refer to
their explicitly identified earlier candidates.
