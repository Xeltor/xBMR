# Arena-edge arrivals — 2026-10-25

The maintainer requests interior positions whenever usable safe space exists,
with arena-edge positions retained when mechanics or assignments require them.
The existing `AvoidArenaEdge` preference used a bounded 2.5-yalm presentation
penalty, below caster walking cost and committed destination continuity. It could
therefore lose even when an interior arrival preserved the intended action.

`PlanningArenaInterior` freezes the selected native arena polygon's interior
margin on the framework thread. It uses the existing 2.5-yalm center-clearance
threshold, real outer contours and holes, and the selected projection layer.
Unknown layered geometry or empty geometry supplies no preference. The local
navigation-map rectangle is never treated as proof of an arena edge. Only an
active encounter with the existing saved edge-avoidance toggle enabled captures
this field. Workers receive immutable cell values, never live arena wrappers.

Among routes preserving required current action coverage/value, native assignment
priority and configured healing needs, the search prefers outside-boss endpoints
and then usable arena interior before earlier delivery, resource conservation,
caster walking cost or cosmetic continuity. First-pass stopping, pruning and the
stationary optimum proof account for the interior preference. A new interior
route must validate before it can replace an ongoing edge-directed walk. Interim
edge incumbents do not preempt the pass's interior comparison; urgent native
safety recovery remains independent.

This does not shrink the playable arena or add a forbidden zone. Edge-only safe
pockets, explicit destinations, native assignments, required healing recipients,
blocked inward routes and a boss occupying the interior still permit an edge
fallback. Every segment, timing deadline, arena boundary and manual-control gate
retains native validation. Movement may cross the margin. The capped preference
stops once adequate clearance is available, without a continuing pull toward the
arena center. Mechanic-clearance refinement cannot trade an already-interior
arrival for an edge endpoint.

`ArenaEndpoints` covers healer edge recovery, first-pass/reference agreement,
replacing an edge walk, holding an adequate interior pose, opt-outs, edge-only
assignments and explicit destinations, healing requirements, inward obstructions,
hazard-covered interiors, holes and layered arena selection. Synthetic validation
and a successful development reload remain separate from live gameplay acceptance.

The combined 2026-10-25 Release build includes this arena preference, untimed
mechanic urgency and FATE-arrival targeting. The full native regression suite
passed 143,375 assertions against the combined artifact. Automatic development
reload was verified through DevBridge at version `7.5.6.3522`, MVID
`ee2fbe5e-a855-405f-ac25-5ea662a7ddca`. This confirms delivery and offline behavior;
representative live arena-edge movement remains unverified.
