# Native offensive targeting: evidence and acceptance

The standalone baseline is `4a0f7968da8a743e8971973821cb9f6f1387198c`.
External source comparison used installed RSR tag `7.5.7.6`, not the older checked-out
working tree. Both service types below are internal native code, not a replacement
IPC or a native BMR rotation predictor. The services are now connected to the native runtime; live acceptance remains pending.

## Implemented native services

`Targeting/OffensiveTargetPolicy.cs` contains an immutable selection frame and
native actor/action geometry values. It reuses `RsrExecutionCapture.Shape` and
`ExecutionOpportunity`, with no hazard implementation. Positive native primary
priority, engagement, forced/manual retention, forbidden collateral, DoT exclusion,
current target and individual required recipients are separate constraints. An
alternative ordinary area anchor needs a strict gain; equivalent choices retain
the current target. This is a guard on an explicit replacement proposal for an already selected opportunity, not a wholesale RSR action-target allowlist. Native single-target DoT/status selection and friendly recipients must retain their original semantics during wiring. Confirmed tank recovery can break a coverage tie without
losing current recipients. Acquisition has one stable candidate and native
`Plugin.SetTarget`'s strict three-dimensional <50-yalm envelope; retaining an
already-present target is a separate case.

`Integrations/RsrAutomaticTargetScope.cs` replaces the old two-Boolean lease with
a same-callback scope that also handles `DataCenter.IpcOverrides.TargetFreely`.
It retains HostileType, AutoOffAfterCombat and FriendlyPartyNpcHealRaise exactly.
It never changes RSR State/IsManual, starts AI or writes a lasting configuration.
Cleanup preserves a foreign record replacement or Boolean value, including on
failed setup. Scope ownership must include the original callback's fallback
phase, not just action selection.

The isolated candidate SHA-256 is
`db5b9317e327df3f347172c689b29df1a55e47a2a5a0ee26a2dc993ac624d87f`,
MVID `33cd6e77-be35-44c9-a7ea-d6e91f2b89af`. It built with six existing upstream
warnings and no errors. The native harness passed 42,400 assertions, including
independent recipient-set alternatives, priorities, opt-outs, collateral,
acquisition, optional override restoration and failure cleanup. Installed RSR
checks constructed and suppressed the real record without mutating a running
plugin. No runtime hook or target write was tested or deployed by this checkpoint.
The prior Pictomancer build remains loaded. The three target settings stay pending.

## Verified RSR behavior that the runtime wiring must cover

- `RotationSolver/Updaters/MajorUpdater.cs`, `OnUpdate`: observation and rotation
  refresh precede `ActionUpdater.UpdateNextAction`, `CanDoAction`, and `DoAction`.
  `MiscAndTargetFreelyUpdate` runs later in the same original callback. With State,
  combat, no NextAction and no hard target, it can select the closest hostile.
- `DataCenter.TargetFreelyEnabled` uses an active optional IPC override before
  configured TargetFreely or TargetFreelyOverride. The old two-Boolean standalone
  lease therefore cannot suppress all installed fallback sources.
- `Commands/RSCommands_Actions.cs`, `SwitchTargetForAction`, writes the hard target
  from the original concrete BaseAction before Use, and can also run through
  `UpdateTargetFromNextAction`. Its friendly-target option and manual mode matter. In Henched mode,
  `IsManual` is true and prevents this writer from changing the hard target; the
  broader automatic-mode writer still needs constraints when that mode is used.
  Suppressing the closest-enemy fallback alone is insufficient.
- `BaseAction.CanUse` sets its concrete action Target/PreviewTarget. It is not a
  read-only forecast query. Do not call it to inspect hypothetical future attacks.
  RSR post-use bookkeeping depends on the original BaseAction type.
- `ActionTargetInfo.GetCanTargets` retains native range/visibility/CanUseTo,
  resistance/status/DoT/TTK checks and CanTarget. `GetMostCanTargetObjects` already
  optimizes native affected-target counts. Native assistance should constrain
  that selection, not duplicate or bypass its resource/config checks.
- Native self/ground/directional shapes require specific treatment. The installed
  rectangle count uses current facing. A mathematical target-aimed alternative
  alone does not prove the actual action will face that way, especially with
  auto-face disabled, manual facing or a gaze. The current native policy tests
  establish recipient-set math for target circles, not full directional delivery.

## Next implementation steps

1. Connect selection through the existing `RsrSupportCallbackLease` owner, keeping
   the original callback at its exact position and running it exactly once. Do not
   install a competing framework callback or make offensive scope depend on an
   unrelated support-party visibility/named-partner gate. Cover inactive startup,
   first activation, job/rotation changes, failure, unload and stale references.
2. Capture native hints, actors, current RSR action identity/recipients and manual
   authority coherently on the framework thread. Freeze only values. Apply scoped
   native candidate predicates around original RSR selection; retain native
   checks, forced commands, friendly recipients and the original concrete action.
   Publish a matching typed target intent through BMR's existing target path before
   movement capture. Recheck generation/target/recipients and exact execution;
   never turn an old cached target into a fresh opportunity.
3. Finish all three saved preferences and native special-priority cases. The
   foundation's ordinary automatic-primary predicate requires nonnegative native
   priority; before broad wiring, explicitly handle native pointless (-1) current
   targets, invincible (-2), manual undesirable (-3) and forbidden (-4) contracts.
   Native activation, ManualTarget, forced/preset strategies and player changes
   remain binding. Reuse confirmed native tank-recovery evidence rather than a
   second aggro detector. Preserve current execution when recovery cannot share it.
4. Validate directional/self/ground behavior, auto-face/user controls, individual
   recipients and original action clocks before crediting a retargeted execution.
   Expose the selected/rejected native intent in the bounded diagnostic snapshot
   and existing in-world markers. Test the actual selected-action/target/publication
   consumer path and live behavior, not only these pure service predicates.

No other repository, installed binary, public feed or upstream contract was changed.

## Runtime connection, 2026-10-24 (offline validated, live pending)

`RsrSelectionScopeOwner` now owns one original callback for both support and
offensive selection. Scopes restore in reverse order on success and on independent
setup, cleanup and reporter failures. `RsrOffensivePredicateScope` adds constraints
to the original action settings; concrete actions, native CanUse and resource/status
checks remain in RSR. Cached delegates become transparent after disposal and foreign
replacements survive restoration. No CanUse call is made as a forecast.

The framework capture freezes native actors, priorities, engagement, the current
selected action and each affected identity. Partial recipient evidence cannot
justify an improved area anchor. Direct single-target RSR status selection stays
native. Circle replacements need an execution gain without losing recipients.
Directional collateral uses a conservative full-facing envelope; no unproved
turn is credited as improved execution. This constraint needs workload/live review.

Normal movement and follow consume the same native target field. Automatic writers
are tagged internally; explicit native writers, manual target changes/clears,
cast/action locks and preset no-retarget policy retain authority. The public field
and upstream IPC contract remain unchanged. Confirmed loose-enemy evidence comes
from the existing tank policy, including its distance/held-pack restriction.
The current snapshot and existing TargetUptime marker expose selected intent, not
action delivery. Capture timing includes preparation and immutable input capture.

Remaining acceptance includes original RSR callback ordering live, first activation,
rotation changes inside that callback, friendly hard-target options, recipient
transitions, actual AoE selection, tank recovery, manual handoff and cost under a
representative target/action set. Offline consumer fixtures do not prove those
lifecycle/delivery cases. The three settings remain unchecked in TODO.md.

The isolated runtime candidate built with SHA-256
`479ac54c039a93f38f81d88c4fcb59583b265c5693a15c01cb3acd61d49f0006`,
MVID `9a5a9863-83dc-423a-ac64-8874e89c6aac`, six existing warnings and no errors.
The native harness passed 42,429 assertions; all 19 broader suites and nine
repository checks passed on that candidate. Whitespace verification passed.
Consumer fixtures exercise original
predicate invocation, recipient-preserving area selection, native target publication,
manual bypass with original checks, foreign replacements, stale wrappers, failure
cleanup and the native -1/-2/-3/-4 contracts. These use a fake candidate-selection
consumer; they do not instantiate or prove the entire live controller. Installed
RSR checks bind the real predicates and eligibility helper signatures.

RSR can skip a framework cycle before action selection. The controller therefore
records selection time only when that action's native scoped checks were observed
in the original callback. A later read must match its action, target and complete
recipient identities within the original freshness ceiling. A skipped cycle,
partial recipient list or failed selection cannot renew that observation.
This needs the pending live ordering and transition checks above.

## Live constraints and corrected mode/command gates, 2026-10-24

Current loaded candidate: SHA-256 `617715bb7eaf00f9c8e809bd651ad0d408f00a977a7f79990c8daef17a3ee202`,
MVID `49e3cabf-56ce-4e23-92af-9bcbfad35120`. Managed build, 42,431 native assertions,
19 broader suites, nine repository checks and installed contracts passed. The
normal watcher finished loading at 00:05:36 UTC and the loaded MVID was checked.
Installing/reloading did not activate AI.

Bounded BLM dummy checks used a temporary native AutoTarget/NormalMovement preset.
The original RSR callback was owned, the native authority gate was open, and
49 original offensive actions were guarded with zero unsupported actions. Twelve
samples had an actually checked selected action plus complete target/recipient
identities. Damage and a player-owned status on the dummy corroborated action
delivery; this does not prove every selected action was delivered. Sampled
positions and destinations remained at the starting point. Cleanup cleared the
preset, stopped linked RSR, deleted the temporary preset and reset the verified
dummy. Reset acceptance initially preceded combat exit; a later fresh snapshot
confirmed normal conditions, no target and the unchanged pose.

The twelve capture measurements were 0.406-26.327 ms, median 0.681 ms. The first
sample was the outlier; its cause is unverified. This measures entry capture and
preparation on one nearby dummy, not total callback/frame cost or a representative
large encounter. No optimization quota was introduced.

Two source-verified distinctions corrected the initial wiring:

- `RSCommands_StateSpecialCommand.ApplyState` sets IsManual for both Manual and
  Henched. Native constraints may run in Henched while leaving its flags intact.
  Explicit Manual, TargetOnly, PvP and unknown modes retain control.
- `StateUpdater.StatusFromCmdOrCondition` adds Burst when AutoBurst is enabled,
  even without an explicit special command. `RsrCommandPolicy` separates that bit
  from an explicit Burst and other commands. The same aggregate-status mistake
  was corrected in native tank recovery/stance, raised healing and mitigation
  sequencing. These support/recovery changes still need their pending live tests.

**Unfinished target choice:** `ActionTargetInfo.GetCanTargets` accepts hostile
anchors in manual/Henched mode only when they are the hard target (or self).
Working candidate predicates therefore do not establish better area-anchor
selection in the default linked mode. Complete native candidate/action planning
and validated target setup, preserving the current opportunity and recipients
before any movement commitment. Do not disable IsManual for the entire callback
as a workaround: that also changes native selection and delivery semantics.
Do not turn this into an endpoint refinement or a second correction walk.

## Joint target search/executor phase (isolated, not live)

SHA-256 `a09a931b372052176c8795f6e5c029d7efad5d84e3982a40f188de3f919f5cb0`,
MVID `f76daa22-e256-4fdb-895e-e338d7587edf`. Managed build and 42,502 native
assertions passed, along with all 19 broader suites, nine repository checks,
installed RSR contracts and whitespace verification. This is a later isolated artifact; the live command-policy
checkpoint above is unchanged.

`PlanningAction.TargetChoices` carries immutable same-action opportunities and
captured setup estimates. The context rejects changed shape/effect/range,
removed or weakened required recipients, removed forbidden recipients and
inconsistent actor geometry. Target choice and one-time setup progress are part
of each label and dominance proof. The action witness records the chosen anchor
and every required recipient. Setup uses the same timed traversal implementation;
no second hazard model exists. Choices can be prepared at any supported safe
route point, including after escaping a warning. Acquiring the target is checked
separately from action range, including captured vertical offset.

The executor has a target directive and confirmation wait. Actual hard-target
identity alone is insufficient: the current native action evidence must confirm
the same target. Setup age is not renewed by rereading a result; failure invalidates
that route. Validation uses the actual stopped pose, and estimated handoff remains
an explicit bounded result. The existing marker style labels the setup stage.

A 54-case independent one-dimensional oracle enumerates cast cells, setup cells,
setup delays, GCD readiness and warning exits. It verifies target/timing choice,
including walking out before setup. Direct tests cover complete recipient identity,
changed mechanics/range, dropped required/forbidden recipients, acquisition height,
manual handoff, setup timeout, confirmation and overlay content. Broader existing
native route/resource tests remain in the harness.

The runtime producer and target publisher are **not connected**. Current live
captures therefore do not use these options. The next steps in TODO.md require
read-only original RSR target eligibility, native expected-target ownership,
confirmed action/recipient handoff, original-clock revalidation and live tests.
The synthetic setup durations are test inputs and must not become production
constants. A captured vertical offset is valid only for a supported projection;
unknown future height/eligibility is not a safety or execution proof.

## Runtime target-choice checkpoint, 2026-10-24

SHA-256 `787db153f476ae50b3e3e70c35ee7d63b54f7bb38e57222c1bbec29f067daee1`,
MVID `7f82c646-fe85-434d-b00b-cd76cac9e421`. This supersedes the dormant runtime
status of the prior isolated phase. Managed compilation passed with the same six
upstream warnings. The exact DLL passed 42,515 native assertions, all 19 broader
suites, nine repository tests, installed RSR contracts and whitespace checks.
Installed-contract validation also constructs the read-only eligibility adapter
against actual BaseAction/ActionSetting and boxed ActionTargetInfo types without
invoking game predicates or mutating CanUse.

`NativeOffensiveTargeting.Planning` captures same-action target-circle alternatives
on the framework thread and freezes their geometry before search. Each preserves
original recipient identities, hitbox-adjusted action range, coherent relative
heights and collateral. Newly affected recipients become individually required.
Read-only original status/resistance/TTK/DoT/visibility/native-use/HP/predicate
checks remain binding. RSR top-priority and nameplate icon ordering is an
independent exclusion; more coverage cannot buy a lower-priority primary.

The host applies a Target directive through native hints. NativeTargetSetupOwner
tracks one current handoff, its original deadline and required recipients. Cached
selection cannot confirm it; the original callback must observe the same action,
new anchor and full recipient set after publication. A foreign target releases
ownership. Conditional restoration retains no saved lease or history. Per-frame
native target-policy initialization does not discard pending local ownership;
publication waits until the native automatic-target policy is resolved. Cast,
movement and manual gates remain native. A pending setup does not clear IsManual
or install another movement writer.

Production setup time uses observed original callback intervals/native frame
duration; no synthetic delay or new optimization quota is installed. The action
and exact stopped pose are revalidated immediately before native publication.
Native safety still validates the complete route. Estimated target setup remains
an explicitly bounded result until observed execution corroborates it.

Automatic watcher reload completed at 01:10:17 UTC, with matching loaded MVID.
A twelve-sample BLM dummy check had two precombat/inactive samples followed by ten
samples with 49 guarded actions, zero unsupported actions and complete selected
recipient evidence. Capture median 0.5283 ms, maximum 0.6369 ms is not total frame
time or an AoE-pack benchmark. Action effects and dummy HP loss corroborated
single-target delivery; no player displacement occurred. Cleanup removed the test
preset and verified RSR Off/no target. The reset request was accepted; a later
read found full dummy HP, no statuses and no dummy target. The initial combat-condition flag persisted after the accepted request; a fresh
01:13:10 UTC observation subsequently verified normal conditions, no target and
the original position. Acceptance and actual combat clearance are separate.

Remaining limits: these dummies are too widely spaced to prove better AoE choice
or an actual target handoff. The current producer requires both better-target and
movement-coverage preferences, so independent target-only operation is unfinished.
Directional delivery, current-target/primary damage semantics, reacquisition,
tank recovery, live setup failures/restoration and lifecycle coverage remain open.
Do not infer full three-setting parity from this checkpoint.

## Independent target and movement coverage preferences, 2026-10-24

Current loaded SHA-256
`b9aa305242b64a25f53952f2dab6bd975b1ca808e7746c7607f1038e7ee63e0c`,
MVID `0a31d2f3-76a8-4a9a-b327-cd8d44380baa`. This supersedes the earlier
requirement that both PickBetterAoeTarget and ManageAoePackPositioning be enabled.
The remainder of the previous targeting acceptance limits still applies.

RsrExecutionCapture values extra hostile target-circle recipients when the target
preference is enabled. That circle is anchored to the target, so its hit coverage
does not vary with the player's pose. This does not enable additional caster,
directional, ground or healing coverage. NativeOffensiveTargeting admits a
replacement with movement coverage off only when both original and alternative
are executable from the captured position and the alternative strictly improves
coverage. Each original recipient, forbidden collateral, original action geometry,
priority and fresh original RSR eligibility/confirmation remain binding. There is
no extra movement writer, endpoint refinement or modified rotation mode.

The shared search retains current action timing ahead of extra hits. A target
setup cannot postpone a ready action. A future evidenced action may use its
existing preparation window; polling does not renew it. Disabling better targeting
releases pending local setup and conditional restoration even when acquisition or
tank-target features remain enabled. Equivalent stationary anchors retain the
existing target. The existing Target setup marker and action-recipient witness
already describe this path, so no new overlay primitive or HUD is introduced.

The managed plugin build passed with six existing warnings and zero errors; the
harness built without warnings/errors. The exact DLL passed 42,670 native
assertions, all 19 broader suites, installed RSR 7.5.7.6 contracts, nine repository
checks and whitespace verification. New native cases exercise all four preference
combinations, immediately-ready versus future actions, unchanged useful position,
individually preserved recipients, bounded handoff evidence, equal alternatives,
a farther cluster requiring movement, forbidden collateral and unrelated coverage
opt-outs. The complete route validates against the same immutable input.

Fresh idle/out-of-combat preflight preceded the normal watched write. The loaded
MVID was verified at 02:39:38 UTC. Settings Ready, RSR Off, no failure, no active
preset/target, normal conditions and unchanged player position were read back.
This is idle lifecycle/provenance proof, not actual improved target delivery.
Linked Henched target-only behavior with ManageMovement entirely disabled,
directional damage/primary-secondary value, reacquisition/tank recovery,
representative setup latency, failure/restoration and live pack tests remain open.


## Stationary target permission, 2026-10-24

ManageMovement previously disabled action-context capture before improved circular
anchor choices could reach search. It now controls movement independently of the
existing PickBetterAoeTarget setting. Supported hostile hard-target circles enter
the same native planning service with an immutable StationaryOnly permission.
Movement coverage, mobility, utility, placement and downtime facing do not gain
permission. A replacement must be a strictly better already-executable anchor,
preserve every current recipient and not postpone an already available action.
Original RSR status/predicate and marked priority checks still apply. Henched's
IsManual flag remains intact; explicit Manual and native target instructions keep
control. Actual target publication still requires stopped-pose route validation
and fresh original action/recipient confirmation under the original setup clock.

Normal/follow movement retain ownership. An active preset without a movement
module can submit a stationary request through that same host after module target
policy resolution. There is still one job, publication and executor. Native safety
or goal recovery takes precedence. The stationary fallback cannot apply its
counterfactual recovery walk. Changing the permission invalidates worker inputs;
revalidation rejects a previous walking/mobility/utility commitment. Existing
native target markers/decisions show publication; no new overlay primitive or
separate selection controller was added.

Managed compilation passed with six existing gauge warnings and zero errors.
The exact artifact passed 42,807 native assertions, all 19 broader suites, installed
RSR 7.5.7.6 reflection/eligibility contracts and nine repository checks. New checks
cover every movement-coverage/target-option combination under stationary and
movement permissions, immediate versus future action availability, unchanged
recipients, rejected old walking commitments, immutable permission copying and
production native goal/warning recovery. These checks do not exercise a complete
live rotation callback or prove actual improved pack delivery.

Artifact SHA-256:
`1f65767b8279e61dd4c9d647b2bbba6558d443e705643e8feefa3ce44e582664`;
MVID `94287657-5f86-41d3-8052-08b212f36e7a`. No watched output was written. The
read-only live snapshot at 04:16 UTC still reported the earlier loaded MVID,
RSR Off, no target, normal conditions and the unchanged idle BLM pose. Actual
linked Henched delivery with ManageMovement off, measured setup latency, failure/
restoration, directional/value semantics and lifecycle/pack acceptance remain open.
