# Rabanastre: lethal-first initial coverage (2026-10-25)

## Current acceptance audit (2026-11-04)

The offline suite verifies WIP registration for all four normal-mode
encounters and activates their actual phase component factories.
The maintainer excluded Argath's Trepidation meteor handling and
Hashmal's Submission Tower fire. The relevant encounter sections below
record the evidence behind the retained warnings and cast handlers.

| Encounter | Evidence exercised offline | Remaining evidence for autonomous play |
| --- | --- | --- |
| Mateus | Skater motion and advancing previews, orb and spout selection, White Whisper targeting, Blizzard IV proximity routing, simulated air recovery and guard assignments. Independent footage shows add explosions before Frostwave. | A full alternate spiral and Blizzard IV sequence, Snowpierce overlapping actual breath recovery, and the numeric Azure Guard to Frostwave transition |
| Hashmal | Recorded Towerfall and dash routes, hammer shadows, independent Earth Shaker deadlines and Command Tower impacts, simulated alliance stack separation and add assignments. Independent footage shows Falling Boulder during To Dust. | Numeric Falling Boulder and Sand Sphere overlap, triple hammer impacts, the alternate dash and wide Towerfall casts, and a complete Golem wave |
| Rofocale | Two recorded Trample orientations, prison walls overlapping the circling charge, Karma, Embrace trigger and cleanse, and a simulated route through the Dark Circle interaction request. Independent footage shows active circles, then intersecting Maverick lanes with gaps at the rim. | Dark Circle actor lifecycle and interaction acknowledgement, numeric post-Dark Geas lane and timing evidence, and independently observed route timing in the Trample holes |
| Argath | Recorded Coldblood and Heartless geometry, native movement goals under bleeding tiles, mask response, Shard approach and the rim transition. Independent footage confirms the arrow and Coldblood overlap beside the rim. | Autonomous arrow-controlled movement through the Coldblood overlap, independently observed last-safe timing, and gauge-cap behavior |

The hostile action inventory was compared with the active module sources
over the boss pulls. The unhandled Mateus actions 9782, 9890, 10004
and 10033 record self or return animations without player effects.
Argath 9765 and 9995 affect the boss only. The 9765 visual also warns
of the next Coldblood cast. Shard 9777 repeatedly targets
Argath and does not hit the party. The corresponding damaging cast or
required add response is represented separately. Trample's inner
pulses are covered by the moving forecast. Embrace's instant burst is
handled by preventing entry into the triggering trap and requesting
cleanse for the resulting Bind. These distinctions do not establish a
server hit boundary.

Both original local replay files are absent from their recorded paths.
The archive still indexes the Rofocale and Argath original as a numeric
projection. The local exports from both originals remain usable for
sequence and geometry checks. Prior candidate replay reports identify
earlier builds. The current-tree assembly, including Ice Azer targeting
and Blizzard forecasts, passed 90,353 synthetic module assertions and
143,614 native movement assertions. The watched development DLL
matches that assembly byte for byte. The client automatically loaded
the matching module ID after an out-of-combat update. A live clear
remains unverified.

### Add-phase gauges

The [normal-mode guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre)
describes killing the phase adds before the duty gauges fill. In the October
exports, duty director `0x8003756A` sends update `0x8000000C` with the
`ContentGauge` row in parameter 1 and the running value in parameter 2.
The [game data mirror](https://v2.xivapi.com/api/sheet/ContentGauge?rows=8,10,12,13&fields=Name,TextString)
corroborates the labels. In the matching 2026.10.16 client,
`ContentDirector.ProcessCommonDirectorUpdate` dispatches update
`0x8000000C` to a handler that reads the low word of parameter 2 as
signed. It hides negative values and clamps nonnegative values to
`0x2710` (10,000). In that build the dispatcher starts at image
offset `0xB74410` and the handler at `0xB75E00`.
[FFXIVClientStructs signature](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InstanceContent/ContentDirector.cs).

| Gauge | Export | Opens at sequence | Last observed value and sequence | Hides at sequence |
| --- | --- | --- | --- | --- |
| Hashmal's Mana (8) | Mateus/Hashmal | 355729 | 2666 at 389550 | 400747 |
| Rofocale's Aetherial Acceleration (13) | Rofocale/Argath | 206093 | 5227 at 223483 | 244048 |
| Argath's Emptiness (10) | Rofocale/Argath | 469228 | 4894 at 478953 | 485992 |

These streams open at zero and use `0xFFFFFFFF` when hidden. Each
module now displays the current whole percent and the add objective
from its matching update. The Mateus Aether row (12) is sheet-backed
and absent from both exports. Argath's increments decrease as
Shards die. None of these samples reaches 100%, so the AI retains
add priorities and waits for the real cast cues for mitigation.
The client display cap does not establish a server wipe deadline.
Hashmal's Command Tower enters combat at sequence 361105 and becomes
targetable at 361203. The existing target priority therefore applies as
soon as the tower can be attacked in this pull.

## Initial scope

Scope requested by the maintainer: arena boundaries and the most lethal mechanics;
leave the rest for later work. Hashmal, Rofocale and Argath previously had only
commented-out placeholders. They are now registered **WIP** modules, not complete
encounter support. Mateus is unchanged in this pass.

## Evidence and limits

Written guides read for the level-70 normal alliance raid:

- https://ffxiv.guide/sb/the-royal-city-of-rabanastre
- https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre

These corroborate tower fall direction, the flaming-side dash, hammer proximity,
Maverick clones, the figure-eight charge, sword crosses and Coldblood variants.
The wiki and Pocket Guide disagree on some command wording; resolve commands
against assignment statuses and recorded outcomes before implementing them.
Guide descriptions do not establish numerical hitboxes or snapshots.

Training originals, both from the same raid on game 2026.10.16.0000.0000:

- `c2a291f769153180808164095a118b0ae9ecdd6720d283e49022d14179bc2d8b`:
  Mateus/Hashmal, closed source, 572,057 decoded operations.
- `69d8033094eef868782ab876ee6d0879a3b3dde3ab776f211f33a7019b16b8c0`:
  Rofocale/Argath, closed source; archive recording
  `2ed4820aff1b7453ae9845ecb0f8560b5f05a78d3a1d3445321f9bafd8702429`, retained
  revision `15b073c5ca9f3db15daad90604196b26c6ba93276213216185f5e4f192c4f0b5`.
  This is one original across projections, not independent evaluation evidence.

Private extraction retains numeric events, raw operation indexes, matched-version
Action/Omen fields, arena layouts and model vertices. Raw recordings and asset
exports are deliberately outside the repository. The archive's indexed September
sheet dataset mismatches this game version; implementation uses the matching local
October sheet extraction instead. Client-frame observations are not server snapshots.

## Initial implementation (2026-10-25)

| Boss | Behavior and evidence | Remaining limit |
|---|---|---|
| Hashmal | Arena at (-320,-46), circle 29.5; matching layout stage origin and original 30-radius reference. Extreme Edge 9680: 66 by 36 rectangle, predicted from cast start. | Unobserved 9678 is deliberately not enabled. Exact lethal rim still uncalibrated. |
| Hashmal | Towerfall 9668: direction-bearing object 0x1EA7DF creates a 40.5 by 10 rectangle with a conservative 9-second activation. Objects at raw 326022, 406374/5, 420406/7 match helper origins/facings on hits 332609, 414199/211, 429288/296. Observed lead 9.1–9.4 seconds. Each object retires independently on its matching hit or destruction. | Castless deadline is an early estimate, not a verified last-safe departure. Other tower appearance effects are not mistaken for falling objects. |
| Hashmal | Tower appearance circles 9666/9838. Earth Hammer warns at the boss's 5-second cast, before the late 1-second helper cast. | Hammer is warning-only; wide towerfall 9674 is unobserved and deliberately not enabled. |
| Rofocale | Corrected layout center (106,-191), circle 29.5. Maverick 9865: 60 by 16 rectangles. Five clones at 178567–178587 and opposing pair at 313933–313936. Cry of Victory 9845/10060: 60-radius, 180-degree cones. | Unobserved 10022 is deliberately not enabled. Trample only provides an early cast warning; its moving figure-eight is not in AI hazards yet. |
| Argath | Corrected layout center (106,-386), square inner bound of half-width 24, replacing the placeholder circle at (106,-385). Heartless 9778: eight concurrent 61 by 11 rectangles, starts 427185–427203, hits 431325–431349. Soulfix 9770: radius 12. | 24 is a conservative inner-platform policy, not the measured lethal edge. Model extends to approximately +/-29; exact rim and phase-dependent death-wall inset need calibration. |
| Argath | Both Coldblood casts give distinct early instructions (9767 flee, 9766 enter the safe circle). Gnawing Dread publishes the native misdirection special mode. | Coldblood instructions are text-only; safe-radius geometry remains unimplemented. |

Dimensions originate from matching Action fields and existing reference conventions
(including hitbox extension where applicable), corroborated by written mechanics;
they have not been independently measured against server hit boundaries. All
implemented risky shapes supply timed AI forbidden zones through native components.
All simultaneous Maverick lanes/Heartless rays remain visible because they resolve
together; no later sequence is painted across the arena. Tower predictions clear
individually. No live radar or gameplay acceptance is claimed.

## Disabled-source audit

History commit `8361ea25d` is titled "commented out some empty modules": these
were empty scaffolds, not previously working encounter implementations. Their
enum comments still do not establish correct geometry or safe behavior.
Unobserved Extreme Edge 9678, Maverick 10022 and wide Towerfall 9674 are left
inactive until their own evidence supports handling. Matching local sheet fields
support only the fields extracted, not full behavioral or physical validation.

## Autonomous AI follow-up (2026-11-04)

The same level-70 normal raid guides were checked against both October 16
recording exports and matching Action fields. The archive search for duty 281
returned the already used Rofocale/Argath original. Both exports are training
observations from the same raid. High damage skipped some phases. No independent
evaluation original was available.

- **Mateus.** The opening prioritizes the Blizzard III add and phase-critical
  Aqua Spheres, Flume Toads and Azure Guards before generic adds or the boss.
  Blizzard IV publishes raid damage as well as the existing 15-yalm proximity
  avoidance zones. Six helpers cast concurrently at main sequences 116694–116735.
  The spiral uses its observed alternating 0.23/0.30-second cadence rather than
  the previous 0.16/0.36-second estimate. This approximates the 228 Ball of Ice
  events after the first bubble at 67565, with the first predicted pulse
  0.15 seconds before the recorded event. The next pulse is bright and the rest
  of the rolling preview stays shaded, following `Components/Exaflare.cs` and
  the Byakko Hundredfold Havoc example. The rolling window limits the use of
  uncertain long-range path predictions. Rebind ends the sequence even if
  some pulses are missing. Recorded helper origins differ from the authored
  coordinates in places, including swaps within pulse pairs. The exact path,
  water phase, guard separation, alternate Blizzard IV patterns and autonomous
  soak reliability still need evidence. Mateus is now WIP.
- **Hashmal.** Added Jagged Edge, Earth Shaker, Falling Rock spreads, Quake IV,
  Landwaster and priority for wipe-critical adds. Six Falling Rock casts at
  386364–386387 target the same six players as icon 120. The pull interrupts
  these casts, so hit behavior remains unobserved. The first hammer shadow
  appears at 345871, about 12.9 seconds before impact 355088. Its early timing
  estimate is reconciled by the actual Hammerfall cast. Independent shadow
  lifetimes support simultaneous hazards. The 18-yalm inner avoidance zone is
  a conservative response to near-origin damage, with a goal that increases
  distance from the nearest shadow. It is a routing policy for damage falloff.
  Three later shadows despawn on boss death before landing. Command Tower
  9660 at 358710 encodes knockback row 86, 50 yalms away from the source.
  Fresh pre/post positions terminate near radius 29.5 from launch radii of
  3.6–21 yalms, supporting wall clipping for this knockback.
- **Rofocale.** Chariot guidance favors shortening the marked player's charge.
  The red mark described in the illustrated guide is icon 23 at
  139367 and 256643. Both recipients match the following Chariot
  casts at 141469 and 259476. The mark leads those casts by 5.27 and
  5.24 seconds and leads the effects by about 8.2 seconds. The AI and
  radar now use the mark to publish the current bait lane, with a
  conservative 7.8-second activation estimate. The marked player can
  start shortening the lane and other players can plan to clear it.
  The actual cast replaces the forecast and its deadline, and an
  unconfirmed forecast expires after six seconds. Impact, interruption,
  and boss or target removal clear the bait. Initial cast lengths from
  fresh poses are about 8.5 and 28.6 yalms. The shortening preference covers the marked
  player's starting distance, including partial progress from the
  longer placement. A separate soft preference checks the finite
  rectangle to the proposed endpoint, so stopping before an ally
  on the same bearing stays available. Other players receive the
  timed live lane and the marked player retains the damage forecast.
  The recorded long placement anchors the native route test, with
  a synthetic ally and concurrent hazard. Exact server snapshots
  and the last-safe bait movement time remain unverified.
  [Normal-mode Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [illustrated Chariot guide](https://ru-eka.com/archives/5695).
  Crush Weapon and Karma publish cast hazards. Embrace publishes the cast
  and retains the 3-yalm trigger at each live trap actor, replacing it when
  the cast ends. The 7-yalm bind burst is a consequence of triggering the trap.
  Spawn/cast correspondence starts at 252735–252815. Pomp and Circumstance
  favors distance, with 15 yalms as a conservative high-damage avoidance
  zone. Added Archaeodemon priority and mitigation guidance at Heavenly
  Subjugation. The two Trample openings (167828 and 276759) support an early
  10-by-10 opening lane using the boss cast origin and facing. A hit near the
  back of the predicted lane in the second pull leaves its exact rear edge
  uncertain. The October figure-eight omen mesh
  `m0445sp03o0c` has model-local bounds ±1.749 by ±3.158. Its particle and
  emitter transforms and the separate helper pulse timings do not yet give
  corroborated per-step world hitboxes. The remaining loops still need
  implementation. For Dark Geas, the normal guide's interact mechanic, the
  historical enum, and the matching `LVD_BOSS_03` placements corroborate
  12 circle objects. The native AI requests only the closest targetable
  in-arena event object from that set. Dark Geas 9864 resolves at 342397 but
  the original has no active circle actors, so the interaction lifecycle
  and reachable route remain unverified.
- **Argath.** Both Coldblood forms now constrain AI movement. The proximity
  form avoids the closest 20 yalms and favors moving farther away, with
  falloff evidence from 433341. The donut uses a conservative 5-yalm safe
  radius. At 543248 the cast has no recorded hit targets and fresh positions
  include successful avoids near 4.9 yalms. The matching
  `m0446show01o0h` asset contains a 0.088 particle scale, consistent with a
  small central safe area, but a raw scale is not a measured world radius.
  Check this route under Gnawing Dread and the lethal rim in live play.
  Unrelenting uses 10-degree half-angles. The matched omen ring scalar
  0.0075961 follows the same cosine relationship as the 30-degree Earth
  Shaker and 45-degree Karma references. The helper casts are separated by
  about 30 degrees, and a hit at 511082 is consistent with the narrow cone.
  The mask actions 9753/9754 precede The Word by about eight seconds and
  match Deceiver/Upholder at resolution. The 1425 status publishes normal
  or inverted gaze and the reversed 1424 command publishes the native
  stop-movement-and-actions mode. The truth-mask movement command still
  needs a safe native movement strategy. Judgment Blade icon 125 gives a
  center bait goal. All eight spawned Heartless helpers at 425391–425436
  predict their overlapping rays about 2.6 seconds before the helper casts
  correct each origin and deadline. Their origins move backward by about
  5.15 yalms at cast start and the recorded effects resolve 6.6 seconds
  after spawn. All rays are visible together because they resolve together.
  Added Crush Weapon, Crippling Blow, Fire IV, Dark Ultima and priority for
  Shades and Shards.

## Bounded completion pass

The scope is the four normal-mode bosses and their encounter mechanics. Each
required mechanic needs an actionable player cue and a native AI response.
Validate first cue, simultaneous constraints, cleanup and reset in representative
patterns before changing WIP maturity. Keep offline acceptance and live clears
separate. The maintainer excluded Trepidation meteor handling and Submission
Tower fire from this goal because the retained clear skipped them. Keep the
existing player cues and independently supported cast handlers.

- **Mateus.** The orb and spout component now chooses one nearby unfilled
  objective, preserves an occupied spout, retires solved spouts on the
  recorded `0x00400080`/idle sequence, and clears on Rebind. It avoids
  requesting a new soak if the sampled skating path reaches the
  objective before the estimated time to run to its center plus two
  seconds to leave. Already occupied objectives use only the two-second
  exit allowance. The passage check accounts for both the ice pulse
  and the objective footprint. Near the north spout, the helper
  pose for 91070 comes within 3.89 yalms of its center, before the
  first sample within three yalms at 91202. The earlier pulse can
  overlap the two-yalm spout. When fresh motion drops out, the
  sampled timetable still screens soak requests without drawing a
  stale lane. Player text distinguishes touching an orb from filling
  or staying in a spout. When several spouts are open, the AI gives
  partially filled spouts first claim on nearby eligible players and
  reserves only the missing places. It can split the remaining players
  across other spouts and leave a spare player available for an orb.
  The west and south spouts resolve independently at 76019 and 81107.
  At the west completion frame the three nearest fresh player samples
  are 1.60, 2.43 and 3.69 yalms from its center. They do not establish
  the server's soak boundary. The component keeps the two-yalm
  EventObject radius as a conservative approach target. The split is
  tested with synthetic assignments at the recorded spout positions.
  This remains a navigation preference because
  the sampled path and straight-line travel estimate do not establish an
  exact soak deadline or a safe route. Blizzard IV favors distance
  from the closest of the simultaneous casters. The [illustrated guide]
  (https://www.nyacchi.com/rabanastre/) also treats the orb and three-player
  spout as separate objectives. Remaining gaps are the full alternate
  spiral/Blizzard IV patterns and water phase routes. Azure Guards retain
  their spawn positions as tank anchors. The AI respects living in-arena
  tank ownership, then prefers an unclaimed guard in its alliance's sector.
  Native movement gets an approach within attack range of the assigned
  guard. If the local tank's guard drifts, the radar outlines its anchor
  and both movement owners prefer a position that pulls it toward that
  anchor. The destination is clamped to the arena and the preference keeps
  all timed hazards binding. The two-yalm tolerance governs positioning.
  The buff's actual distance threshold is unknown.
  Targetable Flume Toads and Azure Guards inside the arena are eligible
  as soon as they appear, including when the native target list marks
  them passive for lack of party enmity. Invincible, dying, forbidden,
  and untargetable actors retain their restrictions. This removes the
  need for a human to hit an add before the AI can create an air bubble
  or start the guard wave. The [normal-mode phase guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre)
  confirms both target objectives. The retained clear contains no
  Toad or Guard spawn, so the offline cases exercise the passive
  and ineligible states synthetically.
  The module uses the same A southwest, B north, C southeast convention as
  for Aqua Spheres. Actual markers can vary by group.
  [Example of a different marker layout](https://jp.finalfantasyxiv.com/lodestone/character/14576096/blog/3581189).
  When one guard remains in a fully observed wave, players get a Frostwave
  preparation cue. The final kill of that three-guard wave publishes an imminent
  raidwide and a soft preference to gather for healing. The matching Action
  row identifies Frostwave 9793 as an instant raidwide. The one-second
  mitigation estimate is conservative guidance. The retained clear skipped
  this phase, so actual tank separation, ultimate timing and the gauge-cap
  trigger remain unverified. A live guard despawn disarms the last-kill cue.
  The original guide footage shows local add explosions around 3:21,
  followed by the center animation at 3:23 and visible raid damage
  by 3:28. The edited footage corroborates keeping active explosions
  binding while gathering for the ultimate. It leaves the last Guard
  death frame, damage snapshot and delay unresolved.
  [Normal-mode Frostwave footage](https://www.youtube.com/watch?v=Zi99W7ejtz8&t=200s).
  [Normal-mode phase guide](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3590646).
  The main source captures the first stack of Breathless for all
  24 players in the same frame at 141913–141942
  and eight Icicle spawns at 142064–142092, but no Snowpierce resolution or
  successful air bubble route. Bubbles retire by actor ID and Breathless
  warns players to create a bubble when none are present. At five or more
  stacks with no live bubbles, the AI raises eligible Flume Toad priority
  and routes within attack range of a reachable one. An in-range toad
  takes precedence over an out-of-range target. Targetability and
  attack exclusions still apply. The first live bubble replaces the toad
  goal with direct recovery. The offline case uses two synthetic toads
  and a lane that makes the nearer one unreachable. Snowpierce
  9792 has a ten-second cast, target-ended charge shape and three-yalm
  width in the matching Action row. Normal-mode guides describe taking
  air before aiming the line when time allows, then getting air again
  after the charge. An air-first route suspends the AI's own aim
  constraints until the stacks clear, keeping other players' timed
  charge lanes and predicted damage. An imminent charge goes first
  when the estimated air window allows recovery after the hit. With
  no time to do both, air takes priority. In aim mode the bubble
  protection only starts near the bubble, leaving short charges on
  the same bearing available. The one-second-per-stack estimate and
  two-second exit allowance are navigation policies. The retained
  clear gives no bubble entry acknowledgement, stack cadence, or
  Snowpierce snapshot for this overlap. The Lodestone guide reports
  one stack every three seconds, so the inherited one-second estimate
  is deliberately conservative pending a recorded cadence. The AI
  anchors this estimate to the observed stacks. An unchanged refresh
  cannot postpone recovery, and losing Breathless clears the estimate.
  A line that starts inside a bubble crosses it regardless of aim.
  The post-charge recovery check counts only bubbles the marked player
  can preserve. A synthetic route reaches the only bubble before that
  charge, while adding another reachable bubble restores the option
  to aim first. Other bubbles and player hazards retain their own
  constraints. A synthetic
  overlap case also checks that native navigation can reach a
  second bubble when another player's earlier charge blocks the
  first. The air-or-bait decision checks the finite lanes of
  other active Snowpierce casts against each bubble, using its
  estimated arrival time both now and after the player's own cast.
  The AI avoids deferring recovery based on a bubble expected
  to be hit before arrival. It recalculates as charge targets
  move or casts cancel. If all bubbles are threatened, it still
  tries immediate recovery while retaining the actual lane hazards.
  The competing-charge timing test is synthetic.
  The guide footage shows that crossing a bubble is sufficient and
  that several target-following lanes can overlap around live bubbles.
  Radar and AI use each Froth actor's current position and collision
  radius in the inherited 0.7–1.4 range. The exact breath reset
  radius remains unverified. Mateus's navigation grid uses 124 cells
  across the same 59-yalm square so even a 0.7-yalm bubble centered
  on a grid corner can contain an entire cell. The focused offline
  case checks recovery after a bubble shrinks and the subsequent
  transition back to line baiting.
  [Normal-mode footage at 2:17](https://www.youtube.com/watch?v=Zi99W7ejtz8&t=137s),
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [normal-mode phase guide](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3590646).
  Frosting 9786 and
  Hydrowave 9796 use the matching Action rows and circular omens for cast-backed
  puddles. Those variants were skipped in the recording.
- **Mateus Ice Azers.** Five targetable Azers open at 44740–44754.
  The guide calls for killing them and leaving their death explosions.
  The AI can now attack eligible Azers even when its target list
  initially classifies them as passive. Higher-priority phase adds
  keep their priority. Seven Blizzard 9784 starts occur at
  46261–51760. The four resolved casts hit only their selected
  players. Each active cast now gives healers a single-target
  forecast for ordinary damage. Death or cast cancellation removes
  the forecast. Hypothermal Combustion stays separate. Azer 67 dies
  at 47121 and begins its explosion cast at 47123.
  [Normal-mode encounter guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
- **Mateus Blizzard Spheres.** The guides agree that marked players
  should aim Chill away from the raid. The Lodestone guide describes
  spheres stopping short of contact, while the community wiki warns
  against contact. The module keeps the existing ten-yalm kiting
  buffer as a conservative policy. Only the sphere actor's current
  tether can request that spacing, so tether changes, death and
  despawn clear the old assignment. Before Chill starts, the tethered
  player sees an outline of the current aim. The AI prefers bearings
  that clear current allies and the boss's hitbox. The aim is a
  preference and leaves all other timed hazards and bounds active.
  The actual cast takes over with the inherited 41-yalm, 20-degree
  cone. The matching 9798 Action row confirms a five-second cone
  cast with base range 40, and the sphere definition gives a one-yalm
  hitbox. The linked `m0121_blizzaga0f` asset has a ring fan scalar
  of 0.0075961 on both decal particles, matching the 20-degree
  telegraph convention already observed for Unrelenting. The retained
  pull skipped this phase, so the tracking cutoff, repeated casts,
  contact distance and exact cone angle still need encounter evidence.
  [Normal-mode phase guide](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3590646),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
- **Mateus live spiral.** Totema 81 starts moving at main sequence 72878.
  During the main spiral its observed radius grows by approximately
  1.178 yalms per radian around `(-320, 241.8)`. The recorded helper origins
  align better with Totema's position about 0.25 seconds later than with
  its position at the client event. The module keeps the existing opening
  estimate and anchors the rolling forecast to Totema's observed angle, radius,
  speed and turn direction once there is enough motion. The last pulse resets
  the clock. Five steps appear on radar and all eight go to the native AI.
  The forecast stops on stale motion, skater despawn, Rebind, or completion.
  A late activation can recover from a pulse and observed skater motion even
  after all orbs disappear. In a separate calculation on this same training
  recording, the 95th-percentile eight-pulse position error was 1.81 yalms,
  compared with 2.70 for the fixed pulse list. This comparison does not
  establish server hit boundaries, safe departure times or alternate paths.
- **Mateus Blizzard III pillars.** Five players gain tether 8 toward
  pillar 106 at 91022–91034. It becomes targetable at 91036, enters
  combat at 92743 about 2.38 seconds later, and starts White Whisper
  10030 at 92802. Its death at 97947 cancels the cast before the
  tethers clear. A targetable pillar inside the arena can now become
  the AI's highest-priority target while still passive. Invulnerable,
  forbidden and dying targets retain their native exclusions. The
  tether gives personal text and a radar link before the cast. The
  AI routes to attack range at targetability, about 2.47 seconds
  before White Whisper begins. It prefers the assigned pillar,
  keeps other eligible pillars reachable, and ranks one already in
  range above an out-of-range target. The offline case adds a
  synthetic second pillar and a hazard blocking the tethered one.
  Immediate air recovery suppresses the optional attack approach.
  The actual cast supplies the countdown. The player guides describe a snowman
  transformation on failure. The retained pull killed the pillar,
  so the failure effect and exact snapshot remain unobserved.
  [Normal-mode Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Rabanastre).
- **Mateus phase transitions.** Flash-freeze 9799 is an instant cone.
  The matching Action row gives base range 12, plus Mateus's 4.5-yalm
  hitbox. The inherited 60-degree half-angle is consistent with the
  observed hits at about 44 and 56 degrees off axis and a fresh miss at
  about 68 degrees. It occurs before Unbind at 50334, while Totema is
  active at 82011, 91675 and 101063, and after Blizzard IV at 138004.
  Totema actually despawns at 140600, after the last sampled cleave.
  The warning and AI avoidance now follow Mateus's own targetability,
  cast and target state. A skater despawn no longer disables that
  protection. The recording does not establish a dependable countdown
  or minimum interval across phases, so between casts the frontal
  sector remains a conservative avoidance zone.
  Dualcast 9788 starts at 111664, about 20.11 seconds before the first
  Blizzard IV effect. The early raidwide forecast uses the cast finish
  plus a conservative 14.8 seconds. Its successful effect at 113225
  confirms the sequence and updates the estimate. An interrupted cast
  clears its preview. Summon Blizzard 9787 at 114845 refines the
  mitigation estimate to 11.8 seconds, compared with about 12.03 seconds
  observed. The six helper spawn positions differ from the ground
  targets, so the forecast leaves movement unrestricted until the
  actual casts at 116694–116735. Each cast supplies its own ground
  location and time. The first five effects land together at
  126871–126879, with the sixth about 42 ms later at 126948. A marker
  impact, despawn or interruption clears only that marker. Confirmed
  markers within 0.25 seconds of their volley start share one damage
  warning. The first volley stays bright and later markers are shaded,
  following Hashmal's Earth Hammer presentation. All confirmed marker
  deadlines remain available to the AI, and its distance preference
  follows the imminent volley. A synthetic overlap verifies out-of-order
  markers and preview advancement. The Pocket Guide describes
  randomized safe regions and subsequent uses, so the estimates apply
  until actual ground casts arrive and do not predict a fixed layout
  or a second volley. [Normal-mode Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre).
  The three Aqua Spheres at 67590, 67593 and 67711 lie north,
  southwest and southeast. The normal-mode guides call for one per
  alliance. The module uses A southwest, B north and C southeast as a
  fixed convention for live alliance labels, with equal priority when
  labels are unavailable. Unbind starts soft staging toward those
  recorded locations. The 9779 effect at 64169 leads targetability
  at 67662–67798 by 6.96–7.13 seconds. The southeast sphere opens
  0.17 seconds after north and southwest. Each alliance retains
  its own preview and AI approach until its sphere is targetable.
  The eligible sphere then takes over the radar marker and native
  attack-range approach. Interruption, Rebind, boss removal and an eight-second
  post-effect timeout retire unconfirmed staging. This timeout is a
  routing policy. Other spheres remain eligible, and active Blizzard
  III adds still outrank them. The offline route uses a fresh recorded
  player start and a synthetic hazard. Alternate spawn layouts and
  the exact Skating Fiend conversion threshold remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
- **Hashmal.** Added the cast-backed 9678 Extreme Edge and 9674 wide Towerfall
  using the exact October Action rows, rather than using placeholder
  comments. The former has the same 60 range and 36 width as 9680. The latter
  has a 9-second cast, 40 range, 16 width and the rectangular omen. The
  wide cast is tested with its own deadline and a synthetic direction
  over the recorded double Towerfall and Jagged Edge overlap. Its
  impact clears the matching lane without retiring the other hazards.
  The wide lane also clears on cast interruption or helper removal. The
  alliance stack listens to 9682's cast target and restricts participation
  by the target's actual party membership. The observed 9683 spread stays
  separate. Sand Sphere priority follows the same A northwest, B south,
  C northeast order as the recording's waymarks. Added tank/raid damage
  warnings and To Dust guidance. The alternate cast variants, Boulder
  resolution, Submission Tower fire lifetime and multiple hammer impacts
  still need a recording. Submission Tower fire is outside this goal.
  **Falling Rock.** The six icon 120 assignments at 386260–386275
  exactly match the six cast targets at 386364–386387. The icons
  lead the casts by about 0.88–1.07 seconds. They now give an early
  spread warning and a conservative 5.75-second movement deadline.
  Each helper cast replaces only its own target's forecast.
  Interruption, death and expiry remove stale assignments. The
  offline route uses the six recorded cast locations as a placement
  fixture. All casts were interrupted in the retained pull, so the
  effect timing and recipients remain unverified.
  [Normal-mode raid guide](https://ffxiv.consolegameswiki.com/wiki/Rabanastre).
  **Falling Boulder.** When two party markers overlap, the ordinary
  gathering and foreign-stack avoidance constraints can conflict.
  The marked player and their party temporarily favor the same
  recorded alliance waymark used for Sand Spheres. Foreign-stack
  avoidance and the shared damage predictions stay active. Each cast
  publishes its own party and mitigation deadline, so a later cast
  cannot inherit an earlier party's deadline. Normal
  party gathering resumes once the markers have room to separate.
  The extra yalm beyond the sum of the two radii is a routing margin.
  Text and a radar outline identify the party's staging position.
  The native planner case starts three carriers and one follower per
  alliance at the same point and adds a hazard over the southern marker.
  It runs the stack and Sand Sphere components together to verify
  that all three parties can route to their sectors, retain their own
  sphere priority, and keep the independent To Dust forecast after
  the stacks end. The stagger between Boulder casts in this fixture
  is synthetic.
  Original normal-mode footage shows players gathering beside their
  sphere and Falling Boulder landing at about 5:41 while To Dust is
  still visible in the enemy list. Cactbot independently keys its
  three-stack alert to 9682 cast start and shows the effect within the
  sphere wave. The archive still has no retained cast 9682. The
  footage and trigger confirm the overlap and cast cue. The route
  check uses synthetic starts and does not establish damage, exact
  assignments, cast intervals or the last safe departure.
  [Normal-mode Hashmal footage](https://www.youtube.com/watch?v=Zi99W7ejtz8&t=337s),
  [Cactbot cast trigger](https://github.com/OverlayPlugin/cactbot/blob/8a680429b0d012b922528acf66f612b0f4153dfa/ui/raidboss/data/04-sb/alliance/royal_city_of_rabanastre.ts#L125-L132).
  [Normal-mode Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [normal-mode phase guide](https://wikiwiki.jp/ff14n/%E3%83%AC%E3%82%A4%E3%83%89/%E5%A4%B1%E3%82%8F%E3%82%8C%E3%81%9F%E9%83%BD%20%E3%83%A9%E3%83%90%E3%83%8A%E3%82%B9%E3%82%BF).
  **Earth Hammer.** The first shadow at 345871 precedes the only
  observed impact at 355088 by 12.89 seconds. Fresh position and
  damage samples range from 10,055 damage at 11.77 yalms to zero
  at 37.0 yalms, supporting a distance preference beyond the
  existing conservative 18-yalm avoidance region. The three
  shadows at 460470–460472 appear in the same frame and disappear
  after the boss dies, before any corresponding impacts. The radar
  follows the same first-wave highlight used by Jagged Edge and
  Earth Shaker. The recorded shadow's animation stages
  `0x00100020`, `0x00400080` and `0x01000200` precede impact by
  7.28, 4.27 and 1.32 seconds. Each stage updates only that shadow,
  using a lead of 7.05, 4.05 or 1.10 seconds to allow for timing
  uncertainty. Repeated or older stages are ignored. The helper
  cast starts 0.96 seconds before impact and replaces the estimate.
  Confirmed casts keep their own deadline through visual removal,
  and each effect clears only its matching cast.
  Later waves keep their movement constraints and get separate
  mitigation times. The 0.25-second grouping window is a display
  and mitigation policy. Native navigation checks use all three
  recorded shadow positions and two observed player starts.
  Independent footage shows multiple hammer heads in the same
  frame at 0:07.1, with players already near the opposite rim.
  Multiple impacts are visible around 0:10.7 before Summon.
  Cactbot's timeline separately records a triple Hammerfall before
  Summon. Together with the retained three shadows, this supports
  showing all simultaneous danger areas and planning against their
  union. The video does not resolve all three packet deadlines.
  Exact triple-impact timing and the follow-up Jagged Edge overlap
  remain unverified. [Normal-mode Hammerfall footage](https://www.youtube.com/watch?v=UO0th4C6z4g&t=7s),
  [Cactbot timeline](https://github.com/OverlayPlugin/cactbot/blob/8a680429b0d012b922528acf66f612b0f4153dfa/ui/raidboss/data/04-sb/alliance/royal_city_of_rabanastre.txt#L107-L117),
  [normal-mode Lodestone guide](https://jp.finalfantasyxiv.com/lodestone/character/172216/blog/3583148).
  **Extreme Edge.** Hashmal's departure timeline 7737 occurs at
  332102 and 428240, about 2.30 seconds before the respective casts.
  Both are 9680. The first travels east and burns the north half,
  while the second travels west and burns the south half. The
  animation therefore gives early timing without establishing a
  safe side. It outlines a central five-yalm band and gives the AI
  a soft staging goal. The preference falls off with distance from
  the band, so the AI can still shorten either escape route when an
  overlap blocks the band. The cast removes staging and uses its actual
  offset and facing. The generic 7747 animation also plays when
  Hashmal returns and does not start a warning. A fresh hit and miss
  straddle the north lane's edge near Z −40 in the first occurrence.
  The offline case uses a recorded starting position with two
  impending Towerfalls at departure, then tests a recorded position
  against the second dash and the concurrent first Jagged Edge wave.
  A synthetic blocked band verifies that the fallback approach
  keeps the blocking hazard binding.
  Cactbot independently identifies 9678 as the opposite cast. Its
  recorded origin and server snapshot remain unverified here.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [Cactbot's Extreme Edge triggers](https://github.com/OverlayPlugin/cactbot/blob/main/ui/raidboss/data/04-sb/alliance/royal_city_of_rabanastre.ts),
  [illustrated phase guide](https://www.nyacchi.com/rabanastre/).
  **Sand Spheres and To Dust.** The guides assign the three
  northwest, south and northeast spawns to separate alliances.
  Command Tower cast 355776 gives about 3.42 seconds to prepare
  before the knockback at 358710. During the cast and for up to
  5.7 seconds after the effect, the radar and native navigation
  offer a soft staging destination from the corresponding waymark
  at 291756–291758. The first Earth Shaker cast appears 5.73 seconds
  after the knockback in this clear. This window is a preparation
  policy and does not predict the sphere spawn time. Staging ends
  separately for each alliance when its own sphere becomes targetable.
  An earlier sphere in another sector does not redirect that alliance.
  Interruption, tower or boss death, or expiry clears all staging.
  The native knockback projection from the local position at 358559
  lands within 0.5 yalms of the wall sample at 360207. The launch
  sample precedes the effect by 41 ms and does not prove its snapshot.
  Knockback and other hazards continue to take priority.
  An eligible targetable sphere can be selected before it acquires
  aggro. The AI prefers its own sector, then helps another sphere
  after its assignment dies. Attack range is a soft goal under
  simultaneous movement hazards and party stacks. The radar highlights
  the assigned sphere, and player text gives its sector and a countdown
  when it begins To Dust. The Command Tower becomes targetable at
  361203, 1.81 seconds after the knockback and 0.97 seconds before the
  first recorded tower HP loss. The AI can attack the tower as soon as
  it is eligible, with sphere staging and live sphere assignments
  retaining their approach goals. Once those objectives clear, the
  AI approaches the tower within attack range and the player warning
  directs the raid back to it. The route from the recorded landing
  around a synthetic hazard was checked with native navigation.
  The matched 9673 Action row confirms a 20-second cast with
  a 60-yalm circular effect. Each active cast publishes a separate
  raid damage deadline that clears on impact, interruption, death
  or despawn. The retained pull skipped the spheres, so spawn timing,
  any overlap with Falling Boulder, and the outcome of a failed
  check remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Rock Cutter.** The matching 9687 Action row specifies a cone with
  base range 4. Hashmal's recorded hitbox adds 6 yalms. At 456605 the
  cast targets the tank and faces about 176.8 degrees. Its effect at
  458499 faces about 206.5 degrees, within one degree of the tank's
  direction at impact. It also hits two other nearby players. The last
  sampled positions of those players are 10.6 and 38.7 degrees off the
  impact axis, with 0.9- and 5.4-second update ages. A fresh miss was
  about 76 degrees off axis and 7 yalms from Hashmal. Use a conservative
  75-degree half-angle and track the cast target rather than the
  initial facing. The native cleave helper constrains other players
  to move out and lets the tank aim away from nearby allies within
  reach. At 456605 the largest gap between in-range ally bearings
  is about 84 degrees, so a 150-degree cleave cannot miss everyone.
  When there is no clear angle, the tank prefers staying near Hashmal
  and clipping fewer allies. Actual hazards still govern
  the route, other players retain their dodge warning, and the tank
  still gets a separate mitigation prediction. An
  early interruption clears it, and a late cast end waits for the
  effect or a short timeout. Exact hit edges and snapshots remain
  unverified. [Normal-mode raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre),
  [player report of the frontal cleave](https://forum.square-enix.com/ffxiv/threads/355653-Why-do-you-fail-at-Hashmal/page3).
  **Control Tower and Jagged Edge.** Control Tower 9665 deals raidwide
  damage before the tower appearances. Sanction 9667 is a separate
  raidwide 3.14–3.16 seconds later at 327405, 407416 and 422154.
  Publish both damage predictions from the 9665 cast and retire
  Sanction on either of its sheet-backed effect IDs.
  **Command Tower.** The `LVD_BOSS_02` visual with layout ID 7075821
  plays `0x00040008` at 360927, about 15.14 and 20.16 seconds
  before the two arena-wide Impact 9671 effects at 375665 and 379545.
  A later visual cue `0x00100020` can start the forecasts if the first
  was missed. The strike animations at 375570 and 379480 precede the
  respective hits by 0.08 and 0.05 seconds. The component forecasts
  both raidwides from the early cue and reconciles each strike
  independently. It clears on tower death or phase end and expires
  stale forecasts. The Impact cue is separate from the alternating
  Earth Shaker cones at 366928–366931, 371401–371403 and the later
  overlapping waves at 383390–383392 and 384919–384921. The earliest
  simultaneous cones stay bright and the later wave is shaded.
  The helpers target themselves and use fixed directions. In the
  overlapping pair the groups land at 387133–387137 and 389778–389783,
  2.019 seconds apart. The later cones fill the earlier gaps. For
  two confirmed three-cone volleys the AI keeps both hit deadlines
  and moves out of the part of the next cone that is too deep to
  leave in the available running time after the first volley. The
  estimate uses ordinary walking speed and the native one-second
  margin. The native route test uses the recorded angles and interval
  with a synthetic outer starting pose. Each confirmed impact
  retires its own cone. The exact last-safe turn time is unverified.
  These timings describe the two observed strikes. Longer phases
  and faster kills have no independent recording. [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [Command Tower guide](https://jp.samurai-gamers.com/finalfantasy/ff14/ff14-ritan-ivaris/).
  **Landwaster and Pennantstone Golems.** The Command Tower dies at
  390388, 3.65 seconds before the paired visual 9669 and helper 9670
  casts and 10.67 seconds before the actual hit. The AI gets an early
  10.4-second mitigation estimate after the death callback. The helper
  cast replaces it, and its impact clears the warning. A living tower
  despawn gives no forecast. Gauge-cap timing remains unverified.
  The guides describe the golems as a pair whose Demolish damage
  escalates. Original normal-mode footage shows two active golems at
  6:22, with Hashmal still in the arena. The footage shows the
  concurrent tank pickup objective and leaves the enmity and cast
  sequence to packet evidence. [Normal-mode Golem footage](https://www.youtube.com/watch?v=Zi99W7ejtz8&t=381s).
  The matching sheets confirm the three-second Summon 9684
  and four-second Demolish 9686. Summon gives an add instruction and
  each golem's cast gets a separate raidwide forecast. Its damage
  event, death or cast finish retires only that golem's prediction.
  The AI preserves live
  tank ownership and assigns loose golems to available off-tanks by
  actor identity, independently of party list order. Targetable golems
  inside the arena are eligible before they enter combat, while native
  attack exclusions remain in force. Tanks outside the arena do
  not hold a pickup assignment. The boss tank can take them if no
  off-tank remains. The AI gets a soft route to attack range of its
  highest-priority golem, so an assigned pickup can be reached while
  timed hazards still constrain the route. The overlap test uses
  synthetic spawn positions and a simulated hazard. Both retained recordings
  skipped the golem phase, so actual spawn order, escalation and
  repeated-wave timing remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  Jagged Edge 9677 arrives in three waves of three circles in this
  recording. All four sequences begin with ground-targeted casts
  and have no correlated player icon in the retained events.
  The guide describes a red target marker, but nearby player positions
  do not consistently identify the target. The module uses the
  confirmed cast origins without guessing which players should bait
  the next wave. As with
  `SimpleAOEs` danger colors and `Exaflare` previews, the next
  concurrent wave is bright while later casts are shaded. Every
  active cast retains its own timing for the AI. A puddle disappears
  when its own impact arrives, its cast ends, or its helper is removed.
- **Rofocale.** The exact October rows support both 60-by-16 Maverick casts
  and the eight-yalm Unholy Darkness. The Archaeodemon layouts and 199397–199418
  spawns support west, north and east prison destinations, using A, B and C
  as the assignment convention. The wall objects have layout IDs
  7082094, 7082095 and 7082097. Their shared asset
  `sgvf_w_btl_b1275.sgb` contains paired hexagonal collision rings
  at 8.7 and 10 yalms from the center to each side. All three play
  `0x00040008` at 198874–198876, 0.45 seconds before the adds appear,
  and `0x00100020` at 209117–209119, 9.05 seconds after the first cue.
  Fresh positions cross both rings before the second animation.
  During the closed interval there are no sampled crossings of the
  inner ring and only small edge corrections around the outer ring.
  Each `0x00010040` reopening follows its add's death by 0.03–0.05
  seconds. The radar shows the assigned prison from the first cue,
  and the AI receives an 8.75-second entry deadline with a preference
  for the center-facing side. The other prisons publish timed wall
  bands at the same cue so navigation can plan around them before
  closure. Actual closure replaces each forecast with its wall band
  as a current obstacle. A native navigation test uses the recorded
  north-prison position at 208061 and the seven predicted charge steps
  to check that the AI can reach safety inside its assigned prison
  during the overlap. An AI already inside a
  different closed prison attacks the reachable add. The AI treats
  both sides of a closed wall as an approach restriction, so an add
  outside its own closed prison cannot become an automatic target.
  Once the local prison opens, the original alliance assignment
  applies again if its add remains reachable. The AI can help another
  targetable add in an open prison when its assignment is gone.
  The recording shows that every remaining add stayed behind its own
  closed wall until death. The open fallback is tested synthetically.
  Missing closure cues expire and each reopen or removal clears its own
  wall. The exact collision activation within the animation frame and
  server action eligibility at the wall remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [normal-mode text guide](https://news.seagm.com/ffxiv-the-royal-city-of-rabanastre-guide/).
  **Karma.** The cones at 211403, 211981 and 211983 begin after
  the prisons close. For the north and west casts, the reported facing
  matches a fresh target bearing within one degree and the same angle
  appears on the hit. The east cast has no recorded hit and its
  target position sample is 0.60 seconds old. Fresh positions show
  seven east-prison players hit by the north cone at 215521 and
  players in both other prisons hit by the west cone at 216506.
  While its assigned tank has aggro and the add is between casts,
  the module prefers a position just outside the hitbox on the
  outward side. The actual cast locks the displayed direction,
  replaces the aim preference with the dodge, and stays dangerous
  to the tank. A hazard can override the preparation preference.
  Caster death or removal retires only that add's cone.
  [Normal-mode text guide](https://news.seagm.com/ffxiv-the-royal-city-of-rabanastre-guide/).
  The boss going untargetable with a live
  Archaeodemon at 206112 gives prison-side guidance about 3.66 seconds
  before the first of 43 charge effects at 209800. Once his approach matches
  the recorded southeast-to-north route at 208065, the AI and radar get a
  north-side opening rectangle about 1.73 seconds before the first effect.
  The countdown from the untargetable event is conservative. The predicted
  first origin was within 0.09 yalms of the recorded origin and the next four
  were within 1.06 yalms on the same recording. Model state 19 at 209422,
  about 0.42 seconds before the first effect, reconciles that countdown.
  The Action row defines a 10-by-10 rectangle. Eighteen damage and
  knockback recipients have movement samples within 0.10 seconds of
  their effect. Their positions span 2.32 yalms behind the reported
  origin to 3.52 yalms ahead, with a maximum lateral offset of 4.98
  yalms. Several fresh positions more than seven yalms ahead of the
  origin and within the lane receive no effect. Both radar and AI use a
  centered rectangle. The observed positions support this placement
  and leave the exact server boundary unverified.
  Each accepted effect refreshes an estimated 17-degree advance in origin
  and 15-degree advance in facing, with a conservative 0.29-second interval.
  The radar highlights the next step and shades two more. The AI sees all
  seven timed steps. Grouped effects can be delivered out of order, so a
  trailing observation cannot immediately rewind the forecast. Model state
  20 at 224302 ends the forecast shortly after the last effect. Expiry and
  Heavenly Subjugation also clear predictions. Alternate opening routes
  and exact server snapshots remain unverified.
  **Crush Helm.** Three complete sequences start at 133531, 192158 and
  322741. Each self-targeted visual is followed by four 9857 hits from
  distinct helpers, then the boss's 9858 finisher. The helpers' effects
  apply one through four stacks of Physical Vulnerability Up to the
  same recipient. The first hits land 0.85–0.91 seconds after the
  visual effect and the finishers at 3.27–3.29 seconds. The module
  warns at cast start, publishes all five deadlines from the visual
  prediction, and rebases them at the visual effect. Icon 381 at
  133521, 192098 and 322655 marks the actual recipient of all five
  hits in each sequence. It arrives in the cast-start frame or up
  to 0.091 seconds before it. The AI uses the mark for advance
  mitigation when the boss's current target differs from the
  marked player. The 0.25-second pairing window for a preceding
  mark is a policy, and the visual still works on its own if the
  mark was missed. The individual helper hits retire their
  predictions and correct the recipient.
  Only the heavier finisher requests tankbuster mitigation. The
  matching status row confirms Esuna eligibility, so the AI requests
  a cleanse at four observed stacks. In two of the three sequences
  the fourth-stack status update reaches the client after the finisher.
  The mitigation plan therefore runs independently of the cleanse.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [normal-mode text guide](https://news.seagm.com/ffxiv-the-royal-city-of-rabanastre-guide/).
  The Lodestone phase guide reports that Archaeodemon spells can be
  stopped. The retained casts show one Karma cancelled by the
  demon's death at 213253–213256 and two that resolve. They do not
  demonstrate a successful interrupt. A live client interrupt flag
  on Karma or Unholy Darkness now enables the native interrupt
  hint for the reachable assigned demon. The dodge stays active
  until the cast clears. Synthetic checks cover the flag, cast
  resolution and a simultaneous cast across a closed prison wall.
  [Normal-mode Lodestone phase guide](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3596031).
  **Heavenly Subjugation.** The last Archaeodemon dies at 223495,
  about 21.91 seconds before impact. Model state 20 at 224302 starts
  the recorded ascent, and the untargetable boss returns to model 0
  roughly 30 yalms above the arena at 237483. The apex precedes the
  hit by 4.94 seconds. The AI gets a conservative 4.7-second mitigation
  estimate at this elevated transition, replaced by the real 9866
  cast timing. The visual resolves at 240190 and the separate 9867
  helper deals damage 0.07 seconds later at 240273. Mitigation stays
  active through that short gap and clears on hit. The game sheets
  distinguish the visual cast from the rectangular helper effect.
  An independent encounter guide lists 9876 as an alternate Heavenly
  Subjugation. The current Action sheet gives it the same instant
  60-by-60 rectangle as 9867. Either impact clears the raidwide
  forecast and enables the later Trample follow-up. The alternate
  trigger, gauge threshold and damage remain unverified.
  [Encounter action list](https://ffxivguide.akurosia.org/sb/the-royal-city-of-rabanastre.html).
  The Lodestone and community guides describe lateral falloff. The
  cast's heading at 238078 is within 0.62 degrees of the helper
  effect at 240273. At that effect the observed player 23.95 yalms
  from the axis records zero encoded HP damage and loses about 2,205
  shield points by the next HP update. Several fresh positions within
  5.6 yalms record 5,514–13,915 damage, with different mitigation and
  equipment. This supports a preference for leaving the center line.
  The visual cast now displays its axis and gives the native planner
  a soft sideways goal. The score stops increasing after 12 yalms as
  a movement policy. Mitigation remains active for the raid and other
  hazards keep priority. The sample does not establish a safe boundary
  or a gauge-cap response.
  [Normal-mode Lodestone guide](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3596031).
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Trample.** Both normal-mode guides identify the exterior of the figure
  eight as safe throughout the charge. The illustrated guide also shows
  closer gaps. Fresh damage recipients at 276759 include a player 1.55
  yalms behind the cast origin and another about 5.03 yalms off axis.
  The bright opening and its AI warning keep the ten-yalm forward
  reach and allow two yalms behind and 5.1 yalms on either side as
  conservative margins. The exact damage boundary remains unverified.
  The October figure-eight omen mesh, projected at the
  particle's horizontal scale of 10, fits in two 18-yalm disks centered
  14.1 yalms forward and back from the cast origin. The emitter/particle
  rotation and reflection are consistent with this axis, and all 12
  recorded hit samples with positions newer than 0.15 seconds fall inside
  this envelope across the two observed origins and facings. Rotating it
  perpendicular to the cast puts an observed hit outside. The largest
  straight-line distance to an in-bounds exterior sample was 14.11 and
  23.34 yalms for the two casts, using players whose positions had been
  updated within one second of cast start. These are same-recording
  checks and omit action locks, travel latency and concurrent hazards.
  The initial single deadline left the edge-side case at 273436 with
  about 0.70 seconds of negative navigation leeway at walking speed.
  The normal-mode guide clip shows the forward loop first, then the
  rear loop. The recording puts the 9851 center crossing 1.39–1.44
  seconds after the opening hit, with a forward-side hit on 9848 and
  a rear-side hit on 9854. The module now constrains the first loop
  and the crossing from 0.25 seconds before cast completion. It
  constrains the rear loop from 0.95 seconds after completion, with
  the crossing event bringing that estimate forward if needed and
  retiring the first loop. The opening lane stays filled and outlines
  show the first loop, crossing and shaded rear preview. The rear
  outline brightens at the crossing. The final helper effect,
  interruption, boss removal/death or timeout clears the remaining
  constraints. At the projected scale, each lobe center is about
  2.45 yalms from the nearest mesh edge. In the second orientation
  a player sampled 1.87 and 1.61 yalms from the rear center on
  pulses 278827 and 279045 was absent from both recipient lists.
  A 1.54-yalm sample before pulse 279227 was 0.136 seconds old
  and also absent from the effect. This corroborates the documented
  gaps without proving the exact snapshot or inner boundary.
  The AI and radar use 1.5-yalm inner gaps for both symmetric loops.
  The 18-yalm outer envelope and crossing stay in force. The
  route checks use the recorded rotations and a synthetic approach
  beside each in-bounds rear gap. [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [illustrated Trample guide and clip](https://www.nyacchi.com/rabanastre/).
  **Cry of Victory follow-up.** The 9845 casts begin 2.31 and 2.37
  seconds after the two recorded Chariot effects. The later 10060
  starts 1.25 seconds after Trample's final pulse at 279227. Its origin
  matches the late Trample cast position, but its facing differs
  from the boss's last pre-cast pose. The first Trample instead leads
  to Maverick. After Heavenly Subjugation resolves, a Trample cast
  gives a soft preference toward its start and warns of Cry of Victory.
  Chariot gives the same preparation after its charge resolves,
  using Rofocale's updated position. All active Trample hazards still
  bind the route. The actual Cry cast replaces the preference with
  its confirmed half-arena cone. The offline check uses a fresh player
  pose near the end of late Trample and simulates walking during
  the recorded turn window. Different delays, final server snapshots
  and actual movement under action locks remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Maverick and Dark Geas.** Timeline 7747 precedes the first five
  Maverick casts by roughly 0.67–4.67 seconds at 175445–177856. It
  precedes the later pair at 313239–313241 by 1.04 seconds. The
  warning requires an inward-facing pose near the rim because the
  same timeline also plays at 187335 and 243547 as the boss returns.
  The boss's first-wave cue arrives before its move, so the module
  waits for a matching pose. Each cue starts a shaded lane using a
  conservative 6.5-second estimate to impact. Any of the three
  sheet-backed casts replaces that clone's preview with its confirmed
  position and timing. Cactbot reports the rare 10036 variant in a
  separate post-Dark Geas log. Both the current Action sheet and the
  September archive row list a 60-by-16 rectangle and a one-second
  base cast. The module takes the deadline from the observed cast.
  The synthetic check covers preview replacement, lane width and
  retirement for 10036. Its actual pre-cast cue, origin and server
  snapshot remain unverified. Confirmed simultaneous lanes remain
  bright.
  No clone count is assumed. The Lodestone report describes three,
  four and five lanes after Dark Geas, while the community guide
  describes five. The retained pull ends before that sequence.
  The native route check uses all five recorded early lanes at
  178567–178587 and a fresh player position. Synthetic subsets
  verify that three and four lanes work without a fixed count.
  An observed resolution retires only its source's lane, and boss
  death clears the wave. A separate synthetic overlap places two
  layout-backed circles around the recorded lanes. It checks that
  the imminent wave blocks the unsafe interaction while keeping
  the other approach available. The fixture does not establish the
  actual post-Dark Geas layout or timing. In a separate normal-mode
  pull, multiple labeled Dark Circles are visible at 4:42.7, and a
  targeted circle and cast bar are still visible in darkness at 4:50.3.
  The arena is lit by 4:53.1, with several intersecting charge lanes.
  At 4:56.6 the lanes are still visible and players occupy a clear
  gap at the rim. This corroborates showing the live interactions
  during darkness and retaining individual lane constraints for the
  transition. The footage gives a visual sequence and a used gap.
  It leaves the packet cue, cast ID, coordinates, interaction
  acknowledgement and last-safe movement timing unresolved.
  [Independent normal-mode pull](https://www.youtube.com/watch?v=CuBJgTsaXL8&t=282s).
  [Normal-mode Lodestone report](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3596031),
  [Cactbot's post-Dark Geas timeline](https://github.com/OverlayPlugin/cactbot/blob/8a680429b0d012b922528acf66f612b0f4153dfa/ui/raidboss/data/04-sb/alliance/royal_city_of_rabanastre.txt#L216-L235),
  [community raid guide](https://consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  Dark Circle markers and the interaction instruction now persist
  while eligible objects are visible. Native navigation receives all
  safe approach points together. A circle behind a lane or trap
  cannot monopolize the objective based on straight-line distance.
  Dark Geas 9864 starts at 339751 and resolves at 342397, about
  2.99 seconds later. During the cast, before any targetable circle
  appears, the radar shows shaded outlines and the AI prefers
  safe approach positions from the twelve EventObject placements
  in the matching `planmap.lgb` layer `LVD_BOSS_03`. The object
  IDs are 2009076 and 2009203–2009213. This gives travel time
  before the interaction and adds no avoidance constraint.
  Targetable actors replace the layout previews immediately.
  The original shows the effect and cast end on the same frame, with
  no live circle through the boss's death 0.44 seconds later. A
  successful effect holds the layout goal and shaded radar for up to
  0.75 seconds while waiting for the first live circle. This window is
  a staging policy. An interrupted cast or boss removal clears the
  preview, and the first eligible circle ends the wait even if it
  becomes unavailable again. The recording provides a fresh player position at
  cast start for the native navigation test. The normal-mode video
  shows several named, lit circles active together during darkness,
  alongside Bleeding applications. The footage confirms the player
  objective and its presentation, but does not expose the object
  IDs, activation packets or server interaction timing.
  [Normal-mode guide footage at 9:12](https://www.youtube.com/watch?v=Zi99W7ejtz8&t=552s).
  The actual interaction request is issued within one yalm, provided
  the player's position is safe through the interaction margin.
  Untimed forbidden zones use the same immediate-danger convention as
  the native movement raster. A Patch 7.1 account also describes
  Maverick casting during darkness, so the AI keeps any confirmed
  clone lanes binding throughout the interaction. [Patch 7.1 Lodestone guide](https://jp.finalfantasyxiv.com/lodestone/character/3169336/blog/5530031/).
  The client still checks actual interaction range. The 2.1-second
  margin combines navigation's one-second cushion and the executor's
  1.1-second throttle. Approach screening adds estimated walking
  time and is rechecked from the actual position before interacting.
  Embrace's bursts at 266316, 267185 and 337691 produce removable
  Bind 564. Bound party members now get a native cleanse request.
  The retained pull ended about 0.49 seconds after the Dark Geas
  effect and contains no active Dark Circles. A fresh archive search
  for action 9864 returned only this recording. A search for the
  layout's Dark Circle OID 2009076 returned none.
  In the synthetic detour case, advancing the player to the native
  route endpoint produces the interaction request for the reachable
  circle. A circle that remains targetable after boss death loses
  its radar marker, movement goal, and interaction request. The
  native executor checks client range and animation lock before
  sending the interaction. An in-range circle whose retry window is
  open reserves the next free action window from automatic rotation.
  While a valid priority request is pending, the combat planner
  releases elective mobility and uses native hazard-aware routing.
  A circle that becomes untargetable or loses the current interaction
  request releases its reservation.
  Manual movement and queued player actions take precedence. The
  synthetic check covers the priority request at the navigation
  endpoint, action selection and per-frame reset. It does not verify
  a live server acknowledgement.
  Targetability transitions, interaction time, and the numeric
  transition to Maverick still need live or replay evidence. [Normal-mode illustrated guide](https://ru-eka.com/archives/5695),
  [post-Dark Geas lane observations](https://jp.finalfantasyxiv.com/lodestone/character/753030/blog/3596031).
- **Argath.** The truth-mask movement command now requests successive
  short steps near status expiry through ordinary hazard-aware navigation,
  so it can respect the arena and concurrent attacks. The text counts
  down to the same 1.25-second movement window or the native one-second
  stop window, then keeps the active instruction visible through The
  Word. These windows are conservative policies. At 498140 a Lies
  recipient had no fresh movement and took damage after an autoattack
  0.37 seconds earlier. At 529042 another Lies recipient was hit after
  actively moving through resolution. Both movement and attacks are
  included in the stop warning.
  Across The Word effects at 416211, 498140, 518376 and 529042, the
  assignment status disappears 35–81 ms before the effect. Its
  advertised expiry is 89–158 ms before the effect. The module
  retains a naturally expiring order until The Word, with a
  0.3-second fallback after nominal expiry. Early cancellation,
  death and a new mask release the old order. Offline checks cover
  both gaze directions, the stop and move orders, and a simultaneous
  hazard during the movement order. Mask actions precede the paired
  command icons and statuses by 2.97–3.00 seconds and give an immediate
  instruction to follow or invert the upcoming order. Fresh facing
  samples at 416211 and 518376 show Truth gaze failures 64.2 and 86.3
  degrees from Argath, beyond the shared 45-degree default. The Truth
  warning and native facing now conservatively require the rear
  half-plane. A fresh Lies failure at 529042 was 59.1 degrees from
  Argath, so Lies retains the conservative 45-degree facing cone.
  The exact server boundaries and movement snapshot remain unverified.
  [Normal-mode Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre).
  Added Trepidation guidance and Unholy Sacrifice mitigation. The
  `LVD_BOSS_04` layout contains eight candidate trigger boxes around the
  middle. Meteor automation is outside this goal.
  **Royal Blood.** The 5.02-second summon at 437754 gives the AI a
  soft gathering goal while the cast is active. Shades spawn
  0.29–0.34 seconds after the effect and become targetable together at
  447500–447526, about 5.55–5.61 seconds after spawning. Unassigned
  players retain the gathering preference through that window.
  Attack range then becomes the approach goal for ordinary Shades
  as well as those casting Unholy Sacrifice. Existing hazards still
  bind both routes. All eight Shades initially target the same player
  on spawn, then receive distinct player assignments through tether
  17 at 443604–443614. The player links clear together at
  447572–447587, about five seconds later. The Shades keep their
  assigned attack targets through the subsequent Shade-to-Shade
  links and attack those same players with action 870. The radar
  and soft native grouping goal now follow that continuing
  assignment until the Shade dies, despawns or changes attack
  target. The AI can also recover an assignment from an observed
  auto-attack if it missed the original tether. The community and
  Lodestone guides recommend grouping for AoE damage. The SEAGM
  guide recommends spreading to avoid links. In the retained clear,
  the links appeared even while the Shades were still spread near
  their spawn ring, and the attack targets stayed the same. Grouping
  remains a soft preference for killing Shades with AoE. The exact
  purpose and range of the Shade-to-Shade link remain unverified.
  [Community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre),
  [Lodestone guide](https://na.finalfantasyxiv.com/lodestone/character/1039312/blog/4025957/),
  [SEAGM guide](https://news.seagm.com/ffxiv-the-royal-city-of-rabanastre-guide/).
  Eligible Shards
  outrank ordinary Shades, while a Shade actively casting Unholy
  Sacrifice takes priority. Death removes that Shade's damage
  prediction. The recorded Shades all died without casting Sacrifice.
  **Empty Soul and Dark Ultima.** The shard instruction starts at
  Empty Soul cast 463703. Eight Shards appear at 467009–467071 and
  become targetable together at 469243–469274, about 2.57–2.64
  seconds after spawning. The AI can use that window to approach
  attack range around the observed actors. The radar shades them
  until they are targetable. The preparation goal leaves all
  movement hazards in force. When the first Shard can be attacked,
  the AI switches to a soft approach to any eligible Shard in attack
  range. A Shard already in range wins over another Shard that is
  farther away. This prevents the current target from holding the AI
  on a blocked approach. An active Unholy Sacrifice takes priority
  for both targeting and approach. The native route test uses a
  recorded starting position and Shard locations with a synthetic
  hazard at the nearer one before and after targetability. The guides agree that Shards must die
  before the gauge fills. This clear does not establish the exact
  placement rule. The
  last recorded death at 478970 precedes Dark Ultima cast 481900 by
  3.11 seconds and impact 483619 by 5.56 seconds. The module publishes
  a conservative 5.25-second mitigation estimate after the last
  death of an observed wave and replaces it with the actual cast.
  A living despawn disarms the estimate. The actual cast still works
  on late module activation or if the gauge caps. The center is a
  soft healing goal during the windup. Gauge-cap timing and
  independent variation in the last-kill delay are unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Platform and Transfiguration.** The phase-four layout places collision
  mesh `z3r1_a4_boss4.pcb` at (106, -679, -386) with unit scale and
  negligible yaw. The matching version-1 mesh has 184 triangles and
  SHA-256 `f17c42e7ff327577886be954fd7db7e3ec42cf0b5f7cc6315f238e73c10fd958`.
  The flat center reaches 26.50 yalms per side. A material change at
  that seam separates it from a flat strip that reaches 27.02 yalms.
  The raised surround is a separate surface. The wider navigation
  square has a 26-yalm half-width and opens only on the recorded
  inactive map signature, including early mask updates at 410628,
  412822 and 416142. Unknown state and reset use the 24-yalm half-width.
  Dark Ultima cast 481900 forecasts the inner square on radar and sets a
  native deadline 0.25 seconds before the predicted cast finish. The hit
  at 483619 applies the hard bounds. The active map update at 483651
  confirms them about 44 ms later. An interrupted cast clears the preview,
  and reset 558342, boss removal or phase exit restore the conservative
  default. The collision material seam does not locate Transfiguration.
  Fresh status gains at 501782 and 530304 occurred 25.40 and 25.52 yalms
  west, and 510797 occurred 25.64 east and 26.50 north. This supports
  keeping the 24-yalm inset after the ultimate. The exact damage boundary
  and last-safe departure remain unverified.
  [FFXIVClientStructs PCB format](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Mesh.cs),
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Unrelenting.** Both five-cone casts share the boss's origin
  and are spaced at 30-degree intervals. The boss's visual 9771
  at 458424 and 509674 is only 45–61 ms ahead of the helper casts.
  It supplies text guidance while the helpers establish the actual
  fan. The second fan lands about 0.69 seconds after the local
  Gnawing Dread status ends, leaving little time for ordinary
  steering. While still misdirected and in a cone, the AI prefers
  endpoints at least one yalm clear of the fan. Tighter gaps
  remain eligible, and a player already in a gap gets no extra
  movement preference. All five simultaneous cones stay visible
  until their individual resolution or cancellation. Offline routing
  uses the recorded origin and facings. The arrow's phase and
  controller travel remain unverified.
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Gnawing Dread into Coldblood.** The two Gnawing Dread casts at
  500543 and 530681 each give about five seconds to prepare. The first is
  followed by Unrelenting. In the second, the local 1422 status arrives
  at 533622, 0.87 seconds after Gnawing Dread resolves, and the donut
  first identifies its origin at cast 534783, 3.32 seconds after status
  gain. The module gives players a radar outline and the AI an optional
  staging area midway between the caster and arena center during
  Gnawing Dread. This keeps a buffer from the rim without assuming which
  follow-up occurs. Argath's self-targeted departure 9765 at 425044 and
  534057 precedes proximity and donut casts by 2.04 and 2.02 seconds.
  At departure his position still reflects the previous location. The
  move to the cast location appears only 50 and 36 ms before the cast.
  The shared cue warns early and gives players near the rim a soft goal
  to step inward unless the arrow already controls their movement.
  The 18-yalm staging radius is a routing preference. It does not
  encode hazard geometry. The actual cast replaces the cue, with
  retargetability, encounter death and expiry clearing a cancelled cue.
  The offline test verifies that the AI can reach staging from a
  synthetic rim position within the observed lead time. The actual
  Coldblood cast keeps the donut constraint
  and adds a preference for its safe center. The native arrow controller
  takes over for each player who has status 1422, including when the
  module starts late. The local status expired at 542318, 2.38 seconds
  before the donut resolved, so normal steering resumes during that cast.
  An offline case uses the recorded boss and a fresh player position
  from 530681 to check both routes. Two fresh player samples at status
  loss were still 15.87 and 11.52 yalms from the safe center. The first
  stopped inside by 543223, about 0.07 seconds before the effect. With
  only 2.38 seconds between loss and impact, the standard one-second
  navigation margin leaves less running time than the first position
  would require. The AI can start toward the confirmed opening during
  misdirection. The 4.5-yalm buffered interior gives equal goal scores
  so it can stop near the edge. The full 5-yalm hazard boundary still
  applies. The native follow controller now forwards the arrow heading
  it checked against the navigation map. Previously it forwarded the
  waypoint bearing, which the movement hook could reject even after the
  controller had found a usable arrow. Synthetic headings exercise an
  oblique approach toward the recorded opening, waiting for the arrow,
  and a blocked path. The retained raw index preserves 520 arrow
  updates between local status gain and loss, without angle payloads.
  It has two consecutive gaps of 1.329 and 0.847 seconds at 534853–536610.
  In the first gap the local player moves from (101.21, −391.76) to
  (102.93, −399.44). In the second they reach (105.17, −403.90) inside
  the opening. The movement during those gaps is consistent with a
  locked heading. Native navigation tests use the direction inferred
  from each leg to verify that both follow and normal movement can
  accept it on the Coldblood map. If cast leeway runs out, normal
  movement preserves the checked heading or waits for a usable
  arrow. While the arrow is active, both automatic movement owners
  use a fresh native route instead of an optimized combat route
  that assumes ordinary steering. This drops the prior commitment
  and leaves the live arrow decision to the movement controller.
  The explicit special-mode opt-out retains its configured behavior.
  Ordinary steering resumes after the status ends. Exact arrow
  phase, input timing and the final server snapshot remain unverified.
  Independent healer footage shows the direction indicators while
  the Coldblood donut is visible at 0:13.6, with the opening close to
  the rim. At 0:17.6 the indicators are gone while the opening is
  still visible and players are gathering inside it. This matches
  the observed ordering of arrow expiry and donut resolution. The
  footage leaves the server snapshot and available travel time
  unresolved. [Independent Coldblood footage](https://www.youtube.com/watch?v=0CcJfTKgcTU&t=13s).
  [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).
  **Judgment Blade floor squares.** Four `0x1EA7ED` objects spawn at
  418666–418669 on the cardinal 16-yalm grid positions. They match four
  of the eight `LVD_BOSS_04` EventRange boxes. The statically placed
  object of the same type has an off-grid position and is ignored.
  The active animation `0x00040008` arrives 3.23 seconds after spawn,
  and `0x00010040` retires each square at 431640–431643, after the
  Heartless hits. Status 642 at 423771 applies near the corner of the
  north square, about 0.12 yalms outside the layout's nominal 5.5-yalm
  half sides. The module uses 5.75-yalm half sides as an avoidance
  margin. It predicts from spawn during the cast using a conservative
  2.8-second deadline and treats a tile first seen after the cast as
  immediately active. The AI keeps all visible squares separate from
  the sword bait and Heartless rays. The alternate grid positions
  share the same layout sizes, but only the four-cardinal pattern
  and its overlap with Heartless are recorded here. The two icons
  at 420821 and 420835 precede the two sword placements by about
  8.39 seconds. The sword centers match the marked players'
  latest poses, within 0.02 yalms at update ages of 0.13–0.22 seconds.
  The radar marks the preferred center placement for the assigned
  player until the swords appear. During the observed overlap,
  Heartless resolves at 431325–431349, 2.68 seconds before
  Coldblood at 433341. The radar keeps the active crosses bright
  and shades Coldblood until those crosses retire. The native
  route test feeds all eight rays, four tiles and the proximity
  marker with their own deadlines. The exact
  application snapshot and overlapping Coldblood/misdirection routes
  remain unverified. [Pocket Guide](https://ffxiv.guide/sb/the-royal-city-of-rabanastre),
  [community raid guide](https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre).

The archive searches for Trepidation 9762, its Rail of the Rat action 9764,
Submission Tower 9837, Falling Boulder 9682 and Mateus's Frostwave 9793 returned
no retained recordings. Those results do not establish absence from the duty.

## Focused validation

`tests/ModuleRegression/Rabanastre.cs` covers generated component activation,
simultaneous/duplicate tower creation, early timing, per-instance/out-of-order
retirement, disappearance, lane width boundaries, AI publication and cast cleanup.
The follow-up adds synthetic coverage for hammer reconciliation, Rock Cutter
aim and mitigation, Jagged Edge and Earth Shaker wave advancement,
staggered tower raidwides and Impact reconciliation, Trample
interruption, exterior routing, midpoint overlap and resolution, mask inversion and reset, spiral advancement,
Dark Circle eligibility, hazard-aware selection and detour, early Maverick
and Heartless rays, separate cast variants, alliance
stack eligibility, Mateus cleave lifecycle and sphere assignments, early
Blizzard IV mitigation, Azure Guard assignments and Frostwave cleanup,
air-first and bait-first Snowpierce behavior, bleeding square warning and independent retirement,
orbit preparation and launch timing, angle wrap, out-of-order
orbit effects, cleanup and timed keep-moving guidance. The navigation cases
also feed the native pathfinder positions and times from selected frames of
the retained clear. They cover the two Trample orientations at 165021 and
273436, the six Blizzard IV casts at 116694–116735, overlapping Towerfall
and Jagged Edge at 412085, and Heartless with active bleeding squares at
427185. A separate Argath test places the player on a recorded rim-hit
position before the forecasted Dark Ultima closure and checks the inward
route, interruption, active state and reset. The tests check that the
resulting route remains in bounds, reaches an
endpoint clear of every active or predicted hazard, and has nonnegative
leeway under the planner's timing model. They do not exercise the live
movement controller or establish server hit times.
