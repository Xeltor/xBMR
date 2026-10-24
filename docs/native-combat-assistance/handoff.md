# Native combat assistance: implementation handoff

2026-10-24. The retained systems have been reintroduced into xBMR. Following the
implementation handoff, the maintainer explicitly authorized committing everything
and releasing 7.5.6.35.19, removing standalone XCAI from the feed, disabling its
publication flows and recording retirement in agent instructions. The versioned
source tag and public release workflow identify the released source. Historical
checkpoint and upstream-trial receipts below retain their original scope; build
and publication are distinct from live dungeon gameplay acceptance.

## What is ready

One native movement owner, immutable action/safety input, joint route/action/
resource search, original deadlines and exact execution validation now serve
normal and follow movement. Native constraints, assignments, current targets,
individual recipients, activation, presets and manual handoff remain authoritative.
The former standalone arrival/IPC transport is removed. All 72 XCAI-only
registrations are absent; the 67 recorded upstream names remain.

Retained systems include positional/range/AoE targeting, job mobility, BLM field
placement/return/relocation, RDM combo movement, PCT observed-field return,
support/healing/mitigation, utility zones, tank stance/recovery, interrupts/stuns,
combat-only pull contact, social/facing preferences, camera, diagnostics/overlay
and opt-in fight sharing. The Material interface and legacy commands use shared
native settings. Import preserves opt-outs and the original rollback file;
standalone coexistence prevents conflicting assistance.

The last code fix makes the first walking publication include useful after-action
zone return, preserving the current spell. The exhaustive-search comparison
regression passes. The original standalone DeferLeyLinesPlacement callback has a
direct native RSR selection replacement; it is not a missing feature.

## Intentional boundaries

ToF, automatic tank following between pulls, unknown-knockback learning/projection
and `/xcai camera` are excluded. Camera settings and explicit native follow remain.
Further authored-knockback expansion/polishing is deferred at the user's request;
the verified native implementation and recovery remain. Unknown field footprints
and action timing are reported as bounded evidence, not fabricated geometry or
haste. Full Inspiration radius and integrated field delivery remain gameplay checks.

A deferred source-level native knockback finding is retained for later work:
`GenericKnockback.CalculateMovements` uses a StopAtWall Clamp whose upper bound
can be below its lower bound for actor radius below 0.5. Expected behavior is
bounded wall clearance; current code can throw for that input. No live failure
was established. The unfinished shared-projection refactor was undone rather
than expanding knockback work during this completion pass.

## Offline evidence

- Final build: zero errors, six existing upstream gauge warnings.
- 42,863 native assertions, including independent small-map reference comparisons,
  recipient/safety priority, locks/resources, ownership, settings and lifecycle.
- All 19 broader suites passed against the exact final DLL.
- Installed RSR 7.5.7.6 contracts passed; this proves reflection/eligibility surface,
  not actual rotation priority or gameplay delivery.
- Nine repository tests, all inventory parsing and whitespace checks passed.
- All 100 legacy settings and 385 source files have explicit native responsibility,
  replacement, exclusion or retirement mappings. A subsystem mapping does not
  assert that every standalone helper was copied or independently live-tested.

Final DLL SHA-256:
`b30830f7f14deedf057b8af82b176045ddee06ad83ba3daeb4e8b381497f5226`

Final MVID: `ba9fbd65-74d7-47b9-897e-32e05b8db224`.
Assembly version remains `7.5.6.3518`; no metadata/feed bump was made.

## Actual newer-upstream trial

Current source snapshot: `f8997079ef43c814f0eec739fb0f317f09f1c173`.
Newer upstream: `ae64e3309905dafad0952dbe72680add167591f9`.
Isolated merge: `5dfa5ddd72704b8ec3463b33782a5a375269787c`, zero conflicts.

The merged source built with zero errors/six existing warnings, then passed the
same 42,863 native assertions and all 19 broader suites. Both result receipts
match their exact DLL hashes. All 229 assistance/test source files match after
normal checkout line-ending conversion. This validates the native compatibility
boundaries; it does not claim live acceptance of upstream encounter changes.
Main ancestry/version and the main working tree were not replaced by the trial.

Trial DLL SHA-256:
`20ea02a893ae02548b3c96311269736b32d6afd26f2c8bf799e4923dc917e899`

Trial MVID: `56547c88-c6c7-4560-a35b-0aa5905b2bff`.

## Next: load and dungeon testing

The final DLL is an isolated build. This completion pass did not deploy it,
reload the client, run game commands or perform idle-client experiments. Before
using it in a dungeon, a later development deployment must freshly verify all
affected clients are out of combat, let the normal watcher reload and verify the
loaded MVID, health and settings ownership. Do not use `/xlreload` or assume an
older loaded checkpoint contains this final code.

Then test actual movement/action delivery: melee/ranged recovery, AoE/healing
recipients, tank facing/contact, mobility/locks/charges, job field returns and
RDM combos. Check manual handoff, explicit follow/presets/Questionable, death/
resurrection, mounted pauses, logout/unload, Material controls/commands, coexistence,
restoration, camera, overlay and sharing. TODO.md contains the scenario checklist.
The user's dungeon supplies the next concrete failures to correct; do not restart
a speculative implementation audit first.

No release/feed publication, standalone uninstall or companion change occurred.
