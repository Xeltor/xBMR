# Questionable compatibility

Inspected on 2026-10-17 against the locally installed Questionable
`15.756.3.52`, including its compiled IPC client, combat module, solo-duty task
executors, plugin checklist, and three embedded presets. The public
[BossMod IPC client](https://github.com/PunishXIV/Questionable/blob/master/Questionable/External/BossModIpc.cs)
and [plugin identity helper](https://github.com/PunishXIV/Questionable/blob/master/Questionable/External/IPCUtils.cs)
provide reference context; the installed assembly was used for version-specific
checks.

## Configuration and commands

xBMR already exposes the `BossMod.Presets.*` endpoints used by this client.
The compatibility changes are built into xBMR:

- `/vbm` calls the existing `/bmr` handler, including `cfg` configuration access.
- `/vbmai` calls the existing `/bmrai` handler. In particular, Questionable's
  `/vbmai off` now reaches xBMR when it hands movement to a preset.
- `/vbm clear-maps` clears the temporary generated obstacle map. Packaged and
  user-authored maps remain available; this does not delete map files or disable
  arena boundaries or encounter hazards.
- `BossMod.Configuration` accepts `IReadOnlyList<string>` arguments and an
  optional leading `cfg`, as used by Questionable's solo-duty setting read.
  Older `List<string>` subscribers and concrete `List<string>` results remain
  supported through Dalamud's IPC conversion. `save` keeps its existing meaning.

The prefixed read matters for cleanup: Questionable reads the original
`ZoneModuleConfig.EnableQuestBattles` value before enabling solo-duty handling,
and uses that value to decide whether to disable it afterwards. This change does
not add new restoration behavior to Questionable or make its other configuration
writes temporary.

Select **BossMod** in Questionable's combat settings. No shim, second plugin,
identity spoofing, or Questionable binary modification is needed. xBMR retains
its `BossModReborn` assembly, internal name, settings directory and existing
commands. Continue running only one actual BossMod variant at a time.

## Identity notices

Questionable detects `BossModReborn` independently and displays an incompatibility
notice. Its plugin checklist looks for the original `BossMod` internal name and
can show that plugin as missing. These indicators remain visible.

In the inspected version, the combat module's `CanHandleFight` uses
`BossModIpc.IsSupported()`, which checks the preset IPC function. Solo-duty
eligibility also uses `IsSupported()`. Neither path requires the plugin-name
version check in `IsReady()`. The combat selector remains selectable even when
the checklist cannot find the original plugin. These conclusions are specific
to the inspected client and should be rechecked when Questionable changes.

## Validation

The current xBMR Release build passed with six existing obsolete-gauge warnings
and no errors. Five existing Python tests passed.

The focused compatibility runner passed **74 assertions**, using the built xBMR
DLL, installed Dalamud API 15 call-gate implementation, and installed Questionable
DLL. It verifies collection-expression and legacy configuration subscribers,
prefixed reads and writes, runtime-only versus saved writes, notifications,
invalid requests, and lossless import/serialization of all three real presets:
Overworld, QuestBattle and NormalMovement. All preset modules, tracks, options
and serialized target fields survive without import diagnostics.

Run it after building xBMR:

```sh
DALAMUD_HOME=${DALAMUD_HOME} \
  dotnet run \
  --project tests/QuestionableCompatibility -c Release -- \
  BossMod/bin/Release/BossModReborn.dll \
  ${DALAMUD_HOME} \
  "$FFXIV_GAME" \
  ${QUESTIONABLE_DLL}
```

This is source, build, IPC-conversion and preset-format evidence. It does not
prove live quest completion, movement handoff, encounter safety or a successful
development-plugin reload. The live bridge was not callable in this chat, so
those checks remain open. This is compatibility with the inspected Questionable
contract, not a claim of complete compatibility with every VBM API or behavior.
