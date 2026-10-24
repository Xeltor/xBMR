# Native knockback migration

## Current scope: authored mechanics only

On 2026-10-24 the user explicitly excluded projecting and learning unknown
knockbacks from the migration. Keep known BMR source geometry, native immunity,
preparation/landing/recovery planning and tank Arm's Length mitigation. Retire
the optional unknown-source fallback, its setting/UI, learned-model store and
legacy knowledge import. Preserve original standalone files for rollback.

Known authored sources must not require the unknown classifier or three prior
observations to participate. Native action-effect and physical-completion
observation remain useful to validate execution of authored mechanics. Where
native timing or projection cannot be established, retain native recovery and
honest diagnostics; do not substitute an invented duration or hazard expiry.

The earlier checkpoints below describe historical work, including the learned
fallback. That fallback is no longer a parity or live-test requirement. Its
native preference/UI, forecast admission, classifier, knowledge persistence/import
and unknown-only regressions are removed. `NativeKnockbackObserver` retains only
bounded current-module observations associated with actual BMR publications;
matched native effects and stable motion produce an expiring completion receipt.
There is no unknown-source forecast or geometry learning. Session timing records
need no former classifier or three-cast admission. Original standalone and prior
imported files remain untouched and are no longer read or written.

The first occurrence of a known mechanic still needs proper timing support without
previous native effect observation. General projection, in-progress casts and
walking/mobility contingencies remain implementation work; unsupported evidence
retains native recovery. Do not describe the retirement as completed known-mechanic
planning. The current isolated artifact is `94a0736934195d57292046962d33197588cf83d89e04e8fc619e8eb2f2fe037a`,
MVID `98148f83-e4c8-4cd8-90ad-2be8b17e9e81`; build, 42,862 native assertions,
all 19 exact-artifact broader suites, installed RSR contracts and nine repository
tests pass. Migration inventories parse and whitespace checks pass.
No watched deployment or gameplay experiment was performed for this retirement.

## Initial safety checkpoint

Checkpoint SHA-256
`b960e1cb7299867175969eb5bb5d399a3e5a0883b40cd0f3089b35d7e48e6559`,
MVID `b8cbf531-4861-4892-9866-c07d3438dfe5`.

The source audit found a migration defect in `CombatAssistanceRuntime.Capture`
and the ordinary `JointMovementSearch` cast path: an authored knockback was not
part of the captured temporal model. A stationary preparation cell could be
native-safe while the ordinary action forecast still credited a cast extending
through the push. This is a source-level defect in the integration, not a claim
that an observed fight failure was caused by upstream encounter logic.

`NativeKnockbackAuthorityCapture` now captures an interrupting native publication
or pending effect before ordinary combat planning. Known native immunity is
checked at the actual authored activation. Unknown activation, past-but-still-
published sources, incomplete geometry and pending effects do not invent expiry.
A footprint excluding the current pose is insufficient to approve all future
route positions. Known events beyond the twelve-second horizon and native
None/zero-distance sources do not interrupt the supported current forecast.

The host immediately clears an older committed plan, disallows ordinary combat
optimization and uses native recovery. The result has NativeRecovery status,
NativeDisplacement reason and no credited attack/heal sequence. Explicit movement
instructions retain their separate native semantics. The existing recovery
witness is displayed with the existing marker/line primitives; the endpoint is
labeled Native knockback recovery, not a predicted combat landing.

Native mobility selection ownership and elective Ley Lines placement yield while
this ordinary forecast is unavailable. Other support, tank recovery and targeting
are not globally disabled. No ability request, RSR setting toggle, second movement
writer or new IPC is added. Installing the plugin still does not activate AI.

This gate is an INTERMEDIATE safety repair. It does not implement the three
knockback preferences or replace the requested joint knockback planner. Those
settings remain open in TODO.md and their inventories. Remove/replace the gate
only when the same native service can represent the actual forced transition,
landing, timing, action interruption, immunity and recovery coherently.

## Shared native geometry

`GenericKnockback.TryUnclippedMovement` factors the existing direction and pull-
limit arithmetic into one scalar operation. Native CalculateMovements calls it;
future frozen planning values can call the same operation without live actors.
Native participant-layer and immunity applicability are also shared. Source
ordering, shape checks, wall stops, safe walls, actor radii, custom destination
rules, public class identities and source contracts remain native and unchanged.
There is no second displacement or hazard model.

Independent endpoint fixtures cover radial pushes, pulls, forward/backward/left/
right displacements, coincident origins and minimum pull distance. Existing native
counterfactual tests cover every native kind across wall modes and starting poses,
including custom destination context and immunity. No encounter module was edited.

## Validation and deployment

The managed plugin build passed with six existing upstream gauge warnings and
zero errors; the harness built without warnings/errors. The exact DLL passed
42,626 native assertions, all 19 broader suites, installed RSR 7.5.7.6 contracts,
nine repository tests and whitespace verification. The broader runtime receipt
matches the artifact hash.

Fresh preflight verified one affected idle BLM client, normal conditions, no
active preset/target, native AI inactive, RSR Off and standalone XCAI unloaded.
The normal watcher completed reload at 02:22:28 UTC on 2026-10-24. Loaded MVID and
watched/artifact hashes matched; the post-reload native snapshot reported settings
Ready, RSR Off and no failure. This is deployment/idle lifecycle proof. An actual
knockback, immunity delivery or improved landing was not tested live here.

## Remaining implementation boundaries

1. Capture coherent native source/provenance and immutable geometry for the joint
   search. Do not pass components, actors, native wrappers or live shape callbacks
   to workers. Use the shared displacement arithmetic and native traversal oracle.
   Re-evaluate old single-source, candidate/raster-window and route-poll limits
   rather than preserving transport-shaped constraints.
2. Model preparation, forced movement and recovery as one route. Keep multiple
   states when time, immunity, action progress or charges differ. Casts that
   complete before a push must remain available even when their animation lock
   overlaps it. A cast extending into actual displacement cannot be credited as
   ordinary stationary execution. Unknown motion completion needs an explicit
   bounded result; cast finish is not proof of landing time or hazard expiry.
3. Separate ONLY exactly attributed preparation constraints from independent
   landing/recovery hazards and assignments. `NativeLandingConstraints` retains
   useful native attribution, geometry and conservative-bound checks, but its
   current one-radial-source/simple-arena restrictions are not a complete joint
   planner. Mixed projection/AoE graphs and opaque native rules are not permission
   to discard requirements. Native recovery and optimization must share the
   resulting implementation, not diverging hazard models.
4. Add Surecast/Arm's Length as legal native action stages with observed resources,
   locks, application/duration and immunity-ignoring source rules. Preserve the
   original RSR concrete actions, explicit commands, saved action opt-outs and
   foreign changes. Do not revive cross-plugin configuration leases or direct
   controller UseAction calls. Existing native buffs remain authoritative until
   a requested new buff is actually corroborated. Compare accept/reposition/
   immunity and evidenced recovery in the same execution windows.
5. Preserve the old tank Arm's Length damage-mitigation behavior explicitly: the
   standalone controller also considered multiple engaged melee attackers when
   no native displacement was published. This is support/mitigation parity, not
   evidence that a knockback exists, and must keep resource/manual/native gates.
6. Migrate local KnockbackKnowledge and its learning/forecast/applicability
   policies to native world/effect observation and native persistence. Preserve
   saved opt-outs, off-by-default learned fallback, bounded local samples and the
   original knowledge file for rollback. The native ActorState effect decoder
   already observes knockback/attract effect rows and pending-result identity;
   reuse that observation instead of installing another packet hook. Do not
   equate a native pending-effect timeout with actual motion duration.
7. Learned admission requires a COMPLETE authored-displacement inventory. A null
   NativeKnockbackAuthorityCapture result is NOT proof of no published source:
   known immunity or an event outside the current horizon can produce null.
   Unsupported custom sources, forced march, projection layers and authored
   constraints retain authority. No companion/coordinator/ToF work is in scope.
8. Revalidate exact preparation, source identity, anchor/facing, trajectory,
   landing and onward route before execution. Keep original deadlines and native
   pending-effect sequencing; cancellation, death, manual input, module removal,
   failed immunity and a stale worker must release scoped ownership correctly.
   Diagnostics/overlay must distinguish source estimates, verified geometry,
   actual buff/effect/landing, bounded search and native recovery.

Required next evidence includes an independent small-map temporal/reference
solver; sequential pushes and warnings; immunity accepted/ignored/unavailable;
casts and overlapping locks; unknown completion; layers/walls/disconnected
geometry; individual attack/heal recipients; stable repeated frames; and actual
movement/immunity/lifecycle checks. Do not close the migration on the strength of
this safety gate, shared math or test count alone.

## Native local observation and import checkpoint, 2026-10-24

Loaded SHA-256 `53c1e540373e6ec52589a020e88538075a10ce282506ba862a70252751d2b380`,
MVID `b2850359-db95-4ed5-af5b-d98715cf44a8`. This stage completes the local
observation/store implementation portion of boundary 6 above. The actual
knockback policies, planner admission and motion/immunity gameplay acceptance
remain incomplete.

`Knockback/NativeKnockbackKnowledge` consumes native World.Actors.CastEvent and
native actor/cast/status values on the framework owner. There is no ECommons
packet hook, reflected transport, raw native cast read, community sync or companion
change. Native cast/effect identities are matched to sampled physical onset and
stable endpoints. Identical effects are deduplicated while distinct target
fragments retain their own identity. Attract variants and reversed/source-applied
effect flags cannot be reinterpreted as ordinary source-to-player Knockback rows.
Unknown or missing evidence yields no learned permission. Source polling retains
the original native cast clock; a repeated zero cannot renew the deadline.

The local motion, competing-origin, timing, area-applicability and contrasting
immunity policies are preserved. Three varied motion samples and distinct timed
casts remain necessary. Undated or stale samples cannot borrow a recent entry's
last-seen timestamp. Current effect-row distance/speed/direction must agree.
Manual or controlled walking contamination, death/resurrection, ownership loss,
logout/identity changes and backward clocks clear pending observation. These
empirical limits are documented evidence bounds, not universal physics or safety
proofs. A classified forecast still needs joint native route/landing validation.

KnowledgeStore/KnowledgeSession perform lifecycle IO outside the framework thread.
An existing native document takes precedence. A one-time validated import records
the original content hash and preserves the standalone file. Invalid/future native
formats are retained rather than replaced from legacy input. Snapshots own their
arrays; no actor or wrapper reaches an IO worker. Saves run outside combat or on
unload; Plugin.DisposeAsync awaits the final ordered persistence task after native
subscriptions are removed. Current diagnostics expose only aggregate entry/motion/
timing/model counts and lifecycle detail. No learning history stream or selected
world position is exposed; DevBridge still owns combat history and captures.

The managed build passed with six existing warnings and zero errors. The exact
artifact passed 42,736 native assertions, all 19 broader suites, installed RSR
contracts, nine repository checks and whitespace verification. Converted legacy
math fixtures and actual native-event/pose fixtures cover varied models, timing,
individual effect identities, duplicate/source flags, manual contamination,
death/resurrection, undated evidence, immutable snapshots, malformed files,
UTF-8 BOM import, repeated startup, ownership gates and final-save cleanup. These
are offline inputs; they do not establish actual live displacement delivery or
representative framework cost. The existing broad formatter limitations remain.

Fresh idle preflight preceded the watched write. Loaded MVID and first import
were verified at 03:06:28 UTC. All three legacy records were retained exactly;
MotionSamples, TimedOrigins and GeometryModels were all zero. The source file hash
matched the native import receipt. A second normal watcher reload produced a new
provider epoch at 03:06:58 UTC and reported Native local observations loaded.
Both files remained byte-for-byte unchanged, proving established native-file
precedence and idle unload/restart. Settings Ready, RSR Off, no failure, no active
preset/target, normal conditions and unchanged player position were verified.
No gameplay action or movement was requested. No new live knockback or immunity
sample was claimed.

**Next implementation:** connect full authored/learned displacement planning,
not another observation adapter. The inventory and phase-capture foundation is
implemented in the checkpoint below; unknown publishers/constraints still cannot
certify absence. Complete cast/effect generation and original timing/resolution
binding, then consume those frozen native phase maps in the joint search.
Integrate preparation, forced motion, immunity and evidenced recovery into one
search/publication/executor, preserving casts completed before a push even when
their action lock overlaps it. Unknown completion remains explicitly bounded.
Retain exact native projection/wall/layer semantics, independent hazards and
recipients, and revalidate the actual route/buff/trajectory/landing before use.
Do not close any of the three knockback preferences on this observer checkpoint.

## Native source inventory and phase-map checkpoint, 2026-10-24

Loaded SHA-256 `0e2072836449d653e6ae0c967966b93cc17b1c6e3090d333786aebee28cbe224`,
MVID `e39f8f66-da0a-467a-ad3a-33c5cad990b4`. This implements the next native
INPUT layer. It does not implement the forced-transition search/executor or close
ManageKnockback, PlanKnockbackLandings or UseLearnedKnockbackFallback.

NativeDisplacementCapture queries registered GenericKnockback and GenericForcedMarch
publishers coherently on the framework owner. The worker-safe inventory retains
raw source geometry, absolute activation, native application limits, immunity,
participant/layer restrictions and wall values. Native source presence survives
ordinary interruption filtering: observed immunity, a distant activation, zero
distance and Kind.None cannot become an empty authored inventory. A current-pose
empty result from a pose-dependent publisher remains incomplete. Pending effects,
march state and registered preparation markers are independently identifiable.
Uninspected hints and opaque/unbounded independent evaluators cannot certify
absence. This is an inventory of native interfaces and declared constraints,
not proof that an unknown future mechanic cannot exist or movement is safe.

A newly exposed source-level integration gap was corrected: an imminent authored
preparation marker with no modeled generic source could otherwise allow the
ordinary stationary action forecast through an unmodeled push. That frame now
uses native recovery with incomplete evidence. Markers beyond the twelve-second
forecast still retain presence without prematurely interrupting the forecast.
No specific observed fight failure or upstream encounter cause is claimed.

NativeKnockbackPhaseCapture verifies exact producer attribution and only separates
recognized pure circle/square preparation leaves from unions. Independent damage,
assignments, future warnings and physical obstacles stay in the original native
rasterization; no hazard expiry is invented. A pure landing envelope is retained
as an explicit requirement at its forced landing. It is not added permanently to
subsequent walking space. Opaque mixed projection/AoE helpers, unsupported
walls/layers/footprints and ambiguous attribution stay native-owned. Successive
sources get ordered maps; native source-count limits are retained rather than
silently increasing applied pushes. Independent simultaneous owners require an
authored composition. These are declared partial-support boundaries to extend,
not a replacement definition of migration completion.

Native producer/shape handles stay in the framework-only capture. Planning contexts
hold scalar records and NativeNavigationSnapshot rasters/frozen goals, using the
same native rasterization as recovery. A changed source, constraint or relevant
countdown invalidates input; an equivalent repeated capture keeps absolute source
times and ages warning deadlines. Changed original union graphs cannot authorize
removal, and mutating a source graph after freeze cannot change a worker map.
Neither the original recovery hints nor public native contracts are modified.

The managed build passed with six existing warnings and zero errors. Its exact
artifact passed 42,761 native assertions, all 19 broader suites, installed RSR
contracts, nine repository checks and whitespace verification. New cases cover
absence versus filtering, pose-dependent empty publishers, march/pending state,
preparation-only rules, exact mixed unions, independent damage, landing-envelope
scope, graph mutation, opaque helpers, successive sources, application limits,
ambiguous simultaneous owners and unchanged original clocks. The 1,600-cell
synthetic phase capture measured median 0.137ms / maximum 0.365ms across ten
captures. That excludes actual game/module workloads and is not live frame-time
acceptance. No optimization quota or old IPC window limit was added.

A fresh idle preflight preceded the normal watched write. Loaded MVID, Settings
Ready, RSR Off, knowledge Ready and no failure were verified at 03:55:04 UTC. The
current diagnostic reported zero owners/sources/preparation rules, complete
inspected inventory and no phase maps requested by an active movement owner.
Native and legacy knowledge files remained unchanged. This is idle diagnostic/
provenance proof, not an actual knockback, selected preparation/landing route,
immunity delivery or live phase-capture benchmark. Full planner/executor, effect
resolution, cast-lock overlap, supported projection expansion and gameplay tests
remain open.


## Compulsory authority versus correction preferences, 2026-10-24

A source-level migration defect gated compulsory march planning on the optional
facing/heading correction owner. With both corrections off, the planner could
instead evaluate ordinary movement/actions during the imminent or active march.
The framework-native compulsory gate now uses the same existing timing evidence
independently of those preferences. The correction owner still determines whether
RSR may be paused and whether heading changes are authorized. With corrections
off, only the observed heading is projected. Missing/empty projections retain
native recovery rather than fall through to ordinary forecasting. Native manual
and activation gates remain binding. Elective selection and downtime facing yield
to the same compulsory authority. Existing decision-overlay forced route markers
remain applicable; no new overlay primitive or settings were added.

Managed compilation passed (six existing warnings, zero errors), and the exact
artifact passed 42,767 native assertions. New cases cover distant countdowns,
imminent/active marches, correction-disabled observed-heading capture and the
existing transition/stale-pending margin. Artifact SHA-256:
`8b9c7933d4c8b1b079993187d45fe6d865968f46aebe438e31ff8810d664b47e`;
MVID `2e834771-f9a1-4933-8552-f01423b1f90b`. This artifact was not copied to the
watched path. No live march failure, cause, correction delivery or acceptance is
claimed. Longer-horizon casting through a later march and complete knockback
planning remain unfinished; the existing correction window is not proof for the
entire twelve-second action forecast.


## Joint phase search, 2026-10-24

The search now consumes explicitly timed NativeKnockbackPhase records. Each label
retains its displacement index: an earlier pre-push label cannot dominate a
post-push state at the same cell. The same shared traversal, mobility and native
raster implementation receives the appropriate frozen map for each phase. Native
source inventory must be complete and match the entire ordered phase sequence.
A final candidate must include all pushes and a useful safe destination, including
an onward escape if the landing has a later warning deadline. It is never enough
to reach the first temporary landing. Resource state and observed action progress
survive the transition; recharge clocks remain relative to the original capture.

A phase requires an explicit absolute onset/landing window. The preparation point
must be reachable before the earlier of authored activation and evidenced onset.
Landing geometry uses shared GenericKnockback scalar mechanics and the native
landing envelope. Because the current timing model does not prove intermediate
speed, the entire forced corridor must be clear through latest landing. This is
conservative and does not invent linear flight timing or warning expiry. Ordinary
steps/actions cannot span an unresolved compulsory event. The initial walking
pass cannot stop at a pre-push current opportunity and call that the complete
compulsory route. Unknown timing, unsupported/missing phases and mismatched native
source records are explicit NativeDisplacement bounds rather than ordinary plans.

This is a staged search implementation, **not runtime knockback completion**.
Framework timing acquisition, native event/source disappearance handling, exact
landing/completion confirmation, current-cast/animation-lock overlap and general
projection support still need implementation. MovementPlanValidation explicitly
rejects the new Knockback stage until that binding exists; the current production
capture continues native recovery. Do not remove the guard merely to obtain a
live movement result. The native twelve-second horizon, freshness and sequence/
memory bounds remain unchanged. No optimization quota was added.

The new regression compares the selected result against independent graph
enumeration using the same native raster/traversal, without the joint labels,
dominance, transition or objective implementation. It also covers unknown motion
timing, unsupported inventory, invented source geometry, authored deadlines,
zero-duration rejection, uncertain corridor timing, capture identity, temporary
landing without escape, successive pushes, charge preservation with and without
resource pruning, and current/subsequent action delivery around the forced phase.
The goal fixture was corrected during validation: native four-corner sampling
requires a goal to cover a full cell; a strict interval ending at both cell
corners had correctly produced zero priority. The native sampler was preserved.

Managed compilation passed with six existing warnings and zero errors. The exact
artifact passed 42,826 native assertions, all 19 broader suites, installed RSR
contracts, nine repository checks and whitespace verification. SHA-256:
`d64ba2bf9eccc9eff7a0838b5aa978acaa2692d2edfee273b81416f6edf4b4ff`;
MVID `83e1c08e-59af-40cf-8e1d-9c804411a8f9`. No watched write or new gameplay
experiment occurred. The watched DLL remains the earlier 0e207283 checkpoint.


## Native preparation validation and completion receipts, 2026-10-24

The blanket search-stage rejection is replaced with a data-bound preparation
validator. Every remaining push must match the fresh complete native inventory,
caster/action/data identity, observed cast generation, absolute motion window and
landing envelope. A new unresolved push invalidates an ordinary commitment. The
validator traverses each frozen native phase map, recomputes the exact executable
projection from the observed stopped pose, and retains action/resource/constraint
checks through onward recovery. No current phase publisher yet supplies all
required timing/generation data: production known-push capture still uses native
recovery. Do not infer runtime activation from the validator's synthetic success.

The native observer now supplies one expiring local KnockbackCompletionEvidence.
It requires a correlated native effect counter/cast generation, reliable observed
motion onset, full physical displacement, a stable endpoint and no pending native
effect. Caster, action/data, cast generation, original onset/landing window,
launch/landing and current player/territory must all match before a stage can be
consumed. Cast completion, source removal and a pending-effect timeout are not
physical completion. Repeated reads cannot renew the original observation time;
manual/lifecycle invalidation and new local physical effects discard old evidence.
This is current transaction state, not a persisted combat-history buffer. Native
forecast contexts also carry the observer's generation, preventing a subsequent
cast of the same action from borrowing an old cast snapshot.

The shared executor waits for a native push rather than emitting walk/action
commands for its displacement. Existing in-world marker primitives show launch
and landing; current diagnostics expose bounded source/generation/timing fields
without serializing native phase maps. No overlay style or new HUD was added.

An important remaining gap is **active-motion refresh**. A normal push can outlast
the existing 250ms plan freshness ceiling. Completion evidence cannot freshen that
expired plan. The framework needs a scoped native transaction from commitment,
through source-removal/effect-result gaps and active motion, to observed landing.
Fresh captures must preserve the original event window and exact launch/landing,
not reapply the full displacement from an intermediate pose. Native recovery
remains the fallback when evidence changes. Before timing activation, also finish
cast/animation-lock overlap, phase-aware departure/cast deadlines, actual body
footprint and supported geometry expansion. The current whole-sequence slack can
be overly conservative after a fixed event: a post-push deadline must not cancel
an otherwise safe pre-push cast whose departure wait can absorb the delay.

The exact artifact passed managed compilation (six existing warnings, zero errors),
42,847 native assertions, all 19 broader suites, installed RSR contracts, nine
repository checks and whitespace verification. Added checks exercise native event/
physical completion, original receipt clocks, generation/source/action mismatch,
pending/still-published sources, stale results, preparation/manual validation and
bounded overlay/diagnostic proof. Artifact SHA-256:
`e845da18261c55c9a1fc8ee5158a6d094d78fb3622d6ed842e04922eeb6fab21`;
MVID `bf0e073c-790e-495e-b506-57a38331f940`. No watched output was written; no
actual gameplay knockback or complete integrated execution is claimed.


## Runtime binding and source/effect transaction, 2026-10-24

Supported native radial phases now enter the runtime planner through typed
NativeKnockbackTimingCapture. The current cast action/data identity, cast finish,
observed generation, distance and origin must match the authored source and
corroborated local motion forecast. This uses local timing to complete an already
authored native geometry model; it does not enable learned geometry fallback.
Native participant, immunity, exact preparation provenance and independent
constraints remain binding. The actual actor landing footprint is retained.
Unmatched, incomplete, ambiguous and unobserved models retain native recovery.
The current imported hit-only entries therefore cannot activate this path.

NativeKnockbackTransaction is one movement owner's scoped causal state. It keeps
the original source/generation, observed launch, expected landing, useful endpoint
and absolute motion window through cast-finish/source-removal and effect-result
gaps. It does not renew times by polling. Active captures use fresh native rasters
and a compulsory prefix through the same label search and executor. The entire
remaining corridor must stay clear through latest landing; no intermediate speed
is invented. The push is not applied again from an intermediate position. Future
native phases remain separate. Source presence and pure preparation separation
are still inspected; unrelated damage, assignments and physical geometry remain.

Correlated stable physical completion releases the transaction for a fresh onward
plan. Window expiry is an explicit failed forecast, never synthetic completion.
Manual/owner/module/player/territory or backward-clock changes release it. Native
immunity may release preparation only before onset and when it covers the whole
onset window. It cannot cancel physical motion already underway. Cast windows are
clamped against the original event after both normal and follow processing. The
old whole-route departure shifter is disabled for fixed pushes; first-departure
validation retains the prefix clock rather than charging an independent post-push
deadline against a safe earlier cast.

Remaining scope includes general projection/wall/layer/custom support, native
immunity stages, tank slow mitigation, learned fallback admission and actual live
mechanic/action delivery. These support boundaries do not redefine migration
completion. An active source that cannot be separated or revalidated still yields
native recovery. No standalone controller, new IPC, actor wrappers on workers,
extra movement writer or gameplay history was introduced.

Native regression covers source removal before effect, nonrenewing event/cast
windows, fresh active prefixes beyond the old preparation input's lifetime,
physical completion versus expiry, changed ownership, typed learned-timing/native
geometry matching and actual footprint. The exact artifact passed managed
compilation (six existing warnings, zero errors), 42,856 native assertions, all 19
broader suites, installed RSR contracts, nine repository checks and whitespace.
SHA-256 `0ff8572c4b893ffafa2c1d97053913bf94cb2d43a40a474660223d7eba98c2e7`;
MVID `f0296d55-13e4-4c4a-9460-6993e7d18667`. Fresh idle/out-of-combat preflight
preceded the watched write; automatic reload/loaded MVID, Ready settings, RSR Off
and no failure were verified. At 05:38 UTC the main BLM remained at the same pose,
without target/preset, under normal conditions, with XCAI unloaded. Native and
legacy knowledge hashes remain unchanged (three records, zero motion models).
This is idle lifecycle/provenance evidence, not actual knockback acceptance.

### Cast delivery and overlapping animation locks

The joint search now separates a cast delivered strictly before the earliest
possible push from its remaining animation lock. Native forced displacement and
subsequent walking may proceed during that lock; another cast, mobility action,
utility or field placement must wait until it clears. Labels retain action-ready
time independently of physical arrival, and dominance cannot discard a state
with earlier action availability. Current action recipients, original action
windows and native route/departure checks remain binding. Cast timing evidence
records actual delivery duration, and diagnostics expose the independent lock.

Fresh validation recomputes the cast delivery, lock and full remaining route. A
consumed cast retains its original absolute lock deadline reduced by actual input
age. The executor still requires observed action delivery and does not infer a
completed cast from its forecast. The blanket runtime recovery case for this
modeled overlap has been removed. Unknown source timing and unsupported native
geometry retain native recovery. This change concerns planned action delivery;
it does not establish live acceptance of an already-in-progress cast or push.

The exact artifact passed the managed plugin build (six existing gauge warnings,
zero errors), 42,864 native assertions, all 19 broader suites, installed RSR
contracts, nine repository checks, inventory parsing and whitespace checks.
SHA-256 `c7e453e16e636f4b6924f623358ee3f03bb17465cea1f03efe7178fba3a68a39`;
MVID `8030a1f4-cf84-4432-9062-bee55f4c2a1d`. The new cases cover lock surviving
the landing, walking during that lock, delayed next action, exact-onset rejection,
changed lock, lost required recipient, observed-delivery gating, reference-search
agreement and bounded diagnostic output. This is offline behavioral evidence;
there was no actual knockback encounter in the idle-client check.

Fresh 05:51 UTC idle/out-of-combat preflight preceded the normal watched write.
Automatic reload at 05:52 UTC reported the expected loaded MVID, Ready settings,
RSR Off and no failure. The final composed snapshot retained the same idle BLM
pose, no target, normal conditions and standalone XCAI unloaded. Both knowledge
files remain byte-for-byte unchanged (three hit-only records, zero motion models).
No gameplay action or test movement was performed for this checkpoint.

### Native immunity search alternatives

Surecast/Arm's Length can now be represented as an immutable utility alternative
in the same phase search, with observed charge/cooldown slots, animation lock,
application bound and the six-second effect lifetime. The effect lifetime is
counted from action start conservatively, while activation must follow the
application bound strictly and the entire latest-onset window must precede expiry.
The action and charge have no independent score bonus. Taking a useful safe push
retains the charge among equivalent execution results; immunity can preserve an
action that would otherwise be lost during forced motion. Every prevented source
is checked separately, including native immunity-ignoring rules and authored
activation times. Only its exact preparation leaf is removed. Independent damage,
assignments, physical geometry and later sources remain in the same native maps.

Fresh validation recomputes readiness, application, resource use and the complete
route. An observed action request cannot stand in for a confirmed native buff:
validation after consuming that stage requires a fresh plan from actual native
status, and the executor cannot consume a prevented event as a physical landing.
Overlay markers identify the planned immunity position using existing primitives;
current diagnostics identify the action, application bound and duration.

This is the native search/validation foundation. Runtime capture deliberately does
not yet emit these alternatives: original RSR concrete-action approval, automatic
selector ownership, manual-command priority, native buff confirmation, application
observation and failure/departure recovery still need the coordinated runtime
owner. Existing RSR/user action configuration is not changed. The current runtime
remains on the previous validated artifact until that runtime work is ready.
Do not close ManageKnockback or claim immunity delivery from synthetic candidates.

Successive-source testing exposed and corrected an illegal optional-push branch:
active immunity automatically prevents eligible displacement. A plan cannot take
a useful covered push by choice. Possible application/expiry bounds are retained
as well as the guaranteed window; partial overlap cannot certify either physical
or prevented motion. Different immunity lifetimes are separate search states,
because a longer buff can remove a useful later push and therefore is not a
universal dominance improvement. Exact validation enforces the same rules.

The exact isolated artifact passed managed plugin compilation with six existing
gauge warnings/zero errors, 42,879 native assertions, all 19 broader suites with
a matching artifact receipt, installed RSR contracts, nine repository checks,
inventory parsing and whitespace checks. The native harness built cleanly.
SHA-256 `c698579d856fa0484ed37e1d13b20e9a8120cfa3a4a829f8941826801d03b023`;
MVID `d7edc439-b933-4fa2-9697-90f132a96c5d`. No watched write, live action or
immunity delivery check was performed. The watched DLL remains the preceding
`c7e453e16e636f4b6924f623358ee3f03bb17465cea1f03efe7178fba3a68a39` artifact.

The next runtime owner must coordinate original RSR concrete-action approval,
automatic selection and explicit-command priority within its existing callback.
RSR 7.5.7.6 recomputes automatic flags inside that callback; setting an outer
flag or porting the old saved-action disable lease would not supply correct
ownership. Application observation, confirmed native buff coverage and failed
application/departure recovery are still required before runtime admission.

### Original RSR selection and runtime admission

Native capture now proposes the job's real Surecast/Arm's Length action using
its native unlock/status, saved enabled state, observed charges/shared cooldown,
lock and response estimates. Admission requires the supported bound native phase
context, ManageKnockback and existing movement/action/manual gates. RSR's actual
automatic AntiKnockback request is required. This does not fabricate rotation
prediction or add an automatic flag around RSR's updater.

RsrKnockbackSelection joins the existing single RSR callback scope. It forwards
original CanUse and preserves the original concrete action. A selected immunity
plan cannot commit until that exact publication receives original approval. At
the verified utility stage, the shared executor waits for the selected rotation;
the owner releases its original selector after exact route/position validation.
There is no separate native UseAction submission, config disable lease or second
handler. Explicit commands, preview, force-enable, saved opt-outs, original
rejection, foreign replacement, actor/zone/source changes and stale inputs keep
or restore external authority. A complete verified plan without immunity may
defer automatic immunity to preserve a useful physical push; bounded no-immunity
search results do not suppress original RSR selection.

Until native status confirms immunity through the whole onset window, planning
requires a physically safe failure branch from the utility position after its
lock. This uses the same native phase transitions, corridors, landing constraints
and final permanence checks. Initial admission supports standing through the
remaining physical events as the failure branch. Walking/mobility contingencies
and utility locks crossing onset remain implementation work; do not close full
knockback parity around this supported subset. Request/effect observation alone
cannot release immunity-dependent movement. Confirmation causes a fresh native
plan from actual status and preserves normal movement continuity.

Current diagnostics include original-selector ownership/status. No live immunity
claim is implied. Following the user's steering, complete remaining systems and
offline checks first; dungeon-dependent testing follows the implementation handoff.

Tank Arm's Length selection remains transparent while its auto-attack slow
opportunity is still absent from joint planning. It is not offered as a native
immunity candidate yet, so this new selector cannot accidentally suppress an
unmodeled original defensive use. Tank mitigation parity remains implementation
work and must be completed before declaring all systems reintroduced.

Runtime-owner checkpoint: managed plugin build passed with six existing gauge
warnings/zero errors; native harness built without warnings/errors and passed
42,884 assertions. All 19 broader suites match the exact artifact. Installed RSR
role factories and automatic-status contracts, nine repository checks, inventory
parsing and whitespace checks pass. SHA-256
`1b9942df93263a35ab6e7500894607277c27d7a38803069b0710f3ef280f5d64`;
MVID `13473baf-e201-407c-b911-dfacf3d7100c`. New regressions verify native
fallback timing after the utility lock, rotation-owned delivery, no movement from
request-only observation, manual handoff and correct job role actions. The artifact
is isolated; no watched write or live gameplay probe was performed.


## 2026-10-24: original role selection, tank mitigation and independent preferences

The current isolated artifact is `1d300cd5a8dd5578f65beb60de8317633da3bf390cb7aa276a407deafd11e920`
(MVID `c04543f1-b02a-4ea6-a386-5201d2f8a501`). The plugin and harness build,
42,916 native assertions, installed RSR contracts, all 19 exact-artifact broader
suites and nine repository checks pass. No watched output was changed.

A native immunity plan can request the original role action through RSR's existing
GeneralAbility slot after its original duty, job, potion, role and speed choices.
The separate RSR AntiKnockback prediction flag was a selection-path restriction,
not a mechanical requirement, and no longer gates a native proposal. The original
CanUse, concrete action, saved enabled state, manual/explicit commands, callback
ownership, exact native approval/stage and observed native buff remain mandatory.
The original automatic selector is also deferred only for the same verified plan;
a bounded no-immunity search cannot suppress its automatic action.

Tank Arm's Length slow mitigation now uses that same original action pipeline.
Its opportunity requires a verified dungeon trash module, at least two distinct
living nearby engaged attackers targeting the tank, no current immunity or unresolved
displacement, no known knockback during the action's actual 120-second cooldown,
and a legal native free weave. No saved action lease or direct UseAction is restored.
The action has no stable spatial decision to add to the world overlay; its current
selection reason is exposed in existing diagnostics.

ManageKnockback and PlanKnockbackLandings now operate independently. With landing
preparation off, search/validation forbid physical preparation alternatives while
retaining a verified immunity-only plan and the required physical failure check.
A charge/readiness failure retains native recovery. The flag is part of immutable
problem identity and is retained across active-prefix refreshes.

This does not complete learned fallback admission, general projection or
walking/mobility failure contingencies. Those are code work. Actual mitigation,
immunity and push delivery remain user-run dungeon acceptance after implementation.


## 2026-10-24: learned motion enters the native planner

Current isolated artifact: `c792c1e1e708a9144529cd54e06c268a6f81505c919a18f83e52293ae74fa7fa`,
MVID `ee8f783c-af68-4936-86e2-f30875e6f0d3`. Managed build: six existing warnings, zero errors;
harness build: zero warnings/errors. 43,007 native assertions, all 19 exact-artifact
broader suites, installed RSR contracts and nine repository checks pass. No watched
output or live client was changed.

`NativeLearnedKnockbackCapture` admits a single uniquely observed BattleNPC cast
only after the COMPLETE raw inventory proves authored absence, including hidden/
failed publishers, preparation constraints, pending effects and forced march.
Learned geometry is a separate immutable binding and never enters that raw inventory.
Existing observations corroborate radial, world-direction and source-relative motion,
source identity/generation, origin, applicability and physical onset/landing windows.
Independent learned immunity evidence is required for that role action; timing or
hit counts alone cannot grant prevention. An already active buff must prove complete
coverage before onset; an uncertain outcome retains native recovery.

The whole supported native map participates in the same search and executor. Native
physical geometry, hazards and independent assignments remain unchanged. The old
4.5-yalm local rings, anchor clustering and 18-cell advice cap are removed. A separate
complete-graph reference agrees on the selected route value and walking distance.
Current-action delivery during a prevented push, independent recipients, source
conflicts, unavailable trust and no safe landing are covered.

Fresh observations keep the FIRST event clock. Reported client cast end and corrected
NPC finish remain distinct, so the normal cast-to-motion gap cannot erase a supported
current-action window or fabricate completion. A source-record generation avoids
invalidating that binding for unrelated learning. An additional known cast, changed
source/model/clock/ownership, invalid coordinates or uncertain buff cannot silently
reuse it. Unsupported projection layers and unmodeled sequences retain native recovery.

A failed scoped forecast remains unresolved after caster publication ends; its
original deadline is not renewed. Native effect plus stable physical completion may
retire the failed event at its actual landing without crediting the earlier predicted
route. Successful route completion retains the original exact landing checks: receipt
and actual-position tolerances cannot accumulate. No timeout or request becomes a
physical-completion receipt. Existing markers identify estimated launch/landing;
in-flight diagnostics distinguish retained learned motion from forced march.

`UseLearnedKnockbackFallback` works with either landing planning or immunity assistance,
retains its saved opt-out/default and original import file, and uses no second movement
writer, serialized local transport or direct immunity submission. Actual learned
movement, immunity and lifecycle delivery remain user-run dungeon acceptance.
General authored projection, in-progress casts and walking/mobility failure
contingencies remain implementation work; this checkpoint does not close them.
