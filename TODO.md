# XCAI → xBMR migration handoff checklist

Release preparation 7.5.6.35.19, 2026-10-24. The maintainer explicitly authorized
commit/push/publication and permanent standalone XCAI retirement. All retained
systems are implemented; remaining gameplay work is validation/tuning.

Updated 2026-10-24. Implementation handoff reached; final offline checks and the
fresh upstream trial passed. The user wants a handoff when only dungeon
testing/tuning remains. Do not reopen completed imports or deferred enhancements.

2026-10-24 follow-up: mandatory standalone XCAI retirement is now requested.
The implementation waits for a safe lifecycle handoff, removes the exact XCAI
installation through Dalamud and preserves legacy settings. A persistent modal
then confirms completed removal; **Got it** records acknowledgement once and
does not authorize or delay removal. The
feed cutover must publish merged xBMR and remove XCAI in the same generated-feed
update. Retired standalone publishers must not re-add it. Validation of this
follow-up is recorded separately from the earlier artifact receipts below.
See [mandatory XCAI retirement](docs/native-combat-assistance/xcai-retirement.md)
for the successful main-client removal, current artifact and remaining rollout.

## Scope and ownership

- One xBMR plugin, one native movement planner/publication/executor. BMR safety,
  assignments, activation, explicit destinations/presets and manual handoff remain
  authoritative. Installing assistance does not activate AI.
- RSR supplies action evidence and original action-selection checks. Native BMR
  rotations keep their own goals. No new native rotation predictor.
- User exclusions: ToF companion integration, automatic tank following between
  pulls, unknown-knockback projection/learning, and the `/xcai camera` subcommand.
  Camera settings remain in the Material interface; native explicit follow remains.
- User deferral, 2026-10-24: stop expanding/polishing knockbacks. Keep the verified
  authored phase/immunity/observation implementation and BMR native recovery for
  unsupported cases. First-occurrence timing, broad wall/layer/custom projection
  and additional cast/mobility combinations are deferred enhancements. Do not
  present those as completed or restart that work during this completion pass.
- Publishing, public feeds, uninstalling XCAI and watched-DLL deployment/reload are
  separate actions. No game/idle-client experiments during this completion pass.

## Implemented systems

- [x] Shared native safety/traversal, immutable worker input, one active owner,
  superseded-worker cancellation, original deadlines and explicit bounded results.
  The proposed 4 ms / 8,192 quota is removed; freshness/departure and documented
  memory/evidence limits remain. Independent small-map solver comparisons exist.
- [x] Current attacks/heals, target/range/positionals and independent recipients;
  native recovery, route detours, warnings and temporary-stop escape validation.
- [x] Mobility mechanics, legal targets/facing/returns, charges, locks, original
  RSR selection, exact pre-execution validation and per-candidate rejection.
- [x] BLM placement/return/Retrace/Between the Lines, RDM evidenced combo recovery
  and PCT field/Smudge return. Unknown timing/footprints remain bounded; no
  invented haste or complete Inspiration radius. The first walking publication
  now includes useful after-action zone return, preserving the current spell.
- [x] Utility zones, healing coverage, support recipients, raised-player healing,
  Kardia/fairy/cards/partner, ranged mitigation, interrupts/stuns, tank stance/
  recovery and tank Arm's Length mitigation. Actual delivery remains for dungeon testing.
- [x] Combat-only pull contact: observed advancing multi-enemy pack, role ranges,
  hysteresis, immediate combat-end retirement and native leader/preset precedence.
  Native vnavmesh quest/obstacle interfaces remain; no second navigation follower.
- [x] Social spacing, natural destinations, party/tank facing, boss-center/edge
  preferences and camera/manual pause. Presentation remains a tie-breaker.
- [x] Material BMRAI interface, native General/Movement bindings, unchanged `/bmr`,
  `/bmrai` operational commands and `/xcai` compatibility except retired camera.
- [x] Separate versioned assistance settings, one-time import/clamping, opt-outs,
  original rollback file, restoration journals and standalone coexistence gates.
- [x] Bounded `BossModReborn.DevBridge.GetSnapshot`, existing world-marker overlay,
  xBMR capture identity, historical XCAI decoding and opt-in fight sharing.
- [x] Remove all 72 XCAI-only contracts and local serialized transport. Preserve
  all 67 recorded upstream IPC names. No standalone dependency or internal IPC.
- [x] Explicit responsibility/retirement mappings for all 100 settings and 385
  source files in adjacent inventories. This is accounting, not a live parity claim.

## Final offline validation

- [x] Build final current source through the managed compilation environment.
- [x] Run the native/reference suite, installed RSR contracts, broader suites,
  repository checks and inventory/whitespace verification on the exact artifact.
- [x] Refresh actual newer-upstream trial, resolve conflicts and validate its
  native behavior/build. Preserve main working tree, ancestry and version scheme.
- [x] Write final artifact/validation/upstream identities and dungeon handoff.

Final evidence: 42,863 native assertions and 19 broader suites pass on both the
final DLL and actual newer-upstream merge. Nine repository tests and installed RSR
contracts pass. Exact hashes, source/merge identities and limits are recorded in
`docs/native-combat-assistance/{handoff.md,validation-current.json}`. The final
artifact was not deployed during this pass. Stop here for user dungeon testing.

## Dungeon and lifecycle acceptance remaining

These are testing/tuning, not requests to invent more systems before handoff.

- [ ] Melee safe dodge/uptime, blocked-route detour, narrow ranged recovery,
  target-circle/cone/line coverage, healing recipients and tank-facing behavior.
- [ ] Successive warnings, temporary stops/onward escape, active/unknown expiry,
  disconnected/unsupported geometry and no-safe-route recovery.
- [ ] Target dashes, deliberate-facing backsteps, return anchors, failed setup,
  locks/cast completion, Sprint/True North and charge preservation. Test supported
  knockbacks only; deferred expansion is not a blocker for this dungeon handoff.
- [ ] Stable repeated-frame destinations and invalidation on actual hazard,
  target/action/recipient changes, manual movement or explicit targeting.
- [ ] BLM placement/return/relocation, PCT observed-field return/Smudge and RDM
  ST/AoE continuation. Confirm delivery/timing and tune actual field boundaries.
- [ ] Combat pull contact, explicit follow/presets, Questionable handoff, RSR
  unavailable/native rotations, death/resurrection, mounted pause, logout/unload.
- [ ] All Material pages/shared controls, command behavior, standalone coexistence,
  repeated startup/import/journal restoration, camera pause, overlay and sharing.
- [ ] Before any later watched deployment: fresh out-of-combat checks for affected
  clients, then normal watcher reload and exact loaded provenance/health checks.
  The last loaded DLL is older than the isolated final candidate. Do not call an
  isolated build a deployed or live-accepted migration.

DevBridge's command-testing extension has been reported available by its agent.
Refresh MCP discovery/skill before using `commands` → `composedSnapshot` →
`action.testPluginCommand` with its single-use tokens. No downstream command or
live gameplay acceptance is claimed from that report.

## References

- `docs/native-combat-assistance/{settings-inventory,source-inventory,migration-map,constraint-review}.json`
- `docs/native-combat-assistance/upstream-interfaces.md`
- `docs/native-combat-assistance/implementation-status.md`
- `docs/native-combat-assistance/migration-history.md` (superseded chronological
  checkpoints; do not turn old paragraphs into new implementation requirements).

Branch: `codex/native-combat-assistance`; main source is intentionally uncommitted
on `f1de3550b1d96bfd8f153d7b4a7ffe53e5e0d530`. Preserve unrelated work. Standalone
source baseline: `4a0f7968da8a743e8971973821cb9f6f1387198c` (left unchanged).
