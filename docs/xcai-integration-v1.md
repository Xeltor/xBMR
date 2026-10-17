# XCAI integration contracts (issue #1)

Start with the [agent usage guide](xcai-integration-agent-guide.md) for connection steps,
source locations and migration boundaries.

Tracking: <https://github.com/Xeltor/xBMR/issues/1>. This document describes the
working-tree implementation, not a published release. The full issue remains
open until its contracts, migration, and acceptance requirements are addressed.

Task ownership: implement and validate the xBMR provider, native corrections,
tests, examples and migration handoff in this repository. XCAI is a read-only
consumer reference. Production XCAI changes and reflection removal belong to
its agents; migration entries below track downstream adoption separately from
the xBMR implementation deliverable.

## Delivery ledger

| Requirement | Current implementation | Remaining acceptance |
| --- | --- | --- |
| 1. Capabilities, stages, goals | Versioned discovery, expiring owners, three BMR-owned stages, scoring surfaces, native score queries, priority composition | Exercise through a real IPC consumer; migrate XCAI's draw wrapper and goal adapters; performance validation |
| 2. Coherent movement | Stage-bound observation; native NormalMovement/follow controller outcomes; distinct search, controller, native override-assignment and input-read identities, including interaction/pause suppression | XCAI adoption, later-hook visibility and representative live correction/input acceptance; provenance is not route safety |
| 3. Navigation/raster/timed routes | Copied raster windows, raw warning limits, native fallback classification, complete current walk-route validation; bounded asynchronous native detour/nearest-safe-raster candidates | Continuous nearest-safe/ingress semantics, more distance/projection support, production migration and live route acceptance |
| 4. Rich arena | Logical containment, signed clearance, boundary and inset candidates; circle/rotated rectangle/custom contours and holes; explicit current projection layer | Route-validated ingress, complete obstacle provenance/generation and consumer migration |
| 5. Targets | Bounded policy/geometry/cast observation, native cast identities/deadline estimates and producer-recorded zone links, captured hint-producer transform provenance, nullable explicit target requirement | More detailed authored/default semantics, additional cast-linked producers and migration |
| 6. Mechanics/projection | Bounded native generic publications, explicit coverage/later-pulse gaps, actual-pose native knockback/march observation preserving DestinationUnsafe, filtered source identity and activation | Counterfactual/independent-hazard/recovery safety, richer applicability and migration |
| 7. Damage/effects | Full bounded damage list with same-observation party mapping/world time, copied recipient identities and explicit resolution completeness; bounded current-update action effects | Consumer migration and representative live overlap/party-change acceptance |
| 8. Ownership controls/native corrections | Allowlisted settings, transient-strategy and module-enablement leases, bounded native automatic-input pacing, synchronous directional scopes, conditional native follow stop; module catalog and native eligibility/active retention | Preset-selection leases, validated movement preferences, additional constraints and other justified native corrections; consumer/live module-toggle acceptance |

No complete reflection-removal or live gameplay acceptance claim is made by this
ledger. Unsupported fields remain null or explicitly unknown. In particular a
controller destination is not proof of a fresh executable navigation route.

## Transport and discovery

All names below have the `BossMod.` prefix. Existing endpoints keep their current
signatures and semantics. New structured payloads are camelCase JSON strings;
callbacks and scoring delegates use only BCL types. No consumer needs an xBMR
assembly reference, WPos adapter, pointer, reflected manager, or mutable map.

`Integration.V1.Capabilities: Func<string>` returns `schemaVersion`, a random
`instanceGeneration` unique to this loaded provider, independently versioned
`families`, `limits`, and an explicit `unsupported` list. A consumer must check
the family it needs instead of inferring support from a plugin release number.
Currently the advertised families are `frame-stages`, `goal-scoring`,
`movement-observation`, `controller-input-provenance`, `automatic-input-pacing`, `target-policy`, `cast-control-observation`, `predicted-damage`, `action-effects`,
`arena-geometry`, `navigation-publication`, `navigation-raster`, and
`walk-route-validation`, `native-route-search`, `nearest-safe-raster-search`,
`mechanic-observation`, `current-native-projection`,
`configuration-leases`, `transient-strategy-leases`, `module-catalog`, `module-enablement-leases`,
`directional-angle-scopes`, and `follow-control`, each
version 1.
The target and movement observations have the gaps listed above.

Every observation contains a context:

```json
{"schemaVersion":1,"instanceGeneration":"<from capabilities>","frameId":42,"stage":"hints-ready"}
```

The frame ID is a BMR integration frame, not the game's input tick. Query endpoints
accept that exact JSON context. A different instance, frame, stage, unsupported
schema, wrong calling thread, or observation older than 250 ms yields
`available:false`; it must never be interpreted as a safe or empty encounter.
Malformed and oversized requests are rejected. Requests are capped at 8,192
characters and native score batches at 128 finite `[x,z]` pairs.

World coordinates are FFXIV yalms. Horizontal vectors use `[x,z]` (`Vector2.X/Y`
in scoring delegates); 3D directions use `[x,y,z]`. Rotations are radians with
zero facing south, positive pi/2 east. Actor IDs and slot masks are hexadecimal
strings, not names or account/content IDs. Actor identity is scoped to the
published world instance and must not be retained across actor destruction.

`worldTime.ticks` and activation ticks are decimal strings of .NET DateTime ticks
to avoid JavaScript integer precision loss. Their kind is provided; subtract
times in the same world clock and publication. World time can jump during
playback/territory changes and is not a server snapshot clock. UTC observation
timestamps are diagnostic wall-clock timestamps. Owner/frame expiry uses a
separate monotonic clock and is not extended by a world-time rewind.

## Owner lifecycle

| Endpoint | IPC signature | Request/result |
| --- | --- | --- |
| `Integration.V1.Register` | `Func<string, Action<string>, string>` | Registration JSON and callback; result includes an opaque owner token |
| `Integration.V1.Renew` | `Func<string, string>` | Lease JSON; accepted/rejected result |
| `Integration.V1.Unregister` | `Func<string, string>` | Lease JSON; revokes this owner and retained scoring delegates |
| `Goals.V1.Clear` | `Func<string, string>` | Lease JSON; clears only this owner's goals, including captured delegates |
| `Goals.V1.Submit` | `Func<string, Func<Vector2,float>, string>` | Goal request plus immutable scoring delegate |

Registration request:

```json
{"schemaVersion":1,"instanceGeneration":"<generation>","name":"XelsCombatAI","leaseMs":1000}
```

Lease request:

```json
{"schemaVersion":1,"instanceGeneration":"<generation>","ownerToken":"<token>","leaseMs":1000}
```

`leaseMs` is only used for renewal. At most eight uniquely named owners may be
registered. Lease duration is 1–2,000 ms. An expired lease cannot be revived;
register again. Explicitly unregister on consumer disposal and discard tokens
when BMR unloads or its instance generation changes. If a client disappears
without unregistering, expiry revokes its callbacks/scorers. This is cooperative
lifetime management, not an authentication boundary between trusted plugins.
Registration, renewal, clear and unregister are synchronized lifecycle operations
and may be called from a consumer's lifecycle/update thread. Gameplay queries and
goal submissions must run synchronously on the BMR frame callback thread. That
thread is bound when BMR first runs the frame, rather than assumed from plugin
initialization.

## BMR-owned frame stages

The callback receives one movement snapshot JSON per stage, with the same frame
identity and stage-specific context. It does not update BMR or execute a frame.

1. `hints-ready`: after world/party/module updates and encounter/zone hint
   generation; before manual-action queue gathering, rotation, follow AI,
   broadcast, and final action gathering. Submit this frame's goals here.
2. `movement-proposed`: after rotation/follow/action gathering and normal window
   drawing, immediately before `ExecuteHints`. This is the final proposed
   automatic movement at that boundary. Observe it for synchronized decisions;
   this stage has no raw movement setter and cannot rerun native processing.
3. `movement-applied`: immediately after `ExecuteHints`. The reported override
   direction is what BMR assigned, including suppression for an interaction.
   It is not proof that the game moved. Last-input-read user/actual directions
   carry their own input generation/time and cannot be relabeled as this frame's
   fresh physical movement.

Exactly one dispatch per stage is permitted. Recursive dispatch is rejected. An
owner's callback exception revokes that owner; remaining owners and native BMR
continue. Callbacks must be short, nonblocking and must not retain game objects.
Neither scoring nor stage callbacks are a sandbox: a trusted in-process delegate
that blocks indefinitely cannot be preempted safely.

## Goal composition

Submission JSON:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"<generation>","frameId":42,"stage":"hints-ready"},"ownerToken":"<token>","category":"combat"}
```

Each owner may submit four scoring surfaces per frame. Allowed categories are
`combat`, `party-utility`, `mechanic-preference`, and `presentation`. Return a
finite score in `[0,16]` from an immutable, thread-safe, allocation-light
`Func<System.Numerics.Vector2,float>`. The function may run concurrently on
native navigation workers; it must close over a copied publication, not current
game state or a mutable consumer controller. Exceptions, invalid scores or an
observed call over 2 ms revoke the owner. A captured scorer is capped at 262,144
evaluations and expires within 250 ms of its source frame or earlier on owner
expiry/clear/unregister/unload/context change/manual handoff.

The four-corner rasterizer evaluates these tiers in order:

1. Native encounter/zone goals captured before integration callbacks.
2. Remaining native rotation/follow goals plus `combat` and `party-utility`
   contributions (additive, same score units).
3. `mechanic-preference` contributions.
4. `presentation` contributions.

Candidate cells receive lexicographic ranks. Lower tiers break exact ties in the
higher tier; an arbitrary number of comfort points cannot overpower an authored
assignment or higher combat value. Ranks preserve ordering, not the numeric
magnitude of the native score. Physical blocks and timed hazards are unchanged;
native pathfinding still chooses the route. `GoalZonesEnabled`, native casting
and imminent-danger rasterization gates remain authoritative. More than 262,144
cells or a native producer replacing the encounter goal prefix falls back to
the original native goals. This does not introduce cast holds, delay movement,
change forced movement, or bypass manual input.

`Goals.V1.EvaluateNative: Func<string,string>` accepts
`{"context":{...},"points":[[x,z],...]}` and returns native scores, enabled state
and publication scope. At `hints-ready` it includes only the encounter/zone
goals already built; after rotation it includes all current native goals.
Contributed goals never enter this list. For final navigation composition the
native rotation/follow goals are captured at the actual navigation scheduling
point, rather than claiming they existed before rotation ran.

## Read-only observations

`Movement.V1.GetSnapshot`, `Hints.V1.GetTargets`, and
`Hints.V1.GetPredictedDamage` are `Func<string,string>` accepting the context.
Null means unavailable/unknown, never infinity or an accidental safe result.
`Movement.V1.Latest: Func<string>` returns the last immutable stage JSON without
reading game managers on the caller's thread. It uses the original frame's
monotonic freshness deadline; beginning a newer frame cannot refresh an older
copy. It is suitable for passive discovery/diagnostics. A later query still has
to satisfy the exact context and callback-thread requirements.

The movement snapshot separates encounter-authored forced direction, proposed
effective direction, and assigned override direction. It includes manual input,
cast pressure, hold/cancel reasons, preset/follow state, interaction target,
selected module versus active phase, zone module, and hint counts. Navigation
generation, source hint generation, build/publication time and source-search
travel estimate are reported when a native result exists for this instance.
These identify the retained search result; they do not claim that native
range/cast/follow adjustments applied precisely that route. Unknown physical
input observations have their own generation, time and assignment correlation as described below.

`lastInputRead` is null before a native walk input result has been observed.
Subsequently it copies `generation`, `observedUtc`, monotonic `ageSeconds`,
`kind`, user/actual/output `[x,z]` vectors, follow-path and misdirection flags.
`automatic` reports that native walk-input code produced an automatic direction;
it does not describe physical movement. `matchingAssignment` carries the exact
native assignment's identity only when captured at the beginning of that input
read, still current at observation, and direction-matched. A same-value
reassignment has a new identity and cannot relabel the read. A newer assignment
cannot rewrite an old sample. This is native provider provenance, not visibility
into later third-party hooks or confirmation that the character moved.
Spinning early returns and flying do not fabricate new walk samples; inspect age
and kind. Existing scalar movement endpoints preserve their semantics.

### Controller, override and input provenance

Require `controller-input-provenance:1` independently. This adds fields to the
existing movement snapshot; query signatures and stage/thread rules are unchanged.

`controllerDecision` is null without a current native outcome. Otherwise it
contains `id`, provider instance/frame, actor identity, world time, monotonic age,
`owner` (`bmr-normal-movement` or `bmr-follow`), `origin`, `destination`,
`direction`, `wroteMovement`, and `reason`. Controller coordinates/directions are
3D `[x,y,z]`. A native no-output/cast/manual/delay result remains distinct from
arrival and from a writer that supplied an identical direction. Reasons describe
native branch outcomes, rather than measured server timing.

NormalMovement observes after native range/cast/misdirection corrections and
records early holds, delay, arrival, spin, explicit destination and the existing
unvalidated ingress branch separately. Follow observes after its live cast/manual
correction; pre-existing authored/other movement retains priority. A suppressed
follow outcome has `wroteMovement:false` and cannot claim the other writer's output.
No controller routing or movement decision is changed by this observation.

`navigationGeneration`, `navigationFrameId`, `navigationInstance`, and
`navigationDestination` describe its optional source search. The latter is a
planar `[x,z]` waypoint, without an invented height. Source search may be absent,
older, or different from the corrected destination. `matchesCurrentHints` and
`matchesEffectiveMovement` compare current values; `routeSafetyVerified` remains
false. This data cannot authorize following the source witness after correction.
Use a fresh complete route query for route safety.

At `movement-applied`, `overrideAssignment` reports provider instance/frame,
monotonically increasing per-instance `generation`, assigned direction, `reason`,
and nullable `controllerDecisionId`, `controllerOwner`, and
`controllerNavigationGeneration`. BMR stamps its actual native writes, including
`native-hints`, `native-movement-pause` and `native-interaction-suppression`.
Controller source is attached only when the same-frame actor/output matches a
writing controller. A changed or missing write at the application boundary is
`unattributed-application-boundary`, without borrowed controller provenance.

An input read can occur after the applied callback. Retain a bounded set of
applied identities and join `lastInputRead.matchingAssignment` by provider,
assignment generation, assignment frame and controller ID/owner. Its input
generation and age belong to the actual read; never substitute the current
callback's frame. Pacing also requires the exact armed assignment object,
including across same-frame same-direction reassignments. Manual/special input
and later hooks are separate from the assignment chain.

The [compiled BCL reader](examples/BossModV1MovementProvenance.cs) remembers at
most eight assignments, clears on provider reload, rejects stale/manual or
unmatched input, and deduplicates observed input generations:

```csharp
var provenance = new IntegrationExample.BossModV1MovementProvenance();
// After independently requiring the family, feed every native stage snapshot:
var observed = provenance.Observe(snapshotJson.RootElement);
// Non-null means a correlated native automatic walk-input sample.
// It does not approve a route, prove displacement, or authorize an action.
```

### Target observations

The target observation is bounded by the 100-target native table. It correlates
position/rotation/hitbox, priority/eligibility, pending death, DOT policy, tank
intent, desired transform, aggro target, FATE ID, interrupt/stun requests and
current cast action/target/remaining time by actor ID. Desired transform
provenance identifies a value captured at the hint-generation boundary as
`encounter-hints`, `zone-hints` or `automatic-hints`; absence is `none`.
A transform changed after that boundary is `post-hints-producer-or-unknown`.
This is producer-boundary evidence, not proof of why an individual component
chose that transform. `targetRequirement` is null until a native producer sets
`ShouldBeTargeted`; true/false preserve an explicit requirement/no-requirement.
False alone does not mean forbidden: priority/eligibility remain authoritative.
Cast remaining time is not a proven server interrupt deadline.

### Native cast identity and linked point observations

The independent `cast-control-observation:1` family provides
`Hints.V1.GetCastControl: Func<string,string>`:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"<provider>","frameId":42,"stage":"hints-ready"},"points":[[100,100],[105,100]]}
```

Call synchronously on the current native callback thread with the exact context.
Omit `points` to inspect the current player's point. An explicit batch has 1–16
finite `[x,z]` world positions. Missing player, stale/incompatible context, invalid
coordinates or unsupported batches return `available:false`. This is a read-only
observation, permitted at each of the three current frame stages.

`targets` contains at most 100 entries with `actorId`, native `priority`,
`pendingDead`, `isTargetable`, `interruptRequested`, `stunRequested` and nullable `cast`. Its cast fields are:
`castId`, `actionId`, `actionType`, `targetId`, `interruptible`, `eventHappened`,
`remainingSeconds`, `reportedRemainingSeconds`, `deadlineTicks`, `deadlineKind`.
`Hints.V1.GetTargets` uses the same cast object/schema, retaining its previous
fields. Opaque `castId` identifies the actual native cast object's lifetime;
restarting the same action/target at the same estimated time produces a new ID.
Discard identities on provider generation change. An ID is not a server sequence.

`worldTime` supplies this publication's `ticks`/`kind`. `deadlineTicks` estimates
`worldTime + native NPCRemainingTime`, including native `NPCFinishDelay`.
`reportedRemainingSeconds` retains the unadjusted game-reported remainder.
Invalid/nonfinite or overflowing timing has a null deadline. These estimates
are not server snapshot, last-safe action or interrupt cutoffs; do not add the
NPC delay again, refresh cached deadlines, or assume enough execution margin.
Native interruptibility and request flags do not establish action range,
readiness, stun immunity or permission to remove the associated hazard.

`zones` enumerates at most 128 current native forbidden zones. Each entry has
frame-local `index`, nullable `sourceActorId`, nullable `castId`/`actionId`,
`correlation`, nullable `activationTicks` and one nullable `distances` value per
requested point. The values come from the actual clipped native shape function:
`<= 0` means point containment, positive means outside that function, and null
means failed/nonfinite/uncomputed. Some native functions are binary or projected;
their magnitudes are not general Euclidean clearance, footprint or route safety.
Zone activation and cast deadline are separate observations, never inferred
from one another.

`correlation` is one of:

- `producer-recorded-cast`: the native producer retained its actual cast source;
  join **both** cast and actor identities to the current target's cast.
- `source-actor-only`: a native source actor is known but no exact cast link was
  recorded. An identical timestamp or a single casting enemy cannot upgrade it.
- `unknown`: source/cast attribution is missing or ambiguous.

Native automatic AoE producers and the four `SimpleAOEs`/charge/group cast-start
paths now record these links. Other generic publications preserve an available
`ActorID` as source-actor evidence but do not invent a cast. Shape/time/source
ambiguity, mutated cast action/target and provenance-table overflow cannot
produce an exact link. A retained old constraint keeps its old cast ID when the
caster cancels or starts another cast. `AIHints.Clear` removes the provenance
table. Geometry, activation, layer clipping and native avoidance remain intact.

`complete` means the current bounded target/zone enumeration and metadata fit;
it is false for overflow or work exhaustion. `pointQueriesComplete` additionally
requires finite results for every returned point/zone. There are at most 128
provenance records, 2,048 native point evaluations and 16,384 provenance tuple
comparisons, with a cooperative 4 ms query budget. A native shape call cannot be
preempted. No map rebuild or worker wait is involved. Coverage of all threats
from a cast or encounter is **not** established: `castThreatCoverageComplete`
remains false even for an empty or completely enumerated result.

The compilechecked [consumer](examples/BossModV1CastControl.cs) returns positive
published threat evidence only when native policy, current cast/actor/action
identity, point containment and event/deadline observations agree. It rejects
incomplete, unknown or mismatched evidence and never turns an empty result into
safety or action permission. Example wiring:

```csharp
var castControl = new IntegrationExample.BossModV1CastControl(
    pi.GetIpcSubscriber<string, string>("BossMod.Hints.V1.GetCastControl").InvokeFunc);
// Inside a current native callback, after discovery of cast-control-observation:1:
var observation = castControl.Read(context, new System.Numerics.Vector2(x, z));
// observation.PublishedThreats is positive evidence; check action legality separately.
// observation.ThreatCoverageComplete is false: absence never proves no threat.
```

Damage predictions are capped at 128 with `complete:false` when truncated. Each
has `activationTicks`, `secondsFromWorldTime`, `type`, `recipientSlotMask`, copied
`recipientActorIds` and `recipientsComplete`. The exact same observation supplies
all 64 native party/alliance/NPC slots and world time. Each slot has `slot`,
`actorId` (null when unknown) and `actorLoaded`; a known party instance ID remains
available when its actor is absent from the local object table. Unknown masked
slots make that prediction's `recipientsComplete:false`, independently of list
truncation. Consumers needing complete overlapping coverage must fail closed on
either kind of incompleteness, and must not resolve masks against an independent
or later party list. Missing local player returns unavailable. Contract fixtures
cover overlaps, slot 63, absent actors, unresolved identities, changed party
slots and immutability across later native list/party updates.

`World.V1.GetActionEffects: Func<string,string>` accepts the context and returns
`publication.complete`, `publication.dropped`, and up to 64 `publication.effects`.
Each effect has a provider-local monotonic `observationSequence`, world frame,
world time/kind, observation UTC, actor/action/action-type/main-target identity,
ground position, native global sequence and native source sequence. Main target
is not an inferred affected-recipient list. Native duplicates are retained in
observation order; consumers can correlate using the supplied sequences without
assuming uniqueness of a timestamp. The list clears before the next world
update, does not retain a combat history, and unsubscribes on disposal. Missed
polls are not recovered. Instance generation scopes sequence continuity across
reload; registration/unregistration does not replay previous action events.

## Arena geometry

`Arena.V1.GetSnapshot: Func<string,string>` takes a context. It reports the
selected encounter's logical arena and current AIHints pathfinding region as
separate objects. `encounterOwnsPositioning` distinguishes an inactive nearby
module from an active phase. Open-world obstacle windows and FATE regions are not
relabelled as encounter geometry. Obstacle presence is provided with explicitly
unknown generation/asset identity until navigation publication is implemented.

Descriptors contain circle radius, rectangular half dimensions/rotation, or
custom polygon parts with exterior and hole contours. Custom contours are
arena-local `[x,z]` coordinates, translated by the supplied center. A layered
arena requires a valid current projection layer; missing/invalid layers do not
fall back to a circle or global union. Shared native 2D projection groups use
their combined authored domain. Invalid, unknown or oversized geometry returns
an unavailable descriptor. Geometry is capped at 64 parts and 4,096 vertices.

`Arena.V1.QueryPoints: Func<string,string>` accepts:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"<generation>","frameId":42,"stage":"hints-ready"},"region":"encounter","points":[[100,100]],"radius":0.5,"projectionLayer":null}
```

`region` is `encounter` or `pathfinding`; an inactive module cannot supply an
active encounter query. The requested projection layer must match the published
current layer. Each result provides containment, signed clearance (positive
inside), closest boundary, and an optional verified inset candidate. Circles
and rectangles are analytical; custom geometry uses native containment and
closest contour queries, including hole boundaries. Radii are 0–5 yalms; bounded
inset searches may return no candidate. An inset candidate is not guaranteed to
be globally nearest. It has `reachability:"not-evaluated"` and the envelope has
`geometryOnly:true`: no hazard, obstacle, travel, mechanic-projection or route
safety verdict is implied. No raw facing or movement setter is exposed.

## Native navigation publications and walk routes

Navigation workers capture player position/cast state, map bounds/center/layer,
the enabled obstacle window, goal delegates, temporary shapes, warnings and
teleporters before scheduling. The obstacle window is copied. The bounds follow
BMR's existing contract: geometry changes create a new ArenaBounds instance.
Native signed-distance objects remain BMR-owned worker-safe values; arbitrary
consumer objects are never accepted as authored constraints.

Each scheduled search gets a new generation with actor, territory, content and
source hint identity, source world time and monotonic capture timestamp. Worker
completion and result publication are separate times. Re-observing a retained
result does not refresh either identity or time. Normal movement uses separate
working/completed contexts so `GreedAutomatic` no longer reads the map being
rewritten by its next worker. Follow results retain their explicit async owner.

`Navigation.V1.GetSnapshot: Func<string,string>` accepts
`{"context":{...},"generation":123}`. It reports source/current hint identity,
capture/build/publication times, source actor/zone/content, origin, native first
and next waypoint, full native endpoint and a bounded witness route, native
score/goal status, leeway/travel estimates and map/projection/teleporter status.
The first waypoint is not relabelled as the whole route's endpoint. A missing or
truncated witness is explicit. Native casts, range adjustments and follow control
can alter final movement after the search: `currentRouteSafetyVerified:false`
is intentional. A source older than 250 ms, unpublished result, different
instance/actor/territory/content, mismatched search generation or invalid stage
is unavailable.

Full copied rasters and witness arrays are produced only while a supported
integration owner is registered and unexpired. With no subscriber, native
navigation still runs and retains its lightweight source/deadline identity,
without allocating diagnostic arrays for each search. After registration,
wait for a newly built publication with `rasterAvailable:true`; a retained
result from before subscription cannot be upgraded or have its age refreshed.
`nativeEndpoint` is null when no complete witness was captured. Owner expiry or
unregister stops new detailed copies. This is an allocation policy, not a change
to authored hazards, goal scoring, native movement or cast permission.

`Navigation.V1.GetLocalMap: Func<string,string>` additionally accepts `left`,
`top`, `width`, and `height` for at most 4,096 cells from a copied raster of at
most 262,144 cells. Arrays are row-major and include cell block codes, nullable
warning limits and goal priority. Codes are `0` unblocked, `1` arena boundary,
`2` source physical obstacle, and `3` source temporary obstacle. Invalid cells
make the requested window unavailable. Goal scores/ranks are not safety data.
Grid coordinates use the native integer-half origin convention: subtract the
published center, rotate into local axes, divide by resolution, then add
`(width >> 1, height >> 1)`; cell centers add 0.5. The published map center
already includes any native bias for an odd grid dimension.

Warning limits are seconds from the supplied source world-time origin, with
BMR's one-second activation cushion already removed. Null on a valid unblocked
cell means no finite published limit. Native pathfinding sometimes clears the
least dangerous cells when everything is covered; the exported limits are copied
**before that relaxation**, and `nativeRelaxedWarnings` records it. Neither those
limits nor native `SafeMaxPrio` prove that a full current route is safe. Teleporter
presence is explicit; these walk queries do not substitute for teleporter or dash
evaluation.

`Navigation.V1.EvaluateRoutes: Func<string,string>` takes:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"<generation>","frameId":42,"stage":"hints-ready"},"generation":123,"routes":[[[90,100],[100,100],[110,100]]],"radius":0.5,"speed":6,"departureDelaySeconds":0,"additionalTimeMarginSeconds":0.25}
```

Supply the complete executable route, including an onward escape from any
temporary destination in a warning. At most eight routes, 64 points per route,
256 yalms and 120 seconds including departure delay per route, 128 current
constraints and a shared 8,192-evaluation / 4 ms
query budget are supported. Radius defaults to the player's current hitbox and
cannot understate it; speed defaults to the current native movement speed and
cannot exceed it. A native cast departure that is not authorized is rejected.
The request does not execute movement, interrupt a cast or change a setting.

The query pins the source raster geometry and rechecks **current** physical
obstacles, temporary obstacles and warnings. Old physical/temporary blocks are
not assumed permanent, and newly published hazards are not omitted just because
the retained search predates them. Missing required physical-map coverage,
changed bounds/center/layer, manual movement, authored forced movement, special
modes, teleporters, invalid input or unsupported constraint functions are
unavailable/unknown. Copied raster snapshots alone remain source-time diagnostics.

For supported native constraint functions, a conservative Lipschitz bound proves
whole swept intervals outside hazards; endpoint-only sampling is insufficient.
Each possible occupancy interval must finish before activation minus the native
cushion and additional margin. A 0.05-yalm footprint margin is also applied.
The terminal footprint must be outside every current warning; an activation is
never interpreted as an expiry. The current supported functions are circles,
inverted circles, donuts, inverted donuts, half-planes, rectangles, inverted
rectangles, capsules, inverted capsules, cones and inverted cones. Non-unit
directions are included in their bounds. Binary, approximate polygon, union and
mechanic-projection functions retain unknown bounds until separately supported.

Results are `clear`, `blocked`, or `unknown`, with reason, first conservative
rejection witness/segment where available, native actor/constraint attribution,
and travel/arrival estimates for clear routes. `clear` proves the supplied walk
model against the current published constraints; it does not approve an action,
facing change, mobility skill, control takeover or unrecorded game collision.
Live server snapshot/latency and actual route execution remain separate proof.
Native goal/assignment composition also retains final authority: spatial route
clearance is not permission to discard an authored goal or assignment. Current
geometry is rechecked under BMR's bounds-instance immutability contract; a module
that mutates cached bounds geometry in place would violate that native contract
and needs its own correctness repair.

## Bounded asynchronous native route searches

`native-route-search` and `nearest-safe-raster-search` are independently
advertised version 1 families. Their queries use native ThetaStar on a copy of
the immutable source raster; they do not change BMR's selected route or wait on
native movement workers. They produce candidate walk routes and then use the
same current swept/timed validator as `EvaluateRoutes`.

`Navigation.V1.StartSearch: Func<string,string>` accepts:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"provider","frameId":42,"stage":"hints-ready"},"ownerToken":"owner","generation":12,"mode":"destination","destination":[100,100],"radius":0.5,"speed":6,"departureDelaySeconds":0,"additionalTimeMarginSeconds":0.25,"destinationTolerance":1}
```

`generation` must identify the currently queryable native publication. Omit
radius/speed to use the actual player's footprint/speed. A radius smaller than
the real hitbox or a faster speed is rejected. Destination distance is limited
to 256 yalms; tolerance is 0.25–3 yalms. `mode:"nearest-safe-raster"` omits
`destination` and scores native safe candidates by proximity to the current
player. It does not claim a continuous-space nearest safe point or global
optimality. Cast-departure permission is rechecked before a result is validated.

At most four retained jobs, one job per owner, and one running worker are
allowed. Search rasters are limited to 16,384 cells. Work is finite native
raster/search work on a background thread, not a four-millisecond framework
wait. Source warning time limits retain their original origin and account for
elapsed time and the requested departure delay. No teleport, flight, special
mode, authored forced movement or manual/pause override is supported. Unknown
physical coverage/distance semantics remain unknown at final validation.

A started request returns `{accepted:true,searchId,status:"pending",...}`.
Poll `Navigation.V1.PollSearch: Func<string,string>` with
`{context,ownerToken,searchId}` using a fresh native callback context. Polling
checks completion without waiting. It may return `available:true,status:"pending"`.
A completed result exposes origin, endpoint, witness, native search score/time,
source navigation/hint generation, current validated hint generation,
`destinationSatisfied`, model and the full route validator result. Its `status`
is the validator's `clear`/`blocked`/`unknown`, never merely the worker's score.
For destination mode require both `clear` and `destinationSatisfied`; a safe
fallback near some other point is not a successful destination route.

The witness must be complete and fit the 64-point current-route validator. The
source's 250 ms lifetime is never refreshed by starting or polling a job.
Changed owner/scope, player movement more than 0.1 yalm from the query origin,
map geometry/layer, actual footprint/speed, manual/cast state or stale source
invalidates the result. New current hazards/physical constraints are checked
before returning clear. Expired jobs are pruned on subsequent requests and
discarded at unload; any already running worker can finish using copied data
but cannot apply movement or affect native state. A completed result is still
read-only translational evidence, not authorization for actions, facing,
immunity, mechanic assignments or final movement.

## Mechanic publications and native current projections

`Mechanics.V1.GetSnapshot: Func<string,string>` accepts the exact context and
requires an active native module and party player. Its `entries` cover native
`GenericAOEs`, `GenericStackSpread`, `GenericBaitAway`, `GenericChasingAOEs`,
`GenericKnockback` and `GenericForcedMarch` publications. At most 64 components
and 128 total entries are exposed. An 8192-step/4 ms cooperative work budget
truncates with incomplete coverage; native component calls cannot be preempted.
An opaque `componentId` identifies that
native component object's lifetime within the provider instance; it is not an
encounter-global sequence or an identity to retain across reload/reset.

Every publication supplies `publishedEntriesComplete`, `encounterForecastComplete`
and `futurePulsesComplete`. The latter two are always false for this initial
family. Complete means all entries from the inspected generic publications fit,
not that the encounter or future sequence is fully modeled. Unknown components
are listed in `components` with `genericPublicationSupported:false`. Validity
ends with the source frame/context, at most 250 ms. `futurePulseCount:null` must
never become zero or an invented later pattern.

Each entry has a `kind` and `componentId`. AoEs expose origin/rotation,
activation, native source actor where present, risk, layer/restriction, display
shape, published pulse count/index, and whether a different native distance
constraint overrides the display shape. Stack/spread entries include actor
target, radius, activation, size/exclusion/dead-target/hints policy. Excluded
actor IDs use this same publication's party mapping, including dead actors.
Baits retain source/target/offset/rotation, native shape and exclusion policies;
chasers retain previous/next position, remaining count and next pulse/interval
but do not promise future positions. Knockbacks expose native ordered sources,
directions/kinds/distance/minimum distance, shape/layers, wall behavior, immunity
at activation and up to 32 safe walls with a completeness flag. March entries
include the actor's forced end, speed, direction/wall policy, and at most 32
pending moves with `pendingComplete`. Applicability must respect the native
layer, immune, target/hint and shape policies; a published source alone is not
proof it affects the local player.

Shapes are `{status,kind,parameters,directionOffset,inverted}`. Parameters are
ordered: circle `[radius]`, donut `[inner,outer]`, cone `[radius,halfAngle]`,
donut-sector `[inner,outer,halfAngle]`, rectangle
`[lengthFront,halfWidth,lengthBack]`, cross `[length,halfWidth]`, capsule
`[radius,length]`. Angles are radians. Apply the direction offset to the entry's
rotation. Unrecognized/invalid geometry has `status:"unknown"`; it is never
approximated by a circle or treated as empty. Display shapes do not replace
native shape-distance overrides or complete timed safety checks.

`Mechanics.V1.EvaluateMovement: Func<string,string>` accepts
`{context,componentId,position:[x,z],facing}`. This initial independently
advertised `current-native-projection` supports only the actual local actor's
exact current pose and bounded native knockback/march components. Counterfactual
poses are unavailable. It calls native movement calculation and each component's
own `DestinationUnsafe`, retaining their order and immunity/wall behavior.
Each movement carries `activationTicks`, `endTicks`, `sourceIndex`, and native
layer/restriction. These come from the same native calculation that produces the
geometry, after immunity/shape/layer filtering; they are never zipped against an
unfiltered source list. Knockback `sourceIndex` refers to the published native
source sequence and has unknown `endTicks` (no displacement duration is claimed).
Pending march entries retain their pending index, activation and native duration
end. An already active march is a remaining segment from current world time to
`ForcedEnd`, with null `sourceIndex`. Default/unscheduled times are null. Native
activation is model timing, not proof of the server snapshot or last-safe move.
`status` is always `unknown`: independent hazards, recovery reachability and
complete safety have not been evaluated. Even an empty movement list or false
`nativeDestinationUnsafe` cannot approve a mobility action or immunity choice.

## Owner-safe settings and follow controls

| Endpoint | IPC signature | Payload |
| --- | --- | --- |
| `Control.V1.GetSettings` | `Func<string,string>` | Exact context; returns discovered setting keys, allowlisted values, duration/synchronous policy, current value/revision and leased flag |
| `Control.V1.ApplySetting` | `Func<string,string>` | `{context,ownerToken,key,expectedRevision,expectedValue,value,leaseMs,leaseId?}` |
| `Control.V1.Renew` | `Func<string,string>` | `{schemaVersion:1,instanceGeneration,ownerToken,leaseId,leaseMs}` |
| `Control.V1.Release` | `Func<string,string>` | `{schemaVersion:1,instanceGeneration,ownerToken,leaseId}` |
| `Control.V1.WithDirectionalAngle` | `Func<string,Func<bool>,string>` | `{context,ownerToken,actionId,expectedRevision,expectedValue}` plus one synchronous action-request callback |
| `Follow.V1.GetSnapshot` | `Func<string,string>` | Exact context; returns revision, active/master/preset/navigation state |
| `Follow.V1.Stop` | `Func<string,string>` | `{context,ownerToken,expectedRevision,reason:"user-requested-compatibility-repair"}` |

Settings values are canonical strings, booleans `"true"`/`"false"`, enums by
exact name. Discover keys rather than assuming an arbitrary configuration field
is permitted. The only cross-frame setting currently allowed is
`BossMod.AI.AIConfig.DisableObstacleMaps` to `"false"` (at most 2000 ms).
Camera-angle options for RPR/DNC/WHM/PCT/shared directional actions and DRG's
Elusive Jump policy are `synchronousOnly:true`, at most 250 ms. They cannot be
borrowed through `ApplySetting`.

`WithDirectionalAngle` accepts only audited Hells' Ingress/Egress, En Avant,
Aetherial Shift, Smudge, Elusive Jump and Occult Featherfoot action IDs. It
temporarily selects the discovered non-camera/default option, calls the supplied
`Func<bool>` once and restores in `finally`, including when it throws. It returns
`actionRequested`, `releaseComplete` and acceptance/reason. It does not execute
an action on its own or validate action legality, facing, cast permission,
landing geometry or effect confirmation. The callback must use the consumer's
existing supported safety checks and must not block. A deferred action queue may
read the option after restoration; this scope provides no guarantee for such a
queue and must not be used as a persistent setting workaround.
[The compile-checked scope example](examples/BossModV1DirectionalScope.cs)
shows current setting/revision discovery and result checking. Supply the matching
job setting key from `GetSettings`, its audited action ID, and an already
safety-checked synchronous request callback. Handle provider disappearance at
its calling boundary; success is still only a request, not an action effect.

At most 32 resources can be leased. A live owner, fresh native context,
permitted duration/value, and exact expected value/revision are required for
acquisition. Setting acquisition is at a contributable stage and is rejected
during manual input, death, mount, cutscene or automatic movement pause. Leases
are checked independently on framework updates, including when a legacy frame
wrapper prevents callbacks. Expiry/owner loss/manual/death/territory/preset
changes/provider disposal restore only still-owned resources. A later explicit
user write or observed value change invalidates ownership, including observed
changes away and back. Native notifications run, while persistence uses original
preferences instead of borrowed settings. Legacy global transient clear cannot
steal owned fields. Failed cleanup remains pending and retries; clients must not
assume a rejected release restored the setting.

Renewal/release use instance/owner/opaque lease identity; renewal checks current
ownership and duration. Lifecycle renewal uses a fresh copied framework control
observation (at most 250 ms old), reads no game objects on the caller thread,
and cannot prevent the next native sweep from revoking a changed scope.
Release may clean up its own lease after owner expiry.
It cannot release another owner's resource. Call release before unregistering
and handle provider disappearance. Do not mix V1 leases with a legacy global
clear/restore workaround for the same resource.

Follow stop requires `hints-ready`, a live registered owner, no manual/mount/
death/cutscene/pause condition, and the revision just read from `GetSnapshot`.
The revision tracks observed native follow object/master/active preset identity;
a changed state rejects the stop. It calls native `SwitchToIdle`, cancels the
current behavior, clears its controller and active preset, and preserves saved
follow settings/AIPreset preferences. No previous follow state is restored by
this one-shot compatibility repair. Native follow workers now build captured
navigation only; native frame processing polls completion without waiting and
alone writes controller/hints/preset state. Retained results preserve their
original deadline and reject old/rewound player/territory/content observations.
This corrects the inspected async-continuation ownership risk; representative
follow/cast/manual behavior still needs in-game acceptance. Legacy consumers
that require the removed private follow `_semaphore` must fail closed until they
use native publication/query identities; a completed map is now separate from
the active worker context. No compatibility claim is made for private-field
reflection or for its old completion heuristic.

Preset selection leases, cast-departure holds/
ingress/destination preferences and contributed projected constraints remain
explicitly unsupported. The general `control-leases` umbrella is not advertised.

## Bounded native automatic-input pacing

`automatic-input-pacing:1` is independent of configuration leases and the still
unsupported general movement-preferences family. It supports short, out-of-combat
travel pacing through the native NormalMovement controller. Stable destinations,
cast-departure holds, ingress and general movement setters remain unsupported.

`Movement.V1.RequestPace: Func<string,string>` accepts only a fresh native
**`movement-proposed`** callback context and a separately renewed integration
owner:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"<provider>","frameId":42,"stage":"movement-proposed"},"ownerToken":"<registered>","generation":123,"scale":0.6,"leaseMs":100}
```

`generation` is the observed native navigation generation; `scale` is finite in
0.35–1 and `leaseMs` is 1–100. At most four validation attempts and one accepted
request are allowed per frame. Another owner cannot replace the accepted proof.
A valid request returns `accepted:true`, `proofId`, `status:"pending-native-application"`,
scale, source generation and its timing model. Rejection is unavailable with a
reason. Acceptance is not confirmation that input was scaled or the player moved.

NormalMovement records `nativeControllerDecision` after its final range/cast
corrections, including the integration instance/frame, origin, actual controller
destination/direction and source navigation generation. `matchesCurrentHints`
means this recorded direction still matches the current native hints; it is not
an inferred identity from an older raster. Requests reject missing/different
controllers, follow ownership, adjusted destinations not matching the native
witness, manual input, combat in the local player or a known party member,
engaged native targets, queued/live casts, native cast pressure, pending
knockbacks, flight/mount/cutscene/pause, interaction/jump, authored forced movement,
forbidden facing and special modes.

The original complete native witness, safe maximum-goal result, source actor/
territory/content/generation, current origin/geometry/layer and physical coverage
must still be valid. Native relaxed-warning/fallback/incomplete/teleporter routes
cannot authorize pacing. The complete walk route is revalidated against current
physical, temporary and timed warning constraints, using the real hitbox and the
lower of source/current native model speed. It conservatively assumes **zero
progress for the entire lease**, plus the age of the live world observation,
then native movement resumes. Native deadline reserve, the native activation
cushion and an additional 0.25 s margin must permit this. It does not assume
analog magnitude is a calibrated speed ratio. World/source observations older
than 250 ms fail unavailable, and replay-only worlds do not fabricate a live
observation timestamp. The usual bounded route evaluator applies; no map rebuild
or worker wait is introduced.

After all proposed-stage callbacks, BMR revalidates the proof. It arms only after
`ExecuteHints` assigned the exact expected direction in the matching native
instance/frame. At actual walk input, the native automatic branch additionally
requires that same assignment and unmodified unit output, no raw manual input,
follow-path, misdirection, block or other native input ownership. Only the
magnitude of already-produced native input is reduced; no direction, destination,
player speed setting or configuration is rewritten. Owner/scope expiry, a newly
queued cast, a changed assignment, a new frame, cleanup and provider disposal
revoke pacing. Provider liveness uses an already-captured owner probe with no
lifecycle dictionary lock or consumer callback on the input path.

Proof lifetime starts at the native controller observation, is at most 100 ms,
and cannot outlive the original navigation source's 250 ms lifetime. Re-reading,
renewing the integration owner or repeatedly sampling input cannot extend it.
Submit afresh each proposed stage; there is no `Control.V1.Renew` for pacing.
Even repeated requests must pass current route/deadline validation each frame.

`Movement.V1.ClearPace: Func<string,string>` accepts the lifecycle payload
`{schemaVersion:1,instanceGeneration,ownerToken}` without a stage context. It may
run on the unload thread, is idempotent when absent, and cannot clear another
owner's proof. A clear racing with native finalization cannot resurrect a ticket.
No saved value or previous follow/controller state is restored.

The movement observation's `automaticPacing` identifies the requested proof and
pending/armed/revoked state. `movement-applied` still describes override assignment.
Actual native input evidence is separate in `lastInputRead.automaticPaceScale`
and `pacingProofId`, together with its own input generation and matching assignment.
An earlier input sample is never relabeled as the new frame's application.
Other plugins' later input hooks and actual physical speed/arrival need separate
live acceptance.

The compiled [BCL consumer](examples/BossModV1AutomaticPace.cs) requests and clears
an owned frame proof. Discovery and owner renewal remain the caller's normal
integration lifecycle:

```csharp
var pacing = new IntegrationExample.BossModV1AutomaticPace(
    pi.GetIpcSubscriber<string, string>("BossMod.Movement.V1.RequestPace").InvokeFunc,
    pi.GetIpcSubscriber<string, string>("BossMod.Movement.V1.ClearPace").InvokeFunc);
// In movement-proposed, using that snapshot's navigationGeneration:
pacing.TryRequest(context, ownerToken, navigationGeneration, 0.6f);
// Observe native arming and matching input proof; clear before owner unregister/unload.
pacing.Clear();
```

## Owned transient strategies

`transient-strategy-leases` is advertised separately from configuration leases.
Both `Strategies.V1.GetSnapshot` and `Strategies.V1.Apply` have IPC signature
`Func<string,string>`. They use the active preset's exact object identity and
an additional opaque `presetId`, not a name-only restoration assumption.

Read with `{context,presetName,module,track}`. `module` is the full registered
rotation-module type name; `track` is its exact internal track name. Only a
module occurring once in the currently active preset is supported. Presets are
limited to 64 modules, each to 128 tracks/options. The response includes its
`presetId`, opaque canonical `value`, resource `revision`, `leased`,
`legacyTransientActive`, option names with their `allowed` policy, numeric bounds
as invariant strings, target kinds and maximum duration. The canonical `value`
is a comparison token, including native target/priority/offset/expiry fields;
echo it unchanged as `expectedValue`. Do not reconstruct it or treat its presence
as permission to apply an arbitrary option.

Apply at `hints-ready`, with a live owner and no manual/death/mount/cutscene/pause
condition:

```json
{"context":{"schemaVersion":1,"instanceGeneration":"provider","frameId":42,"stage":"hints-ready"},"ownerToken":"owner","presetName":"my-preset","presetId":"from-read","module":"BossMod.Autorotation.MiscAI.NormalMovement","track":"Range","expectedRevision":7,"expectedValue":"exact value string from read","value":{"valueText":"GreedAutomatic","target":"Automatic","targetParam":0},"leaseMs":1000}
```

An update additionally supplies its existing `leaseId`. The response uses the
same accepted/reason/leaseId/resourceRevision shape as settings. Renew/release
through `Control.V1.Renew`/`Release`, and renew the registered owner separately.
Native lifetime enforcement follows successful acquisition and renewal, with a
maximum of 2000 ms, even if an external frame wrapper stops normal callbacks.

Option values use exact internal names; numeric values use finite invariant
strings within the native track bounds. No silent clamp is performed. Supported
targets are `Automatic` with parameter 0 and, for option tracks, `EnemyByOID`
with a nonzero native OID encoded in the existing signed 32-bit parameter,
and only when that option reports `canTargetEnemyOid:true`.
Explicit point/facing targets, custom action priority and arbitrary offset writes
are not exposed. Native priority/comment/expiry settings are preserved when
changing an option; target offsets reset when selecting the requested supported
target kind. Native target legality and action safety remain authoritative.

Owned strategies occupy a distinct runtime layer. Saved entries and legacy
transient lists are never edited or restored. Another owner or an existing
legacy transient on that track rejects acquisition; legacy add cannot steal an
owned track, and global legacy clear cannot clear the owned layer. Different
tracks remain independent. User preset replacement, explicit editor changes,
observed native reference/value changes (including equal-value replacement),
owner loss, expiry and invalid scope remove the layer. Releasing a lost lease
never writes over the user's choice. Cloning/serializing presets does not carry
owned runtime values to another preset or to disk.

Held Shift/Ctrl/Alt native strategies retain authority: the owned layer is not
applied while any native modifier is held. Unsupported/oversized native data
cannot keep an old layer active. Normal movement policy denies explicit
destinations/deadlines, greedy cast modes, ignored special modes, additional
movement delays and reduced forbidden-zone cushions. Native pathfinding,
encounter constraints, deadline/cast checks and final application still execute
inside BMR. Preset selection itself is not leased by this family.

## Module identity and enablement observations

`module-catalog` advertises `Modules.V1.GetSnapshot: Func<string,string>`, with
`{context,offset:0,count:64}`. Pages accept count 1–128 and a valid offset;
`total` and nullable `nextOffset` allow bounded enumeration. Entries identify
the full native module type and primary actor OID, maturity, configured
enablement/minimum maturity, expansion/category, selected/loaded/active-phase
state and pending instance count. OIDs are hexadecimal strings. Loaded actor
observations are capped at 16 with an explicit completeness flag; total native
loaded/pending observations must fit 1024. Configuration eligibility does not
claim that a module is loaded or owns positioning.

Catalog observation remains distinct from mutation or a maturity bypass.
`configuredEnabled` is the native configured preference; `effectiveEnabled` and
`eligibleToLoad` include runtime leases. Loaded actor entries additionally report
`retainedAfterLease` when native active-phase coverage survives a temporary
enable's expiry. `enablementLeasesSupported:true` advertises the separate control
family described below. Native disabled-OID caching refreshes
when its public list is replaced or edited in place, so native eligibility and
the catalog do not retain an obsolete disabled set.

### Owned module enablement

`module-enablement-leases:1` advertises two `Func<string,string>` endpoints:

- `Modules.V1.GetEnablement` accepts `{context,identity}`. Use the exact catalog
  `identity` (`<uppercase hexadecimal OID>:<full native module type name>`), never
  infer it from names, category, maturity or hitbox. The response returns
  `savedEnabled`, runtime `enabled`, `revision`, `userRevision`, `leased`,
  `maxLeaseMs`, `meetsMinimumMaturity`, and `canTemporarilyDisable`.
- `Modules.V1.ApplyEnablement` accepts the following. Both booleans must be
  explicitly present; missing/null values reject rather than defaulting to a
  disable. The exact observed revision and effective value must still match.

```json
{"context":{"schemaVersion":1,"instanceGeneration":"provider","frameId":42,"stage":"hints-ready"},"ownerToken":"owner","identity":"41E2:BossMod.Dawntrail.Dungeon.D05Origenics.D050OrigenicsAerostat.D050OrigenicsAerostat","expectedRevision":7,"expectedEnabled":true,"enabled":false,"leaseMs":1000}
```

Acquisition requires hints-ready, a live owner, current native identity/maturity,
and the usual no-manual/death/mount/cutscene/pause state. A new disable additionally
requires the player out of combat and every loaded/pending instance of that OID
inactive with its native pull predicate false and primary actor out of combat.
It cannot withdraw an active or about-to-activate module. Future load eligibility
changes on the next native manager update, not by executing another frame.
An already accepted disable can be renewed without reapplying it; native expiry,
manual/scope/preset/territory changes and owner loss still revoke it. A lost lease
cannot be blindly reacquired during combat. Module disabling removes bespoke
coverage and can fall back to native automatic hints; it is not equivalent hazard
coverage or a generic fix for a faulty encounter arena.

Enabling an explicitly configured-disabled module requires
`reason:"user-requested-temporary-enable"`, reflecting an actual user request.
Automatic integrations must preserve saved opt-outs. Dummy/unknown identities
and modules below the current maturity gate are rejected.

Successful acquisition returns `accepted`, `leaseId` and `resourceRevision`.
An update also supplies its existing `leaseId`. Renew/release through
`Control.V1.Renew`/`Release`, and renew the integration owner separately. The
runtime layer expires within 2000 ms and is removed independently by native
eligibility reads/framework sweeps; lifecycle calls read a copied configuration
observation, not game state from another thread. At most 32 layers/resources and
128 bounded observations are kept. Unowned observations may be evicted, which
invalidates stale revisions. Active native provenance is pinned and cannot be
evicted by catalog paging.

The configured disabled-OID list is never borrowed or rewritten, including on
unrelated saves. Serialization omits the runtime table. Explicit user single,
bulk, expansion/category, console and config-import choices revoke affected
ownership, even when the chosen value equals the borrowed or saved value.
Observed raw array edits also invalidate affected watches. Different OIDs remain
independent. Removal of a runtime layer never restores a stale saved list.

If a temporary enable loaded a module which became active, expiry/release
restores configured eligibility but retains that native instance's coverage
until its normal reset/end/unload. A later explicit user change to that OID
overrides retention. An inactive leased instance can return to pending on
expiry; native cleanup releases its provenance pin. This preserves native
hazards during owner failure without silently changing the user's saved choice.
Use loaded/active/retention observations rather than assuming release unloads
a module immediately. The [compile-checked module lease example](examples/BossModV1ModuleLease.cs)
shows exact state comparison, explicit opt-in for enables, renewal and checked
release. Production hallway-toggle adoption and live behavior remain unverified.

## Migration and validation still required

The XCAI consumer repository remains unchanged at this stage. Replacing a goal
setter alone does not remove its draw/frame wrapper. Its movement safety,
route/raster, custom arena, target/tank/cast, mechanic forecast/projection,
damage/effect, follow/module/settings, movement pacing/hold and native-workaround
families remain tracked by the delivery ledger. RSR-specific reflection is
outside this issue.

| Existing XCAI dependency | Supported replacement now | Residual work / current migration state |
| --- | --- | --- |
| `BossModGoalZoneHook` draw replacement, reflected frame updates, private services, `WPos`/goal delegate adapters | Registration, three stage callbacks and BCL scoring delegate | Production consumer remains on legacy wrapper; preserve all safety features until its other dependencies have supported replacements |
| `BossModMovementIntentReader`, duty/encounter classification and movement diagnostics | Stage snapshots, authored versus proposed/applied direction, selected versus active module; actual async search/build/publication identity | Exact final applied controller/path/follow ownership and physical input publication identity remain incomplete |
| `BossModReflectionSafety`, `BossModRegroupingNavigation`, `BossModRouteObservation`, raster/knockback recovery navigation | Copied map/route publication and bounded current complete walk-route validation; existing dash/position safety endpoints continue | Continuous nearest-safe/ingress semantics and additional projections remain required; production map/task/semaphore reflection not removed yet |
| `BossModCustomArenaClearance`, edge/ingress readers and shape adapters | `Arena.V1` geometry descriptors, containment/clearance/boundary/inset candidates | Route-safe ingress and obstacle generations remain unsupported; production reader not yet migrated |
| `BossModOffensiveTargetHints`, `BossModTankHints`, `BossModCastControlHints`, AoE/FATE target-policy reads | `Hints.V1.GetTargets`, `Hints.V1.GetCastControl`, native cast identity/producer links, hint-boundary transform provenance and nullable explicit requirement | Component-specific default/authored reasons and additional cast-linked producers remain incomplete; native estimates do not prove server interrupt cutoffs; no consumer removal claim |
| `BossModUpcomingAoeReader`, stack/separation/activity, landing/knockback/forced-march/immunity and learned-safety clones | `Mechanics.V1` bounded generic publications and actual-pose native projections | Whole-encounter coverage, counterfactual/independent-hazard/recovery evaluations still required before migrating dependent optimizations |
| `BossModSupportDamageReader`, `BossModMitigationDeadlineReader` | Full same-generation predicted-damage list, copied recipient IDs, party mapping and completeness | Consumer migration and live overlap/party-change acceptance; offline overlap/identity tests pass |
| `BossModGroundActionObserver` / reflected `ActorState.CastEvent` subscription | `World.V1.GetActionEffects` | Poll from the frame callback and handle dropped/incomplete updates; production subscription not yet replaced |
| Follow stop, hallway module enablement, transient configuration/strategy ownership and temporary directional angle settings | Conditional `Follow.V1.Stop`, `Control.V1` settings/scopes, `Strategies.V1` ownership, and `Modules.V1` catalog/enablement leases; legacy IPC stays compatible | Preset-selection leases remain unsupported; honor module acquisition/retention/user-intent semantics during production adoption |
| Writes to `ForcedMovement`, `_rmiWalkHook`/`ActualMove` pacing, cast-departure hold, duty ingress and equivalent destination stabilization | Bounded native automatic-input pacing; no general movement setter | Stable destinations, cast-departure holds and duty ingress still require native validated contracts; production pacing hook migration/live input acceptance remain open |
| Stack masks, redundant tank-facing, FATE pathfinding-frame guards and additional projected constraints | Native defects have not been established by this contract work | Gather evidence, preserve user opt-outs, then implement justified native behavior/capability or a narrowly validated lease; do not clone existing workarounds blindly |

[`examples/BossModV1Consumer.cs`](examples/BossModV1Consumer.cs) is a compile-checked
BCL-only example exercised against the real contract session in the regression
suite. It submits an immutable scoring closure at `hints-ready` and observes
proposed/applied stages; it never replaces a draw handler or invokes native frame
methods. Inject the following Dalamud subscriber delegates using the consumer's
own plugin interface:

```csharp
var client = new IntegrationExample.BossModV1Consumer(
    pi.GetIpcSubscriber<string>("BossMod.Integration.V1.Capabilities").InvokeFunc,
    pi.GetIpcSubscriber<string, Action<string>, string>("BossMod.Integration.V1.Register").InvokeFunc,
    pi.GetIpcSubscriber<string, string>("BossMod.Integration.V1.Renew").InvokeFunc,
    pi.GetIpcSubscriber<string, Func<Vector2, float>, string>("BossMod.Goals.V1.Submit").InvokeFunc,
    pi.GetIpcSubscriber<string, string>("BossMod.Integration.V1.Unregister").InvokeFunc);
```

The example demonstrates transport and stage composition, not XCAI feature
parity. Its caller must handle provider disappearance and reconnect after BMR
reload. Production migration also has to preserve all goal priorities, manual
handoff, mobility and the feature-specific requirements above. A legacy consumer
that has already replaced BMR's `DrawUI` bypasses newly inserted native stages;
discovering the capability alone is not evidence that those stages are running.

Contract checks are in `tests/IntegrationContractRegression`; they cover owner
isolation, schema/stage/freshness rejection, callback/scorer failures, expiry,
manual/scope invalidation, unload, and native raster priority preservation. They
also cover effect identity/order/duplicate/drop behavior, analytic/rotated/custom
arena geometry, holes, missing/invalid layers and unsupported geometry. They
do not establish full async navigation publication correctness, physical arena
geometry, live route deadlines or XCAI gameplay acceptance.

Run with the pinned SDK after building the current plugin:

```sh
dotnet build tests/IntegrationContractRegression/IntegrationContractRegression.csproj -c Release
dotnet tests/IntegrationContractRegression/bin/Release/net10.0/IntegrationContractRegression.dll BossMod/bin/Release/BossModReborn.dll ${DALAMUD_HOME}
```

### Provider evidence, 2026-10-17

- Full Release compilation with the pinned SDK, compatible API 15 references,
  source generation and diagnostic analyzers passed with six existing
  obsolete-gauge warnings and zero errors.
- All 18 regression suites passed: 7,718 assertions/checks, including 1,621
  integration assertions and 153 Heretic among Us checks. Tests selected the
  exact built assembly. These are synthetic/offline checks.
- The twenty independently versioned families were observed through a read-only
  live capabilities query. Live module-toggle behavior and production consumer
  adoption remain unverified.
- Python discovery passed 8 tests with one installed-game helper case skipped.
- Representative gameplay, the full XCAI migration and residual provider
  requirements in the delivery ledger remain open. No release publication or
  complete reflection-removal claim is made. Machine-specific build operations
  and incident diagnostics are maintained outside repository documentation.

### Cast-control checkpoint, 2026-10-17

- Added `cast-control-observation:1` as the twenty-first independently advertised
  family. The existing target schema retains its cast fields and shares the
  native cast identity with the new observation.
- Full Release compilation with the pinned SDK, compatible API 15 references,
  source generation and diagnostic analyzers passed: zero errors and six
  existing obsolete-gauge warnings.
- All 19 regression suites passed against the exact built assembly: 7,807
  assertions/checks, including 1,675 integration assertions and 35 Isgebind
  checks. Coverage includes the actual native query endpoint, point limits,
  schema/thread/context guards, missing player, cast restart/cancellation,
  ambiguous/unknown source links, failed point evaluation, bounded enumeration,
  native cast-start producers, target-schema compatibility and the compiled
  positive-evidence consumer.
- Source/build/fixture proof does not establish a real server interrupt cutoff,
  action legality, complete encounter threat coverage, production XCAI migration
  or representative live gameplay acceptance. The issue remains open for the
  residual provider and consumer requirements in the delivery ledger.

### Automatic-input pacing checkpoint, 2026-10-17

- Added `automatic-input-pacing:1` as the twenty-second independently advertised
  family, plus native final-controller provenance. General destination changes,
  cast-departure holds and duty ingress remain unsupported.
- Full Release compilation with the pinned SDK, compatible API 15 references,
  source generation and diagnostic analyzers passed with zero errors and six
  existing obsolete-gauge warnings.
- All 19 regression suites passed against that exact assembly: 7,893 assertions
  or checks, including 1,761 integration assertions. New coverage exercises the
  native request/finalization/assignment endpoints, route/deadline rejection,
  later constraint changes, owner loss, queued casts, death observation, manual
  and special-input precedence, short monotonic expiry, cancellation races and
  the compiled consumer. Input liveness does not wait for lifecycle locks.
- These are source/build and offline fixture results. Actual game input scaling,
  calibrated analog movement speed, production XCAI hook removal and live
  gameplay acceptance remain unverified. A pending or armed proof is not an
  observation of applied input. The delivery ledger and issue remain open.

### Controller/input provenance checkpoint, 2026-10-17

- Added the twenty-third independent family, `controller-input-provenance:1`.
  Native controller, source search, actual override assignment and input read
  retain distinct identities; corrected destinations do not inherit route safety.
- Full Release compilation with the pinned SDK, compatible API 15 references,
  source generation and diagnostic analyzers passed: zero errors and six
  existing obsolete-gauge warnings.
- All 19 suites passed against the exact built assembly: 7,932 assertions or
  checks, including 1,800 integration assertions. Native follow fixtures cover
  live cast/manual correction, other-writer precedence, missing and older search
  sources, interaction/pause suppression, changed application values and frame
  cleanup. Contract and compiled-consumer fixtures cover assignment/read races,
  same-value reassignment, immutable retained observations, exact pacing arming,
  incompatible schema, stale/manual samples, bounded retention and reload.
- Native input hooks and full NormalMovement range/cast sequences were not
  exercised in game. Later third-party hooks, physical displacement/arrival,
  production XCAI migration and representative gameplay remain unverified.
  The issue stays open for residual provider and consumer requirements.
