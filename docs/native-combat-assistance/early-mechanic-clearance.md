# Early clearance for healers and physical ranged

The maintainer requested earlier visible movement for healers and physical ranged
jobs on 2026-10-25, including clearing another player's tank mechanic without
waiting until the last moment.

The shared movement host captures a 2.5-second warning cushion for CNJ, WHM, SCH,
AST, SGE, ARC, BRD, MCH and DNC. This replaces the ordinary one-second cushion for
these jobs; it is an explicit movement preference, not measured encounter timing.
Known forbidden zones must be exited by that earlier deadline. Required travel,
cast completion and action locks still consume the available time. Warnings
published inside that window become immediately urgent. Other jobs retain their
existing timing. Combat-style late-departure waits are suppressed for these jobs
without rewriting saved settings.

Native recovery, joint action/movement planning, fresh route validation and
capture-failure recovery consume the same adjusted rasterization. Exact authored
warning timestamps, source actors and shapes remain intact, including provenance
used for knockback preparation separation. Arena bounds, obstacles, assignment
geometry, action recipients, activation opt-outs and manual control remain
binding. This policy cannot add missing warnings or establish an encounter's
server snapshot/last-safe timing.

The native regression fixture covers every affected class/job, a timed circle
attributed to another actor, recovery/fallback timing parity, moving before a
cast that cannot meet early clearance, unaffected job policies and an imminent
warning. Build/regression success and automatic reload do not establish live
mechanic acceptance; representative healer and physical ranged play, especially
tank-buster overlaps, remains unverified.
