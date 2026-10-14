# Shinryu Paradox: Starflare transition avoidance

Development repair on 2026-10-14 against parent
`df390976f492ee885b72db8d68bd83c5b33d8616`. The user reported ordinary
Starflare avoidance dropping through the upper hole or taking the lower
teleporter, exposing them to an unintended mechanic hit ("sticker").

## Written guides and evidence

Read the correct Windurst: The Third Walk alliance encounter, Phase 1 Shinryu
Paradox, rather than the Stormblood Shinryu trials:

- <https://consolegameswiki.com/wiki/Windurst:_The_Third_Walk>: Starflare has
  two successive sets of crossing lines on both floors. The upper hole drops
  players down, the lower pad returns them up. Breath needs lower, Tail upper;
  P2 has neither hole nor teleporter.
- <https://eorzeantavern.com/dawntrail/raids/windurst-the-third-walk/>:
  Starflare can be dodged in a gap on either current floor. Its Twilight section
  also warns about overlapping floor assignments. These claims supply player
  response context, not measured transition radii or server deadlines.

Archive training/regression source:

- Recording `0d5844e7ea7bbd9b528237827d2058b39b8a28584ed9d0ffa97ca40c25b1d79a`,
  original digest `482fbc09b779c4d518d0e13d59b21c62e2fbde58e2e4f45c7060c917ef3e9f10`,
  retained revision `3cf5a094b4f5a726905bcc0038d153d67d349be002dee890eaa202c25ea36f12`;
  schema 3, game `2026.10.06.0000.0000`, reported BMR `7.5.6.35`.
- Starflare visual starts: sequences `338113` and `378992` at ticks
  `4717578661` and `5307593209`. Helpers start at `4748362936` and `5338306008`;
  each instance has ten fast (`49126`) and ten slow (`49127`) casts, five per
  physical floor, with alternate checkerboard orientations between instances.
- First fast actions: `343574` / `4798210420`; first slow actions:
  `345963` / `4817778114`. The second instance begins resolving its waves at
  `384649` / `5388254284` and `387069` / `5408109187`.
- **Required transition overlap:** Cosmic Tail (`49110`) starts at `343319` /
  `4794292377`, before the first fast wave, and ends at `351503` / `4863800544`,
  after the slow wave. A blanket lock for all of Starflare would obstruct the
  necessary lower-to-upper transition. The second selected Starflare window
  has no selected floor cast.

The bounded `mechanic_patterns` selection uses visual start anchors, one second
before and eighteen seconds after, and floor/line cast starts, cast ends and
Twilight helper actions. Both selected anchors are exhausted; neither context
is truncated, sequence-clipped or outside the retained range. This selection is
not a full encounter inventory. The source is now a design input, not a holdout.
Schema 3 omits HP updates and other operations; client cast/action timestamps
do not establish snapshots, last-safe departure or physical travel duration.
The source does not independently attribute the user's reported movement to a
specific AI decision, nor establish which debuff was received.

## Cause and repair contract

`BuildArena` excludes the upper hole only from the 3D rendering stencil. Both
physical floors retain rectangular pathfinding geometry. `FloorAOEs` previously
supplied no AI constraint between its recognized floor mechanics, so a normal
Starflare dodge could select a path through either transition. Its existing
same-floor constraints also used the future attack deadline, allowing an
unnecessary crossing before that deadline.

Keep the current floor by adding an **immediate** forbidden circle around its
transition while there is no active floor mechanic, or while the actor is
already on that mechanic's required floor. Reuse the existing module's centre
`(820, -826)` and nominal proxy radii (upper 6, lower 2 yalms); do not enlarge
Starflare lines or infer physical bounds from player positions.

When Breath, Tail, Atomic Tail or assigned Twilight requires the other floor,
omit this additional guard and retain the original timed transition guidance.
Missing/conflicting Twilight assignments keep the existing fail-open behavior;
no desired floor is invented. An early cloak assignment cannot override an
active swipe. Successful helper completion or swipe finish restores ordinary
floor preservation. Native component recreation starts without old mechanic
state. Single-platform P2 has no additional transition restriction, including
the interval before the P1 component is deactivated.

The guard is an AI constraint, not an attack, so it does not add a fake damaging
AOE drawing or warning. Line prediction, shape, per-floor selection, wave
activation and cleanup stay unchanged. Other active hazards still apply;
manual movement and XCAI's control ownership remain authoritative.

## Validation and limits

`tests/ModuleRegression/ShinryuTransitions.cs` adds native synthetic cases for
both floors, both Twilight assignments, all three swipe actions, already-correct
floor bounce-back prevention, both checkerboard orientations and waves, and P2
cleanup. It checks native rasterization and the first movement segment for an
uptime goal across the transition. Existing Twilight direction, helper-order,
assignment replacement and generated activation regressions remain in the suite.

The pre-repair DLL fails the added ordinary-movement exclusion check and the
updated immediate correct-floor constraint check. That baseline also lacks the
unrelated pending Promathia changes; its Promathia failures are not Shinryu
regressions. The test executable can be compiled without rebuilding the plugin.

The complete current working tree builds successfully in Release with SDK
`10.0.401`, the API 15 development dependencies, zero warnings and zero errors.
The build was started only after the 20:26:44 UTC DevBridge observation confirmed
`NormalConditions` with no `InCombat` flag. The stable development DLL output
is `BossMod/bin/Release/BossModReborn.dll`, SHA-256
`e34ed82e686d95f8c3510191a8730d49b8a24aab1433071c7cb4523a602c17c7`.
No installed plugin directory was written and no manual reload was requested.
The Dalamud log records the watched DLL's automatic reload finishing at
22:26:47 Amsterdam time; DevBridge subsequently reports the development
`BossModReborn` plugin loaded. This establishes reload, not live mechanic success.

The complete native synthetic module suite passes **1,416 assertions**, including
unrelated current-tree Promathia work. The test executable reports two existing
nullable warnings in `Regression.cs`; the plugin build itself is warning-free.
The cross-centre navigation fixture uses a 1.5-yalm goal radius because native
goal rasterization requires all four corners of a 0.5-yalm cell to qualify;
an initial 0.5-yalm fixture had no goal cell and correctly produced no movement.
This fixture adjustment changes no encounter geometry or safety margin.

The report reopened the floor-routing concern; the pre-repair failures and
post-repair constraint, overlap and native first-segment checks support closing
this bounded omission. The previous `Contributed` provenance is retained;
whole-encounter Verified or AISupport acceptance is not claimed. Physical
transition contact boundaries, vertical travel, calibrated snapshot deadlines
and live xBMR/XCAI execution remain outside these synthetic checks. No release,
tag or feed update is created.
