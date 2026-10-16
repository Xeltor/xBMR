# xBMR 7.5.6.35.8

This release repairs and clarifies **AAC Heavyweight M3 / The Tyrant (Normal)**
on upstream `7.5.6.35`. Dalamud's install version is **7.5.6.3508**.

## Encounter changes

- Predict arena restoration from Crown and warn players to leave the surviving
  island's outer strip. Timed AI constraints require reaching the overlap with
  the original floor while current bounds continue to prohibit crossing the gap.
- Use server actor identities for consistent Flatliner landing assignments
  across clients. Assign meteor soakers, preserve ordinary same-island landings
  and transfer only enough assigned players to repair a shortage below two.
- Prepare near the local meteor at the earlier two-tower cue. Avalanche then
  requires everyone on the doomed island to share an escape meteor and land on
  the surviving island.
- Replace small green pips with one outlined recommended launch wedge and an
  actionable stand-here/landing-side hint. Check the entire nominal projected
  sector against the assigned floor's interior margin.
- Mark the doomed island with danger fill and outline, and preview the later
  platform impact translucently until floor removal. Preserve its future AI
  hazard and deadline; this display change does not delay AI departure.
- Clear predictions and guidance on cancellation, source disappearance, phase
  updates and resets. Add regression coverage for assignments, launch geometry,
  native navigation, restoration and all four Avalanche variants.

The module remains **WIP**. See the [implementation notes](tyrant-normal-corrections.md),
[October 15 live-run review](tyrant-normal-live-review-20261015.md) and
[validation identities](tyrant-normal-validation.json).

## Validation

The version metadata check, all five release-version tests and the current-tree
Release build passed with SDK 10.0.401 and Dalamud API 15 development assemblies.
The build produced zero errors and six existing obsolete-gauge warnings.
All ten encounter suites passed: **5,823 assertions**, including **2,726 Tyrant
assertions**. Of the total, seven checks use 48 selected recorded Gust callbacks;
the other 5,816 assertions are synthetic. The optional private callback
projection and full original-module prediction replay were not rerun.
The recorded upstream tag matches its expected commit and is an ancestor of
this release.

## Evidence limits

The pre-repair recording includes a clear and a local restoration death; it was
then used for development and is not an untouched holdout for these repairs.
Offline checks do not establish exact physical floor edges, launch behavior,
server snapshots or successful live gameplay. No live acceptance or radar
screenshot of the corrected candidate is claimed. Party assignments depend on
players following them; the module does not control manual party members.

The BossModReborn assembly, settings, commands and IPC remain compatible.
Original licence and third-party notices remain included in the distribution.
