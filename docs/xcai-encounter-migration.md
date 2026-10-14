# XCAI encounter correction migration audit

Source work on 2026-10-14, against xBMR parent
`d17146870c21bc5e0f4657b9cac6146359d06d03` (upstream `7.5.6.35`,
`be8ff771cc545f8f8b5b548b5dd7271d80460f89`). The original migration did not change release metadata or installed plugins.
Paired publication increments only the xBMR fork revision, preserving the
upstream base, IPC name and assembly name.

XCAI's 17 encounter-fix entries and their runtime patches, preferences,
subscriptions, leases, special movement exceptions and overlays are removed.
General integration controls (terrain alignment, hallway ownership, stack
separation, tank facing and forced march) are outside this encounter migration
and remain in XCAI. Saved encounter opt-outs remain inert for configuration
compatibility. The matching release is xBMR `7.5.6.35.2` (install version `7.5.6.3502`).
The migration was reviewed for paired publication with XCAI; source release
does not establish installation or live gameplay acceptance.

## Accepted source changes

These are bounded component corrections, **not whole-encounter certification**.
Existing shapes, capacities, arena geometry and other components remain intact.
Final candidate assembly SHA-256: `a51c6be849c257f69863da1f164dbf86f70c26fa16c56b64012b0c7b4064c9da`.

| Module/component | Reproduced defect and replacement |
| --- | --- |
| Elm Gigas / `UnbowedSpirit` | Final Cyclone/Crosswinds effects removed the first spawned puddle, irrespective of caster. Remove the resolving actor only; deduplicate creation and clean up that actor on destruction. Native moving shapes still come from `Update`. |
| Alabaster Blade / `Acclaim` | Initial casts create cones but publish none until a later arrow status increments `totalGolems`. Count initial visual casters instead. Status updates replace a pending entry and cannot re-expand an already processed golem; initial cast bootstrap is idempotent. Existing rotation and 7.3-second forecasts are retained, not newly physically verified. |
| Pallmagia / `Roulette` | Centre forecast is 18.3 seconds after visual finish; repeated recorded effects arrive around 14.36–14.39 seconds. Use the existing workaround's 14.3-second forecast in the component, so rendering and AI share it. Ring logic and circle size remain unchanged. This is a client-observation timing correction, not a measured server snapshot. |
| Queen Eternal / `LegitimateForce` | `ArenaBoundsCustom` also describes the X arena. The idle split-platform restriction leaked into it. Gate that restriction on the existing split-arena centre `(100,94)` as well as custom bounds. The split phase and active cleave constraints remain. |
| Arbatel / `BookDrop` | The open-world base caches a player pool, including dead actors and excluding later arrivals. Refresh this module's pool from living, present players with positive HP. Native tower routing, minimum three, maximum five and lifecycle remain. No guessed post-cast hold or failure-damage timing is added. |
| Doomtrain / `WindpipeDrawIn` | Override populated a private one-element list, indexed it by party slot, and left inherited drawing/projection sources empty. Publish one native `Casters` source for cars 1/2/5, with cast direction and existing safe walls. Every party slot sees that source; inherited cast finish removes its caster. Retain the existing 20-yalm model; this task does not independently measure its distance or certify wall geometry. |

## Every former encounter switch

| XCAI entry | Disposition |
| --- | --- |
| Elm Gigas moving puddles | Migrated identity/lifecycle correction above. |
| Alabaster Blade Acclaim | Migrated the reproduced visibility/duplicate-callback defect above; no stale-wave timer heuristic. |
| Pallmagia Roulette | Migrated the observed centre-timing correction above. |
| Queen Eternal X arena | Migrated the native restriction gate above. |
| Arbatel towers | Migrated living-pool correction only. XCAI refused more than three towers, while the archive includes larger waves; its extra assignments, grace period and speculative damage forecast are retired. |
| Doomtrain platforms | Migrated draw-in bookkeeping only. Retired the synthetic actor sentinel, state delegate lease, 0.7-yalm pad radius and five-yalm descent exemption. Current native manager already exempts active instanced modules from distance unloading. Physical trigger boundaries/transition timing and final-car wait replacement are not independently established. |
| Arch Kelpie Storm Wave | Retired without a module replacement. Native forecasts and observed helper origins/sides disagree; changing queue consumption alone does not establish a correct hazard model. The proposed queue-only port was rejected. Independent world-space boundaries and matching sequence provenance are still needed. |
| Ruin Hound clone circle | Retired. Holding the original circle/deadline through an inferred clone does not independently validate the clone's activation, footprint or matching lifecycle, and leaves donut follow-ups unaddressed. No timer/geometry invented. |
| Shinryu floor transitions | Retired. Current module already authors floor-specific `FloorAOEs`; the workaround looks for different component names. Its additional six-yalm launch-pad exclusion is not a validated replacement for native layer/transition behavior. |
| Ogre Piece wisp deadlines | Retired. Eleven-yalm protection, one-second expiry extension, teleport flank preference and inferred spawn-priority rules are not independently established physical/snapshot/target-assignment contracts. Native module remains unchanged. |
| Tablitaur Shockwave | Retired the encounter-specific teleport-target prediction and 5.75-yalm landing clearance. Generic knockback planning remains. No new native knockback magnitude, stomp boundary or compound deadline is invented. |
| Metamorph Hellish Breath | Retired. Boundary staging was an optional XCAI goal, not a demonstrated missing native mechanic. No module safety requirement is inferred from that score. |
| Arachne Funnel | Retired. Suppressing only XCAI reengagement is not equivalent to a native all-dash prohibition, which could also suppress escape actions. No broader prohibition is added. |
| Barreltender departure | Retired. Delaying XCAI movement for uptime is not itself an evidenced module forecast defect. Native timing/routing remains. |
| Pallmagia Esoteric departure | Retired. Remove the XCAI route-hold workaround; no new blast geometry, deadline or mandatory movement is inferred. |
| Elm Gigas Cyclone preference guard | Retired. Suppressing advisory goals does not repair a moving-hazard model. Puddle lifecycle is corrected separately above. |
| Claret Dragon ward preference guard | Retired. Native module already supplies the open-side goal; XCAI goal suppression does not prove a missing native requirement. Do not turn that preference into an invented forbidden area. |

## Archive evidence and reproducibility

Read `bmr://authoring`, `bmr://mechanic-implementation`,
`bmr://evidence-contract` and `bmr://implementation-status`. Live health reported
reference revision `bbfa7192aa1216b4e0ac67997eb563dc2e102b26`; that archived source
is distinct from this target fork. Both projection metadata and actual hashed
closed originals were checked. No recording filenames, player names or raw
recordings are included here.

| Recording / source digest | Selected observations |
| --- | --- |
| `8f99fc7bad1fa8ecdbcb1da27bc99ae4d84997d8fe33c68ade7e443dd3fc131b` / `ea6fd4c344db41481e9454bc745bc887821e421ba9701bd8d6e1f4a6e6db24c8` | Elm Cyclone starts seq 896541–896568; effects 903357–903380. Actor 594 created at 855394 and resolves before several older puddles. Actor context pins retained revision `91c28d2871d6e0abec8491bd5ddeecfa462f2eb4547edfc26b1e1a14a6a032bd`. Book Drop starts include 3849556–3849576 and 3858946–3858968, five source actors per selected wave, rather than the workaround's three-tower bound. |
| `aa73fd4bafadb7cd2bee119d54765313bd5f4c841c4752862d9e70cc8597dba4` / `b6e86827fbdc3236267df3a3940d11a6ac886ce9c20f91e30ebecb30884bcfdb` | Acclaim starts 974632–974633 at tick 10606331927; arrow statuses 976611 and 976645 at 10622158558. Baseline sample after 974633 publishes zero cones; candidate publishes two. Later selected samples preserve two visible cones. |
| `465ebc7087b8edb645be95ea82eb3a9cca46a3bb3c7afac03de8a9c645304816` / `6fd82fe7fdec0a45bbb56f1bebdf72c5f71f2b16ca6daca6648d08e4f5eb00b3` | Queen map events 42704–42705 enter X arena; 89656–89658 enter split phase. Exhausted map inventory pins revision `d50d35f84c2751f44f7575653e8855b4c5be683a582a4ad0ab7f9479881e7cb6`. After 43000 baseline has one idle restriction; candidate has none. At split transition 89658 the restriction remains. |
| `14bb654baef1a55f8946333f3ab4de9b897a29fcdbdbcec6d87c0c4e2ecfbc23` | Doomtrain Windpipe starts at 21115, 51121 and 243895. Native source/slot regression is reproduced synthetically; no claim of complete Doomtrain replay acceptance. |
| `8a02883d91b8aed14f8056b1ab4cc676bae74dd7bd88b873f8b6ed4c64aa0916` / `4424fedb7d16db510462fa19e72370a1227719da686b215a18ae5ce3e1e3325e` | Roulette visual finishes 2403320 and 2490385; centre effects 2416009 and 2500011: 14.3654345 and 14.3929249 seconds later. |
| `9edaf2f16d9b237ab36027cb5e114c2aa17f4dfc39a2799b04775ee0c91fdbab` / `9c6ab8b7972ae8c6d232da2f5073e365bbd8192ff427c83c635e888844634945` | Roulette visual finishes 1592930 and 1681733; effects 1604187 and 1692348: 14.3591962 and 14.3767393 seconds later. Third visual 1809042 has no selected subsequent centre effect; not silently paired with a guessed hit. |
| `71117a1e5eaca19826c4f7b13a6805c604fedafada58f7a3ddc78e3a4a4961e3` / `e53c3adff1ca31ad55fbdaee4708ce526ead8965ac7756ee8eeb1029ae87ed0d` | Reserved Roulette evaluation source, inspected after freezing the timer. Visual finishes 736664 and 808030; effects 745294 and 816889: 14.3836526 and 14.3885753 seconds later. Candidate forecasts precede these effects by 83.6526 and 88.5753 ms; both clear on the matching effect. No retuning used this source. |
| `3f32c7ecccf484482554e194aeac9fb62a6a2dcea69c12488d7350064a4bbc44` / `8ed298d6aed68d7c0019f6d6867b0122cdb773758db70cbf539af6ccabdaf112` | Rejected Storm Wave proposal: starts 17303/17986; helper creations 18206/18209/18662/18664 and effects 18418/18420/18875/18876 expose why queue identity alone cannot certify the existing shape/origin model. |

Selected `query_events` pages were exhausted; partial encounter inventories used
for discovery were not treated as complete pattern coverage. Closed-source
replays covered selected Elm, Queen and Acclaim operations before/after, plus
eight Roulette holdout samples. All selected samples were available with zero
module errors and zero skipped operation kinds. Sources used for design are
training evidence; multiple projections of one digest are not independent pulls.

## Validation and limits

- Full Release build: no errors, the same six existing obsolete-gauge warnings
  as baseline. Generated component factories activate all six changed components.
- `tests/ModuleRegression`: 31 synthetic native assertions, covering source
  identity, duplicate callbacks, despawn, fresh waves, pre-status visibility,
  dead/zero-HP/late soakers, split/X arenas, all-slot draw-in and Roulette removal.
  The same suite fails on the baseline in all six affected component groups.
- Release metadata validator and five Python ordering/version tests pass.
- XCAI isolated source build passes using its existing prebuilt ECommons dependency;
  the reference ECommons source currently fails against local development
  assemblies because `ExcelPage` is ambiguous. No reference source was patched.
  XCAI's policy harness passes 368 checks; generic installed-navigation contracts
  are retained rather than deleting their suites with the old encounter fixtures.
  The general knockback/forced-march contract suite also passes against the final
  xBMR assembly. XCAI formatting verification passes.
- Native replay export is intentionally **incomplete**: moving arc-capsule and
  donut-sector geometry are unsupported in the exporter, and the installed game
  data version differs from these recordings. Unknown-floor probes were not
  interpreted as safe space. The comparisons establish selected lifecycle/count/
  timing behavior only, not physical boundaries, safe routes or server snapshots.
- Actual duties remain required by `CONTRIBUTING.md`. No maturity is raised and
  no live xBMR/XCAI gameplay acceptance is claimed. Unsupported migrations above
  remain explicit evidence gaps, not hidden implementation tasks or invented fixes.

Run the synthetic suite after building the full fork:

```sh
dotnet run --project tests/ModuleRegression -c Release -p:EnableWindowsTargeting=true -- \
  /absolute/path/to/BossModReborn.dll /absolute/path/to/dalamud/dev /absolute/path/to/ffxiv/game
```

Private raw runner reports and logs for this task are under
`/tmp/xbmr-migration/`; they are local, ephemeral evidence, not release assets.
