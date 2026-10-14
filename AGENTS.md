# xBMR agent instructions

This is Xel's editable BossMod Reborn fork. Read `README.md`,
`fork-version.json` and `scripts/release-version.py` before preparing a release.
Keep the `BossModReborn` assembly/internal name, settings paths and IPC compatible.
Preserve upstream licence notices. Do not submit AI-assisted changes upstream
against the policy in `CONTRIBUTING.md` or modify installed plugin binaries as
part of a source release.

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
