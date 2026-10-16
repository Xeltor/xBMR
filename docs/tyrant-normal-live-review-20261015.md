# Tyrant Normal live-run review — 2026-10-15

## Scope and provenance

Reviewed the maintainer's WHM clear in territory1324/CFC1072, UTC
15:52:41–16:02:14 (about572.84s). The maintainer reports being the only XCAI
player. They saw green pips around the boss, but did not find them actionable.

- Original BMR recording: `b2d6d4d31ed22574ddd9569a` (bridge-local ID).
- Original copied-prefix SHA256: `b0c30924e3d54dc05dd4b166c993be876ac1e70cde059d089ea9c538eac8068c`.
-3269890 bytes; recording format31; game2026.10.06.0000.0000.
-225965 retained operations; decoded snapshot to EOF; no parse/container error or
 truncation. Omitted operation kinds remain explicit in the bridge response:
 cooldowns, animation locks, move-speed changes and several inventory/stat events.
- XCAI combat archive: `8dc26acbd081405286361ebdcc5a4336`, revision
 `bcbdcfeae9f24bfd8ba935ef8f7ed7d3`;2252 snapshots; no partial/truncated start,
 dropped-from-start frames, write errors or unreadable files reported.
- Loaded XCAI MVID: `df81f45f-b896-4d99-9a9b-c90d57828116`.
- Reviewed pre-repair DLL SHA256: `ffe7dfbfdac9cba162ea48a7db2c7cdea67a6d664145aaa2bf78bfd955394ea9`.
- Dalamud log confirms the xBMR development path was enabled with automatic reload,
 and completed a reload at17:38:11+02, before this pull. Recording format31 does
 not identify its writer build; the replay parser version is not writer proof.

BMR timestamps are recorded local DateTime values without an encoded timezone.
The local UTC+02 mapping is corroborated by the diagnostic fight boundary and
matching player positions/events. Exact strings and sequence order are retained
below; these are client observations, not measured server snapshots.

## Positive findings and limits

The boss died at18:02:14.1885144, seq222375, followed by completion director
updates. Eleven party death events occurred, including one local WHM death.
That is a clear, not a clean AI acceptance run.

At17:58:16.9970529, the post-Flatliner frame
shows seven players west and one east. The sole XCAI player's local split does
not control the seven other players. In the four-tower wave, the east-south
source's46061 event (seq156699) had zero targets. Two other players fell near
the gap after their launches (death seq158349/158391); their input/automation
ownership is unknown. At17:58:55.9883318, the six survivors occupied west4/east2.
This supports the need for party coordination, not a guaranteed all-party plan.

Avalanche46069 began at17:59:18.1931615 (seq168824), targeting east.
The east floor was removed at17:59:29.4970139 (seq173857, director16).
Two players still on east fell (seq174687/174741). The local WHM was already
west and survived that removal. No post-fix doomed-side local AI evacuation
trial occurred in this run.

## Local WHM death: restoration omitted advance movement

| Recorded local time | Sequence | Observation |
|---|---|---|
|17:59:42.3359656|179189|Crown46006 starts; total4.7s, elapsed0.0126418s.|
|17:59:46.5644711|179977|Local HP192385/192385.|
|17:59:47.3277248|180101|Crown effect contains63571 damage to local WHM.|
|17:59:48.1881066|frame|Local actor alive at(75.956154,0,89.544624).|
|17:59:48.3177453|180209|Director0x8000000D,param1=1 restores full arena.|
|17:59:48.3177453|180210|Same XY position starts falling, Y=-0.0071688774.|
|17:59:48.4080606|180231–180233|Y=-0.18875015, HP0, dead.|
|17:59:49.9923385|181128|Y=-31.99409 at unchanged X/Z.|

The damage and later floor fall are separate observations. Full HP before Crown,
alive state after its effect, immediate falling at restoration and subsequent
zero HP support restoration as the lethal failure. The prior module switched
bounds on the director update but supplied no earlier movement requirement.
Selected diagnostics show no manual suppression during the final movement and
waiting interval; the WHM stopped around17:59:21.6 and remained on that outer
strip until falling. This is an encounter-module omission, not an XCAI workaround.

## Corrections and validation

Re-read the correct Normal written guides before implementation:
[Wiki](https://consolegameswiki.com/wiki/AAC_Heavyweight_M3),
[Icy Veins](https://www.icy-veins.com/ffxiv/aac-heavyweight-m3-normal-raid-guide).
Both place arena restoration at Crown after Avalanche. Neither supplies exact
physical edges or a server deadline. No disagreement on this sequence was found.

ArenaChanges now predicts restoration from Crown while split/destruction state
is active. It previews the surviving outer6y strip, gives an inward warning and
adds a timed requirement for the original floor with a0.75y interior margin.
Current island bounds remain authoritative until the director update, so the AI
must reach the overlap without walking across the gap. The deadline is native
cast finish minus0.3s, retaining the navigator's existing1s reserve. This is an
early conservative policy; the observed restoration followed Crown damage by
about0.99s. Cast finish does not clear the prediction; cancellation, actor
removal, reset and the actual restoration do.

The pips were also replaced with one larger recommended green annular wedge per
applicable launch source:1.5–3.5y from the source,10-degree full angle, outlined
and accompanied by a stand-here/landing-side hint. Its full nominal projected
sector must fit the assigned landing floor with0.75y margin. Rectangle-coordinate
extrema are checked at radial endpoints, angular endpoints and any included
cardinal axes; this bounds the whole sector rather than only its centre.
The green display still adds no AI forbidden zones and does not replace hazards.

The regression first failed against the pre-repair DLL for missing restoration
warning. Current build and regression pass:2700 assertions (2693 synthetic,
seven recorded Gust checks). Checks cover both restoration sides, the recorded
WHM position and its mirror, early deadlines, native inward routes, bounds held
until restoration, normal completion, cancellation, reset and ordinary Crown.
The launch regressions now probe the recommended wedge and actionable text;
the lower total reflects drawing one wedge instead of many disks, not removal
of the roster/assignment scenarios.

No original-module prediction replay was available: replayModuleSnapshot returned
`module_replay_contract_unverified`. Frame reconstruction and event decoding
worked; synthetic native callback/navigation validation is separate. The new
run was examined against the frozen pre-repair source, then used for development;
it is no longer an untouched holdout for the corrected candidate. No live test
of the new restoration warning or wedge is claimed. WIP remains appropriate.
Replay state was closed and verified released; source recordings were unchanged.


## Additional feedback: doomed island and early throw movement

The same original was reopened for the bounded Avalanche timing review above.
The facing cue at17:59:18.1931615 and throw hit at17:59:35.6586204 were separate
steps. The retained future deadline was accurate to the client observations;
at15:59:21.3155761Z the diagnostic navigator still exposed13.243929s of leeway.
Early repositioning therefore remains a movement-policy behavior, not corrected
by this radar patch. The new marker names the doomed island, and the throw is
shown as a translucent next-step preview until actual platform removal. Internal
future hazards and cast allowance remain intact. See the corresponding section
of tyrant-normal-corrections.md for the complete contract and2726-check result.
