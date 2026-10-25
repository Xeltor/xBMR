# RSR targeting preferences and fallback

Native targeting consumes RSR's live original `IsTopPriorityNamedHostile`,
`IsTopPriorityHostile` and `ValidityCheck` predicates. The latter calls
`IsAttackable`, including the user's FATE-only, Forlorn-focus and other-player
quest-enemy exclusions, blacklist and configured stop marks. No duplicate option
list or hardcoded nameplate/category decoder is maintained in xBMR.

This import requires RSR's live `AutorotationActive` IPC confirmation as well as
explicit active Henched authority (`State`, `IsHenched` and
`IsManual`, excluding target-only/PvP). A retained `CurrentRotation` is not active
authority. Off, Auto and explicit Manual do not import these rules into native
BMR hints. In Henched, the original `IsAttackable` bypasses its general engagement
tail while keeping its exclusions. Only in that context can `ValidityCheck`
rejection be treated as a hard exclusion. Restrictive engage settings are a
separate fallback decision, and cannot veto a BMR-assigned passive enemy. Failed
or unavailable observation is distinct from a demonstrated exclusion: it grants
no acquisition preference but cannot fabricate a prohibition over native hints.

Specific preferences (enemy parts, FATEs, Forlorns, hunt/relic/leve, quest and
configured named targets) can admit a passive preferred enemy before combat.
Quest priority therefore retains its engagement override in Henched. Ordinary
passive enemies are not admitted by Henched's unrestricted `IsAttackable` branch.
Other-FATE, unsynced/completed-FATE, dying, height-separated, invincible and
module-forbidden enemies retain independent restrictions. Only frozen decisions
are stored in hints; live wrappers and provider delegates stay on the framework.

BMR positive target priorities, explicit assignments and forced targets are above
RSR preferences. Specific acquisition establishes eligibility at priority zero
before module hints. Preferences are ranked only after module hints, when no
authored targeting exists. Named RSR targets rank above ordinary RSR priorities.
Hard user exclusions remain exclusions after module writers, so movement cannot
chase a target that the configured rotation refuses to attack.

When no authored, native pack or specific target is eligible, the engage setting
is the fallback. Capture `CurrentTargetToHostileType`, including the provider's
active override, and apply the selected all-targets, previously-engaged, solo,
solo-in-duty or solo-deep-dungeon policy. Use RSR's original smart deep-dungeon
predicate; do not inherit Henched's blanket engagement shortcut. Consider only
RSR's current hostile collection and admit one fallback target, ordered by native
distance. Existing native pack selection resumes after engagement.

Normal fallback acquisition requires an original RSR single-target offensive action to pass
its current readiness, range/LOS and target eligibility checks. These do not call
`CanUse`/concrete `FindTarget`, replace the rotation or authorize executing an
action. The original rotation still chooses and validates the actual action.
`TargetFreelyEnabled` permits the separate no-action nearest-target recovery only
in combat. Disabling it does not disable normal action targeting, and enabling it
does not override the selected engage rule or hard exclusions. No mode or saved
setting is changed, and RSR's independent target writer stays suppressed during
native selection. Native activation, acquisition opt-outs, preset retarget rules,
manual handoff, freshness and arena/hazard safety remain authoritative.

Regression coverage includes live predicate changes, specific acquisition,
exclusion precedence, native module/assignment precedence, all engage modes,
nearest-target opt-out and installed provider delegate contracts. Offline checks
and builds are separate from live North/South Horn or other gameplay acceptance.
Authority regression runs the same enemy initialization and post-module hint
passes as `AIHintsBuilder`, normalizes the hints and checks native acquisition.
It covers inactive RSR with a retained rotation, active Auto/Manual, stale/unknown
mode flags, a BMR-assigned passive enemy under restrictive engagement, unassigned
passive fallback rejection, genuine exclusions and Henched FATE acquisition.
