# The Lominsan Way

Implementation and evidence review: 2026-10-17. Quest 66347, territory 307,
content-finder condition 354. The new encounter module remains **WIP**.

Follow-up: two more indexed segments exposed this quest's actual Rive action,
1299. The initial implementation incorrectly watched the same-named action
1135 from another variant; the first failed attempt had no casts to reveal it.
The module now watches 1299, warns during targeted Paralyze, requests an
eligible cleanse through native hints while paralysis is present, and warns
from bomb creation. The initial validation figures/artifact below are historical;
the follow-up section records the corrected candidate and new validation.

## Written guidance

Read before implementation:

- [Thonky, solo duty](https://www.thonky.com/final-fantasy-xiv/quests/the-lominsan-way):
  attack the Large Boulder, sidestep Rive, cleanse paralysis if possible, and
  continue attacking the objective during the bomb countdown. Ignore Trachtoum,
  his boulder and the bomb.
- [Console Games Wiki, solo instance and dialogue](https://ffxiv.consolegameswiki.com/wiki/The_Lominsan_Way):
  confirms the boulder contest and prohibition on attacking Trachtoum.
- [Official quest entry](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/6f432e3410f/):
  correct ARR quest identity.
- [Historical official forum report](https://forum.square-enix.com/ffxiv/threads/189166):
  additional context for the bomb race; its reported health threshold is not
  accepted as a verified trigger.

These concern this solo quest, not a dungeon or higher difficulty. Guides do
not establish physical geometry, server snapshots or last-safe movement times.

## Recording and indexed archive

The archive original and the privately preserved native recording have the
same SHA-256:
`420bce988ec24814d7ae4b6fcccb4e69153db677fe75c561a1e27ed2c59f490a`.
They are one authoring input, not independent evaluation pulls.

- Archive recording:
  `8e6296ea0d28ba360b73e1c7376f723233c85b662ccee1ed84970c38c226bbea`.
- Retained revision:
  `e64583f8291f44f05d3e1791ec9618de93995165a588721686965e4c33bb033d`.
- Matching game version: `2026.10.06.0000.0000`; native BMR format 31.
- Original decodes to EOF: 3,774 operations, approximately 54 seconds of
  recorded state. EOF does not independently prove the entire quest was captured.
- Current archive projection retains 468 events and omits 37 HP/MP operations,
  NPC yells and other inputs listed in its source contract. Health conclusions
  below use the native replay/schema-2 projection, not absent archive health.

The revision-bound MCP inventory covers raw sequences 190–3774. Its selected
events are exhausted in one page with 27 token groups. Lifecycle, target and
targetability observations agree with the native replay. The coverage mapping
explicitly declares context actions, player buffs and noncombat event objects
ignored. Mapping declarations are source/replay review claims, not certification
of behavior, complete mechanics, geometry or timing.

Observed actors are Trachtoum `0x6E9` (alias 54), Large Boulder `0x6EA`
(alias 51), and Small Boulder `0x6EB` (alias 52). Both boulders initially have
radius 0.5 at creation; size updates 213/214 change them to 1.5. The module uses
the current actor hitbox for targeting rather than fixing the creation radius.

The Large Boulder becomes targetable at sequence 223 without entering combat.
Its recorded health remains 3,900. Player attacks target Trachtoum first, then
the Small Boulder; Trachtoum also attacks the Small Boulder. The Small Boulder
reaches zero HP at 3346 and dies at 3347, tick 491225190. This is the recorded
failure branch. No Rive casts, paralysis 17, bomb actor or countdown messages
occur in this attempt. Absence here does not remove those guide-supported
branches from the encounter.

## Module behavior

The Large Boulder anchors the encounter. Activation requires its targetability
in the exact territory/CFC, without an InCombat prerequisite. The phase ends
when the Large Boulder dies/is destroyed, or a previously observed Small
Boulder dies/is destroyed. Initial absence of the Small Boulder cannot falsely
complete the phase. Components clear at the recorded failure transition;
leaving the exact duty resets the module.

AI hints promote only the Large Boulder from native passive priority -3 or an
eligible nonnegative priority to 1. Existing exclusions -1/-2/-4 on that
objective remain authoritative. Trachtoum, the Small Boulder and Bomb receive
priority -4 and a DoT prohibition; this deliberately strengthens -1 because -1
still permits incidental area damage. Unrelated, friendly, untargetable and dead
actors are unaffected. A global instruction and current-target warning explain
the objective to the player. No game actions or consumables are queued.

The pre-existing QuestBattle automation script is preserved. Its hints require
the optional quest-battle setting and a native movement consumer. The recording
does not establish those saved settings. Encounter hints provide the objective
handling through the ordinary encounter path without changing global gates.

Rive uses the earliest supported cue available to this implementation: a spell
cast start by hostile Trachtoum with Action 1299. A standard rectangular AOE
uses the sheet's nominal range 30 plus Trachtoum's observed radius 0.5, and
half-width 1. Standard native NPC cast correction supplies the prediction.
It handles bootstrap, duplicate starts, cancellation, death, destruction and
reset. The follow-up source establishes Trachtoum's cast/action identity and
recorded warning lead. It does not establish physical range, server snapshot
or last-safe departure. Synthetic timing checks validate implementation behavior
only. The radar shows the single
active cast; there is no speculative multi-step hazard preview.

Paralyze 1118 produces a warning for its actual cast target during the hostile
cast, including already-running casts at activation. The matching sheet calls
it a four-second single-target spell without an omen; facing/line-of-sight flags
and a guide's suggestion to move behind Trachtoum do not prove an avoidable
cone, facing lock or last-safe response. No speculative movement AOE is added.

Paralysis status 17 produces a cleanse-if-possible warning while present and
sets the affected slot in native `ShouldCleanse`. Existing action eligibility
and healer/manual control remain authoritative; the component queues no ability
or consumable. Dead actors and absent statuses do not request a cleanse. The
matching Status sheet marks it dispellable. This does not prove the player has
a usable cleanse. Status 7 is Silence and is not used.

## Typed game data and bomb branch

The matching QuestBattle parameters refer to **Level sheet rows**, not assumed
LGB instance IDs. Typed BNpcBase references resolve as follows:

| Parameter | Level row | BNpcBase | Meaning |
| --- | --- | --- | --- |
| INIT_POP_1_KAI_01 | 4295469 | 1769 | Trachtoum |
| INIT_POP_1_ROC_01 | 4295470 | 1770 | Large Boulder |
| INIT_POP_1_ROC_02 | 4295472 | 1771 | Small Boulder |
| INIT_POP_2_BOM_01 | 4295473 | 1772 | Bomb |

DEF_PARALYSIS is 17. LOGMESSAGE_QIB_01/02/03 resolve to LogMessage 3217/3218/3219,
which contain the 15/10/5-second self-destruct countdowns. These configured
bindings do not establish observed runtime ordering, spawn-to-explosion timing,
an explosion radius or a safe escape response. The module forbids targeting
the bomb and adds an objective-race warning as soon as the live hostile bomb is
present, even before targetability. It does not invent an
explosion AOE or a countdown from spawn time. The Level placement radius 6 is
not used as a gameplay size.

## Arena and maturity limits

The matching `planevent.lgb` layer `QB_GaiUsb004_001` (93056) contains partial
quest barriers: collision boxes 4295442/44/50/51/55 and associated line VFX.
Other ManSea302 layers belong to another quest. The asset path is
`bg/ffxiv/sea_s1/fld/s1f2/level/planevent.lgb`, SHA-256
`a2fb4fa2a464aebf6fdfc22801fd1ee0dd3d58fd04ff8fbc295a16f3c1e1bdc9`.
The initial note incorrectly named `planlive.lgb`; the follow-up checked each
layer's own path and corrected that provenance.

The fixed circle at (695, -292), radius 12, is a conservative central planning
cap around objective landmarks within the partial barriers. It constrains
native movement/pathfinding, permits an obstacle map if available, and suppresses
an unverified 3D floor border. It is **not an accepted physical arena boundary**.
Recorded player movement was not used to infer it. Full collision semantics,
natural edges, holes, slopes, intervening obstacles, actual map availability and
continuous reachable routes remain unverified. This material arena gap keeps
the module WIP under the maturity policy.

## Validation

### Initial snapshot

- 53 synthetic assertions pass: registration, passive activation, target
  priorities/exclusions, direct absence of queued actions, status loss, nominal
  Rive shape/deadline and lifecycle/reset cases. Bomb/paralysis/Rive branches
  are simulated because the recording does not contain them.
- 236 recording-derived assertions pass over 15 available native samples.
  One deliberately requested post-exit sample is unavailable and is not counted
  as a successful mechanic sample. Callback observations independently confirm
  phase cleanup. No module errors, missing anchors, unapplied raw operations or
  game-version mismatches occur.
- Private native candidate: source SHA-256
  `8833a36141ab1e274ebe42d11016288fe8ab0dc0eb53e0fdb8827538d924f258`,
  DLL SHA-256
  `f16dce5ece323f1a28be508a3c0e9408975fc1b863d4eb34491e0999bf7e1686`,
  MVID `c4a4762d-52bf-49fc-837c-ca6d12066875`.

The current working tree, including unrelated local changes, builds successfully
with the pinned SDK 10.0.401 and API-15 development assemblies: zero errors,
six existing obsolete-gauge warnings. All 53 synthetic checks and all 236 native
recording-derived assertions also pass against that final watched DLL:

- Path: `BossMod/bin/Release/BossModReborn.dll`.
- SHA-256: `e055c206d58fb5d2a1ed8ec30b32d7c37f4fe03557c2bf6faeadac346c1c8e97`.
- MVID: `db4342e3-6e3f-41d5-8136-8f048988e9d4`.
- Fresh out-of-combat observation at 09:54:33 Amsterdam preceded the build.
  The watcher finished loading BossModReborn at 09:55:23. No manual reload or
  installed-plugin replacement was performed. This is reload evidence, not
  live encounter acceptance.

Original, pinned MCP responses, typed-data extractor, native requests/reports,
numeric projection, checkers and build receipts are retained privately under
`<private evidence location>`. Raw recordings,
player identities and extracted assets are not committed. Offline checks and
reload observations do not establish live XCAI/BMR response, physical floor
safety, calibrated warning lead or a successful correct-objective attempt.

### Follow-up: observed casts and bomb race

The new indexed sources are:

| Role | Archive recording | Original SHA-256 |
| --- | --- | --- |
| Mechanically reserved segment | `9bf3f4de271c91cb5e2e77049d7624fa814ec6d1c2ce1a57ed88e482daddc2ac` | `e8076411e290df700169ea58ced5340335e2cbae902c27344456c473ccd5b207` |
| Authoring segment | `d33b7839a788c633a1d7a0686a2aa33ed5fd0c7ab89cfea4a5f065aade5b462f` | `ba47c28df56ec9502f30b6f3c27675db4c7d57b269b0955536dccddeeed15111` |

Retained revisions are respectively
`a3af71e38608f4c59e95f7cbad6110f394eedc754b7877f2ed699c4c0b30da1a`
and `7bab4686730251f6cedbb1dad85d42685539830dad4eff6ae4d9af2caf80509d`.
Both have complete one-part schema-3 manifests. Their native originals decode
to EOF through 8,325 and 5,530 operations using the compatible parser. Schema 3
omits HP/MP, system-log and other observations, so native schema-2 health and raw
log extraction are retained separately. Projection copies are not extra pulls.

The reserved segment was not mechanically inspected until source and behavior
expectations were frozen. However, its end and the authoring segment's start
are only 4.936 seconds apart, with matching objective health and ongoing combat.
They strongly appear to be one contest split by the plugin reload. This is a
reserved segment check, **not an independent trial**. The authoring segment
starts mid-contest, with the Small Boulder at 1,431/2,418 HP; complete decoding
does not mean complete recorder coverage.

Exact authoring observations (relative .NET ticks):

- Trachtoum alias 3 casts Paralyze 1118 at sequence 1375/tick 195715201;
  matching action 1658/tick 235354460, then paralysis 17 gain at
  1769/245696534. Actual status loss is 4137/545509835.
- Rive 1299 starts at 2257/318251148 and has matching action/end at
  2441–2442/343184904. Native correction predicts activation at 343021936,
  approximately 16 ms before the recorded action. This is a frame-observation
  comparison, not snapshot/last-safe calibration. Warning is available from
  cast start, approximately 2.49 seconds before the recorded action.
- Bomb alias 7/OID 1772 appears at 2829/379338399 and becomes targetable
  at 2847/380163517. Last Resort 1133 occurs at 5093/699073463 after its
  death flag. The matching sheet's nominal range 6 is not physical avoidance
  evidence or a reason to abandon the objective race.
- Countdown logs 3217/3218/3219 occur at 4012/526842717,
  4340/576680557 and 4680/626994106. The first notice is 14.75 seconds after
  bomb creation; spawn is therefore not a verified 15-second deadline.
- Large Boulder alias 5 reaches zero HP at 4366 and dies at
  4367/579345466. The final countdown and explosion occur after the objective
  phase has ended. Native callbacks independently confirm component cleanup;
  later signals must not revive its guidance.

The frozen corrected module source SHA-256 is
`49352705f97963f987284ba655ae9ace90e1ce306430116e8faaad698784a9fb`.
Its isolated full-fork DLL SHA-256 is
`b1fb127b0698415fe589de802dc0d35c5e11896c6d00ab52dfbfc34a1de888cf`,
MVID `bdfa2403-3977-4432-a568-a0becf3db8f1`. Full build succeeds with zero errors
and the six existing obsolete-gauge warnings. All **90 synthetic checks pass**,
including the previous 53 and new action-variant rejection, targeted cast,
native cleanse-mask, bomb bootstrap/lifecycle and phase-cleanup cases.

Native checks pass **429 assertions** over 23 available authoring samples and
**125 assertions** over 13 available reserved-segment samples. Five requested
samples precede module initialization and one follows phase exit; those outputs
are explicitly unavailable and are not successful mechanic samples. Expectations
distinguish immediate operation callbacks from the manager's completed-frame
updates. Authoring samples cover three Rive points, three incoming Paralyze
points, twelve paralysis points and eight active-bomb points. The reserved
segment contains none of those later branches. Both replays have no module
errors, missing raw anchors, unapplied operations or game-version mismatches.

A private runner extension exports the actual `ShouldCleanse.Raw` mask and
hashes of global-hint literals. It calls the existing native AI-hint calculation
once and preserves all previous outputs in a 16-sample control replay, apart
from the expected harness hash. This local fixture is not a new archive MCP
capability or proof of live controller/UI delivery. Baseline replay reproduces
zero Rive hazards during 1299 and no cleanse request; the corrected candidate
produces the timed rectangle/AI constraint and current-status cleanse bit.

The exact new originals, frozen contract, pinned MCP inventory/cast pages,
independent numeric checker, extended runner source identity and reports are
private under
`<private evidence location>`.
The separate arena review also confirms no static zone-307/CFC-354 obstacle map
in this checkout and no established collision extents/terrain semantics from
the available LGB dump. Keep WIP for the material floor/route and physical
timing gaps; these checks do not establish live XCAI/BMR encounter acceptance.

Final current-tree build and reload:

- Watched DLL SHA-256:
  `299e51d523b832510be9469b7ad88b78f9bf88e5b64ffaee8bb9f5bee4a3bb15`.
- MVID: `faf5374a-d396-4f19-864d-6632d42a662e`.
- All 90 synthetic checks, 429 authoring assertions, 125 reserved-segment
  assertions and the initial failed attempt's 236 assertions pass against this
  exact DLL. The earlier failed attempt still has its one expected post-exit
  unavailable sample. There are no new gameplay-safety claims from repeating
  these checks against the final artifact.
- A fresh out-of-combat observation at 10:11:09 Amsterdam preceded the Release
  build. It succeeds with zero errors and six existing warnings. The watcher
  finished loading BMR at 10:11:57 without a manual reload or binary copy.
- The revision-bound authoring MCP inventory is exhausted. Frozen handling
  declarations explicitly account for the observed casts/status/lifecycle and
  ignored context, including Last Resort after objective completion. This is
  observed-token mapping, not independent correctness certification.
