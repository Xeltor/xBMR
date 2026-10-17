# Dun Scaith: Deathgaze Hollow repairs

Status: see the current maturity assessment at the end. Earlier sections preserve the investigation history; their pending lists are superseded by the later deck, seal, ice and one-shot-floor evidence. Build/replay and live gameplay are distinct claims. The raid-wide owner records the final build/test/replay results.

## Written guides read before implementation

Primary descriptive guide, following the user's preference: [FFXIV Console Games Wiki, Dun Scaith](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith), Deathgaze Hollow First Half and Second Half. Correct duty: level 60 normal alliance raid, not the Palace of the Dead Deathgaze. It describes repeated centre pulls, green tornado spreads, striped cones, radial knockback into the purple walls, north/south wind stopped by ice blocks, Doom cleansing, and floor squares that become lethal when Void Death IV resolves. The latter half adds Bolt of Darkness overlapping the central wind and reduces the number of ice blocks.

Additional written context: [FFXIV Pocket Guide, Dun Scaith Normal](https://ffxiv.guide/hw/dun-scaith), Death Gaze Hollow sections. It distinguishes the centre wind and half-ship column combo from the ice-block combo. [Tiercel Noire's Lodestone guide](https://na.finalfantasyxiv.com/lodestone/character/1039312/blog/3939753/) contains a first-person concise account: avoid falling, cleanse Doom, move out for proximity before lining up with ice, choose the opposite side from the boss for central wind, leave flashing squares. These accounts support responses, not precise hit boundaries or snapshot measurements. The Console Games Wiki names the fan cue Void Blizzard II; recorded boss casts are7284/7285 with helper7306 named Void Blizzard III. Action identities take precedence over that descriptive naming discrepancy.

## Evidence identities and limits

Full schema3 archive recording `901ecf79648702210c1227cf26422f5ddc1399f5a8c28b65bf3ff6c84f57dc96`, retained revision `b73cb28753f888e02264ad9459cf9146bcf37dcb1424c0907653cc37cdc801ec`, original digest `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`. Complete upload: 101 parts,1,639,349 retained operations, 2,513.7469857seconds, zone 627/duty 220, game 2026.10.06.0000.0000, BMR 7.5.6.35. Primary actor 49/OID0x190F, helpers 43–48/50–52/OID0x1911. Reviewed first Deathgaze fight 128–540seconds; the full file includes later bosses and transitions. All selected occurrences belong to one original; no independent holdout exists. The short recording `fcade708...` lasts 33seconds and contains no mechanic casts, so it provides no mechanic evaluation.

The local original was also projected using the existing native decoder to `events.jsonl` with original operation sequence numbers. `raw-cues.jsonl` contains omitted legacy map effects, NPC yells and system-message IDs. No legacy map effect appeared in this Deathgaze interval; that does not prove there was no client-side arena effect.

The archive game-sheet evidence is 2026.09.15.0000.0000, older than the recording. A separate read-only export of the installed 2026.10.06.0000.0000 SqPack confirmed the selected Action/Omen/Attract/Knockback fields against the exact recording version. Full manifests and selected rows are in `current-game-selected-rows.json`. Action manifest SHA256 `ffedd32dd62674b7081c7b6ef461a41026ee1a8daf886d399f1b1160308483d2`; displacement manifest SHA256 `f6361dfc1e5d0b7aa5952dbf87fdfd10622ddcf864cc5997b228b3d46400364c`. Relevant row fields match retained module constants and recorded effect references; independent client collision extraction is described below. The archive refuses semantic effect interpretation for the 7.5.6.34 reference versus 7.5.6.35 recording. Raw effect hex was therefore cross-checked against this checkout's `ActionEffectType` and `KnockbackDirection`, without relabeling archive parser/runtime identities.

## Contracts and changes

| Mechanic | Cue, lifecycle and implementation | Evidence |
| --- | --- | --- |
| Bolt of Darkness | Activate previously disabled helper 7315 rectangle, 31.5y forward / 10y half-width; helper cast start gives 6.8s warning and cast finish removes it. Both directions use recorded helper facing. No timer fitted to boss order. | Helper cast 133495 at 242.6497889s, action 140385 at 249.4118449s; other direction 210814/215286; repeated 295456/300997. Sheet 7315 cast 6.8s, range 31 plus helper 0.5, full width 20, row SHA 294ff596f480fc813ce6c267811b3edcb839c51179d16707c1ebf72cf98dd030. |
| Void Aero II | Actor-based 5y spread from icon 70, all three alliance targets, per-target removal on 7308. Duplicate icons retain one assignment; death/destruction/recreation clears it. A bounded fallback removes unresolved assignments 2s after the conservative deadline, preventing indefinite stale markers after cancellation or missing helper resolution; this timeout does not assert a server-side cancellation. Native stack/spread supplies both player and AI hints. Conservatively prepare at 5.5s, about 0.44–0.49s before observed hit; that response margin is not a measured server snapshot. | Eight icon groups / 24 targets: 58611–58624 →63986–63988;124357–124364 →128230–128232;152359–152366 →155583–155585;181944–181949 →184835–184837;204354–204357 →206899–206901;225948–225955 →230077–230079;264029–264036 →267548–267550;288633–288640 →290945–290947. Observed icon-to-hit 5.9367436–5.985334s. Sheet 7308 radius 5, row SHA bdff607020647b359f38448857c249dbb34212c0714f2855f6bda29a5b5801ee. |
| Biting Wind | Existing/live FierceWind actors create persistent 4y circle obstacles, removed on death/destruction. No guessed delayed hazard: existence is the earliest modeled cue. | First actors 64317–64319 at 161.3508707s, later BitingWind actions 67775 etc. Six subsequent groups plus eighth group near 507s. Sheet 7317 radius 4, row SHA 126505a645a05b6df81599dac40d37a9d879fd973db349f586709b167dcbb5b4. |
| Void Blizzard III | Correct reference 60y cone to conventional 40.5y (current Action 7306 range 40 plus observed 0.5y helper hitbox); retain existing 10-degree half-angle explicitly unverified. Removes unsupported overlong forbidden space; radius is not tuned to recorded player positions. | Helper starts 67859–67863,158554–158558,233818–233822; current exact-version Action 7306 cast 3s/range 40/cone type 3, helper create 22462/22483/22484/22485/22527 hitbox 0.5. Synthetic probes 40.49/40.51y assert the new boundary; physical hit boundary and fan angle remain pending. |
| Void Aero III centre | Add omitted 10y helper 7309 damage circle from its 6s cast. Distinct from helper 7310 radial 20y displacement and overlapping Bolt. |75250/80066,133498/139494,210817/214801,295461/300321. Sheet 7309 cast 6s / radius 10, row SHA 4566f46227b6917a69e9ead27d2249832c6501e25207d7a606d028944c5d8dda. |
| Void Death pulls | Replace cast-driven components for actions 7313/7314, which never have casts and therefore never activated. Helper 7312 lethal 9s cast now predicts first 40y attraction at finish−8s and second 15y at finish−3s. Resolution removes each source; cast cancellation/destruction clears all predictions. AI must be outside 25y before second pull, retaining 0.3s response margin; this is 15y displacement plus retained 10y centre danger, not an inflated physical hazard. The first attraction is recoverable and does not forbid every playable point. Immunity skips this arrival restriction. | Helper 42902 →first 43293 at+1.050267s →second 48578 at+6.0202967s →centre hit 52375; repeated 167524/168170/172873/175588 and247854/248545/252974/255853. First attract observed1.0274874–1.050267s, second6.0114008–6.0299672s. Raw effects0x0039000000000020 links Attract 57,0x0040000000000020 links Attract 64. Rows: 57 distance 40/min remaining 0/hitbox distance; 64 distance 15/min remaining 0/hitbox distance. RowSHA6c1972fc0c7f5a7bd8a5d64915cec07aded3fbffaedd0d57f75cf9c7cf5b1934 and9eaed00fb3131bd056e0d3b716c085b07c5e58f45eded121ed1364678e4796d9. |
| Void Aero IV | Replace two boss-visual components 7292/7293 with actual helper 7311 cast, forward 37y. Boss visuals finish 0.5s earlier, so they cannot own the displacement deadline. Remove blanket stop-at-wall behavior, which incorrectly treated lethal open edges as solid. Active ice supplies exact8×8y client collision obstacles and four physical stopping faces. AI aligns its37y ray with a live block at helper finish−0.3s, with a player-radius lateral corner margin. Reject persistent tornado landings. Immunity skips the arrival restriction. Missing ice animation evidence supplies instruction without an all-arena restriction. |108934/113121 (north helper facing 0);195031/197287 (south facingπ);277582/280607. Action-minus-cast 2.9730467–2.9902018s. Raw effect0x005d00000000001f links Knockback 93, distance 37 / direction 3(source-forward), row SHA eb215ad5d213f02eabf894e5df243fd85b6a6a72da8832171b146f6d8c128001. |
| Doom / proximity / floor warnings | Activate existing cleansable Doom component for status 910. Show BlizzardIV response from boss 7286/7287 and floor instruction from 7298/7299. These warnings make omissions visible without fabricating proximity falloff or the square hazard interval. | Guide response plus recorded 7300/7301,7286/7287,7298/7299 casts; square EAnim 16/32 immediately precedes floor cast; see unresolved geometry below. |

Retained DarkII, striped-cone half-angle and arena dimensions remain reference-derived. Dedicated ice callback probes additionally cover duplicate activation/cast starts, physical4y boundaries, conservative corner margin, both directions, unknown state, generation replacement, stale destruction, immunity, tornado landing, helper cancellation and helper destruction. This repair does not claim they became physically calibrated through compilation or callback tests.

## Historical geometry and acceptance gaps at the initial audit

This historical list predates the independent deck/seal recovery and one-shot floor evidence below. It is not the current maturity gate.

- Existing arena remains centre(300,410), half-width30/half-height15. Player positions do not verify edge locations. Lethal north/south ship edges and solid purple-wall segments need scene/collision evidence. Generic radial displacement is displayed without a false guarantee that every edge stops movement; its exact safe destinations and AI arrival/path constraints are pending.
- Ice alignment now uses independently extracted client collision dimensions and reliable on/off branches (see below). Server collision response, last-safe departure and live BMR/XCAI movement remain unverified. The0.3s deadline and player-radius corner inset are conservative response margins, not measured server timing or altered physical dimensions. Unknown animation state clears stopping geometry. Destruction/recreation clears the matching actor generation; a late destruction callback from an old generation cannot remove its newly activated replacement. Unknown lifecycle state does not establish a stopping block. Current AI arrival predicate rejects known tornado destinations; a collision-free continuous route and unknown dark-floor overlap are not proved by an aligned ray.
- Dark-square objects2007609–2007616 bind explicit15×15y client boxes and matching15×15y event ranges; this size is independently supported, rather than inferred from grid pitch. Their harmful interval remains unresolved. Stored timeline state and immediate timeline bitmask are separate fields:16/32 stores `df_act` but plays `df_omen`;4/16 stores `df_on` but plays `df_act`. C055 bound to collider10 switches on only in `df_act`. Its release/restoration behavior must be established before an AI expiry is claimed. Current warning remains informational. Sequence143341/143342→boss143426; repeated163679–163681→163736 and217023–217027→217075.
- Proximity falloff/safe distance for 7307, exact fan angle/range 7306, primary hitbox/range reach, collision immunity and last-safe departure require independent calibration. Exact-version sheet field verification does not settle those physical boundaries.
- Native replay checks need samples before cast start, after start, before/after helper actions, cast cancellation and reset. Evaluate both AeroIV directions, both Bolt directions, all spread groups, repeated pulls and overlaps. Synthetic callbacks assert registration/AI predicates/lifecycle only. Live BMR/XCAI survival, movement stability and routes have not been accepted.

## Installed scene/collision evidence

Read-only SqPack inspection used the exact installed2026.10.06.0000.0000 game. A private Lumina scene inspector and bounded timeline decoder in an isolated evidence workspace read assets; no game process, plugin binary, sibling source or sibling build output was modified. `docs/dun-scaith-deathgaze-geometry.json` preserves asset paths, hashes, transforms, mappings and native sample boundaries.

Territory627 maps to `ex1/05_zon_z2/rad/z2r3/level/z2r3`. Its `planmap.lgb` EventObjects bind by `BoundInstanceId` to SharedGroups whose asset path is `bg/ex1/05_zon_z2/shared/for_vfx/sgvf_z2r3_b1053.sgb` for all six ice objects. The prefab CollisionBox instance6 is an explicit box, local translation(0,8,0), scale(4,8,4), rotation0; horizontal full size8×8y. Actor positions supply the placed transform only; neither actor hitbox nor observed player positions establish these dimensions. Both `ic_on`/`ic_on_anim` contain frame0 C055 bound to collider6 with value1; both off branches value0. Handler map slots0–3 resolve `ic_off`, `ic_off_anim`, `ic_on`, `ic_on_anim`. Recorded EAnim4/8 and1/2 correspond respectively to on and off. Repeated native samples independently exercise two-block and one-block patterns in both directions.

Floor prefab `sgvf_z2r3_b1046.sgb` CollisionBox10 is local(0,−0.5,0), scale(7.5,0.25,7.5). Eight independent EventRange boxes6697976/6697981–6697987 also have horizontal half-extents7.5y. This supports dimensions without assuming server damage/kill behavior or when a one-shot animation restores its stored state.

Parser conventions were checked against primary source: [Lumina LayerCommon](https://github.com/NotAdam/Lumina/blob/e383f839cb6ecefdedeb045554dbb39ce8f07065/src/Lumina/Data/Parsing/Layer/LayerCommon.cs), [FFXIVClientStructs FileFormat](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/FileFormat.cs), [EventObject animation semantics](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/Game/Object/EventObject.cs), and [ffxiv_navmesh scene extraction](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/SceneExtractor.cs). Navmesh transforms unit cube vertices±1 by the box scale, independently confirming half-extents. Older Lumina names for the high material/path fields are incompatible with the current FCS layout; no `PushPlayerOut` interpretation was used. Current executable read-only disassembly independently confirmed handler indices are copied from handlerblob+5 into TimelineIndices and PlayTimeline indexes that array. These client asset facts are distinct from server collision and live encounter acceptance.

Regression fixture correction: the placed south ice at z420.6 has measured faces z416.6/424.6. The initial “behind block” probe z423 was inside the physical obstacle and legitimately had a forward intersection with its far face. The corrected behind-block probe is z424.9 (still inside the retained arena bound425). Direct ray probes verify a front-face hit from z410 and rejection of the negative intersection from z424.9; this corrects the fixture without changing the collision rule.

Native replay correction: `BossModule.OnActorEAnim` packs `(param1 << 16) | param2`. The ice on pair[4,8] is therefore `0x00040008`, and off[1,2] is `0x00010002`. The initial source accidentally reversed these fields and native round2 correctly showed no ice alignment zones. Corrected matching is covered through `ActorState.OpEventObjectAnimation`, including a deliberately swapped pair that must not enable collision. Tornado overlap checks use `ActorState.OpCreate` after the lazy enemy cache is initialized; direct dictionary insertion had bypassed the native cache update and made the fixture omit the tornado. These corrections require a fresh root build and native replay before acceptance.

Native object-ID correction: ice EObj rows are decimal2007603–2007608 (hex1EA233–1EA238), and dark-floor rows2007609–2007616 (hex1EA239–1EA240). The initial manually converted hex filter selected different rows and therefore native round3 still supplied no ice constraint. Production matching now uses the exact decimal row IDs from the planmap/native creates. The typed native fixture pins actor33/OID2007607/position(305,2.87,420.6)/hitbox2 to raw create63 and actor27/OID2007605/position(319,2.87,398.6)/hitbox2 to raw create57. These native IDs and parameter packing must both pass an A31-only replay before broader validation.

## Dark-floor runtime continuation (read-only)

The exact installed executable SHA256 `b38840761e2677128b6bec1ceaee3f2870cd84e1d38ab7958216373058383780` and version-file SHA256 `3da1d28ba647edae481c8ea61eeb1fae9a3d1d700ac703165e4b3e73a66d57ac` were checked against the previous receipt; game version remains2026.10.06.0000.0000. Read-only bounded disassembly now independently establishes C055's setter semantics. Its registration at VA0x1428c5730 identifies kind43, factory0x1418c67a0 and vtable0x1422e9180. `Publish` slot31 at0x14192d5b0 dispatches kind43 to0x14192d671, reads `Data+0xC !=0`, and calls the bound layout instance's virtual37 (`SetColliderActive`). Thus the floor's C0551/0 values describe actual client collision activation, rather than an inferred timeline-name meaning.

The specialized hooks at virtual23/24 only clear clip-local flags. Virtual32's target-reset paths at0x14192e140 handle kinds45/46 and skip kind43; its destructor calls the generic clip destructor and has no C055-specific collider undo. These facts do not establish a whole-timeline expiry. In particular, they supply no proof that the collider disables at the C055 entry end or after the enclosing raw length108.

The EventObject layout-ready path0x141757ff0 replays stored SharedTimelineState at0x141758045 only while its byte+0x1B8 is0; it then sets that guard to1. Draw/layout reset paths clear the guard. This establishes initialization/reload replay, not a callback after a one-shot animation ends. The inspected per-frame EventObject update and timeline start/stop wrappers do not establish that missing callback. The exact remaining decoder boundary is enclosing SchedulerTimeline/TimeLineLayoutInstance completion-event propagation, any parent SharedGroup/EventObject state replay or collider-reset callback, its invocation order, and its time base. No guessed108/30s timer or hard floor hazard was added.

For actor29, raw animation146058 at256.180875s starts `df_act` ([4,16]); its next animation163679 at296.6673607s plays `df_omen` ([16,32]). The40.4864857s packet gap does not exclude a client fallback and cannot itself establish an expiry. Even exact client collision shutdown would remain separate from server lethal-floor behavior and last-safe movement timing.

Primary semantics: [BaseClip publication](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/System/Scheduler/Clip/BaseClip.cs), [ILayoutInstance collision control](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/ILayoutInstance.cs), and [EventObject state controls](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/Game/Object/EventObject.cs). Private receipt `floor-lifecycle-runtime-continuation.json` preserves selectors, addresses, disassembly hashes and the immutable asset-cache receipt path. This pass changed notes and private receipts only; it did not build the plugin or perform a live experiment.

## Ship-seal state mapping (read-only)

The exact client binary independently links native EventState to the b0250 seal's collision, with normal loaded-layout/visibility conditions. `GameObject.SetEventState`0x14089b130 writes byte+0x70 and sets dirtyflag+0x9B bit8. Base per-frame Update at0x14089ad1a invokes EventObject vfunc38→0x141757ff0→state updater0x14089b250. Its state predicate0x14089b150 returns false for(state&7)==7 and true forstate0. In the normal event-object path this sets/clears visibility reason8 through0x14089b7d0/0x14089b730; aggregate mask transitions drive SharedGroup hide/show through0x14089e370/0x14089e2b0.

The exact b0250 SGB animation blob has type3 at+24 and subIDs3/4 at+1/+2. Initializer0x14070447b copies these into SharedGroup+0x145/+0x146. The show/hide handler0x1407066d0 selects3 for true and4 for false at0x140706844–0x1407068a7, then starts the selected timeline. SubID3 `vf_bextwall_on` enables collider9 through C0551; subID4 `vf_bextwall_of` disables it through C0550. This supports state0 as the active solid seal and7 as inactive under the normal ready, otherwise-unblocked object conditions. Other visibility reasons and unloaded/destroyed layouts can suppress collision; this mapping does not certify server timing or live movement.

Native east seal actor26/OID2002872 starts state7 (raw56), changes to0 (47166 at143.5423686s), returns7 (307080 at530.6463162s), then0 (323285 at556.7235366s). West seal actor53/OID2007602 is created at raw23107/118.4405235s and has no state operation before600s in this projection; replay default0 is distinct from an observed7→0 transition. Do not assume both doors started state7.

The `SetPosition` signature's trailing OR[RBX+0x70],1 does not modify EventState: unique callsite0x14180c661 passes the GameObject inRDI asRCX, while RBX is a different caller object. Its callee0x1408993e0 writes GameObject position+0xB0/B4/B8 and invokes PositionModified, with no EventState write.

Private receipt `door-state-runtime-investigation.json` preserves exact game/executable/asset hashes, addresses, native state events, disassembly hashes, cache receipt and conditions. The [pinned GameObject definition](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/Game/Object/GameObject.cs) identifies the EventState field and setter; the mapping above comes from the installed executable and actual SGB, not from its undocumented name alone. No encounter source or build output changed in this inspection.


## Dark-floor scheduler continuation (read-only)

A further bounded trace of the same exact executable follows explicit stop and natural completion beyond the C055 clip. SharedGroup stop reaches SchedulerTimeline0x141c00500 → controller0x1418fe4a0 → actor0x141c06800 → clip group0x141c2f9f0 → generic clip virtual58 at0x1419049b0. That hook invokes virtual32 at0x141904a12; the established C055 cleanup skips kind43 and supplies no collider reset. Ending playback through this route does not itself undo the setter.

SchedulerTimeline constructor0x141bfd670 installs vtable0x1424447e0. Its natural `IsFinished` slot4 at0x141bfdad0 checks child completion through0x1418fda40, which calls completion slot22 at0x141bfeb90 with argument1. That branch sets a local completion flag and gates its CharacterTimelineContainer notification on ActionTimeline row+0x224 being different from−1 and a bound GameObject. The constructor initializes that row to−1; the floor's empty ActionTimelineKey uses embedded TMLB setup0x141bfff00 and retains−1. This inspected notification is therefore gated off for that creation route. The separate layout completion path0x140710820→0x14071f190 locates the TimeLineLayoutInstance by scheduler handle, requests release and clears its handle. It does not start a persistent branch, replay EventObject state or change collider activation. These are bounded callback facts, not an exhaustive absence proof for every parent/visibility/unload path.

The clock trace also distinguishes a rate factor from elapsed time. SharedGroup0x140706d30 forwards its float to0x1418cab30→0x141c008e0, which writes Timeline+0x240; the main timeline tick multiplies its advance delta by that field. Scene update0x140471000 forwards a separate float unchanged through SchedulerManager0x1418cce90 and group0x141992490 to tick0x141bff5f0. Advance0x141bffa10 splits the input into at most1-unit pieces; controller0x1418febd0 adds each delta to `CurrentTimestamp`+0x34 and clamps at end+0x40 for the one-shot path. The caller's time-unit contract and applicable time-scale/pause conditions remain unresolved. Play's optional uint argument is independently converted as `a1*30/1000` before seeking; that conversion alone does not establish either the caller units or a safe108/30-second expiry.

The remaining decoder boundary is now narrower: establish the scene-update input units and floor end event, and identify any independent SharedGroup/TimeLineLayoutInstance completion polling or parent EventObject restoration route. The inspected C055 cleanup and scheduler completion callbacks provide no such restore. Do not assume stored `df_on` automatically plays at `df_act` completion, or assume collision necessarily remains active throughout the40.4864857-second packet gap. Server lethal material behavior, harmful interval and last-safe departure remain separate evidence requirements.

Private receipt `floor-lifecycle-scheduler-continuation.json` (SHA256 `56de9cf3afca5abed056374b5c151b89fd1cf5693e51779ff8423e9748129009`) records addresses, bounded disassembly selectors/hashes and exact game/asset/source identity. Immutable cache receipt: `<private evidence location>`. No encounter source, tests, plugin build output or live state changed in this pass.


## Recorded floor strikes and forecast implementation

This section supersedes the earlier informational-only floor coverage. A private numeric reader against immutable candidate-round8 parsed a byte-identical original copy (digest `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`), completed all1,890,796 raw operations, and selected player movement/health/lifecycle through790seconds. The owned copy was removed after completion. It retained25player create generations across24recording-local aliases,428,348movement operations,5,104HP/MP updates and150dead-state changes; no names or persistent player IDs were exported. Parser MVID `828c299e-47d5-4a38-88d5-44db3611e480`, candidate DLL SHA256 `8b5c34e09c596814bd04b7317995aa344e72ee9f8141eacd8686fede29d18fcd`. Private manifests, source hashes, coverage and selected observations are in a private evidence receipt.

The original contains two Deathgaze attempts: boss49 then113. Seven floor warning waves enumerate2,3,5,6,8tiles in the first attempt and2,3in the second. Native4/8 prepares a tile;16/32 warns every tile selected for the next strike;4/16 is the later impact/resolution cue;1/2 resets. Warning precedes4/16 by4.4269536–4.4601000seconds, about0.077–0.088seconds before the boss cast starts. The boss action/cast-end precedes4/16 by about1.37–1.39seconds and cannot own forecast removal. Raw warning/act samples include143341–143342→146058–146059,163679–163681→165985–165987,217023–217027→219201–219205,242625–242630→245670–245675,303299–303306→306660–306666/306704,444395–444396→450066–450067,484743–484745→487290–487292.

Player15 was freshly positioned inside tile29/OID2007616 at raw146044, (326.5195,2.8698258,413.1573), then HP2798→0 and dead at146045/146046, the same timestamp256.180875seconds as tile29's4/16 raw146058. The eight-tile wave records six remaining players'HP→0 at529.8038798seconds, with three fresh same-tick interior positions. No action targeting these selected victims within±1second was found in the numeric projection. These correlated cues support a floor strike response; their shared frame does not prove a server snapshot or causal callback order.

An unprotected surviving return independently constrains the persistent-hazard interpretation. Player54 entered the exact warned/activated tile32/OID2007611 at257.7950766seconds (+1.6142016after impact), remained inside its0.5y-inset footprint for6.2814072seconds, and had positiveHP updates10848/11010/13757 at raw147596/148859/149151. Its active statuses were only2677SurgingTempest and91Defiance; no recorded tank invulnerability or resurrection immunity. Further unprotected returns with in-run positiveHP updates occur in wave2(player54/tile32,+4.837s), wave3(player20/tile36,+2.836s for3.267s), wave4(player20/tile42,+6.711s for4.072s), and second-attempt wave6(player3/tile29,+21.948s for3.062s). Later long stationary runs beginning with resurrection status418 were excluded from these examples. This supports a one-shot strike model and conflicts with assuming selected tiles remain unconditionally lethal until the next40-second animation packet. It does not measure the earliest universal safe return or resolve the client C055 lifetime.

`FloorStrikes` now forecasts exact15×15y per-tile strikes from16/32. AI and drawings receive activation at warning+4.1seconds: a conservative0.327–0.360second response margin before observed impact, explicitly not a measured last-safe departure. Same-generation4/16 removes only that tile's forecast; boss action/cast-end cannot remove it. Prepared4/8supplies no hard hazard. Reset1/2, destruction, replacement, unknown state and module reset clear unsupported predictions. Duplicate warnings preserve the original deadline. A missing resolution retires the stale forecast2seconds after its conservative deadline and retains an explicit global “Floor strike cue lost” warning until another state/reset or actor lifecycle clears it. This is lost-evidence handling, not a physical safety or expiry assertion. No persistent client-collider zone or post-impact safe route is generated.

Footprint rotation comes from placed assets: all eight SGB transforms are identity or horizontal half-turns, including equivalent pairedX/Z half-turns, with unit scale; square collider10 local horizontal translation0 and half-extents7.5. These transforms preserve world-aligned square axes. The native actor facing−0.000047922134 is not substituted for the authored SGB rotation. Regression probes pin native decimalOID and parameter packing,15y boundaries, early AI timing/player warnings, duplicate deadlines, earlier boss-action nonresolution, per-tile resolution, resets, unknown state, generation replacement/stale callbacks, missing-cue warning, strictOID range and the eight-tile wipe followed by a fresh two-tile second-pull pattern. Root owns the fresh plugin build and native replay validation; these source/fixture changes alone do not prove live BMR/XCAI movement or server timing.

Private original findings receipt `floor-recording-findings.json` SHA256 `eb2001c25bb728825c1b7e27262484615ef5de9de6a0aaafde72cbe59ca08a65`; guarded one-shot proposal `floor-one-shot-proposal.json` SHA256 `0b6ab988830e84204a6f2e771fc0e6cadeb0448b5e175f7544c5ced3b7464902`. All selected occurrences belong to one original; no independent holdout or live acceptance is claimed.


## Void Sprite target priority

The preferred [Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith) explicitly calls for killing the two Void Sprites. Native original creates pairs69/70 at152460/152464,83/84 at226063/226066 and140/141 at464873/464877. Previously this module drew them and their cones but supplied no add priority. Eligible living/targetable sprites now receive at least priority2, preserving higher priorities and all negative forbidden/invincible/dying states. This preference does not force a target or tank position and leaves arena and movement constraints unchanged. Native source-selected target fixtures and synthetic unavailable/priority controls are reported in the raid validation artifact; they do not establish live target selection.

## Current maturity assessment after7.5.6.35.4

Review base `bd9bf9228`, using the [module maturity policy](module-maturity-policy.md).
The preferred normal-alliance guide was refreshed on2026-10-14. Its known early
and late mechanics are accounted for: repeated pulls, three-alliance spreads and
persistent tornadoes, five narrow cones, central wind, both Bolt directions,
two-block and one-block directional winds, Doom, adds and one-shot tile strikes.
The asset-backed deck/seals, ice stopping geometry, per-instance lifecycle and
known overlaps have already passed the released offline validation. The release
rerun used all49 unchanged source documents and64 Deathgaze native samples within
729 raid contract assertions. Those historical validation identities remain
unchanged; the proximity repair below needs the owner's fresh build/checks.

### Cone and overlap review

Exact installed Action7306 links Omen134 to
`vfx/omen/eff/m0307_blizzaga0h.avfx`, SHA256
`61dc8ca4196171c7b5537e9614be5ea4a5e12da19a5e090f960f7e9806d663ab`.
Its two procedural decal-ring particles both store RF0.007596135139465332. That
matches the two main particles of exact installed standard
`gl_fan020_0f.avfx`, SHA256
`6cbae4742b3d1cedd3118ff283e83d94823470e277451e8f97e135ab0833f8c5`.
This corroborates retaining the nominal20degree fan rather than widening it from
a filename or a player's path. Action7318 directly links Omen3
`gl_fan060_1bf.avfx`, SHA256
`79adff230e6fa67a37a8d448436ea153486d9fdb303e4285d779c8961a90c703`;
its main particles store RF0.0669873058795929, supporting the retained nominal
60degree fan. These are same-action visual/reference corroborations; the
procedural shader mapping and exact server boundary are not claimed decoded.

The original supplies an actual DarkII recipient at29.3704degrees/17.3681y,
raw239888/player20 with a same-tick position. For Blizzard, all25 helper effects
have no listed recipients. Living nonrecipients have preceding positions as
close as10.6492degrees/24.9755y at160284/player20 (position age0.799s) and
10.8745degrees/23.4313y at479728/player1 (age4.004s). Those observations neither
authenticate server snapshots nor prove standing still; they do not contradict
the retained nominal angles. Fresh synthetic probes protect both sides of the
10degree and30degree boundaries without relabeling them physical measurements.

Known radial-to-floor sequences are separated: warnings start3.107–3.130s after
the radial hit and floor strikes resolve7.534–7.575s after it. The floor component
therefore provides over4s of subsequent warning rather than requiring an
invented same-arrival floor exclusion. The actual Bolt overlap within about0.8s
of radial arrival is already checked by the landing predicate. Ice winds,
tornado arrivals, both directions, corner margins and seal/open-edge behavior
retain their dedicated regressions. These available observations show no
additional material cone or knockback/floor defect. Exact snapshot calibration,
hypothetical undiscovered patterns and personal live play are not automatic
promotion blockers under the policy.

### Supported proximity-warning repair

The previous `VoidBlizzardIV` warned only during boss7286/7287's3s preparation.
The warning disappeared just before actual7307 helpers started their5s casts,
leaving the entire impact-response window without module guidance. Original
100679/100680→106931/106933 and397718/397719→404057/404059 show two simultaneous
sources;190500→193956 and272541→276029 show the later single-source patterns.
Helpers start about0.05s after the boss visual finishes. Their locations directly
identify the future ice markers, and their native cast timing identifies the
future impact. The next directional wind begins about1.51–1.56s after impact,
then resolves about2.98s later. The guide's opposite-side preparation followed
by lining up with ice remains actionable without maximizing distance blindly.

`ProximityImpacts.cs` now preserves the earliest boss preparation instruction,
then tracks each actual helper generation, location and deadline. The global
instruction remains through the impact window and shows the current countdown.
Small crosses mark the helper locations; the40y Action effect range is exported
as informational rather than rendered as a safe/danger boundary. Each helper
supplies future healing planning across living raid participants. Its actual
effect, cancellation, destruction, generation replacement or bounded lost-event
cleanup removes only that source. Duplicates preserve the first deadline;
mid-cast bootstrap recovers remaining time. There is no hard guessed safe
radius, outward movement goal or forced route that competes with ice preparation.

New native-callback regressions cover the visual-to-helper handoff, two-source
and one-source retention, actual future timing, all-alliance planning, duplicate
observation, per-helper effect/cancellation, generation replacement/stale
callbacks, destruction, bootstrap, stale fallback and reset. Root owns the
fresh complete plugin build and execution of those tests and native anchors.
Private extracted assets, parser results, observation ages and overlap facts
are retained under `deathgaze-maturity-audit` in the existing evidence workspace.

### Acceptance decision

The supported source repair closes the last concrete warning/lifecycle gap
found by this review. On the available guide, exact scene/assets, two-attempt
original and prior offline evidence, the known human safe responses are
accounted for. An automatic proximity solver and unmeasured damage-falloff
threshold are not required to claim `Verified`: the module explicitly gives
the reliable response and never advertises a fabricated safe circle. The
remaining physical precision limits are documented rather than treated as
proof of an unsafe region or as mandatory personal testing.

Accepted as `Verified` after the final combined validation below. Personal
play is not required and `AISupport` is not claimed. Reopen if a credible report identifies a material geometry,
timing, assignment, transition or known-pattern defect.

## Final follow-up validation — 2026-10-14

The current-tree Release rebuild succeeded with SDK10.0.401, zero errors and
six existing obsolete-API warnings. Frozen assembly SHA-256 is
`5f88ad34689d6174baf8886e8915fc71bb31ecaf4ad6cfcf1db7aeb8285333c2`;
all51 selected Dun Scaith/core source documents match its PDB and debug identity.
All9 regression suites passed2,973 synthetic assertions against that exact DLL.
Nine native runs supplied402 available checkpoints, zero module errors and
matching game data;716 coordinator contract checks passed. The current
[validation receipt](encounter-acceptance-validation.json) records per-suite
counts, source/build identities and export limitations. Historical receipts above
remain associated with their original artifacts. No live gameplay, exact server
snapshot, complete automatic solver or AISupport claim is made.

Deathgaze187 synthetic assertions;64 preserved native checkpoints plus14
new proximity checkpoints.

Deathgaze is accepted as `Verified`: the known response review above and
current validation close the concrete proximity warning/lifecycle omission.
