# Mandatory standalone XCAI retirement

2026-10-24 follow-up, explicitly requested after the implementation handoff.
Mandatory removal was verified on the development client. The revised completion
modal and durable acknowledgement pass offline regression checks. It is prepared for the authorized 7.5.6.35.19 stable cutover.

## Runtime behavior

Updated xBMR finds every exact `XelsCombatAI` installation, including disabled
copies. After login, automatic removal waits for combat, casts, zoning and
cutscenes to end. It does not ask for consent or wait for acknowledgement.
After the exact XCAI installations are gone, a persistent Material modal confirms
that XCAI is now part of xBMR and has been removed. Its only button is **Got it**.
That button dismisses the completed-migration notice; it cannot approve, delay,
cancel or prevent removal. No chat notice or timed notification is used.

The versioned `xcai-retirement-notice.json` receipt preserves pending/completed
removal and acknowledgement independently of assistance-settings import. An
unacknowledged popup survives reload/logout and has no timeout. Acknowledgement
suppresses it on later startup, including if XCAI is reinstalled and removed
again. Interrupted removal never produces a success notice while XCAI remains.

Dalamud's asynchronous unload must finish for the exact installation before
`ScheduleDeletion(true)` and `RemovePlugin` are called. These are installer
operations; configuration and recording directories are never deleted. The
installer schedules binary cleanup for restart. A development copy instead has
startup/reload disabled and its game registration removed without deleting its
source. An exact development DLL location is disabled persistently; shared
directory registrations are preserved so other plugins are not disabled.

Integrated assistance, import and the `/xcai` alias wait for retirement to finish.
Installer identity changes reject stale completion. Failures are logged and
retried after 30 seconds. No frame blocks on unload, and xBMR shutdown awaits an
already started unload without continuing removal after its owner stops.
Installing or removing XCAI does not activate native AI.

The installer adapter uses a narrow, verified Dalamud internal contract because
the public plugin interface has no uninstall method. Its contract is tested
against the supported installed Dalamud assembly. A future contract change fails
closed and leaves assistance inactive while removal remains pending.

## Feed cutover

The existing feed repository's `publish-xbmr.yml` updates xBMR and removes the
exact standalone XCAI entry in one generated-feed write. Removal requires the
actual built replacement ZIP to contain the approved `XCAI-retirement.json`
policy and a matching replacement manifest, DLL, version and visible feed URL.
Unrelated entries are preserved. Standalone stable/testing feed writers reject
`XelsCombatAI`, preventing an old publication from restoring its listing.

The maintainer authorized commit, push and stable publication of 7.5.6.35.19
(Dalamud 7.5.6.3519). The preceding implementation baseline advertised xBMR
7.5.6.3518 and XCAI 3.1.7.0. That baseline is historical; the generated feed and
immutable release page are authoritative after publication. Existing v18 tags
and assets must never be overwritten.

Standalone source now has retirement-only agent instructions and a retired
publication skill. Its release/testing/scheduled/validation workflows are removed,
and repository Actions are disabled. A dry-run dispatch of its historical release
workflow against v3.1.7 was rejected with HTTP 404. Shared publishers and the
release-assets/feed-writing scripts reject XCAI. Do not reverse these guards or
restore a standalone listing without explicit new human-maintainer authorization.

Removal reaches users when they load updated xBMR. An offline client or one that
never updates cannot execute newly written retirement code. No standalone
retirement update is relied on after its feed entry disappears.

## Validation

- Managed Release build: zero errors; six existing upstream gauge warnings.
- Native suite: 42,888 assertions, including mandatory removal without any
  acknowledgement, disabled installs, combat delay, asynchronous completion,
  identity replacement, failure backoff, owner shutdown, completion-only notice,
  durable acknowledgement and initial-load/shutdown races. Installed Dalamud
  retirement contract verified.
- Nine repository Python tests passed. Final release native and broader suite
  receipts are retained separately from the earlier modal-development evidence.
- Six feed retirement/publication-block tests passed, including rejected premature cutover,
  package/version mismatches, unrelated-entry preservation and blocked re-adds.
- The preceding retirement DLL and unchanged policy were packaged through the
  shared packaging helper. A local feed preview removed only XCAI; no publication
  occurred. The modal refinement was built separately.
- Initial removal test: fresh out-of-combat preflight, normal watched-output
  reload and loaded MVID verified. The superseded chat notice was captured before
  XCAI disappeared from
  the installed-plugin inventory. Its disabled development registration has
  startup/reload and its exact location disabled persistently. All six legacy
  configuration files remained byte-identical; the source DLL remains present.
- Current native settings are Ready, following is off, rotation mode is Off,
  and diagnostics report no failure. The bounded chat trace was stopped.

Revised modal artifact SHA-256:
`3133cd9ff0bc87485ceb310f3d69821bb9b7759893719603af3e4089e81d2168`.
Built MVID: `656ca5dd-204b-443d-ad97-510e1039f842`.

The development modal DLL was copied to the watched development output after another fresh
out-of-combat preflight. Because the prior test had already removed XCAI, a
completed-removal receipt was recorded for that verified removal to exercise the
corrected popup on the existing client. The desktop screenshot helper could not
capture this Wayland session; visual popup appearance and clicking its button
have not been independently verified live. Durable acknowledgement was verified
with real temporary receipt files in the native regression suite.

Live removal was exercised with the existing disabled development installation.
A packaged normal install and restart-time scheduled binary cleanup have not
been exercised live. Those limits do not establish dungeon gameplay acceptance.
