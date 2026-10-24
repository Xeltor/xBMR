# Native combat assistance implementation status

This is an implementation inventory, not a completion or live-acceptance claim.
Standalone XCAI is not a runtime dependency of the new combat-assistance code.
All 72 provenance-identified XCAI-only integration registrations have been
removed. The 67 upstream endpoint names remain. Cutover is not yet complete:
remaining behavioral parity and live validation are recorded below.

## Repair release candidate — 2026-10-24

Release candidate `7.5.6.35.20` includes the repaired native targeting, combat
range, configured healing demand, rotational mobility/preparation, Sprint and
escape commitment. The pre-version-bump implementation builds and passes 43,081
native assertions, installed RSR contracts and nine repository tests. The dated
regression record identifies the exact loaded development artifacts and the
completed NPC comparisons. The maintainer reports substantially improved
similarity to XCAI; the latest Griffin recording confirms delivered movement
actions and scoped mechanic avoidance. The first-boss hide recurrence, remaining
avoidable Talons, caster interruptions, out-of-range healing recovery and
micro-stepping remain in TODO.md. This is a practical repair release, not a claim
of universal mechanic, role or mobility parity. Keep further changes tied to
concrete failures or an evidence-backed micro-stepping diagnosis.

The first maintainer Mistwake run failed gameplay acceptance: active-mechanic
movement, acquisition, range/mobility recovery and out-of-combat facing regressed.
Use the current, shortened `TODO.md` for actual priorities. Its explicit deferred
tuning and ordinary-play checks are not blanket release gates. The new repairs
have source and offline regression evidence; the failed run is not acceptance of
those repairs. Older matrices below are history, not additional required work.
See [the repair record](regressions-2026-10-24.md).

## Current completion pass — 2026-10-24

TODO.md is the authoritative current checklist. The following chronology records
older checkpoints and does not reopen superseded requirements. Feature families have explicit source/settings responsibility mappings
(385 sources, 100 settings), but the subsequent fight analysis found omitted
rotational mobility and proactive configured healing need; those mappings do
not establish implementation completeness or gameplay parity. Source mapping does not assert that every
standalone helper was copied. First walking publication now includes useful
post-action friendly-field recovery rather than stopping before that continuation.
The old unassigned DeferLeyLinesPlacement callback is replaced by direct original
RSR selection in RsrLeyLinesPlacement; it is not an unimplemented native service.

Unknown-knockback learning, between-pull tank following, ToF and the camera
subcommand are excluded. Further authored-knockback expansion/polishing is
explicitly deferred by user. BMR recovery still handles unsupported situations.
PCT unknown footprint/timing remains a stated bounded capability, not permission
to invent geometry. Remaining feature acceptance belongs to dungeon/lifecycle
checks. Final managed build, 42,863 native assertions, all 19 broader suites, installed
RSR contracts and nine repository tests passed. The actual newer-upstream merge
also built and passed the same native/broader checks. See handoff.md and
validation-current.json for exact identities. That earlier checkpoint performed no final deployment or gameplay acceptance.
It is superseded by the maintainer's subsequent active gameplay repair goal;
see TODO.md and the dated regression record for current implementation/testing.

## Scope change — 2026-10-23

The user explicitly retired automatic tank following between pulls. The current
source removes `TrackDungeonParty`, its Material control and its complete private
travel subsystem: stationary catch-up, inferred interactions, crossings, transport,
stairs/drops, travel waiting and pacing. Old enabled values are ignored on both
legacy import and integrated restart; the original standalone file remains intact.
Combat `MaintainPullContact` still requires player combat, observed tank movement,
a corroborated pack and native authority. Explicitly selected native follow stays
independent. Contact retirement clears a previous committed walk. The interaction
dispatcher has its original contract; no assistance-only submission hook remains.
The chronological tracking/interaction entries below describe superseded artifacts
and are no longer outstanding feature or live acceptance requirements.

The last intermediate automatic crossing/transport artifact passed 41,797 native
assertions before retirement. Those feature-only tests and fixtures were removed
with the code. The current reduced suite's 41,695 assertions cover the surviving
behavior, including import/restart retirement and combat-only authority. Counts
must not be read as a feature-parity percentage.

## Resumed implementation — 2026-10-23

The user resumed the migration using TODO.md as an active goal. The current
artifact has SHA-256
`a009cd95581a19d3ccfaef679308c450941a0e996d08620d7b08d90200653d10`,
MVID `1ef7b26d-cb13-430f-83b1-ff8b609d1b8c`. It built successfully and passed
42,286 native assertions, 19 broader suites, nine repository tests and installed
RSR 7.5.7.6 contracts. Watcher deployment and loaded MVID were verified at
22:28 UTC with native AI inactive, RSR Off, XCAI unloaded and no combat. The
following chronological entries refer to earlier artifacts unless stated otherwise.

Pictomancer's selected current action now has matched native center-return
cast/recast calibration, including instant GCD recovery. Landscape identity,
original expiry and other modifier/stack conditions constrain reuse. The Smudge
field opt-out survives later walking/waiting and exact revalidation, while useful
walking and independent legal safety recovery remain available. Ninety-six finite
alternatives and native tests pass. Full footprint, after-action parity and live
Smudge delivery remain open; sampled coverage is not promoted to an exact radius.
See `job-evidence.md` for the matching-version observations and restored client.

The search now excludes only proved inferior execution prefixes and resource
spends, before enumerating mobility landings. A 6,400-cell mobility-rich synthetic
case completed in 28.852 ms; small maps agree with pruning disabled. Charge-return
waits, observed automatic transport progress and shared floor-height observations
have new native regressions. RSR's selected immediate party ability now precedes
its GCD in one native action sequence; commit waits for both opportunities.
The obsolete delayed-heal selector was retired because extra recipients cannot
purchase loss or delay of the current heal. Intermediate action positions use
the existing overlay markers. Red Mage continuation is now a timed native recovery objective, using RSR combo
state and the original game countdown. Supported starter/AoE preparation is now
implemented below; actual job resampling and delivery remain live-pending.
Further feature parity remains open in TODO.md.

DevBridge is available again. Bounded BLM mechanics checks corroborated native
inside/outside cast/recast observations, owned field/buff arrival, Retrace's
0.6-second lock and unchanged remaining duration. Retrace replaces the ground
object ID; this exposed and repaired a concrete assumption in the new observer.
Observed owned Retrace plus the unique live replacement now preserves the
original effect identity/deadline; unexplained replacements cannot inherit it.
Native tests cover the consumed route prefix, identity, expiry, foreign events,
object gaps and a new placement. A separate stopped-navigation check corroborated
Between the Lines through its actual landing and 0.8-second action effect.
These checks left AI/RSR off and do not establish integrated executor or combat
acceptance. Placement/scoped selection and the other TODO.md items remain open.

## Earlier return and command checkpoint

Ley Lines current-spell returns now use matching native inside/outside cast/recast
observations in the joint route search, with shared owned object/status evidence.
No fixed speed multiplier is assumed. Original field/modifier deadlines,
abilities-only entry, charge preservation, native safe landings and current-cycle
protection have regressions. Lost speed evidence invalidates the original cast
promise. Placement, scoped selection, Retrace and full after-action/live parity
remain open. The existing plan/return/action-stop overlay primitives are reused.

Legacy `/xcai` commands remain supported, except the camera subcommand explicitly
retired by the user. Camera settings remain in the Material interface. Removed
camera arguments show help without mutating saved preferences. The isolated
artifact above passed 42,144 native assertions, 19 broader suites, installed RSR
contracts and nine repository tests; no watched-DLL deployment occurred.

## Earlier native field relocation and recovery checkpoint

That checkpoint added native Retrace to joint planning/execution. It
preserves field identity/original expiry and changes later return anchors. Per-action
native recast and recovery now participate before resources, including for instant
spells. Current delivery/recipients remain first. Exact validation rejects lost
cast/recast evidence, uncorroborated relocation and a weave shortened enough to
delay the selected spell. Unknown outside field timing reports ActionTimingEvidence;
no data-sheet or fixed-multiplier guess becomes a safety proof.

The 42,144 native assertions include a 144-case independent delivery/recovery
reference, field lifecycle and resource-pruning comparisons. Installed RSR Retrace
selector contracts, 19 broader suites and nine repository tests passed. The normal
6,400-cell synthetic search measured 49.244ms median and 125.609ms maximum; the
mobility workload was 39.986ms. These are not live BLM frame-time measurements.
No DLL was deployed. Actual buff/object/animation-lock/recast delivery, placement,
scoped Ley Lines selection and full after-action parity remain open in TODO.md.

## Earlier checkpoint — 2026-10-23

The user requested a stop at this verified checkpoint to switch models. The
complete continuation checklist is in [`../../TODO.md`](../../TODO.md). Do not
interpret the older chronological artifact paragraphs below as current deployment
or final acceptance.

The latest isolated artifact is SHA-256
`5f4aa0145fa837acb1bfa4f08e77e224b163154c3fd4827b77c19f47d468da4d`,
MVID `22da086a-d2ec-4827-ae60-d9d1bdff6b29`. It built with no errors and six existing
upstream warnings, then passed 41,539 native assertions, 19 broader suites, nine
repository tests and installed RSR 7.5.7.6 contract checks. It is **not deployed**.

Recent changes compare equally timely walking alternatives before initial
publication, capture the selected RSR action and positional together, feed
confirmed physical corridor blocks into shared navigation, and add a partial
native tank-contact region. Their new deterministic checks pass. Full dungeon
contact, job-specific policy, targeting, knockback and live parity remain open.

Dummy checks on preceding builds demonstrated movement and action delivery but
also exposed incomplete destination selection and a possible physical obstruction.
The latest corrections need comparable live validation. At the final read-only
client check, AI and out-of-combat following were off, the target was clear,
standalone XCAI was unloaded and RSR remained in its original Henched mode.
The client retains MVID `a54ce466-900b-4587-b820-e6015394fd45`, an earlier test build.
The original legacy settings file remains unchanged. The upstream trial recorded
below predates later changes and must be refreshed before final acceptance.

## Pinned sources

- xBMR implementation base: `f1de3550b1d96bfd8f153d7b4a7ffe53e5e0d530`.
- XCAI policy and interface source: `4a0f7968da8a743e8971973821cb9f6f1387198c`.
- Recorded BMR upstream base: `be8ff771cc545f8f8b5b548b5dd7271d80460f89`.
- Newer upstream revision exercised in an intermediate trial merge:
  `ae64e3309905dafad0952dbe72680add167591f9`.

The current implementation snapshot `6dab025f39ff44a85835dfbe930ec8f28c2ce7b1`
was trial-merged with that newer revision as
`9a679675c3b84fcc39da31e92bc5987c7b1900a3`, without conflicts. The merged
artifact built successfully and passed all 22,951 native assertions plus the 19
encounter/Questionable regression suites. Its SHA-256 is
`b4ae749ee1a099c3705e3a0122bfeb02e4a207f6703f4337969673376ab63518`.
This supersedes the earlier settings/UI-only trial. Further implementation still
requires a refreshed final trial; no upstream adoption or release was performed.

## Implemented foundations

- One movement service and immutable native capture for normal movement and AI
  following, with owner cancellation, stale-result rejection and native recovery.
  Explicit preset destinations now join the same publication and executor with
  exact endpoints, native detours and original arrival deadlines. Native recovery
  uses the shared executor with fresh executable-segment checks; emergency egress
  retains its native classification rather than becoming a claimed safe plan.
- Full-map search retaining differences in action delivery, time, resources and
  safety. Current action delivery and its individual recipients precede extra
  coverage and opaque position scores; native goal rankings remain intact among
  equivalent executions and for native rotations. No default four-millisecond
  or 8,192-expansion optimization limit is imposed. Freshness, departure and
  explicit memory/sequence bounds remain distinguishable from completed search.
- Typed current RSR action/recipient evidence, exact AoE coverage and positional
  checks. No invented future attacks and no BMR rotation-prediction adapter.
- Staged walking, waiting, rotation delivery and mobility execution. Target,
  ground, directional and return mechanics use native action definitions and
  exact final validation. Saved movement opt-outs and RSR action opt-outs apply.
- Authored no-movement/pyretic finish times and forbidden directions. Unknown
  expiry is not inferred. Cooldowns and shared job-gauge costs are distinct.
- Material BMRAI window, native activation bindings, versioned settings import,
  coexistence gating and recovery-journal reconciliation.
- Native camera scene capture with retained manual pause and collision behavior.
- Synchronous native action-facing suppression during manual movement, without
  a persisted game-setting override or cross-plugin restoration lease.
- Native support/raise observations, scoped RSR recipient and mitigation selection,
  post-resurrection healing, and the optional melee ranged-fallback correction.
  Healer-selection and Ley Lines placement adapters still need policy wiring.
- Native replay decoding through typed operations, archive format/privacy
  compatibility, and opt-in sharing with the existing custody receipts.
- Current native plan/support overlays using the existing drawing primitives.
- Stable party-facing finishes an arrived plan only during downtime. Formation
  memory and deterministic party lanes participate before the first safety move,
  after combat, native goals, resources and continuity. Extra AoE/heal coverage
  opt-outs never remove required recipients from the selected action.
- The migration command is an unhandled-command fallback, not a competing
  `/xcai` registration. A subsequently loaded standalone plugin can register its
  own command. Native command dispatch and ownership always take precedence.
- Shared geometry, displacement, route, physical-probe and replay services moved
  out of the deleted integration transport. Native exceptions attempt original
  safety recovery instead of silently disabling movement.
- Current-state `BossModReborn.DevBridge.GetSnapshot` diagnostics, bounded by the
  existing bridge response size while retaining the full plan internally.

The deterministic native suite most recently passed 41,470 assertions, including
utility-resource planning, exact input-change revalidation and forced-march pause
ownership. Obsolete generic reflected-shape copier tests were retired with that unused
service; native raster/goal immutability checks remain. The latest native changes
also remove remaining hypothetical-pose/source-count transport caps. Installed
RSR 7.5.7.6 support/True North and melee fallback contract checks passed after
accounting for its consolidated OnUpdate callback; actual delivery remains untested. Builds use
the managed environment. An intermediate build passed an idle live-load and
settings-import check; actual combat movement and action delivery remain untested.

True North now participates in the same route, timing and charge comparison.
Observed status expiry is binding. The Material overlay marks its planned stage.
RSR retains manual/forced selections and automatic ownership when no fresh native
plan owns the selected opportunity. Forced-march facing protection uses direct
component state and scoped RSR mode restoration; joint march heading optimization
remains unfinished. Native cast control now uses exact producer cast identity and
native action-effect resistance observations. Scoped RSR selection preserves saved
action toggles. Sprint is an expiring utility stage in the same route/resource
search; observed speed expiry and an unknown post-buff baseline cannot credit
unverified travel. Native mobility selectors are scoped per action so RSR cannot
automatically spend a movement action that this planner owns. Installed RSR
metadata checks pass; actual cast-control and mobility delivery remain untested. The input-update carry-forward path retains only a complete
alternative that passes exact revalidation, with an explicit InputChanged bound
and the original input/departure timestamps.

Friendly ground effects now enter the timed search from native action events and
channel statuses. Original expiry survives Retrace; Earthly Star detonation
retires the current effect. Passage keeps its rear-cone geometry. Healing,
mitigation and personal casting benefits remain distinct and cannot displace
required recipients, native goals or useful charges. The overlay and current
diagnostics show the selected effect and its original lifetime.

The native cast-time observation now carries its actual proc and expiry. Search
and final validation use the slower cast after that proc expires or after leaving
an observed haste field. Unobserved future cast-speed gains are not credited.
The no-proc native duration and nominal spell duration provide a conservative
fallback; matching live cast delivery is still required.

Starry Muse's thirty-yalm party-buff radius is not evidence of its Inspiration
footprint. Until that geometry is corroborated, the planner uses the observed
placement center and currently covered pose, with a `FriendlyEffectGeometry`
bound. This is deliberately recorded as incomplete Pictomancer parity.
The official [Black Mage guide](https://na.finalfantasyxiv.com/jobguide/blackmage/)
confirms Ley Lines' twenty-second lifetime and Retrace's unchanged expiry.
The [Astrologian guide](https://na.finalfantasyxiv.com/jobguide/astrologian/)
distinguishes Collective Unconscious's thirty-yalm mitigation and eight-yalm
healing ranges. The [Pictomancer guide](https://na.finalfantasyxiv.com/jobguide/pictomancer/)
distinguishes the party damage buff from Inspiration and Hyperphantasia.

## Remaining cutover work

- Complete all policy/settings dispositions in `settings-inventory.json` and all
  source dispositions in `source-inventory.json`. Persisting a setting does not
  establish behavioral parity.
- Finish mobility parity: rotational setup/actions, exact teleport transitions,
  shared cooldown timing, native physical/projection evidence, and live facing,
  anchor, return and action-delivery checks.
- Finish job-specific assistance, healing selection, targeting, sprint, party
  utility, forced-march/knockback/cast corrections, and vnavmesh-dependent behavior.
  Exercise actual support and mobility delivery against the installed RSR runtime.
- Validate the shared recovery/explicit/automatic executor against actual movement
  and teleporter/projection behavior; no arrival correction or pacing owner remains.
- Complete native replacements for useful deleted transport-era regression tests.
  Contract checks already confirm the 72 removed endpoints and retained upstream
  surface, with no internal calls to the former versioned transport.
- Validate command registration across standalone unload/reload, migration
  repeatability and shared controls; refresh repository instructions and user docs.
- Refresh upstream trial merge, regression validation and representative live
  checks. Watched deployment requires fresh out-of-combat checks and separate
  reload/provenance proof. Publication/feed changes and plugin uninstall are
  separate rollout actions.

ToF cooperative FATE and collection-delivery integration is intentionally excluded.
General BMR arena/FATE safety corrections have native implementations and remain
independent of the excluded companion integration.

## Native defect corrected during integration

`ActionPredicate.AllowDashToTarget` returned approval for pending knockbacks,
forbidden facing and imminent no-movement/pyretic mechanics. Its callers interpret
`true` as permission. Those branches now reject the dash; the optional safety
toggle again behaves as an opt-out rather than disabling all target dashes.
Deterministic regression checks cover these branches. No past live incident has
been attributed to this source defect.

`AIManager.HandleFollowCommand` indexed the missing follow argument when chat
echo was disabled. Argument validation now runs independently of echo output.
This is a verified command compatibility defect, not evidence of a past live crash.

The legacy default tank-facing switch had no behavioral consumer at the pinned
XCAI revision. Native `AIHints.Enemy.DesiredRotation` already defaults to unset;
the standalone code only reported that fact. The redundant control is retired,
the imported value remains readable, and authored native facing stays intact.

## Initial integrated live load

An intermediate artifact loaded through the development watcher with matching
MVID `1320a906-7d77-4e2f-9398-c31e0d7d4d4a` and SHA-256
`244a2ac78f7155a873017127a7d8b45f50c70594cfc6bcb8c316245f50ed0899`.
The native diagnostic provider reported ready settings, no following and no
active movement. All 99 imported preference fields were compared: saved feature
choices were preserved, eleven recovery journals were reconciled and cleared,
and the legacy file remained byte-for-byte unchanged. Serialization type metadata
was omitted from the native preference file. This establishes idle load and first
import only; subsequent code and actual movement/actions still need validation.

Tank stance coordination now consumes native target-matched enmity, retains the
three-second ownership observation and 50/80 percent hysteresis, and yields to
manual selection and saved RSR opt-outs. Holding an enemy or a native pending swap
retains stance. Submission requires an available weave and fresh native authority.
Observed tank-death recovery, authored swaps and ranged recovery now use native
actors and action submission. The current GCD target and each evidenced recipient
remain binding. Automatic hard-target recovery still needs wiring.

The two legacy tank-cleave controls had no active policy consumer at the pinned
source: `TryInjectNative` cleared the cone plan and the private builder had no
caller. They are retired from the interface with imported values retained. This
is not a removal of native hazard avoidance or authored tank-facing requirements.

Live command dispatch with standalone XCAI unloaded confirmed bare `/bmrai`
leaves activation off, `/bmrai ui` toggles the shared native `DrawUI` setting,
and `/xcai config` opens the same window through the migration fallback. The
initial visibility was restored. The game window was absent from the available
desktop capture, so Material rendering remains visually unverified.

## Shared cooldowns and recovery validation

The current isolated artifact has SHA-256
`609cd428b073b6dcda634eaf29bb59867cdd1abf79610806801f0f82a3c8cb38`
and MVID `669070d9-3af3-4a64-a5fe-161e29fb8d59`. Its 41,426 native assertions
include independent exhaustive charge-return comparisons, shared secondary
cooldowns, delayed charge availability, stale cooldown invalidation and a retained
native safety incumbent. The 6,400-cell worker benchmark completed all ten runs:
median 41.044 ms, maximum 169.081 ms. Framework capture measured median 0.607 ms
and maximum 4.147 ms. These synthetic results do not establish live frame time.

Charge regeneration now uses native main and secondary cooldown observations.
Spending a partial stack preserves its running timer; spending from full starts
its timer at execution. The search retains resource states that have different
future availability. Gauge costs never regenerate merely because time passes.
An unobserved secondary repeat duration is an explicit `CooldownEvidence` bound,
not an invented delay or a claim of completed optimization. Live game Action
metadata confirmed secondary groups on the affected movement actions and True
North. This artifact has not yet been deployed; the previous idle-validated build
remains loaded.

## Native ingress and authored transport

`NormalMovement.UpdateMovement` previously entered an out-of-bounds dungeon arena
before checking the disabled-movement strategy and special movement locks. The
native correction now runs after those gates through the shared executor. It
checks the whole entry corridor with existing physical and route services,
retains the saved boundary-entry preference, and withholds an unverified entry.
Synthetic checks cover missing floor, unloaded collision, walls, successive
warnings and no-movement/pyretic constraints. No past live incident is attributed
to this verified source defect.

The joint search now captures authored teleporters as typed compound transitions.
The approach, entrance wait and exact landing have separate checks; intervening
space is never represented as ordinary walking. A pad cannot become an ordinary
stationary action or final destination. Changed exits and ambiguous overlapping
triggers are rejected. The existing executor walks to the entry, waits for native
transport and advances after observing the landing. The overlay retains the same
line/marker primitives and distinguishes the entry and landing. Synthetic tests
pass; live transport and the next full integration build remain outstanding.

## Compulsory forced-march routes

Native component projections now participate as compulsory stages in the joint
search. The planner checks each wait and leg against shared native traversal,
retains the component's destination rules and direction changes, and includes an
onward escape when the landing is temporary. The shared executor prepares an
exact heading and waits for game-controlled movement; it never substitutes a
walking command. Active marches cannot be steered. Fresh native projections and
confirmed RSR Off state are required before preparing a heading. Manual handoff
still wins. Current diagnostic snapshots and the existing overlay show the
compulsory legs and useful destination.

Pending headings are sampled at five-degree intervals plus the current heading.
The result explicitly reports `HeadingSamples`; this is not continuous optimality
or an inherited transport ceiling. Simultaneous independent component projections
need authored composition and do not silently discard one owner. No hazard expiry
is inferred. The existing twelve-second forecast horizon remains disclosed.

Artifact `9dd220ba2b25a387468adcdef4c7a238ba6f0b8bbb0c47f7efa332b7722f0b63`
passed 41,508 native assertions, including small-map exhaustive compulsory-prefix
comparison, changed projection, manual handoff, intervening obstruction, successive
warning, temporary landing with and without escape, current heading and RSR pause
readback tests. Synthetic 72-heading capture averaged 0.154 ms over 100 captures.
These checks do not establish live forced-march acceptance. The running client
still has the preceding tested artifact until a separately verified watcher load.

## Live incumbent handoff correction

A bounded training-dummy check exercised native follow, action-aware movement and
RSR delivery. It exposed a handoff fault: the owner ignored complete verified
worker incumbents while optimization was still searching, allowing a native-only
destination to replace the joint decision. `CommitCandidate` now accepts such a
route as `Bounded / SearchInProgress` once the current action is preserved. It
retains original freshness and the executor's stage progress. Native recovery
still wins at mandatory departure; optional native movement cannot replace an
invalidated joint route while there is time for its replacement.

Artifact `1b71afe33edeee51ceb7a0523586e7288bab4591a97af428a802f47e2b083a44`
passed 41,512 native assertions and 19 broader suites. The watched client loaded
MVID `e0a64d7a-9957-46d9-9850-8094f8f189aa` after a fresh out-of-combat check.
The first dummy test is evidence of movement/action delivery, not acceptance of
movement stability. The temporary follow setting and target were restored; AI
was disabled and the original RSR Henched mode retained. Follow-up live checking
is required for the handoff correction and encounter behavior remains untested.

### Typed displacement services

Removed serialized position arrays and clock strings from native hypothetical displacement and landing inspections. Positions use WPos, directions use Angle, original activation/end clocks use DateTime, blocker identities use ulong, and projected steps are immutable. Native immunity, wall clipping, source indices and actual-actor destination overrides remain unchanged. The source inventory explicitly maps the retired march transport and partial knockback replacements; policy parity is still open. The managed build and 41,681 native assertions passed against SHA-256 `cfdb497931b03ab1eaec95f137a68574ff42ccd6c415b01b8ab8f265466c22c0`, MVID `65974e69-81fa-47aa-9d86-087780285a38`. No deployment occurred. Codex tool discovery still exposes no live DevBridge tools despite the user confirming the service is up.

### Dungeon contact catch-up

Native dungeon tracking now catches up to a stationary observed tank as well as an advancing pull. It stops at the useful boundary, retains the restart margin, uses six yalms out of combat, and never changes the saved or active follow leader. Following gates use the observed leader's combat state, matching native follow semantics; pull-only assistance retains its independent combat opt-in. Actor and zone changes clear previous motion/recovery evidence. The UI keeps both saved choices independently editable, and the README describes activation, migration, commands and current limitations. Doors, authored/observed floor transitions, long-range navigation and loose-enemy transfer remain incomplete. Existing in-world contact markers already render this selected region; no new overlay primitive is needed.

Managed build and 41,699 native assertions passed against SHA-256 `c8cc2d2f2a12499d5aa66b0a636a5bd061b364ceaeb2a1363226f466dfe74c25`, MVID `43c1db19-a8dc-457d-87df-edb0bee1051d`. This is source/synthetic validation, not actual dungeon following or Material rendering acceptance. No watched output was changed.

### Corroborated party interaction

Native two-member departure inference now preserves the old tank corroboration, object identity, original evidence clocks and one-attempt semantics. A typed region enters joint search before an interaction request can be published. The request is published only after movement planning, avoiding an extra high-weight native proximity goal and arrival centering. BMR's existing dispatcher submits it after a scoped authorization that rechecks live object identity/pose/kind/targetability, player pose/cast/HP, movement, locks, dialogs, native safety and collision line of sight. Other authored interactions remain unchanged. The original five-second wait is a shared execution lock, not an IPC pace override or invented server animation duration; a new hazard still uses native recovery. Retiring a contact owner releases its old route and anchors continuity at the current pose.

The former forty-yalm horizontal approach neighborhood and serialized observation throttle/array guard are removed. Native party membership bounds observation; original relevance/corroboration clocks and vertical evidence remain separately classified. No unknown transport landing is forecast. The existing contact overlay marker now labels party interactions, and the bounded current diagnostic snapshot exposes interaction status.

Managed build and 41,731 native assertions passed against SHA-256 `1e804e1634d3bff2d3ffd4e65411743dd47b6055710b1bebbacc000069bd4ada`, MVID `1b1d839a-34ed-489f-91c0-e1b71846b2a4`. Actual door delivery, collision/streaming behavior and transfer success remain live-pending. Automatic walkthrough/transport transitions, floor changes, loose-enemy transfer and long-range navigation remain incomplete. No development DLL was deployed.

### Expanded legacy command compatibility

This intermediate checkpoint recognized the pinned `/xcai` command set through the unhandled
shell fallback. No command registration can collide with a subsequent standalone
constructor. Original handler results, the command registry and loaded-plugin
state retain precedence. Bare `/xcai` and on/off/toggle mutate the same assistance
master switch as the Material interface, preserving native AI activation gates.
The camera command was subsequently removed by explicit user request; its settings
remain in the interface. Knockback commands share preferences and the ordered save queue;
config and notes open native windows. Party commands explain the user-authorized
retirement. Historical transport check names report native current movement/action
and recording-sharing status without restoring probes or serialized self-calls.

Managed plugin/harness builds passed, with 41,719 native assertions, 19 broader
suites, installed RSR contracts, nine repository tests and whitespace verification.
The native tests cover whitespace/case/invalid input, original-handler precedence,
loading/coexistence write rejection, shared controls and persistence after restart.
The exact plugin SHA-256 is
`5cc0bd0612028ac27e5aeb01f30d5799c6a4be5e457337e97b0b0921b832270b`,
MVID `fa09b8e7-4bde-4385-a966-ceee9d01c68e`. No DLL was deployed. Expanded actual
chat/window delivery and standalone load/unload behavior remain live-pending.

### Native Red Mage preparation and execution timing

Supported Reborn starter policy and Beiruta's observed out-of-range hold now
supplement native ST/AoE continuation. Read-only fields/properties supply frozen
intent; no CanUse or private selection method is invoked. Current selected
execution, target and each recipient remain first. A prospective cone carries
its actual geometry and configured threshold; a separately enabled ST option
survives local AoE failure. Already selected sword steps use current execution
geometry, and ranged Manafication swords/finishers are preserved. Unknown actions
and gauge-consuming Reprise do not borrow unchanged resources for preparation.

The same search labels retain the actual next GCD slot after selected-action
start/recast/end, and dominance preserves meaningfully different readiness.
Prospective action locks, target facing and authored prohibitions apply at that
slot. Native combo expiry, explicit range-wait deadline, enabling Swordplay or
Embolden expiry and a disclosed starter forecast window remain distinguishable.
A later capture/revalidation never renews an old deadline. Preparation is not a
second delivered-action bit or a promise of an unknown future rotation choice.

The preliminary walking pass now considers the full combo/contact objective
before stopping at its current action. Resource pruning cannot stop at a weaker
ST preparation while a legal movement action can preserve the full AoE result.
The existing in-world marker distinguishes melee preparation from combo recovery;
current diagnostics expose action identity, recipients, clock kinds, next slot
and preparation value. UI text and the source/settings/constraint inventories
record the replacement of fixed approach radii, reflection confirmation waits
and a separate optional phase-exit writer.

Source review also found a concrete adapter gap: this native definition table
has original swords only, while installed RSR can select 45960/45961/45962.
The selected-action reader previously rejected their missing definitions.
A narrow resolver prefers exact definitions, otherwise borrowing metadata only
from the three corresponding native sword definitions. Actual selected ID,
current game cast timing, RSR range/geometry/recipients and native safety remain
unchanged. This models the same family for which native ActionQueue.CanExecute
already adjusts original sword range under Manafication. Actual variant packet
animation locks and delivery remain live acceptance items; no arbitrary unknown
adjusted action receives borrowed metadata.

Managed plugin/harness builds, **41,769 native assertions**, **19 broader suites**,
installed RSR 7.5.7.6 contracts, nine repository tests and whitespace verification
passed against SHA-256 `7b842d17f6ddf7821bd582cddcfd9b1423099c85d4633c991d4c1a55534c62b8`,
MVID `44d92d73-dbc4-4432-b745-840944e32572`. New cases cover first-pass AoE,
ST fallback, independent recipients, forbidden hits, next GCD/expiry, procs,
facing/action prohibitions, bounded incumbent timing and resource pruning.
No DLL was deployed; actual RSR resampling, movement, packets and lifecycle
remain live-pending. No live DevBridge tools were discoverable in this session.
