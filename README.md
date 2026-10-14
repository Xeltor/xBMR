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
switching distributions. The fork uses its own release versions; its first release
is `v7.5.7` (`7.5.7.0`), based on upstream commit
`be8ff771cc545f8f8b5b548b5dd7271d80460f89`.

## Development and releases

Build with .NET SDK 10.0.401 and current Dalamud API 15 dependencies:

```sh
DALAMUD_HOME=/path/to/dalamud dotnet build BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
```

`origin` is this fork; `upstream` is Combat Reborn. Preserve fork changes when
merging upstream; do not force-sync over them. GitHub validates builds on pushes
and pull requests. Publish an immutable source tag, then manually run **Publish
xBMR Stable** in the Forgejo feed with that tag, its full commit SHA and assembly
version. The workflow builds the pinned source, includes licence notices, publishes
namespaced public assets and updates only this plugin's stable feed entry.

Upstream's contribution policy is retained in `CONTRIBUTING.md`; AI-assisted fork
changes must not be submitted upstream against that policy. This fork is maintained
independently and is not endorsed by the upstream authors. The original BSD licence
and third-party notices remain in source and binary distributions.
