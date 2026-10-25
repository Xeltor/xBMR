# xBMR combat assistance — remaining work

Updated 25 October 2026. XCAI is merged and retired. The current development build
is loaded and the maintainer reports that it feels much more like XCAI. Keep that
working behavior; fix concrete problems instead of expanding the migration.

## Fix and check next

- [ ] **Physical obstacles: broader live validation.** The general collision path
  now uses the game's walking filter and swept body, feeds observed surfaces into
  native search, and checks the final recovery waypoint. Synthetic detours and
  pass-through material checks pass; live probes detect the FC structure that the
  former filter missed. Confirm actual trash-area wall avoidance and repeated
  movement around scenery. See [collision evidence](docs/native-combat-assistance/collision-2026-10-25.md).
- [ ] **Combat efficiency.** Idle overhead is repaired and planning allocations
  are substantially reduced. Combat frame-rate loss is still measurable with
  automatic camera enabled. Exact interval traversal and partitioned search
  frontiers now reduce broad-route and multi-state search cost. The new math build
  passes 129,688 checks; its short dummy comparison measured 55.96 before versus
  56.77 updates/second afterward, with slightly different positions and mixed
  tail timings. This does not close the overall frame-rate issue. Keep camera enabled.
- [ ] **Melee uptime and useful mobility.** Native planning now delivers actual
  Winged Glides, aimed Elusive Jumps and damaging dives through Griffin mechanics.
  The completed Support test confirms their effects and melee landings. Sprint's
  false stationary animation-lock delay is also repaired. A reported first-boss
  escape/dash reversal is repaired: partial range cannot buy a dash, and an escape
  already started is retained through the same pending mechanic. The repaired
  Griffin run also confirms two in-combat Sprint effects. Classify remaining
  Talons and fix reproduced avoidable
  losses. Walking must not win when legal mobility or productive setup preserves
  a melee attack. Keep real mechanics and manual control intact.
- [ ] **Mechanic movement.** Check the reported late/missed dodges and excessive
  retreat against the repaired run. Fix any reproduced failure. Warnings should
  allow safe uptime; active danger still needs an immediate escape. No requirement
  to certify every encounter or hypothetical hazard combination. Recheck the
  first boss's hide on the new escape-commitment build; its report predates reload.
- [ ] **Readable spread/stack movement.** A carried spread should take a clear
  route away from other players, avoiding new overlaps even while its timer would
  permit crossing them. Bring a carried stack to eligible party members promptly,
  favoring a safe gathering point near the boss for melee uptime. Preserve authored
  assignments and emergency escape; do not make every warning an immediate hazard
  or force needlessly early retreat. Current generic hints encode resolution-time
  constraints. Stack interior/group gathering now participates in native goals,
  with a flat interior and safety fallback; offline coverage includes circle and
  line stacks. Live stack acceptance and explicit marker-aware spread routing /
  prompt departure behavior remain to verify or implement.
- [ ] **Healer positioning.** When RSR's saved thresholds indicate healing is
  needed, get the relevant party members in range. The Sage run already shows
  heals reaching all four party members. Still check a real out-of-range recovery
  before calling that particular behavior verified.
- [ ] **Caster uptime.** Review the four interrupted casts found in the repaired
  BLM recording. Check whether movement required cancellation or whether a cast
  was started despite a known imminent departure. Fix the latter if confirmed.
  Check useful Ley Lines placement/return during ordinary play.
- [ ] **Micro-stepping: validate the first reproduced repair.** Mistwake trash
  samples showed walking repeatedly replaced by stationary GCD-wait plans with
  clear collision checks. The fast opportunity shortcut skipped committed-route
  comparison. Its regression failed on the old DLL and passes after the fix,
  preserving attack timing, recipients and safety. Check mob-to-mob motion live;
  this does not establish that every source of short stepping is fixed. Preserve
  small moves that mechanics actually require.

## Already repaired or implemented

- [x] AI-enabled idle efficiency. Remove empty navigation capture and idle action
  wrapping, and share plugin discovery only within synchronous observations.
  The measured idle development build holds 59.99–60.00 framework updates/second
  enabled in the scene that previously ran at 52–54. Combat measurements and the
  remaining work are recorded in the efficiency note.
- [x] Automatic target bootstrap, fresh native safety recovery and party-combat
  continuity; the NPC run now progresses through packs. Reopen for a concrete stall.
- [x] Machinist's unnecessary melee following. The recorded comparison shows
  improved spacing without losing its matched attack cadence.
- [x] Black Mage Umbral Soul capture failure. The later complete caster run has
  no recurrence and shows Umbral Soul working while movement continues.
- [x] Native walking/Sprint/mobility planning, damaging movement actions and
  productive preparations. Short hostile dashes and useful friendly relays no
  longer inherit the wrong blanket eight-yalm restriction. Actual uptime issues
  belong in the melee item above.
- [x] True North positional handling and module-immunity social-facing gate.
  Watch these during normal play; reopen an observed regression rather than
  requiring a separate test campaign.
- [x] Material interface, legacy commands except camera, saved-setting import,
  support features, diagnostics and standalone XCAI retirement. Preserve the
  imported settings and opt-outs.

## Keep the scope practical

Use the current NPC run and normal gameplay to exercise job-specific actions,
field returns, support features and lifecycle behavior. Add a task when something
actually fails or when a specific important behavior has no useful evidence.
Testing every job, every settings page, every lifecycle permutation or every
geometry combination is not a blocker for using this plugin.

Micro-stepping is follow-up repair work once diagnosed. UI polish, exhaustive
feature matrices, repeated inventory audits and another upstream trial merge for
an unrelated small fix are not current blockers. Keep useful existing tests; do
not turn this list into another general verification project.

ToF, automatic tank-following between pulls, unknown-knockback learning and the
camera subcommand stay excluded. Do not restart knockback expansion. Publishing
is a separate request. Preserve the current build during the active run; the
maintainer has explicitly authorized development reloads inside the NPC dungeon.

## Evidence, if needed

The latest validated and loaded efficiency/arrival/math DLL is MVID
`5ef2015c-af46-4f61-a5de-f28dcee251d3`. It builds and passes 129,688 native assertions,
installed RSR contract checks and nine repository tests. The latest dummy
measurement is recorded in the efficiency note; it does not establish a complete
FPS fix. AI and RSR were restored to their off state after the comparison.
The earlier gameplay
repair run used MVID `33a2f701-ded3-42f0-9c58-67d872fd1889`. Those checks support
the repairs; gameplay decides whether combat behavior is good enough.
Loaded provenance, AI following and RSR Henched were verified after reload.
Dash/backstep/Sprint effects are live-verified. Griffin's repaired run completed;
the first-boss hide report occurred before this reload and needs its own recheck.

- [Run findings and build details](docs/native-combat-assistance/regressions-2026-10-24.md)
- [AI efficiency pass and measured limits](docs/native-combat-assistance/performance-2026-10-24.md)
- [Exact pathing math, research and measurements](docs/native-combat-assistance/pathing-math-2026-10-25.md)
- [Direct arrival and large-boss positioning](docs/native-combat-assistance/deliberate-arrival-2026-10-25.md)
- [Machinist spacing comparison](docs/native-combat-assistance/machinist-range-2026-10-24.json)
- [Earlier migration history](docs/native-combat-assistance/migration-history.md)

The old detailed checklist was consolidated here. Historical notes and old open
boxes in other documents do not create additional work or release gates. Reopen
work for a concrete defect, a useful missing check, or a maintainer request.
