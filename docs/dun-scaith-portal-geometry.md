# Dun Scaith: Diabolos portal geometry investigation

Read-only investigation on 2026-10-14 against installed game `2026.10.06.0000.0000`. This recovers concrete collision assets and identifies a parser gap; it does **not** certify a walkable arena or authorize a guessed boundary implementation.

## Descriptive context

The [preferred Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Phase_3:_Diabolos_Hollow) describes entering the Diabolic Gate, killing Shadow Dahak, and instant death outside the portal arena. It supplies no measured radius, collision material interpretation, transition signal or snapshot deadline. The distinct portal floor at approximately `(350, -300, -445)` must not be flattened into the main arena at `(-350, ..., -445)`.

## Why the initial Lumina output was corrupt

The inspected `Lumina.Data.Parsing.Layer.LayerCommon.CollisionBoxInstanceObject.Read` reads two low material words after the trigger-box parent, then a byte labelled `PushPlayerOut` and a string offset. The [current FFXIVClientStructs file definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/FileFormat.cs#L315) contains two additional 32-bit material words before the Boolean and path offset:

| Offset from instance | Current file field | Raw value in all four portal collision instances |
| --- | --- | --- |
| `0x3C` | MaterialMaskLow | `0xFFFFFFFF` |
| `0x40` | MaterialIdLow | `0x00020000` |
| `0x44` | MaterialMaskHigh | `0x0000001F` |
| `0x48` | MaterialIdHigh | `0x00000000` |
| `0x4C` | Layer43h Boolean | `0` |
| `0x50` | Relative path offset | Valid PCB path |

Thus the old parser reports `PushPlayerOut=31` because it reads the low byte of MaterialMaskHigh. Its next string offset comes from MaterialIdHigh, pointing at the instance header and producing the bogus string `9`. This is a concrete decoder-layout mismatch, not evidence of a missing collision asset.

The four recovered instance headers have asset type `57` (CollisionBox), keys `12`–`15`, and raw SGB offsets `6668`, `6756`, `6844`, `6932`. Trigger shape is mesh (`5`), priority `100`, active-by-default byte `1`. Each local transform is translation `(0, -1, 0)`, zero rotation and unit scale. No Lumina, FFXIVClientStructs, plugin or sibling-repository source was changed.

## Recovered mesh geometry

Parent SGB: `bg/ex1/05_zon_z2/shared/for_vfx/sgvf_z2r3_b1045.sgb`, 9,678 bytes, SHA256 `8e1e4748270ed5acf139bd59f07cbc2a478ef2a0a91522b0c9883f50d44b9e09`.

Corrected PCB paths all start with `bg/ex1/05_zon_z2/rad/z2r3/collision/`:

| Instance | Asset | Local triangle coverage | Nodes / triangles | SHA256 |
| --- | --- | --- | --- | --- |
| 12 | `z2r3_a4_gmc10.pcb` | Annulus: inner vertex radius 15, outer 20 | 15 / 96 | `b084535604f5ce3d86ccb1e2c9dc4e71c6b00e82a96f392cca6c68aaf3f74a54` |
| 13 | `z2r3_a4_gmc20.pcb` | Annulus: inner vertex radius 10, outer 20 | 15 / 96 | `32996e79c560d63b2dbbd3cc40dd42981e6b0628e34065a1a6369f21f2d94a3e` |
| 14 | `z2r3_a4_gmc30.pcb` | Annulus: inner vertex radius 5, outer 20 | 15 / 96 | `7fb576220f3f7cecda5f8aaf7e4ad7b320c658d49df5687d72b4eb59a804beec` |
| 15 | `z2r3_a4_gmc40.pcb` | Full disk: center 0, outer vertex radius 20 | 7 / 48 | `5c695235ba72c11d3b8bcc1b8c549318ba3129a2cf949ad99e433072ed2bc123` |

Each outer loop has 48 segments. Annuli also have a 48-segment inner loop; the final disk has no inner hole. All vertices have local Y=0. Raw/compressed coordinate quantization gives radii within approximately 0.0003 yalms of the nominal values. These are polygonal mesh boundaries, not exact analytic circles: a 20-yalm radius circle extends approximately 0.043 yalms past an ideal 48-gon's edge midpoint.

The meshes were additionally decoded directly using the [current FFXIVClientStructs PCB binary-tree layout](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Mesh.cs). All child-node and triangle totals match their headers. All decoded 64-bit primitive materials are `0x2000`. The installed Lumina PCB parser exposes only a partial 16-bit `Unknown` field and reports `32`; that truncated value must not replace the full material. Object-level material values/masks and primitive materials are separate inputs. The pinned [BGCollisionModule contract](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/BGCollisionModule.cs#L18) documents effective mesh material as `(primitive & ~objectMask) | objectValue`. For these assets, primitive `0x2000`, object mask `0x0000001FFFFFFFFF`, and object value `0x00020000` yield effective material `0x00020000`. This arithmetic narrows the gap to the meaning of that effective material and its active state; it does not establish lethality or walkability.

The planmap places SharedGroup instance `6586142` at `(350, -300, -445)`, zero rotation and unit scale. Composing that parent with the four collision transforms puts these triangle planes at world Y=-301, centered X/Z `(350, -445)`. This is an authored collision-plane height, not proof of the player's foot height or a physical walkable floor.

## Authored state references

The SGB contains ten scene timelines. Reading its SCN1 timeline list, instance-reference list and embedded TMLB nodes recovers the following `C055` state words for collision instances 12–15. TMAC/TMTR child offsets are relative to the node data after the eight-byte node header; instance references explicitly map sub-IDs 101–104 to collision keys 12–15.

| Timeline | Scene sub-ID | State words for instances 12, 13, 14, 15 |
| --- | --- | --- |
| `vw_def` | 2 | `0, 0, 0, 0` |
| `vw_def_anim` | 3 | `0, 0, 0, 0` |
| `vw_on1` | 4 | `1, 0, 0, 0` |
| `vw_on1_anim` | 5 | `0, 0, 0, 0` |
| `vw_on2` | 6 | `0, 1, 0, 0` |
| `vw_on2_anim` | 7 | `1, 0, 0, 0` |
| `vw_on3` | 8 | `0, 0, 1, 0` |
| `vw_on3_anim` | 9 | `0, 1, 0, 0` |
| `vw_on4` | 10 | `0, 0, 0, 1` |
| `vw_on4_anim` | 11 | `0, 0, 0, 1` |

The one-at-a-time expanding annuli, material override and transition names strongly suggest staged danger coverage. That is an **inference**, not a verified mapping to damage, death or a safe-space complement. A separate current-binary investigation has identified `C055` as a collider enable/disable operation. For exact executable SHA256 `b38840761e2677128b6bec1ceaee3f2870cd84e1d38ab7958216373058383780`, registration `0x1428c5730` binds tag C055/kind43 to factory `0x1418c67a0` and vtable `0x1422e9180`; publish slot31 at `0x14192d5b0` dispatches kind43 to `0x14192d671`, reads the Boolean at data+`0xC`, and invokes bound layout virtual37, identified as `SetColliderActive(bool)` by the pinned [ILayoutInstance definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/ILayoutInstance.cs). The private receipt `floor-lifecycle-runtime-continuation.json` and hashed disassembly preserve this independent trace. Specialized cleanup slot32 does not undo kind43; enclosing timeline completion/fallback remains unresolved. No automatic undo or lethal-material rule follows from that operation. No raw timeline time unit was converted into a combat deadline. In particular, these four meshes must not be presented as four verified walkable floor variants or as a measured safe radius sequence.

Planmap EventObject instance `6625118`, BaseId `2007638` (`0x1EA256`), explicitly binds SharedGroup `6586142`. Its own object position is `(343.047, -300, -447.1576)`, distinct from the group's arena center. The inspected complete original contains **zero create rows with this OID**, so it supplies no matching object-lifecycle/state-transition trial for this group. No mapping from a retained main-arena event to portal stage was invented.

## Independent physical supporting floor

The typed LVB terrain path is `bg/ex1/05_zon_z2/rad/z2r3`. The pinned [vnavmesh scene reader](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneDefinition.cs) appends `/collision` to this exact terrain path; its [extractor](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneExtractor.cs) reads `list.pcb` and the indexed `trXXXX.pcb` assets. The current [FFXIVClientStructs streamed-collider definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Common/Component/BGCollision/Collider.cs) independently documents that file chain. This identifies physical terrain without relying on decorative models, player positions or an EventRange.

`collision/list.pcb` has 57 mesh entries and SHA256 `899135129cf1fce54880e2f284aff25742e2e3b04f8215bfa0ba260ef913f046`. Filtering its X/Z bounds around the portal selects exactly two assets:

| Asset | Bytes | Nodes / triangles | SHA256 |
| --- | --- | --- | --- |
| `tr2108.pcb` | 5,984 | 27 / 189 | `08b1a08f64178dff0cd2120f06bddb7466b1855fc496d3ac3fe1729d5e4ed90f` |
| `tr2109.pcb` | 6,432 | 27 / 213 | `74dbffcfdf18f2bf532f54284b0cff80602e76897fe6a622deec342796245572` |

Direct decoding with the same current PCB layout matches both header node/triangle counts. These contain 44 and 52 upward-facing planar triangles at world Y=-300, respectively, all primitive material `0x7004`. Their combined area is `5022.966840086` square yalms. The floor triangles form one closed 96-segment outer loop, centered at X/Z `(350, -445)`, with vertex radii `39.9996157`–`40.0004169` and **no inner boundary loops**. This is the footprint of a physical supporting collision disk of nominal radius 40. It is separate from the timeline-controlled annuli of outer radius 20, whose plane is one yalm lower. Scratch provenance and selected triangles are in `portal-terrain-summary.json` under the investigation evidence directory.

**Radius 40 is not a verified safe combat radius.** Supporting collision alone does not establish lethal trigger coverage, server-side arena rules or the enabled state after entry. Likewise, the EventRange's radius 20 is not the physical floor footprint. Neither value can be substituted for an accepted playable boundary without resolving the danger rules.

A later inventory of **all** remaining triangles in these two terrain assets identifies another distinct authored structure: all 192 material `0x2000` triangles form a vertical cylinder at radius approximately 20 (`19.999513`–`20.000509`), from world Y=-300 to -284. Their normals point inward (radial dot normal approximately -1). The 114 other material `0x7004` triangles form the support disk's outer sides, at radius 40 and extending down to Y=-316. The radius 20 inward-facing collision cylinder is independent of the EventRange and matches the annuli's outer footprint; however, material `0x2000` has no independently established player-blocking or lethal meaning in the inspected primary contracts. It cannot yet certify radius 20 as safe either. Complete decoded triangles are retained in `scathach-portal-terrain-alltriangles.json`.

## Recorded portal entry and visibility limits

The earlier nonmovement projection omitted the entry cues. A complete native decode of the original, including all 1,550,436 movement rows, finds 10 position rows near the lower portal. Source digest is `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`; all 1,890,796 raw entries were processed, game `2026.10.06.0000.0000`, native parser MVID `701feb46-428d-4ed6-9fc6-2dcd880a5b7f`. An isolated private reader used the frozen baseline assembly and did not reconstruct modules, mutate bridge replay state or write shared outputs.

| Participant alias | Entry raw sequence | Entry relative seconds | First observed return to main floor |
| --- | --- | --- | --- |
| 8 | 1756553 | 2336.5251869 | Recreated at 2359.4402383 |
| 19 | 1756562 | 2336.5251869 | Recreated at 2359.4557325 |
| 54 | 1757680 | 2338.0800349 | Recreated at 2359.4557325 |
| 12 | 1762147 | 2343.3671168 | Recreated at 2359.4402383 |
| 20 | 1763256 | 2344.9249366 | Recreated at 2359.4402383 |
| 9 | 1765489 | 2347.7191568 | Recreated at 2359.4402383 |
| 5 | 1766084 | 2348.6581347 | Move1771505 at 2359.0166793 |
| 11 | 1766468 | 2349.2800650 | Recreated at 2359.4557325 |

All entries land around `(350,-300,-445)` and all observed returns are around `(-350,1,-445)`. This corroborates the distinct floor height and portal transport, **not its boundary**. Seven participants are destroyed from the recorder's world approximately 0.06–0.14 seconds after entry and reappear on return; these visibility removals are not death evidence. No `dead=true` event occurs for these aliases between entry and return. Actor 5 remains observable, has two additional lower-floor moves at 2353.8183232/2353.9102511, and attacks unknown alias 596 with actions 7477/7/7478 at 2353.8183232–2356.5314970. Alias596 has no own create, cast, position or OID event, so identifying it as Shadow Dahak would remain an inference.

Diabolic Gate OID `0x190B` is actor 592, created at main-floor center at2332.2217835 (raw 1752012), starts Deep Flow 7189 at 2350.9667108 (raw 1767501) with recorded total 27.7 seconds, and is destroyed at 2357.8191904 (raw 1770909) before that cast finishes. Nearby spawns and casts remain main-floor events; neither portal EventObject `0x1EA256` nor a measured stage transition appears. Receipts are `scathach-portal-entry-proof.json`, `scathach-portal-entry-window.json` and `scathach-portal-participant-lifecycle.json` in the private evidence directory.

The already-completed round 5 native module lifecycle retains one `A36DiabolosHollow` instance, primary actor 576/OID6408, active from 2255.7594934 through 2500.5830688, encompassing all portal entries. Its source uses fixed main center `(-350,-445)` and circle 35; `BossModule.CalculateAIHints` assigns that center/bounds before component hints.

An isolated private runner then sampled actor 5 directly. At raw 1766084,1768986 and 1770354, actor 5 is on the lower floor, but the generated module class remains `BossMod.Heavensward.Alliance.A36DiabolosHollow.A36DiabolosHollow`, instance 1/primary 576/active state 0. AI pathfinding center is still `(-350,-445)`, bounds `ArenaBoundsCircle` radius 35, actor-inside=false, special mode `Normal`. The control immediately before entry and after return 1771505 has the same main-floor contract and actor-inside=true; simultaneous actor 1 control at 1768986 is also inside. All six samples succeed without module errors or executed actions. Frozen round 5 DLL SHA256 is `3202717b7778045b73f4ea4c58af8d350252ab873b3cd7121ff74624a78cedcf`, MVID `8e0dd7d7-bf4e-48e9-b024-a79bd15121fd`; private harness SHA256 is `e78730a822e1497c9ad650f094506b6c155d3d1a0c3e84ba97da74fa0d021ae6`. Full report: `scathach-portal-native-hints-round5.json`. This proves a wrong-floor pathfinding contract. These selected samples contain zero forbidden zones/goals, and no actual controller route or harmful movement was executed or certified.

A bounded failover can classify participants by the independently identified lower floor and pause automatic movement with an explicit manual-portal warning. Clearing main-floor zones/goals/obstacles/directions/damage predictions for that actor and applying immediate `NoMovement` would avoid inventing a portal route or safe radius. This is a proposed scoped fallback, not implemented by this read-only investigation or full portal mechanic coverage. Preserve manual target/action control; do not clear the whole `AIHints` object. The standard BMR AI navigation honors `NoMovement`; NormalMovement honors it when special modes are configured `Automatic`. A user override disabling special modes remains an explicit limitation. A replacement navigable portal map still requires verified barrier/hazard semantics and stage state.

## Material classification and exhausted source boundary

The pinned BGCollision contract above establishes material filtering and object overrides. Its standard raycast helper uses filter mask/value `0x4000`; terrain material `0x7004` satisfies that filter, whereas `0x20000` does not. This establishes a concrete difference in ordinary collision-query eligibility. It does **not** prove that the rejected material kills players, that the client samples it for damage, or that its complement is safe.

The pinned vnavmesh extractor independently classifies `0x200000` as unlandable, `0x2000000` as forcibly unwalkable, and low material type `0x11` as an invisible wall. None of these classifications applies to `0x7004` or `0x20000`. Its `DebugGameCollision` and `DebugLayout` readers expose raw material values/masks and effective triangle materials, but contain no death/damage classification for `0x20000`. Targeted online searches for FFXIV collision material `0x20000`, death semantics and the corresponding material constants produced no additional supported primary definition. The concrete remaining missing link is a current client/server hazard-material consumer or an independently synchronized portal-stage observation; neither a generic ground-ray result nor navigation's omission of an unwalkable flag resolves it.

## Exact remaining evidence

The parser gap is resolved sufficiently to retrieve and measure the actual collision meshes. Remaining acceptance gaps are specific:

1. Establish effective collision material `0x00020000` hazard semantics and runtime enablement, including reset, entry and each transition. `C055` collider enablement and state words alone do not establish lethality or timing.
2. Corroborate the recovered radius-40 supporting floor against the active portal hazard rules. The independent supporting geometry and ordinary ray-filter classification are now recovered; the safe playable complement remains unverified.
3. Obtain portal EventObject `0x1EA256` and active-stage/reset evidence. Participant entry/return and floor height are now observed in the original, but visibility loss leaves the interior add/stage lifecycle missing; these landing clusters cannot validate stage mapping or the safe route.
4. Verify server snapshots, last-safe departure, vertical placement and safe route timing before enforcing shrinking movement bounds.

No portal module code, installed binary or shared build output was changed by this investigation. Source mesh recovery and header checks are separate from physical or live gameplay acceptance.


## Implemented conservative portal fallback

`A36DiabolosHollow` now classifies the observed separate region using Y<-100 and X>0. These thresholds distinguish the recorded lower-floor plane from the main arena; they are not a safe-boundary radius. `PortalSafety` is activated last so portal participants receive an explicit manual-movement instruction instead of main-floor personal/global instructions. Its post-component AI guard removes main-floor spatial/damage guidance, goals, temporary obstacles, teleporters, gaze directions and obstacle-map data, and sets immediateNoMovement/ForbidDashes. Target/focus selection, potential targets, queued attacks and existing manual ForcedMovement remain intact. Fresh frames on return rebuild ordinary main-floor hints without retained portal state.

The normal NoMovement mode also feeds ActionManagerEx's manual-keyboard block under its default threshold, so simply setting that mode would contradict the manual-movement instruction. An additive `AIHints.NoMovementAllowsManualInput` flag exempts this fallback from that specific keyboard block; `SpecialModeBlocksManualMovement` remains true for real NoMovement by default and for Pyretic even when the exception flag is set. `AIHints.Clear` resets the exception. Existing casting/manual controls continue to apply. BMR AI and XCAI retain the NoMovement automation hold without changing the XCAI repository. NormalMovement now checks AutomaticMovementPaused before all strategy branches, including Ignore. Plugin execution and Movement.IsMoving consume EffectiveForcedMovement so an automatic request cannot bypass this hold. Full live configuration/input/controller acceptance remains separate; personal maintainer play is optional.

No safe portal radius, staged lethal-floor rule, portal route or new arena is claimed. The inherited main-floor bounds remain metadata that must not be used for portal navigation during the hold. This fallback is incomplete encounter coverage. Synthetic and selected native regression results are tracked in `dun-scaith-validation.json`; they do not establish live safety.
