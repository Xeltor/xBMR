# The World of Darkness

Level 50 alliance raid, territory 151, content finder 111. This work covers the four bosses. The existing module factories use the boss OIDs below. The encounter components publish player warnings and radar shapes through the usual module callbacks. The same active components provide forbidden zones, required destinations, damage forecasts, target priorities and interaction targets through `BossModule.CalculateAIHints` and `AIHintsBuilder`.

| Boss | Boss OID | Coverage and response |
| --- | --- | --- |
| Angra Mainyu | `0xE00` | Alternate Double Vision brands, look away for Mortal Gaze, use lit cleansing pads for Doom, clear the Level 100/150 circle as an unmarked player, and dodge Stare and Daewa casts. Kill Final Hourglasses and leave the final Roulette quadrant. The radar shows one subdued sector while Roulette spins and switches to a danger fill and AI avoidance when it locks. |
| Five-Headed Dragon | `0xDFC` | Avoid the frontal and elemental breaths and the growing Ice Floor. Share Thunder, spread Fermata, stop moving and acting for Heat Wave, kill the extra heads for Discordance and prioritize Prominence and slimes. A resistant player can suppress each poison pool. Prey handoff pauses during an active Thunder stack and resumes if Prey is still present when the stack resolves. |
| Cerberus | `0xDF6` | Avoid the surface casts and route on the correct physical floor. Assigned players take Mini before entering a purple puddle and prioritize stomach walls once swallowed. The live puddle is an explicit surface hazard for other players, with a safe arrival marker for an eligible Mini entrant. A damaging Slabber cast, Seized, Abandonment, transport or Reawakening blocks the ingestion assignment. Surface players prioritize Wolfsbanes and eligible volunteers attach both chains through the native interaction hints. |
| Cloud of Darkness | `0xCFA` | Dodge Zero-form and bait the chasing beam. Soak towers and prioritize approaching clouds. Enter the assigned Hypercharged Cloud box before closure, then fight within it without a forced center position. A chased player gets beam avoidance, and players assigned a box are excluded from tower assignments. The first cloud spawn supplies an estimated Flood of Darkness damage time. |

## Evidence

- The two written guides agree on the color swap, gaze, rotating quadrant, elemental responses, Mini and chain sequence, meteor soaks and Hypercharged Clouds. The guides describe the responses and strategy. They differ on some details, such as whether poison-pool occupancy removes the pool or suppresses spawns, and how many extra dragon heads are described. The module retains the existing occupancy response and the four independently targetable head OIDs. [Normal raid guide](https://www.thonky.com/final-fantasy-xiv/duties/the-world-of-darkness-duty), [raid wiki](https://ffxiv.consolegameswiki.com/wiki/The_World_of_Darkness).
- Existing IDs and arena definitions come from the registered encounter modules. Existing archived-mechanic work is recorded in `301ca4054`, `936d97282`, `2cced8be3` and `56f8f8cdd`. The Roulette preview uses the existing live sector selection and the existing lock cues. Its shade follows the convention used by growing Ice Floor and the shared sequential AOE components.
- The retained archive contains three declared originals for this duty, all from game build `2026.10.06.0000.0000`. These changes reviewed selected event windows from the recordings below. The third recording (`ca0a42b0db0e`, revision `522cacc1bb67`) provided Angra Stare actions (seq 118614, 127227) and Discordance cast observations (seq 400512, 407950, 408467). The latter show changing remaining cast durations, so the module continues to use actual cast events.
- Recording `5ae847c3627d`, retained revision `2703390abd97`, places the southwest, north and southeast box barriers at OIDs `0x1E9752`, `0x1E9750` and `0x1E9751` (seq 264158–264160). The opening cue, cloud spawn, closure and reopening occur at seq 370597–370669 and 379533–381408. Recording `8c419584ed5a`, retained revision `a1d07c184b2a`, corroborates the positions (seq 927658–927660) and lifecycle (seq 1058123–1058125 and 1067571–1071058). The old code associated all three closures with the wrong alliances.
- Flood `3305` lists 24 targets in both recordings. The first ordinary cloud spawn and Flood are at seq 313987 and 366624 in `5ae847c3627d` (68.93 seconds apart) and 995557 and 1051905 in `8c419584ed5a` (68.80 seconds). The module publishes an early 68.5-second estimate from the first observed wave per pull. It clears the estimate on impact, box phase, expiry or boss death. A module activated with existing ordinary clouds skips the estimate. In the first recording, beam icon 197 precedes the initial location cast `3298` (seq 275406 and 275507), so the marker preview follows the player until the cast supplies a position.
- Cerberus cast and actor lifecycle anchors include Mini, Slabber and Reawakening at seq 111080, 113016 and 161566 in `5ae847c3627d`. In `8c419584ed5a`, Slabber begins at seq 752742 and the persistent purple event object `0x1E968C` appears at seq 760608. The puddle uses the existing eight-yalm avoidance model. The overlap and stale-puddle tests are synthetic edge cases.

## Deterministic validation

`tests/WorldOfDarknessRegression` runs synthetic WorldState operations through the built plugin. The suite checks generated registration and activation for all four bosses, player-facing hazard state, safe and unsafe points in native hints, predicted damage, target priority, interaction eligibility, resolution and cancellation. It also runs the pre-existing tests for the intervening encounters. The Release candidate passed all 386 assertions.

With the repository's SDK and matching Dalamud references, build the Release plugin, then run:

```sh
dotnet run --project tests/WorldOfDarknessRegression -c Release \
  -p:BossModAssembly="$PLUGIN_DLL" -- "$PLUGIN_DLL" "$DALAMUD_HOME" "$GAME_DIRECTORY"
```

Use absolute paths and the game directory that contains `sqpack`.

## Acceptance limits

- These tests exercise simulated callbacks and module outputs. Original replay playback, physical routes, server snapshots and autonomous live clears were outside this validation.
- **Angra Mainyu.** The late Roulette lock, timeout branch and Doom cleanse travel margin still need physical validation. A reload may miss an already-lit pad or marker.
- **Five-Headed Dragon.** The handoff pause is a safety policy for conflicting destinations. A real overlapping Thunder and Prey expiration was unavailable in the reviewed windows. Poison suppression uses an interior arrival point; exact pool boundaries, eligibility and unobserved timing variants remain open.
- **Cerberus.** Ingestion assignments assume a sufficiently complete alliance roster and honor the configured manual strategy. The archive did not establish an exact Abandonment proximity threshold, safe Gastric Juice bait placement, or recovery routes for a failed ingestion or incomplete chain sequence. Those branches can still require player input.
- **Cloud of Darkness.** Flood timing is an estimate from two recorded waves. Failure variants and late attachment need further evidence. Box centers provide staging destinations; temporary wall collision, missed closure recovery and precise meteor and beam deadlines still require gameplay validation.
- All four modules remain WIP because those gaps can affect an autonomous clear.
