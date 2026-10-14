# Xel's Boss Mods Reborn (xBMR)

Xel's fork of [BossMod Reborn](https://github.com/FFXIV-CombatReborn/BossmodReborn),
based on [BossMod by awgil](https://github.com/awgil/ffxiv_bossmod).
It includes the upstream radar, mechanic hints, replay viewer, cooldown planner,
autorotation and optional AI features. The fork preserves upstream compatibility and keeps validated encounter corrections
in their existing modules. See [the encounter migration audit](docs/xcai-encounter-migration.md)
for the source changes, retired workarounds and remaining validation.
The subsequent [Shinryu floor corrections](docs/shinryu-floor-corrections.md)
document the bounded Twilight repair and its replay evidence.
See the [7.5.6.35.5 release notes](docs/release-7.5.6.35.5.md) for the complete
encounter changes and validation limits.

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

## Module maturity

Modules leave WIP when encounter coverage is supported by available evidence and
relevant offline validation passes. Personal live testing is optional. Credible
complaints or Git issues about material module defects return them to WIP until
resolved. See [the module maturity policy](docs/module-maturity-policy.md) for
promotion, reopening and archive collection rules.

## Development and releases

Build with .NET SDK 10.0.401 and current Dalamud API 15 dependencies:

```sh
DALAMUD_HOME=/path/to/dalamud dotnet build BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
```

`origin` is this fork; `upstream` is Combat Reborn. Preserve fork changes when
merging upstream; do not force-sync over them. GitHub validates builds on pushes
and pull requests. [AGENTS.md](AGENTS.md) contains the version policy, validation
commands and complete release procedure. Releases use the manual **Publish xBMR
Stable** workflow in the Forgejo feed, which derives the install version from
the tagged source and publishes the package and generated feed entry together.

Upstream's contribution policy is retained in `CONTRIBUTING.md`; AI-assisted fork
changes must not be submitted upstream against that policy. This fork is maintained
independently and is not endorsed by the upstream authors. The original BSD licence
and third-party notices remain in source and binary distributions.
