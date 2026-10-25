# Serpent of Tural maze failure — 25 October 2026

The reported wipe was Greatest Labyrinth in Tender Valley (CFC 834, territory
1203), using xBMR 7.5.6.3522, module MVID
`a8d0952c-4de8-4396-8cd3-a7cf0efcc6a2`. The original recording's game version is
`2026.10.16.0000.0000`. Identifying originals remain private.

## Evidence and limits

The local original's SHA256 is
`8cf55835c29d6327f76a3adb11d6154422aad01c46474fea2fc8e58b09bb351f`.
Its sequence 39667 publishes maze state `0x01000080` at map index 1. Sequences
39681–39726 apply Greatest Curse (3825). The recorded local player receives it
at 39726, with nominal expiry about eighteen seconds later, and remains in the
center during the failed escape. Maze clearing occurs at sequence 42895.
These are recorded client observations, not calibrated server snapshots.

The corresponding DevBridge capture is `f3d22da05e1d4064bb241c6e5ef6d1f4`, revision
`7012b739847441a5814eb3d289883dce`, SHA256
`e97896bc74b81f4b69b63abb2b0eb7a9a43a492c14135c62f79a3a6b5eb2ba87`.
It contains 483 frames, no reported dropped start frames, and a complete lifecycle.
The framework snapshot clocks and original recorded clocks have a one-hour
offset in this source; compare corresponding sequences explicitly.

At capture sequence 302, recovery is waiting at approximately (-130.02, -554.01),
with two retained physical surfaces and `observed-movement-collision` reported.
At sequence 325, the escape towards (-124.25, -550.75) is invalidated for that
collision reason. By sequence 327 the retained count is nine. It subsequently
reports arrival at approximately (-130.00, -554.01), still in the center, until
the mechanic fails. The old capture contains counts, but no hit coordinates,
normals, material or native recovery score. It cannot identify which collider
was hit or prove that a reported hit actually obstructed this player's traversal.

A later pattern (`0x10000800`, original sequence 59716) has observed player
movement onto its north entrance and along the arrow route, with curse removal
at sequence 61408. This demonstrates recorded transport, not acceptance of a new
build or proof that the planner executed it. Movement author was not established.

Written context:

- [Icy Veins: Tender Valley](https://www.icy-veins.com/ffxiv/tender-valley-full-dungeon-guide):
  Greatest Labyrinth requires entering the arrow path leading to the cleansing
  panel before Greatest Curse expires. It supplies no physical trigger geometry
  or exact collision eligibility.
- [FFXIV Community Wiki: Tender Valley](https://ffxiv.consolegameswiki.com/wiki/Tender_Valley):
  describes tracing the path backwards from the circle and returning after
  cleansing. Its fifteen-second duration differs from the approximately eighteen
  seconds recorded here; the recording governs this investigation's observations.

## Confirmed defect and bounded repair

The shared recovery executor treated an absent next waypoint as arrival without
checking native destination safety. It now rejects stationary recovery with an
unsafe endpoint, clears the committed destination and propagates failure. A safe
endpoint remains distinct from the safety of the route used to reach it
(`UltimatelySafe` versus the fully safe route scores).
Least-harm moving egress remains available for all native scores. This corrects
false success reporting; it does not establish a working maze escape.

Current diagnostics now include native recovery score, destination safety,
warning relaxation, witness completeness and endpoint. Physical diagnostics
include the last two currently retained obstacle witnesses: numeric route step,
radius, surface segment, hit point, normal, material and hit distance. They expose
no native collider handles and create no additional history. DevBridge retains
the sampled evidence. No collision filter, body radius, arena, hazard or movement
authority was relaxed to conceal the failure.

Synthetic regressions cover all native stationary/moving recovery scores, exact
collision-witness projection and all four existing maze pattern constraints and
reset variants. These check source behavior, not physical maze transport.

## Still unresolved

The exact surface and why it rejected the maze entrance need a fresh maze capture
with the new current-state witnesses. Reproduce both east and north entrances,
compare the game's actual route with the swept-body result, and determine whether
conditional collision, trigger transport or rasterization is responsible before
changing collision acceptance. The native integration must honor supported BMR
mechanics across encounters. A build or synthetic regression cannot establish
that requirement, and this investigation does not claim the wipe is fixed.

The encounter module remains unchanged: the evidence currently identifies a
shared movement failure, rather than an established omission in its pattern IDs.
