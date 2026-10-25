# Mechanic arrival clearance — 2026-10-25

The maintainer reports that escapes stop exactly at the mechanic boundary even
when more usable safe space exists. The existing extra-space preference was
enabled at two yalms, but it was only a destination presentation score, below
caster movement loss and prior walking continuity. Native cushioning also uses
eight raster samples rather than a continuous clearance preference. These source
findings explain a route-selection gap, not every observed gameplay trajectory.

Archived XCAI references: `Combat/MeleeMechanicClearancePolicy.cs` and
`Combat/LocalArrivalController.Combat.cs`. XCAI refined an existing required
escape into nearby space, capped extra travel by the configured buffer, retained
execution and independent goals, and verified route deadlines. It did not start
a new step solely because a safe current pose was close to an AoE boundary.

`PlanningMechanicClearance` freezes bounded authored-warning distances on the
framework thread. No mutable shape or actor crosses into the worker. Distances
are normalized by the shape's conservative Lipschitz bound and are destination
preferences only; they never replace the native raster, timing or safety checks.
The existing Avoid danger zones toggle and zero extra-space setting disable it.

The shared movement worker refines complete escape publications before walking
commitment. It retains the original escape and adds at most the requested
distance of walking into nearby space, choosing greater bounded clearance and
then the shortest equivalent continuation. It can consume an existing wait before
a selected cast but cannot delay that cast or reduce its value or required
recipients. No mobility charge is added. Combat/healing/contact reach, native
priority, beneficial zones, physical checks, phase limits and original departure
deadlines remain binding. Full native validation checks the whole revised route,
including hazards crossed by the extra leg, not just its final point.

Explicit destinations, stationary permission, forced movement, knockback phases
and teleporter routes retain their own authority. Unknown/unsupported clearance,
stale search input, a tight safe pocket or a selected action that needs the tighter
arrival leaves the original route usable. Clearance is capped, so an accepted
arrival does not introduce an outward gradient or a follow-up correction move.
Refined results remain explicitly bounded rather than claiming a global optimum.

Healers can also use the same required escape to improve party coverage. The
enabled group-heal footprint supplies a soft arrival preference; it does not
publish a party-contact movement goal or shrink the usable footprint to half
its radius. After hazard clearance ties, nearby arrivals nearer the coverage
center are preferred. Party preparation alone must fit an existing selected-cast
wait or observed free GCD window. A party member moving does not cancel the safe
worker merely to refresh this soft preference. Selected healing needs and each
required recipient remain independent constraints.

`MechanicOverstep` covers healer movement cost, bounded extra travel, retained
deadlines/resources, casts with/without an existing free window, action coverage,
combat/healing reach, assignments, future hazards on the extra leg, tight pockets,
already-safe poses, warning-field capture and preference changes. Build/offline
validation and development reload are separate from live gameplay acceptance.

Validation: current plugin sources built successfully with zero errors and six
existing obsolete-gauge warnings. The native movement suite passed 143,326
assertions. The main development output automatically reloaded; bridge reads
confirmed loaded version `7.5.6.3522`, module identity
`226d8d65-2271-40b4-ad94-12bddded9ede`, settings ready and no reported failure.
Assistance was inactive at verification. Representative live movement acceptance
remains open.
