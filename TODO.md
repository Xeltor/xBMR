# xBMR combat assistance — remaining work

Updated 25 October 2026. XCAI is merged and retired. The current development build
is loaded and the maintainer reports that it feels much more like XCAI. Keep that
working behavior; fix concrete problems instead of expanding the migration.

## Fix and check next

- [ ] **Combat efficiency.** Idle overhead is repaired and planning allocations
  are substantially reduced. Combat frame-rate loss is still measurable with
  automatic camera enabled. The committed build passes offline checks and was
  measured live at 54.58 updates/second. Its settled camera evaluation is cheap,
  but the view changed between comparisons; the earlier expensive view and the
  remaining overall frame-rate loss still need investigation. Keep camera enabled.
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
- [ ] **Healer positioning.** When RSR's saved thresholds indicate healing is
  needed, get the relevant party members in range. The Sage run already shows
  heals reaching all four party members. Still check a real out-of-range recovery
  before calling that particular behavior verified.
- [ ] **Caster uptime.** Review the four interrupted casts found in the repaired
  BLM recording. Check whether movement required cancellation or whether a cast
  was started despite a known imminent departure. Fix the latter if confirmed.
  Check useful Ley Lines placement/return during ordinary play.
- [ ] **Micro-stepping.** Find the cause, then fix it. This remains an open defect,
  not accepted behavior. Initial samples show short walks around native goal-field
  changes; that is a lead, not a diagnosis. Preserve small moves that mechanics
  actually require.

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

The latest validated and loaded efficiency/arrival DLL is MVID
`5bf068de-fc50-4ce2-9602-946dabf47703`. It builds and passes 54,506 native assertions,
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
- [Direct arrival and large-boss positioning](docs/native-combat-assistance/deliberate-arrival-2026-10-25.md)
- [Machinist spacing comparison](docs/native-combat-assistance/machinist-range-2026-10-24.json)
- [Earlier migration history](docs/native-combat-assistance/migration-history.md)

The old detailed checklist was consolidated here. Historical notes and old open
boxes in other documents do not create additional work or release gates. Reopen
work for a concrete defect, a useful missing check, or a maintainer request.
