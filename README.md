# Xel's Boss Mods Reborn (xBMR)

Xel's fork of [BossMod Reborn](https://github.com/FFXIV-CombatReborn/BossmodReborn),
based on [BossMod by awgil](https://github.com/awgil/ffxiv_bossmod).
It includes the upstream radar, mechanic hints, replay viewer, cooldown planner,
autorotation and optional AI features. This initial release changes branding and
distribution; encounter mechanics remain those of the upstream source.

## Install

Add this custom repository in Dalamud:

```text
https://forge.xel-serv.com/xelsplugins/XelsDalamudRepo/raw/branch/main/pluginmaster.json
```

Install **Xel's Boss Mods Reborn (xBMR)**. Releases and public downloads live in
[Xel's Dalamud repository](https://forge.xel-serv.com/xelsplugins/XelsDalamudRepo/releases).
Use [xBMR issues](https://github.com/Xeltor/xBMR/issues) for fork-specific support.

xBMR retains the `BossModReborn` internal name, assembly, settings directory,
commands and IPC for compatibility with existing settings and XCAI. It replaces
BMR; run only one BossMod variant at a time. Back up your existing settings before
switching distributions. Releases follow the upstream version with one extra local revision:
`7.5.6.35.1`, `7.5.6.35.2`, then `7.5.6.36.0` on a new upstream base.
The local revision resets to zero for each upstream update and increments for
fork releases on that base, including this fork's initial branding release.
The current upstream base, exact commit and local revision are recorded in
`fork-version.json`.

Dalamud and .NET accept four numeric components. The installed version encodes
`upstream revision * 100 + local revision` in the fourth component, so release
`7.5.6.35.1` installs as `7.5.6.3501`, and `7.5.6.36.0` as `7.5.6.3600`.
Local revisions are limited to 0–99; the release validator refuses overflow.
The project version and informational version must agree with that metadata;
`scripts/release-version.py` checks this.

The bootstrap `v7.5.7` (`7.5.7.0`) release is superseded by this scheme. Anyone who
installed that bootstrap version must reinstall xBMR once, because its numeric
version sorts above the corrected version. Existing settings keep the same path.

## Development and releases

Build with .NET SDK 10.0.401 and current Dalamud API 15 dependencies:

```sh
DALAMUD_HOME=/path/to/dalamud dotnet build BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
```

`origin` is this fork; `upstream` is Combat Reborn. Preserve fork changes when
merging upstream; do not force-sync over them. GitHub validates builds on pushes
and pull requests. When updating upstream, update `upstream_version` and
`upstream_commit` in `fork-version.json` and reset `fork_revision` to zero; keep
fork changes when merging. Increment only `fork_revision` for subsequent local
releases. Update the project's `Version` and `InformationalVersion` to match,
then run `python scripts/release-version.py` and
`python -m unittest discover -s tests`. Publish the reported immutable source tag,
then manually run **Publish xBMR Stable** in the Forgejo feed with that tag and
its full commit SHA. The workflow derives the install version from the source. The workflow builds the pinned source, includes licence notices, publishes
namespaced public assets and updates only this plugin's stable feed entry.

Upstream's contribution policy is retained in `CONTRIBUTING.md`; AI-assisted fork
changes must not be submitted upstream against that policy. This fork is maintained
independently and is not endorsed by the upstream authors. The original BSD licence
and third-party notices remain in source and binary distributions.
