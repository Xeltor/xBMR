# Healer delivery positioning

The reported defect was approaching for a heal or shield and returning to an
earlier position before the action went off. A search's predicted delivery and
RSR's current healing-demand flags are not native effect confirmation.

`NativeHealingDelivery` retains an already usable healing pose while the selected
action is pending. The same framework capture freezes its actual target,
required recipients, geometry and ready/cast/animation timing. Sage's explicitly
retained `_EukrasiaActionAim` supplies the shield follow-up during Eukrasia; an
offensive Eukrasian aim never qualifies. RSR keeps action selection/submission.

The hold ends on the player's matching native action effect, cancellation,
selection/actor/zone/job change, authority release, or its original bounded
completion window. The window uses native ready, cast and animation timings plus
RSR's captured queue window; repeated snapshots cannot renew it. An observed
ongoing heal cast retains its full remaining time rather than applying the usual
half-second slidecast allowance to the retreat.

The existing movement host emits the wait. No second movement writer or action
submission is introduced. It requires a fresh, indefinitely safe native footprint
and valid current recipient geometry. Current/future hazards, explicit movement,
stack gathering, independent party contact, displacement and action prohibitions
retain their ordinary planning behavior. Manual control and saved movement/healer
preferences remain authoritative. Out-of-range actions keep their approach.

`HealingDelivery` in NativeMovementRegression covers effect/setup distinction,
nonrenewing expiry, cancellation, full remaining cast, other healers' effects,
recipient movement, offensive Eukrasia, and independent safety/action constraints.
Compilation and regressions establish offline behavior only. A representative
live approach/shield/retreat sequence is still required for gameplay acceptance.
