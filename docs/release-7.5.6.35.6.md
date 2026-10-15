# xBMR 7.5.6.35.6

This release improves **AAC Heavyweight M3 / The Tyrant (Normal)**, based on
upstream `7.5.6.35`. Dalamud's install version is **7.5.6.3506**. The
BossModReborn assembly, settings, commands and IPC remain compatible.

## Encounter changes

- Add or correct early weapon predictions, individual Stardust hits, Dance
  pulses and explosion lines, shared tankbusters, tornadoes, meteor towers,
  tank interception, two-rock shelter and repeated Heartbreak stacks.
- Model the split arena as disconnected playable platforms and constrain
  Flatliner and meteor launches to safe landing positions. During Avalanche,
  require everyone on the doomed side to use a launch tower even when occupied.
- Handle all four Avalanche visual/helper action pairs. A live failure recording
  exposed a missing variant that prevented the AI from receiving the mandatory
  evacuation instruction; that omission is repaired.
- Make overlapping cone waves readable: highlight the current wave and show
  the next wave translucently beneath it. Apply the same treatment to staggered
  ground meteors, Dance lines, weapon forecasts and portal lanes. Draw only the
  current and next waves while retaining further predictions internally.
- Preserve original hazard dimensions, future activations, AI constraints and
  manual-control authority. Simultaneous hazards such as Fire and Fury remain
  simultaneous; radar preview limits do not remove future AI hazards.

The module is reopened from Contributed to **WIP** following material mechanic
omissions and remains WIP for the documented evidence gaps. See the
[implementation and evidence notes](tyrant-normal-corrections.md) and
[validation identities](tyrant-normal-validation.json).

## Validation and remaining limits

The current-tree Release build passed with SDK10.0.401 and Dalamud API15
development assemblies, zero errors and six existing obsolete-gauge warnings.
All five release-version ordering tests and all ten encounter suites passed:
3,275 assertions in total (3,268 synthetic assertions and seven checks over48
selected archived Gust callbacks). The Tyrant suite contributes178 assertions.
The recorded upstream tag resolves to the expected commit and is an ancestor
of this release.

Build, native navigation and selected callback checks are separate from live
gameplay acceptance. Exact Normal floor edges, physical launch behavior and
server snapshot timing remain unverified. Great Wall of Fire and some Avalanche
branches lack recorded resolution coverage. The corrected Avalanche evacuation
and radar display have no successful live acceptance run or screenshot yet.
The optional earlier4,891-row private callback reconstruction was not rerun in
this release; the committed selected Gust fixture is not a full original replay.

Encounter notes preserve their development DLL identities; those hashes are not
the versioned release DLL or the public package digest. Original licence and
third-party notices remain included in the distribution.
