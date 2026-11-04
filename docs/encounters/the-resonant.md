# The Resonant — Fordola rem Lupis

The walkthrough identifies the second part of the level 69 solo duty as Fordola's fight. It describes using Aetheric Siphon against her barrier and dodging area attacks when Lyse joins. The recording supplies the missing Terminus Est details. [The Resonant walkthrough](https://ffxiv.consolegameswiki.com/wiki/The_Resonant#Walkthrough)

In the recorded duty (territory 684), Fordola (OID 0x1B7D) started The Order (9106) at raw replay sequence 90184. All four Terminus actors (OID 0x1BCA) were near (0.004, 0.004) with diagonal facings. Those same four actors delivered Terminus Est (9108) in one recorded timestamp group about 5.86 seconds after the cast began. The center check discarded all four real sources and left null shapes in the returned AOE array.

The component uses the existing generic AOE drawing and AI hints, showing the four simultaneous lines from their recorded positions and facings as soon as The Order starts. Their shared authored activation was about 5.79 seconds after cast start. The existing rectangle dimensions and activation calculation were carried forward. The Magitek Ray overlap retains its own warnings and activation. The recordings establish client-observed cues and effects, while exact hit boundaries, server snapshot timing, and live radar rendering remain unverified.

The missing-radar report reopened the module during diagnosis. The same recording reproduced the exception on the installed stable build. The corrected Release build passed 49 offline samples covering registration and activation, the cue, overlapping Magitek Rays, the four simultaneous warnings and AI constraints, and their removal at resolution. This scoped repair restores Contributed maturity.
