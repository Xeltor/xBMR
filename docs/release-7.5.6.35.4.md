# xBMR 7.5.6.35.4

This release includes the complete pending xBMR source changes, based on upstream
7.5.6.35. Dalamud's install version is **7.5.6.3504**. The BossModReborn assembly,
settings, commands and IPC remain compatible.

## Encounter changes

- **Dun Scaith:** substantial repairs across Deathgaze Hollow, Ferdiad Hollow,
  Proto Ultima, Scathach, Diabolos and Diabolos Hollow. Deathgaze gains playable
  deck boundaries, ice-block and seal handling, knockback landing checks and
  missing floor/hazard forecasts. Other bosses gain earlier mechanic warnings,
  independent helper/target retirement, corrected overlaps, add/gate priorities,
  stacks, gaze, towers, multi-hit Nox and gate urgency. Portal participants receive
  a conservative automatic-movement pause with manual movement available while
  portal geometry remains unverified. [Detailed coverage and limits](dun-scaith-corrections.md).
- **Promathia:** ring warnings follow the boss when he relocates, fixing the
  contradictory ring/cleave overlap. After the local Memory Receptacle dies,
  observed enabled pads provide optional routing to a surviving island; guidance
  waits locally at the entrance and clears through System Lock and phase reset.
  [Ring correction](promathia-ring-overlap.md), [island support](promathia-island-support.md).
- **Doomtrain:** event-driven encounter progression, floor-aware hazards and
  corrected car bounds; Arcane Revelation forecasts final explosions rather than
  treating the travelling indicator as continuous danger. Upper-deck movement
  instructions describe observed routes and retain explicit physical limits.
  [Corrections and validation](doomtrain-corrections.md).
- **Quaqua, variant:** add left-path hammer landings and Scalding Waves, including
  repeat/hazard cleanup. Preserve early armament previews while limiting immediate
  danger treatment to the final response window. Middle/right paths remain
  unsupported. [Coverage](quaqua-variant.md).
- **Eden's Gate: Resurrection, normal:** new module with asset-supported square
  arena bounds and handling for tank hits, spreads, tank lines, Delta Attack,
  corner attacks, beam/ground-circle overlaps, add landing/rear positioning and
  early cinematic raidwide warnings. It remains WIP
  with specific missing mechanics. [Evidence and omissions](eden-prime-normal.md).
- Include the current Al Zahbi validation notes, encounter regression projects,
  written-guide instructions and [module maturity policy](module-maturity-policy.md).

## Validation and remaining limits

The combined source tree passed a full Release build with SDK 10.0.401 and
Dalamud API 15: zero errors and six existing obsolete-gauge warnings. All five
release-version ordering tests and all nine encounter regression suites passed,
with 2,834 synthetic assertions. Encounter notes retain the original artifact identities and native replay
results from development; those historical DLL hashes are not the release DLL.

Dun Scaith's final development checks passed 642 synthetic assertions and 730
native replay contract assertions across 269 checkpoints. A fresh replay run
against the isolated release build passed 729 contract assertions across those
269 checkpoints; its checks omit the historical development PDB/runtime assertion.
All 49 Dun Scaith/core source documents match the final development snapshot,
and the release replay reports contain no missing samples or module errors.
These verify the
modeled behavior in available evidence, not every physical boundary or live
BMR/XCAI outcome. Known gaps include several Ferdiad/Proto hazard boundaries,
later Scathach knockback patterns and Diabolos portal geometry. Critical Gravity
keeps conditional manual interception guidance rather than inventing a safe
automatic assignment. Eden Prime and Quaqua likewise retain documented omissions.

These incomplete modules remain WIP. Build, replay and publication success are
reported separately from live gameplay acceptance; personal live testing is
optional under the maturity policy. Original licence and third-party notices
remain included in the distribution.
