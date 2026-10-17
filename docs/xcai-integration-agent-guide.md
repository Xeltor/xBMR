# Using the versioned xBMR integration

Start with [the contract specification](xcai-integration-v1.md) and the
[compile-checked consumer](examples/BossModV1Consumer.cs) and
[directional setting scope](examples/BossModV1DirectionalScope.cs), plus the
[owned strategy example](examples/BossModV1StrategyLease.cs) and
[module lease example](examples/BossModV1ModuleLease.cs). The implementation
tracks [GitHub issue #1](https://github.com/Xeltor/xBMR/issues/1). This is a
working-tree integration, not a published release or a completed XCAI migration.

## Scope of this work

Implementation changes belong to xBMR: provider contracts, native behavior,
regression tests, examples and this handoff. XCAI is a read-only reference for
consumer requirements. Its agents own production migration and removal of its
reflection/frame wrapper. Do not edit XCAI as part of this xBMR task.

The provider must supply and verify the supported replacements needed for that
migration. The ledger distinguishes missing provider capabilities from external
consumer adoption and live gameplay evidence; downstream adoption is not an
instruction to change the consumer repository here.

The final agent handoff must be posted directly in
[GitHub issue #1](https://github.com/Xeltor/xBMR/issues/1). Include the supported
families and exact usage/signatures, copyable examples, lifecycle/safety limits,
validation evidence, provider limitations and the downstream migration matrix.
Repository documentation supports that issue handoff; links alone do not replace
the actionable information in the issue. Keep working-tree/build proof distinct
from a published release and from live consumer/gameplay acceptance.

## Connect and contribute

1. Obtain `BossMod.Integration.V1.Capabilities` as `Func<string>`. Verify JSON
   `schemaVersion == 1` and each required entry in `families` independently.
   Keep its `instanceGeneration`; discard all old state when it changes.
2. Register one named owner through `Integration.V1.Register`, IPC signature
   `Func<string, Action<string>, string>`. Pass schema, instance, a unique name,
   and `leaseMs` in 1–2000. Save the returned opaque `ownerToken`.
3. Prepare immutable scoring closures from your own observations. A scorer is
   `Func<System.Numerics.Vector2,float>` over world `[x,z]` in yalms. Return a
   finite value in 0–16. It can run on a navigation worker; never capture live
   actors, mutable lists, IPC subscribers, UI or game services inside it.
4. In the BMR `hints-ready` callback, renew the owner and submit at most four
   scorers with the exact callback `context`, owner token and category. Use
   `combat` or `party-utility` for action value, `mechanic-preference` for
   nonbinding mechanic preferences, and `presentation` for comfort tie-breaks.
   Native encounter constraints and encounter goals retain priority.
5. Observe `movement-proposed` and `movement-applied` without executing BMR's
   update or movement functions. Applied means the override was assigned;
   physical/input observations have separate semantics. Do not call private
   frame methods or replace `UiBuilder.Draw`.
6. Clear your goals or unregister on disable/unload. Stop using cached contexts
   after their stage/frame changes or 250 ms. BMR retires the previous context
   and latest publication before the next native world update mutates state;
   queries remain unavailable until the next completed hint build. Reconnect
   after provider reload.

All endpoints have the `BossMod.` prefix. Structured requests/responses are JSON
strings; no xBMR assembly reference is required. Query signatures are generally
`Func<string,string>`. They must run synchronously on the BMR callback thread
with the exact context. `Movement.V1.Latest` is a copied observation that can be
read outside callbacks; it does not make a cached context queryable elsewhere.

For a local test, connect the example's constructor to Dalamud subscribers as
shown in the specification. Its `SetScore` accepts an immutable scoring closure;
its proposed/applied events are observation points. The sample demonstrates
transport and stage composition. It does not contain XCAI's complete combat
policy or its remaining native-contract migrations.

## Choose the right observation

| Need | Endpoint/family | Required interpretation |
| --- | --- | --- |
| Direction, deadlines, cast pressure, follow/encounter state | `Movement.V1.GetSnapshot` | Stage-bound; retained navigation can be older than hints; final physical input is not implied |
| Native controller to override to input identity | `controller-input-provenance:1` movement fields | Search, controller, assignment and read have separate identities; retain bounded applied identities to join later input; no route/arrival proof |
| Native hint goal presence, dash restriction and enmity membership | `native-goal-presence:1`, `dash-policy-observation:1`, `target-aggro-observation:1` | Captured native prefix count is independent of scores/enabled state; forbidDashes is a restriction; nullable aggroPlayer differs from targetId; missing evidence stays unknown |
| Target eligibility, tank intent, cast identity | `Hints.V1.GetTargets` | Native policy is authoritative; provenance and missing source/deadline correlations remain explicit |
| Native cast identity, policy and linked point threats | `Hints.V1.GetCastControl` | Exact producer-recorded cast/actor links; nullable native function values; no timestamp-based attribution or negative safety inference |
| Overlapping damage recipients | `Hints.V1.GetPredictedDamage` | Use all entries and the exact same-observation party mapping |
| Current action effects | `World.V1.GetActionEffects` | Per-world-update, bounded observations; duplicates retained; inspect dropped count |
| Shape/containment/clearance | `Arena.V1.GetSnapshot` / `QueryPoints` | Arena and pathfinding window differ; containment/inset is geometry, not a safe route |
| Inherited FATE/default search terrain alignment | `native-default-terrain-frame:1`, `Arena.V1` `obstacleMap.defaultFrameAlignment` | Native center/window correction replaces reflected frame writes for inherited SimpleBossModule methods; nullable current provenance is not physical coverage, route or ingress proof |
| Original navigation witness/identity | `Navigation.V1.GetSnapshot` | Search/build/publication identities are distinct; a fallback is not a verified goal route |
| Immutable raster window | `Navigation.V1.GetLocalMap` | Copied, bounded cells; physical, arena, temporary and timed limits have separate meanings |
| Native detour or nearest safe raster candidate | `Navigation.V1.StartSearch` / `PollSearch` | One running copied-data worker; fresh-context polling; require current route validation and destination satisfaction |
| Complete explicit walk route | `Navigation.V1.EvaluateRoutes` | Only `clear` under its stated model is positive evidence; supply onward escape from future warnings |
| Native generic mechanic entries | `Mechanics.V1.GetSnapshot` | Current generic publications only; missing components/future pulses are never evidence of safety |
| Current native knockback/march projection | `Mechanics.V1.EvaluateMovement` | Actual actor pose only; native `DestinationUnsafe` retained; complete safety/recovery remains unknown |
| Hypothetical native displacement geometry | `Mechanics.V1.ProjectPoses` | Independently require counterfactual-projection-geometry:1; at most four poses; unchanged SimpleKnockbacks providers or native march state; actual actor context retained; geometry never approves independent-hazard/recovery safety |
| Native knockback landing constraint view | `native-knockback-landing-constraints:1`, `Mechanics.V1.EvaluateLandings` | Register before the next hint build; producer-linked single radial knockback preparation transforms retain their landing envelopes; clear current published constraints never proves physical coverage, preparation/recovery reachability or complete safety |
| Temporary configuration | `Control.V1.GetSettings` / `ApplySetting` / `Renew` / `Release` | Allowlisted, expiring compare-and-restore leases; inspect expected value/revision |
| Directional-action angle option | `Control.V1.WithDirectionalAngle` | Synchronous, audited action only; restore in `finally`; does not validate or confirm an action |
| Owned transient strategy | `Strategies.V1.GetSnapshot` / `Apply`, `Control.V1.Renew` / `Release` | Exact active preset identity, track/revision/value; native modifier/user changes win; no saved-list mutation |
| Module identity/state and temporary enablement | `Modules.V1.GetSnapshot`, `GetEnablement` / `ApplyEnablement` | Exact stable identity and expected value/revision; runtime layers never edit the saved list; active native coverage survives temporary-enable expiry; later user intent wins |
| Out-of-combat native input pacing | `Movement.V1.RequestPace` / `ClearPace` | Request only at movement-proposed; exact native decision/witness, at most 100 ms; pending acknowledgement is not applied input; observe matching proof in lastInputRead |
| Existing follow compatibility repair | `Follow.V1.GetSnapshot` / `Stop` | Exact observed revision, `hints-ready`, live owner and explicit repair reason; saved preferences remain intact |

Every consumer must handle both `available:false` observations and
`accepted:false` operations. Unknown, stale, truncated, unsupported, incomplete
or missing data must suppress the dependent optimization. Do not translate it
to an empty encounter or a safe route. Keep using existing supported dash/position
safety APIs for those operations.

## Ownership and failures

Owners expire within two seconds without renewal. Goal submissions are specific
to one frame. Already captured immutable worker scorers retain their original
source deadline, at most 250 ms, across ordinary frame turnover; a new frame
cannot renew or resubmit them. Manual/scope changes, clear, unregister and
disposal invalidate them, including a return from an already running scorer.
Callback/scorer failure revokes that owner and leaves native BMR processing
running. In-process delegates cannot be preempted: scorers and callbacks must
remain short and must never block.

Control leases have their own opaque IDs and resource revisions. Release each
lease explicitly in `finally`/unload and unregister the owner. Native expiry
also sweeps on framework updates, independently of frame-stage delivery. Death,
territory/preset changes, manual intervention, movement pause, owner expiry and
provider disposal revoke controls. Restoration happens only while the lease
still owns the resource; later user choices must win. A failed cleanup remains
pending for a bounded retry and is not silently treated as success.

Never use global legacy `Configuration.ClearTransient` to clear another
consumer's state. Never write back a saved value after losing a lease.

## Where to change the provider

- `BossMod/Framework/IPCProvider.cs`: exact endpoint registrations/signatures.
- `BossMod/Framework/Plugin.cs`: the three BMR-owned stage insertion points.
- `BossMod/Framework/Integration/IntegrationSession.cs`: schema, lifecycle,
  freshness, thread/stage validation and scorer containment.
- `IntegrationCoordinator*.cs`: native observations, input validation and
  independently advertised capabilities.
- `IntegrationGoalSet.cs`: encounter/combat/mechanic/presentation composition.
- `IntegrationCastControl.cs` and `AIHints.CastSources.cs`: current cast identity, native deadline estimates and producer-recorded cast/zone links. The [BCL consumer](examples/BossModV1CastControl.cs) joins exact identities for positive point-threat evidence.
- `IntegrationControllerDecision.cs`, `IntegrationCoordinator.Movement.cs`, and `IntegrationInputObservation.cs`: native outcome/assignment/read provenance; [compiled bounded reader](examples/BossModV1MovementProvenance.cs).
- `IntegrationPacing.cs` and `IntegrationCoordinator.Pacing.cs`: native frame-bound route/deadline authorization and input gate; [compiled consumer](examples/BossModV1AutomaticPace.cs).
- `IntegrationHintEvidence.cs`: pre-integration native goal presence and nullable actor aggro observation; dash policy is copied at the requested movement stage.
- `SimpleBossModule.cs` and `Pathfinding/DefaultTerrainFrame.cs`: inherited default search-frame/terrain alignment; custom frame overrides remain native-owned.
- `AIHints.ProjectionSources.cs` and `IntegrationLandingConstraints.cs`: bounded native collection provenance and read-only landing constraint separation; [compiled BCL reader](examples/BossModV1LandingConstraints.cs).
- `IntegrationMechanics.cs`: bounded generic publications and coverage.
- `IntegrationCounterfactualProjection.cs`: bounded hypothetical native geometry; [compiled BCL reader](examples/BossModV1ProjectionGeometry.cs). Pose-dependent source overrides and complete independent-hazard/recovery safety remain unsupported.
- `IntegrationArena.cs`, `IntegrationRoutes.cs`: geometry and swept timed-route
  queries. An unsupported distance bound must continue to return unknown.
- `IntegrationLeases.cs`, `IntegrationSetting.cs`,
  `BossMod/Config/ConfigRoot.Owned.cs`: owner isolation and nonpersistent runtime
  settings with revision-checked restoration.
- `BossMod/Pathfinding/NavigationPublication.cs`, `NavigationMapSource.cs`,
  `NavigationDecision.cs`: immutable captured input, raster and publication.
- `BossMod/AI/AIBehaviour.cs`, `Autorotation/MiscAI/NormalMovement.cs`: native
  asynchronous navigation workers; apply completed results only in native frame
  processing and retain their original time origin.

## Verify and continue the migration

Build with the pinned SDK and compatible API 15 references. Keep machine-specific
build hosts, toolchain locations and resource policies in private environment
configuration outside this repository. Select the exact built assembly for tests:

```sh
dotnet build tests/IntegrationContractRegression/IntegrationContractRegression.csproj \
  -c Release -p:BossModAssembly="$XBMR_ASSEMBLY"
dotnet tests/IntegrationContractRegression/bin/Release/net10.0/IntegrationContractRegression.dll \
  "$XBMR_ASSEMBLY" "$DALAMUD_HOME"
python -m unittest discover -s tests
```

The delivery ledger and reflected-dependency matrix are the acceptance checklist.
Outstanding work includes production XCAI migration, continuous nearest-safe/
ingress queries, counterfactual mechanic projections with independent hazards
and recovery timing, preset-selection leases, validated movement preferences and
justified native corrections. Do not remove a safety fallback solely because a
related family is advertised. Continuous native stage delivery, once-per-frame
execution, owner cleanup/manual handoff and native movement authority need
consumer acceptance. Build, contract, replay, reload and live gameplay remain
separate proof levels.
