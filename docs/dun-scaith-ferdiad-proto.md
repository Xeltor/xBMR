# Dun Scaith: Ferdiad Hollow and Proto Ultima corrections

Review history, 2026-10-14. Ferdiad is accepted as Verified in the final
follow-up below; Proto Ultima remains WIP. This is evidence-backed
repair work, not independent validation or a claim that all mechanics are complete.

## Written guide review

Preferred guide: <https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith>, normal level-60
alliance raid, specifically its Ferdiad Hollow and Proto Ultima sections. Read before
implementation. Ferdiad's scythes persist; Wormhole exits depend on projectile/portal
identity; Debilitator requires changing puddles to the opposite element; Jester's
Jig is unavoidable. Proto Ultima has proximity explosions, add control, and a
collector/enrage phase. The page calls the laser trio Magitek Ray; recorded IDs
7574–7576 are named Aetherochemical Laser by the current Action sheet.

Supplemental Japanese guide, also the normal raid:
<https://gameranbu.jp/ff14/b8307965c74648a6a92a>. Its Ferdiad section corroborates
the opposite-element puddle response, projectile-shape-dependent exits, and add
phase. Neither guide establishes exact hit boundaries or server snapshot deadlines.

A searched alternative, <https://ffxiv.wtf/allianceraids/dun-scaith/>, describes
Debilitator as a spread and Flameflow as lines, contradicting the preferred guide
and observed raidwide helper actions. It was not used for implementation.

## Evidence identity

Training source only:

- Archive recording `901ecf79648702210c1227cf26422f5ddc1399f5a8c28b65bf3ff6c84f57dc96`.
- Original source SHA-256 `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`.
- Closed original `DunScaith_2026_10_14_18_01_48.log`, complete schema-3 projection,
  2513.7 seconds; archive sequence numbers below are not native replay operation indices.
- Pinned game fields `2026.09.15.0000.0000`, evidence database SHA-256
  `6f98b919501d005d944c4840e714df456143dd4cb3a60285f4463a666637d86b`.
  These are typed Action/Omen inputs, not independently measured physical hitboxes.
  Recording/current installed game is `2026.10.06.0000.0000`; the dedicated
  regression reads current Lumina rows and checks applicability of fields used
  for the correction. Asset filenames/meshes are not treated as matched-version
  physical proof.
- Initial 33-second upload `fcade7080b9af5db06b50cfdcc74e14b2c8d82033a3f50e1e1c542245cf475ac`
  did not contain encounter casts and was not used to tune mechanics.

No mechanically independent evaluation recording has been reserved; a projection
or a native parse of the same original is not a holdout.

## Ferdiad Hollow

### Reproduced defects and corrected contracts

1. **Wrong/expired Wormhole hazards.** Incoming portal visual 7241 was incorrectly
   a radius-20 damage circle. Exit visuals 7324/7325 had warnings that vanished
   before actual explosions 7329/7330. Remove the incoming false hazard; create
   exit forecasts and retain them after successful visual completion. Match actual
   explosions spatially, replacing only their own forecast, and retire on their
   own action. Cancelled casts and source destruction clear corresponding state.
   Repeated callbacks replace state rather than duplicating it.

   | Variant | Exit start | Exit visual resolution | Explosion start | Explosion action |
   | --- | --- | --- | --- | --- |
   | Circle | seq701057 / ticks9391406322 | seq709427 / ticks9476043631 | seq711676 / ticks9500807216 | seq712470 / ticks9510729218 |
   | Donut | seq730657 / ticks9724585160 | seq739021 / ticks9809297690 | seq741660 / ticks9834209524 | seq742995 / ticks9844187240 |
   | Double circle | seq832959 Wormhole anchor | seq840481,840484 / ticks10896702097 | seq843279,843281 / ticks10921671983 | seq844772,844777 / ticks10931357203 |

   Observed post-visual action gaps are 3.4655–3.4889 seconds. The prediction uses
   the native cast deadline plus 3.4 seconds, then the actual explosion cast
   replaces that estimate. This is conservative relative to observed action
   delivery, **not proof of last-safe departure or server snapshot timing**.

   Exit portal positions are (-353.68884,243.5492) or
   (-346.21198,206.40881); explosion actors stop at
   (-352.98694,239.52087) or (-347.03595,210.34558). Forecasts use 4.1 yalms
   inward along portal facing, agreeing with all four observed instances, then
   use the actual projectile position when its cast starts. Circle radius20
   follows action7329. Donut outer75 follows action7330; the inherited inner6
   estimate remains **unverified**. Omen `gl_sircle_3004af` exists but its asset
   has no embedded draw mesh, and its filename is not a physical radius measurement.
   Mixed simultaneous circle/donut exits remain an unobserved variant.

2. **Scythe marks never handled.** Icon23 sets at seq690920/690929/690931 resolve
   to targets9,19,2 at seq694528/695708/696827. Icon order is 2,9,19: removing
   the first bait on every hit is incorrect. Match the actual hit target.
   The two recorded sets start resolving 5.1556 and 5.2737 seconds after icons;
   all marked targets receive an early conservative 5-second deadline since hit
   order is not established beforehand. Persistent scythe NPC OID0x194E is a
   separate radius4 obstacle (Action7345), retained until death/destruction.
   Prediction→spawn gaps, exact physical radius, and fresh-source behavior require
   additional live verification.

3. **Jester's Jig misclassified as radius9 dodge.** Visual7332 starts seq815280,
   completes seq821045, then actual damage7333 occurs seq822860 to all24 targets.
   Use a raidwide prediction from the earliest visual with 1.9-second helper
   offset after the native cast deadline. Remove the false central avoidance zone.
   Flameflow similarly uses visual7335 (seq762829/911499) and persists through
   helper7336/7337 (seq767164/767171 and914557/914560), approximately0.81 seconds
   after the visual action. These helper IDs are instant actions, so the old
   `RaidwideCast` components could never activate on them. The warning clears on
   helper resolution or early cancellation. One prediction represents each
   compound raidwide; helper damage magnitude is not inferred.

4. **Missing add-phase cast activation.** Activate Icefall7344 (sheet circle5),
   Lucky Pierrot7343 (full width6, half width3), and prioritize Ferdiad's Fools.
   Transported Lucky Pierrot7342 now uses the portal's explicit outgoing cast
   segment through actual add resolution; see continuation evidence below.

5. **No Debilitator instructions.** Fire resistance-down1137 prompts blue/water
   puddles; water resistance-down1157 prompts red/fire puddles. Puddle OID2007621
   spawns seq744382–744393. Actor216 animation changes at seq748983 `[64,128]`,
   seq762529 `[1024,2048]`, seq764556 `[256,512]`, and seq768359 `[4,8]` are
   available. The later exact C059/VFX selector proof establishes both colour
   branches, so current guidance includes opposite-element text, per-object
   colour labels and wrong/unknown counts. Automated toggling, circle dimensions
   and assignment constraints remain unverified; see the continuation below.

6. **Blackfire's final footprint was underpredicted.** The old radius7 circle is
   the base Action7339 range and ignores Area of Influence Up618. The preferred
   guide describes growing circles. Eight helpers start7339 atseq868367–868375,
   the same frame as boss visual7338, approximately8.95seconds before damage.
   Helpers gain618 stacks1–6 at cast+1.925,2.934,3.950,4.987,6.003,7.025seconds.
   All eight actions resolve atseq878241–878256, immediately followed by their
   cast-end events; no intermediate growing damage ticks occur in this recording.
   Actor6 receives two simultaneous hits from centres23.085y apart, impossible
   for two radius7 circles. Its last retained position was stationary1.594seconds
   before damage,12.615y from one hitting centre and10.472y from the other; a
   third centre13.548y away does not hit it. These observations strongly support
   a final radius near13; they do not establish an exact server boundary or snapshot.

   Current installed Status618 explicitly says the area of effect is expanded,
   allows16stacks, and supplies no radius formula. The same pinned fork's
   `D133MistDragon.ColdFog` uses radius4+618stack, corroborating a possible
   +1y/stack interpretation without proving this fight's formula. Other statuses
   sharing the English name use different IDs and cannot establish this mapping.
   The correction forecasts a **conservative estimated radius13.5** from the
   earliest7339 helper cast, with the native corrected future cast deadline.
   It does not start at7 and enlarge only after later statuses, which would
   postpone route planning. This remains estimated geometry, not physical proof.

   A0.1y-grid union check within the existing centre(-350,225)/radius30 arena
   leaves approximately505.53y² safe at radius13 and438.18y² at radius13.5
   (17.88% and15.50%). All22 retained living nonhit players remain outside the
   conservative union; the closest is13.7467y from a centre. Actor6's two hits
   are covered. These are feasibility/corroboration checks on the training source,
   not independent confirmation of exact geometry or the physical arena edge.
   Known618 values0–6 preserve the forecast. Any value above6 emits an explicit
   uncertainty warning and withdraws that helper's hard AI avoidance claim while
   retaining its informational estimate. Actual7339 effects, early cancellation,
   destruction and generation replacement retire only the matching helper.
   Missing effects expire two seconds after the predicted deadline as stale-data
   cleanup, not an assertion that a physical hazard lasts two seconds.

   Dedicated tests drive native actor creation, casts, status changes, damage,
   destruction and replacement, check all eight future deadlines and union
   feasibility, retain normal-completion warnings through actual effects, and
   reject stale-generation callbacks. Private reproducible receipts are
   `audit-receipt.json` and
   `blackfire-safe-space.json`, derived from the byte-identical complete original
   with native parser MVID828c299e-47d5-4a38-88d5-44db3611e480,1,890,796 entries.

### Remaining cast and counter audit

The one Blackbolt marker62 atseq899074 precedes cast7341/seq899221 by0.2136444s;
damage903997 arrives5.2446103s after the marker and5.0309659s after the cast.
The existing target-following stack6 warning therefore arrives approximately5s
before damage, but does not yet use this slightly earlier icon. Eight observed
recipients corroborate the inherited minimum8 without proving a required count
or maximum; no exclusive eight-person assignment is inferred. Four Reap7745
casts and one Reward7331 cast resolve within0.038s of the native corrected cast
deadline, then finish in the same frame. Their existing cone forecasts do not
expire before recorded damage. No cancellation or retarget branch was recorded.

Both Debilitator waves apply only WaterDown1157 (24players then22players); no
FireDown trial exists in this source. Live colour/unknown counts follow actual
per-player weakness, object generation and exact EAnim observations; this audit
found no recorded count mismatch. Automated toggle eligibility, ownership,
cooldown and crossing radius remain unverified. These findings do not extend
coverage to unrecorded alternate branches or certify live movement.

Arena center(-350,225), circle30, remaining original attacks and WIP status are
preserved. Lethal-edge accuracy and all alternate/reset cases are not established
by this one training recording.

## Proto Ultima

- **Impossible Flare Star exclusions:** the old radius31 central hazard covers the
  entire circle30 arena. Recorded central7588 starts seq995965 and hits all24
  players atseq1004030. The paired sources at(-350,-70) and(-350,-30) start
  seq1004856/1004859 and hit atseq1013135/1013141. The Action sheet uses proximity
  omen `gl_shipimpact0f`. Keep the raidwide prediction and source markers, but
  remove the invented lethal exclusion. An initially added distance preference
  was subsequently removed because both waves overlap the unverified outer Pool
  and native navigation selected outer positions. Keep early damage deadlines,
  source markers and proximity/Pool instructions. No safe damage threshold,
  automated combined positioning or continuous route is established.
- Activate direct Light Pillar7589 circle3 and Proto Bit Diffractive Laser7583
  cone20.6/60degrees (sheet range20 plus known0.6 actor hitbox). Neither attack
  occurs in this training recording; these are guide/sheet-supported omissions
  with physical/live acceptance pending. Chasing Light Pillar repeat timing and
  target identity remain unimplemented, not inferred from unrelated Nox mechanics.
- Activate Supernova7581 raidwide (seq1030353) and long Supernova7593 enrage warning
  (seq1048001), with collector/add target priority. Preserve the original arena
  and laser geometry.
- **Eikonizer orb guidance:** marker62 atseq989427 belongs to actor355,
  OID0x196C (ProtoUltimaHelper2), created seq987623 at(-350,-50). It is not an
  Aether Collector0x1969. The orb's unnamed7596 action (sheet radius8, zero
  recorded targets) and helper334's named Eikonizer7597 (sheet radius40,
  23 targets) resolve together atseq995176/995177,5.5446seconds after the marker.
  Provide gathering text, a bounded soft preference toward that exact marked
  orb, and a5.4-second raidwide client deadline. Its own7596 resolution or source
  destruction/replacement clears guidance. The temporal relationship corroborates
  guide context, but does not prove radius8 is stack eligibility: no hard safe
  circle was invented. Native movement under casting/overlaps and physical stack
  eligibility remain unverified. Synthetic tests reject Collector markers,
  repeated icons, unrelated resolutions and replaced orb identities.
- Replace the old inactive full-arena AetherialPool pull with an early cast
  instruction only. Action7587 has target3 in both recorded actions
  seq1002941/1011640; the second explicitly links Attract27 (maximum40), while
  the first is zero damage without attraction during Invincibility1570. This
  association is not proof that every Invincibility variant prevents displacement.
  Exact annular eligibility and combined Pool/proximity positioning remain
  unresolved. Do not invent a hard annulus or reward outward movement. Independent
  supporting-floor and pre-fix native navigation evidence is recorded in
  `dun-scaith-proto-pool.md`.
- Integrated Aetheromodulator7582 now has a bounded cast warning and per-source
  point label, with the current native remaining cast timer. Its donut topology
  is verified by the guide and exact installed Omen/AVFX link, but world radii
  remain unknown: no hard avoidance ring, safe-region goal or damage amount is
  inferred. There are no observed casts of this action in the training recording.
  Eikonizer physical stack radius/server deadline and boss instant frontal
  attacks also remain pending. Detailed continuation evidence follows below.

### Proto Bit donut: bounded warning and exact asset evidence

The preferred normal-raid guide's Proto Ultima section describes each immobile
Proto Bit using a narrow donut, Integrated Aetheromodulator, before its cone.
Current installed Action7582 has CastType5, EffectRange15, Cast100ms25 and Omen78;
that Omen's exact path is `gl_sircle_1512af`. The installed asset
`vfx/omen/eff/gl_sircle_1512af.avfx` is14,252bytes with SHA-256
`ede00ad5d7f3fd66f966cb01e0fa2c7d3ab2b741e7b6520b979eed100bcd7716`.
The bounded decoder inventories zero embedded models and three procedural
DecalRing particles. This corroborates ring topology independently of the path
name, but does not compose world-space inner radius, caster-hitbox expansion or
server eligibility. The primary format reference is the pinned VFXEditor
[AvfxParticleDataDecalRing definition](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Particle/Data/AvfxParticleDataDecalRing.cs): its Width, ScalingScale and RingFan fields
are visual parameters, not a declaration of damage boundaries.

The current archive inventory was checked again and retains only game version
2026.09.15.0000.0000 for Action, mesh, parameter and graph evidence. This extraction
instead uses the installed recording-matching game2026.10.06.0000.0000. Private
facts and provenance are in
`research-receipt.json`, with the
complete bounded mesh/parameter/graph inventories alongside it. Different-fight
Action8283 in Ala Mhigo is deliberately not used to establish7582's world radii.

The original creates five bits atseq1039048–1039075 and removes them atseq1041206–
1041210, approximately2.79seconds later, without any7582 or7583 cast/effect. No
physical radius, cast-to-hit offset or server snapshot can be measured here.
The component therefore starts only on an actual7582 cast from the current
Proto Bit actor generation. It displays the earliest current native remaining
cast countdown and a separate timed point label at each source. Normal cast
completion/cancellation, actual action, despawn and generation replacement clear
only the matching cast. Duplicate observations cannot duplicate sources; obsolete
cast objects cannot clear a newer cast on the same actor. Bootstrap reconstructs
actual ongoing casts; reset discards history. Missing native cleanup has a bounded
fallback two seconds after the corrected cast deadline, which is stale-data
cleanup rather than a physical hazard-duration assertion.

Dedicated native-operation regression cases cover simultaneous sources, actual
remaining timers, duplicate observations, effects, cancellation, despawn, stale
callbacks, actor generation replacement, bootstrap and reset. They explicitly
assert that this advisory warning creates no forbidden region, movement goal or
predicted damage amount. Build/test success still cannot establish live warning
lead time or automation response for this unrecorded attack.

Chasing Light Pillars remain separate. The recording's six instant7592 effects
atseq934826–940930 occur before Proto Ultima's creation945322, on the elevated
approach ramp (Z71.27→33.83, Y2.55→11.89), with no targets. They cannot establish
the boss's chasing cadence. Three icon197 markers appear on players4/17/11 after
bit spawn, but no7578/7589/7592 action follows in the Proto fight before its phase
ends. Icon ownership and follow-up timing are therefore not inferred from the
shared marker or these environmental ramp effects.

### Eikonizer marker and missing-resolution lifecycle

The recorded marker-to-hit interval remains 5.5446seconds, with the existing
conservative 5.4-second client deadline. A repeated valid marker from the same
live orb generation now preserves that earliest deadline. Dead or stale actor
generations cannot create a new assignment. Actual own7596 resolution, death,
destruction, replacement and reset clear the matching guidance as before.

If an otherwise live source has no resolution, gathering, text and predicted
damage now expire at activation+2seconds. This bounds stale guidance rather than
asserting that an unresolved mechanic is physically safe. Repeated markers from
that same unresolved expired generation are ignored; a late actual7596 resolution
allows a subsequent marker to start a fresh wave. A fresh actor generation can
start independently. No persistent uncertainty banner is retained after cleanup.
Native-operation regressions check duplicate markers separated in time, the
original damage deadline, expiry without resolution, late duplicates, late actual
resolution, fresh waves/generations, dead/stale callbacks and reset. These are
lifecycle checks on the existing soft gathering preference, not stack-radius or
live gameplay acceptance.

## Validation

`tests/DunScaithFerdiadProtoRegression` supplies synthetic callback cases for
registration/activation, independent portal instances, repeated starts, completion
versus cancellation, actual explosion replacement/resolution, target-specific
scythe mark removal, persistent-source destruction, raidwide lifecycle, new-pull
cleanup, removal of unsafe single/paired proximity incentives, retained damage
predictions, early Pool instructions, three-ray forecasts and nonempty legal arena.
These exercise code contracts; they are not physical expected-answer measurements.

The parent task owns the full current-tree build and native original replay run.
Record those results in the raid-wide report. Live BMR/XCAI warning consumption,
travel feasibility, last-safe response, fresh holdouts and unresolved mechanics
above are required before whole-encounter acceptance.


## Continuation: transported charges and explicit puddle-asset investigation

### Transported Lucky Pierrot lifecycle repair

The preferred guide's add-phase description was re-read before this continuation.
Two retained waves contain explicit outgoing path cues; no portal-colour relation
is required to reconstruct the path:

- Initial six exit casts7326 start seq771584–771596/ticks10123348560, resolve
  visual7326 atseq775823–775841/ticks10153298399. The six corresponding add
  charges7342 resolve seq776983–776993/ticks10161117852,0.7819453seconds later.
- Later three exit casts7766 start seq790754/790757/790769,
  ticks10335627942–10335855371; visual resolution seq794768–794773,
  ticks10400576613. Their add charges7342 resolve seq795330/795332/795333,
  ticks10408592066,0.8015453seconds later.
- In all nine cases, the actual charge's explicit endpoint agrees with its
  outgoing cast endpoint within0.001y. Initial endpoints span the room's six
  radial routes; the second wave repeats three routes. The helper casts have
  zero action targets, while the following add charges include hit targets.

The previous `ChargeAOEs` components cleared at helper visual completion,
leaving a concrete0.78–0.80second warning gap before the damaging add charge.
`TransportedLuckyPierrot` replaces these two components: line origin is the
casting portal's actual position; endpoint is the cast's actual location;
half width3 follows all three current Action rows7326/7766/7342 (cast type8,
full width6). Activation is the native cast deadline plus0.7seconds, slightly
before observed client hit delivery. This deadline is not a measured server
snapshot or last-safe movement time. Successful visual completion retains the
line; an early cancellation or destruction clears that source. The actual
7342 action retires a forecast only when its explicit endpoint identifies one
instance uniquely. Ambiguous/coincident endpoints retain warnings instead of
removing an arbitrary line. A3-second post-visual fallback expires unresolved
forecasts after a conservative grace interval (both observed hits arrived
within0.81seconds), preventing a missing/ambiguous hit from leaking an arena
constraint for the rest of the fight. This is an event/identity contract, not an inferred
colour pairing. Nominal line width and continuous travel safety still require
independent physical and live acceptance.

Dedicated synthetic cases cover both cue IDs, repeated starts, explicit line
probes, independent simultaneous exits, normal visual completion versus early
cancellation, source destruction, exact-endpoint retirement and ambiguous-endpoint
retention. Current-sheet applicability assertions include all three action rows
and the incoming7343 charge. The first six and subsequent three waves are training
instances from the same original, not independent holdouts.

### Debilitator: exact evidence and remaining decoder boundary

All24 event objects use OID2007621. Both recorded Debilitator trials give the
recorder Water Resistance Down II1157; no Fire Resistance Down trial is present.
Each wave initially has six `[16,32]` and six `[64,128]` animation observations.
Subsequent flips use `[256,512]` and `[1024,2048]`. The first resolution has eleven
`[4,8]` and one `[1,2]`; the second has twelve `[4,8]`. For example, actor218's
last flip seq767474 precedes its unique `[1,2]` resolution seq768361. These
observations identify repeated states, not their visual colours.

Native damage fields independently identify helper7337 as Water (`Param1=0x65`)
and7336 as Fire (`Param1=0x15`). Water damage is stronger in the first trial than
the second, which correlates with the one differing terminal puddle state. This
correlation is **not used to assign colours or automate toggling**: it cannot
independently establish the scene animation's visual identity or eligibility.

Read-only asset investigation resolved a previously missing explicit source link:

- Current-game typed `EObj[2007621].SgbPath` references `ExportedSG[13673]`, whose
  path is `bg/ex1/05_zon_z2/shared/for_vfx/sgvf_z2r3_b1048.sgb`.
- The planmap EventObject has BoundInstanceId0, so following only bound-instance
  links would miss this asset. The typed Excel reference resolves it directly.
- SGB SHA-256 `66abedd60f3d7e64fd3be2f518a1801f5bbeff2cc4e12dafbe3371912c937f12`,3875bytes.
  It contains ten named timeline branches (`rvs_off1`, `rvs_off2`, `rvs_on1`,
  `rvs_on2`, their `_anim` variants and `rvs_1t2_anim`/`rvs_2t1_anim`).
- Its VFX instance points to `bg/ex1/05_zon_z2/common/vfx/eff/b1048_revs_y.avfx`,
  SHA-256 `116d0aad9950f57ca0bfc1d3c4a4049781a8da34a1fe95d4b1c07c2f52a96b31`,144528bytes.
  The scene instance supplies white RGBA255 multipliers; elemental colours must
  come from the VFX branches/textures rather than this outer multiplier.
- The available Lumina scene reader decodes layers and asset references, but
  does not decode the scene timeline's C019/C056–C059 node semantics into AVFX
  scheduler/colour branches. The pinned primary FFXIVClientStructs FileFormat
  schema documents timeline offsets and opaque Cxxx nodes; the reviewed
  VFXEditor source has no handlers for these node IDs. Raw names and numeric
  similarity are insufficient to connect observed EAnim bits to red/blue.

The precise remaining blocker is that missing scene-timeline→VFX branch semantic
mapping, or independently synchronized visual evidence. The assets and raw
observations now exist in the private evidence workspace for that follow-up.
No additional replay with the same unknown colour labels would by itself fix
this decoder boundary. Gameplay guidance now also begins at Debilitator cast
start with a preparation warning, before the elemental status arrives, then
uses the verified status-specific text. Tests cover earliest cast warning,
completion/cancellation cleanup, both weakness responses and absence of invented
hard puddle constraints.


### Decoder continuation: scene branch identity is now resolved

A private read-only SGB decoder, grounded in pinned FFXIVClientStructs scene
headers plus VFXEditor TMAC/TMTR readers, now resolves the actor-instance bindings
and named branches. Installed-game read-only disassembly by the Deathgaze task
confirms the scene animation-handler table begins at handlerBlob+5: each event
animation bit indexes one of16 timeline IDs. This resolves an earlier off-by-one
ambiguity. For the exact puddle SGB above:

| Observed EAnim pair | Handler indices | Scene branches | Bound VFX instance1 C059 payload scalar at+0x0C |
| --- | --- | --- | --- |
| `[16,32]` |4,5 | `rvs_on1`, `rvs_on1_anim` |3 |
| `[64,128]` |6,7 | `rvs_on2`, `rvs_on2_anim` |2 |
| `[256,512]` |8,9 | `rvs_on2`, `rvs_1t2_anim` |2 |
| `[1024,2048]` |10,11 | `rvs_on1`, `rvs_2t1_anim` |3 |

Every displayed branch binds its C059 entry to scene VFX instance1, which is the
exact `b1048_revs_y.avfx` asset. The distinction between visual family1/family2
is now supported without damage correlation. **The colours are still not mapped.**
The remaining precise decoder boundary is the semantics of C059's scalar2/3 and
its connection to the AVFX scheduler/colour/texture branch. Treating that integer
as a scheduler index without a verified call/format contract would still be a
guess. The reviewed primary VFXEditor tree atrevision
`05f6eb0cce19a6a91ae275bce609299d573914b3` has no C059 handler. Namedscene branches
such as `on1`/`on2` do not themselves mean fire/water. No new automated colour
selection was enabled by this partial decode.

### Decoder continuation: exact installed selector and colour mapping

The previous C059 boundary is now resolved by bounded, read-only inspection of
the installed `2026.10.06.0000.0000` executable and the exact AVFX above. This
supersedes the preceding statement that the colours are unmapped. No module or
gameplay mutation was needed for this investigation.

The newly discovered archive graph and parameter tools are healthy, but their
indexed game dataset remains `2026.09.15.0000.0000`. Their public schemas accept
Action/Omen-linked queries rather than arbitrary scene asset paths. The puddle
therefore uses the exact installed asset, parsed through private copies of the
available bounded graph/parameter readers; an older indexed dataset was not
silently substituted for current-game evidence.

The executable SHA-256 is
`b38840761e2677128b6bec1ceaee3f2870cd84e1d38ab7958216373058383780`.
Its C059 registry entry identifies command type47 (`0x2F`). That type's scene
command handler at `0x14192D80B` reads payload+`0x0C` and passes it to the bound
VFX layout instance at `0x140742C00`. The call chain then follows the layout's
GraphicsObject+`0x30`, the graphics VfxResourceInstance+`0x2A0`, and resource
function `0x14038F790`. The resource records the selector and queues its bit in
the effect manager. The consumer at `0x1403B54AF` subtracts one before invoking
the zero-based trigger table at `0x1403BC590`. Selector0 is skipped. This proves
that the C059 scalar is a **one-based AVFX trigger selector**, rather than a
scene timeline index or an assumed direct emitter index.

The final serialized scheduler trigger table maps trigger1 to AVFX timeline0
and trigger2 to timeline2. Their persistent items select emitter0 and emitter1,
respectively. Final emitter `ItPr` records use `TgtB` as the particle index.
Emitter0 reaches particles8/0/2/4/5/9, with red/orange keys including particle9
RGB `(255,77,37)` and particle0 `(192,105,24)`. Emitter1 reaches
particles3/1/6/7/10/11/12, with blue keys including particle3 `(115,177,224)`,
particle6 `(115,160,255)` and particle7 `(22,96,166)`. Both parent emitters have
white RGB multipliers. Neutral and green highlight particles are preserved in
the inventory; the classification describes each composite branch's dominant
colour, not every constituent particle.

| Observed EAnim pair | C059 selector | Trigger → AVFX timeline → emitter | Visual branch |
| --- | --- | --- | --- |
| `[16,32]`, `[1024,2048]` |3 |2 →2 →1 |Blue/water |
| `[64,128]`, `[256,512]` |2 |1 →0 →0 |Red/orange/fire |

Primary format/control sources are pinned VFXEditor revision
`05f6eb0cce19a6a91ae275bce609299d573914b3`, specifically
[AvfxEmitterItem](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Emitter/AvfxEmitterItem.cs),
[AvfxCurveColor](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Curve/AvfxCurveColor.cs),
[AvfxCurveKey](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Curve/AvfxCurveKey.cs),
[scheduler reader](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Scheduler/AvfxScheduler.cs),
and its [trigger signature](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Interop/Constants.cs).
The signature has one match in the installed executable, at `0x1403B54B3`.
The pointer fields cross-check with pinned FFXIVClientStructs revision
`994319b521c85e445e8907474b3a905cc05187c3`
[VfxLayoutInstance](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/LayoutEngine/Layer/VfxLayoutInstance.cs)
and [graphics VfxObject](https://github.com/aers/FFXIVClientStructs/blob/994319b521c85e445e8907474b3a905cc05187c3/FFXIVClientStructs/FFXIV/Client/Graphics/Scene/VfxObject.cs).

The private receipt `ferdiad-assets/puddle-selector-color-proof.json`, graph
`puddle-avfx-graph.json`, full colour-key inventory `puddle-color-curves.json`
and eight bounded disassembly snippets preserve the exact chain. The mapping
does not rely on player damage correlations. It does not establish a rendered
footprint, a physical entry/toggle radius, player eligibility, repeat-trigger
cooldown, server snapshot, or last-safe crossing. Those remain distinct evidence
requirements before hard movement or automated puddle toggling is enabled.

### Debilitator colour-state guidance

The module now tracks each live raw `EObj2007621` actor generation independently.
It consumes the exact native animation pairs above immediately, without waiting
for a frame update, cast finish, terminal cue or damage. The native callback
packing is `(param1 <<16) | param2`; regression inputs use `OpCreate` with decimal
OID2007621 and `OpEventObjectAnimation` with the original ushort pairs to verify
that transport boundary, rather than invoking only the packed component API.

For the displayed player's actual weakness, the global hint reports the number
of wrong-colour and unknown puddles. Position-anchored text points identify
`RED/BLUE: change`, `RED/BLUE: keep`, or `?: unknown`. They draw no circle and
claim no physical trigger radius. Both missing weakness and ambiguous simultaneous
weaknesses suppress colour assignments. The existing preparation warning still
starts at Debilitator cast start; the opposite-element personal instruction
remains status-driven.

Unknown pairs invalidate the previous known colour and retain that live object
in the unknown count. Exact terminal pairs `[1,2]` and `[4,8]` remove their own
object. Destruction and actor-ID replacement use actor-reference generation
identity; a stale generation cannot clear or recolour its replacement. Component
reset discards history, and existing objects bootstrap as unknown because the
world actor snapshot does not retain transient EAnim parameters.

Either actual Flameflow helper7336/7337 clears the counts, point markers and
personal instructions until the next Debilitator cast. This resolution guard
is required by the original: actor218 flips to blue at seq767474 **after** the
first Flameflow actions seq767164/767171. Thus its blue terminal animation at
seq768361 does not prove that it was blue at the earlier damage/snapshot. The
earlier damage/terminal correlation is not a colour or snapshot proof. Late
animations cannot reopen resolved guidance.

Native replay expectations for the recorder's Water Resistance Down II1157:
seq748995 and seq898892 have six wrong, zero unknown; seq766687 and seq914328
have zero wrong, zero unknown; seq767164 and seq914557 clear the colour-state
guidance. These samples check event consumption and state lifecycle, not an
independent physical or live acceptance test.

Dedicated regression cases additionally cover both weakness responses, both
initial colour families, both flips, duplicate cues, mismatched/unknown pairs,
both terminal families, wrong OID, generation replacement, stale-generation
callbacks, native destruction, reset/bootstrap, repeated waves, both actual
Flameflow helper actions and post-resolution late cues. AI forbidden/goal zones
remain empty for this component: ownership, toggle radius, player eligibility,
repeat-trigger cooldown and safe crossing/snapshot timing remain unverified.


## Continuation: Proto Ultima three-ray forecast

Re-read the preferred guide's normal Proto Ultima section before changing this sequence. It explicitly describes three rays, with the second and third angled45 degrees either side of the first. The exact original records firstcast7574 at raw970470/ticks12175456466, firsthit973194/ticks12208682208; actual7576 secondcast973914/ticks12219818304, hit976256/ticks12249576420; actual7575 thirdcast977175/ticks12260634385, hit979163/ticks12290508078. Cast rotations are-0.5021467209,+0.2832634449,-1.2875570059 radians: the side differences are+/-0.7854102, within0.001 degrees of45. The hit intervals are4.0894212 and4.0931658 seconds. Action-event facing is the original firstfacing on allthree, so it must not replace the actual side cast rotations.

`AetherochemicalLasers` replaces the three isolated cast components. Firstcast supplies the actual first ray and both45-degree side forecasts. Since the guide does not establish side order, both initially use the earliest side deadline (corrected firstcastfinish+4.0s). The actual nextcast supplies its own rotation/deadline and postpones the remaining unstarted side to corrected nextcastfinish+4.0s. Thus uncertainty does not invent a fixed left/right order, while identified later hazards keep their future activation rather than forcing immediate movement. Origins follow the actual source position; inherited71y/fullwidth8 geometry is retained and remains metadata-derived, not an independently measured damage boundary.

Actual actions retire only their ray. Successful cast-end callbacks retain the remaining predictions; cancellation, next unrelated primarycast, untargetability, destruction, fresh module and bounded stale expiry withdraw unconfirmed guidance. A midsequence load displays only the actual observed side cast, without inventing an unseen first direction. Synthetic tests exercise both side orders (only+then- was recorded), geometry/deadlines, actual-cast replacement, per-ray retirement, duplicate cue, source movement, cancellation, phase exit/destruction and stale cleanup. All original samples remain training evidence; there is no independent holdout, exact server snapshot or live warning/navigation acceptance yet.


## Ferdiad acceptance reassessment after7.5.6.35.4

Reviewed release source `bd9bf9228e404bbbdf966d5b9bb28b04afc04a89` and the current
metadata-only Ferdiad candidate under the module maturity policy. The preferred
normal [Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith) was
refreshed. Scythe drops/persistence, circle/donut and known mixed Wormhole exits,
opposite-element puddles, transported charges, gaze, Icefall/Reward, adds, Jig,
tankbuster/Reap/stack and growing Blackfire are covered or explicitly accounted
for. Mixed exits retain concurrent source-specific circle/donut forecasts;
missing recorded mixed footage is not the same as absent branch handling.

Debilitator supplies actual-weakness instructions, keep/change labels and
wrong/unknown counts. Automatic ownership/toggling is optional and is not
advertised as solved. The inherited6y donut inner region is a disclosed
conservative nominal model with guide close-to-exit response and available
stationary nonhit8.9621/hit13.565–13.615 evidence; no contrary safe-response
observation was established. Exact clipping remains unmeasured.

Blackfire13.5y is a documented conservative nominal final constraint, not a
newly calibrated boundary. The growing-mechanic guide, expansion status618,
recorded hit12.615/nonhit13.548 and22 nonhits outside the candidate footprint
support available coverage; the modeled remaining safe union is438.18square
units. Recognized expansion0–6 receives the final forecast early; unrecognized
larger stacks withdraw the hard claim. No known underprediction or infeasible
room was found. These observations do not authenticate every exact boundary.

Set Ferdiad to `Verified` for known response coverage, subject to the final
current-tree build/regression/native cohort being recorded below. A material
failure requires reverting/reopening. Personal play, exact server snapshots,
a complete automatic solver or pristine held-out footage are not independent
gates. This is neither `AISupport` nor live gameplay certification. Proto Ultima
is a separate module and retains WIP for its concrete incomplete responses.

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

Ferdiad/Proto174 synthetic assertions; Ferdiad45 current native checkpoints
retain its previously accepted prediction surfaces.

Ferdiad is accepted as `Verified` under the review above. Proto Ultima retains
WIP for its concrete pool/chasing response gaps.
