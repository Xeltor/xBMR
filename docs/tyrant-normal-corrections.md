# AAC Heavyweight M3 / M11N — The Tyrant

## Scope and maturity

Normal difficulty, CFC 1072, territory 1324, primary OID 0x4AE8. Reviewed source
base `bd76a4daa6a852045fc8fc604fad157f22e42916` (published 7.5.6.35.7), with
the current uncommitted launch-guidance corrections. Assembly/internal compatibility and release version
are unchanged.

Reopened from Contributed to WIP on 2026-10-14 following the maintainer's report
that many mechanics were missing and AI handling was inadequate. This repair
removes known source defects and adds early guidance. It remains WIP because
normal split-floor translations, precise launch/collision semantics and
last-safe timing have not received independent physical validation, and the
recording ends before Great Wall of Fire. The repository maturity policy does
not require personal maintainer testing; these are specific evidence gaps,
not a generic demand for live testing of every encounter.

## Written guides reviewed first

- [Icy Veins: AAC Heavyweight M3 Normal](https://www.icy-veins.com/ffxiv/aac-heavyweight-m3-normal-raid-guide)
- [Console Games Wiki: AAC Heavyweight M3](https://consolegameswiki.com/wiki/AAC_Heavyweight_M3)

Both cover **Normal**, all phases: three weapon shapes, chained jumps, chasing
spreads, Dance lines, shared tankbuster, meteor towers, tank interception,
two-rock shelter, split arena, mandatory launch towers, targeted platform
removal and repeated Heartbreak stacks. Savage timing, portal spacing and
platform offsets are not substituted for Normal.

Disagreements/qualifications: Wiki describes eight Dance hits; the retained
helper sequence contains seven (`46034`, five `46036`, `47081`). The boss
visual `46033` is separate. The component predicts the seven observed helper
pulses; the guide's total does not establish an extra damaging helper.
The tank named as Raw Steel/Great Wall target differs among descriptions;
actual icons/cast targets own assignments, not an assumed main/off-tank.
Both guides say launch immunity cannot prevent the mandatory launches.
Guides establish intended responses, not exact geometry or server snapshots.

## Recording and coverage

Archive recording:
`954347b97b5cb4eb96cc24b7dc417241d89ad2b4ceb197056e333f7c87adf490`.
Retained revision:
`1b44b148e391522c63f98fd95d886863328e4f3c5bdad92bfde1f1dac082f2c2`.
Declared original digest:
`889596041acf580aacaa3c681c4bb42d78e9921719fa7160d029930c3caf2853`.
Recorded 2026-09-13, game `2026.09.01.0000.0000`, BMR `7.5.6.35`, projection
schema 1, parser MVID `52cc4dc7-8b32-46be-8b49-2f03899b31c4`.

One original, all used for development; **no independent holdout**. EOF decode
complete, 213,582 decoded entries, 155,564 retained numeric events. Selected
combat-domain review uses seq 5703–212842, without claiming that a proposed
combat union proves pull boundaries. HPMP, render flags, frames, incoming
effects and other operations are omitted. The original is unavailable in the
local replay directory; a native callback reconstruction from selected numeric
rows is expressly **not** a full original replay.

## Mechanics and contracts

| Mechanic | Evidence/cues | Correction and AI contract |
|---|---|---|
| Crown / One and Only | 8201/9276; 98879/100720 | Raidwide healing prediction instead of fictitious avoidable side rectangles. One and Only follows the actual nine-second helper, not the six-second visual. |
| Draw Steel / Smashdown | Draws 11076, 16269, 26477; helpers 13413, 18421, 30537 | Axe circle8, scythe donut5–60, sword cross40/half-width5. Draw cue predicts before late helper. Position/facing follow boss until helper locks exact geometry. Draw finish +9.1s is a recorded prediction; helper refines it. |
| Trophy / Assault | Tether chains36965–36968 and48183–48186; starts36984/48204; spawn timelines36455–36457,47544–47546 | Timeline4561/2/3 maps sword/axe/scythe. Evolved follows native tether chain; Apex follows spawn order. First activation visual finish +2.3s; observed subsequent spacing4.15s/3.65s. Later weapons visible; active/next weapon owns hard guidance. |
| Void Stardust | Icons21513–21529; first hits23603–23619, repeats24554–24598/25901–25908; ground hits25202–27820 | Per-target three-hit state; separate chasing spread and ground circles. Ground hits cannot prematurely retire spreads. New sequence clears old state. Helper casts refine the first deadline; follow-ups2.1s. |
| Dance | First helper60742/62577; five46036 hits62765–63460; final47081 at63747; lines63804–63815 | Seven observed damage pulses predicted for healing. Both46035 and47033 lines handled at half-width5. Each helper retains its own deadline/cleanup. |
| Raw Steel / Wall | Raw icon70660, six target helpers70693–70700, buster73033 | Shared buster and actual target spreads. Great Wall watches damaging46075 rather than visual46074; aftermath46077/half-width3 is separately handled. Great Wall is synthetic-only here; not observed before kill. |
| Gust / tornado | Tornado creates86757/90601; gust waves91336–91347 and92350–92357 | Source-bound live tornado circles; no clearing all on one destruction. Gust uses individual helper geometry and future activations, avoiding the previous immediate union of both angular waves. |
| Meteorain | Towers103593–103596,112680–112685; tethers107300/107302 and117850/117852 | One AI soaker per meteor tower. Tethers can start on non-party rocks. Distinct tanks intercept distinct source-to-rock segments; single-target damage creates no false6y splash. Retarget preserves original8s deadline. |
| Double Tyrannhilation | Windup123719; first387.52s shockwave; second helper127128/128186; rock explosions127320–127335 and128464/128467 | Select far rocks whose complete shadows fit inside a nearer rock's shadow. Hold far cover through both hits. First forecast visual finish+1.1s, then actual second helper deadline. First cast finish must not clear shelter; exploding rocks retire individually. |
| Flatliner / floor | Helper130816/133411; director133439 `(0x8000000D,2)` | Radial15y launch, immunity ignored, alternating sorted living server actor identities assigned west/east for later tower coverage; destination tested against assigned floor with0.75y landing margin. Strip is not an immediate forbidden AOE that fights the mandatory knockback. Split bounds actually remove the gap from native pathfinding. |
| Majestic portals | Map cues137804–137807,155301–155304,194674–194677; helper140298–140302 etc. | Predict normal lanes from map spawn, about4s before helper and10.2s before hit. Split columns79/89/111/121 and east rows at131; restored columns85/95/105/115 and east rows at125. Helpers replace corresponding forecasts and own exact timing. |
| Fire and Fury / proximity | Helpers142926/142927,205322/205324; proximity142758/142765 etc. | Both opposite45-degree half-angle cones.22y proximity avoidance remains an inherited policy, not a measured lethal radius; actual damage range60y is not a whole-arena movement ban. |
| Launch towers | Four147025–147029 / hits151864–151873; two165600/165601 / hits170720/170722 | Ten-second tower deadlines;25y radial landing model, ignore immunity. Scope forbidden landing directions to each4y affected circle. Advertise only same-platform towers. Normal towers distribute one AI each; every player on doomed side must enter its tower. |
| Avalanche | West-facing cast16863446065; launch471.78s; west removal173790; toss17636946066; repair179847 | Aim launches at surviving side from facing cue. Keep both current floors until actual removal; never direct walking across the gap. Handle46065/46066 and alternate46069/46070. Toss is independently timed. Repair follows director1 rather than prematurely restoring on Crown start. |
| Heartbreak | Windups182641/187031; five pulses184853–186076 and188399–189923 | Full-party shared4y tower persists through visual finish and all five distinct pulses. Next pulse2s; first visual finish+1.1s. No stale tower retained until an unrelated future Majestic cast. |

All offsets above are client-observation forecasts, **not measured server
snapshots**. General native navigation adds its existing1s activation cushion;
launch AI deadlines also precede predicted activation by0.3s. Landing polygons
have a separate0.75y inset. These are conservative policy margins, not changes
to authored hazard dimensions or proof of safe cast greed. No manual-control
or XCAI ownership bypass is introduced; no immunity/action executor is added.

## Arena and asset provenance

[Selected asset and Action facts](tyrant-normal-asset-facts.json) bind the current
local game version, version-file hash, original LGB/SGB/MDL hashes and reader
identity. Territory1324 resolves to `ex5/01_xkt_x6/rad/x6rb/level/x6rb`.
The platform's model halves have local X bounds[-20,0] and[0,20], Z[-20,20].
Normal portal/tower layout places towers at(84,89),(84,111),(116,89),(116,111).
Normal uses the inherited6y translation, making floor rectangles[74,94] and
[106,126], Z[80,120]. Savage uses different values and is not a geometry oracle.
The initial floor remains[80,120]².

Visual model bounds, tower locations and the prior reference together support
this **nominal** floor model; they do not independently prove physical edges.
The linked floor BG entries expose no collision asset. Dynamic SGB translation
semantics and physical collision/edge measurements remain unverified. The
recorded launch at151864 is followed by a player falling beyond Z80, corroborating
that a diagonal launch can be lethal, not calibrating the exact edge. Launch25y
and Flatliner15y are inherited displacement models, corroborated by observed
motion and the guides; effects did not expose a usable Knockback-sheet link.

## Validation and remaining acceptance

`tests/TyrantRegression` exercises the current generated registration and native
WorldState dispatch, shapes, AI constraints, damage predictions, cancellation,
individual cleanup, phase reset and mirrored platform removal. It also uses the
native navigator to check a launch approach on the current platform. Optional
private callback input adds13 checkpoints over4,891 selected archived rows;
it deliberately excludes omitted state and is not labelled original replay.

2026-10-14 result: **135 assertions passed** (121 synthetic,13 recorded checkpoints
and one projection-completion guard), Release build passed with zero errors
and six existing obsolete-gauge warnings. These checks include actual native
forbidden-zone predicates and one native navigation approach, not live movement.

The [primary-actor token audit](tyrant-normal-primary-coverage.json) exhausts62
cast/action groups covering149 observations for actor87. Helper/environment
behavior is reviewed separately in the table above; this is not a full-encounter
coverage percentage.22 observations have direct primary-hook mappings,123 are
autoattacks or visuals whose hazards are owned by helpers/environment cues, and
four self-target animation observations (`46078`, `46081`, `46082`) retain
unresolved semantics. They are not silently advertised as verified handling.

Run the Release plugin build first, then:

```sh
DALAMUD_HOME=${DALAMUD_HOME} \
  dotnet build tests/TyrantRegression -c Release
dotnet \
  tests/TyrantRegression/bin/Release/net10.0/TyrantRegression.dll \
  BossMod/bin/Release/BossModReborn.dll ${DALAMUD_HOME} \
  "$FFXIV_GAME"
```

A fourth argument may select a private sanitized numeric callback trace; private
recordings and proprietary asset bytes are not committed. Build/result identities
are recorded in `tyrant-normal-validation.json`. The development DLL path remains
stable and automatically reloads. No installed plugin copy, manual reload,
version bump, tag, publication or sibling-repository change is part of this repair.

Remaining: independent normal floor/launch/snapshot evidence, original replay
with all state, Great Wall through resolution, additional Avalanche orientations
and patterns, and representative actual BMR/XCAI runs. The mirrored branch has
synthetic coverage; one observed orientation is not both. Matching current Action
fields does not establish historical game-asset equivalence. Successful build,
callback checks and navigator choices are separate from live survival/uptime.

## Live report: missed Avalanche evacuation, 2026-10-14

The maintainer reported remaining on the platform lifted and thrown by Avalanche
instead of using the meteor launch to reach the other platform. The earlier
diagnosis of empty towers described the first pull; it did not explain this
later pull. The current recording contains both attempts and must not be treated
as one pull.

The closed local v31 recording has SHA-256
`747a59508d98aa9390897cb7416e538fbe0a307e9062c9c5dfc977baa21d4cba`,
game `2026.10.06.0000.0000`, parser MVID
`5eeefa7b-ec03-42c1-accc-51c599bedefe`, 357,319 retained operations, decoded
snapshot to EOF without a parse error. DevBridge recording ID
`15eb328e2bf61542cd2d0f29` is a local lookup token, not an archive identity.
Archive searches by duty1072 and zone1324 at investigation time returned only
the September source; the latest local recording was used directly.

Relevant client-local observations in the **second** pull:

- Seq327369/327370,23:34:56.9528485: two Explosion46061 casts start at
  `(115.983765,88.97534)` and `(83.97034,110.97888)`, total9.7s.
- Seq329893,23:35:02.0956844: boss casts **46063**, facing-1.5708683 radians
  (west). Seq329894 starts helper **46064**, the thrown-platform rectangle.
  The module recognized46065/46069 and46066/46070, omitting this pair.
- Seq331972/331974,23:35:06.9434743: both towers launch three players. The
  local AST is absent from both target lists and remains west near
  `(77.557396,107.75933)`. Five seconds elapsed between the reliable visual
  cue and the observed launch; these are not server snapshot measurements.
- Seq334952,23:35:13.3748203: director state8 removes the west platform.
  Seq336218,23:35:15.4763316: local AST becomes dead after falling westward.
  Helper46064 resolves at seq339456,23:35:19.5844101.

XCAI capture `f5c91d14d1b146039f5515eb18f0b9e7`, revision
`cf34f3df2c974cc8bf7a4c37f8eb6b3d`, samples1532–1593 confirm no manual
movement suppression and no automatic move request during the launch approach.
The module's missing branch explains the absence of mandatory tower-entry and
surviving-platform landing constraints; this is a material module defect.

The repair adds46063/46064 and46067/46068 to the existing handlers. Current
local Action rows establish four matching visual/helper field pairs, with an
8s visual plus9.5s extra time and a17.5s,40-by-40 helper rectangle. Facing
selects the doomed side; numeric IDs do not override that cue. Only46063/46064
and the earlier46065/46066 branch have recorded coverage; the remaining pairs
have sheet and synthetic coverage.

The regression first failed against the previous DLL because46063 left an
occupied escape tower capped at one soaker. It now tests all four pairs with
the recorded remaining4.85s launch window and west-side starting position (east
cases mirrored), including mandatory entry, occupied towers, both landing
directions, native navigation arrival and thrown-platform retirement. All36
new assertions pass alongside the existing99. This is a repair informed by
live failure evidence, not a successful live evacuation with the corrected DLL.
The module remains WIP for the other documented evidence gaps.

## Radar sequence review, 2026-10-15

The maintainer reported that overlapping phase-one cones looked simultaneous,
leaving no apparent safe space on the radar. Written Normal guides above were
re-read: Wiki explicitly describes two sets of cones after the whirlwinds;
Icy Veins describes the tornado cones following Assault Apex. Their Savage
guides and bait assignments were excluded. Neither Normal guide establishes
precise wave timing; the numeric recording establishes the observed order.

The existing September source/revision above remains the available archive
source for this review. All48 action46041 start/action/end observations were
retrieved to the final page. Eight cardinal cone casts start at seq91336–91347,
ticks2955777152; eight diagonal cones start at seq92350–92357,
ticks2975879036. Both use total5.7s cast timers, with actions at
ticks3015429669–3015608205 and3035414281 respectively. Their roughly2.01s
forecast separation is preserved. These are client observations, not server
snapshots. The numeric fixture in `tests/TyrantRegression/GustCallbacks.json`
retains all48 selected callbacks and source identity; missing original state is
not reconstructed or advertised as a full replay.

| Reviewed display | Evidence and result |
|---|---|
| Powerful Gust cones | Both active waves previously used the same fill. Highlight the entire earliest wave and show later cones as translucent previews. Promote the next wave only as earlier sources retire. |
| Cometite ground circles | Three eight-circle batches start at seq23834–23841,24935–24943,26277–26287, approximately2.08s apart with2.2s timers. Apply the same sequence styling during overlaps. |
| Dance explosion lines | Six46035 and six47033 casts start together at seq63804–63815 but use5.7s and6.2s timers. Preserve their0.5s separation visually; retain both AI deadlines. |
| Majestic Meteor ground circles | Nine four-circle batches; seq137230–139050 shows the first three, approximately2s apart with3.7s timers. Distinguish overlapping batches by activation. |
| Queued weapon attacks | Later forecasts already had separate deadlines and non-current Risky flags, but identical fills. Highlight the earliest activation and preview only the next attack; retain further predictions internally. Preserve existing weapon AI ownership. |
| Majestic Meteorain portal lines | Predicted lanes and refined helper casts previously shared one fill regardless of activation. Apply the same ordering when their deadlines differ; same-wave lanes stay together. |
| Fire and Fury | Opposite cones46072/46073 start simultaneously at seq142926/142927 and205322/205324. Both remain hazards; do not present one as a later alternate. |
| Persistent tornadoes, spreads, tankbusters, towers, two-rock cover and Avalanche | Persistent hazards and simultaneous assignments are not later waves. Retain their existing displays, constraints and phase handling. No additional sequence-display defect identified in this pass. |

`Components.SequencedAOEs` and `AOESequence.HighlightNext` change presentation
only, with opt-in use by this encounter. They use configured `Colors.Danger`
for the earliest batch and `Colors.AOE` for later translucent previews. Later
fills draw first, preventing them from covering the earliest wave. The0.2s
batch window groups adjacent client-frame arrivals; it is a display tolerance,
not a safety margin or server timing claim. Geometry, activations, Risky flags,
all timed AI constraints and user colour settings remain intact. Shared default
components and other encounters do not opt into this change automatically.
The radar draws only the current wave and the next wave; further predictions
remain in `ActiveAOEs` and timed AI hints, becoming visible as the sequence
advances. This also prevents several translucent queued weapon shapes from
combining into near-opaque shading over the whole arena.

Radar repair checkpoint (before the launch-guidance changes): **178 assertions passed**:171 synthetic cases, including
all121 earlier synthetic cases, plus seven assertions over the48 recorded Gust
callbacks. The new cases cover arrival order, same-wave jitter, out-of-order
cleanup, promotion, cancellation, future-first draw ordering, weapon forecasts,
portal forecasts and preserved AI constraints. The optional September
4,891-row private reconstruction from the earlier run is no longer present in
scratch storage and was not rerun; its historical13 checkpoints remain separate.
Release build succeeds with zero errors and the same six existing obsolete-gauge
warnings. No live radar screenshot, corrected gameplay run or physical boundary
acceptance is claimed; the module remains WIP.


## Launch positions and island coverage — 2026-10-15 initial checkpoint

The maintainer requested Prishe-style tiny green launch positions and at least
two living players on each surviving island. Re-reviewed the Normal guide's
Flatliner, explosion-tower and Arcadion Avalanche sections above: ordinary tower
launches can stay on the same island or cross; the doomed island must evacuate.
The presentation reference is
`BossMod/Modules/Dawntrail/Alliance/A11Prishe/AuroralUppercut.cs`, specifically
`AuroralUppercutHint`. Its encounter geometry and knockback distances are not
reused.

A material assignment defect was found in the prior Flatliner implementation:
`PartyState.PlayerSlot` is zero for the local player on every client, so alternating
local party slots can send all clients west. Flatliner now alternates **living
server actor identities**, sorted numerically, producing the same 4/4 plan on
every client with the same eight-person roster. This does not establish a
role-balanced split or control players who choose another position.

Ordinary explosion waves default to **same-island landings**. Distinct living
actors, ordered by server identity, cover the local towers ordered north to south.
Only assigned tower users cross to repair a deficit below two players, and the
donor island retains at least two. These choices are cached through movement and
flight, rebuilding for new sources, cancellation, destruction, a changed living
roster or the destruction cue. An empty island's current towers cannot be reached
by walking; the module warns about this rather than routing across the gap.
Fewer than four living players, or insufficient launch sources to repair the
split, also produce a coverage warning.

Avalanche overrides ordinary coverage: every actor targets the surviving island,
and everyone on the doomed island must enter a local meteor even when occupied.
Required soakers see their assigned local tower with sharing allowed, preventing
an occupied-tower avoidance rule from contradicting evacuation or deficit repair.
The minimum is a **planned landing population**, conditional on players following
the assignments; it is not a promise that manual players will comply.

`LaunchPositionHints` draws small green disks (radius 0.2y) around the launch
source. They use the same assigned landing island as the AI and retain the
source's activation. For towers, an assigned soaker sees its own tower; an
unassigned player sees guidance only while already inside a tower. The marker
component contributes no hazards, forced movement or inverted AI goals. Other
active hazards and the knockback constraints remain authoritative.

For the nominal radial model, every point in each disk must land inside the
assigned floor's 0.75y inset. A conservative analytic deviation bound,
`r * (1 + distance / (startingRadius - r))`, adds an extra inset when selecting
disk centres. Flatliner centres are 2y from their source, meteor centres 3y.
This tests a complete disk, rather than merely a centre that happens to project
onto the island. It remains conditional on the documented 15y/25y radial model
and nominal floor bounds; it does not certify physical collision or snapshots.

The two-tower cue also starts preparation within 12y of the local meteor, before
the later facing cue decides who must cross. Its AI deadline is launch activation
minus 5.3s; the existing navigator keeps its additional 1s reserve. This is a
conservative preparation policy, not a measured server deadline. The October
failure has about 4.85s between the facing cue (seq329893) and tower effects
(seq331972/331974); asking a far-corner player to start approaching only then
would leave too little margin. After the facing cue, preparation is replaced by
the actual stay/cross assignment. Players already nearby retain the normal
future launch deadline.

Validation at the initial launch-guidance checkpoint: **14,365 assertions passed**,
including seven recorded Gust assertions and 14,358 synthetic assertions. Most
of the new count consists of boundary probes around green disks, not independent
encounter trials. The checks rotate all eight local party orders; cover ordinary
0/8, 1/7, 2/6, 4/4, 6/2, 7/1 and 8/0 populations; test both destruction sides,
occupied required towers, deaths, stable flight plans, later waves, cancelled
sources, helper disappearance and fresh-pull cleanup. Native navigation follows
intermediate waypoints for preparation and evacuation in four mirrored far-corner
layouts, using the knockback component's timed constraints. The current-tree
Release build passed with zero errors (incremental build: zero warnings; the
preceding full compile retained six existing obsolete-gauge warnings).

No new full-original replay, independent holdout, live radar capture or live AI
acceptance is claimed. The earlier evidence gaps and WIP status remain. These
changes are local development work after release 7.5.6.35.7; no new source tag or
public release is created by this update.


## Live-run follow-up — restoration and actionable green guidance

The [October15 live review](tyrant-normal-live-review-20261015.md) found a material
restoration omission: the local WHM survived east-platform destruction on west,
but fell from X75.956 when director1 returned the floor to its original position.
Crown now predicts that return and requires movement onto shared old/new floor
before the outer strip disappears. Both Normal guides above support the
Crown/restoration sequence; exact director timing comes from the new recording,
not the guides. Physical bounds remain nominal, with this observed fall now
corroborating the loss of the outer west strip.

The maintainer was the only XCAI player. Initial7/1 population and an empty tower
cannot establish that a coordinated all-AI split worked or failed. The module's
minimum population remains a conditional plan, not control over manual players.
The maintainer also reported that the green pips were visible but not actionable.
The initial disk presentation above is superseded by **one outlined green wedge**
per applicable source, with a direct stand-here/land-west-or-east hint.

Restoration repair checkpoint:2700 assertions passed (2693 synthetic, seven recorded Gust).
Restoration checks first fail against the prior DLL, then pass for mirrored
inward routes, warning timing, held arena bounds and cleanup. Wedge validation
covers the complete projected sector's coordinate extrema and samples its
boundaries. Current-tree Release build passes with zero errors and six existing
obsolete-gauge warnings. The compiled DLL stays at the watched development path.
The correction has not been tested live; WIP and public release7.5.6.35.7 remain
unchanged. Source/build identities are in tyrant-normal-validation.json.


## Avalanche destruction marker and throw preview — 2026-10-15

The maintainer reported early movement away from the later platform impact and
no marker identifying the island being destroyed. Re-read the Normal Avalanche
sections in both linked guides: the sequence is launch/evacuation, removal of the
faced island, then the platform throw. Savage-specific visibility/timing was not
used.

The latest original shows helper46070 start at17:59:18.1931615 (seq168822),
rotation-60degrees, origin(104.32593,97.48987), total17.2s and elapsed0.0155093s.
With the native0.3s cast correction, the forecast is17:59:35.6776522; the observed
effect is17:59:35.6586204 (seq176746). This is about19ms prediction difference,
not proof of the server snapshot. Boss46069 starts in the same frame (seq168824),
facing east. East removal is17:59:29.4970139 (seq173857).

Combat diagnostics at15:59:21.3155761Z show position(76.34587,90.259125),
navigation target(75.25,88.25), time-to-goal0.48591265s, leeway13.243929s and
max-cast-time13.243929s. No cast was active at that sample. The player had
prepositioned well before impact; the module had not supplied an immediate or
incorrect throw deadline. The movement layer prefers an ultimately safe point
and moves between casts even with positive leeway. This update does not claim
to delay AI departure or establish an exact safe greed window.

ArenaChanges now draws a filled and outlined platform warning at the
facing cue, only on the designated current island. It also tells players on that
floor to leave through their meteor. The marker is display-only: the existing
launch requirement and landing constraints own evacuation, and current bounds
still prohibit walking across the gap. Actual island removal, cancellation and
reset clear the marker; normal visual completion retains it until removal.

ArcadionAvalancheToss now draws the later impact with the configured translucent
AOE colour while the doomed island is still present, then promotes it to the
immediate danger colour after actual removal. Before removal its text describes
the next step instead of saying to leave the throw immediately. Geometry, Risky
flags and the complete future AI activation remain unchanged. Suppressing that
future hazard to make the AI wait would discard reliable safety information;
any departure scheduling improvement belongs in the movement layer and needs
its own route/cast/latency validation.

Current validation:2726 assertions pass (2719 synthetic and seven recorded Gust).
All four Avalanche visual/helper pairs cover the marker side, absence of an
ordinary walking prohibition, shaded preview, unchanged AI deadline, promotion
on removal and cleanup. The Release build passes with zero errors and six
existing obsolete-gauge warnings. No live radar or AI acceptance of this update
is claimed; the module remains WIP and the public release is unchanged.

The maintainer requested removal of the extra radar text label. The platform
warning now uses only the established danger fill and outline; mechanic text
hints and all AI timing remain unchanged.


## Release checkpoint — 2026-10-16

The restoration, launch-assignment/wedge and Avalanche display changes above
are included in release **7.5.6.35.8** (install version **7.5.6.3508**). Earlier
statements that these changes were uncommitted or unpublished describe their
development checkpoints. The current-tree build, all five version tests and
all ten encounter suites passed; see the [release notes](release-7.5.6.35.8.md)
and release_validation in tyrant-normal-validation.json for current identities.
WIP and the documented physical, replay and live-acceptance limits remain.
