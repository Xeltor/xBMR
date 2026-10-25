# xBMR 7.5.6.35.23

Upstream remains `7.5.6.35` at
`be8ff771cc545f8f8b5b548b5dd7271d80460f89`. Dalamud install version is
`7.5.6.3523`. This release includes all pending assistance changes approved
for publication by the maintainer.

## Changes

- Treat unknown mechanic activation as immediate. Required native escapes no
  longer wait for optional combat optimization. Preserve real finite timers
  and the current cast window when the complete safe route permits it.
- Assess complete emergency walking routes against independent warnings and
  prefer fewer predicted exposures. Explicit lower-exposure panic holds remain
  separate from safe arrival. Unbridled jump preparation no longer closes a
  legal nearby escape from the current attack.
- Improve combat and healing contact, walking progress, mobility comparisons,
  job-aware movement cost, mechanic clearance, stack gathering, and arena/enemy
  endpoint preferences. Preserve native safety and assignments above preferences.
- Improve configured healing delivery and party coverage, Earthly Star returns,
  Sage pack approach, permitted FATE acquisition, and Phantom mobility handling.
- Refine social spacing and camera/manual handoff. Reuse RSR action adapters and
  add bounded current-state diagnostics for movement and frame delays.
- Reject stationary unsafe recovery being reported as arrival. Add Serpent
  Labyrinth pattern/collision diagnostics and regressions; physical maze escape
  remains unresolved and is not claimed fixed.

## Validation and remaining limits

Release validation uses the pinned .NET SDK and Dalamud API 15 references,
`python scripts/release-version.py`, `python -m unittest discover -s tests`,
and the Release plugin build. The build passed with zero errors and six
existing obsolete-API warnings. All 9 Python tests and all 20 compiled regression
suites passed, including 143,427 native movement assertions and 89,705 native
module assertions.

Build, offline checks, public package verification and client auto-reload do
not establish live encounter acceptance. Individual evidence and remaining
limits are recorded in the linked implementation notes in
[implementation-status.md](implementation-status.md). Existing saved opt-outs,
manual control, native activation, upstream public IPC and settings identities
remain authoritative.
