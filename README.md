# Xel's Boss Mods Reborn (xBMR)

Xel's fork of [BossMod Reborn](https://github.com/FFXIV-CombatReborn/BossmodReborn),
based on [BossMod by awgil](https://github.com/awgil/ffxiv_bossmod).
It includes the upstream radar, mechanic hints, replay viewer, cooldown planner,
autorotation and optional AI features. The fork preserves upstream compatibility
and keeps encounter corrections in their existing modules.

## Install

Add this custom repository in Dalamud:

```text
https://forge.xel-serv.com/xelsplugins/XelsDalamudRepo/raw/branch/main/pluginmaster.json
```

Install **Xel's Boss Mods Reborn (xBMR)**. Releases and public downloads live in
[Xel's Dalamud repository](https://forge.xel-serv.com/xelsplugins/XelsDalamudRepo/releases).
Use [xBMR issues](https://github.com/Xeltor/xBMR/issues) for fork-specific support.

xBMR retains the `BossModReborn` internal name, assembly, settings directory,
commands and upstream IPC for compatibility with existing settings and integrations. It replaces
BMR; run only one BossMod variant at a time. Back up your existing settings before
switching distributions. Releases follow the upstream version with one extra local revision:
`7.5.6.35.1`, `7.5.6.35.2`, then `7.5.6.36.0` on a new upstream base.
The local revision resets to zero for each upstream update and increments for
fork releases on that base, including this fork's initial branding release.
The current upstream base, exact commit and local revision are recorded in
`fork-version.json`.

The version shown by Dalamud combines the upstream and local revisions. Existing
settings keep their original directory when switching from BMR.

The bootstrap `v7.5.7` (`7.5.7.0`) release is superseded by this scheme. Anyone who
installed that bootstrap version must reinstall xBMR once, because its numeric
version sorts above the corrected version. Existing settings keep the same path.

## Module maturity

Modules leave WIP when encounter coverage is supported by available evidence and
relevant offline validation passes. Personal live testing is optional. Credible
complaints or Git issues about material module defects return them to WIP until
resolved. Promotion and reopening rules are in [AGENTS.md](AGENTS.md).

## Questionable compatibility

Select **BossMod** as Questionable's combat module. xBMR provides the shared
`BossMod.*` preset IPC and the `/vbm` and `/vbmai` command aliases itself; no
companion plugin is required. Questionable may still display its BossMod Reborn
warning and mark the original BossMod plugin as missing. The inspected version's
combat and solo-duty paths check the IPC directly, independently of those notices.
Compatibility findings and remaining gameplay validation belong in the relevant
[xBMR issue](https://github.com/Xeltor/xBMR/issues).

## Native combat assistance

XCAI combat assistance is integrated into xBMR’s native movement planner. Combat
preferences participate in route and action selection before movement begins.
Supported behavior depends on current action evidence and authored encounter
constraints; dungeon and job-specific live validation continues. Standalone XCAI
is retired and must not be installed alongside this version.

Use `/bmrai` to open the Material settings window. `/bmrai on`, `/bmrai off`,
`/bmrai toggle` and `/bmrai follow Slot2` retain their operational meanings.
Follow also accepts a party member name. `/bmrai ui`
toggles the native AI debug view. `/bmr` retains its encounter, preset, replay and
configuration views.

XCAI has merged into xBMR. When this version finds an installed standalone XCAI,
it removes XCAI through Dalamud once you are logged in and out of combat, casts,
loading screens and cutscenes. Removal is mandatory and includes disabled installs.
After removal succeeds, a modal popup confirms that XCAI has merged into xBMR and
has been removed. It stays open until you press **Got it**. That button only
dismisses the completed-migration notice; removal does not wait for it. The notice
is not repeated after acknowledgement. Saved settings and recordings are
preserved. Assistance and settings import wait for removal
to finish. Development copies have startup and automatic reload disabled and
their game registration removed; development source files are preserved.

The old `/xcai` commands remain available after standalone XCAI is removed:

| Command | Behavior |
| --- | --- |
| `/xcai`, `/xcai toggle` | Toggle the **Combat assistance** setting. |
| `/xcai on`, `/xcai off` | Enable or disable combat assistance. Native AI activation remains controlled by `/bmrai`. |
| `/xcai config` | Open the Material settings. |
| `/xcai notes` | Open patch notes. |
| `/xcai knockback on`, `/xcai knockback off` | Set the shared knockback immunity preference; the migration's remaining knockback limitations still apply. |
| `/xcai check-ipc` | Report current native movement/action status. |
| `/xcai check-recording-ipc` | Report current recording-sharing status. |
| `/xcai party on`, `/xcai party off` | Explain the retirement of automatic between-pull following. |

Ley Lines returns compare walking, Between the Lines and Retrace within the
same safe movement plan. Supported spell timing includes recast benefits for
instant spells. Retrace keeps the active field's remaining duration. Its setting
remains separate from walking returns and gap closers. Placement uses current native safety and action evidence. Live encounter
validation continues.

Pictomancer can return to an observed covered spot in Starry Muse when the
selected spell's observed cast or recast improves and the complete route remains
safe. The full Inspiration area is still being verified; its separate party-buff
radius is not used as the landscape boundary. **Use Smudge to return** permits
Smudge for this benefit. Turning it off keeps walking available and does not
disable Smudge for independent safety or attack-range recovery. Global movement
ability and Pictomancer ability switches still apply.

Combat camera options are configured in the Material interface.

The compatibility commands use the same settings as the interface. They yield to
standalone XCAI's command handler while that plugin is loaded.

Installing or opening the settings does not start AI. Existing activation,
following, presets, movement restrictions and manual handoff still apply. Combat
preferences participate in the native route choice before movement starts;
encounter constraints and the current attack or heal opportunity take priority
over spacing and presentation. New hazard, target or action evidence can require
a new plan.

RotationSolver Reborn supplies selected-action evidence for assisted attack,
healing and movement choices. Native BMR rotations keep their own movement goals.
With RSR unavailable, RSR-specific enhancements are unavailable; native BMR safety
and goals remain available.

Knockback assistance uses authored BMR mechanics for preparation, landing and
recovery. Prediction and learning of unknown knockbacks are excluded. Original
standalone knowledge files are retained for rollback and are not imported. Native
effect and physical-completion observation stays local to the current encounter;
unsupported geometry or uncertain timing retains native recovery.
For supported native plans, immunity follows RotationSolver’s original action
checks and waits for buff confirmation. Saved action settings and explicit
commands retain priority. General knockback planning is still being completed.

AoE target improvements are still being completed for linked RotationSolver
mode. Supported circular, cone and line attacks can now supply alternative targets to the same
movement planner, with native confirmation before execution. **Pick better AoE
target** can choose a supported circular attack's target while **Move for better
AoE hits** is off, provided that the better target is already usable from the
current position. Extra hits cannot delay the current action or lose its required
recipients. It also works with **Assist automatic movement** off: target changes
stay stationary and yield to any required native movement. A native AI or preset
must already be active. Linked-mode delivery, directional attacks and recovery
behavior are still being validated. Native encounter targets, explicit targeting
strategies and manual target changes retain control.

Black Mage's **Choose Ley Lines placement** can prepare a safer position as part
of automatic movement. A verified route must fit the current spell timing, and
RotationSolver must approve the ability before that route starts. Missing or
stale preparation keeps RotationSolver's normal placement. Improved placement
under encounter mechanics is still being validated.

Red Mage's **Move for melee combo** preference uses observed combo and supported
rotation intent to prepare a safe single-target or AoE position. It preserves the
selected spell and target, accounts for the next GCD slot and original combo
expiry, and keeps AoE recipients separate. Unsupported intent leaves current
native goals in control. Actual job and movement-ability delivery still needs
in-game validation on this development branch.

The first startup imports the standalone settings when integrated settings do
not already exist. The original XCAI configuration is retained. Existing native
BMR settings remain in their original configuration. If standalone XCAI is
loaded, integrated assistance stays inactive to avoid conflicting ownership.

In **Dungeons & AoE**, **Keep up with dungeon pulls** helps maintain contact
with an observed moving tank gathering enemies while you are in combat. It
stops when combat ends and does not activate AI. Automatic tank following between
pulls has been retired; its old saved setting is ignored. Use the native follow
controls in **General** when you explicitly want to follow a party member.

The decision overlay uses in-world markers for the selected destination, relevant
action stops and party contact. Diagnostics distinguish a completed search,
a bounded best-found result and native recovery. Search completion describes the
available evidence; it does not predict unknown mechanics or future attacks.

Upstream's contribution policy is retained in `CONTRIBUTING.md`; AI-assisted fork
changes must not be submitted upstream against that policy. This fork is maintained
independently and is not endorsed by the upstream authors. The original BSD licence
and third-party notices remain in source and binary distributions.
