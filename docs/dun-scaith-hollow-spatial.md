# Diabolos Hollow: spatial evidence continuation

## Reassessment after7.5.6.35.4

Reviewed released source`bd9bf9228` under the module maturity policy. Exact snapshots, personal live approval and a complete automated solver are not automatic requirements for `Verified`; the existing marker/cast instructions explicitly account for manual responses where spatial ownership is unsupported. The **separate portal arena** remains a material boundary gap: its staged lethal/supporting regions are not established, and `PortalSafety` intentionally pauses automatic movement there rather than asserting a playable arena. The fallback preserves a human's manual response, but cannot certify the missing arena contract. Hollow therefore remains WIP for that concrete geometry/phase concern, not merely lack of `AISupport` or maintainer play.

A further supported recovery repair makes already present meteor towers immediately actionable when the component loads mid-window. Their actor state has no original creation timestamp, so the previous bootstrap restart of9.7s could permit arriving after the actual impact. The component now snapshots preexisting source references, uses immediate AI guidance and an explicit unavailable-original-timing instruction, while new native creation retains the observed9.7s future deadline. Actual impacts/destruction clear unknown-age guidance; its stale bound covers at most the full observed lifetime plus2s and is not presented as a damage time. Tank-only center eligibility and alliance masks remain active during bootstrap. Replacement by a non-tower removes the old semantics immediately. New synthetic native callback cases cover these distinctions; the isolated fresh-source Diabolos suite passed187 assertions. Full generated-plugin/replay validation is pending the coordinator's candidate cohort.

Research uses the normal level60 alliance-raid
[Console Games Wiki guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith),
Phase3 Diabolos Hollow. It describes Blindside as a tank stack marker and
Earthshaker as three boss-origin cones whose marked players separate. The guide
does not establish either cone's angle, physical snapshot or sharing count.

The complete original recording is
`901ecf79648702210c1227cf26422f5ddc1399f5a8c28b65bf3ff6c84f57dc96`, digest
`55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`.
A byte-identical private copy was independently decoded without sharing the
original's exclusive file handle:1890796entries, complete schema1, game
`2026.10.06.0000.0000`, parser MVID
`828c299e-47d5-4a38-88d5-44db3611e480`. Nineteen relevant marker/hit snapshots
retain latest world XYZ, actor generation/death, packet facing and last movement
age. Bounded nearby movement windows are also retained. Packet TargetPos values
near zero were not mistaken for caster world positions.

The archive service is healthy; indexed Action/Omen mesh/parameter/graph datasets
still cover only `2026.09.15.0000.0000`. Exact installed typed rows and resources
are used instead. Both damaging actions have Omen/OmenAlt0; this is an absent
link, not proof of absent VFX or service failure.

## Earthshaker

Current Action7210 has cast type3, range60 and width modifier0. Its AnimationEnd
key directly names `mon_sp/gimmick/a2fd_boss_gimmick11`. The installed TMB's C012
entry points to `vfx/monster/m0117/eff/baha_earth_30c0s.avfx`, with scale `(1,1,1)`,
zero rotation/position and caster/target character bindings. This is a link from
the exact Dun Scaith action, rather than another encounter's similarly named
ability. The TMB SHA256 is
`5ff0362176e3894d3b5f03af1d938974db528b1a1efd7f32c7d3648e995dbb8d`;
AVFX SHA256 is
`f0aac37c8c797233bdb55173e8bbe02dd00d2150c776d91878200afce40f309b`.

The normal scheduler reaches timeline0 and emitter2. Emitter2 is Model type5,
references model4 and uses generate method3 (`OrderOnVertex`). Model4's ten
28-byte emission records have explicit XYZ/normal/RGBA; noncentral XZ vectors
span approximately `-15.000006..+14.999999`degrees. This establishes a local
30degree emission pattern without inferring angle from a filename. It is not a
filled damage cone: emitter2 has anisotropic X/Z scale45/56 and local Z offset8,
and visible particles have their own dimensions. Those transforms prevent using
the raw local15degree value as a composed world visual boundary.

The six original helper hits independently corroborate the existing nominal
15degree half-angle. The largest observed hit-centre angle is13.902degrees
(actor11, seq1882670). A living same-floor actor20 is absent at15.346degrees
(seq1860981; last movement1.781s earlier); actor3 is absent at16.068degrees
(seq1882672; last movement0.035s earlier). Dead actors and other floors are
excluded from these comparisons. These observations support retaining15degrees;
they do not establish exact server clipping, target-hitbox expansion or last-safe
movement. The guide's cone description agrees with the typed Action and this
same-action asset evidence; a supplemental guide's column description remains
contradicted.

Icons40 precede the boss7209 cast and subsequent7210 helper hits. The existing
4.7s warning remains conservative relative to the observed roughly4.85–4.86s
icon-to-hit delivery. Neither delivery nor the asset's entry time7 is a verified
server snapshot. Helper world origins are approximately `(-350,-445)`; current
boss position differs by about0.04y, rather than the near-zero action TargetPos.

## Blindside

Current Action7208 is cast type3 with EffectRange9 and no Omen link. AnimationEnd
3205 is `mon_sp/[SKL_ID]/mon_sp005`; BNpcBase6408 references ModelChara346,
monster model124. The matching installed candidate
`chara/action/mon_sp/m0124/mon_sp005.tmb` contains a C012 entry for
`vfx/monster/m0124/eff/m0124sp05c0c.avfx`. Exact runtime `[SKL_ID]` substitution
has not been decoded here: the reviewed primary VFXEditor ActionRow explicitly
rejects unresolved templates. The candidate's local emission model spans about
±36degrees and its draw mesh about±54degrees. These different decorative extents
are not interchangeable with a damage cone and do not justify selecting30,45,
54 or60degrees for gameplay.

The original's first Blindside hits actor19 at a current client angle37.033degrees
while actor23 is absent at44.210degrees. Both are moving; actor23's resurrection
immunity418 had already been lost at seq1850788. Nearby movement shows substantial
angular change before delivery, so these are client observations rather than
exact server-boundary probes. All recorded recipients lie within about5.06y,
which does not calibrate the Action row's outer radius or any boss-radius addition.
Hard spatial assignment therefore remains unsupported.

Sharing is not tank-exclusive: the first hit includes WAR21, SGE40, RDM35 and
VPR41; later hits include all broad combat roles. Recipient counts are4,11 and10.
They do not prove a required minimum or maximum. The three resolved icon93 leads
are5.1764912,5.1191072 and5.1778207s; the existing5.0s forecast remains earlier.

The third marked GNB actor9 receives icon93 at seq1869385, dies at seq1871821,
and the actual7208 still hits ten players at seq1873918,2.425s after that death.
Target unavailability therefore cannot be treated as cancellation. The module
now retains a bounded incoming global warning, explicitly says to watch boss
facing when aim is uncertain, and suppresses target-specific healing for a dead,
destroyed or replaced actor. Actual7208 resolves the warning; missing resolution
expires exactly2s after the forecast deadline. Fresh markers/new actor generations
get fresh deadlines; duplicate markers do not postpone them. Regression cases
drive native create/icon/death/destruction/action events and cover these lifecycles
plus expiry, repetition and module reset. No new cone dimensions were introduced.

## Provenance and remaining boundary

Private `a36-spatial/research-receipt.json` binds asset hashes, typed rows, exact
native sequences and parser identity. Its sibling files retain TMB links, complete
bounded AVFX mesh/graph/parameter inventories, emission vertices, hit-angle facts
and nearby motion. Raw proprietary assets and original recordings stay private.

Primary format semantics use VFXEditor revision
`05f6eb0cce19a6a91ae275bce609299d573914b3`:
[C012 bindings/transforms](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/TmbFormat/Entries/C012.cs),
[action timeline paths/template limitation](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Select/Tabs/Actions/ActionRow.cs),
[model emission records](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Model/Emit/AvfxEmitVertexes.cs),
[model emitter fields](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Emitter/Data/AvfxEmitterDataModel.cs),
and [raw enum semantics](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/AvfxFormat/Base/Enums.cs).

Remaining Blindside blockers are exact runtime template/attachment composition
and, independently, a calibrated server damage boundary/snapshot. Resolving the
template alone would not convert decorative VFX into a safe sharing region.
Offline extraction and regression success remain separate from live acceptance.

## Critical Gravity displacement and portal evidence refresh

All four original Critical Gravity7767 actions (raw1795979,1799372,1801279 and
1883596) include effect word `0x000100000000001F` for their player recipients.
Its type31 is Knockback, Value1 selects Knockback row1, and Param0 adds zero
distance. The exact installed `2026.10.06.0000.0000` typed row independently
specifies distance10, speed20, direction0 (`AwayFromSource`), Motion=true and
CancelMove=true. The displacement rows SHA256 is
`5bcfa9b2885d6d813c314c5cc0eb3de322fa3506e9f7f0c0400f3c699372b9a2`;
its manifest SHA256 is
`f6361dfc1e5d0b7aa5952dbf87fdfd10622ddcf864cc5997b228b3d46400364c`.
The manifest retains the sheet-page, version-file, extractor and Lumina hashes.

The existing orb warning omitted this knockback. It now warns from orb creation
to check the10y landing before manual interception, and its nearby instruction
mentions both the explosion and outward displacement. Interception remains
optional and unassigned. This agrees with the
[preferred guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Phase_3:_Diabolos_Hollow)
on keeping the blast away from the group;
[攻略:ark](https://ff14a.net/raids-dun-scaith) additionally describes letting
the spheres pass rather than intercepting them. The latter is descriptive
context, not an independently measured trajectory. Recorded orb aggro targets
change together and do not identify a required interceptor.

The nominal8+source-hitbox information circle and per-source lifecycle are
unchanged. Neither the effect word nor its sheet row establishes contact radius,
immunity, collision stopping, precise launch time, server snapshot or safe
landing route. Bounded archived movement pages for orb630/player18 around the
last hit are complete, but player18's preceding position is over7s old; those
samples do not calibrate the contact threshold or full physical displacement.
Literal and lifecycle regressions check early warning, per-source retention,
last-source retirement and absence of invented AI arrival/landing constraints.

A fresh duty220 archive search still finds only the full original and the
33second introduction recording. There are zero legacy map/yell/system cues
in the exact portal-entry-to-return interval1752012–1771505. Thus an alternate
retained cue cannot recover the absent portal EventObject2007638 stage here.
The remaining portal input contract is a capture from an interior participant
retaining that object's creation/state/EAnim transitions, synchronized with
enabled annulus state and an independent death-boundary observation, or an
equivalent reviewed current-client/server consumer establishing those semantics.
The recovered support disk and annulus assets alone do not supply that contract.
Existing manual portal fallback remains appropriate; no safe radius is inferred.

These are evidence gaps in safe response coverage, rather than a requirement
for the maintainer to personally play the encounter. Maturity follows the
[module policy](module-maturity-policy.md); adequate independently supported
offline evidence can close them. Private provenance and the bounded motion
pages are retained under `gravity-displacement-continuation` in the task evidence
directory. No shared plugin build was run by this investigation.

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

The shared Diabolos187 synthetic assertions cover the tower bootstrap
change;60 Hollow native checkpoints retain normal tower timing and portal hold.

Diabolos Hollow remains WIP for the documented portal combat-boundary/stage
and spatial response gaps. Existing towers no longer receive a false fresh
countdown when their creation age is unavailable.
