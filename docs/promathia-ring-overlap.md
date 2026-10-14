# Promathia ring and cleave overlap correction

Follow-up on 2026-10-14 after the user reported no available space during
overlapping mechanics and a vulnerability stack. The previous ring timing repair
also froze each ring's centre at the boss's visual-cast starting position. That
was incorrect: the ring resolves at Promathia's relocated position. With Bastion
of Twilight and Pestilent Penance together, the old safe circle lies entirely in
the frontal cleave, creating contradictory avoidance constraints.

Both Wheel of Impregnability and Bastion now retain their source actor and update
their origin from its current position when producing drawing and AI geometry.
Their fixed activation deadlines, dimensions, independent helper retirement and
cancellation behavior remain unchanged. Puddles, towers, arena boundaries,
cleaves and linked lines retain their existing constraints.

## Current-run evidence

The closed local recording has SHA-256
`482fbc09b779c4d518d0e13d59b21c62e2fbde58e2e4f45c7060c917ef3e9f10`.
It records game version `2026.10.06.0000.0000`, matching the offline game data.
Raw sequences below come from the native decoder. DevBridge retained-operation
sequences differ because it omits some operation kinds; they are not substituted
for raw runner boundaries.

- First Bastion visual: raw `46809`, relative tick `605705316`, centre
  `(-820.0046, -820.0046)`. Penance helper starts at raw `51457`, tick `663184224`,
  after the boss relocates to `(-813.0159, -807.8889)`, facing approximately
  -150 degrees. Ring helper movement at raw `58044`, tick `731289798`, places it
  at `(-813, -807.8757)`. Its Bastion action `50324` at raw `58088`, tick
  `732078667`, hits recorded local slot zero. Vulnerability Up `1789` follows at
  raw `58324`, tick `737116269`. The latest recorded local position before the
  hit is `(-804.1957, -804.4625)`, outside the 8-yalm inner safe circle.
- The later Bastion visual is raw `182681`, tick `2487142896`. The boss moves
  from the arena centre to `(-813.016, -832.15076)` before the Penance helper cast
  at raw `188845`. Ten linked-line casts start at raw `190765`–`190790`.
  The ring helper moves to `(-813, -832.1243)` immediately before its action at
  raw `196747`, tick `2613473002`. The Penance action follows at raw `196811`.
  These are two occurrences in one recording, not independent holdouts.
- XCAI combat archive `13fbd70d8d85490d9ddfe8cffb793e19`, revision
  `6e50d9940859410a8794d74c74a4dc5e`, captures the later overlap with twelve
  forbidden constraints. Its sampled raster becomes unavailable around
  `15:14:27` UTC until the ring resolves. This is diagnostic context, not proof
  of exact server timing or of which constraint caused every controller decision.

The first investigation snapshot had an unfinished compressed tail. It was
superseded for native validation by the closed original above, which decodes
completely. Early exploratory samples using retained-operation sequences were
also superseded by correctly mapped raw-sequence requests. They are excluded
from the final validation counts.

## Validation and limits

The full current working tree builds successfully with the pinned SDK, zero
errors and six existing obsolete-gauge warnings. The watched development output
is rebuilt in place; no installed plugin directory is modified and no manual
reload is requested. Unrelated Al Zahbi and Quaqua changes remain in the tree.

Nine added synthetic assertions cover both moving ring shapes, unchanged fixed
deadlines, matching AI predicates and the overlapping rear safe pocket. The full
native regression suite passes 1,260 assertions. The new moving-origin check
fails against the saved previous assembly.

Twelve paired native samples across both recorded Bastion overlaps pass with
complete and identical numeric projections, matching game data, no missing
samples, no module errors, and no skipped or unsupported output kinds. Every
other component's exported AOEs and all ring activation deadlines match the
baseline. At the first cleave and before its ring hit, the rear probe is rejected
by the baseline ring and accepted by the candidate with both hazards present.
The later linked-line overlap likewise retains all twelve constraints while
accepting its rear probe. Earlier Explosion constraints still reject that probe
while those attacks remain active; the correction does not turn an earlier
hazard into safe floor. Eight optional native navigation fixtures are included.
Detailed requests, reports and summary are under
`/tmp/xbmr-promathia-overlap/`.

- Baseline assembly: `0c237993a4fba0b957a2e01b70bba9127758504f2daffe0728370ef63e2170a6`
- Candidate assembly: `9d07bd1011b4a7691bf75352e7493eaab61d17389c52458b88b0d288b7f3f768`
- Main module source: `17612de48fde8fcac6ae51ee89672180b8b69a6e1182ee83610473b390434a5d`

This verifies the source regression and its offline predictions. It does not
prove a continuous physical route, the server snapshot deadline, or successful
live controller execution after this build. Heavy pool edges and movement speed
retain the uncertainties documented in the previous repair. A fresh live
Promathia run remains necessary for gameplay acceptance.
