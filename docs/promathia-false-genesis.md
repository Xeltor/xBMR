# Promathia: False Genesis arrival and Empty Seed barriers

Development repair on 2026-10-14 against parent
`c56311efc798507846648705dc517dd7fa7cb706`. Release metadata, module maturity,
upstream notices and plugin compatibility are unchanged.

## Repair contract

False Genesis (`50343`) previously drew three safe squares but supplied no
AI arrival constraint. Arena bounds changed only when the cast finished, too
late to plan the approach. Its text predicate also warned players inside either
rotated square. The component now supplies a timed forbidden region outside the
union of all three squares from cast start, with a fixed native cast-finish
deadline. The connecting floor remains available while approaching. Drawing,
text and AI use the same platform definitions; no alliance assignment is invented.
The floor changes on the successful boss action, rather than any cast-finish
callback (which also occurs on interruption). The existing restoration map effect,
index `0x0C`, state `0x00080004`, restores the circle and clears pending preparation.

Empty Seed (`50349`) previously forced a small cone near one specific corner,
only while within 9 yalms of the source, without checking immunity or providing
the knockback deadline. The replacement derives four safe radial sectors from
all eight existing barrier segments. Each sector is the convex region between
the source and a corner's two segments; every interior ray reaches a barrier
within the 10-yalm displacement. The exact source point is excluded because its
radial direction is undefined. Open edge directions remain forbidden. The
constraint applies only to an affected actor's platform and is timed to that
source's native cast finish. Immunity must cover impact, rather than merely be
active now. Existing native knockback projections use those same barrier segments.
Duplicate source starts replace their own state; cast finish, actor destruction
and arena restoration clear the relevant predictions independently.

The nominal platform centres, rotations and half size remain
`(-820,-807), 0 degrees`; `(-808.724,-826.5), +120 degrees`;
`(-831.258,-826.5), -120 degrees`; half size **6.5 yalms**.
The existing corner distance **8.9** and barrier segment length **3.5** are
preserved collision proxies, not newly measured physical boundaries.
No shared knockback/pathfinding code, XCAI source or teleporter behaviour changes.
A read-only XCAI source check confirms that its generic sequence planner yields
for shaped knockbacks with SafeWalls; the native BMR constraint retains authority.

## Recording evidence

Six distinct original source digests contain one False Genesis sequence each.
The first two informed the repair; the other four Promathia windows were inspected
after source/build freeze. These originals were previously used for other
encounter investigations, so they are not claimed to be pristine whole-recording
holdouts. Duplicate imports were excluded. All selected anchor/context pages
finished without truncation, sequence clipping or retained-range exclusions.

| Use | Recording ID | Original SHA-256 | Retained revision | Boss cast start / action / cast end sequences |
| --- | --- | --- | --- | --- |
| Design | `216d4cfe585ec3ef6501bf9b10b940c76a207a26a6551fd872a54e7dd10e4318` | `eaf65885e8c4dc128618d6fdd8633504243f643b2370df157c7da99794b048db` | `616395e1e18d29e4f821635ebfe4b2cb7dd16af6e098fd3cac801c8a5ba593a5` | `1036168 / 1041595 / 1041596` |
| Design | `6c85fbd4c236802850758b3dfc4c44c258c691b1eca2fe29d33329c016330ec0` | `fa629e1f29c71a94fb27654d626b9b20e697172eb3805b2b6196428a41c8bf90` | `df75ce04f4a83a8b228aa835a9511d74ba5a34f5af30961255e9a221ace98940` | `1087438 / 1095053 / 1095054` |
| Post-freeze evaluation | `bbedd3081c1583ad3a5c34c3f8319e0ef56001267c9774196ba1da49fcfb27bb` | `6d02e9c89109687c97d8bf9d7398d178099a4b45b2b06d4c009f55b072100dec` | `616ea8a27cd271cb510f21ce534e933343cb03581ea42d4b33e15316ef3f21b0` | `1135016 / 1142461 / 1142462` |
| Post-freeze evaluation | `d20a982a413d1f48cdb840c23acde4713ca9a9218d4239d60b426b0e6648f743` | `d736ff6630d21d5e22b6e1dc574bf0f86088b4d5662581e7414631ed105f97b5` | `85e4d21d904af981b4a19fc945cc787a83b46205fb96edc9c528940690bf3e54` | `1107548 / 1114294 / 1114295` |
| Post-freeze evaluation | `e208c64b10ea036c15fcd066bbae657c41e0b8c810e9c28720659c340555541f` | `c97cef48d62d3d4da612911409967e9cdf7dbd475b062d6f8e7eb834777c31f2` | `1f579298649513cb05697dcb4004f3a4e6e5da646881e293fd9c9624076f5853` | `982568 / 989935 / 989936` |
| Post-freeze evaluation | `7c2d1362d4ed6e90c39c281b9941fb2bb8192f6320821a54cdec9bbd04b93984` | `df9f1b2b316191f3215a584098f85277cfc5b4fd652d9a93a2fdbcf81a037ccf` | `498fb25060b8cf2a44dbde46db41a8583ac486f472be0ef8f40aeb835fd45ace` | `1064452 / 1073634 / 1073635` |

Across these sequences, the successful boss action arrives **9.4807–9.5137 s**
after cast start; the helper False Genesis (`50344`) action arrives
**10.0424–10.0877 s** after that start. These are client observations, not server
snapshots. The repair uses the existing NPC cast correction and does not fit a
new timer to these samples. Memory Receptacle (`0x4DF0`) creation observations
place sources at the three existing platform centres (small quantization/nominal
coordinate differences remain). Seed casts overlap across all three platforms;
later casts can end without an action as receptacles die. Both rotation icons
(`689`, `690`) and the existing `50351`, `50353/50354`, `50355` add mechanics occur.

Matching `2026.09.15.0000.0000` Action-sheet evidence reports Empty Seed's circle
range as 10. Recorded effect word `0x000100000000001f` selects Knockback row 1;
that row reports distance 10, speed 20 and radial direction. The archive correctly
refuses automatic effect interpretation against its older pinned BMR reference;
the raw layout was reviewed against the current fork. These fields support the
existing displacement model, not collision or immunity measurements.
Auroral Drape's sheet fields support the existing 7-by-7 rectangular model;
this repair does not replace it with a guessed 180-degree cone.

The unfiltered-ID create/cast-start/icon/map inventory exhausts 210 selected
observations in the first design window. A frozen implementation-coverage review
maps the nine changed cast/restoration observations; the other 201 observations
remain outside this bounded repair, rather than being declared irrelevant.
Review contract: `5394aec9a0758742c0a17c3476af8051925c33a17ecd005b858331819033a981`.

## Validation and limits

- Full current-working-tree Release build: success, zero errors, six existing
  obsolete-gauge warnings. Generated factories activate all changed components.
- Native module regression suite: **181 assertions pass**, including all three
  platforms, all twelve corners, open edges, arrival deadlines, immunity expiry,
  duplicate starts, interrupted Genesis, native corner projections and restoration.
  The saved baseline fails the new early-arrival and duplicate-source cases.
  These are synthetic callback/geometry checks, not physical measurements.
- Closed-original native replay: **286 available candidate samples** across all
  six sequences; zero module errors, skipped native operation kinds, unsupported
  output kinds or missing candidate samples. Original hashes are checked before
  and after playback. **24 early preparation samples** contain the required union
  constraint; six native navigation fixtures select destinations inside a square
  from cast start, including destinations on each of the three platforms.
- **106 paired baseline/candidate samples** preserve every other component's
  exported AOE geometry/timing. The baseline supplies no False Genesis arrival
  constraint. Two initial baseline navigation fixtures mistakenly used interior
  timestamp-group anchors and were unavailable; they are excluded from comparisons.
  Candidate navigation uses valid after-operation anchors.
- All five Python release/version tests and the metadata validator pass.

Frozen module source SHA-256:
`84d7df04d54c58a349b06f95e3135503feca3b3b3207bd427a93687745350d72`.
Baseline assembly SHA-256:
`8689a7d6ab8263e7c2a61350a4e7bc1f28c186d82a06785c2a7e3234bf7b21c7`.
Candidate assembly SHA-256:
`1518243fc09199de095c22dd5e683e70a17726c0a27e68f1d3f55dd1ef724309`.
Candidate MVID: `94058e24-7cde-426c-905c-a828d3e3cee0`.
Private requests, reports and detailed evidence are in `/tmp/xbmr-promathia/`.
At completion of that repair, the normal development output contained the
candidate DLL and was watched for
automatic reload. DevBridge reports the development plugin loaded; its inspection
cannot confirm this build's loaded MVID. No installed-directory copy or manual
reload was performed.

Native projection decoding is complete, but the runner's aggregate export stays
incomplete because physical geometry, snapshots, controller execution and other
surfaces are unverified. Its offline game version differs from each recording;
matching sheet datasets do not erase that replay limitation. The anonymized
projections omit operations; the full native replay used the closed originals.
The exact platform edges, barrier collision margins, server last-safe departure
and immunity outcomes still need independent/live evidence. Teleporter use,
System Lock and assistance on other platforms are unchanged and unvalidated by
this repair. Live xBMR/XCAI arrival and knockback survival remain pending. There
is no maturity promotion, tag or public release.

The subsequent [ring, tower and Heavy-puddle repair](promathia-rings-towers-puddles.md)
supersedes the development build identity above while preserving these changes.
