# Alexander Resurrected movement intent — 2026-10-25

The maintainer's movement policy is safety, job-specific damage uptime,
job/party-specific healing uptime, or optional social movement. With no such
reason, hold position. Explicit native following and manual instructions retain
their existing authority. Mechanic assignments and necessary preparation belong
to safety. Useful healer prepositioning can share an already-required mechanic
movement window; it is not an unconditional centering instruction.

## Recorded evidence

Read the current Alexander Resurrected fight in Windurst: The Third Walk,
territory 1368. The live target identified the boss with hitbox radius 8.25.
Completed diagnostic capture `1e1eb76a3d22440fac07837cbc0fbdaf`, revision
`8321e317aa954ea09a5f8d79c5e740bb`, spans 19:44:12–19:50:17 UTC and contains
1,408 sampled frames with no truncated start or dropped frames reported. All
selected-query pages were consumed. The loaded baseline module identity was
`32057c82-7a6b-4497-9272-4b2ac26ad0f2`.

The last input-change reason was `party contact region` in 916 sampled frames;
that is not a count of distinct input changes. There were 777 native-recovery
samples and 257 walking samples. Ninety sampled proposals placed a destination
within 4.35 yalms of the recorded target center while still more than one yalm
from the player. These counts include pending proposals, not only executed moves.

At 19:45:01.245735 UTC, a bounded walking plan proposed `(4.089845,361.00845)`
from `(4.990428,363.3212)`, with the target at approximately
`(-0.015320,359.9756)`. The destination was about 4.23 yalms from the boss center.
The recorded party-contact region had radius 10, centered near
`(-1.707139,352.66125)`, and the provider reported `No configured healing demand`.
At 19:45:11.532971 UTC, another walking proposal ended about 2.66 yalms from the
same recorded boss center, again with no configured healing demand.

Other frames recorded native hazard recovery and physical no-ground-support
checks near the center. Those are separate evidence, not proof that all reported
movement shares one cause. The capture does not freeze the complete native map
or establish server snapshot timing, and target movement can change distances.

## Source findings and changes

`CapturePartyContact` unconditionally supplied a preparatory healer region derived
from all living party members and half the smallest enabled group-heal radius.
That made party movement a new positioning objective even without current healing
need. This standalone healer-contact publication is removed. Selected heals and
configured healing requirements still request actual access; required escapes
can instead consume a soft full-footprint party posture within the existing
movement window, as described in `mechanic-overstep.md`.

Enemy-center avoidance previously capped its soft radius at 4.35, below this
boss's observed 8.25 hitbox. The worker now freezes complete enemy footprints and
compares usable outside endpoints before inside delivery timing, charge saving,
caster walking cost and cosmetic continuity. Delivered action coverage/value,
individual recipients, healing need and native assignment priority remain ahead
of this endpoint preference. Current required actions must still fit their
original opportunity and the complete route must validate.

Crossing through a boss remains legal. Retaining the current pose when a boss
moves over it remains legal and does not create a cosmetic escape. A route that
leaves and then returns under the boss cannot use that stationary exception.
An assigned safe pocket or a boss filling the playable area can still require
an inside fallback. Saved enemy-avoidance opt-outs remain effective. Search
stopping/pruning cannot mistake an inside incumbent for a settled outside choice;
bounded search is not proof that no outside route exists. Urgent native safety
recovery remains available independently of optional optimization.

`EnemyEndpoints` covers outside approach/delivery, permitted crossings, inner
assignment fallback, a boss moving over the current pose, a healer already in
range holding still, a map-filling boss, opt-outs, and pruned/reference agreement.
The encounter module is unchanged; these changes concern shared movement.
Build/replay success and loaded-build provenance do not establish representative
live gameplay acceptance.

Validation: Release build passed with zero errors, the native movement suite
passed 143,326 assertions, and automatic development reload was confirmed with
module identity `226d8d65-2271-40b4-ad94-12bddded9ede`. This verifies delivery;
the reviewed Alexander fight predates this build.
