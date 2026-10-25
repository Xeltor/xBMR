# Required recovery and panic exposure — 2026-10-25

## Required movement cannot wait for optimization

Neo Garula / Noise Complaint's 2026-10-25 death capture used loaded MVID
`9c41dd2b-ef1c-425f-b50f-ac1df8b4bfb6`, version `7.5.6.3522`.
The bridge segment `892e32b1ab5744dd8e88a5515a28e58e` has no dropped/truncated
start in the inspected window. Matching local requests and action source
sequences identify the player in the recording. The flushed recording prefix
covered the death; its unfinished tail does not establish whole-source EOF.

From approximately 21:03:32.96 to 21:03:36.72 UTC, the player remained at
(469.65335, -361.35526) while fresh native recovery supplied an escape and the
host returned `fresh native departure permits further optimization`.
Leeway fell from about 3.11 seconds to 0.014 seconds. Movement began with
negative leeway. When the first attack resolved, the host again deferred the
remaining short escape. Recorded effects list the player for Rumble (`41179`)
at recorded time 22:03:38.6863014 and Lightning Crossing Charge (`42984`) at
22:03:39.8710807; HP reached zero at 22:03:40.5481749. Client event delivery and
HP updates are not exact server snapshots or independent damage attribution.

`CanDeferRecovery` now requires an indefinitely safe current position. A pending
optimizer cannot suspend a required native escape. The fresh native leeway is
still passed to the movement controller, preserving a safe current cast where
its actual remaining duration fits. Explicit optimized plans can still include
fully validated waits. This does not change saved opt-outs or manual authority.

`RequiredRecoveryProgress` checks positive-leeway required movement, a changed
mechanic after the previous one resolves, and retained cast leeway.

## Panic recovery minimizes predicted warning exposure

The native raster's earliest deadline alone does not distinguish one missed
warning from two overlapping missed warnings. While the current footprint has
a deadline and multiple warnings are present, the framework thread captures
independent immutable warning bitmaps with the existing native rasterizer.
Workers receive no live shapes or actor handles. Changed independent overlap
geometry retires a retained escape even if aggregate earliest deadlines match.

Recovery assesses every segment of its complete witness with `TimedTraversal`
and cumulative native walking time against each warning's own deadline.
Ending inside a warning also counts as unresolved exposure. An eventual safe
endpoint cannot conceal a second warning crossed too late. Known future zones
crossed and exited in time do not add exposure. No expiry is invented.

When the native proposal has exposure, further native ThetaStar passes block
avoidable failed warning footprints. Recovery prefers fewer predicted warning
exposures, then a clear endpoint and shorter travel. It can explicitly hold
in one unavoidable warning rather than cross two; a panic hold is never
reported as safe arrival. Physical bounds, collision, source freshness and
manual authority still apply. Diagnostics expose `RecoveryExposureCount` and
`RecoveryHoldForFewerExposures` separately from native route safety.

This is best-found emergency recovery, not an exhaustive global optimum or an
actual damage model. Independent authored warning leaves are counted; incomplete
or duplicated module hints and unknown server snapshots limit what those counts
mean. Teleporter recovery is left to its existing native path because an ordinary
walking witness cannot certify teleporter timing. No fixed warning/expansion
quota is added: each improvement blocks another captured warning, and production
alternative search is bounded by the existing 250 ms native input freshness
ceiling. Synthetic 10,000-cell capture/recovery checks cover 3 and 8 leaves;
their measured costs are reported by the regression run, not inferred live FPS.

`PanicRecovery` covers a safe endpoint reached through two warnings, a complete
one-warning detour, physical walls requiring an honest lower-exposure hold,
manual authority, preserved future-timer traversal and independent-map changes.
The Unbridled recording-derived fixture additionally validates the complete
small escape against all original native warning deadlines. Sprint and mobility
regressions remain adjacent checks; no unobserved dash or Sprint use is claimed
for either recorded death.

## Offline validation

The complete current working tree built successfully for Release with no errors
(six existing obsolete-API warnings). The focused urgency/Unbridled/recovery,
full-route, early-clearance, Sprint and mobility checks passed 170 assertions.
The 10,000-cell synthetic run measured warm capture maxima of 1.198 ms and
recovery maxima of 0.821 ms across 3/8 independent leaves. These are offline
measurements, not client frame-time or encounter acceptance claims.

The complete native movement regression run passed 143,427 assertions on build
MVID `3e8556a0-b9ae-4933-96ed-0bbb23748191` (`7.5.6.3522`).

After an observed out-of-combat window, the validated watched development output
was refreshed. DevBridge verified automatic reload to the exact MVID above.
No representative post-fix fight has yet established live gameplay acceptance.
