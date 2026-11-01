# Out-of-combat movement ownership

Selected NPCs, nearby collision and inactive encounter geometry are not movement
objectives. Previously, selecting a target or retaining custom bounds could
bypass the idle shortcut, and enabled assistance alone could authorize the
combat optimizer with no engagement. This left destination preferences active
while another plugin was approaching a door, wall or NPC.

The shared native movement host now skips objective-free idle navigation even
with a selected target, temporary physical obstacles or inactive custom bounds.
Combat optimization requires native player/party engagement or an active
encounter timeline. Authored forced movement retains its separate safety path.
Deliberate following, explicit destinations, interaction goals and mechanic
warnings still receive native navigation. Active arena bounds and route
constraints remain binding whenever navigation is needed.

Arena-edge preferences already require an active encounter state and continue
to do so. Party-facing now also requires an active encounter state rather than
mere module presence. It still requires stable consensus from nearby living
player party members; an empty party never authorizes a cosmetic turn. Active
immunity downtime can retain this behavior when the local combat flag drops.

Regression coverage includes NPC selection, walls without objectives, inactive
versus active arena geometry, explicit and following goals, future hazards,
combat optimization authority, and an empty party. Release build and native
movement regressions passed (143,455 assertions); the build retains six existing
obsolete gauge API warnings. These checks do not reproduce the reported plugin
interaction in live gameplay.

The validated development DLL automatically reloaded as version 7.5.7.201,
MVID `ba97ab58-e46f-4cb5-826a-81e5316e10d1`. DevBridge observed Settings Ready,
no movement plan and no failure. Native AI and linked RSR were off after reload;
they were not automatically reactivated. This is loaded-build proof, not live
acceptance of door/NPC interactions with other plugins.
