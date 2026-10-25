# Committed movement and input churn

## Crowded movement and route replacement (2026-10-25)

The maintainer reported competing movement, visible routes without useful execution,
and severe stutter stepping, and specified that cosmetic spacing must accept overlap
in crowds. The opening 64 samples of DevBridge combat archive
`948b688fe66f4178af1f9b4f65e2c5c9`, revision
`19bb52c51a1f43ea896ae6739964b5ca`, show walking interleaved with
`fresh native departure permits further optimization`, party-contact input churn,
and `action, target or required recipients changed` failures. These 250 ms samples
support the interruption report; they cannot attribute every frame-level stop to
social spacing or prove that all causes have been identified.

- A fresh complete validation of an active walk gives it execution priority over
  replacement optimization results. Higher native destination priorities and new
  healing requirements prevent retaining that priority. Cast/action locks, exact
  recipients, deadlines, manual control and physical checks remain binding.
- If a rotation selection disappears, an already started approach can retain only
  its remaining walking corridor when it still improves current combat range or
  party contact. This uses the existing complete-route validator on fresh input,
  removes all retired action/wait/resource predictions, and refuses displacement,
  teleporter, combo, field-placement and recipient-sensitive healing plans. It
  does not claim delivery of an action that disappeared.
- One continuous overlap uses one frozen cosmetic preference. Small peer motion
  cannot continually rotate its score field. Three nearby visible eligible PCs
  anywhere in the existing three-unit observation radius suppress optional social
  movement; deliberate crowd overlap is acceptable. Saved opt-outs remain intact.

Focused regressions exercise real walking execution, a disappearing rotation
selection, current-action preservation, new assignments, changed targets,
obstructions, manual handoff, stable cosmetic preferences and wider crowd overlap.
Build and offline results remain separate from live movement acceptance.

## Report and observed evidence (2026-10-25)

The maintainer reported frequent overlay route recalculation, hesitant movement
and stutter stepping compared with standalone XCAI. DevBridge combat archive
`22f032fc769f41bbb0d288e9c5890aa0`, revision
`b7901106fdd64a72b50880cebc82f4e9`, contains 564 samples over approximately
141 seconds in Vanguard as SGE, with no dropped initial samples. Loaded assembly
version was `7.5.6.3522`, MVID `578dd6c7-2f83-4afb-88a3-190fc3188a1d`.
At the bridge's 250 ms sampling interval, execution was Walk in 47 samples,
Wait in 280 and Arrived in 229; these categories include normal casting and
cannot by themselves quantify unwanted hesitation. Short walking publications
were followed by fresh-native-departure optimization waits. Input-change reasons
included mobility mechanics, moving range/action geometry and native goal fields.
Some worker searches approached the existing 250 ms freshness ceiling. Samples
bracket observations; they do not identify every frame-level transition or prove
that a specific input change caused a particular stop.

## Changes

- A replacement must pass complete current route/action validation before it
  replaces the committed plan. Failed or stale replacements leave the old
  executor commitment intact; that old route still passes its own fresh check
  before execution. Initial unsubmitted mobility uses the existing current-pose
  rebind before replacement validation.
- Search can propose the exact committed destination directly from the origin or
  after an action. The existing timed traversal proves that complete chord;
  blocked chords still use neighboring-cell detours. Intermediate walking labels
  retain local expansion rather than adding long ray queries at every cell.
- A completed equivalent search is reused only within its original native 250 ms
  freshness window. New revisions and expiry trigger new work. No timestamps,
  waits, action windows or departure deadlines are renewed by coalescing.
- Mobility readiness estimates may change while a worker finishes, as already
  supported for utility readiness. This exception applies only to cancellation
  equivalence; normal problem identity still changes and publication/execution
  rechecks current readiness, charges, anchors, recipients and the complete route.

There is no movement debounce, artificial pause, enlarged arrival tolerance or
relaxed hazard/assignment constraint. Manual authority remains with native owners.
Standalone XCAI stays retired.

## Focused validation

`PlannerContinuity` covers a 28-cell open continuation in one search expansion,
blocked-chord rejection and safe detours, rejection of stale/obstructed replacement
plans, continued execution of the prior valid route, immediate invalidation of
that route on a real obstruction, completed-search coalescing across 100 equivalent
observations, expiry/changed-revision refresh and mobility-readiness churn.
Existing walking, mobility, native safety and action-recipient regressions remain
relevant. Build/regression success and an observed reload are separate from live
movement acceptance; a comparable post-change fight is still required to judge
feel and remaining hesitation.
