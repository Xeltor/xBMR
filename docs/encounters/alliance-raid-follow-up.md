# Alliance raid follow-up

This pass covers Promathia's island transition and return knockback, Diabolos Hollow's portal separation, and Compound 2P's transferred Prime Blade line and Three Parts Disdain. It uses the existing encounter IDs, module factories, radar and AI hint interfaces.

## Windurst: The Third Walk — Promathia

The normal-mode guide describes entering one of three platforms for False Genesis and bracing against the corner walls during the add phase. The archive distinguishes the boss's visual action, the helper's damage action and the island map signal. [Windurst guide](https://www.icy-veins.com/ffxiv/windurst-the-third-walk-alliance-raid-guide)

| Recording / retained revision | Boss action 50343 | Helper action 50344 | Map 0x0C / 0x00020001 |
| --- | --- | --- | --- |
| `0d5844e7ea7b` / `3cf5a094b4f5` | seq 102182 | seq 102444, +0.617 s | seq 102649, +0.399 s after helper |
| `216d4cfe585e` / `616395e1e18d` | seq 1041595 | seq 1041719, +0.568 s | seq 1041798, +0.433 s after helper |

The platform arrival deadline remains the boss's cast finish as an early response policy. The circular floor stays available to navigation through the visual resolution. The damage event or map signal switches to the three island bounds and releases the preparation overlay. The map signal can recover a missed helper event. An interrupted visual clears the arrival instruction. The helper event and map signal are idempotent with respect to arena replacement.

In `216d4cfe585e`, the restore map signal precedes Deadly Rebirth's visual and helper starts (seq 1085521, 1086649 and 1092815). The visual forecasts the existing 20-yalm forward push, then the helper owns its direction, deadline and retirement. The preparation shape now uses the native knockback constraint for landing inside the restored circle. Obsolete visual callbacks cannot clear the helper's prediction, and a missed resolution expires after two seconds.

The helper action is a conservative point to switch the modeled floor. The physical disappearance instant, last-safe departure, island wall geometry and knockback immunity still need validation. Existing island routing and knockback distances are retained; the module stays WIP.

## Dun Scaith — Diabolos Hollow

The normal-mode guide describes a portal team killing Shadow Dahak and a shrinking floor with a lethal edge. The indexed fight establishes the separate portal region and an outside sequence. It also retains some inside participant observations. [Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Phase_3:_Diabolos_Hollow)

In recording `901ecf796487`, revision `b73cb28753f8`, Diabolic Gate spawns at seq 1752012. Its Deep Flow starts at seq 1767501 and the gate disappears at seq 1770909 without a retained Deep Flow impact. Actor 5 has portal observations near (350, -300, -445) and targets actor 596 at seq 1769014. The reviewed projection lacks a matching creation identifying that target's OID.

Use the existing portal-region predicate on automatic target candidates. Opposite-floor candidates are suppressed for auto-selection while keeping manual targeting and existing stricter prohibitions. Re-evaluate on current position so returning players regain their surface targets. If the gate and its live cast are present for the viewer, retain the Deep Flow countdown even inside the portal. Cast cancellation or gate removal retires it.

Portal entry and movement remain manual. The retained transitions do not establish the entry footprint, entrant selection, safe floor geometry over time, or the portal enemy's complete mechanics. This module remains WIP.

## The Puppets' Bunker — Compound 2P

The normal-mode guide describes the transferred Prime Blade patterns and a three-hit stack with knockbacks. The recorded teleport provides an observed destination and direction, and the three action effects distinguish the deadlines. [Puppets' Bunker guide](https://twinfinite.net/guides/ffxiv-puppets-bunker-alliance-raid-guide/)

In recording `dde91ac1e9a5`, revision `833f6311b5d6`, Transfer1 tether 116 connects helper 310 at (225, -725) to helper 315 at (200, -700) at seq 826345. Boss 343 begins rectangle 20889 at seq 826633 with rotation -2.916 radians, then moves to the tether destination at seq 833707 with facing -1.107 radians, approximately 4.10 seconds before the action. The action packet preserves the original cast heading at seq 838155. Listed target 65 is observed near (211.15, -708.57) before impact, which is compatible with the existing rectangle at the destination using the arrival facing. Its position is outside that rectangle using the initial heading. This is a consistency check on orientation, with no claim about exact damage edges.

The module binds the destination while the link is present and uses the specific caster's arrival pose for the line. The pre-arrival player warning appears before the destination facing can be confirmed. Missing or ambiguous links withhold geometric guidance. Circle and donut transfers retain their early tether-based forecasts. Each caster's own cancellation or impact removes its prediction.

Three Parts Disdain resolves at seq 656956, 657598 and 659801 in the same recording. The observed intervals are approximately 1.09 s and 1.38 s. Stack and knockback share one sequence state. Each hit anchors the next prediction, with existing 8/8/12-yalm displacements and the 29-yalm preparation boundary. The carrier is guided for the full remaining displacement and stackers for the immediate landing. A duplicate action cannot advance the sequence twice. Target loss, cancellation and timeout retire the state.

A second partial recording lacks these observed variants. Other transfer patterns, target death or replacement during Three Parts Disdain, and physical landing behavior remain open. The concrete timing and orientation defects reopen Compound 2P as WIP.

## Validation

Build the full Release plugin with the repository SDK and matching Dalamud references. Run `tests/ModuleRegression` and `tests/DiabolosRegression` against that assembly. The suites exercise generated component activation, native hint publication and deterministic callback sequences. Checks include False Genesis cancellation and map-only recovery, stale Rebirth visual callbacks, portal countdown and return, transferred-line orientation and retirement, and per-hit stack and knockback timing.

The reviewed recording windows supplied the implementation evidence. The regression checks are synthetic and do not constitute an independent original replay, a physical route measurement or live AI acceptance.

The isolated Release build passed with six obsolete API warnings in unrelated files. The module suite passed 90,085 assertions, the Diabolos suite passed 194, the adjacent Ferdiad/Proto suite passed 174, and the native movement suite passed 143,589. All nine Python tests passed.
