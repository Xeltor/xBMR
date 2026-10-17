# Brayflox's Longstop normal: recorded coverage and repairs

Implementation review: 2026-10-17. Content-finder condition 8, territory 1041,
game `2026.10.06.0000.0000`. This concerns the current normal dungeon, including
the post-6.1 final Aiatar; it does not cover the hard dungeon or legacy Aiatar.

## Written guidance

Read before implementation:

- [Console Games Wiki](https://ffxiv.consolegameswiki.com/wiki/Brayflox%27s_Longstop):
  current normal bosses, follow-up Aiatar after Hellbender, add breaths and final
  Aiatar's staggered exploding pods.
- [Icy Veins](https://www.icy-veins.com/ffxiv/brayfloxs-longstop-dungeon-guide):
  current normal encounters, adds attacking Brayflox and final pod response.
- [Thonky](https://www.thonky.com/final-fantasy-xiv/duties/brayfloxs-longstop):
  useful early-boss context. Its final-boss healing puddles/Granite Rain describe
  legacy behavior and were not used for the recorded final-boss implementation.

Guides establish encounter context, not measured angles, physical hit boundaries,
server snapshots or last-safe departure times.

## Evidence identity

Two closed originals were decoded through EOF. They are adjacent segments of one
dungeon, separated by 4.826 seconds, not independent clears. Both were inspected
before implementation; all subsequent replay checks are training regressions,
with no independent holdout or live gameplay acceptance claim.

| Segment | Original SHA-256 | Archive recording | Retained revision |
| --- | --- | --- | --- |
| Earlier | `af5ac35cdcc0cf7cd40f54ea50da93600fafcf836e843d09fd956bf3fb8f28f0` | `146777408ddf43ddf1433a511cbc7b4c55380b3a0a79c163b3f771a5ad34c908` | `1436d14a622b2071b3648e5ea1bd8721821920706d8f5c361e6a5da89e08f936` |
| Later | `8f00ff951d07122a5088e2c98534d844782226d631102dc5281e0c814232f9e1` | `67eebdfbbccebe11a5c1556ea0010f03929ea1c91643274811800c7ab77940ea` | `54ce9eec1e9e276c2dab24cd333d3e49964066eed4f7060e4cdaea9fe5d6e975` |

Earlier: 218,564 decoded raw operations, 153 numeric parts, 155,782 retained
events. Later: 104,964 operations, 66 parts, 66,757 events. Sanitized schema-2
archive imports are complete and indexed. Numeric omission accounting is retained;
raw NPC yells and native cast payloads were inspected separately. EOF/import
completion does not establish that recording captured every dungeon event.

Private originals, baseline defect reports, requests, frozen expected contracts,
native outputs and build receipts are under
`~/.local/share/xbmr-evidence/brayflox-review-20261017/` and
`~/.local/share/xbmr-evidence/brayflox-fix-20261017/`. Originals were not added to
the source repository.

## Changes and mechanic contracts

### Great Yellow Pelican

Sable Back Slumber Breath 508 now produces cast guidance. The earlier segment
starts it at sequence 47236. Sable Back dies at 47954 before cast end 47957;
there is no resolving 508 action, so this supports warning and cancellation
cleanup, not a completed sleep. Nominal cone radius is 7.2 (sheet range 6 plus
recorded hitbox 1.2), half-angle 60 degrees from the matching fan omen. Exact
physical edges and server snapshot remain uncalibrated.

Only eligible recognized actors receive boss/add priorities. Negative priorities,
allies, dead/untargetable actors and unrelated actors retain their existing values.
The same preservation rule applies to Inferno Drake and Hellbender.

Pelican remains WIP: four instant Poison Breath 1393 actions have no established
advance cue/angle. No post-damage prediction or guessed timer was added.

### Inferno Drake and Tempest Biast

Burning Cyclone's nominal radius is corrected from 8.6 to 9.6: Action 984 range 6
plus recorded Inferno Drake hitbox 3.6. Its existing half-angle 60 degrees remains
uncalibrated. Three recorded short casts are covered; this arithmetic and replay
are not a measured physical boundary.

Levinshower 985 has approximately 0.501 seconds from observed cast start to action.
At sequence 156327 its cast facing points toward Brayflox while its ordinary
TargetID still points toward the tank, about 83 degrees away. Before the cast,
the living hostile Tempest Biast therefore previews toward the living friendly
Brayflox actor, even when Brayflox is untargetable. During the cast it uses the
captured cast origin/facing and native cast deadline. Missing Brayflox falls back
to caster facing with an uncertainty hint.

The front semicircle (radius 8.16, half-angle 90 degrees) is an explicit
conservative avoidance policy, not a measured 180-degree attack. The radius is
nominal range 6 plus recorded hitbox 2.16; the matching action has no angle omen.
Preview before the cast carries no invented future activation time. Cast identity
prevents duplicate starts from moving deadlines and stale finishes from removing
a replacement cast. Cancellation returns to the living-add preview; death and
destruction remove it.

Brayflox yell 151 occurred once about 7.367 seconds before the hit; yell 156 also
occurs during Burning Cyclone sequences and is not a unique Levinshower cue.
Neither establishes a reusable timed prediction. The live add cue starts about
44 seconds earlier in this segment. Inferno Drake remains WIP because the attack
angle and safe response timing still need stronger evidence; the policy is
deliberately conservative and may restrict more space than the actual attack.

### Hellbender and the first Aiatar appearance

Hellbender dies at later-segment sequence 22492 and its original module unloads
at 22493. Aiatar OID `0x1AD` is a distinct primary from final Aiatar `0x38C5`.
The new separately registered module handles Dragon Breath 29618 (rectangle 30
forward, half-width 4; CastType 12 does not add the caster hitbox) and Salivous
Snap 29617 (targeted tankbuster).

The module uses the existing Hellbender arena object; it does not infer a new
floor from player paths. It activates only for a living hostile primary in the
correct territory/duty and retains its phase through untargetability. In the
recording, Aiatar becomes untargetable at 24822 while Snap is still casting;
cast end is 24992, action 25004, destruction 26180. Native replay confirms the
tank-only prediction survives that transition and clears at native cast end.
That retirement precedes the action by about 0.084 seconds and is not proof of
the server snapshot or exact safe resolution time.

Hellbender remains WIP: five Bog Bubble 980 actions are instant, without cast
starts, so the existing cast component cannot warn for them. An earlier reliable
cue is still unknown. Existing Bind 280 pairs were reviewed without finding a
stale-target-mask defect. The new follow-up remains WIP pending stronger arena
and physical timing evidence for this newly covered phase.

### Final Aiatar

The old `maxCasts=4` limited both the radar and AI constraints. Later-segment
sequences 83408–83411 start four Burst 28658 casts; 84557–84560 start three more
before the first four end at 85278–85284. Three such overlap intervals are
recorded, lasting approximately 3.88–3.90 seconds.

Burst now retains every concurrent hostile helper cast internally, sorted by
activation. Every risk/deadline remains available to AI forbidden zones. The
radar and immediate human warning show only the earliest activation cohort
(within 0.25 seconds of its first deadline). That tolerance groups display only;
it does not alter hazard timing. When a cohort ends the next becomes visible.
This avoids covering the arena with all seven circles or forcing the player out
of a later-only circle prematurely. Source filtering, deduplication, cancellation,
death and destruction cleanup are covered by synthetic fixtures.

The existing arena, Toxic Vomit, final Dragon Breath and Salivous Snap are
preserved. AI receives nominal radius-10 Burst geometry with each native cast
deadline. A native deadline is not a measured server snapshot or verified safe
route. Radar cohort selection is tested through the actual preview method;
in-game rendering and continuous XCAI movement remain separate acceptance levels.

## Validation and maturity

The full isolated plugin builds with generated factories. The dedicated
`tests/BrayfloxRegression` suite passes 93 synthetic checks, including seven
internal/AI hazards versus four displayed, advancement to the remaining three,
negative-priority preservation, cast identity, bootstrap, replacement,
cancellation, death/destruction, registration and untargetable tankbuster state.

Native checks use the original closed recordings, exact matching game assets,
and evidence-derived expectations frozen before inspecting candidate output.
Follow-up/Hellbender validation passes 201 assertions over 21 samples. Pelican
and Drake pass 614 assertions over 45 available samples (two additional requested
samples deliberately follow module exit). Final Aiatar passes 849 assertions
over 17 samples, covering all three seven-cast overlaps, each cohort retirement,
adjacent Toxic Vomit and final cleanup. These exports have zero missing anchors,
module errors or unapplied operations. The final watched-build identity is
recorded in the private validation manifest. Native exports cover module geometry, timed constraints, target
fixtures and damage predictions; they do not execute live controllers/actions.

The review reopened known material omissions, rather than requiring personal
live tests. The remaining concrete gaps above justify WIP for the early bosses
and newly covered follow-up. Final Aiatar returns to Verified after repairing
the concrete truncation defect and validating all observed overlap transitions
and adjacent cleanup. No additional material defect was identified in that
module. Its existing arena and unaffected mechanics retain their earlier
acceptance; lack of personal live testing alone is not a reopening reason.

Readability was also inspected using a schematic of recorded pod locations:
all seven internal circles cover the floor, while the four-circle immediate
cohort and subsequent three-circle cohort expose distinct response positions.
This supplements the actual-preview-method assertions; it is not a screenshot
of in-game rendering or proof that a continuous route is safe.

The final current-tree Release build passes with zero errors and seven warnings
in unrelated code (six obsolete-gauge references and one nullable dereference).
Its DLL SHA-256 is
`06d988287695bf65091b9822b36d27fdb7835c0c10c266093f379ec39e5a4037`.
The build began after a fresh out-of-combat check; Dalamud finished automatic
loading at `2026-10-17T11:24:49.490+02:00`. The exact rebuilt DLL passes the
93-check fixture and all 1,664 native assertions across 83 available samples;
two further requested samples correctly have no module after exit. Its MVID is
`bc2e8c7a-7328-4022-a821-f757733d2966`. Build/reload and offline acceptance do not establish live
encounter performance. No installed-directory copy or manual reload was used.
