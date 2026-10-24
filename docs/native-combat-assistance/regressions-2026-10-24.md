# Mistwake migration regressions — 2026-10-24

## Evidence and scope

The maintainer reported multiple deaths/party failures, missed active mechanics,
no pack acquisition, excessive distance during warnings, missing gap closing and
unsolicited out-of-combat facing. The run used development assembly
`656ca5dd-204b-443d-ad97-510e1039f842`, not either repair build.

DevBridge retained final-boss capture `dd12c370fd5c4046955027f61c7c7b90`, revision
`67beb8800fb2460d88ab0aee918da67e`, from 11:03:37 to 11:06:36 UTC. Across all
707 samples, 122 had no movement plan, 21 reported stale planning input and no
sample showed mobility execution. Three samples reported a route no longer
clearing native deadlines. Capture sampling does not reconstruct every hit or
prove which individual defect caused each death. Earlier deaths predate this
retained capture.

## Repairs

- `NativeOffensiveTargeting.ResolveTargetIntent`: acquire an eligible engaged
  native enemy without first requiring an RSR action. Henched RSR requires a hard
  target to choose its attack. Keep current usable targets, manual retention,
  preset/encounter authority, blacklist/stop constraints and saved opt-outs.
- RSR `CurrentInterceptedAction` is historical and can survive an expired queue.
  Targeting now yields to the live `CommandNextAction` queue plus existing command,
  preview and force gates. The installed 7.5.7.6 contract is checked.
- `NativeMovementHost`: when no fully revalidated optimized directive is usable,
  calculate recovery using the current frozen native map and original ThetaStar
  immediately. A prior worker publication's positive leeway cannot authorize
  waiting under changed hazards. Mandatory movement cannot depend on optimizer
  publication, cancellation or freshness. Manual and special-mode gates remain.
- `CombatAssistanceRuntime.Capture`: no attack selection no longer drops the
  available mobility inventory. Capture native role range independently of a
  ranged filler. The archived XCAI `MeleeUptimePolicy` likewise explicitly allowed
  range recovery without predicting an attack; that policy was missing in the
  initial native conversion.
- Joint search compares useful range recovery with the observed native GCD window
  before saving a charge. Walking and mobility arriving before that window tie,
  preserving the resource. Current actions/recipients and native constraints keep
  their higher priority. Never invent a subsequent attack or warning expiry.
- The first complete opportunity pass considers walking and legal anchors/returns.
  The full search still considers spatial ground/directional alternatives. First
  walking-only publication must not preempt an available useful targeted dash.
- Cosmetic party facing belongs to module-owned boss immunity/downtime. After
  maintainer clarification, its gate requires an encounter module and BMR downtime
  or unavailable-target evidence, without requiring the player's combat flag or a
  globally active timeline state. Stable party consensus, manual input and mechanic
  protections still apply. The existing world-marker overlay distinguishes target
  acquisition from an evidenced action target and renders native routes/stages.

## Validation and limits

Managed Release compilation succeeds with zero errors and six existing obsolete
job-gauge API warnings. The native suite passes **42,910 assertions**; nine
repository tests and installed RSR 7.5.7.6 contracts pass. New regressions include:

- No-action hard-target bootstrap, dead/removed/unusable targets, opt-outs,
  priorities, manual authority, retained usable targets and target-distance limits.
- A worker deliberately blocked until cancellation: native movement and immediate
  active-area egress still execute in the same frame, including optimization mode.
- Empty action evidence and ranged-filler range recovery with a legal target dash;
  comparisons with unpruned exhaustive small-map searches; first publication with
  target mobility; timed-warning traversal; physically blocked dash rejection.
- Charge preservation when walking restores range before the observed GCD window;
  countdown-equivalent input continuity and native melee/caster hitbox distances.

Final development DLL SHA-256:
`013ae0ca9af508f21e728bd43b3398e7b0017511c515b1796197a2cd4f5672ca`.
MVID: `4d940fb8-81c9-4c68-9047-61c5781658eb`.
The public release has not been changed by this repair pass.

A comparable live dungeon run is still required to accept exact mechanic movement,
real target handoff, dash/action delivery and sustained uptime. Do not mark these
accepted from test counts, source inventory or a successful DLL reload. At that first checkpoint, no gameplay inputs, target changes or AI activation
were issued. Later NPC test runs below were explicitly authorized.

## Social-facing clarification

The maintainer clarified that social facing belongs to boss immunity/downtime
inside an encounter module. Requiring both the local combat flag and an active
state machine was an overcorrection. The corrected eligibility requires a module
plus unavailable native targets or authored downtime. It preserves stable party
consensus and the existing manual-input, action, gaze and mechanic protections.

Six additional regression assertions cover missing-module rejection, immunity
without a local combat/state requirement, authored downtime, active-opportunity
and unknown-timeline rejection, consensus stability and module-exit release.
Managed compilation and all **42,916 native assertions** pass.

Follow-up DLL SHA-256:
`c64b0faaf13f4b3367fa8810b3c61663dd94aa25b8a5d6f13ca46f341c2d519a`.
MVID: `3bc8784e-f3ae-414f-a0fd-f5472fa59aa3`.

## Duty Support Mistwake follow-up

The maintainer ran Mistwake with Duty Support on the above development build.
DevBridge retained the native combat stream. Native party synchronization adds
DutyHelper actor IDs independently of Dalamud's empty player-party list; support
NPCs therefore must not be diagnosed as missing from that empty list alone.
Target bootstrap is observed in pack captures, including the transition back to
the original RSR action selector. This is evidence of that path, not complete
targeting acceptance.

The maintainer reported boss-position shaking, ranged fallback instead of useful
melee recovery, and an ongoing failure to approach a pack. Exact retained evidence:

- First boss: capture `a6003e9e2b3d411986fe4d367483dcb3`, revision
  `52550876257c459e8ee6857e93d546b5`, 11:49:24–11:52:11 UTC, 653 samples.
  At 11:49:57 the publication waits for action 90 (Piercing Talon) while outside
  melee. Later frames contain a ranged-action stage before further walking.
  At 11:51:19–11:51:22 destinations move between approximately `(79.75,374.75)`
  and `(79.75,367.75)` across current action changes. The native desired positional
  still followed the raw action even while observed True North bypassed it in the
  execution evaluator. This explains one unnecessary movement source; do not
  infer that every destination change in the fight was unjustified.
- Pack: capture `66aaa729e87045f99d6b7efedea07c3e`, revision
  `238f9edad260421d8f679de5d0fb0843`, 11:59:41–12:00:57 UTC, 312 samples.
  294 samples have `Invalidated / native recovery route changed`; the remaining
  18 have no movement publication. At the maintainer's 12:00:16 report the player
  is about 8.5 yalms from the selected enemy and native leeway is about -1000.
  The failure field is null despite the executor rejection; inspect execution
  reasons as well as that field when diagnosing this build.

Repairs:

- A fresh native recovery publication from the identical frozen map and exact
  starting position retains ThetaStar's own egress semantics. Elective traversal
  cannot reject its occupied blocked starting cell and permanently prevent escape.
  Changed-position/publication checks, actual collision observations and manual
  authority remain. Elective routing still rejects impassable starts.
- Melee fallback actions no longer become a required stationary execution before
  restoring melee access. RSR continues selecting and delivering its normal
  actions; this repair changes native movement planning, without adding a new
  ranged-action veto. Other attacks and independently required heals remain.
- While optional optimization is pending, a complete strict native witness that
  restores combat range may execute immediately when no real action/cast is being
  preserved. Positive safety leeway is not permission to stall combat recovery.
- Native positional following uses the same observed True North window as attack
  execution. A buff expiring before the selected attack does not waive its
  positional requirement.

Managed Release compilation passes with zero errors and six existing job-gauge
warnings. All **42,923 native assertions** pass, including native blocked-start
egress, unchanged elective rejection, complete strict range-recovery eligibility,
fallback classification and True North window behavior.

Repair artifact SHA-256:
`1a3255bed628cdb71bac24fe9854390ab55e8be79fc858b6ffc8dc4fb8761a6c`.
MVID: `e4db215e-a364-47ce-ab24-b7c5347577b9`.
The watched development DLL was updated after an out-of-combat preflight at
12:10:10 UTC. At 12:10:25 UTC the bridge confirms that exact loaded MVID, with
native following inactive, RSR Off and settings Ready. Live gameplay acceptance
remains open. The captured failures above belong to the preceding MVID, not this
repair; the completed dungeon does not validate it.

The source comparison also exposes a separate parity omission: native mobility
capture lacks XCAI's damaging rotational gap closers and evidenced GCD/setup
movement chains. The non-damaging target-dash tests cannot certify those paths.
This remains tracked in TODO rather than being reported as completed migration.


## Retained Griffin action evidence and NPC repair runs

The second failed recording `c656244f1fa6ed2a581cf490` decodes as v31 with
248,039 operations, without truncation or parse/container errors. Its SHA-256 is
`1376fa49f174767dc847f03be96b7720fca3a3d09f4d258d07f7d60607a0d975`.
Recorded times are local clock values; live correlation establishes UTC = local
minus two hours for this run. The Griffin segment spans 12:04:52–12:09:08 UTC.
All 124 local action requests match an action effect by action ID/source sequence.
Of 73 GCDs, 24 are Piercing Talon and 49 are melee GCDs. The largest GCD gaps are
29.613 and 24.972 seconds. Three native untargetable intervals last about four
seconds each; those flags do not explain either long gap. Mechanic timing still
needs checking before classifying every second of those gaps as avoidable.
Do not measure range from stale cached targeting coordinates.

The next authorized one-run AutoDuty queue used the saved **Support** mode,
three NPC buddies and one local player, with optional non-duty chores suppressed.
The repaired build `e4db215e-a364-47ce-ab24-b7c5347577b9` produced complete pack
captures `ac08988ad5ea44bb8c36e11d478bbe40` (215 frames) and
`765b2df3a3bb468db9fd35a85113c30e` (196 frames). The latter has no sampled
`native recovery route changed` rejection, versus 294/312 in the failed pack.
This supports the starting-cell repair; it does not establish full combat parity.
The client crashed during the run and was restarted by the maintainer. The faulting
stack was in Una/Umbra drawing. This does not prove the absence of an earlier
memory fault. Live replay-frame reconstruction was stopped; the resumed test and
its partial captures must not be described as an uninterrupted completed run.
The resumed Griffin capture is `d4adb5f46da843b781934f50ccb74750`, 880 samples,
12:41:06–12:44:51 UTC, on that same build.

### Movement continuity and proactive healing repairs

- An Any-positional attack does not depend on target rotation. Actual positional
  and tank-facing geometry still binds. A timing/pose-only refresh may let the
  one worker finish, but cannot publish any result without full exact validation
  against current safety, recipients, resources and geometry.
- A newly committed worker result still needs both original ages below 250 ms.
  An existing route is a proposal whose complete remainder is re-proved on a
  fresh native snapshot every frame. Current proof age remains below 250 ms;
  original stage/departure/action times are retained and never renewed. A stale
  result cannot become newly committed by claiming this continuation path.
- Normal cast delivery releases ordinary walking. Its animation lock still
  delays another action; a cast or displacement lock is not removed. Required
  action/effect observations remain binding.
- Consecutive walking states permit exact native lookahead. Whole-route validation
  rejects unsafe chords and retains required detours, casts, waits and mobility.
- Configured healing demand is captured with RSR's original range-independent
  need/HoT predicates, its saved global and per-action percentages, saved action
  opt-outs and original basic availability predicates. Frozen party identities
  and observed supported healing geometry enter the same search. No synthetic
  heal is submitted or credited as delivered. Each recipient stays explicit;
  eligible healing categories are alternatives. Target labels alone are not
  healing semantics: Diagnosis can use BeAttacked and radial heals can use Self.
  The native catalogue distinguishes actual heals from support links/buffs.
  Ground/target-centered/pet and custom job-selection cases need explicit live
  coverage; do not infer their acceptance from this direct/radial adapter.
- Native party engagement can authorize assistance while the local combat flag
  is absent, but only for an already-engaged eligible enemy targeting a native
  party/Duty Support member. Unrelated NPC fights and unpulled enemies do not
  authorize a pull. This restores an omitted archived XCAI engagement condition.

At 12:49:58 UTC a fresh out-of-combat preflight allowed watched-output deployment.
The loaded MVID `764c3165-38ad-4a96-99fe-31e085e1e8ac` was verified before
switching saved gearset 1 to Sage. Artifact SHA-256:
`a6ec6fd891f257b1c090dbfd0d4f2c3e4ac5ac6d7ad46c810fe8841e1c10a95d`.
That checkpoint passed 42,943 native assertions and installed RSR contracts.
One Support run was queued at 12:50:46 UTC. It contains the first healing-adapter
version; the corrected action catalogue, walking lookahead and party-engagement
fixes were added afterward and are not validated by that run. Later artifacts
are isolated until a new safe watcher preflight. The public release is unchanged.


The Sage queue returned out of duty by 13:16:31 UTC, and AutoDuty reported stopped
at 13:17:53 UTC. Its full final-boss diagnostic segment is
`23ab044e649d4f54b6c0fabf9820f298`, revision
`0892606b1c2b4faab5ad296810f8c930`, 895 samples,
13:11:22–13:15:11 UTC, without a partial start, on MVID `764c3165-...` above.
Dungeon completion is not acceptance of healing-range behavior, action delivery,
mechanic safety or smooth movement. During that older-build test, live checks at
13:06:10–13:06:43 showed the local combat flag absent while a native Duty Support
tank and its targeted enemies were still fighting; the native targeting authority
source gated on the local flag alone. This motivates the party-engagement repair,
without attributing every pause to that one gate.

A later isolated Release artifact built successfully with the corrected healing
catalogue, walk lookahead and party-engagement code: MVID
`8a5adcc9-365e-4e5b-b046-de81f0a91cc8`, SHA-256
`85405c873afe734bb5cec2623380a1e6caae060e0505388ba96f46c8a2becad6`.
Existing compiled native checks pass 42,946 assertions against it and installed
RSR contracts pass. New party-engagement test source and the final diagnostic
reset still await the next managed build/rerun. This artifact has not been
written to the watched output; do not report it as the loaded test build.


Final managed verification for this repair checkpoint: Release build succeeds
with zero errors and six existing job-gauge warnings; all **42,950 native
assertions**, installed RSR contracts and nine repository checks pass. This
includes the new party-engagement fixture and final healing diagnostic reset.
MVID `4dea0f76-7d79-4dd5-a939-2070b981d9ab`; SHA-256
`c2aa5338767ec79dc68a40c0b1a241fba3f20bc9cb6131ebcfaf9fdcf151d2a4`.
The watched development output was written at 13:26:40 UTC after a fresh
out-of-combat preflight. Reload provenance and subsequent gameplay are separate
checks. Damaging mobility/setup chains and physical-ranged/caster acceptance
remain open. No public publication was made by this repair pass.

Reload of the final checkpoint was verified at 13:27:11 UTC with exactly the
above MVID, settings Ready, native following inactive and RSR Off. Saved gearset
7 was equipped and its current-set/job identity re-read as Machinist. With no
player party and the saved Support mode reverified, one NPC-only Mistwake run
was requested at 13:27:55 UTC. Current combat capture was armed before entry.
This is a test start, not a successful-run or gameplay-acceptance claim.


The Machinist queue's opening is excluded from assistance acceptance: after the
previous run's explicit cleanup, AutoDuty started route navigation while native
following and RSR remained inactive. Navigation was stopped between pulls at
13:31:33 UTC. The plugin command tester's out-of-combat guard was retained;
activation waited for the NPC pack to finish. Native `/bmrai on` was then issued
through the registered development-command tool, with fresh state and both tokens.
Following/linked RSR activation must be confirmed before route resumption. This
is test orchestration evidence, not a new plugin activation policy or permission
to start automation merely because the integrated version was installed.

Native following and linked RSR Henched mode were confirmed before
`AutoDuty.Start(false)` resumed the current route at 13:34:36 UTC. Navigation then
reported active. The resumed Machinist segment uses MVID `4dea0f76-...` above;
its pre-activation opening remains excluded. The active repair goal is not
complete; rotational mobility/setup implementation and role-specific acceptance
remain in TODO.md.

The initial `Start(false)` continuation did not advance the route. AutoDuty's
Stop implementation clears Indexer to -1; its Start(false) preserves that sentinel,
while StageReadingPath refuses to advance -1. The supported Start(true) was issued
out of combat to reset the existing duty path. This can revisit already cleared
waypoints; it does not queue another party. Exclude the stalled interval from
movement/uptime comparisons and verify actual position advancement after restart.


### Machinist close-follow report and ready rotational mobility

The maintainer observed Machinist repeatedly staying in melee range. The saved
native follow-target maximum is 3y; its generic goal outranked the separately
captured 25y range-recovery objective. At 14:02:51 UTC the selected action was
16498, a 25y single-target attack, with no reason in that action to approach
melee. This source conflict explains unnecessary inward following; it does not
claim every close position was wrong. Short-range pack actions remain useful.

During active native RSR assistance, the identified follow-target goal now uses
job reach. Exact current-action, recipient, assignment and hazard requirements
remain independently enforced. This is a maximum useful reach, not a new minimum
or a maximum-range retreat. The shared native `UseCombatPositioningRange` option
defaults on; disabling it explicitly selects the existing fixed follow distance,
whose saved value is preserved. Native rotations, presets, leader following and
assistance opt-outs retain their prior path.

The same development checkpoint adds ready Forked Raiju, Primal Rend, Crimson
Cyclone, Dragonfire Dive, Stardiver, J Kick, instant Flying Frenzy and instant
Loom to native mobility capture. A selected moving attack is delivered once in
the mobility transition, preserving its recipients and shared GCD clock. Scoped
RSR delivery ownership keeps its selection observable and leaves explicit/manual
actions with their original owner. Successful submitted flight is observed
through its effect and actual landing; consumed charges/procs cannot cause a
second submission while the native action lock is active. This pending state
does not publish a new safety proof. Setup chains, equivalent moving-action
replacement and proc-expiry forecasting remain open; ready-action support is
not full archived XCAI rotational parity.

Managed Release build: zero errors, six existing gauge warnings. Native suite:
42,979 assertions passed, including ranged hold versus fixed close-follow,
short-range approach, obstruction rejection, shared GCD timing, normal contact
damage, exact landing and manual handoff. Installed RSR contract checks and all
nine repository tests pass. Artifact MVID
`eb746a24-6184-4abe-999b-2b5383c91f2e`, SHA-256
`07af1e6370dc703abec68e43f83b168dad49b9290f689dfce2cd439f67b9a7e7`.
These are offline results; deployment and gameplay acceptance are separate.
The preceding Machinist run exited duty by 14:12:40 UTC on the older
`4dea0f76-...` artifact and cannot validate this repair.

The tested artifact was written to the watched development output after a fresh
idle/out-of-combat preflight at 14:14:31 UTC; the loaded MVID was verified at
14:15:00 UTC. Settings were Ready, following inactive and RSR Off. Native
`/bmrai on` was tested through the registered development command at 14:15:20;
following and linked RSR Henched mode were verified before requesting one
additional Support-mode Machinist Mistwake run at 14:15:33 UTC. This avoids the
previous run's inactive-control opening. Capture was armed for comparable range
behavior. Request acceptance and loaded provenance do not establish gameplay
acceptance. No public release was made.

First new-build boss samples: at 14:21:12 UTC Machinist was approximately 5.92y
beyond the target/player hitboxes while selecting a 25y target-circle action.
At 14:21:32 it moved closer through native `UnsafeImprove` recovery. These
samples demonstrate that close positions can also come from mechanic recovery;
they do not establish whole-fight spacing acceptance. Earlier pack samples
retained Scattergun's real 12y cone. The replay/capture comparison remains open.

A final source guard limits rotational delivery ownership to an actual normal or
follow movement owner with an available ordinary movement forecast. Unsupported
forecast recovery and presets without a movement owner retain RSR delivery.
The Material field is labelled Fixed distance to target to clarify its switch.
This current-source Release artifact also builds with zero errors; the same
42,979 native assertions and installed RSR contract checks pass. MVID
`a6ff0948-7e58-4fca-bbdd-78fdc82e5151`, SHA-256
`88b42138ba2461e6437af6f6edd1db29bee4aec9f5f36348a9a9ce69dff3cb98`.
It remains isolated while the Machinist run continues on the verified
`eb746a24-...` build. No combat reload was performed.


### Explicit Fleeting/Forked Raiju equivalence

The next source checkpoint permits a selected Fleeting Raiju opportunity to be
delivered by an actually available Forked Raiju on the same target. The mapping
is explicit and single-target: exact recipients, target identity, geometry,
recast and native mobility ownership remain binding. It cannot substitute a
dash for an unrelated attack or heal. Walking to Fleeting and moving with Forked
compete in the same search, so ordinary Fleeting wins when already in melee and
Forked can win when it delivers the current hit sooner. Both paths consume the
same captured Raiju resource. Diagnostics record the actual delivered action.
No additional movement writer or ranged-filler veto was introduced.

The managed current-tree Release build succeeds with zero errors and six existing
gauge warnings. All 42,985 native assertions pass, including comparison against
unpruned search, exact executable validation, same-proc consumption, normal melee
delivery and unavailable/disabled alternative handling. Installed RSR contracts
and all nine repository tests also pass. MVID
`288ec844-8fc8-4253-923d-845ec41d2c59`; SHA-256
`d7518aac91842a10836d4195a01ef8da3ce5a7548b02e94dd0fbde643e16b7cb`.
Setup chains, proc-expiry forecasts and live rotational delivery remain open.

The Machinist client process exited during the new run, after the first boss and
further packs. The last available log entry was 14:26:58 UTC. No new crash report
was found, so the cause of exit is not established. The maintainer restarted the
client, and the bridge reconnected with Machinist idle inside the duty and
AutoDuty stopped. Preserve this as an interrupted test, not a completed run.

The 42,985-assertion artifact was written to the watched output at 14:37:06 UTC
with a fresh out-of-combat preflight, and loaded MVID was verified at 14:39:30.
Native `/bmrai on` restored following and linked RSR Henched mode; both were
verified before resuming the existing Support duty through AutoDuty.Start(true).
The restarted client and reload form a new lifecycle segment. Machinist cannot
validate the new Ninja equivalence path; native synthetic/exhaustive checks and
its eventual job-specific gameplay acceptance remain distinct.


### Recorded Machinist first-boss spacing comparison

The completed earlier Machinist recording `0c382de7a3f4f002648b0e3c` has
SHA-256 `765a306f9f356363905fdc0cb320aff4b02fa8bb9ac3638862c4a2e0f02d2b67`.
Its Treno window is 13:54:24.887–13:56:51.172 UTC, after native AI/RSR activation.
The new recording `644d03fe9109a0ebc2c313d1` has SHA-256
`d301f856cc2b1fcc4908f8e57bac900ff513e8b273da3ca5046b58d6057bc71b`;
its full Treno window is 14:20:07.033–14:22:43.494 UTC. That recording has a later
incomplete compressed tail at 14:27:01, not an incomplete first-boss interval.

Using each actor's latest recorded position at 250ms intervals and subtracting
both hitboxes, time within 3y falls from 75.4% (586 samples) to 34.3% (626).
Median hitbox distance increases from 1.85y to 8.23y. All 112 old and 125 new
local requests match player action effects by action ID/source sequence. The
windows contain 67 and 72 native MCH GCDs respectively: 0.458 and 0.460 per
second. Maximum request gaps are 2.624 and 3.981 seconds; neither has a gap over
four seconds. This supports the specific unnecessary close-follow repair while
retaining attack delivery. It is not mechanic-normalized, potency/DPS acceptance
or whole-role safety parity. See `machinist-range-2026-10-24.json`.

The resumed Machinist duty returned to territory339 by 14:55:19 UTC with
AutoDuty stopped, on loaded MVID `288ec844-...`. This was a restart/resume, not
an uninterrupted full-run acceptance claim. Following and RSR Henched remained
active. Saved gearset14 was equipped and its current-set/job identity verified
as level100 Black Mage before requesting one additional Support Mistwake run.
The caster test uses the same verified development artifact; no public release
was made. Current casting, Ley Lines and departure behavior still require
scenario evidence.

The resumed MCH final-boss capture is `9bf83cae1049413f8caad73e14f3b17f`,
revision `1cbe83d3489b42aa8635114e1120d0b3`: 879 samples spanning
14:51:13–14:54:59 UTC, without a partial start, on MVID `288ec844-...`.
Black Mage's one-run Support queue was requested at 14:56:19 UTC after current
gearset14/job/level and native AI/RSR controls were verified.

The caster run exposed an artificial normal-owner-only Ley Lines restriction.
The follow-owner/lifecycle/selection repair and its isolated current-source
artifact are documented in `ley-lines-placement.md` (42,989 assertions, MVID
`d0045773-32a8-4f88-97f1-3f1ed888ec25`, SHA-256
`d58483ce95a580610ea029bebc94fc90d9604a40bb70b226c3230306884a0744`).
The caster run still uses `288ec844-...`; no watched reload occurred in combat.

### Caster completion, self-only actions and selected setup resources

The Black Mage Support run completed and returned to territory339 before the
15:29:36 UTC observation. Final-boss capture `97fd2bdbe0324ff7af811cb05fb14842`,
revision `be1b92e3b7e144ad9c8c6f0cdc724182`, contains all 829 samples from
15:15:59–15:19:31 UTC, without a partial start or dropped prefix. It ran loaded
MVID `288ec844-8fc8-4253-923d-845ec41d2c59`. Its full recording is
`1f024d4b3cb35670a21a7eb3`, SHA-256
`f5b9991935ac2c87e61f07b3af25c1325e714a7472d43278b44ec11c45a9acd4`:
236,871 decoded operations, no parse error or truncated tail. Recorded timestamps
are local UTC+2 in this run; bridge sample timestamps are UTC.

All 829 final-boss samples were inspected for capture failure. There are 28
`Incomplete executed action geometry` failures in two windows, 15:17:12.420–
15:17:15.779 and 15:18:41.870–15:18:45.187 UTC. Both align with Transpose then
Umbral Soul requests. RSR's `IsFriendly` category is false for this self-only buff;
the former capture excluded the player from its recipients and threw. Native
zero-range, zero-radius, self-targeted actions now use an explicit Self geometry.
It follows the caster, preserves its sole recipient and imposes no hostile target
facing/positional condition. The recorded Umbral Soul case and Inner Release are
covered by native tests. This is a source-supported repair, pending a repaired
intermission observation. The 114 local final-boss requests have 112 matched
player effects by action ID/source sequence; Flare Star sequence1090 and FireIV
sequence1142 have no matching delivery. Completion is not cast-uptime acceptance.

Selected setup actions can now grant a separate bounded proc resource after
delivery: Raiton→Raiju, InnerRelease→Rend, SummonIfrit→Cyclone and
Geirskogul→Stardiver. A grant does not reset the movement action's observed
cooldown or replenish with elapsed time. Search states retain delivered action
dependencies, resources, action lock and GCD clock; validation replays them and
rechecks newly selected moving-attack recipients even for an earlier unsourced
mobility stage. Real native status, adjusted action and availability still bind
submission. Ready proc/gauge expiry is captured as a countdown and checked before
submission. Synthetic checks compare conditional routes against unpruned search
and reject missing grants, impossible setups, expired procs and lost recipients.

The official [Ninja guide](https://na.finalfantasyxiv.com/jobguide/ninja/) and
[Warrior guide](https://na.finalfantasyxiv.com/jobguide/warrior/) specify 30-second
Raiju/Rend grants; the [Dragoon guide](https://na.finalfantasyxiv.com/jobguide/dragoon/)
specifies 20-second Life of the Dragon. These exceed the retained 12-second native
forecast horizon for a newly delivered setup. The
[Summoner guide](https://na.finalfantasyxiv.com/jobguide/summoner/) identifies
Ifrit's Favor and its action-dependent removal; no timer is invented for it.
Current observations supersede predicted effects after setup. Full mudras,
productive fallback setup replacement and planned-GCD offering remain open.

Current managed artifact: MVID `e68d9e13-a0c5-4028-a057-83980c2f2d6f`, SHA-256
`4c999d44e8b3b725e76c264469669c1c27c88cacc85454fbe0060fbfae0ae7d5`.
43,001 native assertions, installed RSR contracts and all nine repository tests
pass. It includes the Ley Lines follow/lifecycle repair. A fresh 15:37:44 UTC
preflight showed the sole client on Black Mage, normal conditions and out of
combat; the watched DLL was written at 15:37:51. Loaded-provenance confirmation
and new gameplay checks are tracked separately. No public release was made.

Automatic reload of MVID `e68d9e13-...` was confirmed at 15:38:30 UTC. Native
`/bmrai on` restored following and linked RSR Henched, verified before another
one-run Support queue at 15:39:11 UTC. The first-boss capture
`2cfab12e00f94e51bae45b5255fa1a21`, revision
`6360aaee01c34c89b58498ddd5b6ba3d`, spans 15:43:57–15:46:29 (590 samples,
no partial start). It has no geometry exceptions. Four ordinary route/action
invalidations invoke fresh native recovery. A real Ley Lines field is present in
75 samples and the placement selector is active under follow ownership. This
does not establish an optimized placement detour or full caster acceptance.

Planned movement GCDs which RSR has not already selected are now offered through
its original general-GCD extension slot, scoped to the original update callback.
Existing duty/support choices run first; RSR's own emergency/healing decisions
precede this slot. Its original CanUse, saved enable flag, adjusted identity and
actual target must approve the requested action. Approval belongs to one exact
plan/stage and expires at the native 250ms freshness ceiling. Explicit commands
retain original delivery. A declined candidate returns to other legal routes;
a mandatory native departure cannot wait for approval. No manual command queue,
second callback or independent movement writer is added. Ordinary originally
selected movement attacks remain observable and executable without a second
selection handshake. Native binding/rejection/staleness/departure tests pass;
actual moving-GCD selection and delivery still need job-specific live evidence.

The selection-stage checkpoint builds as MVID
`5148970b-add1-4287-95ab-1938bbf5db64`, SHA-256
`0e8fd9d7c6eb5baa1006aca25878121e146ab6f1ec8fba4b58475e4caf11520a`.
43,006 native assertions, installed RSR contracts and all nine repository tests
pass against that artifact. It is isolated while the ongoing Black Mage run uses
`e68d9e13-...`; no reload is attempted during its combat. The five added assertions
cover an original selected attack, pending offering, per-candidate refusal,
exact-plan binding, stale approval and mandatory-departure behavior.

The repaired Black Mage Support run completed and exited to territory339 by
16:02:38 UTC. Its final-boss capture `d0617dee7ff849fca9a1d756f380d7a8`, revision
`5124f51fe36d48008a0ecac001bffa74`, contains all 846 samples from
15:58:29–16:02:07, without a partial start. All samples retain following. There
are no geometry exceptions; 14 samples from 15:59:42.475–15:59:45.865 explicitly
capture Umbral Soul as Self geometry, including continued native walking. This
exercises the original intermission failure on the repaired artifact. Two other
fresh safety invalidations (`current lock no longer clears native deadline` and
`final position needs a different assignment or onward escape`) use native
recovery; they are separate from the repaired capture exception. Whole caster
uptime, placement-detour and server-snapshot acceptance remain distinct.

Selected Raiton can now forecast its conditional Raiju after completing the active
mudra, instead of the former blanket mudra filter discarding that continuation.
The live mudra expiry constrains Raiton's opportunity. All unrelated mobility and
utility actions remain excluded while mudras are active; an existing Raiju stack
cannot authorize breaking the sequence before Raiton. These changes do not yet
start a full mudra preparation from Throwing Dagger. Bounded current timing samples
now include the relevant Ninja, Warrior, Summoner and Dragoon setup/movement actions
to verify their native clocks and actual effects, without an arbitrary inspection
API or plugin-owned history.

This checkpoint builds as MVID `bc071145-30b3-4249-a830-4c634aeb766c`, SHA-256
`9491d80544a6d2a91d0d997d4476ad1cca548bb885babc600cf910562053c184`.
43,008 native assertions, installed RSR contracts and nine repository tests pass.
The sole client was normal/out of combat at 16:05:05 UTC; its watched DLL was
written at 16:05:14. Saved gearset11/current Ninja100 was independently verified
for a bounded idle timing observation before the next NPC run. Loaded provenance
must be confirmed separately before attributing new gameplay to this artifact.

### Ninja gear prerequisite and shared mudra clock

The maintainer explicitly clarified that Ninja's earlier pause required a gear
change. It was not a planner, movement, rotation or lifecycle failure. Exclude that
interval from fault counts. Gearset11/Ninja100 was verified before the idle timing
observation on loaded MVID `bc071145-30b3-4249-a830-4c634aeb766c` at 16:06:04 UTC.
Ten2259 exposes a 20,000ms primary charge clock; the native follow-up Ten18805 and
Chi18806 expose the 500ms shared spell clock. Raiton2267 exposes 1,500ms; Forked25777
and Fleeting25778 expose 2,112ms adjusted (2,500ms base). Capture now uses the
follow-up clock for first mudras instead of treating the charge cooldown as a GCD.
The isolated checkpoint `998a99d6-1d47-4807-9dd8-96a52039955c` passed 43,010 native
assertions including a synthetic Ten → Chi → Raiton → Raiju clock sequence. That
fixture establishes timing, not complete native mudra preparation implementation.

### Dragoon recovery evidence and bounded melee delivery reservation

The Support run queued at 16:07:22 UTC used loaded MVID `bc071145-...`, completed
its final encounter at 16:31:31 UTC and returned to territory339. First-boss capture
`dee779ae4c354e709af8b594f288acc2`, revision
`83b2f197c94e40e1945f2836eeb48a2d`, contains 652 samples from
16:12:02–16:14:48 UTC with no partial start. Recording
`f39919d372c38d3a6cfcf4af`, fixed-prefix SHA-256
`3c2a5ff5ee2ddf823f6e3f1a4e33402b627ff1ef51cc433704f4c01414fb`,
contains 108 first-boss player requests, all matched to player effects using action
ID and source sequence after paging all 165 effect rows. The inspected compressed
prefix ends later with an incomplete active tail; it contains the complete first
boss, not the complete dungeon. No player death appears in that inspected prefix.

Four requests delivered Piercing Talon90 at 16:12:34.902, 16:13:12.385,
16:13:52.458 and 16:14:40.258 UTC. The latter three sampled windows show native
SafeMaxPrio recovery with approximately 0.94, 0.67 and 0.92 seconds to its goal,
followed by short enhanced walks and melee action selection. The first window
uses UnsafeImprove egress; strict elective route certification may reject it.
Do not weaken hazard requirements to count that interval as an elective safe plan.
Dragonfire Dive and Stardiver are delivered in the first boss. No Winged Glide
request is present; its absence alone does not establish the reason or eligibility.

The new delivery guard compares an exact freshly revalidated complete melee route
against the ranged filler's observed ready/recovery clock. It supports enhanced
routes and complete unrelaxed native witnesses through the existing shared native
incumbent validation. It uses retimed remaining traversal, not an old duration
minus age, and cannot extend its original observed GCD deadline across repeated
frames. Changed target, hazards, manual authority or explicit RSR commands release
it. A slow/unverified route retains ordinary ranged delivery. This adds no separate
movement writer, blanket ranged-action prohibition or renewed transport lease.

Final-boss capture `69ac4101f20941ada39547287ee00b35`, revision
`0225cf65f21649cd868903f3188e443c`, contains all 931 samples from
16:27:30–16:31:31 UTC. All retain following. No geometry exception appears. Four
fresh route/action/anchor changes are recorded; 720 samples have bounded plans,
196 native recovery and 15 no plan. These diagnostic counts do not prove mechanic
safety, damage avoidance or uptime parity.

The new checkpoint builds as MVID `b61fd090-e186-4f73-9bda-52e0ed22249c`, SHA-256
`e575c8ec83e0c04c05dd2d79921e012958662120bb46bd3380adc28832f16131`.
43,018 native assertions, installed RSR contracts and nine repository tests pass.
It includes the shared mudra-clock correction. Deployment and post-reload action
delivery are separate evidence; this paragraph does not claim either.

The sole affected client was normal and out of combat at 16:38:45 UTC. The watched
DLL was written afterward and its exact hash verified. Automatic reload of MVID
`b61fd090-e186-4f73-9bda-52e0ed22249c` was confirmed at 16:39:08 UTC; settings
were Ready. Native `/bmrai on` restored following and linked RSR Henched, both
verified at 16:39:21 UTC. Another one-run NPC-only Support comparison was then
requested with the saved Support mode. No public release was made.

At 16:42:06 UTC the new run is active on Dragoon with following and RSR Henched.
The delivery scope reports a verified melee-recovery reservation has occurred.
The current selection is a four-recipient Draconian Fury25770 opportunity; there
is no current diagnostic failure. This is initial scope activity, not the exact
first-boss request/effect comparison or proof that every avoidable filler is gone.

## 17:48 UTC repair checkpoint — preserve XCAI behavior before native preferences

The 16:39 Dragoon run completed. First-boss capture
`113ba9b4d7c44d64818bd8ffc75e3f1d`, revision
`45f6543532af4458802c021720c25edc`, covers 16:44:15–16:47:05 UTC.
All 107 player action requests matched effects by action and source sequence,
including four Piercing Talons, one Dragonfire Dive and two Stardivers. There was
no Winged Glide request. Two fillers occurred while arrived at a shelter position;
two during approximately one-second re-entry routes. The reservation alone did
not eliminate the defect. Counts do not identify every candidate's legality or
justify weakening the shelter mechanic.

The next source checkpoint adds:

- Complete optional preparation sequences (Ninja Ten/Chi/Raiton, Warrior Inner
  Release and Dragoon Geirskogul), with actual native resource/proc clocks.
  Approval does not advance the sequence; observed action effects do. Once a
  sequence starts its remaining actions are committed, retain their target and
  cannot be displaced by an unrelated mudra-breaking action. Delivery remains
  through the original RSR selection slots and preserves saved action opt-outs.
- Known readiness contact along the complete walking/waiting/escape route,
  rather than only at its final permanent destination. No subsequent attack or
  unknown hazard expiry is invented. A delayed native witness is independently
  re-proved on the current strict map. Native recovery with a relaxed source can
  seed optimization only if that strict re-proof succeeds. Synthetic warning
  workload: 1,600 cells, 0.216 ms, one expansion, bounded best found result.
  This is neither a completed-search claim nor live frame-time acceptance.
- Shared Swiftcast consumption and expiry for BLU casted mobility; one observed
  instant-cast effect cannot authorize two different movement spells.
- Current usable non-moving RSR abilities retain their original opportunity when
  departure permits; elective movement also uses the original ability slot.
  A materially changed planning problem may form a new revalidated melee
  reservation, while equivalent frames cannot renew an expired reservation.
- Corrected gap eligibility: archived XCAI uptime checks passed a zero minimum;
  its eight-yalm preference belonged to friendly escape anchors. The conversion
  had applied that preference to all ordinary mobility, discarding short hostile
  dashes before comparing execution with walking. The captured minimum now belongs
  to each friendly anchor. Hostile, directional, ground and return candidates
  retain their original zero minimum, subject to unchanged native legality and
  complete-route validation. A six-yalm hostile dash restoring the melee window
  is covered through search and exact execution validation. Friendly anchors
  retain the saved minimum. No saved configuration was edited.

Direct legacy/integrated configuration comparison found 95 common saved fields.
The only differing simple value was the restoration-journal generation. This
supports retained values, not behavioral parity: applying a setting to a broader
policy than XCAI remains a migration defect even when the value itself imports.

Managed Release build: zero errors, six existing obsolete gauge warnings.
43,043 native assertions, installed RSR contracts and nine repository tests pass.
MVID `ef0ae6d1-5624-4dd2-abd5-3546f27563c3`, SHA-256
`f4664946ff0cc07912511fb53f11619c278d8b51addb87b2ea48bb58dc802f53`.
The sole affected client was normal and out of combat at 17:48:00 UTC; the watched
development output was written afterward and its exact hash verified. Loaded
MVID was verified at 17:48:43 UTC. Previously active following and linked RSR
Henched were restored through the native command and verified at 17:49:21 UTC;
settings Ready, no current diagnostic failure. The combat diagnostic lease was
renewed for post-reload evidence. Post-reload gameplay remains unverified.
Public release unchanged.

The Sage Support run on the preceding `b61fd090` artifact completed by 17:36 UTC.
Its diagnostic lease had expired in the first half and was renewed at 17:23:51
UTC. The saved replay remains available, but the missing diagnostic interval
precludes a complete current-state history claim. Its completion does not accept
healing coverage or healing delivery on the newer artifact.

The follow-up retains XCAI's exception for short friendly uptime relays as well:
an anchor below the saved escape minimum remains eligible when its landing
improves evidenced melee access, configured healing access or a current action's
required execution geometry. This only admits the candidate; the complete joint
comparison and execution revalidation still decide it. Pure optional short ally
escape retains the configured minimum. Managed compilation, 43,044 native
assertions, installed RSR contracts and nine repository tests pass.
SHA-256 `9fd21cb3fcc01eda17f9cfd070dc386b5dfd6b66390402a2e829ac828580b38a`,
MVID `08328560-1533-4cbb-8f5b-eb951d344414`.

The maintainer explicitly instructed immediate reload during the NPC dungeon.
The watched DLL was therefore refreshed during combat, superseding the usual
out-of-combat staging for this requested experiment. Loaded MVID was verified
at 17:54:09 UTC. Following and linked RSR Henched were observed active again at
17:55:42 UTC. The first boss is now running on this artifact; the run's earlier
trash segments span the reload and cannot be represented as one artifact.

Sage final-boss capture `40e6bea3927a48d3a5ef39e160982e89`, revision
`19e06042ed8c4132b41d474c104c3887`, retains all 906 samples from
17:32:25–17:36:24 UTC, on the preceding `b61fd090` artifact. Configured healing
requirements occur in 61 planned samples; every explicitly required recipient
is covered by that plan's final destination. Five samples invalidate on changed
actions/targets/recipients. These are final-destination coverage observations,
not proof of actual arrival, heal delivery or current-artifact healer acceptance.
The corresponding bounded replay event window at recorded 19:32:48–19:33:08
(UTC 17:32:48–17:33:08), recording `2ce6ae998306411b1bc3b4da`, decoded completely
through its window end. At 19:32:52.686, Eukrasian Prognosis II37034 reports healing
effects on the player and all three Support NPCs. At 19:32:53.246, Pepsis24301
also reports healing effects on all four, with observed values 27,804, 44,703,
28,114 and 27,001. These delivered effects corroborate actual four-person access
after the configured need at 17:32:49.693. They do not establish a previously
out-of-range recovery, every later healing window or the newer artifact. The
event cache was closed afterward; no full replay or module playback was opened.

## Verified implementation handoff after maintainer feedback

The maintainer reports that the loaded `08328560` build feels substantially more
like XCAI. Residual micro-stepping is explicitly deferred for improvement over
time. The maintainer subsequently clarified that deferral means diagnose its
cause and then fix it; it remains an open defect, not accepted behavior.
Preserve this artifact during the current run. The later quality goal and
authorized NPC validation remain open; the earlier implementation-only handoff
condition is not evidence that this quality goal has been achieved.

The current first-boss capture is `b7e0c408e24d42f4a0df186ac8751459`, revision
`63bbb4c3b6044e939ae4229b9b2ace8d`, 17:55:35–17:58:22 UTC, 654 samples, no
partial start. Every sample reports the new MVID and following enabled. It
contains 526 bounded plans, 117 native recovery and 11 no-plan samples. Ten
samples record current route/action/assignment/lock revalidation failures.
These failures cause reconsideration, not permission to execute stale movement.
Forty-five samples select Piercing Talon, but selection is not delivery: no claim
that all avoidable fillers disappeared is supported by that count. The active
recording had not flushed a readable fixed prefix at this checkpoint; exact
request/effect and injury review remains part of gameplay acceptance.

Implementation handoff audit:

| Requirement | Current evidence | Remaining acceptance |
| --- | --- | --- |
| Target acquisition, manual/preset authority | Native target bootstrap and original predicate tests; pack observations in dated runs | Further target transitions and explicit handoff gameplay |
| Active mechanics and changed hazards | Fresh native recovery, blocked-worker egress, strict traversal/deadline regressions | Exact mechanic hits/last-safe timing in comparable runs |
| Walking, Sprint, gap closers | Same search/executor, native resource/timing capture, small-map reference and short-hostile/friendly-relay tests | Per-action delivery and saved opt-outs across roles |
| Productive setups and rotational mobility | All eight archived moving-action families; effect-observed Ninja sequence and WAR/DRG setups; original RSR slots | Actual preparation/proc delivery in new gameplay |
| Melee contact and movement stability | Readiness contact with complete onward route, bounded filler reservation, True North positional tests | Exact avoidable downtime; micro-stepping deferred by maintainer |
| Configured healing need | Range-independent original RSR need, explicit recipients, native tests; Sage four-person heal effects | Out-of-range recovery and broader healing scenarios |
| Physical ranged and caster | MCH spacing comparison, BLM repaired Umbral Soul run, field ownership/timing tests and bounded checks | New-build job-specific combat/field delivery |
| Build, deployment, preservation | Managed current-source build, 43,044 native assertions, installed RSR contracts, nine repository tests, exact watched hash and loaded MVID | No public repair release; no broad parity claim |

The one-run NPC Support duty continues with the current artifact. Preserve its
behavior while reviewing it; broader quality acceptance remains open. Focus any
additional NPC validation on missing evidence, and make source repairs for
concrete material failures rather than speculative expansion. Micro-stepping
remains an open defect to diagnose and fix. No public publication is authorized
by this checkpoint.

Subsequent read-only review recovered a fixed active-recording prefix for the
new first boss, recording `c901337ead0c88ba93c46030`, SHA-256
`41f2e94aec64c5272e149cd95f50ab8772d1334e329325e7b027cc6355675562`.
The two bounded windows cover recorded 19:55:35–19:58:24. All 108 local requests
match action effects by action ID and source sequence. This includes two
Stardivers, one Dragonfire Dive, one Elusive Jump and six Piercing Talons; no
Winged Glide request is present. Sprint is delivered after the boss ends.
The copied active compressed container reports an incomplete later tail;
selected events were observed through both requested window ends without a
parse error. Recheck the finalized recording before making a completeness claim.
Six delivered fillers do not meet an avoidable-downtime criterion by themselves:
exact candidate legality, departure timing and execution windows still need
classification. Do not describe this as eliminated filler or gap-closer parity.

Micro-stepping investigation: 654 diagnostic samples contain 35 sampled walking
episodes; five have less than one yalm of before/after displacement. Four begin
with `native goal field` as the input-change reason; one follows changed action
geometry. At 17:56:48 UTC, a small resumed walk follows a changed native field
and an origin priority of float.MinValue. That can involve changing warning
cells as well as position preferences. The observations identify where to
investigate; they do not yet prove a cosmetic tolerance defect. Do not suppress
small required mechanic movement with a blanket distance threshold.

Caster evidence review: the repaired BLM recording
`930152bc8e7c84257dbddb16`, recorded 17:44:00–17:46:00, has 67 local requests,
63 matching delivered effects and four casts that end early without delivery
(three Fire IV and one Blizzard IV). At three inspected cancellation windows,
diagnostics change from waiting on a short native departure deadline to
UnsafeImprove recovery. This supports real interrupted casts, but not a claim
that canceling them was unnecessary. Review whether those spells should have
been offered an instant alternative before departure, using current-build
evidence; do not weaken safety or blame RSR's rotation for movement failures.
No module playback was initialized, and the full replay cache was closed.

## Griffin cross-room re-engagement — maintainer report and focused repair

The maintainer observed the boss jumping across warning mechanics while the
player stayed distant instead of gapping or deliberately turning for Elusive
Jump. Capture `c3bec147330c4a5b98603201298046a6`, revision
`4ce42dcee4d44c55b794540e8ea5d3d0`, retains 910 final-boss samples on MVID
`08328560`. At 18:14:09.697–18:14:14.814 UTC, player position remains
(286.02194, -627.9452), boss (290.97424, -602.6856): 25.74y center distance.
The executor repeatedly reports `fresh native departure permits further
optimization`, with no executable optimized publication. Input changes include
native goal field and utility timing; these do not by themselves prove which
individual search lost the useful mobility candidate.

Finalized recording `6ccc633d155e5d8c33dfb720`, bounded complete event window
20:14:08–20:14:16 local, confirms Piercing Talon requests at 20:14:09.376,
20:14:11.863 and 20:14:14.358. The reported stall is real, not only a sampled
selection label. The event cache was closed afterward.

Focused changes:

- Publish complete verified mobility alternatives from the initial opportunity
  search immediately, rather than keeping them private until that pass finishes
  comparing its other routes. Initial native walking seeds still await comparison.
- Include aimed directional/backstep and ground candidates toward combat reach
  in that first pass. For Elusive Jump the facing points away from the destination;
  the displacement goes toward it. The full map/facing search remains available.
- A fully re-proved native route that makes safe progress toward melee range can
  execute while optimization is pending. It no longer must reach the entire
  range objective in one native endpoint before any walking is permitted.

Regressions cover reverse-jumping through a warning strip when no targeted dash
is present, its exact facing/route validation, physical-wall rejection, early
publication of a useful targeted dash and partial native recovery progress.
The managed Release build passes with zero errors and six existing gauge
warnings; 43,049 native assertions, installed RSR contracts and nine repository
tests pass. SHA-256
`c2b1c431c2659b5c0b7b4c8f4e79b4b62af5baf53c49aaf13ad25f867dc46faf`,
MVID `39eb457d-5c78-404c-8ce5-f820fd26742a`.
The watched output was refreshed after a normal/out-of-combat check at
18:25:56 UTC. Exact loaded provenance and replaying this same Griffin transition
are separate checks. Exact MVID was confirmed loaded at 18:26:27 UTC; the native
activation command restored following and linked RSR after reload. No encounter
hazard, assignment, warning deadline, native
freshness ceiling or saved setting was weakened.

Offline reconstruction of the finalized recording also reproduces the exact
stationary position with ten Thunderbolt lanes and 5.4667472 seconds until their
authored activation (candidate-runner raw operation 209400; bridge and runner
operation numbering differ). The focused `GriffinWarningRecovery` regression
reuses those shapes, the native arena raster and a captured melee target. It
finds a complete reverse-jump/contact/escape route: the first verified useful
publication takes about 161 ms, while that first opportunity pass finishes at
about 537 ms. This demonstrates why retaining the route privately until the
pass ends can miss the native 250 ms freshness ceiling. The fixture does not
claim live physical collision, RSR availability or action delivery. All 43,050
native assertions pass; rebuilding the plugin yields the same DLL hash/MVID.

The isolated Winged Glide case also exposes expensive discovery of its required
onward escape. The initial finite/aimed mobility pass now asks the existing
frozen native ThetaStar search for a continuation from a temporary landing,
then re-proves every leg on the original strict map with actual elapsed time,
travel effects, execution rules and destination requirements. It supplies an
incumbent, not a new hazard model or a restriction on the full search. This is
disabled during full spatial enumeration and for displacement phases/teleporters.
The recorded ten-lane fixture's first complete mobility publication drops from
about 161 ms to 8.5 ms and selects Winged Glide plus one escape walk. Isolated
Winged Glide and reverse-jump cases publish in about 1.7 and 1.2 ms respectively;
both complete routes pass exact validation. All 43,052 assertions, installed RSR
contracts and nine repository tests pass. Managed Release build: zero errors,
six existing gauge warnings; MVID `edb1e3da-a981-4c38-8ae4-98e982019339`, SHA-256
`9181af8d21d2874864254cb65d29bead8ee2fe2ba04a612ca3088fc39bdaf5a5`.
The `39eb457d` comparison run completed; its final boss retains 900 diagnostic
samples in segment `e64895492dfc4df28cc09ed2e2fde39b`, 18:47:34–18:51:27 UTC.
This is not a live check of the subsequent fast-continuation repair. The new
DLL was written to the watched development output after a fresh normal/OOC
check at 18:52:04 UTC. Exact MVID, matching watched/artifact hashes, following,
RSR Henched and no current provider failure were confirmed at 18:52:54 UTC.
The client is idle in zone 339. No additional run was queued or release published.

### Exact-pose mobility handoff follow-up

A subsequent one-run Support comparison on `edb1e3da` still loses uptime at the
matched warning jump. Capture `ebd7bf7fadb142c69b105e88b7c75613`, revision
`0085cea89395471da88901d845a976be`, contains 991 final-boss frames. At
19:23:50.804–19:23:53.749 UTC it walks back into reach while Winged Glide remains
at two available charges. Rejections include `executable mobility anchor, facing,
landing or charges changed`. Finalized replay `150f98d5946e4f680a0ea83a`, complete
21:23:48–21:24:01 local window, shows GCD requests at 21:23:50.347 and
21:23:53.765: no Talon in that window, but a 3.418-second gap still loses uptime.
This run does not establish the requested mobility behavior.

An unsubmitted initial mobility control now resolves its exact current player
pose, same anchor and heading, reconnects its existing onward route, and passes
the full fresh validator before execution. It preserves input/action times,
manual authority, resources, recipients and strict hazard checks. In-flight
actions retain their submitted landing. Targeted-dash and reverse-jump tests
cover player/anchor motion, new physical obstructions, manual denial and pending
action effects. Managed build succeeds; 43,066 assertions, installed RSR contracts
and nine repository tests pass. SHA-256
`0d112a78be9004adc1bfc64278f22be0d3cf1905ec7564aa29b66e3eeeb80ff6`,
MVID `103f89d2-2066-4403-80c0-a042c13b7ec4`. Exact loaded build, following and RSR
Henched were confirmed at 19:32:11 UTC; a single NPC Support comparison was queued
at 19:32:11 UTC. Its result remains pending.

That Support run completed on `103f89d2`. Finalized replay
`a4579b8d4bb00f0dc4d1b7b8` has complete bounded windows covering the Griffin
fight, 21:55:47–22:00:15 local. Requests match action-specific effects for five
Winged Glides, three Elusive Jumps, three Stardivers and two Dragonfire Dives.
Sampled positions confirm useful melee landings followed by the planned walking
escape. The cross-room jump around 38 seconds regains contact in about 1.3 s,
versus roughly 3.4 s in the intermediate walking run. Three aimed Elusive Jumps
also visibly recover reach. Ninety captured Thunderbolt/Rush/Electrogenetic
Force/StormSurge events have empty hit lists; this is scoped effect evidence,
not complete encounter or all-job acceptance.

At the matched 128-second warning jump, live reads confirm Winged Glide and
Elusive Jump both have zero charges and status 582. That window is therefore
not a still-ready-dash failure. Talons remain in the recording and are not all
classified as unavoidable. The Sprint forecast had a separate defect: it held
walking for the utility animation lock. Speed utilities now release walking at
delivery, preserve their lock for subsequent actions and range-execution
readiness, and use an early fully verified native continuation. The recorded
ten-lane Sprint-only fixture publishes in about 2.6 ms. Running-position rebinding
also covers Sprint. Retrace/True North/immunity timing is preserved.

The scoped build passes 43,070 assertions, installed RSR contracts and nine
repository tests. SHA-256
`b35dec716aadbaacb0253fa2599769feac125a1bbb4c305ae4f5b8dd009b9dc9`,
MVID `32198288-ca35-4c06-aca7-81c55bdd6142`. It was reloaded OOC; loaded identity,
following and RSR Henched were verified at 20:23:56 UTC. One Support comparison
was queued at 20:23:56 UTC. Its result remains pending.

### Started escapes must not reverse for a new combat choice

The maintainer observed first-boss hide/return oscillation during the next run.
Capture `1906eba3f19c4bec8d30053693993987` on `32198288` covers the complete
20:29:46–20:32:47 UTC first boss. At 20:30:52.830 the player starts the selected
walk to shelter for Bedeviling Light. At 20:30:53.608 Winged Glide reverses that
escape, then the route walks to shelter again. Its range loss at the actual
next readiness window is still 3.04 yalms: no melee attack was recovered. The
earlier report of successful Griffin mobility does not excuse this reversal.

Two focused changes address it. Only complete attack-range access counts ahead
of resource preservation; partial proximity cannot buy the return dash. Once
the chosen attack window ends and the walking-only shelter suffix begins, that
complete escape is retained through the same native safety map. Rotation goals
and readiness updates cannot reverse it. Each remainder is re-proved against
fresh native geometry/deadlines and execution rules. Authored hazard removal,
new geometry, target/owner/manual changes or invalid remaining traversal release
the commitment. Reaching cover retains it while the same mechanic is pending.
This does not force an early departure or remove productive pre-departure attacks.

The early-publication regression fails on `32198288` and passes after repair.
Additional checks cover holding the chosen escape despite a new combat goal,
remaining in shelter, immediate release on authored clearance, new obstruction
and target identity, and expired deadlines. Productive dash/contact/escape,
Griffin's ten warnings, Sprint, Ninja preparations and Ley Lines regressions pass.
Managed Release build: zero errors, six existing obsolete-gauge warnings;
43,081 native assertions, installed RSR contract checks and nine Python tests pass.
SHA-256 `6d27051ea93bdb04c4a6320694c02b6a5dba4c8d955e2951aa5f871f4890a146`,
MVID `33a2f701-ded3-42f0-9c58-67d872fd1889`. Watched development output was updated
OOC at 20:45:47 UTC. Loaded identity, following and RSR Henched were verified at
20:46:08 UTC. This reload occurs after the reported first-boss hide; the live
recurrence check on that exact mechanic is still pending.

The same one-run Support comparison completed on `33a2f701`: Griffin lifecycle
20:47:42.490–20:52:08.055 UTC, full segment
`730143d2d44e440ab085fbf43de45f2b`, revision `8f6ebbc3e2d1442d9f7f52a675de980e`,
1,025 samples with no reported gap. Finalized replay `8acc4e5f0d4910d973c569a9`
has complete bounded windows 22:47:42–22:49:42, 22:49:42–22:51:42 and
22:51:42–22:52:12 local. Five Winged Glides, two Elusive Jumps, five Stardivers,
two Dragonfire Dives and two in-combat Sprints each have matching request/effect
sequences. Sprint effects occur at 22:49:02.384 and 22:51:11.781 local; the
250 ms stream shows one Sprint candidate and may miss short submission stages,
so individual request ownership is not inferred solely from those samples.

The first cross-room jump recovers contact in roughly 1.4 s. At the heavier
128-second jump, Winged Glide is delivered after walking into its legal range,
then the short escape completes and holds its safe melee destination through
the pending warnings. The later read of zero Winged charges occurs after that
delivery, and must not be misreported as unavailability at jump start. Ninety
captured events for IDs 45296/45297/45298/45302/45304/45299 have empty recipient
lists; the player survives. These are scoped mechanic results, not all-fight or
all-job acceptance. Thirteen Talon requests remain and are not all classified.
The first-boss hide itself was before reload, so its exact recurrence remains
open despite the passing native reproduction. AutoDuty is stopped at run end;
no repeat was queued and all replay caches were closed.
