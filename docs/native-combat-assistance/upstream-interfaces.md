# Modified upstream interfaces

The encounter module tree remains independent of `CombatAssistance`. Existing
class identities, strategy tracks, presets and native configuration identities
are retained. The inventory below describes implemented boundaries, not final
cutover acceptance; the adjacent status file lists remaining work.

| Boundary | Change and reason |
| --- | --- |
| `Framework/Plugin` | Own assistance settings, Material frame/theme lifetime, diagnostics, support updates, opt-in recording sharing, mandatory legacy XCAI removal and its persistent post-removal acknowledgement modal. Existing activation still gates automation. Scope repeated plugin-inventory reads to the synchronous update and expose its existing update timer in current diagnostics. |
| `AI/AIManager`, `AIManagementWindow` | Material management window and compatible commands; legacy /xcai switches use shared assistance settings and a non-registering shell fallback, yielding to standalone. The native AI config remains the source for overlapping controls. Dungeon contact checks both saved and active leader selection without changing either. |
| `Autorotation/RotationModuleManager` | Own one native movement service and its lifecycle. Collect the current RSR target before movement and refresh the loop target after automatic selection on the assisted path. Expose only whether a native automatic-target module is active. After module policy resolution, submit stationary target assistance through the same host only when no normal/follow owner is running; do not apply a recovery walk from that caller. |
| `Autorotation/MiscAI/NormalMovement` | Submit automatic and explicit destination intent through the same capture/publication/executor. Resolve existing explicit range/greed choices before search; preserve the exact requested endpoint and its arrival deadline. Native arena entry now follows movement locks and validates its whole physical corridor. Same-target range intent retains combat recovery and the observed readiness clock rather than masking it as optional spacing. |
| `AI/AIBehaviour`, `AIController` | Submit follow planning and active encounter identity through the same host, including the shared dungeon-arena entry check; preserve leader/follow/manual semantics. Native combat Sprint yields to owned native utility planning and saved Sprint opt-outs. |
| `AI/AIConfig`, `AI/AIBehaviour` combat target range | `UseCombatPositioningRange` selects job reach for the identified generic follow-target goal while native RSR combat assistance is active. Exact action geometry remains in the joint planner. Turning it off restores the saved fixed distance. Both native and Material windows bind to that one setting; presets, leader following and unrelated goals retain their contracts. |
| `BossModule/AIHints` | Freeze actor-dependent target goal values; retain separate native constraints and temporary obstacles. Distinguish native automatic target intents from authored assignments while preserving the public `ForcedTarget` field. Local correction requests do not replace authored hints. |
| `BossModule/AIHints.CastSources` | Preserve exact cast producer identity without the former transport source-count limit. Native cast control never removes a hazard after merely submitting an interrupt or stun. |
| `Pathfinding/NavigationDecision`, `NavigationPublication` | Coherent capture and native recovery; complete witnesses, original deadline aging and cancellation. Captured warning cushions let the shared host require earlier healer/physical-ranged clearance without changing authored timestamps or provenance; see `early-mechanic-clearance.md`. Unspecified and `DateTime.MaxValue` forbidden-zone activations are due now, ordered before timed warnings during raster sampling; unknown timing cannot create a two-minute wait. |
| `Pathfinding/TimedTraversal` | Shared swept-cell traversal and exit-deadline checks for native recovery and joint planning. Exact separable interval clipping accelerates long diagonal routes while retaining the native footprint and deadline arithmetic. |
| `Pathfinding/NavigationPublication` recovery assessment | Optional independent-warning exposure count and explicit panic-hold flag retain original source timestamps. Shared rasterization accepts framework-only single-warning capture; no public IPC endpoint or serialized transport is added. |
| `Framework/MovementOverride` | Current native manual-input observation, without consumer leases or callback transport. |
| `Framework/ActionManagerEx` | Exact planned-action submission and synchronous, conditional restoration of the auto-face option. Manual input and native availability remain binding. |
| `Components/Knockback` | Share native scalar direction/pull arithmetic and participant/immunity applicability with typed capture. Public class/source identities, shape, wall, safe-wall and destination semantics remain unchanged; native reference/wall fixtures validate the factor. |
| `Components/ForcedMarch` | Native hypothetical pose projection without the former transport pending-move cap. Authored direction, wall and destination rules remain in the component. |
| `BossModule/AIHints.ProjectionSources`, knockback shape envelopes | Native producer attribution for separating displacement preparation constraints from independent hazards. No versioned integration transport. |
| `Framework/IPCProvider` | Remove the 72 provenance-identified XCAI integration registrations; retain all 67 recorded upstream names. |
| `Replay/Evidence` | Native typed replay decoding for opt-in sharing; preserve historical archive compatibility. |
| `ActionQueue/ActionPredicate` | Correct target-dash rejection for knockback, facing, no-movement and pyretic constraints; preserve the documented safety opt-out. |

The final implementation snapshot `f8997079ef43c814f0eec739fb0f317f09f1c173`
merged newer upstream `ae64e3309905dafad0952dbe72680add167591f9` in isolated merge
`5dfa5ddd72704b8ec3463b33782a5a375269787c`, with zero conflicts. Its managed
build, 42,863 native assertions and all 19 broader suites passed against the
exact trial DLL. Main ancestry/version were left unchanged at that checkpoint. The adjacent
`validation-current.json` records both artifact identities; `handoff.md` lists
remaining actual gameplay/lifecycle acceptance.

On 2026-10-25, upstream release `7.5.7.2` at
`ae64e3309905dafad0952dbe72680add167591f9` was adopted on
`codex/merge-upstream-7.5.7.2`, starting from fork commit
`63c1bc59416dae97b7128958e391ff1024341586`. The merge had no conflicts:
upstream changed 20 paths, with no overlap against the fork's 716 changed paths.
All fork-modified paths were preserved; only the version metadata was then
updated to `7.5.7.2.0` (assembly `7.5.7.200`, fork revision zero).
The exact Release DLL passed 143,427 native movement assertions and all 19
broader regression suites; all nine Python tests and release metadata validation
also passed. Compilation had six existing obsolete-API warnings and zero errors.
DLL SHA-256: `44f72574658b4e529f18c6abbde84c3c8130fe7029fcc3f7b25f31028da7dfbd`;
MVID: `b060294a-122f-410c-a175-5076cf399c48`.
These checks establish build and existing-regression compatibility. They do not
provide new replay or live acceptance for the imported DMU, Shinryu Unreal or
Shadowcaster changes. The client was off, so loaded provenance and gameplay
acceptance remain unverified.

Automatic tank following between pulls was retired by user request on 2026-10-23.
The assistance-owned interaction guard and travel observation/waiting hooks were
removed; the upstream interaction executor retains its original contract. Combat
contact and explicitly selected native follow remain separate.

The native AutoTarget writer and follow selector tag automatic intents. Existing
authored writers in xan Tank, AkechiTools/DRG and QuestBattle UnmanagedRotation
use `SetNativeTarget`, including same-actor reassignment. Encounter modules keep
the original public field and have no assistance dependency. Native RSR selection
scopes share one original framework callback; no second callback or internal IPC
is introduced. The loop target fix addresses a source-proven stale local after
AutoTarget changes the target; comparable live impact remains unverified.

Unknown-knockback learning/persistence/import was retired by user on 2026-10-24.
The temporary local-knowledge lifecycle await was removed. Retained authored
observation is bounded to a native module session with no file/history buffer.
Further knockback expansion was deferred by user; native recovery remains.

Native projection provenance collection is now requested through the existing
AIHints flag when enabled displacement preferences require it. Framework capture
retains registered source presence and freezes phase maps with the shared native
rasterizer; exact pure envelope recognition/landing checks are factored from
NativeLandingConstraints. Unknown/mixed rules remain binding. Planning contexts
include that immutable input for the pending joint-transition search; this does
not add an encounter-module dependency or another movement executor.

Physical observation uses the native walking material filter and swept body, with
frozen surface segments fed into both strict and recovery maps. See
[collision evidence](collision-2026-10-25.md); vnavmesh does not execute combat routes.

Generic circle and bait-stack hints add a native gathering goal for eligible
participants and carriers. Prefer the group interior (up to 2y for circle stacks),
with a flat interior to avoid exact-centre chasing. Existing stack radius,
exclusions, capacity, projection layers and hazard deadlines remain authoritative.
Line-stack participants prefer eligible people already inside the authored shape.
Frozen positions are captured when hints are built; workers never read party actors.
Validation: Release build and 142,241 native assertions pass, including interior
selection, safe fallback, capacity/exclusion and line-stack grouping checks.
Actual live stack positioning still needs a matching mechanic observation.

Gathering goals retain a typed native identity during framework-thread capture.
The frozen snapshot keeps their scores separately so a complete, freshly verified
gathering walk can run while optional optimization is pending. Positive native
leeway alone cannot indefinitely hold a stack carrier as peers approach. Current
cast/action locks, stationary-only authority, authored movement prohibitions and
the strict route witness remain binding; arbitrary goal scores do not enable this
fallback. Clear/cancel/disabled goals remove it with the next capture.
For equal eligible group sizes, circle-stack carriers prefer contact with a live,
targetable primary boss. The contact plateau includes the hitbox plus 2y; larger
groups, original eligibility and hard route safety retain precedence. Joiners
still follow the actual stack carrier.

Phantom movement uses the existing external RSR `DataCenter.CurrentDutyRotation`
identity and generated `IBaseAction` getters/Lazy fields, alongside regular job
rotation ownership. Saved enable flags come from the original duty actions; no
new upstream IPC or action writer is introduced. See [Phantom movement](phantom-mobility.md).
