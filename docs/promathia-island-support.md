# Promathia island support routing

Added on 2026-10-14 after the user requested guidance through an enabled pad to
help another island finish its Memory Receptacle. `IslandSupport` is registered
in the existing Promathia phase. Assembly naming, IPC, release metadata and
existing mechanic constraints are unchanged.

## Behavior

- Keep the player's living local receptacle as the preferred target.
- After it dies, require both recorded pad activation signals for that island.
  Pick a surviving destination using the nearer entrance and retain that choice
  until it dies, becomes unavailable or the player changes island.
- Register only directed, single-hop transfers from the current cleared island
  to islands with living targetable receptacles. Add a destination attack-range
  preference, an informational hint and a circle around the chosen entrance.
  Existing hazards and lethal arena bounds continue to constrain navigation.
- Once inside the entrance, replace the remote preference with a small local
  pad preference and withhold the graph edge while awaiting the actual transfer.
  A graph jump must not become a request to walk across the void.
- Suppress transfer guidance during observed System Lock. Do not assume the
  three-second duration in the fight description: current observations are
  approximately four seconds. Arrival on a live island resumes its local target.
  Dash guidance is forbidden while approaching/waiting for an island transfer.
- Clear enabled pads and assignments on a new False Genesis or arena restoration.
  Do not enable transfers solely because an add died. Stop when all adds are dead.

## Evidence

The newly uploaded recording is
`0d5844e7ea7bbd9b528237827d2058b39b8a28584ed9d0ffa97ca40c25b1d79a`,
schema 3, source SHA-256
`482fbc09b779c4d518d0e13d59b21c62e2fbde58e2e4f45c7060c917ef3e9f10`.
Its complete private original and game data both use
`2026.10.06.0000.0000`. Raw native sequences are used for replay samples.

The three receptacles spawn at raw sequences `102806`, `102808`, `102810`.
Northern receptacle death `126145` precedes pad effects `126186`–`126187`;
western pad effects `133616`–`133617` precede the same-frame western death
`133633`; eastern death `137132` precedes pad effects `137154`–`137155`.
Activation state is `0x00020001`, removal state `0x00080004`.
The paired indices are north `0x5F/0x60`, east `0x0F/0x5E`, west `0x0D/0x0E`.
Both effects are required instead of guessing the individual index-to-pad mapping.
Restoration and pad removal occur at `141158`–`141165`.

The six independent layout positions come from `Boss3_Gimmick_03` in
`bg/ex5/05_zon_z6/rad/z6r3/level/planmap.lgb`, SHA-256
`e96f3964c53a9bbc3121d3147d3843755aff4acd764c5c8281d92db897fc31e9`.
EventRange entries `12545727`, `12545728`, `12545731`–`12545734` are cylinders
with unit XZ scale. Matching PopRange entries supply the nominal exits.
These asset dimensions are not independently calibrated server trigger radii.

| From | To | Entrance XZ | Exit XZ |
| --- | --- | --- | --- |
| North | East | -816, -812 | -811.0718, -820.5359 |
| North | West | -824, -812 | -828.9281, -820.5359 |
| East | North | -811.0718, -820.5359 | -816, -812 |
| East | West | -815.0718, -827.4641 | -824.9281, -827.4641 |
| West | North | -828.9281, -820.5359 | -824, -812 |
| West | East | -824.9281, -827.4641 | -815.0718, -827.4641 |

Current System Lock/movement windows corroborate north-to-east, north-to-west,
west-to-north, west-to-east and east-to-north pairings; positions interpolate
during transfer rather than appearing as one large movement operation.
Examples are lock sequences `128079`, `131255`, `134482`, `134515`, `139252`.
The older recording `6c85fbd4c2...`, source `fa629e1f29...`, also corroborates
east-to-west: actor 84 gains Lock at `1133315`, tick `12198641995`, and reaches
approximately `(-824.9485, -827.4815)` by movement `1133628`.
The nominal graph transfer cost is 0.5 seconds; physical travel/controller timing
is still an acceptance limit.

## Validation

- Full current-tree Release build succeeds with the pinned SDK, zero errors and
  six existing obsolete-gauge warnings. The watched development output is rebuilt
  in place, with no installed-directory copy, manual reload or release.
- The native regression suite passes 1,292 assertions, including 32 new assertions
  for activation pairs, local priority, all six directed routes, native pad
  approaches, entrance waiting, System Lock, destination death, arrival, all-dead
  completion and phase reset. These are synthetic contract tests.
- Twelve current-original native samples are available with complete numeric
  projection parity, no module errors or unsupported/skipped outputs and matching
  game data. Eleven are paired against the prior assembly: existing AOEs and
  forbidden constraints are identical. North-to-east and west-to-east fixtures
  each contain one actual native teleporter edge and first approach their local
  entrance. An additional before-Lock sample on the northern entrance contains
  no teleporter edge and requests no movement across the gap.
- Six post-freeze samples on `6c85fbd4c2...` cover a different kill order, an
  eastern support preference, Lock and cleanup. The sampled pre-transfer position matches the
  archive's separate actor observation. Its older game version differs from the
  current offline data. Those selected native chains contain no transfer edge,
  so they do not establish cross-island navigation. These historical recordings were used earlier for other
  Promathia work and are not pristine whole-recording holdouts.
- Exploratory `216d4cfe58...` samples with actor 83 have a native position that
  differs from the archive's projected movement, so they are excluded from route
  acceptance. A replacement actor 69 fixture lacks native party eligibility and
  is also excluded. Their map-effect evidence still corroborates a different kill
  order; neither replay success nor an unavailable viewpoint is called route proof.

Requests, reports, layout inspection and the validation summary are private local
artifacts under `/tmp/xbmr-promathia-teleports/`. The final candidate assembly is
`e20c50e216c2c39a9eb363b462f7b3dcd529a067acc89a27464ad43d911b139b`.
Earlier temporary candidate copies are superseded by `current/` and its reports.

Live transfer execution, collision and trigger edges remain unverified. Casts
and urgent hazards can suppress the optional destination goal; safety and manual
movement retain authority. Mid-phase reload cannot recover historical map-effect
activation state, so support routing conservatively waits for observed cues.
The build is ready for a fresh pull, not certified live gameplay acceptance.
