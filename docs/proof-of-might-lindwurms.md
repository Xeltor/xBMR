# Proof of Might: lindwurms

## Scope and maturity

Added on 2026-10-17 against source base
`244e0d8b24c7a25b510ae5da0b1f28ff8079ba35`, preserving existing local changes.
This covers the first level-30 Lancer quest battle, protecting Thibain in
Central Shroud: territory 239, CFC 309, quest 65975, level sync 34.
The later Foulques duel has its own module, documented in
[the ARR Lancer notes](arr-lancer-quest-modules.md).

**WIP:** full terrain/arena geometry, physical cone boundaries, calibrated server
snapshots and live AI performance are unverified. The central planning envelope
is deliberately provisional; this is useful mechanic and target-priority
coverage, not complete arena acceptance under the maturity policy.

## Written research

Searched for `FFXIV "Proof of Might" quest solo duty guide` and lindwurm-specific
strategy before implementation. Read these sources:

- [Quest walkthrough and dialogue](https://ffxiv.consolegameswiki.com/wiki/Proof_of_Might):
  distinguishes the lindwurm rescue from the subsequent Foulques duel and
  identifies Thibain as the rescue objective. It supplies no detailed cone
  geometry or avoidance deadlines.
- [Quest battle catalogue](https://ffxiv.consolegameswiki.com/wiki/Quest_Battles):
  lists both solo battles separately, including their areas and level sync.
- [Historical strategy discussion](https://gamefaqs.gamespot.com/boards/678051-final-fantasy-xiv-online-a-realm-reborn/67234423):
  the substantive combat advice concerns Foulques, his adds and ice fields.
  Those mechanics must not be assigned to the earlier lindwurm battle.
- [Official quest entry](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/256148b3e98/):
  confirms the level-30 Lancer quest identity, without mechanic measurements.

No suitable detailed written guide to the first battle was found. Mechanics and
the failure condition below come from the recordings and matching local data.

## Recorded evidence and implementation contract

Both closed local originals are authoring inputs, not independent holdouts:

| Source SHA-256 | Scope |
| --- | --- |
| `132dd6c4eb5ee7d84043ff76210d9f56cef710662e4a0d91dfce03ad44f80466` | Failed rescue; 17 Frost Breath casts; Thibain reaches zero HP |
| `2adf6c148ec2c66e31b6117db50f376ea850b0b83fbadd4eec683691845b6deb` | Successful rescue; eight cast starts, seven resolutions and one cast interrupted by caster death |

The originals decode to EOF with game version `2026.10.06.0000.0000`.
The numeric schema-2 inventories contain 1,512 and 1,855 observations. Their
omission inventories remain attached to the private reports; the native runner
uses the originals and applies every raw operation. Archive searches in the
ordinary Shroud territories found no sources; the actual instanced territory was
identified from the local replay. Copies/projections are not additional trials.

- Lindwurms: OID `0x59B`, radius 1.4. Thibain: OID `0x256`, NameID 627,
  radius 0.5. Four active enemies and two initially hidden reserve objects occur
  in each original. No damaging action other than autoattack 870 and Frost
  Breath 318 is observed from the lindwurms in these sources.
- The failed rescue has Thibain alias 50 at 55/3205 HP (raw sequence 5830),
  7 HP (5877), then zero HP (5900) and death (5901). Two lindwurms remain alive.
  The player is not recorded dying in that attempt. The NPC-targeted enemies
  need priority over the enemies already attacking the player.
- Priorities are 3 for a wounded enemy targeting Thibain, 2 for a healthy enemy
  targeting him, and 1 for another eligible lindwurm. Existing pending-death
  and other negative native priorities remain authoritative. No forced targeting,
  forced movement or speculative interrupt/stun flags are introduced.
- Frost Breath starts are independent per caster. Failed-source sequences
  471/476 demonstrate overlap, with matching action/cast-end pairs 758/759 and
  773/774. Successful-source sequences 2783 and 2886/2889 cover a cast ending
  when its caster dies. SimpleAOEs supplies native bootstrap, timing, AI
  constraints and individual cast removal.
- The observed cast timer is 2.2 seconds. BMR's existing 0.3-second NPC correction
  predicts approximately 2.5 seconds from the original cast start, consistent
  with observed action delivery. Warnings and AI constraints start at cast
  observation; they are not delayed until impact. No earlier reliable mechanic
  cue was established. Action delivery is not a calibrated server snapshot or
  last-safe departure time; no additional greed window is claimed.
- Current local Action 318 has CastType 3, EffectRange 6 and Omen 4,
  `gl_fan090_1bf`. The implemented nominal shape is a 7.4-yalm, 90-degree cone
  (6 plus the recorded caster radius; constructor half-angle 45 degrees).
  Matching-version typed fields agree with the older archive dataset for these
  selected values. Their physical boundaries remain uncalibrated.
- The module anchors on Thibain, whose lifetime spans the rescue. Pull detection
  checks the exact duty and any fighting lindwurm, including when Thibain is
  initially untargetable. Individual enemy deaths cannot unload this anchor.
  Completion waits for the in-bounds enemies; hidden off-arena reserve objects
  do not prolong the fight. NPC death ends the phase. Phase exit clears all
  components; fresh construction starts without retained cast state.
- Radar shows all currently casting cones, at most two concurrently in these
  sources, plus the enemies and protected NPC. There is no speculative future
  sequence or extra arena text. Visual in-game readability remains unverified.

## Arena evidence and remaining work

The matching local TerritoryType selects
`bg/ffxiv/fst_f1/fld/f1f1/level/f1f1.lvb`. Its planner layout has quest layer
83337, `QB_ClsLnc006_001`, with collision boxes and matching line VFX around
the rescue area. The planner LGB SHA-256 is
`6ee92d11049fad0887c3b5164f49ff8ce0c0b36eb9dae54882c91f7263114fa4`.
Relevant collision instances are 3818477, 3818482, 3818484, 3818486 and 3899698.
They bound entrances around Z -208/-128 and X -369, with a western passage
near (-438, -129). These are layout observations, not a complete floor polygon.

The implementation uses a fixed central planning circle at (-395, -173), radius
24, inside those duty barriers. This radius is a conservative planning policy,
**not a measured physical arena boundary**, and is not derived from the extent
of player movement. It constrains native pathfinding, so it also excludes some
possibly usable space. No lethal circle, hole or phase boundary is asserted.
There is no preexisting obstacle map for this territory. The exact natural
terrain perimeter, obstacles, slopes and wall contact need independent mapping;
the 3D arena border is suppressed to avoid projecting an invented floor edge.
Do not promote this initial module or describe its routes as physically verified
until that material gap is resolved.

## Validation

- Full current-tree Release build with SDK 10.0.401 and API-15 development
  dependencies succeeds: zero errors, six existing obsolete-gauge warnings.
- Generated registration, simultaneous preexisting casts, cone probes, AI
  constraints, priorities, cancellation, first-enemy death, hidden reserves,
  retry construction, forbidden targets and protected-NPC death pass 16 synthetic checks in
  `tests/ProofOfMightRegression`.
- Native replay examines 84 samples across both originals and passes 701
  recording-derived assertions for per-caster state, cast origins/activations,
  nominal cone probes and explicit seeded target priorities. Both replays have
  zero module errors and no unapplied raw operations, with exact local game-data
  version matches. Each deliberately requested final post-unload sample is
  unavailable; lifecycle records separately confirm zero components on exit.
  Thus the runner's `export_complete` remains false for those two expected
  unavailable samples; it is not represented as full physical acceptance.
- Existing Python tests: 5 passed. Whitespace validation passes.
- Initial lindwurm build assembly MVID: `78809389-b3f1-49d8-8cd9-a5d95423da6b`;
  SHA-256: `4d98e145e2c3a2b203a2b9dd4fac8f885ff56781f89d69748f1e1d52d7e469f6`.
  The active second-profile client was out of combat before the build. Its
  watcher completed loading the development plugin at 09:00:33 Amsterdam time.
  No manual reload or installed-plugin binary replacement was used.

The subsequent combined ARR build, source identity and regression totals are
recorded in [the coverage summary](arr-recorded-quest-coverage.md). The lindwurm
native regression was repeated against the combined candidate and still passes
701 recording-derived assertions; the additional synthetic check explicitly
preserves forbidden priority -4 as well as the setter-protected -1 priority.

Private requests, reports and assertion checker are retained under
`<private evidence location>`.
Offline replay does not prove live target selection, navigation, player/NPC
survival, physical hitboxes or last-safe movement timing. Live duty testing and
the arena work above remain open. No version bump, commit, push or release is
part of this implementation request.
