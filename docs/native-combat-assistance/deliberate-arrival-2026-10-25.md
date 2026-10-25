# Choose the useful arrival before walking

## Recorded evidence

Reviewed the October 24 Mistwake DRG combat captures through the historical
analysis service, with each query pinned to its imported revision:

| Capture | Revision | UTC interval |
| --- | --- | --- |
| `1906eba3f19c4bec8d30053693993987` | `01cddd19902f4607bc98633530661131` | 20:29:46–20:32:47 |
| `1eb54a8ba1264d368e511bcf4d1de7ee` | `00f3ebcd2a0148108fa50cfb9018b3a1` | 20:38:23–20:41:21 |
| `0ee37f1dec174261a874365945281d3a` | `68ddec9bdbd04b31a3b23714585e1296` | 19:55:47–20:00:13 |
| `730143d2d44e440ab085fbf43de45f2b` | `8f6ebbc3e2d1442d9f7f52a675de980e` | 20:47:42–20:52:08 |

The bounded capture inventory also contained trash intervals and later Mist dummy
sessions. These four boss intervals were selected for detailed movement review;
this is not a claim to have reviewed every recording from that day.

At 19:55:48.310644 UTC, the recorded plan walked from approximately
`(281.509, -621.319)` to `(281.014, -630.477)`, while the target observation was
`(280.995, -630.030)`: about 0.45 units from the target center. Destination
presentation was `-0.9350752`. At 19:55:51.718653, another walk went from
`(280.793, -629.507)` to `(280.891, -627.980)`.

At 20:38:44.173492, another plan walked from `(283.255, -290.117)` to
`(281.049, -284.001)` beside target center `(280.995, -285.023)`. At
20:38:49.440269 it walked again from approximately that arrival to
`(282.023, -284.550)`. Center-directed arrivals also appeared in the first boss
interval, including 20:32:33.711088, followed by further position adjustments.

These are sampled provider decisions and stage origins, not continuous physical
trajectories. Target observations can move, and the captures do not freeze the
entire hazard map or prove that every correction had the same cause. Query
pagination was exhausted for the selected revisions; returned sample counts
were lower than their original frame counts. No unique gameplay rate or exact
snapshot timing is inferred.

## Source finding and change

`JointMovementSearch` had a preliminary-pass shortcut that accepted maximal
native priority and timely attack/range recovery without comparing presentation.
A complete native seed could therefore finish the initial search under the boss
even when another arrival preserved the same execution window. The shortcut now
also requires the presentation ceiling. Otherwise the first pass compares the
reachable alternatives in the same readiness window before returning. The
range-only frontier includes the observed readiness time, not just the seed's
arrival time.

Optional native range recovery also declines a destination with worse
presentation than the origin while the worker compares arrivals. The existing
fresh, mandatory departure path still executes urgent native recovery. This
does not wait through a hazard deadline for a comfortable position.

Archived XCAI references: `Combat/BossCenterAvoidanceController.cs` and
`Combat/SocialArrivalPolicy.cs`. XCAI capped center clearance at 4.35 units, but
its standalone controller first required being inside the center region and a
dwell observation; it also suppressed the preference during native movement.
The native presentation field now uses its bounded radius rule
`clamp(hitbox + 0.35, 1.25, 4.35)` from the outset. Huge hitboxes therefore allow
close interior positions instead of encouraging travel to their outer edge.
The rule is a soft preference, not a forbidden region. The zero penalty beyond
clearance avoids a perpetual outward gradient.

Native safety, assignments, current actions and recipients, positional geometry,
range recovery, resources and beneficial ground effects retain their existing
priority over presentation. A necessary under-boss pocket remains usable.
The standalone XCAI project is unchanged.

## Validation scope

`DeliberateArrival` exercises a center-directed native seed, the first range and
positional arrival, actual rear/flank/any execution geometry, a blocked clear
endpoint, immediate-action timing, elective fallback, and huge-boss radius
semantics. Existing native regression coverage checks urgent escape, action
locks, resources, recipients and freshness. Build and offline checks are
separate from live gameplay acceptance; no watched or installed DLL is changed.

Combined current-tree Release build and native harness build passed, followed
by **54,498 native assertions** and the installed RSR contract checks. Validated
plugin MVID: `c1079557-4f86-46a6-bc45-9d50c3dcaa1c`; SHA-256:
`584b56c8bacd30030c7bc2ba638312ea92becd4549671607b1a2a5dbf69f2f09`.
The concurrent efficiency task owns the subsequent development reload and dummy
performance test. That is separate from live boss-fight arrival acceptance.
