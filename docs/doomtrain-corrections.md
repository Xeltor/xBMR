# Hell on Rails / Doomtrain corrections

Authoring and offline checks: 14 October 2026. Normal duty 1076, territory 1307.
This is a local development correction, not a release or live gameplay acceptance.

## Final explosion forecasts

Arcane Revelation's moving indicator does not damage players along its route.
Its previous permanent bait circle could make AI follow irrelevant moving danger
and enter a corner pocket. The module now marks the **final explosion destination**
with a future activation time, and leaves the harmless travelling indicator out
of forbidden zones.

Boss actions 45656 and 45657 identify two and three steps around the diamond.
Initial observed movement distinguishes the two travel directions. The destination
is the starting vertex plus that signed step count; the lower-deck diamond vertices
are `(100, centreZ-10)`, `(95.2, centreZ)`, `(100, centreZ+10)` and
`(104.8, centreZ)`. These rounded vertices describe the recorded trajectory, not
arena edges. The first car-four explosion stays at the initial north vertex,
consistent with the supplied guide and the recording. That exception requires
observed Revelation/spawn history, so enabling the module mid-mechanic does not
invent a stationary blast.

Cue-to-impact observations are 7.984–8.033 seconds for two steps and
10.946–10.992 seconds for three steps. Forecast deadlines use 8/11 seconds minus
an explicit 0.2-second conservative margin. This margin is not a measured server
snapshot offset. The final helper cast replaces the forecast with its locked
location and corrected NPC cast timer, keeping the same margin. Resolution,
cast cancellation and source destruction retire the corresponding guidance.
Unknown direction, ambiguous indicators or an unrecognized starting vertex do
not produce an invented destination; the helper cast remains the fallback.

The forecast applies to both heights. It reaches the same native forbidden-zone
interface consumed by BMR AI and XCAI; no forced movement or manual-control
override was added. Future activation permits safe travel/uptime before the
deadline without forbidding the indicator's entire harmless route.

## Other repairs

- Replace the timeout-driven encounter timeline with event-driven components and
  recorded environment transitions. The old intermission setup did run on entry
  to its 70,000-second wait; it was the following timeline that was blocked.
- Keep the east/west edges bounded on every car. Cars three/five now author lower
  and upper physical floors in a shared navigation group. Head-on Emission and
  Plasma Beam are clipped to the affected floor and carry activation times.
- Raised portals on cars without upper decks do not create ground danger.
  Plasma Beam spawn forecasts are corrected by the actual short cast and clear
  on cancellation, resolution or destruction.
- Upper Electray uses the matching Action-sheet range of 25 instead of 10 and
  retires each turret separately. Ground Electray has no invalid layer declaration
  on the unlayered second car.
- The intermission retains the existing 14.5-radius playable circle; the external
  railway drawing is no longer offered as a disconnected walkable escape region.
- Treat Derail as lethal across the whole old car. The pad's observed open/close
  events expose a one-way connection to the next car; both cars coexist during
  that transfer and the lethal gap remains outside walkable bounds. The previous
  donut incorrectly marked the pad as a safe place to remain until impact.
- Derailment Siege / Battering Arms are shared towers, rather than stacks on an
  NPC. Arrival is due at the first pulse, 2.9 seconds before the long helper cast
  finishes. The soak persists across two pulses and retires on the third pulse's
  distinct action. The final long-cast event is separate from those damage pulses.
- Aetherial Ray's three observed icon-to-hit delays are 7.65–7.67 seconds, rather
  than the previous 9.7-second prediction. Its bait follows the live train and
  selected player; it is not a fixed-destination explosion. Source/target loss
  clears the bait, and unrelated Stop statuses cannot replace its source.
- Predict the actual no-cast end-of-intermission raidwide and Shockwave from their
  preceding visual actions. Initial Runaway Train transport is an intermission
  instruction, not a predicted damage event. Windpipe respects immunity.

## Evidence and bounded validation

The supplied encounter guide is an independent qualitative description. All
recorded implementation tuning uses **one** source, not independent holdouts:

- Archive ID: `14bb654baef1a55f8946333f3ab4de9b897a29fcdbdbcec6d87c0c4e2ecfbc23`.
- Original SHA-256: `63e5e454497a5c85e39120027956ba41d02368e3cb9ee4c77e85dba608ef725c`.
- Retained revision: `b1e9cdcff6c43991f8e2714120925b4d74d49b304de9b656c645a0f9dcf47165`.
- Recorded 17 September 2026; writer BMR 7.5.6.35, game
  `2026.09.15.0000.0000`, projection schema 1, decoded through EOF.

| Cue sequence | Helper lock sequence | Resolution sequence | Final centre X/Z | Cue-to-hit seconds |
| --- | --- | --- | --- | --- |
| 143937 | 145447 | 146632 | 99.992 / 239.979 | 7.984 |
| 147882 | 153502 | 155028 | 104.784 / 249.989 | 10.992 |
| 175157 | 178322 | 180174 | 99.992 / 309.987 | 8.024 |
| 181713 | 186280 | 187849 | 95.170 / 299.977 | 10.946 |
| 216890 | 221979 | 223367 | 104.784 / 299.977 | 10.948 |
| 224702 | 227493 | 228954 | 95.170 / 299.977 | 8.033 |

Indicator aliases 119, 131 and 160 supply the full retained trajectories. The
six forecasts include two-then-three and three-then-two orders. All recorded
initial travel uses one direction; the opposite direction has synthetic checks
only. Forecast centres are within 0.04 yalm of the recorded locked centres.

Arena transitions are cited at sequences 30744, 64283, 92186, 100505, 130361,
162430, 166486 and 169381. Car-four tower helper/pulses occur at 156292,
159561, 159859 and 160260; the long helper event 160423 contains a separate
non-damage effect tuple. Both car-five towers use the 47236/47237 pulse family.
Ghost-train icons are 104981, 108659 and 115549; Ray resolutions are 108240,
113282 and 119881.

Matching archived Action/Omen data supports Hail radius 16, tower radius 5,
Electray range 25 and Aether Surge range 30 with a 45-degree cone omen. These
fields do not independently prove physical hitboxes, world placement or snapshots.
The archive deliberately does not apply its 7.5.6.34 effect interpretation to
the different 7.5.6.35 writer; the exact effect words were retained without
claiming verified damage amounts.

The selected discovery inventory exhausts three revision-bound pages and 777
create/cast-start/map/icon/timeline observations. A frozen declaration review
records 178 handled observations, 22 explicitly ignored supporting starts and
577 unmapped observations, including player casts and undecoded scenery cues.
This is not a full encounter coverage percentage or proof that declarations work.
Actions, statuses, physical variants and resets are separate evidence dimensions.

Validation on the frozen final build:

- Release build succeeds with the pinned .NET 10.0.401 SDK and Dalamud API 15
  development assemblies: zero errors and six existing obsolete-gauge warnings.
- 74 native synthetic assertions pass, covering floor predicates, lethal edges,
  car/intermission bootstrap, pad lifecycle, simultaneous caster retirement,
  two/three-step forecasts in both directions, cancellation, reset and immunity.
- 340 paired baseline/candidate samples pass with zero candidate module errors,
  missing samples or unsupported outputs. The baseline reports 14 module errors.
  Numeric projections and timestamp-group updates agree between the two builds.
- Another 24 final-build samples inspect six early forecasts, helper locks and
  resolutions. All six early centres agree with the independently recorded later
  locations within 0.04 yalm. Sampled warning lead is 7.216–10.181 seconds, and
  forecast deadlines precede the observed effect by 0.146–0.233 seconds. Native AI
  point predicates cover the final 16-yalm blast rather than its travel route.
- The original checksum is unchanged. The frozen tested DLL equals the normal
  development output; no installed plugin directory or manual reload was used.

Final module source-manifest SHA-256:
`1c0cd665828f8d9281318b45845a9f8cce2f6792093d2748b3b8cbb9294ba024`.
Candidate DLL SHA-256:
`c2991ee416a1dbf7aeffa04ad9ed531d68695ea5518edea13c655e5803d4f170`.
Candidate assembly MVID: `701feb46-428d-4ed6-9fc6-2dcd880a5b7f`.
The declaration-review contract is
`50eabe250d3d18854222e0e8a3b0d7908bdd7a7bb8b889a112fdee13f69208a0`.
Private reports and original paths remain outside Git under `/tmp/xbmr-doomtrain`.

## Follow-up from the 14 October live run

The user reported failed drops, one missed upward jump followed by a pillar stall,
a box-corner snag, and an unclear rear escape pad. The new closed original is
archive recording `db40ded55b6465e4c5c57659f48af17190580bc04bf14c54447d87221e558b7c`,
SHA-256 `cb9fa3da27d2ced26027e06ccd122b6203b6f7ca697d1c3441847452d566ec98`.
Both this original and the earlier recording are declared training sources; this
is not an independent held-out acceptance run. The corresponding XCAI diagnostic
segment retained 1,627 snapshots without a partial start or dropped entries.

Written normal-mode guides were checked before the correction:
[Thonky](https://www.thonky.com/final-fantasy-xiv/duties/hell-on-rails) and
[Icy Veins](https://www.icy-veins.com/ffxiv/hell-on-rails-trial-guide).
They describe using ground pads to ascend, falling back down for the headlight,
using boxes to block turret shots, and escaping Derail through the rear pad.

### Placement and physical observations

Local assets exactly match this recording's `2026.10.06.0000.0000` game version.
Typed normal-duty layout entries in `bg/ex5/01_xkt_x6/fld/x6fg/level/planmap.lgb`
place the four upward triggers at `(96.5,0,205)`, `(103.5,0,205)`,
`(96.5,0,310)` and `(103.5,0,300)` (instances 12105357/59/61/63).
Each has cylinder shape 3 and scale `(0.75,0.2,0.75)`. Corresponding background
pad placements agree; recorded launches corroborate their location. These typed
fields identify the authored trigger, not a universal proof of server collision
semantics or enabled state. The normal rear trigger is at `(100,0,262.5)`
(instance 12108340, scale `(2,1,1)`); observed map index 10 controls availability.
`planmap.lgb` SHA-256 is
`244a734012d1f5115a806d761d0cb7ceb41f2113c66c33ef539808596d526512`;
`bg.lgb` SHA-256 is
`9253281aab80a98d0c3321f7a0149dbd365bf90d037b826868fa70a8e8741b12`.

At raw sequence 49200 the player is on the ground at `(104.680,204.750)`,
but the old native path returns upper-deck `(107.750,204.750)` with zero transfer
cost. The diagnostic frames show approximately two seconds of attempted movement
against the pillar before manual correction. The corrected native path first
returns `(103.750,204.750)`, inside the placed trigger, then the raised endpoint.
Pad proxies now use radius 0.75 and include 0.6 seconds for the launch.

Drops now have directed inward connections through the platform's inner edge,
away from the up pad. Narrow routing barriers separate the floors on their shared
2D map and prevent walking up a vertical wall. They sit on the platform side so
a character already pressed against the pillar can retreat to the pad. The
connections are movement-planning edges: the character physically walks off the
inner edge; no game teleport/jump action is executed by the component.
Recorded car-three and car-five descents took 0.558 and 0.548 seconds from leaving
height 5 to the ground. Drop edges allow 0.8 seconds. That allowance is conservative
relative to these observations, not a verified server snapshot/latency budget.
The upper rectangles now agree with the normal layout zones at x=92.5/107.5,
half-width 2.5; car-three half-length is 10 and car-five half-length is 5.

The box snag occurs at `(94.788,160.527)`, raw sequence 31307. Bounds alone let
the planner skim the nominal x=95/z=160 corner. Two temporary obstacles now add
0.5 yalm of physical clearance around the boxes; native rasterization supplies
its usual additional grid margin. A separate 0.5-yalm inward car-edge margin
also avoids the recorded car-five wall stall near z=285.533. These are terrain
clearance rules, not enlarged damage zones or relaxed lethal boundaries.
From the earlier approach (sequence 30600), the corrected path goes through
`(93.750,161.250)` before taking cover, with 0.087 seconds of native leeway after
the native one-second activation reserve. This late probe has little spare
margin; it does not establish a server last-safe departure time. From the already
snagged position, the planner first backs away and then routes around the box.

Derail now announces the rear pad at cast start and draws a filled marker,
thick foreground outline and direction line that remain visible over the full-car
hazard fill. The text distinguishes preparing for the pad from using it once
open. The connection still requires the observed open event, includes transfer
cost, and clears on closure/phase transition. The pad remains inside Derail's
forbidden area at impact: the highlight identifies an exit, not a safe place to wait.

### Follow-up validation

- The current working tree builds successfully with SDK 10.0.401 and API 15:
  zero errors and six existing obsolete-gauge warnings.
- 171 assertions pass, including native pad approaches and inward descents on
  all four platforms, drop travel cost, warning reserve, box-corner segment
  clearance, car-five wall clearance, escape routing, warning cleanup and the
  landing interval between pad closure and the car-five event.
- 55 paired original-replay samples, including 13 native navigation pairs,
  have zero module errors, missing samples or unsupported outputs. The numeric
  projection and timestamp-group updates agree between baseline and candidate.
  All exported native parent chains are complete. The scoped export is complete
  with matching game assets, but writer-build authentication and physical/live
  acceptance remain false.
- The tested frozen DLL equals the normal development build output; the original
  checksum is unchanged. No replay cache was opened in the live bridge.

Sanitized checksums and navigation results are retained in
[doomtrain-movement-validation.json](doomtrain-movement-validation.json).
Module source-manifest SHA-256 is
`ef352c2cc16b212962fb00ce3c7fe7feccd4ee11335f19d6db1925541c0f25a7`;
DLL SHA-256 is `11f1114139f73c699d8c90ab4f218d4d3a993502141aed9a4fa37a0a26b02b24`;
assembly MVID is `58165e14-1952-40e2-a763-c1794580dde3`.
Detailed reports and identifying original filenames remain private under
`/tmp/xbmr-doomtrain-followup`. The bridge observes the development plugin loaded,
but its plugin description does not expose an assembly MVID, so this is not proof
that the running instance has this exact build identity.

## Remaining acceptance

Personal live testing is optional under the current maturity policy. Actual
upward activation, inward drops, box-corner clearance and rear-pad visibility
remain unverified as live execution outcomes. Native path success
cannot prove the character crosses a trigger or completes a fall. Server snapshots,
last-safe departure, latency/cast-lock margins, alternate overlapping hazards and
real wipe/re-entry behavior remain unverified. The other car/intermission bounds
and Lightning Express / Windpipe displacement retain their earlier evidence limits.
Early turret glow has not been decoded into a reliable earlier side-selection
cue; turret AOEs currently start at their observed five-second cast.

The earlier recording's asset mismatch and schema-1 omissions still limit the
previous explosion-forecast checks; the new recording's matching assets do not
retroactively repair them. Both recordings informed development, so neither is a
held-out acceptance source. Maturity is unchanged. No release version, tag, feed
entry or installed binary directory was modified.
