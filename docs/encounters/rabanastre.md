# Rabanastre: lethal-first initial coverage (2026-10-25)

Scope requested by the maintainer: arena boundaries and the most lethal mechanics;
leave the rest for later work. Hashmal, Rofocale and Argath previously had only
commented-out placeholders. They are now registered **WIP** modules, not complete
encounter support. Mateus is unchanged in this pass.

## Evidence and limits

Written guides read for the level-70 normal alliance raid:

- https://ffxiv.guide/sb/the-royal-city-of-rabanastre
- https://ffxiv.consolegameswiki.com/wiki/Royal_City_of_Rabanastre

These corroborate tower fall direction, the flaming-side dash, hammer proximity,
Maverick clones, the figure-eight charge, sword crosses and Coldblood variants.
The wiki and Pocket Guide disagree on some command wording; resolve commands
against assignment statuses and recorded outcomes before implementing them.
Guide descriptions do not establish numerical hitboxes or snapshots.

Training originals, both from the same raid on game 2026.10.16.0000.0000:

- `c2a291f769153180808164095a118b0ae9ecdd6720d283e49022d14179bc2d8b`:
  Mateus/Hashmal, closed source, 572,057 decoded operations.
- `69d8033094eef868782ab876ee6d0879a3b3dde3ab776f211f33a7019b16b8c0`:
  Rofocale/Argath, closed source; archive recording
  `2ed4820aff1b7453ae9845ecb0f8560b5f05a78d3a1d3445321f9bafd8702429`, retained
  revision `15b073c5ca9f3db15daad90604196b26c6ba93276213216185f5e4f192c4f0b5`.
  This is one original across projections, not independent evaluation evidence.

Private extraction retains numeric events, raw operation indexes, matched-version
Action/Omen fields, arena layouts and model vertices. Raw recordings and asset
exports are deliberately outside the repository. The archive's indexed September
sheet dataset mismatches this game version; implementation uses the matching local
October sheet extraction instead. Client-frame observations are not server snapshots.

## Implemented

| Boss | Behavior and evidence | Remaining limit |
|---|---|---|
| Hashmal | Arena at (-320,-46), circle 29.5; matching layout stage origin and original 30-radius reference. Extreme Edge 9680: 66 by 36 rectangle, predicted from cast start. | Unobserved 9678 is deliberately not enabled. Exact lethal rim still uncalibrated. |
| Hashmal | Towerfall 9668: direction-bearing object 0x1EA7DF creates a 40.5 by 10 rectangle with a conservative 9-second activation. Objects at raw 326022, 406374/5, 420406/7 match helper origins/facings on hits 332609, 414199/211, 429288/296. Observed lead 9.1–9.4 seconds. Each object retires independently on its matching hit or destruction. | Castless deadline is an early estimate, not a verified last-safe departure. Other tower appearance effects are not mistaken for falling objects. |
| Hashmal | Tower appearance circles 9666/9838. Earth Hammer warns at the boss's 5-second cast, before the late 1-second helper cast. | Hammer is warning-only; wide towerfall 9674 is unobserved and deliberately not enabled. |
| Rofocale | Corrected layout center (106,-191), circle 29.5. Maverick 9865: 60 by 16 rectangles. Five clones at 178567–178587 and opposing pair at 313933–313936. Cry of Victory 9845/10060: 60-radius, 180-degree cones. | Unobserved 10022 is deliberately not enabled. Trample only provides an early cast warning; its moving figure-eight is not in AI hazards yet. |
| Argath | Corrected layout center (106,-386), square inner bound of half-width 24, replacing the placeholder circle at (106,-385). Heartless 9778: eight concurrent 61 by 11 rectangles, starts 427185–427203, hits 431325–431349. Soulfix 9770: radius 12. | 24 is a conservative inner-platform policy, not the measured lethal edge. Model extends to approximately +/-29; exact rim and phase-dependent death-wall inset need calibration. |
| Argath | Both Coldblood casts give distinct early instructions (9767 flee, 9766 enter the safe circle). Gnawing Dread publishes the native misdirection special mode. | Coldblood instructions are text-only; safe-radius geometry remains unimplemented. |

Dimensions originate from matching Action fields and existing reference conventions
(including hitbox extension where applicable), corroborated by written mechanics;
they have not been independently measured against server hit boundaries. All
implemented risky shapes supply timed AI forbidden zones through native components.
All simultaneous Maverick lanes/Heartless rays remain visible because they resolve
together; no later sequence is painted across the arena. Tower predictions clear
individually. No live radar or gameplay acceptance is claimed.

## Disabled-source audit

History commit `8361ea25d` is titled "commented out some empty modules": these
were empty scaffolds, not previously working encounter implementations. Their
enum comments still do not establish correct geometry or safe behavior.
Unobserved Extreme Edge 9678, Maverick 10022 and wide Towerfall 9674 are left
inactive until their own evidence supports handling. Matching local sheet fields
support only the fields extracted, not full behavioral or physical validation.

## TODO handoff, in priority order

- [ ] Validate unobserved 9678/10022/9674 independently before enabling them;
  do not inherit handling or geometry merely from the old commented enums.

- [ ] **Argath lethal edge:** calibrate the square platform and later purple death
  wall, including transition/reset; retain the conservative inner bound until then.
  Layout `z3r1_a4_boss4.mdl` is placed at (106,-679,-386). Do not use player positions
  as boundary evidence. Check whether the inset excludes necessary safe pockets.
- [ ] **Coldblood:** decode omen 157 (`m0446show01o0h`) for the donut inner radius,
  corroborate with damage/position evidence, then add timed AI geometry. Cast 9766
  at 534783, impact 543248. Proximity 9767 starts 426080, impact 433341; establish
  a defensible falloff policy instead of forbidding the entire 66-radius arena.
  Include simultaneous Heartless, misdirection and the death wall.
- [ ] **Hashmal hammers:** predict individual shadow origins from object 0x1EA7E1;
  main source 345871 -> helper 9676 at 354397/355088. Calibrate proximity falloff
  and later simultaneous shadows; never wait for the 1-second helper cast.
- [ ] **Rofocale Trample:** two observed rotations start 165021 and 273436. Hits
  9847–9855 span 167828–169910 and 276759–279227. Establish both loop centers,
  inner radius, connecting rectangles, per-step deadlines and cleanup. Keep the
  full sequence internally but show only useful immediate/next previews.
- [ ] **Argath commands:** resolve masks 9753/9754, statuses 1445/1446 and player
  commands 1424/1425. Implement actor-specific gaze inversion and stay/move timing;
  test replacement, death, recreation and both masks. Text alone is insufficient.
- [ ] **Argath floor squares and Unrelenting:** decode map/state changes and omen
  158 cone angle; preserve narrow safe gaps. Add meteor towers, marked sword baits
  before Heartless appears, Shade/Shards targeting and ultimate warnings.
- [ ] **Hashmal remaining mechanics:** central tower collision/knockback,
  Submission Tower persistent fire, Falling Boulder stacks versus Falling Rock
  spreads, sand spheres/golems and enrage. Check preexisting objects on activation.
- [ ] **Rofocale remaining mechanics:** Heavenly Subjugation, add-prison boundaries,
  Embrace traps, Dark Geas interactions, proximity falloff, baits and damage hints.
- [ ] **Mateus audit:** inspect the first original for missed warnings/timing and
  IceSpiral's hardcoded coordinates. Reopen maturity if a material defect is found.
- [ ] Fresh independent recordings for unobserved variants, cancellation, wipe and
  re-entry; verify physical geometry, warning lead versus last-safe response, radar
  readability, and live assistance behavior. Build/replay success is separate.

## Focused validation

`tests/ModuleRegression/Rabanastre.cs` covers generated component activation,
simultaneous/duplicate tower creation, early timing, per-instance/out-of-order
retirement, disappearance, lane width boundaries, AI publication and cast cleanup.
These are synthetic implementation checks, not physical measurements.
