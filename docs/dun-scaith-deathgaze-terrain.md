# Dun Scaith: Deathgaze supporting collision footprint

Read-only terrain extraction on 2026-10-14, installed game `2026.10.06.0000.0000`. This supplies independent physical floor measurements for the arena/wall investigation; it does not establish lethal edge rules or sealed-door state semantics.

The [preferred Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Deathgaze_Hollow) describes the knockback and lethal deck edges. It supplies no measured center or dimensions. The exact territory LVB's typed terrain path, `bg/ex1/05_zon_z2/rad/z2r3`, leads to `collision/list.pcb` and indexed `trXXXX.pcb` through the pinned [vnavmesh terrain reader](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneExtractor.cs) and [current streamed-collider definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Collider.cs). The index SHA256 is `899135129cf1fce54880e2f284aff25742e2e3b04f8215bfa0ba260ef913f046`. Filtering its bounds near X265–335/Z390–430 selects exactly two terrain meshes:

| Asset | Bytes | Nodes / triangles | SHA256 |
| --- | --- | --- | --- |
| `tr2022.pcb` | 30,000 | 119 / 769 | `b66e2a04722c70713aa1131aada1cc03aeca5a6c3061569a700916bf1ed030c3` |
| `tr2122.pcb` | 125,120 | 349 / 3,517 | `426143d24b2cb2c161b096e58d23e086681f6a1d464cc836a0c70633c493376b` |

Direct decoding follows the pinned [PCB node/primitive definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Mesh.cs), including 64-bit primitive material at primitive offset4 and node-local compressed coordinates. Both complete decoded node and triangle totals match their file headers. No decorative model bounds or recorded player positions enter these measurements.

Selecting upward-facing triangles whose vertices are within 0.002 yalms of world Y=2.87, whose primitive material contains the standard collision-query bit `0x4000`, and whose maximum X is below 329 isolates 530 main-deck triangles, materials `0x7005`/`0x7006`. Their outer convex hull in X/Z is:

```text
(269.0198059082031, 395.0205383300781)
(328.9978942871094, 395.0015563964844)
(328.9979248046875, 409.68609619140625)
(328.9979248046875, 424.30157470703125)
(299.0088806152344, 424.3130798339844)
(269.01983642578125, 424.3245544433594)
```

The middle points are nearly collinear edge subdivisions. The four corner mean is approximately `(299.0088654, 409.6620560)`; the footprint is slightly skewed, rather than an exact axis-aligned rectangle. Triangle area sum is `1757.4787494777702` square yalms and convex-hull area is `1757.4796592677012`. Pairwise triangle clipping finds zero positive-area overlap. Thus uncovered area inside this hull is at most `0.0009097899311` square yalms, consistent with quantized seams rather than a meaningful floor hole. Independent X scanlines from 269.1 through 328.873 each have one filled Z interval, merging only gaps below 0.01. Private selected triangles, hull and checks are in `scathach-deathgaze-mainfloor.json` and `scathach-deathgaze-floor-scanlines.json` in the private evidence receipts.

These facts support calibrating the encounter center and floor edges independently of wall models. The prior `(300,410)` with half-extents `(30,15)` reaches beyond the recovered east and north supporting edges. Sealed boss-door inner faces and phase-dependent rails must be intersected with this floor separately; neither a supporting triangle nor its material alone proves that a knockback is safe. Standard ray-filter bit eligibility is established by the pinned [BGCollision filtering contract](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs), not an independently decoded lethal-material classification.

No module source, gameplay state or shared build output was changed during this extraction.
