# xBMR 7.5.6.35.5

This release includes the changes since `v7.5.6.35.4`, based on upstream
`7.5.6.35`. Dalamud's install version is **7.5.6.3505**. The BossModReborn
assembly, settings, commands and IPC remain compatible.

## Encounter changes

- **Promathia:** leave physical teleport pads during System Lock and after the
  last Memory Receptacle dies; represent all six endpoints of the paired
  post-island Heavy pools; forecast Deadly Rebirth's directional knockback from
  the early visual and constrain unsafe landing positions. The material safety
  report reopens the module as WIP; this repair does not establish complete
  encounter acceptance. [Evidence and limits](promathia-phase-return.md).
- **Shinryu Paradox:** ordinary movement on either Phase 1 floor avoids its
  transition, including during Starflare. Required Breath, Tail and Twilight
  floor changes remain available; arriving on the required floor immediately
  prevents an unwanted return. Phase 2 has no transition restriction.
  [Evidence and limits](shinryu-starflare-transitions.md).
- **Dun Scaith:** preserve Deathgaze proximity warnings through impact, cover
  Scathach's later Wings preparation and Hound variants, and repair tower
  generation, expiry and bootstrap behavior. Available evidence and known
  response review support Verified maturity for Deathgaze, Ferdiad, Scathach
  and first-form Diabolos. Proto Ultima and Diabolos Hollow remain WIP.
- **Eden Prime, normal:** bound cinematic prediction refinement and retire
  landing hazards using actor generations and actual targetability. Known
  normal responses meet the evidence-based Verified policy.
- **Aloalo, variant:** independently corroborate Quaqua and Ketuduke arena
  geometry; prevent mid-dash Quaqua bootstrap from moving the arena. Incomplete
  route coverage keeps Quaqua WIP and returns Ketuduke to WIP.

The committed [acceptance follow-up](encounter-acceptance-followup.md) contains
the whole known-response reviews, detailed corrections and remaining limits.
Verified maturity is distinct from AISupport or live automatic execution proof.

## Validation and remaining limits

The full current-tree Release build passed with SDK 10.0.401 and Dalamud API 15,
zero errors and six existing obsolete-gauge warnings. All five release-version
ordering tests and all nine encounter regression suites passed, totaling
3,097 synthetic assertions against the fresh versioned DLL. The test executable
reports two existing nullable warnings. The upstream tag resolves to the recorded
commit and is an ancestor of this release.

Encounter implementation notes retain their historical build and replay
identities; those development DLL hashes are not the versioned release DLL.
Recording/replay checks and synthetic navigation checks do not establish exact
server snapshots, last-safe departure times or live BMR/XCAI encounter success.
Promathia immunity behavior and exact Heavy thresholds, Shinryu physical
transition boundaries, and previously documented incomplete encounter coverage
remain explicit limits. Original licence and third-party notices remain included
in the distribution.
