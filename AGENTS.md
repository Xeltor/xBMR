# xBMR agent instructions

This is Xel's editable BossMod Reborn fork. Read `README.md`,
`fork-version.json` and `scripts/release-version.py` before preparing a release.
Keep the `BossModReborn` assembly/internal name, settings paths and IPC compatible.
Preserve upstream licence notices. Do not submit AI-assisted changes upstream
against the policy in `CONTRIBUTING.md` or modify installed plugin binaries as
part of a source release.

## New encounter modules: arena first

For fights with no module at all, the first implementation priority is a minimal
module with accurate arena boundaries. Walking off a lethal arena edge means
death at any point in the fight, so establish the playable area before building
detailed mechanic handling or optimizing combat movement.

Verify the arena center, shape, size, lethal edges, holes, and any phase-dependent
boundary changes from encounter evidence. Ensure these bounds constrain movement
and pathfinding, rather than only drawing the arena. Do not infer a safe boundary
from where recorded players happened to stand; keep unverified geometry explicit.
Validate the bounds throughout the fight, including transitions and resets.
An arena-only module is useful initial coverage, not complete mechanic support or
proof of live gameplay safety.

## Encounter mechanics: early warning, safety first, greed second

For every new or corrected mechanic, show actionable warnings and predicted
hazards early enough for players, XCAI and BMR AI to react, plan and reach safety.
Use the earliest reliable encounter cue; do not wait for damage, the final visual
telegraph or an unnecessarily late component activation when earlier evidence
identifies the mechanic. Keep uncertain predictions explicit and update or clear
them when the pattern resolves, changes, cancels or resets.

Provide accurate internal timing through the module's state machine, components
and AI hints, not only drawings or text. Distinguish warning availability, hazard
activation, server snapshot/last-safe departure, resolution and hazard expiry.
Verify these from encounter evidence; cast finish or damage time alone does not
prove the snapshot or last-safe movement time. Account for travel distance, route
constraints, cast/action locks and latency when deciding how much warning and
movement margin is needed. Early visibility must not force premature movement
when a verified future activation time allows safe uptime.

Allow XCAI and BMR AI to preserve uptime and finish safe casts/actions only when
the known timing and safe route support it. Safety is the first priority; greed
is second. If timing, geometry, pattern, route or evidence freshness is uncertain,
use conservative margins and move safely rather than gamble on an unverified
greed window. Never weaken arena bounds, hazards, assignments or manual control
to gain uptime.

Validate warning lead time and AI timing behavior as well as mechanic resolution,
including alternate patterns, overlaps, transitions, cancellations and resets.
Check that warnings arrive before the last safe response and that both AIs can
consume the timing without late dodges or needless early departures. Document
unverified timing and keep replay/build success separate from live gameplay
acceptance.

## Local development builds

After applying changes, always build the plugin successfully before handing the
result back for testing. Build the current working tree, including local changes;
do not leave a stale DLL as the reported test artifact. If the build fails, fix
errors within the task's scope or report the blocker explicitly.

Use the SDK pinned in `global.json` and compatible Dalamud API 15 development
assemblies. On this workstation, the portable SDK and build command are:

```sh
DALAMUD_HOME=/home/xeltor/.xlcore/dalamud/Hooks/dev \
  /home/xeltor/.local/share/xbmr-dotnet/dotnet build \
  BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
```

The Dalamud development plugin targets this build output directly:
`/home/xeltor/Projects/xBMR/BossMod/bin/Release/BossModReborn.dll`.
Keep that output path stable. Dalamud automatically reloads the development
plugin when the DLL is rebuilt; do not copy it into installed plugin directories
or trigger a manual reload. Report build success separately from live gameplay
validation.

## Release version policy

The upstream release version is authoritative. Do not independently bump its
major, minor, patch or revision for fork changes.

- Release labels and immutable source tags use `X.Y.Z.U.F` and `vX.Y.Z.U.F`.
  `X.Y.Z.U` is the exact upstream release tag; `F` is the local fork revision.
- For a new release on the same upstream base, increment only `fork_revision`.
  For example, `7.5.6.35.1` becomes `7.5.6.35.2`.
- When adopting a newer upstream release, update `upstream_version` and its exact
  tag commit in `fork-version.json`, and reset `fork_revision` to **0**.
  For example, the next upstream base becomes `7.5.6.36.0`.
- Fetching upstream, retrying publication of an existing tag, or documentation
  edits alone do not automatically create a new version. Preserve fork changes
  when merging upstream; never force-sync over them.
- Dalamud/.NET use four numeric parts: `X.Y.Z.(U * 100 + F)`.
  Thus `7.5.6.35.2` installs as `7.5.6.3502`, and `7.5.6.36.0` as
  `7.5.6.3600`. Keep `F` within 0–99; never silently carry overflow into upstream
  version fields.
- Keep `BossMod/BossModReborn.csproj`'s `Version` equal to the encoded four-part
  version and `InformationalVersion` equal to the five-part release label.
  `scripts/release-version.py` validates these against `fork-version.json` and
  reports the source tag and install version.
- Do not use the feed's generic `calculate-version.py` for xBMR; its independent
  three-part SemVer policy is for other plugins.

## When the user asks to release

1. Inspect repository status, existing source tags and public feed/release state.
   Preserve unrelated changes. Decide whether this is a new release or a retry
   of an already published immutable tag. A release request alone does not
   require merging a new upstream base.
2. Apply the version policy above. Verify the upstream tag resolves to the
   recorded `upstream_commit` and that the release source contains that base.
   Never reuse a published tag for different source or overwrite release assets.
3. Validate the metadata, ordering tests and Release build:

   ```sh
   python scripts/release-version.py
   python -m unittest discover -s tests
   dotnet build BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
   ```

   Use the SDK pinned in `global.json` and set `DALAMUD_HOME` to compatible
   Dalamud API 15 development assemblies. A compatible portable SDK is acceptable;
   do not downgrade generator dependencies to accommodate an older compiler.
4. Commit only owned paths with Conventional Commits and push to `Xeltor/xBMR`.
   Create and push the exact source tag reported by `release-version.py` at the
   intended release commit. For a retry, reuse its original commit and tag.
5. Dispatch **Publish xBMR Stable** (`publish-xbmr.yml`) on `main` in
   `xelsplugins/XelsDalamudRepo` on `https://forge.xel-serv.com` with only:
   - `source_tag`: the immutable five-part source tag;
   - `source_sha`: its full 40-character commit SHA.

   The API route is
   `/api/v1/repos/xelsplugins/XelsDalamudRepo/actions/workflows/publish-xbmr.yml/dispatches`.
   Set dispatch `ref` to `main`. The workflow derives the install version from
   the tagged source, validates the upstream tag, builds, packages licence notices,
   and publishes `BossModReborn-vX.Y.Z.U.F` public assets in the feed repository.
   Its scoped OIDC authorization is already configured for this manual workflow.
6. Wait for publishing to succeed. Verify the anonymous public ZIP/manifest and
   persisted feed entry have the expected name, install version and download URL.
   Preserve every other plugin entry. Never manually edit generated
   `pluginmaster.json` to bypass publication. Report release and validation links;
   separate build/publication proof from live gameplay acceptance.

The legacy bootstrap `v7.5.7` / `7.5.7.0` is superseded. Never use it as the base
for future version calculation. Its one-time reinstall note is in the README
and release notes; preserve its existing tag and assets.
