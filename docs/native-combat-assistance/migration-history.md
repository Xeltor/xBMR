# XCAI native integration: continuation checklist

Updated 2026-10-24. **The cutover is incomplete.** The user resumed the migration
after switching models; the checklist is the active goal.
User steering, 2026-10-24: prioritize reintroducing all systems and relevant
offline implementation checks. Stop when only testing/tuning remains; the user
will run a dungeon for mechanism-specific acceptance and corrections. Do not
continue idle-client experiments or treat dungeon proof as a prerequisite to
finishing the implementation handoff.

This checklist supersedes older progress statements in the chronological
implementation notes. A compiled feature or imported switch is not proof of
behavioral parity.

**Scope change, 2026-10-23:** the user retired **Follow the tank between pulls**.
Remove `TrackDungeonParty` and its automatic catch-up, inferred door/crossing/lift,
stair/drop, pacing and travel waiting behavior. These are no longer parity or live
acceptance requirements. Preserve combat `MaintainPullContact` and explicitly
selected native BMR follow. Do not migrate retired tracking into another control.

**Scope change, 2026-10-24:** the user excluded learning and projecting
**unknown knockbacks**. Retain native authored-knockback handling, immunity,
landing/recovery planning and tank Arm's Length mitigation. Retire
`UseLearnedKnockbackFallback`, unknown-source inference and their migrated
learning/import machinery. Preserve original standalone files for rollback.
Known authored mechanics must not depend on the unknown-knockback classifier or
require three observed casts before they can be handled. Native action-effect
and physical-completion observation still serve known-mechanic execution.
The earlier learned-fallback checkpoint below records work already done; it is
superseded scope, not a remaining parity or live-test requirement.

**Priority change, 2026-10-24:** stop spending implementation time expanding or
polishing knockback support. Keep the last verified authored/native-recovery
checkpoint and the completed unknown-learning retirement. The unfinished shared
wall-projection refactor was undone before building or deploying it. Leave the
remaining knockback enhancements explicitly deferred and proceed with the other
migration systems; do not restart that audit during general parity work.

## Start here

- Working branch: `codex/native-combat-assistance`.
- Implementation is currently **uncommitted** on xBMR base
  `f1de3550b1d96bfd8f153d7b4a7ffe53e5e0d530`. Preserve the working tree; the large
  integration deletions and new files are intentional. Do not reset it to HEAD.
- Standalone source baseline: XelsCombatAI
  `4a0f7968da8a743e8971973821cb9f6f1387198c`; that repository was left unchanged.
- Read `AGENTS.md`, this file, then
  `docs/native-combat-assistance/{settings-inventory,source-inventory,migration-map,constraint-review}.json`
  and `upstream-interfaces.md`. Inspect the actual code before relying on an old
  status paragraph.
- Keep one native movement owner, native safety/assignments/manual authority,
  current-action target and individual recipients, and existing activation rules.
  Installation must not enable AI. Do not restore endpoint refinements, arrival
  corrections, IPC-shaped internal services or a second hazard implementation.
- The proposed 4 ms / 8,192-expansion quota was our design choice, not an inherited
  BMR requirement, and has been removed. Re-evaluate transport/reflection limits
  individually. The 250 ms native freshness ceiling, actual departure deadlines,
  twelve-second evidence horizon, sequence limits and explicit memory bounds
  still have distinct meanings. Never claim exhaustive completion after a bound.
- ToF cooperative FATE and collection-delivery integration is excluded. Do not
  add companion changes, a native BMR rotation prediction adapter, or a standalone
  shim. Publishing, public feeds and uninstalling XCAI are separate rollout work.

## Resumed implementation progress

The latest **isolated source checkpoint** removes the excluded unknown-knockback
subsystem. Its SHA-256 is
`94a0736934195d57292046962d33197588cf83d89e04e8fc619e8eb2f2fe037a`,
MVID `98148f83-e4c8-4cd8-90ad-2be8b17e9e81`. Managed compilation passed with
six existing warnings/zero errors. **42,862 native assertions**, installed RSR
contracts, all **19 exact-artifact broader suites**, nine repository tests,
inventory parsing and whitespace checks pass. The harness build has zero warnings/errors. This artifact is
isolated; the watched/loaded checkpoint below is unchanged.

Removed the native unknown forecast admission, geometry/applicability/immunity
learning, persisted knowledge store/import, preference/UI and unknown-only tests.
Retained native observation accepts only current BMR publications and matched
native effects; it has bounded session timing records and an expiring physical
completion receipt, with no saved knowledge file or geometry classifier. Settings
import and repeated startup discard the retired setting; original standalone and
previously imported knowledge files are untouched and no longer used. Existing
world-marker primitives now describe only authored knockbacks.

Known authored handling is still incomplete: first-event timing without a prior
native effect observation, general wall/directional/layer/custom projection,
already-in-progress casts and walking/mobility contingencies remain code work.
Session timing does not require the former unknown classifier or three casts.
Unsupported evidence retains native recovery; source removal and a timeout never
count as physical completion. The earlier learned-fallback artifact is historical
and no longer defines the intended migration.

This checkpoint retains tank Arm's Length mitigation, original RSR role action
approval without a duplicate RSR prediction requirement, independent immunity/
landing opt-outs, and circular/cone/rectangle target choices in the joint planner.
Original action priority, saved opt-outs, current recipients and exact facing remain
binding. No second movement writer or direct immunity submission exists.

Remaining implementation includes general native projection and walking/mobility
failure contingencies, current in-progress casts, and the remaining source/settings
parity audit. Do not close those items from this checkpoint. Follow the user's
implementation-first steering; do not add idle-client gameplay checks before the
implementation handoff.

The current **loaded checkpoint** has SHA-256
`c7e453e16e636f4b6924f623358ee3f03bb17465cea1f03efe7178fba3a68a39`,
MVID `8030a1f4-cf84-4432-9062-bee55f4c2a1d`. Managed compilation passed with six
existing warnings and zero errors; its native harness passed **42,864 assertions**,
all **19 broader suites** matched the exact artifact, installed RSR contracts,
nine repository checks and whitespace verification passed. A fresh one-client,
normal/out-of-combat preflight with RSR Off, no target/preset and standalone XCAI
unloaded preceded the normal watched write. Loaded identity, Settings Ready and
no failure were read back. A final 05:52 UTC snapshot confirmed unchanged idle
BLM position/normal conditions and XCAI unloaded. Native/legacy knowledge files
remain byte-for-byte unchanged: three records, zero motion/timing models. Idle
reload is not actual knockback or improved-route acceptance.

The native runtime now binds supported authored radial geometry to corroborated
local motion windows and cast generations. The framework validates exact current
cast/source, original finish, origin/distance and actual landing footprint before
promoting the context; workers receive only frozen maps/scalars. A scoped movement
transaction keeps the same launch, expected landing and absolute event window
across source disappearance and active motion. Fresh active captures enter the
same search/executor as a compulsory prefix; they do not apply the full push again
from an intermediate pose. Only native effect plus stable physical completion
releases it. Expiry is a failed forecast, not completion. Manual/owner/module/
player/territory changes release ownership. Pending/unsupported input and changed
constraints retain native recovery. No separate movement writer or IPC was added.

First-departure deadlines now retain the prefix clock across fixed events; the
old whole-route late-departure shifter cannot postpone a push. Cast windows are
clamped after normal/follow processing against the original event window, including
cast-finish/effect-result gaps. Native immunity covering the whole onset window
can release preparation before the push, never cancel active physical motion.
Planned casts delivered strictly before a push now retain their remaining
animation lock independently of physical movement. Post-landing walking may
proceed; subsequent actions wait for the lock. Fresh validation preserves the
original action/recipient promise, and the executor requires observed delivery.
Actual already-in-progress casts and live push delivery still need acceptance.
General walls,
layers, custom/native composite support, immunity stages, tank Arm's Length
mitigation and learned-geometry fallback admission remain unfinished. Native
authored timing evidence is separate from enabling learned geometry fallback.

The preceding phase-aware joint search/reference, source/completion validation,
source-generation diagnostic proof, overlay launch/landing markers, stationary
target-only path and independent compulsory-march authority fix are included.
Their remaining live/lifecycle acceptance is unchanged. The separate DevBridge
extension agent is working independently.

Native displacement input now includes an unfiltered source inventory and frozen
phase rasters. Presence is retained despite immunity, horizon, zero-distance or
native-None filtering; pose-dependent/failed publishers and uninspected/opaque
constraints cannot prove absence. Preparation markers without a modeled imminent
source retain native recovery. Exactly attributed pure preparation leaves can be
separated from independent union damage/assignment leaves. Their landing envelopes
remain transition requirements, not invented permanent assignments. Successive
sources retain native ordering and application limits; ambiguous independent
simultaneous owners remain unsupported. Workers receive frozen native maps and
scalar records, never the capture's native producer handles. Synthetic full-map
capture (1,600 cells) measured 0.137ms median / 0.365ms maximum; this is not live
frame-time proof. The latest offline search now consumes phases only with explicit motion windows;
native timing binding, scoped transaction and fresh compulsory-prefix refresh now exist for supported corroborated radial sources; general projection and live acceptance remain unfinished.
This is not completed joint knockback policy.

Local knockback observation/import now runs natively. World cast/effect events
replace the standalone hook, snapshots and raw native cast reads. Bounded local
motion, origin/timing, area and immunity evidence policies are retained; retired
community paths stay removed. Lifecycle IO imports once, preserves the original,
prioritizes existing native data and awaits final persistence outside the framework
thread. Live import retained all three legacy records exactly, with zero motion
samples, timing samples or geometry models. Native/legacy files remained byte-for-
byte unchanged through the second reload. A record count is not learned forecast
or movement acceptance. Joint landing/immunity/recovery and learned-fallback
admission remain unfinished; see the updated knockback guide before proceeding.

The previous checkpoint separated circular-AoE target improvements from movement-coverage
preferences. With ManageAoePackPositioning off, a proposed replacement must already
be executable from the captured pose, preserve every required recipient, and
strictly increase coverage. It cannot introduce a walk for extra hits or delay an
already available action. Non-circle, ground and healing movement objectives do
not inherit this permission. Disabling PickBetterAoeTarget releases pending setup
and conditional restoration even while other target features remain enabled.
Original RSR confirmation, native priorities, manual input and the single executor
remain binding. This is source/offline and idle-reload proof; improved AoE delivery
and linked Henched operation with ManageMovement entirely disabled remain open.

This stage repairs a source-level migration defect: ordinary combat forecasting
could credit a cast through an authored knockback without modeling displacement.
Known native knockbacks/pending effects now hand the frame to native recovery,
clear stale commitment and yield elective placement/mobility selection ownership.
Shared native direction/pull arithmetic is factored without changing wall/source
semantics. This is an INTERMEDIATE gate, not completed knockback policy. All three
knockback settings remain open. Read `knockback-migration.md` for the exact next
implementation boundaries; do not substitute this fallback for the joint planner.

The prior Ley Lines checkpoint `b2855303` / `87e036f0` remains the last actual dummy
selection/delivery check. It observed native Ley Lines, its field and buff without
unnecessary movement; temporary preset deletion, RSR Off/no target and normal
conditions were verified. The current stage has no live knockback/immunity proof.
Native circular-AoE targeting and placement acceptance remain separately recorded
in `targeting-evidence.md` and `ley-lines-placement.md`.

The live work found and corrected two migrated guard mistakes: Henched deliberately
sets `IsManual`, and automatic burst contributes to `CommandStatus`. The former
must not be mistaken for explicit Manual mode; the latter must not be mistaken
for an explicit command. Mode flags remain unchanged, and actual commands still
retain control. The command distinction also applies to the migrated tank and
support gates that used the same aggregate check.

**Next targeting work:** validate actual target setup and cross-frame native
commitment in linked Henched mode, then finish independent option interactions,
reacquisition, tank recovery and directional delivery. The circular-AoE
producer now supports PickBetterAoeTarget with movement coverage disabled. Linked
Henched operation with ManageMovement entirely disabled remains a separate gap. Native and
RSR marked-target priorities are independently binding. Do not clear IsManual,
replace concrete actions, add an arrival correction or infer complete feature
parity from a one-target dummy check. See `targeting-evidence.md`.

The earlier Pictomancer **loaded checkpoint** had SHA-256
`a009cd95581a19d3ccfaef679308c450941a0e996d08620d7b08d90200653d10`,
MVID `1ef7b26d-cb13-430f-83b1-ff8b609d1b8c`. It passed **42,286 native assertions**,
**19 broader suites**, installed RSR 7.5.7.6 contracts (including immediate/GCD selection and
Red Mage combo properties), nine repository tests and whitespace verification.
Normal watcher deployment and loaded MVID were verified at 22:28 UTC. The main
client was out of combat, XCAI unloaded, native AI inactive and RSR Off. DevBridge
is available again. Fresh state remains required before every later mutation.

Live BLM checks confirmed native cast/recast changes inside/outside Ley Lines,
owned field/buff arrival, a 0.6-second Retrace lock and preservation of remaining
duration. Retrace replaces the ground object ID. The observer now carries the
original effect identity/deadline across that replacement only with an observed
owned Retrace; exact route validation accepts that corroborated consumed prefix.
Unexplained/foreign replacements, expiry, missing geometry and a new placement
remain separate. Tests cover those distinctions. A stopped-navigation check at
21:11 UTC also corroborated Between the Lines by actual landing, buff/timing
readback and a 0.8-second action effect. These are bounded mechanics checks with
AI/RSR off, not integrated executor or combat acceptance. The user has explicitly
left the BLM client idle for continued testing and another agent is extending
DevBridge; refresh discovery after its report instead of changing that companion.

DevBridge's extension has now been deployed by its own agent. New typed saved-set,
action timing/status and position-guarded action tools work; old callable schemas
may be cached, so use fresh MCP discovery/the official MCP SDK rather than legacy
HTTP routes. Controlled PCT tests switched saved set 14 (BLM) to 17 (PCT), then
restored 14. All 14 equipped slots match the pre-test item data, including materia,
dyes and durability (observation tokens differ). Dummy reset/target clearing and
normal conditions were read back; AI and RSR stayed Off. The client is back near
the dummy position the user selected, with no active test navigation.

Native Starry Muse and Inspiration observations now match the running game:
Fire in Red cast/recast changed from 1486/2477 ms to 1115/1858 ms. Inspiration
was observed at 7.845 yalms from placement and absent by 8.374; these bound the
sampled behavior without proving an exact radius or complete footprint. The
30-yalm party-effect row cannot supply that footprint. Later navigation requests
did not reach their nominal points, so only actual positions were used and the
unverified Smudge setup was skipped. Exact field/height/expiry and native Smudge
delivery remain open. The current bounded timing diagnostic includes those PCT
actions and Inspiration; no history or new command endpoint was added.

Completed source work includes proved pruning, charge-return wait alternatives,
observed teleporter progress, reuse of verified floor height instead of an extra
unchecked native query, and selected party-action/GCD sequencing. An intermediate
action stop uses the existing overlay marker. Red Mage now includes ST/AoE continuation, supported Reborn starter preparation
and Beiruta explicit holds. Current selected execution remains first; next GCD
readiness, geometry, required recipients and original clocks enter native search
and revalidation. The preliminary walking pass no longer stops before meaningful
combo preparation or combat contact has been considered.
The hypothetical displacement and landing services now use native position, angle, actor identity and DateTime values; their immutable projection results preserve the existing native geometry oracle. Live job-specific movement remains open. Source/setting inventories record
the replacement of delayed-heal selection machinery; feature choices remain
preserved. The old checkpoint below is retained for provenance.

## Earlier checkpoint (historical)

This earlier code built successfully in the managed compilation environment,
with six existing upstream obsolete-gauge warnings and no errors.

- Plugin SHA-256:
  `5f4aa0145fa837acb1bfa4f08e77e224b163154c3fd4827b77c19f47d468da4d`.
- Plugin MVID: `22da086a-d2ec-4827-ae60-d9d1bdff6b29`.
- Native harness SHA-256:
  `43a2cd7d2821f47e9f14fbc94210a5f0eda53902f76e4c832e5b5f572099f411`.
- **41,539 native assertions**, **19 encounter/Questionable regression suites**,
  **9 Python repository tests**, installed RSR 7.5.7.6 contract checks and
  `git diff --check` passed against this checkpoint.
- Synthetic 6,400-cell search: median 42.554 ms, maximum 163.446 ms, all ten runs
  completed; native capture median 0.523 ms, maximum 2.938 ms. Forced-march capture
  averaged 0.154 ms for 72 headings. These are synthetic workloads, not evidence
  of live frame time or mobility-rich search performance.
- This checkpoint **has not been deployed**. Do not confuse the isolated artifact
  with the watched DLL or loaded assembly. Compilation tooling and receipt paths
  are private environment configuration; use the managed build instructions.

Historically, the main client was checked at 04:19 UTC on 2026-10-23: normal conditions,
AI inactive, follow out of combat disabled, no selected target, XCAI unloaded,
and RSR still in its then-current `Henched` mode. It then retained the ninth test
build, SHA-256 `b5e381b8947a0744f554b73dfed2adfd0595f7c5f7de107fc56511354fb8660b`,
MVID `a54ce466-900b-4587-b820-e6015394fd45`. Dummy tests moved the character;
the original physical position was not restored. Fresh live state is required
before any later experiment or watched-DLL deployment.

## Existing foundation: extend rather than rebuild

- Normal movement and following submit to one native context/search/executor.
  Shared traversal, native recovery, frozen goals, exact explicit destinations,
  original deadline aging, cancellation and manual gates already exist.
- The full-map search retains meaningful timing/resource/action labels and uses
  individual required recipients. Mobility, Sprint, True North, authored
  teleporters and forced-march stages have deterministic coverage.
- All 72 provenance-identified XCAI-only versioned contracts were removed; the
  recorded 67 upstream IPC names remain. Native services replace transport.
- Material BMRAI, versioned preference import, local override ownership, support,
  camera, diagnostics, replay decoding and opt-in fight sharing have native code.
- Live import preserved 99 preference fields. Eleven recovery journals were
  reconciled. The original XCAI config remains byte-identical to the retained
  pre-test copy. Later differences are patch-note acknowledgments, omitted type
  metadata, and reconciled journals; feature choices remain preserved.
- Dummy checks demonstrated movement and RSR damage delivery. They did **not**
  establish full one-shot movement, physical route, encounter or job parity.

## Native targeting services checkpoint (historical foundations)

A later isolated candidate built with SHA-256
`db5b9317e327df3f347172c689b29df1a55e47a2a5a0ee26a2dc993ac624d87f`,
MVID `33cd6e77-be35-44c9-a7ea-d6e91f2b89af`, and passed **42,400 native
assertions** plus installed RSR contract checks, including the real optional
`IpcStateOverrides` record. It adds immutable native target/recipient constraints
and a synchronous fallback-restoration scope. At that checkpoint the services were **not wired to
live targeting**, were **not deployed**, and did not complete the three target
preferences. The loaded Pictomancer checkpoint above remains authoritative for
live behavior. See `targeting-evidence.md` before continuing the runtime wiring;
do not repeat the source discovery or mistake the new tests for delivery proof.

The later source now connects these policies through one shared RSR callback
owner and the native target intent consumed before normal/follow movement.
It preserves the public target field, distinguishes authored versus automatic
writes, freezes action recipients and reuses confirmed tank recovery. Current
diagnostics and the existing target marker are wired. The isolated candidate has SHA-256
`479ac54c039a93f38f81d88c4fcb59583b265c5693a15c01cb3acd61d49f0006`,
MVID `9a5a9863-83dc-423a-ac64-8874e89c6aac`; its managed build passed with six
existing warnings, **42,429 native assertions**, **19 broader suites**, nine
repository tests and installed RSR 7.5.7.6 contracts. It has not been deployed. Selection age advances only after observed
candidate checks in the original RSR callback; skipped cycles and rereading a
cached result cannot renew it. Action, target and complete recipients must still
match before publication. The three settings remain open until lifecycle,
selection/delivery and representative cost are validated. See the latest
section of `targeting-evidence.md` for the precise remaining acceptance.

## Native target-choice search and runtime checkpoint

The initial isolated search artifact `a09a931b` passed 42,502 assertions,
including a 54-case independent target/cell/setup/warning oracle. It is superseded
by the loaded runtime checkpoint above; its provenance remains in
`targeting-evidence.md`.

The existing search now carries a hard-target choice and setup progress in its
labels. The same cell with a different target is not pruned as an equivalent
state. Each option preserves original action geometry, range, required recipients
and forbidden recipients. Target setup is a timed route stage, uses native exit
deadlines and the strict native 50-yalm acquisition envelope, and may occur after
necessary walking. The final useful destination is selected jointly; no endpoint
refinement or arrival-correction controller is added. Execution waits for matching
actual-target and native selected-action evidence. An unconfirmed setup cannot
renew its captured window. Estimated setup remains `TargetSetupEvidence`, rather
than being advertised as completed proof. The existing overlay marker explains
that stage.

**Next implementation actions (do these before further live AoE claims):**

- [x] Capture `PlanningAction.TargetChoices` from the actually selected current
  RSR action on the framework thread. Reuse native `TargetInfo` eligibility checks
  (`CheckStatus`, resistance, time-to-kill, restricted-DoT, visibility/CanUseTo,
  the original target predicate and configured HP limits) without calling
  mutating `CanUse` as a forecast. Keep forced targets, presets, manual opt-outs,
  current recipients and native priorities binding. Capture only supported
  projection/height data for `VerticalOffset`; exact native eligibility still
  needs revalidation at the actual setup pose.
- [x] Wire `MovementExecutionKind.Target` through the single native host/executor
  and `NativeTargetSetupOwner`. It publishes native hints, recognizes expected
  changes, preserves foreign/user targets, and requires fresh native action and
  recipient confirmation. Local clocks cannot renew. Capture and publication
  recheck RSR marked/icon priority, native priority, collateral and original
  predicates. Source and owner/oracle tests pass; an actual live setup is unverified.
- [x] Capture setup estimates from observed original RSR callback intervals and
  native frame duration. Synthetic test delays are not production defaults.
  Expire the original plan clock and isolate failed target candidates; do not
  resurrect the removed IPC quotas.
- [ ] Calibrate actual setup latency in a representative supported AoE opportunity.
  Verify fallback/conditional restoration, cross-frame policy reset, action locks,
  exact stopped pose and selection confirmation with actual delivery.
- [x] Separate supported target-circle selection from movement-coverage opt-outs
  while the native movement planner is active. All four combinations, immediately
  ready versus future actions, equivalent anchors, walk-dependent gains and
  collateral have native regression coverage. Pending setup/restoration yields
  when the target preference is disabled. Actual improved pack delivery remains
  unverified; this does not close the full targeting feature.
- [x] Implement stationary target-circle planning with ManageMovement entirely
  disabled. The same host/worker/executor supports normal/follow owners and a
  stationary fallback for active presets without a movement module. Native
  recovery wins, old walking commitments invalidate and target-only setup cannot
  add movement, mobility, utility or field placement. Frozen-permission copies,
  option combinations and native goal/warning recovery have offline coverage.
- [ ] Validate linked Henched target-only delivery with ManageMovement disabled,
  and finish directional actions, reacquisition and tank recovery. Verify
  current-target value and primary/secondary damage effects rather than equating
  recipient count with complete rotational parity.
- [ ] Recheck cross-frame commitment after confirmation, actor/target/recipient
  changes, failure, explicit Manual/presets, rotation replacement and unload.
  Then run bounded live target/route checks. Do not clear Henched `IsManual` for
  an entire callback or add a second movement writer as a workaround.

## 1. Finish planner correctness and practical search performance

- [ ] Re-test the newest walking-pass selection. `JointMovementSearch` now checks
  equally timely alternatives before the preliminary walking pass publishes its
  first action-bearing plan. Previously the first rear-position cell could be
  inside a target hitbox and continuity could retain it. The deterministic
  regression passes; the change is in the loaded build, with comparable live acceptance pending.
- [ ] Verify the coherent RSR snapshot in live movement. Positional evidence is
  captured with the selected action once per native frame. The earlier dummy
  trace's rear requirement was legitimate: 25772 is Chaotic Spring and 3556 is
  Wheeling Thrust. An earlier action-name interpretation was wrong. Do not cite
  stale positional evidence as a demonstrated cause of that live symptom.
- [ ] Address mobility enumeration cost in `MobilityMechanics.From`,
  `MovementPlanningContext.MobilityFrom` and `JointMovementSearch`. Enumerating
  every landing from every label can consume freshness before useful walking
  continuations are expanded. Benchmark a mobility-rich map, action delivery,
  temporary stops and party/zone destinations, not just a plain 6,400-cell map.
- [x] Add proved execution/resource pruning before mobility enumeration. One
  complete incumbent must attain the proved execution, native-goal and required
  region ceilings before a nonrecoverable resource spend is excluded. Conditional
  casts and later actions disable that narrow resource proof. Objective upper
  bounds also reject prefixes that cannot match the incumbent's earlier action.
  Unpruned small-map comparisons and a 6,400-cell mobility workload pass. Latest
  synthetic mobility run completed in 28.852 ms; this is not live frame time.
- [ ] Check after-action useful destinations are considered promptly enough to
  avoid a subsequent correction walk. Cheap-pass termination now considers full
  combo preparation and combat-contact objectives; their tests pass. Zone return
  and other subsequent-execution cases still need review and comparable live checks.
- [ ] Re-audit cross-frame commitment and exact revalidation when the action,
  target, recipients, contact region, hazard, speed, locks or manual input changes.
  Preserve the original capture/departure times. Cosmetic changes alone must not
  restart movement after arrival.
- [x] Preserve observed automatic teleporter entrance progress across replans.
  Native not-before gates, leaving the trigger and authority loss have separate
  checks. A fresh plan no longer restarts a use interval longer than freshness.
  Repeated-capture synthetic tests pass; actual transport delivery is still a
  live validation item.
- [ ] Review full route/resource/action validation, same-cell dominance and
  bounded-status diagnostics against the required acceptance matrix. Forced-march
  heading sampling remains explicitly bounded; simultaneous independent producers
  need authored composition rather than silently selecting one.

## 2. Validate physical routes and finish displacement policy

- [ ] Live-test `NativeRoutePhysicalObservation`. It now checks the remaining
  ordinary walking corridor with body-width native probes and feeds confirmed
  blocks into the shared native raster. Native recovery and enhanced search use
  that same map. Synthetic walls, unknown floors, stairs and transport-chord
  regressions pass; the change is in the loaded build, with physical/live acceptance pending.
- [ ] Establish whether the observed dummy stop short was physical collision.
  It is currently a hypothesis. Inspect current collision evidence before naming
  a cause. Verify a blocked direct route produces a detour rather than a hold loop.
- [ ] Measure framework cost of complete corridor probes, refresh/removal of
  remembered blocks, near-current-cell wall observations, streaming changes,
  stairs, unknown floor and height changes after transports. Unknown collision
  data must not be represented as proof of a wall or floor.
- [ ] Finish `ManageKnockback` and `PlanKnockbackLandings`. Preserve authored projection authority, active
  hazards and opt-outs. Native projection services now use typed positions, angles and original times rather than transport arrays/tick strings. They are present, but these saved
  policies do not yet have complete native behavior. The current native-recovery
  gate prevents false ordinary cast/route forecasts during a known push. It is
  not the final implementation: finish joint forced-motion/landing/recovery,
  native immunity action stages. Native local
  observation/import is implemented and tested below; it does not replace those
  policies. Tank Arm's Length damage mitigation is now restored through original RSR selection; actual dungeon delivery remains a test item.
  Follow `knockback-migration.md` rather than rediscovering transport or timing
  assumptions. A pending-effect timeout is not a motion-duration proof.
- [x] Apply the user-authorized unknown-knockback retirement. Removed the
  estimate preference/UI, unknown forecast admission, geometry/applicability/
  immunity learning, knowledge persistence/import and unknown-only tests.
  Authored effect/completion observation is now bounded to the current module
  session and published source/cast identity; no unknown geometry is inferred.
  Settings import/repeated startup discard the retired field, preserving original
  standalone files. Managed build and native/installed-RSR checks pass. First-event
  authored timing and general known planning remain tracked separately below.
- [x] Replace the former general learning observer with native authored-source
  observation. Matched native effects, duplicate suppression, actual stable
  completion, freshness, manual handoff and module/unload cleanup pass offline.
  Session records have no file or legacy import; previous import/restart checks
  are historical rollback evidence, not retained learning requirements.
- [x] Capture registered native source presence independently of the ordinary
  interruption filter, including preparation-constraint presence. Keep absent,
  present and incomplete states distinct. Native phase capture separates only
  exact pure union leaves, retains independent constraints and landing envelopes,
  and freezes the same native raster for workers. Scope, unsupported shapes and
  tests are recorded in the knockback guide; this does not implement transitions.
- [x] Add phase-aware native search for explicitly timed supported radial pushes.
  An independent small-map solver, successive pushes, conservative uncertain-onset
  traversal, no-escape temporary landings, current/subsequent actions and charge
  preservation pass offline. Workers receive only frozen maps and scalar windows.
  Runtime promotion requires matching corroborated timing/cast source and the
  scoped event transaction; unsupported sources retain native recovery.
- [x] Add native preparation validation and physical-completion evidence. Caster,
  action/data and cast-generation identity, original motion window, exact launch/
  corridor/landing and complete source sequence are revalidated. The shared
  executor waits through a push. Native effect plus stable full motion creates one
  expiring completion receipt; neither source removal nor a timeout completes it.
  Source/generation/clock/pending/landing regressions and overlay diagnostics pass.
  Supported runtime binding and in-flight refresh are implemented below; actual
  physical gameplay acceptance remains open.
- [x] Bind supported native timing and maintain scoped source/effect continuity.
  The scoped native observer binds only published authored source/cast identity
  and known native-effect timing; a fresh compulsory prefix keeps original launch/landing and
  event clocks across source removal/active motion. Physical completion releases;
  expiry fails. First-event timing remains open. Native body footprint, first-departure/cast windows, exact source
  ownership, changed/manual scopes and fresh-prefix tests pass offline. Idle
  watcher reload is verified; comparable actual knockback delivery remains open.
- [x] Add explicit native immunity alternatives to phase search and exact validation.
  Each source retains its own authored/onset window and immunity-ignoring rule;
  charges, action lock, application and six-second lifetime remain explicit.
  Equivalent useful pushes conserve the resource. Independent hazards remain,
  and consuming a request cannot fabricate native buff confirmation. The native
  runtime capture/RSR approval/selection owner is now implemented; this item
  does not close ManageKnockback, tank mitigation or actual action delivery.
- [x] Connect supported role immunity proposals to native resource capture,
  original RSR CanUse approval and the existing callback scope.
  Shared execution waits for the original rotation action and actual buff status;
  manual input, explicit commands, saved settings, stale input and foreign changes
  preserve authority. A native physical failure branch after the ability lock is
  required. Offline build/regressions pass; walking/mobility contingencies and general native
  projection remain code work. Separate immunity/landing opt-outs are now implemented.
- [x] Restore tank Arm's Length slow mitigation through the original RSR ability
  pipeline. Native role/dungeon classification, independent attacker identities,
  actual cooldown, complete displacement inventory, native weave/unlock/status
  checks and original ability priority are covered offline. Actual tank dungeon
  delivery remains a live test; this is a self action with no spatial overlay marker.
- [ ] Finish the stateful preparation/forced-motion/landing/recovery search and
  executor using those native inputs. Bind cast/effect identities and resolution
  so source removal at cast finish cannot masquerade as completed displacement.
  Planned casts now retain delivery before a push while their animation lock
  overlaps it; subsequent walking is allowed while other actions remain locked.
  Search/reference, recipient, exact validation and delivery-observation checks
  cover this offline. Actual in-progress casts and live push delivery remain open;
  unknown completion remains bounded. Extend wall/layer/custom/native composite
  support and exact revalidation instead of treating the initial pure-envelope
  support as the final requirement. Also cover native no-op/applicability states:
  avoiding a source or reaching a pull limit must not consume the event early
  and then permit walking into it before activation.
  Before broadening phase promotion, preserve custom native DestinationUnsafe
  rules: a pose-independent source does not imply that its destination evaluator
  is frozen. Current Freeze must gate or capture that rule, with a regression for
  a SimpleKnockbacks subclass overriding only DestinationUnsafe. Also distinguish
  planned non-movement abilities from mobility when invalidating observation:
  the current callback treats every Action directive as contamination, including
  a cast/ability whose animation lock may legally overlap the push.
- [x] Historical implementation, now excluded by the 2026-10-24 scope change:
  admit learned forecasts only after a COMPLETE authored-displacement
  inventory proves no conflicting native source, preparation or march. Native
  observation/cast/source generations, original reported/NPC/onset/landing clocks,
  applicability and independent immunity trust feed the joint native map search.
  The original inventory is never overwritten with an inferred source. Exact
  route/landing checks, known-versus-unknown immunity, overlapping casts, failed
  forecast retention, native physical completion and existing world-marker labels
  pass offline. Unsupported layers or uncertain outcomes retain native recovery.
  Learned movement/immunity delivery is no longer an acceptance requirement.
  Retire this implementation as tracked above; general authored planning remains open.
- [ ] Complete rotational/GCD mobility setup where the evidence supports it.
  Verify target anchors, deliberate-facing backsteps, return points, ground
  locations, action locks, failed setup and per-candidate failure isolation live.
- [ ] Exercise Sprint/True North, shared charges, gauge costs, cooldown changes
  and cast completion against actual delivery. Installed RSR contract compatibility
  and synthetic tests alone are insufficient.

## 3. Finish job, targeting and support parity

- [ ] Black Mage: validate native placement/scoped selection under actual encounter
  warnings, then finish after-action return and complete `ManageLeylines` /
  `GreedyLeyLines` / `ReturnToLeylines` behavior. `PlanLeyLinesPlacement` now
  prepares a typed stage in the joint search, with original-RSR approval required
  before movement commitment. A missing/stale preparation preserves native timing.
  Original GCD clocks, original-position continuity, saved action opt-outs, casts,
  occupied weaves and foreign ownership remain binding. Independent solver and
  pruned/unpruned checks pass. The 256-cell worker case completed in 19.738 ms,
  replacing an initial freshness-bounded 250 ms result through proved pruning,
  without a new quota. Normal native Ley Lines delivery/fallback is verified on
  the loaded DLL; actual improved encounter placement remains pending. `UseBetweenTheLines` and `UseRetrace` now enter
  the shared planner for supported selected-spell execution and recovery. Existing zone,
  status and original expiry now feed shared native ownership. Matched inside/outside
  native cast/recast samples for the same spell and timing conditions support a
  current-action return in the joint search. Walking/abilities-only, original
  effect/modifier deadlines, charge preservation, safe landings, lost timing and
  current-cycle protection have deterministic checks. Improved encounter placement,
  after-action return parity and integrated cast/return/relocation delivery remain open.
  Retrace is a typed utility stage: the field moves, its original expiry survives,
  and later Between the Lines uses the new anchor. The selected spell's native
  recast is an independent recovery objective, including for instant spells.
  Tests compare delivery/recovery against a 144-case finite-state oracle. Unknown
  outside timing reports `ActionTimingEvidence`; it cannot borrow a hasted sample.
  An unsupported selected RSR ability keeps its weave. Live native recast,
  buff/object arrival, replacement identity, unchanged duration and 0.6-second
  Retrace / 0.8-second Between the Lines effects are corroborated. Integrated
  action delivery, sampled condition changes and combat behavior remain open.
- [ ] Red Mage: validate the implemented ST/AoE continuation, supported Reborn
  starter gates and Beiruta explicit hold live. Native geometry, individual AoE
  recipients, configured thresholds, single-target fallback, next GCD readiness,
  locks/facing, enabling-effect expiry and original combo/hold clocks now have
  native regressions and installed RSR contracts. The three ranged Manafication
  sword IDs retain exact selected identity with narrowly aliased native metadata.
  Verify actual RSR resampling, supported fallback actions, selected
  sword steps, pooling, opt-outs, Corps-a-corps/Displacement and actor/rotation
  changes. Unknown intent falls back to current native goals; no mana-only starter.
- [ ] Pictomancer: corroborate the Inspiration footprint, complete action-aware
  return beyond the observed center and validate `UsePictomancerStarryMuseSmudge` live. The switch now governs credited field benefit throughout the joint route, while preserving independent legal Smudge recovery and walking return. The 30-yalm party-buff radius
  does not prove the Inspiration footprint. The current center/observed-covered
  pose fallback retains geometry/timing bounds (`FriendlyEffectGeometry` or
  `ActionTimingEvidence`); it is intentionally incomplete.
  Matching-version live samples now bracket the covered-player distance between
  at least 7.845 and less than 8.374 yalms, with corresponding native cast/recast
  changes. Do not equate this sampled bracket with an exact radius, ignore player
  hitbox/vertical effects, or treat an intended endpoint as an actual landing.
  Smudge was deliberately not submitted after its setup failed corroboration.
  Current selected-action center return now uses matched native inside/outside
  cast/recast observations, including instant GCDs, with original landscape and
  modifier expiry. Unknown action/stack conditions, replaced/lost fields and
  expired samples revoke the forecast. Revalidation preserves the saved opt-out
  across Smudge plus later walking/waiting; 96 finite alternatives and native
  policy checks pass. No complete radius is inferred. A later live Starry Muse
  event was corroborated by its action effect and timing; an object-table diff
  found no new/owned ground object. This observation does not prove such objects
  can never exist. BLM/set 14 and all equipment were restored at the exact initial
  pose after the dummy reset; AI/RSR remained off.
- [x] Replace the obsolete healing-selection hold with direct native evidence
  for RSR's selected immediate party ability and GCD. Supported direct party heals and the GCD enter one joint plan,
  preserving the immediate heal and every current recipient. Native regression
  and installed selection-contract checks pass; actual healing delivery remains
  in the live support checklist. `DeferLeyLinesPlacement` is still unassigned and
  remains part of the Black Mage placement work above.
- [ ] Finish `PickBetterAoeTarget`, `KeepTrashTargetSelected` and
  `TankTargetLostTrashAggro` target selection. Native selected-action geometry,
  tank recovery, death/swap observations and ranged aggro submission exist; hard
  target intent is now connected to normal/follow movement, with shared callback
  ownership, original RSR checks, immutable recipient constraints and scoped
  restoration. Offline build/native/installed-contract checks pass. Validate first
  activation, rotation replacement inside the callback, manual clear/change,
  friendly-target options, actual selected-action delivery and representative
  capture cost. Verify the intended interaction with disabled movement and native
  explicit-target strategies. Keep one native targeting authority and preserve
  the current opportunity, manual/forced targets and RSR action opt-outs.
- [ ] Verify support/raise/cards/mitigation, Kardia/fairy coverage, tank stance,
  cast interrupt/stun, post-resurrection recovery and linked RSR ownership live.
  Their deterministic policies and installed contracts already have tests.
- [ ] Verify friendly ground effects, Passage cone, original expiry, cast-time
  procs and haste-field departure. Never credit unobserved future cast-speed gains.

## 4. Finish dungeon contact and external navigation parity

- [ ] Finish the **combat-only** `MaintainPullContact` audit: observed moving
  multi-enemy packs, role distances, hysteresis, target/action coverage, authority,
  native route reachability and stopping immediately at combat end. Native follow
  leaders and presets must retain precedence. No tank catch-up between pulls.
- [x] Retire automatic between-pull tank following by explicit user request.
  Removed `TrackDungeonParty`, its UI, automatic travel/interaction policies,
  observation, execution waits, private diagnostic/overlay hooks and dedicated
  fixtures. Import/restart tests discard enabled legacy/integrated values while
  retaining the original standalone file. Managed build, 41,695 native assertions,
  19 broader suites, installed RSR contracts and nine repository tests passed.
  No DLL was deployed. Native explicit follow remains unchanged.
- [ ] Preserve explicit follow leaders and presets, Questionable handoff and
  manual control through the existing native path. Combat contact must never
  activate AI, substitute a second writer or infer a future tank path.
- [ ] Audit surviving combat-contact distance/snapshot caps and external vnavmesh
  requirements. Between-pull tracking, inferred transitions, stairs, descents,
  transfer waiting and travel pacing are intentionally retired, not missing parity.

## 5. Complete migration, interface and source inventories

- [ ] Finish all 100 setting dispositions in `settings-inventory.json`; keep
  partial, live-pending, retired and excluded entries honest. The source inventory
  still has **254 of 385 files awaiting explicit disposition** at this checkpoint.
  Many are obsolete transport/helpers, but do not bulk-label them without mapping
  the behavior and its consumers.
- [ ] Preserve intentional retirements: no active consumer existed for the old
  default tank-facing and two tank-cleave controls at the pinned source. Their
  imported values remain readable; authored native requirements remain intact.
  ToF remains excluded; between-pull following is also explicitly retired by the
  user. Other retirements need a stated reason.
- [ ] Visually validate all Material pages, shared controls and scoped style.
  Bare `/bmrai`, `/bmrai ui` and `/xcai config` dispatch were checked live without
  standalone XCAI, but screenshots were black/unavailable; rendering is unverified.
DevBridge extension report received 2026-10-24: bounded, directly owned plugin
slash-command testing is available through `commands` -> `composedSnapshot` ->
`action.testPluginCommand`, using single-use 30-second tokens. This is the other
agent's deployment report, not independently repeated acceptance here. Refresh
MCP discovery and the skill before using it. Keep the user's implementation-first
instruction: no command/gameplay probes were run for this retirement checkpoint.

- [ ] Validate expanded `/xcai` compatibility live: bare toggle, on/off/toggle,
  config/notes, knockback and native status aliases. Camera subcommands were
  explicitly retired; camera controls remain in the interface. Shared settings and
  normal persistence are implemented; party commands only explain retirement.
  The fallback never registers over standalone's name or activates native AI.
  Deterministic dispatch/shared-settings/persistence/coexistence tests passed, along
  with the current managed build, 42,144 native assertions, 19 broader suites and installed
  RSR contracts. Actual chat/window delivery and load/unload transitions remain pending.
- [ ] Exercise standalone coexistence across load/unload/reload, command fallback,
  and diagnostic registration. Initial unloaded detection is verified; actual
  coexistence transitions are not. Do not uninstall plugins automatically.
- [ ] Recheck repeated startup/import precedence, restored journals, user changes
  during owned overrides, disabled corrections, job toggles, action opt-outs and
  shared native settings. Keep the original standalone config for rollback.
- [ ] Verify every changed decision's overlay accurately reflects the selected
  native plan, useful destination, contact region, stage and rejection reason.
  Preserve existing primitives and avoid a separate status HUD.
- [ ] User-facing README now covers integrated commands, activation, settings
  import, dependencies and combat contact, with retired following and incomplete
  features disclosed.
  Removed obsolete standalone-dependency and developer/build guidance. Finish the
  remaining behavior/settings documentation after policy parity. Keep architecture/build instructions in the appropriate
  developer documents, not README. Refresh `upstream-interfaces.md` for all final
  changes, and consolidate obsolete chronological status claims.
- [ ] Check camera/manual pause, diagnostics lifecycle, opt-in fight sharing,
  privacy/receipts and historical XCAI capture decoding. DevBridge owns history;
  xBMR exposes only bounded current diagnostics. No companion changes are in scope.

## 6. Finish validation and upstream compatibility

- [ ] Convert remaining useful deleted IPC regression coverage into native-service
  tests, especially displacement/physical/projection/lifecycle cases. Keep the
  removed-endpoint/retained-upstream assertions. Do not restore transport-only tests.
- [ ] Review the requested scenario matrix: melee dodge, blocked detour, narrow
  ranged recovery, AoE and heal recipients, authored tank facing, successive
  warnings, temporary landing with/without escape, known/unknown expiry,
  disconnected/unsupported geometry, no route, mobility setup/failure, knockback,
  casts, charge preservation and repeated-frame stability. Existing synthetic
  tests cover many; fill actual gaps instead of equating assertion count with parity.
- [ ] Exercise lifecycle acceptance: RSR unavailable, native BMR rotations,
  following, explicit presets, Questionable, death/resurrection, mounted pauses,
  logout/unload, stale workers and manual input. Maintain exact restoration and
  no independent activation.
- [ ] Replay representative retained fights, then run comparable live movement,
  action-delivery and lifecycle checks. Main-client dummy delivery is not dungeon,
  healer, mobility, forced-march or knockback acceptance. Clearly disclose live
  cases that cannot be exercised in the available session.
- [ ] Refresh the upstream trial on the final implementation. Recorded upstream
  base is `be8ff771cc545f8f8b5b548b5dd7271d80460f89`; newer revision already fetched
  is `ae64e3309905dafad0952dbe72680add167591f9`. The earlier isolated trial merged
  implementation snapshot `6dab025f39ff44a85835dfbe930ec8f28c2ce7b1` as
  `9a679675c3b84fcc39da31e92bc5987c7b1900a3`, built and passed 22,951 native assertions
  plus 19 broader suites. That trial predates substantial later work. Use only
  the task-owned isolated trial; do not disturb unrelated worktrees or adopt
  upstream on the main working branch merely to test compatibility.
- [ ] Build and run relevant checks against the exact final artifact through the
  managed environment. Refresh stale harnesses after signatures change. Run
  formatting verification when supported and report limitations; `diff --check`
  is not a substitute for a full formatter.
  Managed formatting now covers all assistance C# and modified upstream C# files
  explicitly. Whitespace, CRLF and import ordering were repaired without changing
  behavior. Full semantic style verification now runs with Windows targeting
  enabled, rather than silently loading incomplete references. It still fails on
  existing port style (mostly explicit `this.` and embedded statements), naming,
  simplifications and private members, including JSON compatibility properties.
  Review those members before removing anything: reflective serialization use is
  not proved dead by an unused-member diagnostic. No encounter-wide reformat.
- [ ] Before a later watched deployment, freshly confirm every affected client is
  out of combat. Verify artifact hash, watched hash, watcher reload, loaded MVID,
  plugin health and cleanup separately. Never use `/xlreload` or overwrite the
  installed plugin directory. The current checkpoint above is loaded; future
  artifacts still require their own fresh deployment and provenance verification.
- [ ] Final handoff must identify the built source/artifact, migration results,
  remaining live-test limits and refreshed upstream-merge evidence. Stop when
  the user-requested cutover is genuinely complete; release/feed/uninstall actions
  remain separate and require their own authorization.

## Suggested order on resumption

1. Read the checkpoint and inspect the current branch/client; do not redo the
   initial inventory, source import or IPC removal.
2. Resolve first-commit route quality, physical detours and mobility-rich search
   cost, with measured evidence and small reference-solver tests.
3. Complete the concrete missing job/target/support/dungeon policies above,
   updating their setting/source dispositions as each is handled.
4. Finish UI/migration/lifecycle acceptance and user documentation.
5. Refresh the upstream trial and exact-artifact validation, then perform the
   separately verified final development load and report live limits honestly.

## Separate verified upstream defects

`BossMod/Data/ClientState.cs`, `ClientState.Stats.Equals(object)`, checks `Combo`
instead of `Stats`. Boxed stat values therefore compare incorrectly; it should
compare the stat type. Native combat timing uses the correct typed `==` operator,
so this is not an identified cause of a movement fault. The existing upstream
file is unchanged. Broader consumer/combat impact has not been established; this
separate defect is not a newly added migration requirement.

`BossMod/Framework/IPCProvider.cs` registers `AI.SetPreset` and `AI.GetPreset`
twice in adjacent identical blocks. The pinned upstream base contains both
blocks too. Expected ownership is one provider registration per contract; the
loaded client is healthy and no combat impact has been demonstrated. This is
separate from the 72 retired integration contracts. It is recorded for later
upstream repair, without broadening the migration or changing public contracts.
