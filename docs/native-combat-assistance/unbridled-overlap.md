# Unbridled overlapping attacks — 2026-10-25

## Failure and sources

The maintainer reported a fatal overlapping-mechanic hit while investigating
repeated movement starts and stops. The loaded build was `7.5.6.3522`, MVID
`ee2fbe5e-a855-405f-ac25-5ea662a7ddca`. The bridge combat archive
`5d77e987d51d41649833291c4c3b952d`, revision
`c681a08133de4ebf9c1ca3412a02b2d7`, retained 1,271 frames without a dropped or
truncated start. Movement sampling is 250 ms, not a server snapshot clock.

The game recording's initially flushed prefix ended before the death. A later
fixed prefix (SHA-256
`e54d4e7fb50155d40a4e9b61c97b1f120e03b3715a3c49bca71c5115d32b1bbe`)
covered the death window. Its unfinished tail remains explicitly incomplete;
this is not a claim that the entire recording was decoded. Local requests and
matching action-effect source sequences identify the recorded player.

At recorded time 21:43:07.6921853, two distinct Heated Outburst sources
(`37804`, global sequences 537430 and 537431) each listed the player with a
damage effect. Their recorded origins were approximately (617, 817) and
(622, 802). The player was at (624.8939, 810.1777) shortly before the hits,
inside both nominal 13-yalm circles. Recorded HP fell from 109,369 to 7,846
and then zero; death was observed at 21:43:08.3702683. These client observations
do not establish exact server snapshots or independently measure the radius.

Before the hit, the movement capture shows a nearby recovery endpoint
(606.7393, 808.23474). At 20:43:04.836634 UTC, that became a much longer
route toward (631.7393, 811.23474), with negative departure leeway and native
`UnsafeImprove` status. It traversed the overlapping circles before their
resolution. This was emergency recovery, not a verified complete timed route.

Written guides reviewed for the South Horn Critical Engagement, not the
Heroes' Gauntlet boss or another variant:

- <https://ffxiv.consolegameswiki.com/wiki/The_Unbridled> describes the combined
  expanding-circle, jump/donut and cleave sequence.
- <https://nowff14.blogspot.com/2025/06/SCE-Crescent-Berserker.html> explains
  waiting for the relevant expanding circle, then the appropriate jump, before
  entering its cleared region; moving on the wrong event can be too early.

These guides support ordering and player response. They do not establish
geometry, snapshot deadlines or measured travel margins. Existing nominal
radii and native cast timing are unchanged by this repair.

## Repair and validation scope

`HoppingMad.AddAIHints` previously encoded being near the next jump's edge as
an additional hard forbidden zone. When the next jump became current during
Heated Outburst, this excluded the nearby escape even though the point was
outside every actual incoming damage shape. Emergency recovery then favored a
farther endpoint and crossed the overlap too late.

Preparation is now a soft native donut goal. Actual jump circles, follow-up
donuts and all expanding-circle instances remain independent hard constraints.
Their geometry, activation times, source identities, removal callbacks and
radar presentation are unchanged. The current hazard and its next response
retain the existing limited preview; this repair does not add visual clutter.

The recording-derived `UnbridledOverlap` fixture uses the actual component
types and recorded cast locations/order. It reproduces the former rejection of
the short escape, retains both damage circles at the fatal overlap, checks the
replacement native recovery direction, and checks the next circle/donut state.
Recorded positions and nominal module geometry are training inputs, not an
independent physical boundary measurement or whole-fight live acceptance.

This repairs the reproduced preparatory-constraint defect. Emergency native
recovery still has explicit least-bad behavior when a player is already late;
it must not be described as a complete safe transit proof. General spell-change
walking continuity, already diagnosed separately, remains open.
