# xBMR 7.5.7.2.2

Upstream remains 7.5.7.2 at `ae64e3309905dafad0952dbe72680add167591f9`.
Fork revision 2 installs as 7.5.7.202.

- Objective-free out-of-combat navigation no longer starts because an NPC is
  selected, collision is nearby or inactive encounter bounds remain loaded.
  Combat optimization requires engagement or an active encounter, with authored
  forced movement retaining its safety path. Party-facing also requires an
  active encounter and stable consensus from observed party members.
- Tank ranged filler joins melee filler in yielding to freshly verified melee
  access. Access timing accounts for hitbox entry, GCD readiness and action
  locks while still validating the complete onward route. Automatic delivery
  retains the actual recipient; explicit actions and tank aggro recovery remain
  independent. Initial mobility assessment uses the existing 250 ms freshness
  ceiling and cannot be extended by equivalent callbacks.

Validation: `python scripts/release-version.py`, all nine Python release tests,
and the Release build passed. All 20 native/encounter/compatibility regression
suites passed against that exact DLL, including 143,459 native movement
assertions and 74 Questionable compatibility assertions. The plugin build
retains six existing obsolete gauge API warnings and introduces no new warnings.
These checks do not establish live gameplay acceptance of the reported door/NPC
interactions or melee recovery behavior.
