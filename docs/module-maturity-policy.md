# Module maturity policy

xBMR uses evidence-based promotion and issue-driven reopening. WIP means known
incompleteness or a credible reported correctness concern. It does not mean that
the maintainer has not personally tested the encounter.

## Leaving WIP

A module can leave WIP when:

- Relevant mechanics, phases and known alternate patterns are covered or
  explicitly accounted for. Arena-only coverage remains WIP.
- Available encounter evidence supports the arena, geometry, assignments,
  warning lead times and hazard timing. Any remaining uncertainty is documented
  and does not leave a known material gap in the safe response.
- Relevant offline validation passes, including overlaps, transitions,
  cancellations and reset cleanup where applicable, and the plugin builds
  successfully.
- No known unresolved module defect materially affects correctness or safety.

Personal live testing is optional supporting evidence, not a promotion gate.
There is no requirement to personally test every encounter, every role or every
change. Relevant replay and offline validation can be sufficient; record their
limits and do not describe them as live gameplay proof.

Evidence needs to cover the encounter's distinct known behavior. No fixed number
of recordings, successful clears or days without reports automatically qualifies
a module. Hypothetical undiscovered patterns and minor cosmetic or optional
improvements do not by themselves prevent promotion.

The maintainer or an agent working on the module applies this policy and records
a short acceptance note in the encounter's implementation notes or relevant Git
issue/PR: the reviewed source revision, covered behavior, evidence, validation,
remaining limitations and reason for promotion. Separate personal live approval
is not required.

Use `Verified` for coverage accepted under this policy. Reserve `AISupport` for
additional live evidence that BMR AI handles the encounter well. `Contributed`
describes contribution provenance; it is not an intermediate completion gate.

## Returning to WIP

Return a module to WIP when a user complaint or Git issue identifies a credible
material omission or defect, such as a missing mechanic or alternate pattern,
incorrect arena or hazard, unsafe assignment or guidance, late warning, or broken
transition/reset behavior. A credible severe report is enough to reopen it while
investigating; personal reproduction and multiple reports are not prerequisites.

Cosmetic requests, feature suggestions and unrelated XCAI rotation or navigation
problems do not require reopening an otherwise correct module. Investigate
unclear reports to establish whether the module is implicated.

Record the reopening reason and link the complaint or issue, preserving the
previous acceptance history. Keep the module WIP until the concern is fixed or
shown not to be a module defect. After a repair, validate the affected behavior
and adjacent interactions; broader changes require broader validation. Retesting
the entire encounter after every change is not required. Record the outcome and
promote again when the leaving-WIP criteria are satisfied.

## Archive collection

XCAI currently considers only exact `WIP` modules eligible for automatic archive
collection. `Contributed`, `Verified` and `AISupport` do not qualify. Do not change
a maturity label merely to suppress uploads without meeting this policy.

Promotion stops WIP-based collection and reopening resumes eligibility once
users load the corresponding xBMR build, subject to XCAI's existing opt-in and
whole-recording eligibility rules. Previously skipped recordings can be
reconsidered with the new build; completed upload receipts remain intact.
