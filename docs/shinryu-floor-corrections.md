# Shinryu Paradox: bounded floor corrections

Source repair on 2026-10-14 against parent
`c56311efc798507846648705dc517dd7fa7cb706`, preserving upstream
`7.5.6.35` / `be8ff771cc545f8f8b5b548b5dd7271d80460f89`.
This is a development change in the existing `FloorAOEs` component, not a new
release or whole-fight certification. Release metadata and module maturity are
unchanged. See also the earlier [encounter migration audit](xcai-encounter-migration.md).

## Mechanic contract and changes

Twilight uses the existing assignment contract: Waning Light (`5352`) needs the
upper floor, Waxing Dark (`5353`) the lower. The previous rendered colours/text
selected these floors, but both custom AI distance predicates selected the
opposite transition behaviour. Correct the predicates so an assigned actor
already on the required floor avoids its transition proxy; an actor on the
other floor selects that floor's existing transition proxy. Missing/conflicting
assignments supply neither floor guidance nor an invented assignment.

Twilight's boss visual (`49113`) does not retire the sequence. Keep guidance and
the state-machine completion condition pending until **both distinct** damaging
helper actions, Radiance (`49115`) and Shadow (`49116`), arrive. Either helper can
arrive first. Duplicate helper callbacks cannot count as the missing counterpart
or complete twice. A repeated callback with the same active cast object cannot
discard an already observed helper. A late visual finish cannot clear a newer
Breath/Tail mechanic.

Native status-loss callbacks remove assignment bits; refreshes remain
idempotent, and an unknown party actor cannot write a negative slot into the
bit mask. A subsequent recognized floor mechanic replaces an incomplete
Twilight. Phase exit/wipe/component reactivation discards its state through the
existing native component lifecycle. There is no inferred cancellation timer:
if a helper is missing, Twilight is not falsely marked complete. Association of
delayed helpers from an earlier interrupted sequence with a later Twilight is
not established by these sources and remains outside the validated cases.

Cosmic Breath (`49107`) retains its lower-floor rule; Cosmic Tail (`49110`) and
Atomic Tail (`49130`) retain their upper-floor rule and cast-finish retirement.
An early colour assignment does not override the preceding swipe. Other fight
components, manual control and XCAI's movement authority are unchanged.

## Evidence

The indexed sources examined contain six distinct original digests, twelve
selected Twilight instances. Duplicate imports/projections are not extra trials.
All six were already inspected during earlier investigations and are declared
training/regression evidence, **not fresh holdouts**. The bounded
`mechanic_patterns` queries exhausted all selected anchors and contexts without
event truncation, clipped sequence bounds or retained-range exclusions.

| Recording ID | Original SHA-256 | Retained revision |
| --- | --- | --- |
| `bc9518b574ccac34b717bb90bf8a5f162c7674170bb55a8d62227bafbd537833` | `c97cef48d62d3d4da612911409967e9cdf7dbd475b062d6f8e7eb834777c31f2` | `bd2a15643ed74d9156858140ce1c39383b49305f2854c7a1ec3331653adb0019` |
| `be7036fca7dd5667aef3767a3cd876fe67cdb6081f7725d89dce85ac3e3f9505` | `d736ff6630d21d5e22b6e1dc574bf0f86088b4d5662581e7414631ed105f97b5` | `d67958d6538e2b9f151ccbf82000a65f263d64624c1ffa52523e694865f88579` |
| `d2e18f75e64b5c24c423ebe4f4bde3018be2dd75d2706fb887fe936f54daf0b2` | `eaf65885e8c4dc128618d6fdd8633504243f643b2370df157c7da99794b048db` | `80e44c71319f8472df864d849733b71dbd8122421c620373b9b575808dbfbc90` |
| `f14a7db52c27c369bae2e39c3cdd32500cf8a03655b8878916a30bdaaa41ef0f` | `df9f1b2b316191f3215a584098f85277cfc5b4fd652d9a93a2fdbcf81a037ccf` | `3b998a3bdf324e888cf344a7f8e8c3eb1df1478a4ed7c91a01cf9f38f616b226` |
| `6d9e905f6cd4fffd2fde87859e6d2940e01600852d5702c7eb70f8ecb2fa1dab` | `6d02e9c89109687c97d8bf9d7398d178099a4b45b2b06d4c009f55b072100dec` | `e3b9a1a099effaaf8911232eeaf2ea4406fd1eb74f40479890a501a9da511915` |
| `9b464c2e2d1550013c0f5b34107ecd9590a7bafdc78d6b4055c7c72bae6a16e4` | `fa629e1f29c71a94fb27654d626b9b20e697172eb3805b2b6196428a41c8bf90` | `142d905d784d99a12ab3e45f43fc53bc580f8ec590916bf6d5663e708558803b` |

Key sequence citations (recording rows above, ordered by raw operation):

| Source row | First Twilight start / visual end / helper actions | Second Twilight start / visual end / helper actions |
| --- | --- | --- |
| 1 | `1260133 / 1264367 / 1264417,1264419` | `1307290 / 1310610 / 1310557,1310580` |
| 2 | `1381582 / 1387273 / 1387337,1387360` | `1434709 / 1441001 / 1441104,1441115` |
| 3 | `1348231 / 1352681 / 1352749,1352764` | `1397460 / 1402984 / 1403078,1403086` |
| 4 | `1376640 / 1381334 / 1381427,1381443` | `1432152 / 1438823 / 1438902,1438930` |
| 5 | `1385646 / 1390051 / 1390145,1390150` | `1441132 / 1446880 / 1447020,1447026` |
| 6 | `1423589 / 1429431 / 1429534,1429539` | `1477003 / 1483227 / 1483348,1483354` |

For example, row 2's second visual finishes at relative tick `16821684924`;
both helpers arrive at `16822455881`, **77.0957 ms later**. Row 1's second
helpers precede the visual finish in sequence at the same timestamp,
`16087470724`. Rows 2 and 3 also separate the two helpers across timestamp
groups. These are client observations, not server snapshot measurements.

An additional unfiltered-ID `implementation_coverage` review exhausts three
pages in row 1's first cast-to-last-helper window (214 selected events), with
frozen review contract
`5d2b255a4ca493ca594617006b28fae13f35a8b9c6209b75dec249e7917154a4`.
The four floor callback events are declared handled; four boss/tail visual
events are explicitly ignored as completion triggers. The other 206 selected
events remain unmapped to this bounded repair. This is not full encounter
coverage or a claim that other observed events are irrelevant to the fight.

## Validation

- Full Release plugin build succeeds with the six existing obsolete-gauge
  warnings and no errors. Generated factories activate `FloorAOEs`; native
  closed-original replay activates the real module/state machine.
- The complete `tests/ModuleRegression` suite passes **105 assertions**,
  including the earlier six module groups. Shinryu fixtures cover both colours
  on both authored layers, custom distance predicates, native AI filtering,
  rendered colours, text/risk hints, both helper orders and visual orders,
  duplicate callbacks, missing helpers, status loss/replacement/unknown actors,
  a newer mechanic replacing an incomplete one, deactivation/reactivation,
  existing-state activation bootstrap and all three swipe IDs. These are
  synthetic callback regressions, not physical evidence. The pre-change build
  fails the Shinryu direction, retirement and conflicting-assignment cases.
- Paired native replay evaluates **156 samples** across all twelve sequences.
  All samples are available, with zero module errors, skipped raw operation
  kinds or unsupported output kinds. Both parsers produce identical complete
  numeric-event manifests and coverage, apart from their expected different
  assembly MVIDs. Original file SHA-256 values are checked before and after
  replay; private originals are not modified.
- **70 active Twilight samples** have the expected centre/outside AI predicates,
  covering all four colour/layer combinations. Baseline comparisons reproduce
  26 wrong-direction samples, 44 prematurely cleared samples and two samples
  retained too long when both helpers precede the visual finish.
- **72 preceding-mechanic samples** preserve geometry and AI constraints.
  Other components' exported AOEs match at every paired sample. Wherever both
  versions publish floor AOEs, exported geometry, layer selection and activation
  deadlines are identical (68 samples); only the separate custom AI predicate
  changes during Twilight.
- Release metadata validation and all five Python ordering/version tests pass.

Frozen component source SHA-256:
`e621da7b6b61ec84f53e3ec058e3803812100cd78691c66ed52dc9337af920f7`.
Baseline assembly SHA-256:
`f710ce5f8130dc885965eaf7cfbe4a0b139af053355461c2632375666373c1ac`.
Candidate assembly SHA-256:
`8689a7d6ab8263e7c2a61350a4e7bc1f28c186d82a06785c2a7e3234bf7b21c7`.
The normal development output contains that candidate assembly. Private replay
requests, reports, pattern citations, comparison scripts and the validation
summary are under `/tmp/xbmr-shinryu/`; these are local ephemeral evidence,
not release assets.

## Remaining acceptance

The original 2-yalm launcher proxy, 6-yalm hole proxy, `(820,-826)` origin,
arena stencils, height split and cast-finish activation forecasts are preserved.
Their physical boundaries, launch/drop reachability, last-safe departure time
and server snapshot timing are not newly measured. Caller probe floor labels
are not physical mapping evidence. The native exporter reports incomplete
acceptance and a local game-data version mismatch for all six originals.

No soft staging goal, forced movement, travel-time estimate or invented
cancellation deadline is introduced. Fresh independent recordings and actual
duty testing are still required for live xBMR/XCAI acceptance, including casting
during transitions and interrupted/missing-helper variants. This repair does
not certify every Shinryu mechanic or change module maturity. No source tag,
public release or installed-directory copy is made by this task.
