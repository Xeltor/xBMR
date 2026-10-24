# Job evidence and explicit limits

## Pictomancer landscape boundary

Question: what proves the area in which Starry Muse grants Inspiration's cast
benefit, separately from its party damage buff?

The installed game version observed on 2026-10-23 is `2026.10.16.0000.0000`
(version-file SHA-256
`008c5f87f12675f893fe2afa0cbdf941b24c6bb75c48c2ddc116e4516d1f109b`).
The archive's available Action/Status extraction is `2026.09.15.0000.0000`.
That is an explicit version mismatch, not a compatibility assertion.

The retained Action 34675 row has cast type 2, effect range 30 and no Omen link.
Its row SHA-256 is
`5971d0f03e83330c9456927f37180d57ef1872a378a2e289bdb5ca8f567b0313`;
the Action dataset SHA-256 is
`6f98b919501d005d944c4840e714df456143dd4cb3a60285f4463a666637d86b`.
No independent Inspiration boundary follows from those values. The current
[official job guide](https://na.finalfantasyxiv.com/jobguide/pictomancer/)
distinguishes the party buff and the landscape-dependent cast benefit, but does
not separately calibrate the latter's boundary in the inspected text.

The retained Status rows identify 3688 as Hyperphantasia, 3689 as Inspiration,
and 3679 as Rainbow Bright. Their row SHA-256 values are respectively
`6d4b1c589271d051cbe9daa30879bd5df4dde17f64eb20a2610624c7d25099ed`,
`ac9498340f5348c78578874d9923d3174621a2b680786f824827faa7a33504fd`,
and `ec2a7e4efb269f18fc344ba23c60c47896683976578a69769ac488dc6032d3fe`.
The extraction reports Inspiration's permanent flag; flags and descriptions
alone do not establish live gain/loss behavior or geometric containment. The
Status dataset digest is
`58782435e8322138b29eb05b7c94b75989734b72c141e87b81dcffbac8646946`.
Both retained families bind to version-file digest
`54c61ca2de432e31cc397dc9cb5e2c77d68a6ab0216f44c88722cce072939a01`.

The native planner therefore retains explicit geometry/timing bounds: `FriendlyEffectGeometry`, or `ActionTimingEvidence` when conditional timing is limiting too.
Complete radius and return-mobility acceptance still require matching-version
evidence and independent field-boundary/expiry observations. A visual mesh or
status name alone must not become a gameplay containment proof. No game files,
archive datasets or companion services were modified during this inspection.

## Red Mage preparation evidence

Read-only source comparison uses the pinned standalone revision and RSR 7.5.7.6,
including `RebornRotations/Magical/RDM_Reborn.cs`,
`ExtraRotations/Magical/BeirutaRDM.cs` and the basic Red Mage/action-target types.
Installed assembly checks validate the action/selection/config surface, resource
and proc flags, Reborn pooling gates and Beiruta's explicit hold field. The cone
half angle matches RSR's `ActionTargetInfo._alpha = Math.PI / 3` implementation;
current action range/effect data and hitboxes supply the supported reach.

Native combo state supplies the original ST/AoE continuation and expiry. Supported
Reborn starter gates and Beiruta's observed out-of-range hold supply conditional
preparation. No `CanUse` or private selection method is invoked: these can mutate
RSR targets. A live same-target selected ranged fallback corroborates intent.
Only known ranged mana-generating RDM GCDs may precede preparation; selected sword
steps already have mandatory native execution geometry, while Reprise, ranged
sword/finisher chains and unknown resource effects do not borrow unchanged gauge
or duplicate an attack. No forecast mana gain is credited.

The current selected attack/target/recipients remain first. A prospective cone
keeps its independently required recipients and configured threshold; an enabled
ST fallback remains separate. Native forbidden recipients cannot be traded for a
larger hit count. Selected ranged Manafication swords are preserved rather than
replaced to force a different AoE starter. Unknown rotations retain ordinary
selected-action/native-goal support without inventing private starter policy.

Preparatory value is not an extra delivered-action bit. Its prospective GCD slot
follows the actual selected-action stage start/recast/end and animation lock.
Action/facing prohibitions apply at that time. Native combo expiry is a deadline
for execution; an explicit RSR range-wait deadline requires timely range recovery;
a starter's twelve-second forecast window is labeled separately. Original clocks
age across captures and exact revalidation. The same-cell label dominance check
also retains a meaningfully earlier future GCD slot. Original native enabling
Swordplay/Embolden expiry bounds the policy evidence independently; contradictory
RSR/native status observations withhold optional preparation.

The old fixed approach radii, 250ms starter confirmation and separate optional
phase-exit writer are not native constraints. The shared route/mobility search
uses actual geometry, resources and native authority. Resource pruning cannot
stop at a weaker ST fallback when a legal mobility action can preserve the full
AoE opportunity. Cosmetic post-combo jumps rank below useful resources.

The managed artifact passed 41,769 native assertions, installed RSR 7.5.7.6
contracts and 19 broader suites. These prove supported source/synthetic behavior,
not actual RSR resampling, packet delivery, gameplay cone boundaries or in-game
rotation/lifecycle parity. Those remain on TODO.md. No DLL was deployed.

### Ranged sword metadata gap

`ActionDefinitions.this[ActionID]` performs an exact registered lookup; this
base's `ActionQueue/Casters/RDM.cs` registers original sword IDs and lacks
45960/45961/45962. `ActionQueue.CanExecute` instead expands original sword range
under Manafication. Installed RSR's concrete selection exposes the newer IDs.
The old `RsrActionEvidenceSource.Capture` therefore rejected those current actions
as unsupported. The adapter now prefers an exact definition or uses only the
known corresponding 7527/7528/7529 family metadata. Selected IDs are never
normalized in execution evidence, and current game/RSR data still supplies cast
and geometry. Installed enum identity checks and exact-definition-precedence /
unknown-ID rejection tests pass. Shared native family animation metadata remains
an assumption to validate against actual variant delivery; no live packet claim
follows from the source relationship alone.

## Black Mage observed return timing

The native return path uses one `NativeLeyLines` observation for the exact owned
0x179 ground object and active 737 personal status. An ambiguous/foreign object,
invalid position or expired effect cannot supply a return anchor. Retrace cast
observation still moves the known center without renewing its original lifetime;
planning or submitting Retrace is not implemented by this step.

`NativeLeyLinesTiming` calibrates a current spell only from matching inside/outside
native no-proc cast-time and recast observations. Matching includes actor, zone,
level, native speed stats, elemental stance and other active status identities.
Either observation order works. There is no assumed haste multiplier or credit
for an unselected future spell. This is a current-condition cache, reset on
lifecycle/condition changes, rather than combat-history retention. Expiry jitter
cannot extend the original field or matched-modifier deadline.

The frozen profile participates in the full route search. An abilities-only
return carries a distinct label until it leaves the field or the field expires;
ordinary walking cannot acquire that projected speed benefit. Walking and return
mobility compete on actual selected-spell timing, setup/lock, resources and shared
native safety. An equally timely walk preserves a charge. A weak speed gain cannot
delay a safely available current cast/GCD cycle. Selected action stages retain
their original sequence slot and predicted cast duration, so exact revalidation
rejects loss of the speed evidence even within a longer generic forecast window.

The existing selected-plan route, return stage and action-stop overlay primitives
show these decisions; no separate correction writer or overlay HUD was added.
Current diagnostics expose the conditional cast evidence and selected source slot.
The combined managed checkpoint passed 41,809 native assertions, installed RSR
contracts and 19 broader suites. Native live no-proc/recast sampling, ownership
status/object updates, actual return locks and spell delivery still need live
validation. Placement/scoped selection, Retrace and complete after-action return
parity remain explicitly open in TODO.md.

## Native Retrace and selected-spell recovery

The [official Black Mage guide](https://na.finalfantasyxiv.com/jobguide/blackmage/),
checked 2026-10-23, identifies the three-yalm, twenty-second Ley Lines effect,
cast/recast benefit, owned return and Retrace relocation without duration renewal.
It also confirms Ley Lines cannot replace an already active effect. That supports
the mechanics model; it does not validate packet timing or a live action lock.

Native per-action adjusted recast now accompanies cast evidence. This replaces
using RSR's generic GCD total as the selected action's recast. The planner keeps
current action delivery, independent recipients/value and own recovery identifiable.
Instant spells can improve their evidenced recast when entry completes before
readiness. A faster recovery cannot buy a delayed current spell, and resource
pruning cannot stop at instant delivery while a better recovery remains possible.
The same condition checks run in search and final route validation. Immediate
execution requires native cast/recast readback, rather than only a moved object.

`PlanningPersonalField` carries owned object identity, center and original expiry.
Retrace is `PlanningUtilityEffect.RelocatePersonalField` in the shared executor.
It changes the field, keeps the original expiry, and updates subsequent Between
the Lines mechanics. Labels retain different field geometry and GCD recovery;
those states cannot dominate one another merely through earlier arrival. Native
object corroboration is required after an observed relocation. A failed setup
removes that candidate while other positions/actions remain eligible.

Native status expiry jitter, ordinary rotation-condition changes and temporary
missing/ambiguous objects cannot restart the effect lifetime. A real owned
placement or a new observed object can establish another lifetime. Owner,
territory, clock and lifecycle changes clear the context. Zone recovery consumes the same stabilized field observation. The RSR
selector uses scoped original-action ownership and keeps saved opt-outs. An
unmodeled selected RSR ability keeps its weave rather than funding an elective
field action. No native rotation-prediction adapter or second movement writer
was introduced.

An inside-only sample cannot establish outside timing under arbitrary other speed
modifiers. Missing/expired outside evidence therefore reports `ActionTimingEvidence`
and withholds that forecast. This does not block the currently supported spell or
native safety recovery. It does not assume a fixed multiplier. The broader known
modifier/gauge-expiry audit remains part of planner validation.

The combined artifact passed 42,144 native assertions, including 144 obstacle,
expiry, readiness and return-mode cases against an independent finite-state
reference for delivery and recovery. Nineteen broader suites, installed RSR
contracts and nine repository tests also passed. The overlay reuses its utility
marker labeled Retrace and shows the relocated useful field. Current diagnostics
include field identity/original center, selected recast and recovery clocks.
The later live mechanics observations below supersede the native-readback limits
of that checkpoint. RSR/integrated execution, placement/scoped Ley Lines selection
and full phase/after-action return parity remain open.

### Live native mechanics, 2026-10-23

The on-demand `nativeTiming` view uses the existing bounded diagnostic provider.
It samples a fixed mechanics-specific set of current BLM/PCT/DRG action timings on
the framework thread after request validation. It does not submit actions, retain
history or open an arbitrary action-inspection API. Invalid/foreign-thread
requests never sample native state; display truncation never repeats the sampling.

With BLM100 idle, native AI inactive, RSR Off and standalone XCAI unloaded,
Fire IV/Blizzard IV returned 1,940 ms cast and 2,425 ms recast outside Ley Lines;
inside they returned 1,649 ms and 2,061 ms. Both cast API modes (`applyProcs`
true and false) included the field benefit. Xenoglossy stayed instant while its
native recast changed by the same observed amounts. Thus disabling proc handling
does not establish an outside-field counterfactual. The current matched-condition
inside/outside calibration remains necessary; these particular values are not
universal spell constants or a license to synthesize future native timings.

At 20:58:36–20:58:42 UTC, placement, a short walk out and Retrace were corroborated
by native field/buff/timing observations and the owned action effect. Retrace's
observed animation lock was 0.6 seconds. The unique owned ground object changed
from `1073750066` to `1073750067`; its center moved to the player and its remaining
duration continued from 16.677 to 15.238 seconds across 1.417 seconds of observation
time. A later repetition corroborated the replacement and unchanged expiry.

That replacement exposed a defect in the new native observer's same-object
assumption. The observer now distinguishes actual object ID from the current
effect's identity. Only an observed owned Retrace followed by a unique live owned
replacement may preserve the old effect identity, calibration and original
deadline. Another owner's action, an unexplained object, expiry or a real new
placement cannot supply that continuity. A temporary object gap still withholds
spatial proof. Exact consumed-prefix validation accepts the corroborated new
object at the planned center; it still rejects an unrelated replacement.

At 21:11:04–21:11:06 UTC, navigation was explicitly stopped before Between the
Lines. A normal action request with the actual player target was followed by the
actual landing at the owned center, Circle of Power, faster native timing and a
0.8-second action effect. An earlier accepted request without corresponding
landing/effect evidence was not counted as delivery. These observations support
the current native 0.8-second estimate, not complete integrated return acceptance.

The corrected plugin passed 42,155 native assertions, 19 broader suites, installed
RSR contracts and nine repository tests. Artifact SHA-256
`9d1f65f2201b9f5b980c867af1de54fd62c47aeaa6269ec38c1d352a2f97f38d`,
MVID `70206029-3cc7-4843-b68b-008e14734226`, was deployed through the normal watcher
and its loaded identity verified. The mechanics checks used typed bridge actions
with fresh state; they did not activate AI/RSR, exercise the native executor in
combat, prove complete route quality or finish placement/scoped selection.

## Pictomancer matching-version observations and native center return

With AI inactive, RSR Off and standalone XCAI unloaded, saved set 17 was used for
bounded PCT checks and set 14/BLM restored afterward. At 21:40–21:45 UTC on
2026-10-23, Fire in Red changed from 1486/2477 ms cast/recast to 1115/1858 ms with
Inspiration. Coverage was observed at an actual 7.845-yalm distance and absent by
8.374; intended endpoints that were not reached were excluded. These are sampled
bounds, not proof of an exact radius, full circle or height behavior. Smudge was
not submitted after the setup position failed corroboration.

At 22:19:56–22:19:57 UTC, a further owned Starry Muse action effect (0.6-second
lock) and Inspiration/fast timing were observed. The object-table comparison
before/after found no newly appearing object and no object owned by the local
player. This does not prove that all native field representations are absent;
it withholds the stronger Ley Lines-style owned-object claim for this test.
After a normal dummy reset, target clearing and normal conditions were read back.
BLM/set 14 and all 14 equipment slots matched the pre-test data, excluding only
observation-token metadata. Position was unchanged. AI and RSR remained off.

`NativePictomancerTiming` now calibrates only the exact selected action under
matching player, territory, level, stats and other live modifier/stack conditions.
An owned placement event supplies its sequence, fixed center and original expiry.
The center becomes a forecast alternative only after native Inspiration and that
action's timing have actually been observed there. No 30-yalm party radius, fixed
movement-speed estimate, placement grace extension or second movement executor is
retained. A fresh covered stopped pose can corroborate execution within arrival
tolerance without pretending the player teleported to a mathematical point.
Full footprint/expiry acceptance and integrated live return remain open.

Naturally instant GCDs now also carry Inspiration's conditional recast; leaving
the observed pose cannot retain that faster recovery without outside evidence.
The Smudge field opt-out travels with route state. A declined field benefit cannot
be regained by adding walking or waiting after Smudge. General legal Smudge safety
or range recovery remains available with conservative timing, and walking return
remains available. State dominance, incumbent continuation and exact validation
preserve this distinction. The existing in-world movement marker now names Smudge.

The checkpoint passed 42,286 native assertions including 96 finite reference
alternatives, 19 broader suites, installed RSR contracts and nine repository tests.
Plugin SHA-256 `a009cd95581a19d3ccfaef679308c450941a0e996d08620d7b08d90200653d10`,
MVID `1ef7b26d-cb13-430f-83b1-ff8b609d1b8c`, was automatically reloaded and verified
at 22:28 UTC. These offline/source and bounded mechanics checks do not establish
integrated live Smudge delivery or complete Pictomancer parity.
