# Exact pathing math — 25 October 2026

The acceptance rule is faster computation with the same or better decisions,
not weaker mechanics, fewer recipients, coarser coordinates or another work quota.
This pass starts from `9ee973961`. It changes shared traversal and the worker's
frontier index, not the objective order, available actions or execution authority.

## Implemented algorithms

### Separable swept-cell intervals

The previous traversal clips every cell in the expanded bounding rectangle of a
segment. For a long diagonal, most of those cells cannot intersect the route.

For each expanded cell, compute the segment-parameter intervals `Ix` and `Iy`
inside its horizontal and vertical slabs. The cell is touched precisely when
`Ix ∩ Iy ∩ [0,1]` is nonempty. Its exit parameter is the minimum of the three
upper bounds. Horizontal intervals are monotone in travel order, so two binary
searches select the exact interval range intersecting each row. Visit those
cells in the previous row/column order and apply the original deadline expression
`limit - elapsed - duration * exit`. The footprint expansion, inclusive corner
contacts, float divisions and strict positive-slack requirement are unchanged.

This reduces rectangular work `O(W*H)` to `O(W + H*log(W) + K)`, where `K` is the
number of touched cells. Small or narrow rectangles retain direct clipping.
The six-cell-per-axis crossover selects an algorithm; it never truncates work.
The first 128 horizontal intervals use at most 1 KiB of stack; larger sweeps use
a returned pooled buffer, with no map or result retained across calls.

`TraversalMath` compares verdict **and exact slack**, including failed requests,
against the previous rectangle implementation. Cases cover rotated maps, three
resolutions, both travel directions, zero movement/duration, physical-only checks,
nonzero and wide footprints, NaN/infinite blocked cells, isolated corner contacts
and one-ULP deadline boundaries. Independent diagonal corner assertions supplement
the differential reference. This is a reorganization of the shared native safety
operation, not a second hazard model.

### Partitioned Pareto frontiers

The existing dominance relation requires equality of cell, completed-action mask,
displacement phase, last stage kind, focus target and target-choice mask. Therefore
`a dominates b` implies `key(a) == key(b)`. Indexing by this key omits only comparisons
that would necessarily return false. Within each partition the full original
dominance test still checks exact position, delivery/recovery times, independent
values, resources, safety, waiting feasibility and other execution state.

Admission, removal, queue order and incumbent selection stay identical. Queue
retirement now uses an explicit membership bit instead of searching an admitted
list; it is never movement evidence and does not change the frozen parent route.
The test-only `partitionFrontier: false` path retains the unpartitioned reference.
Tests compare exact stages, destination, expansion count, action values/times,
recovery and inventory across warning maps and resource-recharge cases.

## Research considered

| Method | Applicability here |
| --- | --- |
| [SIPP, Phillips and Likhachev, 2011](https://www.cs.cmu.edu/~maxim/files/sipp_icra11.pdf) | Safe intervals remove redundant time states when earlier arrival can reproduce later arrival. xBMR already requires safe waiting for dominance. It must additionally retain differing actions, recipients and replenishing resources; one earliest label per cell would be wrong. |
| [MO-SIPP, Ren et al., 2022](https://publications.ri.cmu.edu/storage/publications/2022/07/ren22_mosipp_RAL_IROS22.pdf) | Multiple nondominated labels are appropriate. Its fixed graph and additive nonnegative cost-vector model does not directly represent our ordered execution objectives and action-dependent transitions. Use exact frontier reasoning, not an unsupported claim that the full paper's guarantee transfers. |
| [Temporal Jump Point Search, Hu et al., 2021](https://webdocs.cs.ualberta.ca/~nathanst/papers/hu2021jpst.pdf) | The paper proves symmetry pruning for four-connected timed grid moves. Our exact endpoints, diagonal swept footprints, casts, speed effects and mobility actions break that symmetry. No production jump pruning was introduced. |
| [Distance transforms, Felzenszwalb and Huttenlocher, 2012](https://cs.brown.edu/~pff/papers/dt-final.pdf) | Linear-time nearest-cell fields are useful for repeated grid distance queries. Current forbidden-zone presentation uses eight samples per cell, already linear; an EDT would change its semantics. Raster distance is also not exact clearance to the native polygon. No replacement was justified. |
| [StochSIPP, Kemisetti et al., August 2026](https://arxiv.org/abs/2608.00792) | The abstract's expected-arrival guarantee requires correct independent probabilities and complete outcome generation. The game inputs do not supply those probabilities. Do not invent hazard expiry or confidence distributions to apply it. |
| [AOC-CBS, Combrink et al., August 2026](https://arxiv.org/abs/2608.08175) | The abstract describes exact coordination among multiple planned agents. We control one actor and do not own party trajectories. A multi-agent conflict tree adds a different problem rather than removing our current work. |
| [SIPP-PP with learning-guided repair, IJCAI 2026](https://ijcai.org/proceedings/2026/842) | The published summary concerns coordinated robot fleets. Learned conflict repair is not evidence of equivalent xBMR action/safety decisions; not adopted. |

The implemented interval method is derived from the existing slab-intersection
equations. It uses exact slab overlap instead of importing a center-ray DDA that
could miss footprint or boundary contacts. Recent papers were evaluated for their
assumptions, not adopted merely because they are recent. GPU ray tracing was not
introduced: transferring native game collision geometry and maintaining matching
acceleration structures would be a separate integration, not acceleration of the
existing CPU queries by changing one call.

## Measurement and limits

Managed Release build passed with six existing obsolete-gauge warnings. Native
regressions passed **129,688 assertions**; installed RSR surface, fallback and
target contracts also passed. Validated module ID:
`5ef2015c-af46-4f61-a5de-f28dcee251d3`; DLL SHA256:
`42ebb8b39582e34f12a52ee062fc79b0e1e81b36c5e49a911a1bfc2694464c3a`.

The following paired synthetic timings disable tiered compilation so changes in
JIT tier do not masquerade as algorithm gains. Five measured repetitions follow
warmup; reference/optimized order alternates. Times are medians, not thresholds.

| Traversal workload, 2,000 checks | Rectangle | Interval | Ratio |
| --- | ---: | ---: | ---: |
| Adjacent diagonal | 0.081 ms | 0.084 ms | Effectively unchanged; about 1.5 ns/check difference |
| Five-cell diagonal displacement | 0.460 ms | 0.208 ms | 2.2× |
| 30-cell diagonal displacement | 11.975 ms | 1.259 ms | 9.5× |
| 78-cell diagonal displacement | 72.741 ms | 3.359 ms | 21.7× |
| 150-cell diagonal, pooled buffer | 262.568 ms | 7.467 ms | 35.2× |
| Wide footprint diagonal | 80.093 ms | 9.886 ms | 8.1× |
| Horizontal | 0.814 ms | 0.805 ms | Effectively unchanged |

| Search workload | Unpartitioned | Partitioned | Same work/result |
| --- | ---: | ---: | --- |
| 64 cells, two actions, Sprint, recharge | 409.334 ms | 293.337 ms | 9,810 expansions; SequenceLimit; identical route |
| 256 cells, same features | 157.453 ms | 120.383 ms | 8,098 expansions; StateMemory; identical route |

Those stress searches use a **test-only** 12,000-admission allowance, with freshness
disabled in both variants, to compare equal work. No production quota changed.
The first reaches the existing sequence bound before that allowance. Allocation
rose by 102,816 / 204,464 bytes (about 0.02% / 0.13%) for the additional frontier
lists; the retained time gains were 28.3% / 23.5%. These are deliberately expensive
stress cases, not game-frame latency predictions.

The existing 6,400-cell search completed in median 42.804 ms with 6,665 expansions.
The reconstructed Griffin ten-warning case published its first complete mobility
route in 0.989 ms; isolated dash, backstep and Sprint alternatives all passed.
These separate observations are not controlled before/after speedup claims.

Synthetic speedups do not establish a game FPS improvement. Automatic camera
remains enabled; the earlier combat FPS issue stays open until a comparable live
sample supports closing it.

The normal tiered-runtime repeat also passed all 129,688 assertions. Search
partitioning retained 30.3% / 24.5% time reductions; long diagonals retained large
gains. Small calls are not universally faster: adjacent checks measured 0.294 vs
0.334 ms per 2,000, and horizontal checks 0.799 vs 1.023 ms. That is about 20 / 112 ns
extra per check in that run. The implementation is retained for the substantially
larger savings on broad routes and multi-state searches, not a claim that every
primitive invocation is faster.

## Live comparison and cleanup

The DLL watcher loaded the validated module above while the client was out of
combat. Both samples used Dragoon on the same nearby dummy with AI, RSR and
automatic camera enabled. Each comparison window contains 160 framework reads
at roughly 200 ms spacing and 40 provider reads; initial post-reload samples were
excluded. No local regression process ran during either live window.

| Observation | Previous checkpoint | New math |
| --- | ---: | ---: |
| Framework updates/second | 55.96 | 56.77 |
| Plugin update median | 1.399 ms | 1.378 ms |
| Plugin update sampled p95 | 3.159 ms | 4.733 ms |
| Planning capture median | 0.956 ms | 0.876 ms |
| Last worker duration median | 0.860 ms | 0.751 ms |
| Last worker duration sampled p95 | 249.496 ms | 249.307 ms |
| Camera evaluation median | 0.318 ms | 0.345 ms |

These are framework rates, not direct rendered-FPS measurements. Provider fields
retain the last job/evaluation, so samples are not a distribution of unique jobs.
The actor moved about 0.7 yalms and camera yaw changed about 0.04 radians between
windows; action sequences and cooldowns also differed. The slight rate increase
is insufficient evidence that overall FPS is fixed, especially with unchanged
expensive workers and the higher sampled update tail. No assistance failure was
reported. AI and RSR were restored off, the dummy reset was requested and the
target cleared. This is dummy-runtime validation, not a new dungeon safety claim.


## Walking continuity correction

Live Mistwake trash observations showed repeated Walk → planned-execution Wait
replacements while collision checks stayed clear. One fast current-opportunity
exit considered maximal attack coverage, native priority, presentation and timely
range access sufficient, but omitted the existing committed-destination preference.
That could publish a stationary replacement before exploring an equally timely
continuation of the current walk. The full optimum check already included that
continuity condition; the fast opportunity exit now does too.

The new regression fails on the previous loaded DLL and passes on the correction.
It checks that an equivalent walk continues before the same next-GCD delivery,
without losing a recipient, delaying an immediately ready attack or keeping a
newly blocked destination. No timer tolerance, movement throttle, hazard deadline,
or resource ordering changed. Full native regression: **142,229 assertions**.
Release build passed; watcher-loaded MVID `c3e257dd-9c69-4ed7-8341-2806b4aee042`,
SHA256 `930722bf6815ac6cd1d852721cec798d304e030917a882fe1f57d3377de9569b`.
Reload occurred between combat phases and active AI was restored afterward.
This is one reproduced source of micro-stepping; pack-transition gameplay still
needs comparison before declaring the entire symptom resolved.
