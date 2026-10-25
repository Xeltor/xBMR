# Sage trash AoE approach

The maintainer reported lost Sage trash AoE uptime on 2026-10-25. Archived XCAI
had a local healer pack-approach policy; the native integration only retained a
25-yalm healer combat-range objective. RSR chooses Dyskrasia from enemies already
inside its effect, so selected-action capture alone cannot request the approach.

Framework capture now reads the current unlocked Dyskrasia variant, original
saved action enable flag, RSR Full AoE mode, per-action minimum count, BasicCheck
and lifetime/status/resistance predicates. Existing native/RSR target validity,
engagement, blacklist and stop-marker checks remain authoritative. No CanUse or
FindTarget call mutates RSR selection, and no unselected action is submitted or
credited as delivered. Healing demand and selected healing suppress the elective
approach; compulsory movement retains its own control. An active module alone
does not suppress approach: dungeon trash also has encounter modules. Their
boundaries, hazards and authored assignments remain binding.

Frozen eligible positions define conservative discs guaranteed to cover the
configured count and current target with Dyskrasia. Each disc includes height,
enemy hitbox and arrival-tolerance arithmetic, and excludes idle/forbidden
collateral. Enemy positions and the local pack centroid propose the discs; this
does not claim an exhaustive optimum over all possible pack arrangements.
RSR still selects the actual action after entry. The five-yalm effect radius is
confirmed by the [official Sage guide](https://na.finalfantasyxiv.com/jobguide/sage/)
and is read from current action metadata rather than hard-coded for movement.

While genuine pack access is missing, unstarted stationary Dosis-family filler
does not reserve every following GCD and stop the approach. The native current
cast unlock remains binding; selected healing and instant actions remain in the
evidenced sequence. This matches the existing melee ranged-fallback distinction,
without cancelling an actual cast or predicting the future AoE action.

The existing typed range recovery service owns walking and any enabled legal
mobility. Current action delivery, independent healing recipients, arena bounds,
hazards, assignments, manual control, explicit range instructions and saved
movement/AoE opt-outs remain binding. Capture refreshes moving packs and removes
the objective when the configured count or eligibility no longer supports it.
Build and synthetic route checks do not establish live trash-pack acceptance.
