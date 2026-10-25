# Unified range movement — 2026-10-25

The maintainer reports hesitant combat/healing approach and mechanic hits, and
requests walking, Sprint, gap closing and reverse-jump-forward as one movement
choice. Source inspection identifies two concrete parity gaps, without claiming
that they caused every reported hit:

- The first finite opportunity pass aimed untargeted mobility only at offensive
  recovery. Healing recipients and advancing party contact waited for full
  spatial enumeration. Healing coverage compared endpoint reach but not useful
  arrival time before resource conservation.
- The execution owner retained any still-valid active walk unconditionally,
  preventing a subsequently proved faster mobility plan from replacing it.

Archived XCAI `Combat/MobilityCandidateSelectionPolicy.cs` compares mechanic
timing, execution seconds saved, uptime and recovery before charge cost.
`Combat/SprintAlternativePolicy.cs` compares Sprint with a dash using the same
action deadline and does not award fictitious extra attacks for early arrival.
`Combat/CombatMovementPlan.cs` releases walking retention when mobility is
requested. These are historical references, not new runtime dependencies.

The native joint search now proposes direct walking and deliberate spatial
mobility toward combat recovery, configured healing reach and party contact.
Required/committed positions and the verified native recovery endpoint also
supply finite mobility directions. Backward displacement reverses facing to
travel toward the goal; setup, path, landing, action locks, enabled ownership
and charge accounting remain in the existing joint transition and validator.
The full map search remains available for other landings and detours.

For an injured recipient currently outside configured healing access, equally
covered endpoints compare complete arrival before conserving mobility. The
resource pruning proof cannot discard faster arrivals after finding a walk.
Known offensive readiness windows still tie arrivals that deliver equivalent
execution, allowing a charge to be retained when walking suffices.

Walking continuity still suppresses cosmetic replacements. A complete mobility
alternative may replace it only after fresh validation of both routes, with no
loss of current action delivery/value/required recipients, native endpoint
priority, healing/combat/contact reach, combo value, placement or active zone
benefit. It must improve evidenced execution or complete arrival for an active
movement need. New hazards, stale inputs and manual handoff retain their native
authority. Mobility requests remain distinct from observed action effects.

`UnifiedRangeMovement` exercises each movement mode for combat and healing,
their combined choice, deliberate reverse facing, finite/full search agreement,
walking replacement, stale/blocked alternatives, party contact and a hazard
activation deadline that walking cannot clear. These synthetic regressions and
a successful build do not establish live mechanic snapshot timing or acceptance
for the reported encounters. Representative gameplay validation remains open.

Validation: Release build passed with zero errors (six existing obsolete-gauge
warnings), and the native movement suite passed 143,300 assertions. The earlier
healing fixture now accepts the exact verified range boundary as well as its
former raster-cell center, while still requiring both recipients in reach.
The watched development output automatically reloaded, and read-only bridge
observations confirmed version `7.5.6.3522`, module identity
`32057c82-7a6b-4497-9272-4b2ac26ad0f2`, loaded with settings ready and no reported
failure. Assistance was inactive at observation; this is delivery proof only.
