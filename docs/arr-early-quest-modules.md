# Early ARR solo quest modules

Authored 2026-10-17. These cover the retained solo recordings, not the open-world
quest steps or Copperbell Mines. All four quests remain **WIP** because complete
playable floor geometry, physical hit boundaries, server snapshots and live
navigation are unverified. To Guard a Guardian has two separately registered
battles because the first battle's actors are removed during the cutscene.

## Written research before implementation

Read the solo-duty sections of these walkthroughs before choosing mechanics:

| Quest | Guide and relevant findings |
| --- | --- |
| Spirithold Broken | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/spirithold-broken): the injured Hearer starts the solo Clay Golem encounter after the open-world rescue. True Grit is frontal and Plaincracker surrounds the golem. |
| Leia's Legacy | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/leias-legacy), [community wiki](https://ffxiv.consolegameswiki.com/wiki/Leia%27s_Legacy): three Hedge Knights accompany Janremi; defeat the cane-wielding healer first, then archers, then Janremi. |
| To Guard a Guardian | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/to-guard-a-guardian): prioritize Ixali before Quzal, the incoming Sincallers and Blanga; the following battle is Gargoyle before Masked Mage. |
| Into a Copper Hell | [Thonky](https://www.thonky.com/final-fantasy-xiv/quests/into-a-copper-hell): after Copperbell Mines, reporting to Painted Mesa starts a separate Ul'dah bodyguard battle. Raucous Bodyguard first, then Filthy, then Boastful. |

The guides give player responses and ordering, not exact dimensions or safe
departure times. Their priority advice agrees with the observed actor/action
identities. The recorded Copper Hell player kills Filthy before Raucous; this
does not contradict the recommended caster-first strategy. In Leia's recording
the healer survives late and repeatedly casts Cure, supporting that priority.
No terrain perimeter, authoritative snapshot or angle measurement is inferred
from these guides.

## Evidence and arena contract

Closed originals and version-matched Action/Omen/LGB summaries are retained under
the private evidence directory `arr-solo-20261017`. All sources are training;
there are no independent holdouts. Numeric projections are schema 1 and omit
some native operations; replay validation uses the closed originals instead.
Game data and recordings identify `2026.10.06.0000.0000`.

| Quest | Territory / CFC / quest row | Original SHA-256 |
| --- | --- | --- |
| Spirithold | 225 / 297 / 65665 | `fc56ed1f4cdcab0525a53ea746d85663bec3b9529a6aa82d1dbaf1e3875d27e6` |
| Leia | 227 / 299 / 65697 | `d614eb1bfdaa9e3763eb8d40227739737d42d8a282dc7b768ea81e54b2f84ff6` |
| Guardian | 226 / 298 / 65983 | `743d232dc8b6bc36422303c8b84ee0f42eb09e61460594603068491eac745b8e` |
| Copper Hell | 274 / 338 / 66196 | `20e7f768cd47b093e71201fb2d7693c974b4daaa76164ba652ded2da9e2cc2f9` |

Arena investigation preceded implementation. Matching quest layer
`QB_SubFst035_001` (83940) for Spirithold has no perimeter collision objects.
Leia's `QB_SubFst055_001` (83854) has three collision boxes, instances 3863571–3,
at Z 253.6286 / 320.4066 and X -133.2832; its western cliff and uneven floor are
not established by those three entrance barriers. Guardian's
`QB_ManFst007_001` (83231) has only the southern entrance barrier 3838144 near
(-287.029, 118.5026). Copper Hell's searched town LGBs provide no matching
quest barrier layer. The Shroud planner asset hash is
`6ee92d11049fad0887c3b5164f49ff8ce0c0b36eb9dae54882c91f7263114fa4`;
per-file receipts are retained in `zone-*-layouts.json`.

These partial barriers do **not** define complete playable terrain, holes,
slopes, lethal edges or cutscene boundary changes. Initial planning envelopes
are policy circles around the combat areas: Spirithold (251,191)/20; Leia
(-177,290)/20; Guardian (-310,172)/35; Copper (39,-145)/15. They constrain native
pathfinding, but do not assert that everything inside is walkable or that their
circle is an actual edge. Their radii are deliberately explicit provisional
policy, not measurements from player trajectories. Three-dimensional arena
outlines are suppressed to avoid projecting invented floor edges. Obstacle maps
are allowed where one is available; that setting is not proof that a map exists.
Exact floor mapping remains a material acceptance gap for every module.

## Implemented mechanics and lifecycle

- **Spirithold:** Clay Golem `0x225`, R2.2, NameID 30. True Grit 445 has current
  CastType 3, range 6 and `gl_fan120_1bf`: nominal range 8.2, half-angle 60°.
  Plaincracker 482 has CastType 5, range 6: nominal radius 8.2. Cast starts
  1990/3626 and 4098/5478 have independent start/end tracking; no helper delay
  is observed. Starts immediately supply radar and AI constraints. Standard
  NPC cast correction adds 0.3 seconds to observed remaining time. Cast
  resolutions and visual end are not a calibrated server snapshot or greed
  allowance. Both casts clean up individually, on cancellation and phase exit.
- **Leia:** Janremi `0x98`, R0.55, NameID 185. Heartstopper 866 has CastType 4,
  range 3, full width 3 and `general02f`: nominal forward length 3.55,
  half-width 1.5. Starts 5491/6623/7831 are tracked at once, with matching
  cast-end retirement. Healer `0x9A` outranks archers `0x99`, then Janremi.
  The module remains active if Janremi dies while an accomplice survives;
  individual enemy death never prematurely clears the remaining fight.
- **Guardian first battle:** Galfrid `0x39A` anchors all Ixali waves and Blanga.
  Current ally/targetable/death flags are checked before assigning priorities;
  some recorded Ixali begin as allies, so initial OID alone is insufficient.
  Blanga `0x3A0` and the Sincallers (`0x39F`, `0x22D`) outrank Quzal `0x39C`.
  Blanga Terror Eye 740 is a ground-targeted radius-6 cast, start 23362/end
  23666. Galfrid is destroyed at raw sequence 24823 during the cutscene. That
  clears the first module; it cannot safely serve as an anchor for both fights.
- **Guardian second battle:** Masked Mage `0x3A2`, NameID 617, and Lesser
  Gargoyle `0x3A1` are created at sequences 24977/24979. A separate registration
  activates on either actor entering combat and prioritizes the Gargoyle.
  Fire II 147 is a radius-5 target-following splash, represented with native
  bait guidance rather than pretending a solo player's own targeted attack
  can be escaped by moving away from its initial location. Blizzard II 146 is
  a radius-5 caster-centered circle. Starts 30462 and 31677 supply warnings
  immediately. Papalymo can use the same spell IDs: source OID and current
  hostility explicitly exclude friendly magic. Mage death does not end the
  module if the Gargoyle still lives. Cancellation retires matching casts.
- **Copper Hell:** Filthy `0x3C0`, Boastful `0x3C1`, Raucous `0x3C2`, all R0.5.
  Priorities follow the guide's caster-first order. The first bodyguard's death
  at 5211 keeps the module alive through remaining deaths 5782 and 6809.
  The observed Fire141 is targeted damage with no independently supported
  avoidable area, so it is not drawn as a false ground hazard.

All priority logic preserves negative caller priorities and excludes dead,
destroyed, untargetable and friendly actors. No interrupt/stun flags, forced
movement or external AI ownership changes are introduced. The radar draws only
current casts and participating enemies, without speculative future waves.
This gives early available information without covering the arena in predicted
yellow. In-game readability and actual XCAI execution remain unverified.

Guardian Power Wave 2188 has CastType 3 and range 6 plus caster radius, but no
matching Omen angle or independently established early cue in this evidence.
Its recorded multi-target hits alone cannot supply a trustworthy cone model or
warning deadline; it remains an explicit handling gap. Coming Storm 423 and
Swift Gust 424 are self-buffs, not missing dodge AOEs: both have CastType 1 and
EffectRange 0, with self-targeted actions and corresponding status effects
(423 grants status 8; 424 grants status 31). Ordinary attacks, healer casts and
NPC/player actions are inventoried in the retained facts; no unsupported dodge
circles are added.
Unobserved branches, enemy control immunity and full natural terrain are open.

## Validation

Current-source full plugin and generated factories are built in an isolated
copy under `<isolated validation output>`, with pinned SDK 10.0.401 and API-15
dependencies. This does not touch the watched development output. The parent
task performs the final current-tree Release build when the client is out of
combat. No release/version/installed-binary changes are included.

`tests/ArrEarlyQuestRegression` covers registration, already-running casts,
nominal geometry probes, native AI constraints, source hostility, priorities,
hidden enemies, cancellation, primary death with surviving adds, the Guardian
cutscene and second-battle activation. Native replay requests/reports and
recording-derived checks are retained privately under
`arr-solo-20261017/early-arr/`. Build, native replay and synthetic checks do not
establish physical safety, calibrated last-safe movement or live acceptance.

The isolated full build succeeds with zero errors and six existing obsolete
gauge warnings. All 34 synthetic assertions pass. Five native replay requests
(including both Guardian registrations against the same original) export all
88 requested samples, with zero missing samples, module errors or unapplied raw
operations. The checker passes 601 recording-derived assertions for activation,
cast identity/origin/rotation, nominal shape dimensions, warning deadlines, AI
inside/outside probes, eligible target priorities and phase cleanup. There are
four originals, not five independent trials. Final current-tree integration
build proof is recorded by the parent task. The native fixture exports module
AI hints; it does not run XCAI, draw the actual radar or navigate the player.

A separate full-native-state inspection checks the target fixture's input
assumption. At Guardian sequence 5000, protected allies occupy party slots
24–27; enemies attacking them satisfy the native builder's party-target rule
and begin at priority 0. Equivalent checks confirm Thibain at slot 24,
Nouveau's ally at slot 25 and Company's Storm Marauder at slot 25. The stored
`native-default-target-priorities.jsonl` records AggroPlayer, target slot,
current targetability and the builder-rule result. Hidden lindwurm reserves
remain at passive priority -3. This supports preserving all negative priorities
instead of broadly promoting passive enemies. It is a source-state check of
native builder rules, separate from live XCAI selection.
