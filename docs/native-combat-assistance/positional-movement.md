# Positional movement eligibility

Rear/flank movement requires positive current evidence: a striking dummy, or a
resolved living other player/allied battle NPC that the enemy is targeting.
Targets attacking the local player, missing/unresolved/self victims, dead or
destroyed victims and omnidirectional targets do not grant this movement.
Casting and a temporarily stationary pose are not evidence of fixed facing.
Other stationary encounters without explicit facing evidence conservatively
skip rear/flank movement. Tank/front positioning remains independent.

`PositionalMovement.ForTarget` is shared by native RSR action capture, the RSR
positional hint, rotation goals, GoToPositional and BMR following. Actor state is
checked on the framework thread before worker opportunities/goals are frozen;
fresh capture removes obsolete positional requirements when the victim changes.
Attack range, required recipients, encounter safety and manual authority retain
their existing ownership.

`PositionalMovementEligibility` exercises player aggro including casts, unknown
victims, another player, allied NPCs, dead/destroyed victims, dummies and
omnidirectional enemies through both native goal scoring and RSR action coverage.
These offline checks and a build do not establish live gameplay acceptance.
