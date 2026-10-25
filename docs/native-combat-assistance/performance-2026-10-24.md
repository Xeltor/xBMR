# AI efficiency pass — 24 October 2026

The maintainer reported lower FPS with AI enabled. A stationary, out-of-combat
comparison reproduced lower framework update throughput with AI following and
linked RSR enabled. The changes below remove repeated idle work and share only
synchronous plugin observations. No planning budget, response interval or cache
expiry was introduced.

## Changes

- Skip navigation rasterization, physical capture, map copies and worker
  submission when there is no encounter, engagement, target, movement intent,
  goal, authored arena, projection, warning, obstacle or other navigation demand.
  Release any previous plan. Recheck these conditions on every update; a target,
  follow goal, explicit destination or hazard immediately restores normal capture.
- In the RSR selection callback, retain original idle selection without wrapping
  every action predicate when the fresh target frame has no current/forced/
  recovery target or eligible engaged enemy. This does not skip RSR's callback.
- Wire the existing scoped plugin-inventory observation into the xBMR update and
  RSR selection callback. Repeated consumers share one inventory only within that
  synchronous scope. Nested callbacks invalidate the parent snapshot; disposal
  clears it. Loaded-plugin checks and mutable rotation/action reads remain live.
- Cache RSR assembly metadata by the loaded provider assembly identity. Read the
  current rotation on every access, and invalidate metadata on unload/replacement.
- Transfer newly captured, unshared map/score buffers into the immutable planning
  context instead of cloning them again. Ordinary context callers retain the
  defensive copy boundary. Reuse only the separate framework inspection map and
  the single worker's recovery scratch; neither is shared with frozen contexts
  or published route witnesses.
- Evaluate native goals once on the framework thread using native corner
  coordinates, then apply that same frozen aggregate to recovery through its
  original casting/urgent-warning gate. Preserve unscored-cell semantics and
  skip unused per-goal leaf arrays on this aggregate-only path. Other consumers
  retain the independent-leaf capture option. Private capture buffers have no
  exposed mutable references.
- Avoid extra strict-map/priority copies for an absent native cushion, and skip
  the parallel cushion calculation when the freshly captured map has no warning
  or physical blocked cells. Nonempty constraints retain the native calculation.
- Normalize a newly created search candidate in place before admission instead
  of making up to six temporary copies of its large record. Admitted candidates
  and parent chains remain unchanged. Replace per-comparison ZIP/enumerator
  allocations with the same elementwise timing and value comparisons. Resource,
  action, timing, safety and dominance requirements retain their meaning.
- Finish search when a complete permanent stationary plan demonstrably reaches
  every applicable objective ceiling: earliest instantaneous current execution
  and recovery, maximal action/native/presentation values, useful range/contact,
  continuity and zero movement/resource spending. Conditional casts, future
  actions, resource grants, healing, fields, zones, displacement and unresolved
  destinations remain outside this narrow proof. This is completion by proof,
  with the existing freshness/departure and explicit test limits retained.
- Expose the last framework-thread planning call's time, allocations and cell
  count, previous xBMR update time and last finished/cancelled worker costs through
  the existing bounded current diagnostic snapshot. DevBridge continues to own
  sampling and history.

## Live comparison

Same stationary DRG, zone 339, no target, no combat or movement. Each sample used
32 framework observations spaced about 200 ms apart, spanning 6.6–6.7 seconds.
Rates below are framework ticks divided by observation wall time. This is evidence
about game update throughput, not a direct measurement of GPU/render time.

| Build and state | Updates/second | Sampled update delta, median / maximum |
| --- | ---: | ---: |
| Original, AI + linked RSR enabled | 52.27 | 19 / 24 ms |
| Original, AI + linked RSR disabled | 59.98 | 16 / 19 ms |
| Original, enabled again | 53.56 | 19 / 23 ms |
| Idle performance build, enabled, first sample after reload | 52.50 | 18 / 32 ms |
| Idle performance build, enabled, warmed sample | 59.99 | 16 / 19 ms |
| Idle performance build, enabled, warmed repeat | 60.00 | 16 / 23 ms |

The first sample after each development reload remained slower. Two warmed
samples on the intermediate performance build also reached 59.84 and 59.77
updates/second. The reload/startup effect has not been attributed to a specific
component. The original enabled baseline was already running before this pass;
its enabled/disabled/enabled comparison preceded the reloads.

The native `/bmrai off` control also turns linked RSR off. This therefore compares
the combined enabled path, rather than isolating RSR's own cost. The idle build
remained enabled with RSR Henched throughout its measurements, and was left in
that original state. The maintainer subsequently started a dummy fight for combat
measurement; combat reloads were explicitly requested by the maintainer.

Twelve warmed samples on the idle build recorded mean xBMR update time of
0.264 ms, maximum 0.370 ms. The planning fast path took at most 0.0032 ms,
allocated 72 bytes and processed zero navigation cells. These current-call
measurements exclude separate RSR callback time, worker CPU and rendering.

## Earlier validation and limits

The single-goal-capture working tree built successfully in Release. The native harness passed
43,345 assertions, including immediate restoration of navigation for targets,
engagement, goals, warnings, forced movement and authored geometry; idle target
constraints; synchronous observation freshness; unchanged cushion scores; and
independent safety/route publications across buffer reuse; and 192 comparisons of
objective-ceiling completion with unrestricted search. Installed RSR 7.5.7.7 contract
checks and all nine repository tests passed. The build retains the existing six
obsolete-gauge warnings.

The validated development DLL was observed loaded with MVID
`99bf5f35-006b-425c-8a25-d87f5af52421`, SHA-256
`fdf3872a4285b7a888bd5eeee4ad0c4dda999a0ec28f8512f70ce0afdeb8538c`.
The original live baseline used MVID
`33a2f701-ded3-42f0-9c58-67d872fd1889`. AI following, RSR Henched and no assistance
failure were verified after the final reload.

The idle comparison used development MVID `dc22bcd1-08ff-4669-885b-33def75cc1bf`.
In the maintainer's later dummy fight, 32 samples on that build recorded planning
allocations averaging 1,541,795 bytes per call and median planning time of 1.401 ms.
The first buffer optimization build (`be992a01-7735-430c-96ab-aba5f6bb10f2`) reduced
these to 890,305 bytes and 1.070 ms over 48 warmed samples. Median context setup
fell from 0.482 to 0.369 ms. This reduction did not restore total combat throughput:
the initial dummy sample was 50.98 updates/second, and the warmed buffer-build
sample was 49.31. That is a remaining performance defect, not a demonstrated FPS
gain. Initial post-reload sampling was excluded from those warmed comparisons.

Worker diagnostics added in the next build
(`ea782115-bd33-47d7-8a94-2fe9e1b9717e`) exposed a much larger allocation source:
48 observations of the last completed search had median allocation of 95.95 MB,
95th percentile 321.63 MB, and median duration 248.94 ms. These are sampled last-job
values, not a complete distribution of unique jobs. The jobs frequently reached
the existing freshness ceiling. This motivated the candidate-normalization and
dominance-comparison changes; the search space and freshness limit were retained.

After candidate allocation changes, a subsequent 48-observation sample had median
worker allocation of 72.63 MB. After objective-ceiling completion was added
(`fac28177-d7d0-4dcb-80f3-d6f17638d767`), the median sampled worker was 0.990 ms and
54,768 bytes. One of those 48 observations still represented a 249 ms/80 MB job;
nontrivial alternatives correctly retain full search. The corresponding two
throughput samples were 52.33 and 47.97 updates/second, so worker improvements
alone did not eliminate the combat frame-rate drop.

The maintainer then disabled xBMR AI while leaving RSR attacking in Auto mode.
That sample reached 59.87 updates/second, with mean xBMR update time 0.303 ms.
Assistance was restored using the newly updated bridge's command-testing tool,
and actual enabled state was re-read. The RSR mode chosen for this comparison
was Auto, whereas the original idle baseline used Henched.

The final single-goal-capture build reduced sampled framework planning allocation
to 659,397 bytes on average over 48 combat observations, with median input capture
0.275 ms and median planning 1.065 ms. Target-constraint setup had median cost
0.305 ms. More complex worker jobs remained visible during this window: median
38.85 ms, with a 250.27 ms 95th percentile. Objective-ceiling completion applies
only when all its requirements are actually proved.

A longer enabled combat sample covered 33.70 seconds and 1,771 framework ticks:
52.55 updates/second, sampled delta median 18 ms and maximum 37 ms. Short samples
were 57.49, 53.00 and 51.30 updates/second. These results do not establish a
sustained combat FPS improvement over the initial short dummy sample. The CPU
and allocation reductions are measured; the residual frame-rate drop remains
under investigation. The user's camera assistance was enabled and the decision
overlay was disabled during these observations.

These diagnostic samples are not a full per-frame trace, and last-call planning
and previous-update totals refer to adjacent calls. The idle navigation shortcut
deliberately does not apply in combat. Existing safety, route freshness,
assignment and action checks were retained; this result does not establish new
combat acceptance or resolve the unrelated open gameplay items in `TODO.md`.

## 25 October checkpoint: camera and pathing arithmetic

Automatic camera remains enabled. The maintainer's temporary camera-off comparison
was diagnostic only: 55.55 framework updates/second versus the preceding 52.55
camera-enabled sample. Disabling the feature is not the proposed fix.

The camera now reuses its projection basis within each view and skips only views
whose optimistic score cannot improve the incumbent. Hysteresis requires a
separate conservative bound comparison: a slightly higher fight bound must not
hide an actual equal-fight candidate with better party framing. Visibility tests
proceed by priority and stop when the remaining optimistic result cannot win.
Existing candidate generation, input ownership, smoothing, zoom limits, physical
checks and the 250 ms evaluation interval remain unchanged.

The latest source also shares exactly identical collision rays only inside one
synchronous view evaluation. Origin, direction and length all belong to the key;
both hit presence and hit point are retained. Scope disposal clears the samples,
including on an exception. Execution-time queries always read native collision
afresh. This removes repeated baseline and overlapping transition probes without
reusing collision evidence across frames.

A fresh enabled baseline on October 25 measured 56.23 updates/second over
34.70 seconds; the first camera-pruning build measured 56.48 over 34.51 seconds.
This does not establish a meaningful overall speedup. Its median sampled camera
evaluation was 11.35 ms. The next combined arrival/camera build recorded 14.12 ms
median evaluation and 0.0031 ms median input-hook work; pruning alone did not
resolve the camera cost. These observations motivated the exact-ray reuse above.
The latest reuse build has not yet been measured live at this checkpoint.

Pathing arithmetic changes retain the same comparison rules: active queue labels
are checked by identity instead of recursive record/parent equality, cooldown
schedule comparisons avoid temporary ZIP iterators, and packed resource counts
use guarded arithmetic to compare all sixteen independent four-bit slots.
All charge pairs in every slot and 10,000 mixed inventories match a separate
scalar reference. A 1,048,576-pair equal/dominating/missing-charge microbenchmark
took 61.03 ms with the scalar reference and 3.86 ms with the packed comparison.
This is a primitive speedup, not a whole-search or FPS claim. The 6,400-cell search
still expanded 6,665 states, with median 52.35 ms versus 53.80 ms in the preceding
combined build; that small difference alone does not establish a sustained gain.

The checkpoint includes the separately documented direct-arrival and large-boss
positioning fixes. The latest Release DLL is MVID
`5bf068de-fc50-4ce2-9602-946dabf47703`, SHA-256
`98e8441f891f9591fbcf3371e94d9342caba17d89b7158ecb7f640705261cf66`.
It passes 54,506 native assertions and the installed RSR contracts. Nine repository
tests also passed. The live client still uses the preceding combined MVID
`c1079557-4f86-46a6-bc45-9d50c3dcaa1c`; AI, RSR Henched and automatic camera were
verified enabled with no assistance failure. Final camera-reuse live validation
and broader pathfinding-algorithm evaluation remain unfinished.

Stable 7.5.6.35.20's immutable source tag and public assets are unchanged.

### Post-checkpoint live verification

Commit `bc579702e` was created before further algorithm changes. Its validated
DLL (`5bf068de-fc50-4ce2-9602-946dabf47703`) subsequently auto-reloaded, and the
maintainer-authorized dummy test ran with AI, RSR Henched and automatic camera on.
Forty-eight warmed diagnostic observations recorded median camera evaluation
0.361 ms, median input-hook work 0.0037 ms, and median full xBMR update 1.327 ms
(95th percentile 3.576 ms). The settled view pruned all 17 alternatives; the last
evaluation made 29 native rays and reused none. This sample therefore exercises
pruning, not a demonstrated live ray-cache saving. The camera yaw differed from
the earlier expensive view, so these are not controlled equivalent-view timings.

A separate 34.979-second sample recorded 1,909 framework ticks: 54.58 updates per
second, median sampled delta 18 ms and maximum 44 ms. The overall combat frame-rate
drop remains unresolved; neither this measurement nor the arithmetic benchmark
establishes a full FPS fix. Afterward, AI and RSR were restored to their observed
off state and the test target was cleared. Automatic camera's saved preference
remains enabled. No assistance failure was observed. Public assets are unchanged.
