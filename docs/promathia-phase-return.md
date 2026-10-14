# Promathia island completion and phase return

Reopened on 2026-10-14 following the user's reports of repeated transfers after
all receptacles die, missing small black pools after the islands, and the
following push knocking players off the restored arena. The prior Contributed
label is returned to WIP while this material safety report is repaired.

## Written guides and implementation contract

Consulted the Promathia sections of the normal level-100 Windurst alliance raid:

- https://ffxiv.consolegameswiki.com/wiki/Windurst:_The_Third_Walk
  describes the three False Genesis islands, corner barriers, Memory Receptacles,
  active teleporters, Deadly Rebirth's arena restoration, knockback and eight-second
  stun, and the subsequent paired Empty Salvation puddles and traveling orbs.
- https://www.icy-veins.com/ffxiv/windurst-the-third-walk-alliance-raid-guide
  corroborates the island/support sequence, Heavy puddles and subsequent traveling
  circles. Its Deadly Rebirth description mentions damage/stun but omits knockback.

These guides do not establish exact geometry, displacement direction or server
snapshots. The wiki's three-second System Lock differs from approximately four
seconds in recordings; guidance continues to use actual status presence.

## Changes

### Leave physical pads while waiting

Suppressing a graph edge does not disable a physical teleporter. A character left
on a pad after the last receptacle dies can be transferred again when Lock expires.
During System Lock, avoid the two pad locations on the current island. After all
live targetable receptacles disappear, avoid the still-enabled current-island pads
until arena restoration. The avoidance radius is 1.5y, an explicit navigation margin
around the nominal unit-radius layout trigger, not a new damage radius.

Continue routing toward live adds only, retain local target preference, and wait
on a selected entrance when a useful transfer is pending without Lock. No graph
edge, remote goal or forced movement is introduced for the all-cleared wait.
Arena restoration and new Genesis clear pad state and its avoidance.

### Track both endpoints of post-island pools

The earlier six individual pools use OID `0x1EC097`. After Rebirth, three objects
of OID `0x1EC098` each supply TWO puddles: their origin and local Z+15 transformed
by object facing. Track and draw both endpoints and expose both to AI. The link
between endpoints is not itself marked as a Heavy pool.

Predict from object creation using Empty Salvation's cast finish +0.8s; the
observed `0x00010002` animation makes both endpoints immediately active. Link/orb
animations `0x00100020` and `0x00400080` preserve them; fade `0x00040008`, matching
actor destruction, a replacement Salvation and False Genesis clean them up.
Existing individual-pool/tower exemptions and expanded follow-ups are preserved.

Current-game EObj 2015384 references
`bg/ex5/05_zon_z6/shared/for_vfx/sgvf_z6r3_b3493.sgb`, SHA-256
`1631151b220e4c7160e141461c39d5c4eb6a1de74a7046af686e1cc2a98b2bca`.
Its small-pool visuals are at local Z=0 and Z=15; link visuals are centered at
Z=7.5. The six matching `Boss3_Gimmick_01` cylindrical EventRanges in
`bg/ex5/05_zon_z6/rad/z6r3/level/planmap.lgb`, SHA-256
`e96f3964c53a9bbc3121d3147d3843755aff4acd764c5c8281d92db897fc31e9`,
have nominal XZ radius 2.5y. This supports the post-island footprint; it does not
independently calibrate the exact server Heavy threshold. Earlier individual
pool radii are outside this repair and retain their previous implementation.

### Forecast Deadly Rebirth's forward push

Action 50347 contains displacement effect type 31, value 78. Current game Knockback
row 78 specifies distance 20, speed 40, direction 3, direction argument 0. Recorded
movement corroborates fixed caster-forward displacement, rather than radial
movement from the boss. Keep the existing raidwide component and add a separate
knockback component with trajectory and unsafe-landing hints.

Begin warning on visual 50694, about 9.7s before the helper hit, with fixed
cast-finish +2s activation. Refine using the actual helper 50347 cast when it
appears. The AI constraint requires `position + 20 * facing` to remain inside
the restored 25y circular arena. Forecasting does not stop the push at the lethal
edge. Immunity effectiveness is unverified, so the safe positioning requirement
is retained even with a knockback immunity buff. Successful visual completion
keeps the forecast; helper resolution, matching cancellation/destruction and new
Genesis retire it. Duplicate starts do not drift the deadline.

## Recording evidence

The new local recording is `9a6a12976821108a9a642e8a`, game
`2026.10.06.0000.0000`, parsed with xBMR `7.5.6.3504`, MVID
`b13de42d-74a3-4004-bcc7-08657ff0c537`. The private frozen prefix has source SHA-256
`bd8a52a63d92a5972241a5856104b6a39896f21134df2a1c9f31e0ee7f23b304`.
It was copied from an active writer: its compressed tail is incomplete. Selected
operation windows precede that tail; the prefix is not a complete raid recording.
Raw native sequence numbers below differ from retained DevBridge indices.

- Receptacle deaths: west `742311`, east `751302`, north `757941`.
  Final northern pad pair: `757995`–`757996`, relative tick `8395004652`.
  Arena restore: `762608`, tick `8455401448`; pad removals finish `762627`.
- Visual 50694: `764896`, tick `8483056523`, 7.682s remaining, boss at
  `(-820.0046,-844.5411)`, facing approximately zero. Helper 50347 starts
  `771888`, tick `8563753306`, 1.682s remaining. Resolution `772793`, tick
  `8583512280`, affects 24 players. Local movement is approximately +20y in Z
  with constant X, followed by the stun. The visual forecast precedes the actual
  event by roughly 0.36s; helper forecast by roughly 0.30s. These differences do
  not certify a server snapshot or a last-safe departure time.
- Subsequent Salvation: `776816`, tick `8680024353`. Paired objects `411`–`413`
  create at `776907`–`776909`, tick `8680991143`, at
  `(-832.9904,-827.5)` facing 120 degrees, `(-832.9904,-812.5)` facing 60 degrees,
  and `(-807.0096,-812.5)` facing 180 degrees. Active animations `782189`–`782191`
  precede two link-animation sets `783645`–`783647`, `785680`–`785682`.

## Validation and acceptance

The fresh live snapshot at `2026-10-14T20:25:55Z` confirmed that InCombat was
absent before the watched-output build started. The full current-tree Release
build succeeded with zero errors and six existing obsolete-gauge warnings.
Candidate DLL SHA-256:
`e34ed82e686d95f8c3510191a8730d49b8a24aab1433071c7cb4523a602c17c7`,
MVID `d2578fb3-ca88-4e97-b936-afb151e7b321`.

- All seven focused Promathia native regression groups pass 214 assertions,
  including Genesis/island bounds, corner-barrier Empty Seed, ring deadlines,
  committed towers, previous puddles, six pad routes, stepping off pads while
  locked/all-cleared, linked endpoints and lifecycle, and early directional
  Rebirth in three synthetic rotations. These are callback/navigation tests,
  not physical calibration or live game actions.
- The shared suite also ran. A concurrent, unrelated Shinryu transition test
  failed its first-waypoint assertion. No Promathia group failed. A private copy
  of the same test sources restricted to the seven Promathia groups supplies the
  focused result without changing or hiding the shared Shinryu tests.
- After combat ended, the complete new original was copied and authenticated:
  SHA-256 `af7825766ff97a450fb127bd8361f90ccd24b292e930f3c716bf492490cbad54`,
  14,862,880 bytes. This is the full version of the prefix described above, not
  another independent pull. Native replay of the complete recording succeeds.
- Eleven complete-original native samples match the prefix baseline's actor
  positions. Final pad activation adds two immediate exclusions; the native
  first destination leaves the physical northern entrance while remaining on
  the same island, with no teleporter edge. Arena restoration removes them.
  Rebirth adds its directional landing predicate on the early visual, retains
  it through visual completion, refines it on the helper, and clears on the hit.
  The six actual hexagon endpoints are independently probed exactly once each,
  with clear link midpoints, from creation through both travel animations.
- Fourteen samples on the earlier complete afternoon original, source SHA-256
  `482fbc09b779c4d518d0e13d59b21c62e2fbde58e2e4f45c7060c917ef3e9f10`,
  cross-check the wait/restore/push and two differently oriented pool generations.
  Both generations retain six pools through travel and remove all pool endpoints
  on fade. Other active attacks remain present. Both full replay runs have zero
  module errors, no missing samples and no unsupported outputs, using matching
  current game data. This historical source was already used for earlier
  Promathia repairs; it is not a pristine whole-recording holdout.

The three reported defects are repaired and pass the affected offline checks.
The module remains WIP: prior notes explicitly did not establish complete
encounter acceptance from its inherited Contributed label, and this scoped
repair does not supply a full known-pattern coverage review. Promotion is not
blocked on a personal playthrough. Exact server Heavy thresholds, immunity
behavior, last-safe departures and live BMR/XCAI execution remain unverified;
recording/replay success is not a claim that the changed plugin has completed a
live encounter safely.

Detailed private inputs/outputs are under `/tmp/xbmr-promathia-phase-repair`.
Concurrent Shinryu source/test changes are preserved and are not owned by this
repair. No installed plugin copy, manual reload, release, or live game action is
performed. The watched development DLL was rebuilt only after the out-of-combat
observation; no further plugin rebuild was needed for these results.
