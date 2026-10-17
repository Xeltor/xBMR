# Dun Scaith: phase-one Diabolos continuation

The preferred [ConsoleGamesWiki Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith#Diabolos), reread2026-10-14, covers the normal level60 alliance encounter and identifies Ruinous Omen as raidwide damage. Its first Diabolos form is kept separate from Diabolos Hollow. The guide supplies descriptive response, not exact visual/helper timing or server snapshots.

## Ruinous Omen visual and actual damage

Source recording SHA256 `55e6a6363ba7cc262658903c46e164d786cb543e2d1a6a3f89cb6474e1dcf0cf`, exact game`2026.10.06.0000.0000`, original raw sequence numbers:

| Cue | Raw sequence | Time (seconds) | Observed role |
| --- | --- | --- | --- |
| Boss527 starts7184 |1597690|2082.7534210|Reported remaining14.6783835s; corrected finish2097.7318045s|
| Helper528 starts7185 |1597695|2082.7534210|Reported remaining15.6783835s; corrected finish2098.7318045s|
| Boss7184 action/cast end |1606550/1606551|2097.6995928|24 target entries, effect types61/27, no standard damage effects3/5/6|
| Helper7185 action/cast end |1607179/1607180|2098.7280714|24 target entries with standard damage effects|

The observed damage is1.0284786s after the visual action. Current October Action rows independently bind7184 to a15s cast/range50 and7185 to a16s cast/range30; both have no omen. Action7184 links hit timeline2750,7185 links3115. Exact version-file SHA256 is`3da1d28ba647edae481c8ea61eeb1fae9a3d1d700ac703165e4b3e73a66d57ac`, Action source-page SHA256`0b0560ebb7dcb2511ce6906bca167a6e820aa9251feb379a1541eb8ace814cc4`, trusted extractor SHA256`40646f32a3a1e0b793999a3d2d1a3fbb7059f2d70d44502204a1b2246fa87c90`. Effect61 is unnamed in the local decoder; no extra semantic claim is made about it.

The previous two `RaidwideCast` components predicted two damage instances at their different cast finishes. The scoped repair keeps the boss7184 cast instruction (`Raidwide: Ruinous Omen (delayed impact)`) but makes it a text-only `CastHint`. The existing helper7185 component owns the actual raidwide prediction at its own corrected cast finish. Its native cast begins simultaneously, so this loses no observed warning lead and requires no guessed follow-up timer. Boss visual completion/cancellation affects only its instruction; helper completion/cancellation affects only its damage forecast. A future recording without that helper cast remains an uncovered variant rather than a fabricated timer.

`PhaseOneOmenChecks` exercises native WorldState start/action/end callbacks for simultaneous boss/helper casts, the one-second deadline difference, all three alliance recipient slots, visual completion with the helper still pending, actual helper resolution, and separate cancellation of each source. Synthetic callbacks validate component contracts, not physical geometry, server snapshots or live mitigation acceptance. Only one completed original observation is available; it is training evidence, not an independent holdout.

## Same defect in Hollow Omen

The guide separately identifies Hollow Omen as the final form's raidwide. Original simultaneous starts are helper7203 sequence1828919 and boss7202 sequence1828933 at2422.6839363s. Corrected finishes are2437.6630415s for the boss visual and2442.6630425s for the helper, approximately five seconds apart. Boss7202 action1839155 at2437.6591548s has20 target entries with effect types61/27 and no standard damage; helper7203 action1842056 at2442.6771061s supplies standard damage (type3) and action metadata (type27). The recorded visual-to-damage gap is5.0179513s. Matching October Action7202 is cast15s/range50/hit timeline3242, and7203 is cast20s/range30/hit timeline3115; both have no omen. Provenance is the same exact-version extraction above.

The scoped main-file correction makes only boss7202 a `CastHint`, with `Raidwide: Hollow Omen (delayed impact)`. The active helper7203 keeps its actual20s raidwide prediction. The same native callback regressions run for both forms, with1s/5s differences and independent source cancellation. No helper delay is guessed or transferred across variants. Gate guidance, arena routing and other Hollow components are outside this change.

The private fresh-source Omen harness passed132 Diabolos assertions (20 new Omen assertions across both forms), against the frozen build13 for unchanged components. The gate test hook was then wired at the owner's request; the owner's full current-tree build14 is the combined generated-registration/gate acceptance check. No watched plugin build was run by this subtask.

## Read-only Ultimate Terror asset continuation

A new exact-version route bypassed the missing Omen link: Action7186 hit timeline2974 has key`mon_sp/m0115/mon_sp005_hit`, resolving to`chara/action/mon_sp/m0115/mon_sp005_hit.tmb` (443bytes, SHA256`61b1fdeeb343fbedabce0e564a9f9e4f8874c29356e343b757c1ad85d5d0a454`). Its C012 VFX entry was decoded against pinned [C012](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/TmbFormat/Entries/C012.cs) and [offset-reader definitions](https://github.com/0ceal0t/Dalamud-VFXEditor/blob/05f6eb0cce19a6a91ae275bce609299d573914b3/VFXEditor/Formats/TmbFormat/Utils/TmbReader.cs), with bounded offset/count checks. It points to`vfx/monster/m0115/eff/m0115sp_05t0s.avfx` (37460bytes, SHA256`57c9cce0a2d7a246e3eba38ab1424acfac32eaa78eb60e63e52d96f490df9fbf`). Stored TMB scale is(1,1,1), rotation/position(0,0,0); bind origins are caster/target, character type, bind IDs-1. This is a hit-effect chain, not a physical arena/damage-boundary link.

The existing archive parsers recover three embedded meshes (one filled disk and two local annular meshes) and six particles with time-varying transforms. Particle type payloads13/1/8 are unsupported by that parser; binder/recipient world composition and material/alpha boundaries are not certified. Local annular inner radii around0.298/0.360 with outer radius around1 cannot be copied into a5y safe region. Exact source/parser hashes and bounded facts are retained in private receipt `bounded-assets-receipt.json`.

The sole recorded7186 cast1573623 starts2043.4611045s. Low Blow7540 action1573753 at2043.6650452s applies status2 to boss527; cast-end1574193 and Stun status1574194 arrive2044.3359019s, with no7186 action. This corroborates the guide's phase-one stun response, and supplies a cancellation trial rather than a resolved inner-boundary trial. No new safe inner radius or Ultimate Terror source change follows from this inspection.


## Deathgate and Lifegate intermission

The preferred [Dun Scaith guide](https://ffxiv.consolegameswiki.com/wiki/Dun_Scaith) requires destroying Deathgates before they summon adds and continuing damage through the Lifegate health refills. In the original, pair538/539 appears at1579862/1579865, becomes targetable1579908/1579911 and starts Void Call7188 at1583869/1583870. Creation precedes cast start by5.973664s. Deaths1584536/1584624 cancel their casts at1584539/1584626; no successful summon is recorded. Pair556/557 starts1649976/1649977 and dies1653141/1650383, cancelling1653144/1650386. Lifegate549 creates1633822, becomes targetable1633849, receives HPBoost586 extras1–4 at1645018/1648223/1657013/1662541, becomes untargetable1668290 and destroys1668370.

GateIntermission reads the current actor generation and actual CastInfo, providing preparation/kill instructions and the earliest live summon countdown. Target hints retain priority2 for eligible Deathgates, priority1 for the Lifegate and at least priority3 for the earliest eligible summon, with50ms equality tolerance only for this preference. Stronger and negative priorities are preserved. This is a target preference, not a forced target or proof of live target choice. There is no fabricated summon damage/physical geometry or fixed wave schedule. Native checks cover both pairs, independent cancellations, overlapping refills and actual target fixtures; synthetic cases additionally cover replacement/bootstrap/reset and differing cast times.

## Evidence-based acceptance reassessment after7.5.6.35.4

Reviewed source`bd9bf9228` and the current scoped candidate under [the module maturity policy](module-maturity-policy.md). ConsoleGamesWiki and the normal [Pocket Guide](https://ffxiv.guide/hw/dun-scaith) were refreshed before this reassessment. ConsoleGamesWiki lists Shadethrust in its first Diabolos phase. The Pocket Guide's explicit first-form script instead lists Nightmare, Ultimate Terror, gates, Ruinous Omen and Night Terror, placing Shadethrust only in the Hollow form's script. The complete original boss527 inventory contains19 autoattacks7180, two instant Camisado7181 actions, Nightmare7182, Noctoshield7183, Ruinous Omen7184, one cancelled Ultimate Terror7186, Night Terror7187 and transition7190, with no Shadethrust. Exact current Action rows named Shadethrust are7194 (observed Hollow form) and7459 (Scathach). Auto7180 is short-range cone metadata9+R, not the40y line. No phase-one line ID is invented or copied from Hollow, and the conflicting guide claim is not treated as a proven material omission. A future positive first-form observation would reopen that question.

The two Camisado7181 hits are instant, unavoidable single-target damage with no recorded cast/icon/unique advance cue; they are accounted for as direct tank damage, not an omitted avoidable or cast-warning mechanic. Routine autoattacks are likewise not fabricated into timed mechanics. Current coverage includes the actual gaze cast, icon stack with late-hit recovery, supported nominal Ultimate Terror donut, separate visual/damaging Omen clocks, Noctoshield's native target eligibility, and Deathgate/Lifegate instructions, cast progress and priorities. Cast cancellation, source replacement, stack expiry and gate refills/reset have dedicated offline checks. The inherited nominal5/19.5 Terror dimensions have qualitative close/far guide support and no known counterexample; absence of an exact server clipping/snapshot calibration is documented uncertainty, not itself a demonstrated unsafe module defect. No cold-load timer, unseen phase-one attack, fixed summon schedule or player-tested approval gate is assumed.

Set the first-form module to`Verified` on this bounded coverage assessment, using the previously validated released source plus the current metadata-only candidate change. The final current-tree build and native/regression cohort below confirm this decision; a material failure requires reopening/reverting the label. The acceptance concerns this first form only. It makes no`AISupport` claim and does not certify personal live testing, exact snapshots or all automation outcomes. Hollow's separate portal-arena boundary and Scathach's later knockback concern do not transfer to this module.

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

Diabolos187 synthetic assertions and34 first-form native checkpoints,
unchanged from the accepted release sample objects.

First-form Diabolos is accepted as `Verified` under the complete known-response
review above. This decision does not extend to Hollow.
