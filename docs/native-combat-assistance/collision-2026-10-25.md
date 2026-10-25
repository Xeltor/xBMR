# Native movement collision — 25 October 2026

General collision observations now feed the same BMR map used for native recovery
and joint movement planning. No scene/object names or FC coordinates are encoded.
vnavmesh is not used to execute combat movement or answer per-frame path requests.
Two short development movements used it only to observe the game's own queries.

## Evidence and implementation

A temporary read-only observer recorded ordinary walking and ground sweeps using
layer 1, algorithm 5, material mask `0x2800`, value `0x2000`, radius 0.5 and body
centre 1.01 above the player's feet. Camera sweeps used a different material
filter (`0x1000`). The previous convenience ray helper uses `0x4000` and missed
movement-blocking furniture in the test scene. Querying all layers with that same
old material filter does not solve the discrepancy.

The production adapter uses the observed movement filter and a swept body with
the actor's radius. It ignores render-object bounds entirely; decoration excluded
by the game's movement filter cannot become an obstacle. Native scene access
holds the collision module's shared update lock. If the lock is busy, the probe
is unknown rather than blocking the framework thread or claiming clear geometry.

A second live comparison established that a sphere sweep reports the moving
centre at impact. With radius 0.5 the centre hit was about 0.44 from the returned
triangle, whereas the ray hit lay on its triangle. Surface rasterization therefore
uses actual returned triangles and adds the radius once. Primitive/degenerate
hits retain only the already-expanded blocked centre; unset vertices never become
an invented wall. Triangles are sliced at observed body height and mapped through
exact segment-to-cell distance tests, including tangencies and rotated maps.

The adapter copies values only. Workers never receive collider pointers or live
query delegates. Both optimized routes and the actual post-replan native recovery
waypoint are physically inspected before movement. The latter closes an unchecked
replacement route in the old recovery path. Known surfaces enter native, strict
and classified maps together, so search can choose a detour with the original
useful destination and mechanic deadlines. A live test found a raster-rounding
trap beside a wall: its conservative cell overlap covered the clear exact player
position. That occupied cell now retains its original limits only when the exact
point is outside the observed expanded surface; independent hazards remain intact.
Every actual exit still requires the native body sweep.

Floor proof is reused only between consecutive route steps with matching verified
support. Height follows observed ground; displacement resets the proof. Missing
floor during incomplete streaming is unknown, while a positive wall hit remains
usable. Refresh removes observations outside the current map or after identity,
layer or substantial height changes. A saved sweep must still identify the same
surface: hitting a different object cannot keep a disappeared obstruction alive.

## Cost and limits

A synthetic six-step flat route requires 35 floor rays and six body sweeps:
**41 native calls instead of 96**, without shortening the inspected corridor.
A remembered wall uses one witness sweep for refresh instead of repeating a full
floor/clearance probe. Current diagnostics expose route-query count, refresh-query
count and retained surface count. These counts are not a rendered-FPS benchmark.

The collision policy and centre-point semantics were measured on the live client;
the temporary hook and radial probe experiment were removed from production.
Synthetic tests cover material exclusion, swept width, floor reuse/reset, slopes,
streaming uncertainty, exact capsule/cell boundaries, rotated maps and native
search detours. This is not full-world collision coverage or dungeon acceptance.
The map learns surfaces encountered by proposed routes; unseen geometry can still
require a new route before movement. Native raster resolution and evidence height
remain relevant, and unusual multi-level/conditional collision needs live checks.

Native API reference:
[FFXIVClientStructs BGCollisionModule](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs).
The source documents filtering and the update lock; walking policy and sweep hit
semantics above come from the separate live observation, not guessed bit meanings.

## Validation result

Release build passed (six existing obsolete-gauge warnings); native movement
regressions passed **142,223 assertions**, and the nine Python tests passed.
Validated and watcher-loaded module: `36024519-cd33-402f-be83-1e3685c9b47c`.
Artifact and watched-DLL SHA256 both:
`fd9815a45d4f56cf7b7d0cbcc816ce77f08ac4f22318f675466d34e4b90a7883`.

After the occupied-cell repair, a live Dragoon dummy sample contained 40 combat
snapshots with movement and no reported collision or assistance failure. The last
route inspection used six queries. This confirms runtime execution in that scene,
not successful avoidance of every local object or a dungeon-wide improvement.
AI and RSR were restored off and the dummy reset requested; camera preferences
were unchanged. No vnavmesh path was active during the combat check. No release
was published.
