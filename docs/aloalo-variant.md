# Aloalo Island (Variant)

The 7.5.8.10 merge integrates the new Quaqua and Ketuduke route components under the existing xBMR module identities. Quaqua is registered at `0x40BA` and Ketuduke at `0x4091` in duty `961`. The merge keeps the original per-mechanic forecasts and AI handling for shared mechanics, including Quaqua's numbered Hammer Landing sequence and Ketuduke's observed wind direction, Saturate cast origins, Twintides sequence and per-player Water III guidance.

- [BossMod Reborn 7.5.8.10](https://github.com/FFXIV-CombatReborn/BossmodReborn/releases/tag/7.5.8.10) supplies the imported route components and their reference timings.
- The [Aloalo Island Variant guide](https://ffxiv.consolegameswiki.com/wiki/Aloalo_Island#Bosses) and [Icy Veins Variant guide](https://www.icy-veins.com/ffxiv/variant-dungeon-guide-aloalo-island) describe the route conditions and player responses. The guides do not establish exact timings or hit boundaries.

## Quaqua

The Variant guide distinguishes the left weather branches, middle water or poison spears, and right statue gazes or pursuing orbs. xBMR keeps its left-path Scalding Waves and Cloud to Ground forecasts. Middle-path Elemental Impact, rotating Flowing Lance crosses and growing poison pools, plus right-path statue gazes and Arcane Pursuit, come from 7.5.8.10. Rotating crosses use the existing AOE bright and shaded colors for the imminent and next steps. Statue beams harm the orbs and are a positioning preference only for pursued players. Orb movement snapshots, disappearing or overlapping beams, the route-specific hazard timings and safe paths still need replay checks. The module remains WIP.

The three arena centers come from TerritoryType `1176`'s `bg/ex4/09_ocn_o5/dun/o5d1/level/bg.lgb` (SHA-256 `e4c912540afea0cfff1f33e5dd34f723a589440e1b33126d7ef512d6644432e2`). Its CollisionBox instances `10146156`, `10146163` and `10146155` have translations `(-538,18,94)`, `(50,50,-160)` and `(850,219,49)`, identity rotations and unit scales. All three reference `bg/ex4/09_ocn_o5/dun/o5d1/collision/o5d1_xx_col01.pcb`, the mesh previously decoded as a nominal radius-20 perimeter. The module picks the closest authored center when created and retains it through the fight. It constrains AI pathfinding from the first frame, including activation while the boss is off center. The mesh is independent of player positions. The exact server damage-over-time boundary and activation remain unverified.

## Ketuduke

The module keeps the existing authored square perimeter at `(-790,-395)`. The Variant guide describes separate paths for crossing Hydrosurge in a bubble, avoiding the ground Zaratan cleave, and entering or avoiding bubbles according to the Ogrebon's elevation. Route 2 adds Tidal Wave, the persistent Hydrosurge strip, Wavefoam bubbles and Anila's knockback for bubbled players. The Tidal Wave setup uses the native directional knockback boundary. Routes 3 and 4 add the Zaratan and Ogrebon mechanics. Wavefoam provides setup guidance for the elevation mechanics, including the Aerial Shock case where planar exclusion of the whole arena would also block access to the safe bubbles. These route-specific changes still need replays of crystal/bubble overlaps and the arrival timing, duration and resolution of both bubble outcomes. The module remains WIP.

Wavefoam tracks bubble actors individually and clears their guidance on resolution. Bubble Net status `3744` is the player eligibility signal, corroborated by the archive's Status sheet for game build `2026.09.15.0000.0000`.

The archive had no retained recordings matching the inspected new-branch actions Flowing Lance clockwise (`35745`), Arcane Pursuit (`35748`), Tidal Wave damage (`36114`) or Aerial Shock damage (`35484`) at merge time. The focused synthetic suites exercise module registration and the existing callback and geometry contracts. They cannot establish live route or timing acceptance.
