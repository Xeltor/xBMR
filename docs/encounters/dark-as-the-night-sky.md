# Dark as the Night Sky — Protect Orn Khai by defeating Faunehm

## Scope and maturity

Level 68 Dragoon quest 68449, level sync 70, CFC 267, territory 713.
This is the first Faunehm battle (`0x1CDC`), **not** Dragon Sound's
level 70 battle (`0x1CDD`). The existing QuestBattle waypoint is unchanged.

New **WIP** combat coverage: Abyssic Buster, Chaotic Verse, Comet warnings,
Dark Star, and both targetable wings. Exact physical arena bounds and Comet's
safe damage-falloff threshold remain unresolved. Do not promote based on the
build or lifecycle checks alone. This is not complete automatic-dodging support.

## Written-guide research (2026-11-02)

- [Quest walkthrough and dialogue](https://ffxiv.consolegameswiki.com/wiki/Dark_as_the_Night_Sky):
  confirms the correct quest and objective; the scene ends in retreat rather
  than Faunehm's death. Its battle section has no detailed attack strategy or
  measurements. Dialogue supports the story sequence, not a movement deadline.
- [Quest-battle roster](https://ffxiv.consolegameswiki.com/wiki/Quest_Battles):
  distinguishes this level 68 / sync 70 battle from Dragon Sound.
- [Official quest entry](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/8fd405544a6/):
  confirms level 68 and Dragoon eligibility.

Additional searches for the exact quest title with “walkthrough”, and Faunehm
with “Comet”, found no suitable written mechanic guide. Do not import Dragon
Sound's Enervation/Elusive Jump interaction or Heavensfall into this fight.

## Evidence and inventory

Combat recording `abad83800ee63055e1d032c7c8f0fa329d9fbee78c5d4bdf3409d80b6dcb2e9a`,
retained revision `b56053ec110521d64ca465f81ce89740010683909640f95b3776b62f5de760c6`,
original SHA-256 `7f67f32c4aafd7649abf033b42e703c75fbc4eb2a2f5786df1d336d9a677dd6b`.
The complete original contains 9,967 decoded entries; the schema-3 archive has
2,791 retained events and omits HP/MP, frame and several other operation kinds.
Game version `2026.10.16.0000.0000`, recorded xBMR `7.5.7.300`.
Local original inspection supplies HP/MP and NPC/Layout IDs; private filenames,
identities and raw assets are not repository artifacts.

Recording `7289dc61923eb5a1d0ea2ebf8fcd357fc25a4bdfbfb594797fb5a29b93d9a647`,
revision `70d107160a3153a6bb257604878f0751af68280dfb5973d53f282a018484b526`,
original SHA-256 `e678778f05869eb5c97bb5e292ad8dc5f766566e691d50ff79b1893699ca48c0`,
contains the initial scene and timeout, with no combat actions. It is not an
independent mechanic evaluation. Both sources were inspected during development;
there is no held-out combat pull.

The completed inventory includes boss `0x1CDC`, helper `0x18D6`, allied Orn Khai
`0x1CDF`, left wing `0x1D05` and right wing `0x1D06`. Other initial enemy OIDs are
overworld scenery/mobs, with no observed combat actions. All observed hostile
actions are 870 and 8929–8933. Player actions/buffs, Chaos Thrust on Faunehm,
party/identity updates, duty timers and ordinary actor movement are not additional
authored hazards. Vulnerability Up 1402 is observed after Chaotic Verse.

## Arena first

The module currently uses a **provisional planning envelope**, center
`(-340, -393)`, radius 30. Center is tied to Faunehm's initial placement
`(-340.056, 69.91415, -393.0817)` (combat source sequence 99).
The radius is a planning policy, **not** measured from player positions, a
physical wall, lethal edge, or verified walkable disk. It constrains the native
map; obstacle-map integration remains enabled. The 3D bounds drawing is disabled
to avoid presenting it as a measured physical boundary.

Original territory/background and LGB inspection did not resolve the duty's
playable boundary. Territory 713 references overworld background e3f3 and has no
QuestBattle row. The inspected `QST_JobDrg680_001` layer contains quest NPCs and
an arrival PopRange, not a verified combat arena. No physical floors, holes,
lethal edges, transition/reset changes or complete perimeter are established.
Do not enlarge this envelope or certify edge safety without further evidence.

## Mechanic contracts

| Mechanic | Earliest cue and implementation | Resolution and limits |
| --- | --- | --- |
| Abyssic Buster 8929 | Cast start; `SimpleAOEs`, caster-location cone, radius 31.84, half-angle 45 degrees. Both observed rotations are handled by the cast rotation. | Cast/action pairs 6624→6906/6907 and 8543→8694/8695. NPC timer correction yields about 2s warning; each source retires on its own cast finish/cancel. |
| Chaotic Verse 8930 | Cast start; targeted-damage/vulnerability warning, timed damage prediction only for a valid party slot. | 5419→5750/5751 targets Orn Khai; 9028→9174/9175 targets the player. Never map an absent NPC slot to bit 63. The later status is not an earlier dodge cue. |
| Comet visual 8932 | Immediate text warning at cast start 7259. | Visual action/end 7738/7739 does not remove the helper warning. Duplicate text is suppressed once the helper is warning. |
| Comet impact 8933 | Helper location cast at 7264, marker outline at the **cast destination**, and its separate damage deadline. The helper actor's distant parked position is not the impact point. | Impact/end 7874/7875, roughly 6s after its cue and about 1.1s after the visual resolves. 50y is the sheet damage range; no invented lethal or safe radius. Marker outline radius 1 is a locator, not damage geometry. Automatic escape constraints remain unimplemented pending falloff/boundary evidence. |
| Dark Star 8931 | Cast start 9534; raidwide warning and damage prediction. | Action/end 9672/9673. No full-arena forbidden zone. |
| Wings | Both spawned targetable parts are drawn and prioritized among eligible native targets. Their appearance and later death are tracked independently. | Spawn 9029/9033; right dies 9465, left 9827. Faunehm stays alive and untargetable. First wing death does not end the module; both deaths do. Negative native target exclusions are preserved. |

Current-version offline Action/Omen fields match the older archive rows for
8929–8933. The exact-version sheet check binds these fields to the recording;
it does not prove physical hitboxes. Cone geometry follows the same-action
`DragonSound.cs` implementation plus Action 8929's range 25, recorded boss
hitbox 6.84, and 90-degree omen. Independent hit-boundary tests are still absent.
The Comet omen is `gl_shipimpact0f`; a single hit is not a calibrated falloff curve.

Cast finish plus the native 0.3s NPC correction is the native estimated
deadline, not verified server snapshot/last-safe-departure evidence. No extra
greed window is asserted. Future independent timing evidence can refine it.

## Presentation, lifecycle and validation

Presentation follows current `GenericAOEs.cs`, `CastHint.cs`, and
`UnavoidableDamage.cs`. `DragonSound.cs` supplies the same-action cone example;
`A33ProtoUltima.cs` supplies the warning/marker pattern for unmeasured proximity
falloff. Comet has one locator outline instead of a yellow arena-sized exclusion.
There is no discrete multi-step AOE sequence to preview. Drawing does not imply
an AI constraint; Comet's automatic movement remains explicitly unresolved.

Faunehm's combat flag can precede targetability. Activate on combat in the exact
duty, retaining the opening warning. Orn Khai's scripted knockout (5842) does not
end the encounter. Component activation bootstraps existing casts and wing actors.
Phase completion clears components after both wings die or the primary disappears.

`tests/DarkAsTheNightSkyRegression` checks registration, distinct Dragon Sound
registration, untargetable activation, NPC/party damage masks, cone rotation and
authored boundaries, native timing, cancellations, separate Comet/helper cleanup,
raidwide predictions, wing exclusions, first/final wing deaths and fresh activation.
These are synthetic implementation checks, not physical measurements.

Validation on the current module source
`7e30e43433861c4382368228dd7167f105bf5ea3e41611e38f6cc370e1b41368`:

- Release plugin build passed: zero errors, six existing obsolete-gauge warnings,
  no new module warnings. Assembly SHA-256
  `e68ea74d5ccbc0b7b8d262192b48b4fd6c33c800ddb9f8589a6fd921d12840e5`,
  MVID `d71b0616-ad27-4bfe-9aba-a6bb26031e19`.
- All 25 synthetic regression checks passed, including one Comet global warning
  before the helper appears and one while both casts overlap.
- Actual original replay: 39 boundary samples requested, 38 available, zero
  module errors and zero unapplied raw operation kinds. Both cones produce one
  timed AI constraint and clear; Comet's prediction survives visual completion
  and clears at the helper end; Dark Star predicts damage; both wings receive
  priority and the first wing death preserves the active state.
- The completed-group sample after both wings die is explicitly unavailable
  because the manager unloads the module. Lifecycle callbacks independently show
  deactivation/unload with zero components. `export_complete=false` records that
  unavailable sample; it is not an empty valid mechanic sample or disposal proof.
- Replay uses the runner's supported schema-2 numeric export of the same original,
  with the exact installed game version. Archive evidence is schema 3. Original
  sequence/actor/cast inventories were reviewed directly; byte-for-byte schema-3
  projection equivalence is not claimed. Global warning delivery is covered by
  the synthetic check; the runner's personal-text fixture does not export globals.
- Observed-token mapping completed to a null cursor with no unmapped selected
  groups. Comet impact's three tokens remain explicitly unresolved for full
  geometry/movement handling; 35 observations are declared handled and 85 ignored.
  This selection covers creation, combat/cast/action and encounter signals;
  original status/movement/health inventories were reviewed separately. Mapping
  contract `9dc8322cebcc89c6c7ee22122c4a2eceda03f974049b91cbd87e5f2b6e299c07`
  is a source-review claim, not mechanic correctness certification.

Remaining acceptance: mapped playable arena, measured Comet response/falloff,
independent combat pulls, snapshot/travel margins, and live player/AI behavior.
