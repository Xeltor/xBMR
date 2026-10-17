# Al Zahbi movement and mechanic corrections

Reviewed on 2026-10-14 for Windurst: The Third Walk, duty 1117, territory
1368. These are private fork corrections; no upstream submission or maturity
increase is implied.

## Follow-up: live knockback failure and integration correction

**Current ownership:** the maintainer requested removal of the XCAI-specific
workaround from xBMR. The module again publishes its original combined landing
and exact-origin constraint. The XCAI-specific comment and optional integration
test were removed from this repository. The encounter's native safety hints,
exaflare corrections, wave-1 coverage and interrupt component remain. XCAI's
agent owns the general consumer-side fix. The diagnosis and temporary-workaround
validation below are historical evidence, not acceptance of the restored code
with the affected XCAI build. The removed test fixture is preserved privately at
`<isolated validation output>` for that agent.

The first correction did **not** pass live acceptance. A later Al Zahbi push
still sent the player past the west boundary. The following combined regression
and correction supersede the earlier module-only knockback acceptance evidence.

The retained XCAI fight `30ac7460b40d4be7b8d0f3e19b20b226`, revision
`3b0ef25d4ba244f19709e098d968ad02`, captures the failure on 2026-10-14.
Frames 118–134 show a targetless player stationary around
(715.74713,720.1962), with `A31AlZahbi` active and no navigation destination
after frame 118. The sampled manual movement vector is zero. Knockback guidance
is present: frame 126 has two constraints and XCAI reports a one-push sequence.
The subsequent action 50481 is recorded at retained operation 357445,
raw timestamp `2026-10-14T16:59:42.4307903` (no offset). This aligns with bridge
UTC `14:59:42.4307903` under the current client's UTC+2 offset. Its origin is
approximately (720.9734,719.9968). Frames 135–137 show displacement to
(702.45013,720.693), (695.6945,720.9453), then (693.7695,721.0168).
The authored west edge is x=696. The player subsequently walks back in.

Root cause: the original fix wrapped its radial landing predicate and exact
origin check in `SDUnion`. XCAI's native sequence reader identifies projection
shapes by type. It retained this unrecognized union as an ordinary hazard while
validating projected landings with `ActionPredicate.IsDashSafe`. This tests the
landing as if it were another preparation point, effectively requiring another
22-yalm push. No safe cells remain. BMR's existing navigation fallback clears
the least dangerous cells when everything is forbidden, explaining the unsafe
stationary result even though both constraints exist.

The temporary workaround published the native
`SDKnockbackInAABBRectAwayFromOrigin` directly, with a separate zero-radius
origin constraint. The then-current XCAI reader recognized it as displacement
geometry outside its supported projection allowlist and yields to native BMR
guidance, instead of adding the conflicting sequence constraint. That workaround
has since been removed at the maintainer's request. Native hints,
arena dimensions, deadlines, immunity and manual authority remain intact.
No XCAI source or settings were changed.

Validation uses the actual XCAI assembly via optional `XCAI_DLL` in the native
regression harness. The previous build fails from the recorded targetless
position after XCAI processes its hints. The corrected build selects
(717.25,721.75), whose 22-yalm landing is inside the rectangle, both without
other hazards and with the four staggered Transcendent Shot rows. Tests also
require that native constraints survive and the unsafe starting cell remains
time-restricted. **1,260 native assertions pass with this integration enabled.**
The full current working tree builds in Release with zero errors and six existing
obsolete-gauge warnings.

Candidate source SHA256:
`ed469e998b05f806b5af2bf68c97a9aac8a910d251d034f6d0761b851d6839de`.
Candidate DLL SHA256:
`0c237993a4fba0b957a2e01b70bba9127758504f2daffe0728370ef63e2170a6`.
Candidate and live DevBridge BMR parser MVID agree:
`b2f7ff54-f085-4207-b75a-6274f7c6b1bb`.
XCAI test assembly MVID: `ce72fa68-3a4f-4cc6-9bbf-66d20319d25c`.
The live failed source's parser MVID was
`c4518902-09a3-4e53-9c61-82be46407a14`.

Private receipt and logs: `<isolated validation output>`. The selected diagnostic
scan reached the retained archive's end; full mechanic-window output was
truncated and is not treated as complete. The replay window consumed an
unfinished compressed prefix, so recorder completeness remains false. Event
window references were released after inspection. The temporary workaround's
DLL was confirmed loaded then; that identity does not describe the later build
restoring the original combined constraint. A corrected XCAI build and a new live
Al Zahbi knockback remain required for gameplay acceptance.

## Changes and mechanic contract

- **Leaping Cleave (50481):** the upstream `SimpleKnockbacks` implementation
  draws displacement and warns about dangerous destinations, but has no
  `AddAIHints` implementation. The encounter now supplies a timed forbidden
  zone for starts whose 22-yalm radial landing leaves the existing rectangle.
  It uses the cast's actual location and deadline, independently of an attack
  target. The exact origin is forbidden because its radial direction is
  undefined. Observed immunity must cover impact to suppress the restriction.
  Cast finish/cancellation and helper destruction clear it; duplicate starts
  replace the same helper's source. Arena dimensions remain 25 by 20 half-size
  around (721,720), with no newly invented collision barrier or enlarged hazard.
  Close north/south starts can still land outside the shorter arena dimension;
  east/west and diagonal safe pockets remain available.
- **Dance to Dust (50096/50097):** follow-up forecasts now advance 9 yalms at
  nominal 1.1-second intervals, replacing 8 yalms and 2 seconds. Each helper
  owns its line, so packet matching does not depend on approximate spatial
  coincidence. Final explosions remove their line instead of leaving exhausted
  entries and phantom AOEs. Cancelled initial casts and helper destruction
  remove only that source; repeated starts replace it. The existing 7-yalm
  radius and two north/south versus three other explosions are retained.
- **Wave 1:** Abazohn (0x4DA6), the original module's primary actor, is created
  only with wave 2. A separately registered Acrolith (0x4DA4) module activates
  Earthshatter and add handling during wave 1. It ends when that wave's Acroliths
  and Lamia Rovers are dead/destroyed. Primary Acrolith death alone does not
  remove surviving enemies' guidance. Later-wave registration remains intact.
- **Fulmination Khryseos (50093):** activate the already-defined interrupt
  component. It was omitted from the state machine. The reviewed training
  recording has no occurrence of this action, so activation is verified but
  a real cast/interrupt outcome is still pending.

These outputs constrain native BMR navigation and therefore reach BMR AI and
XCAI through their existing encounter-hint integration. No XCAI source, player
controls, rotation settings or installed plugin directory is changed.

## Evidence

Archive recording `216d4cfe585ec3ef6501bf9b10b940c76a207a26a6551fd872a54e7dd10e4318`
is the main training original, digest
`eaf65885e8c4dc128618d6fdd8633504243f643b2370df157c7da99794b048db`.
It was recorded with BMR 7.5.6.35 and game 2026.09.15.0000.0000.

- 363926: Abazohn creation, after wave-1 actors and Earthshatter.
- 379025: Leaping Cleave starts at approximately (720.9735,719.99695).
- 383640/383641: action followed by cast end at the same recorded timestamp.
  Effect hex `0x007400000000001f` links the current source's ActionEffect
  layout to Knockback row 116, distance 22 and direction 0. The matching
  September game dataset reports speed 30. The archive's automatic effect
  interpreter refuses the .34/.35 reference mismatch; the row link was reviewed
  explicitly against this checkout's `ActionEffect` field layout.
- 474082–474098 and 477555–477570: intercardinal then cardinal exaflare starts.
  Helper 437 moves at 479301 and 480254 by approximately nine yalms per step;
  actions 478541, 479332 and 480309 are approximately 1.09/1.11 seconds apart.
  Helper 434 moves to (720,736) at 482233, supporting the nine-yalm cardinal
  step. These observations establish nominal recorded helper motion and effect
  cadence, not independent lethal-boundary or server-snapshot measurements.
- Transcendent Shot starts in two groups of four, separated by about two
  seconds (374850–374854, 376965–376968). Its existing four-at-a-time display
  limit is retained; it is intentional staggering in these observations.

Recording `6c85fbd4c236802850758b3dfc4c44c258c691b1eca2fe29d33329c016330ec0`
(digest `fa629e1f29c71a94fb27654d626b9b20e697172eb3805b2b6196428a41c8bf90`)
was also inspected during authoring. It contains the opposite exaflare order,
cardinal then intercardinal, and is training rather than a holdout.

The separately reserved original
`bbedd3081c1583ad3a5c34c3f8319e0ef56001267c9774196ba1da49fcfb27bb`
(digest `6d02e9c89109687c97d8bf9d7398d178099a4b45b2b06d4c009f55b072100dec`)
was evaluated after the knockback/exaflare implementation was frozen. It did
not cause parameter tuning. The final build was replayed against this original
again after adding separate wave-1 registration. Its Leaping Cleave starts at
422018; exaflare starts are 535400–535406 and 539590–539611, with final follow-up
actions through 546607. All corresponding pages reached their end.

## Validation and limits

- Final Release build succeeds with the pinned SDK 10.0.401 and API 15
  development assemblies: zero errors, six existing obsolete-gauge warnings.
- Native synthetic suite: **1,213 assertions pass**, including 1,000 independent
  rectangular landing probes, four targetless native-navigation starts,
  immunity/cancellation/destruction, exaflare lifecycle and wave-1 registration,
  primary death and retirement. These are synthetic, not physical evidence.
- Baseline replay supplies four line-AOE constraints and no knockback constraint
  at 379025. Candidate supplies those four plus landing safety. The new zone
  forbids north/far-east/origin probes and allows east/diagonal probes.
- At 483200 the baseline retains eight Dance to Dust AOEs; the candidate has
  zero after the final helper effects and the following module update.
- The final held-out replay has twelve available samples, zero module errors,
  no missing samples and no unsupported output kinds. At 547000 Dance to Dust
  has no remaining AOEs. The interrupt component is active in every sampled
  main-module state.
- Wave-1 replay activates at 341359, emits Earthshatter at 353608 and removes
  it at 357545. It deactivates/unloads at 367693 with zero components. The later
  wave-2 sample intentionally reports the wave-1 module unavailable.
- Both replay originals' file digests were checked against the archive claims.
  The source projections are schema 1 and omit operations, including HP/MP.
  The native runner consumes the originals; its offline game dataset is
  2026.10.06.0000.0000, differing from the recordings. Consequently reports
  retain `export_complete=false` and `module_verified=false`. Do not present
  these selected lifecycle/predicate checks as whole-encounter certification.
- Physical arena/7-yalm AOE boundaries, exact server snapshots, every overlap,
  cancellation order and actual in-duty BMR/XCAI execution remain unverified.
  No gameplay actions or manual reload were performed. The development DLL is
  built at the documented watched output; loaded dev-plugin state alone does
  not identify the exact loaded assembly or prove gameplay acceptance.

Final module source SHA256:
`bd52e59d9a1b0dbbff0b8b9580368e3ae0bb636ad0aea43d703d78a2edcbd21a`.
Evaluated DLL SHA256 (before another local encounter build updated the shared output):
`245e0e30a853f01dcb874d495d61a803e4fd23a6950534b12917707f9a0223c7`.
Evaluated DLL MVID: `6042b42c-aa90-4bbc-88a0-1dcaec44d8d3`.
The Al Zahbi source hash remains unchanged in the subsequent workspace build.
That current workspace also builds successfully and passes **1,251 native
synthetic assertions**, including additional separately owned Promathia cases.
Its frozen test DLL SHA256 is
`51d0b09dfaf93f461866530bbf1e5f5418a30b4936780c5721fcdbdfed766f3b`.
Private runner reports and build/test logs are under `<isolated validation output>`.
