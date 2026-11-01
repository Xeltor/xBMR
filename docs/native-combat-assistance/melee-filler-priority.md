# Melee access before ranged filler

Melee and tank throwaway ranged GCDs are fallback actions when useful melee
access cannot be restored in time. They do not reserve stationary delivery in
the movement search. Shield Lob, Tomahawk, Unmend and Lightning Shot now share
the existing melee filler policy; real ranged damage opportunities such as
Holy Spirit and Uncoiled Fury retain their selected-action requirements.

Automatic RSR filler yields when the current complete route, freshly validated
from the actual player pose, restores melee execution before that filler's
observed recovery window. Validation reports the first attack-range access,
including native GCD readiness and action locks, rather than the final route
completion. The complete onward route and final assignment must still validate.
Ranged filler remains available when the route is unsafe, stale, too slow or
unavailable. This is an access policy, not a potency forecast of future attacks.

At engagement/target changes, RSR delivery can precede the movement owner's first
assessment. A provisional native walk must not consume the GCD while the first
pass is still assessing a potentially timely gap closer. This pending decision
is capped by the existing 250 ms native freshness ceiling, starts once per
actor/target/filler lifecycle, and cannot be renewed by equivalent callbacks.
An assessed result ends that wait. This does not certify safety or submit any
movement; only the existing validated executor may do that.

The synchronous delivery gate now receives the actual action recipient. A route
to one target cannot suppress filler sent to another. Native activation,
movement opt-outs, manual input and explicitly requested RSR actions retain
authority. Independent tank aggro recovery remains exempt. The engagement gate
is unchanged: this repair does not authorize pulling untouched enemies.

Focused regression coverage includes tank/melee classification, real ranged
actions, bounded first assessment, target-specific delivery, early hitbox entry,
native GCD readiness, delayed walking, and unsafe/incomplete/stale routes.
Offline validation and automatic reload are separate from live pull acceptance.
