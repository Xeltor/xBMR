# Dun Scaith: Proto Ultima Aetherial Pool investigation

Read-only research on 2026-10-14, exact installed game `2026.10.06.0000.0000`. No module source or shared build output was changed during this investigation.

## Written guide and recorded effects

The preferred [ConsoleGamesWiki normal Proto Ultima section](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Proto_Ultima) describes Eikonizer followed by Flare Star proximity explosions and an unsafe outer arena region, Aetherial Pool. Its description calls the displacement a knockback; the exact second recorded effect is a pull. The guide does not measure the inner boundary, explain eligibility or supply snapshot/last-safe-departure timing.

Original source digest: `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`. A private byte-identical recording copy was decoded completely with frozen native parser MVID `701feb46-428d-4ed6-9fc6-2dcd880a5b7f`, covering all 1,890,796 raw entries and 1,550,436 movement rows. Action 7587 is cast by helper 342 at the arena center:

| Pool cast / action | Cast start seconds | Effect seconds | Listed recipients | Encoded result |
| --- | --- | --- | --- | --- |
| 998221 / 1002941 | 1252.8659551 | 1256.3182752 | `[3]` | Damage amount 0; no displacement effect |
| 1006895 / 1011640 | 1260.9111140 | 1264.3753789 | `[3]` | Damage amount 1812; Attract 1 row 27 |

Second effect raw `0x001b000000000020` decodes through the current `ActionEffect` layout as type 32/Attract 1, value 27, other parameters 0, applied to the target. Exact October Attract 27 has max distance 40, speed 25, minimum remaining distance 0, direction 0 and `UseDistanceBetweenHitboxes=false`. It is a bounded attract-to-source row, not proof of a 40-yalm overshooting knockback. Full displacement rows SHA256: `5bcfa9b2885d6d813c314c5cc0eb3de322fa3506e9f7f0c0400f3c699372b9a2`; version-file SHA256: `3da1d28ba647edae481c8ea61eeb1fae9a3d1d700ac703165e4b3e73a66d57ac`.

Participant 3 has status 1570 from raw 993262 at 1247.0814109 until observed loss 1003513 at 1257.0772416. The exact October Status row names it **Invincibility**, with description “Invulnerable to all damage.” At the first Pool action its nominal remaining duration is 0.745218s. The first zero-damage/no-pull result coincides with that active status; the second damage/pull result follows its observed loss. This is an observed association, not a general proof that 1570 supplies knockback immunity. None of the selected participants has an observed active standard Surecast 160/Arm's Length 1209 status at these effects. Nominal expiry and observed loss remain distinct.

All 24 alliance members are retained at both effect snapshots. Recipient 3's most recent position is `(-349.51,1,-25.599)`, radius 24.406 from helper 342, but the observation is **7.728s and 15.785s old**, respectively, with no new position inside either ±1s window. Other players' latest radii mostly lie around 17–21.4, with differing observation ages. These positions cannot establish a safe annulus, exact inner edge or server snapshot. Private receipts `scathach-proto-pool-eligibility-receipt.json`, `scathach-proto-pool-snapshots.json` and `scathach-proto-pool-window.json` preserve every participant's latest position, age, selected statuses and all position rows within ±1s of each effect.

## Independent assets and their boundary

Exact October Action 7587 is CastType 2, EffectRange 32, cast 3.5s, Omen 83 `fate_altema_madoufensu1t`, ActionTimelineHit 1875. The original AVFX at `vfx/omen/eff/fate_altema_madoufensu1t.avfx` is 17,828 bytes, SHA256 `342ce68ec539f175554ad6f2d08ada76d4372641b344187648aa83bf2ef83041`. This is byte-identical to the retained September asset, permitting reuse of its original parser facts for this exact asset while preserving the distinct game-version manifests.

The entire inventory has one model without draw-mesh arrays and three particles. Particle 0 is type 1, scale X 15, axis-connect 3. Particles 1/2 are procedural DecalRing type 11, scales X 36.5/37.25 and Y 7.5, axis-connect 3, width 0.1, RingFan 1 and ScalingScale 1. All graph nodes are inventoried: one scheduler, two timelines and one emitter; no binder nodes. Scheduler→timeline 0→emitter 0 has three enabled particle links, target indices 1/2/0. The emitter has no stored scale channels; absence is not a decoded identity transform. The separate timeline 1 includes opaque clips. No renderer contract composes these scales, procedural ring width, textures/alpha and action placement into world-space damage geometry. The values are **not** an accepted inner/outer radius. Asset/parameter/graph facts and pinned parser provenance are retained in `scathach-proto-pool-asset-facts.json`.

The typed territory LVB terrain path `bg/ex1/05_zon_z2/rad/z2r3` leads through `collision/list.pcb` (SHA256 `899135129cf1fce54880e2f284aff25742e2e3b04f8215bfa0ba260ef913f046`) to five meshes intersecting the Proto platform bounds. All directly decoded node/triangle counts match headers:

| Terrain asset | Nodes / triangles | SHA256 |
| --- | --- | --- |
| `tr0914.pcb` | 1 / 11 | `afe86ce7e4c10ac0155bb954a0717136b9684380d74d2cd82c6ebec1860893a1` |
| `tr1014.pcb` | 57 / 508 | `6217128e50c27305f2d5fafb605c1503aa0407079008c61ac8aab7f01189f44e` |
| `tr1015.pcb` | 59 / 428 | `dde0b98e5b3b05e62bc7bca61e4680a0fbfd4b9f4e81a2bbe49e412192b4a018` |
| `tr1114.pcb` | 1 / 11 | `e6e891a4b267067cf83c939f4030f727793825d03a09d6f91ce7d32d11a08bfd` |
| `tr1115.pcb` | 3 / 28 | `88d36a9f8dcf4b45eedac0d77de72c1bba503d49c8148f8991274c878860c3f9` |

Upward Y=1 floor triangles have material `0x7004`:288 triangles, area 2920.380972 square yalms, with a 96-segment outer loop centered `(-350,-50)` at radius 30.4993–30.50043. A tiny four-vertex boundary near radius 30 is a tessellation seam, not an independently established gameplay hole. A separate 192-triangle vertical cylinder at radius 30, Y 1–17, has material `0x2000`. This establishes independent supporting-floor/wall geometry and retains the module's conservative circle 30 inside the support. It supplies **no Pool inner boundary**; no static inner ring around 20–24 appears in these terrain assets.

The local LGB SharedGroup 6601720 at `(-350,1.1,-74)` links `sgbg_z2r3_a3_gmc02.sgb`, a transporter assembly, with EventObject 2007672 bound to it. Its nearby disabled EventRange is not Pool geometry. Another local EventObject 2007702 has BoundInstanceId 0 and Excel-linked ExportedSG 10099→`bgcommon/world/btl/shared/for_vfx/sgvf_w_btl_b0801.sgb`: that SGB contains a VoidSludge floor VFX and sound, no collider. The original has no create for this OID, so it does not establish an active Pool stage. No matching LGB/SGB collision trigger was identified. Typed source-chain and current-format PCB contracts are the same pinned [FFXIVClientStructs collision definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Mesh.cs) and [vnavmesh terrain extraction](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneExtractor.cs) used in the other Dun Scaith geometry notes.

## Flare Star conflict and scoped proposal

Pool resolves while the Flare Star distance preference is active, before the proximity burst:

| Wave | Flare cast start | Pool effect | Flare effect | Pool precedes Flare |
| --- | --- | --- | --- | --- |
| Central | 1250.9918161 | 1256.3182752 | 1257.9813202 | 1.6630450s |
| Two opposed markers | 1259.0353287 | 1264.3753789 | 1266.0182436 | 1.6428647s |

The pre-correction soft goal was `min(5, min_distance_to_Flare/6)`. For the central marker it reaches its maximum only at radius 30, the module's arena edge. For opposed markers at approximately ±20Z, perpendicular positions reach the cap near radius 22.36; farther points through radius 30 tie. Native path selection may prefer the nearest maximum, so it must not be claimed that the paired wave necessarily routes to radius 30. Both preferences nevertheless reward moving outward into a region the guide explicitly identifies as unsafe and whose exact boundary the module does not implement.

An isolated native navigation probe against the frozen round 5 pre-correction DLL independently consumes that goal. Six samples complete with zero module errors, unsupported outputs or executed actions. At first Pool cast 998221 it chooses actor 1 destination `(-345.25,-20.75)`, radius 29.63, estimated travel 4.055s. At first Pool effect 1002941 it chooses `(-342.75,-21.25)`, radius 29.65, estimated travel 1.455s while the Flare effect remains 1.663s away. At second Pool cast 1006895 it chooses `(-323.75,-45.25)`, radius 26.68, estimated travel 0.950s; after second Pool effect 1011640 it chooses a radius 23.25 point. This is **module-hints-only path selection**, without supplied physical obstacle map or Pool geometry: it proves outward goal consumption, not that a route is safe, that a player entered Pool or that live controllers executed it. Full parent chains and artifact identity are in `scathach-proto-pool-native-navigation.json`.

A source-supported scoped correction is to remove/suppress the outward preference during this overlapping mechanic and preserve the proximity damage warning plus explicit outer-floor warning/manual response. A hard-coded safe annulus, an arbitrary distance cap or activation of the old full-arena pull would invent eligibility. Automated Pool constraints still need independently established inner geometry, actor-hitbox eligibility, applicable immunity and snapshot/route timing. Builds or native navigation probes alone do not certify those missing facts.

The implementation owner has now removed the outward goal and converted Aetherial Pool to an early cast warning, retaining Flare Star's damage deadline and manual response text. Current-artifact build and regression results are recorded by that owner separately; this investigation's native navigation report deliberately preserves the pre-correction artifact for the causal comparison. Physical supporting-floor provenance is additionally summarized in `scathach-proto-physical-floor-receipt.json`. Combined Pool/proximity automation remains unverified until the missing inner eligibility geometry and timing are established.
