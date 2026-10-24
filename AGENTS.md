# xBMR agent instructions

This is Xel's editable BossMod Reborn fork. Read `README.md`,
`fork-version.json` and `scripts/release-version.py` before preparing a release.
Keep the `BossModReborn` assembly/internal name, settings paths and IPC compatible.
Preserve upstream licence notices. Do not submit AI-assisted changes upstream
against the policy in `CONTRIBUTING.md` or modify installed plugin binaries as
part of a source release.

## Encounter research: written guides first

Before creating, fixing or improving a specific encounter module, search online
for written guides to that encounter and read the relevant mechanic sections.
Confirm that each guide covers the correct difficulty, variant and phase. Use
these guides for additional context on mechanic names, sequences, alternate
patterns, arena changes and player responses before planning implementation.

Record the guide URLs and relevant findings in the encounter's implementation
notes, including disagreements with recordings or existing code. Cross-check
guide claims against encounter evidence; a guide alone does not establish exact
geometry, server snapshots or last-safe movement timing. Keep those uncertainties
explicit. If no suitable written guide is available, document the search and
continue with the available evidence without inventing missing details.

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

## Encounter radar: readable sequences and clear safe positions

When creating or correcting mechanics, ensure the radar is legible to players:
it must be clear where to stand now and how to respond next. For back-to-back or
repeating mechanics, do not draw so many future steps as full yellow hazards that
the whole arena becomes covered and the useful safe positions disappear.

Choose the clearest presentation for the mechanic. Use shaded yellow to preview
the next upcoming hazard when that helps distinguish it from the immediate
danger, or show only a limited number of steps that make the required safe
position and route clear. Include further steps only when they help players plan
the response without obscuring the current safe area. Advance the preview and
fill as hazards become imminent, and promptly remove resolved hazards. Clear or
correct previews on pattern changes, cancellations, transitions and resets.

Keep display choices separate from internal hazard predictions and AI timing.
Reducing visual clutter must not discard reliable future hazards, delay actionable
warnings, or weaken the timing, geometry and route constraints consumed by XCAI
and BMR AI. Preserve enough advance information for players to reach safety.

Validate radar readability at representative points through the sequence,
including overlaps and alternate patterns: players should be able to identify
the immediate safe position and next response without decoding an arena covered
in yellow. Record the chosen preview behavior and any unverified visual behavior
in the encounter's implementation notes.

## Module maturity: evidence-based promotion, issue-driven reopening

Apply the maturity rules below when deciding whether a module should leave or
return to WIP. Promote when relevant mechanics
and known patterns are covered, available evidence supports correctness, and
relevant offline validation and the build pass with no known material defect.
Personal live testing is optional; do not require the maintainer to test every
encounter or change. Keep offline acceptance distinct from live gameplay proof.

Return a module to WIP when a complaint or Git issue identifies a credible
material omission or correctness/safety defect, including while investigating a
credible severe report. Cosmetic requests and unrelated XCAI problems do not
qualify. Record promotion/reopening reasons and evidence or issue links. After
repair, validate the affected behavior and adjacent interactions before promoting
again; a full encounter retest is not required for every change.

## Native combat assistance

Standalone XelsCombatAI is retired into xBMR. All future assistance implementation
and releases belong here. Do not republish standalone XCAI, restore its feed entry,
or reinstate its removed IPC contracts. Existing XCAI installations are removed
automatically through Dalamud when safe; only afterward does a persistent modal
confirm completed removal. Its sole **Got it** button records acknowledgement,
never consent. Preserve legacy settings and recordings. The feed workflow removes
XCAI transactionally with the merged xBMR release. Its old source is archival and
its Forgejo Actions are disabled. Explicit maintainer authorization is required
to reverse that retirement.

Combat policies, external RSR adapters, versioned assistance preferences and the
Material interface live under `BossMod/CombatAssistance`. Normal movement and AI
following use the same native planning service. Encounter modules remain
independent of that area. Capture mutable game state on the framework thread;
workers receive frozen values and must never read live actors or Dalamud wrappers.

The former XCAI-only `BossMod.*.V1.*` contracts are intentionally retired. Preserve
the upstream public IPC listed in `tests/NativeMovementRegression/Baseline.json`.
Use typed native services internally; do not restore serialized local transport,
consumer leases or capability negotiation. RSR and vnavmesh remain external
boundaries where their features require them. ToF companion integration is excluded.

Keep independent safety, assignment and action-recipient requirements visible.
Opaque position scores are not safety proofs. Keep completed search, bounded best
found results and native recovery distinct. Do not add performance limits without
recording their origin and measuring the supported workloads. The native freshness
ceiling and real departure deadlines remain binding.

Settings import preserves opt-outs and the original legacy file. Installing
assistance never activates AI. Native activation, following, presets and manual
handoff remain authoritative. Material styling stays scoped to its own windows;
`/bmr` and native configuration identities remain intact. DevBridge owns history
and captures; the plugin exposes only bounded current-state diagnostics.

Displacement capture must retain registered source presence independently of
immunity, horizon and movement filters. Native preparation shapes implement
`IKnockbackPreparationConstraint`; an opaque or pose-dependent publisher cannot
certify authored absence. Separate only exact attributed pure preparation leaves,
retain independent constraints, and keep landing envelopes scoped to their forced
landing. Workers receive frozen native maps and values, never live producer handles.

Track the migration and modified upstream interfaces in
`docs/native-combat-assistance`. A build, source inventory or advertised capability
does not establish behavioral parity or live acceptance.

## Local development builds

After applying changes, always build the plugin successfully before handing the
result back for testing. Build the current working tree, including local changes;
do not leave a stale DLL as the reported test artifact. If the build fails, fix
errors within the task's scope or report the blocker explicitly.

Use the SDK pinned in `global.json` and compatible Dalamud API 15 development
assemblies. Configure `DALAMUD_HOME` in your environment, then build:

```sh
dotnet build BossMod/BossModReborn.csproj -c Release -p:EnableWindowsTargeting=true
```

The development plugin may watch `BossMod/bin/Release/BossModReborn.dll` and
reload automatically when it changes. Preserve the configured output path;
confirm affected clients are out of combat before rebuilding a watched output.
Use an isolated output for offline validation while combat state is unknown.
Do not copy into installed plugin directories or trigger a manual reload.
Report build success separately from live gameplay validation. Machine-specific
build environments belong in local configuration outside the repository.

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

## Repository scope and focused validation

Keep the native integration inventory concise and tests close to the integrated
code. Convert useful geometry/lifecycle regressions into native-service tests;
remove tests that only assert retired transport behavior. Do not duplicate native
safety implementations or add a general inspection framework. Keep one-off tools
outside the repository. Retain the existing release-version checker.

Regression tests are welcome when they target changed IPC behavior or encounter
fixes. Use the smallest relevant checks; do not require full BMR coverage or grow
an unrelated testing framework. Existing upstream debug and replay tools remain
available. Preserve encounter-module work when trimming agent scaffolding.
