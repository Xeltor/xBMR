# xBMR 7.5.7.3.0

Upstream is 7.5.7.3 at `c35c484a76cdf36061990996b48a35939b976be8`.
Fork revision 0 installs as 7.5.7.300.

- Integrates the upstream 7.5.7.3 upgrade while retaining native combat
  assistance, encounter corrections, existing settings and public IPC.
- Moves combat-assistance settings into the main BMR Automovement page using
  the upstream interface styling. `/bmrai` opens that page. The Enable AI toggle
  indicates activation; redundant status text and the unaccepted follow selector
  are omitted. Explicit native follow commands remain available.
- Adds opt-in Questionable on/off control for its active run. Select **BossMod**
  in Questionable and enable **Allow Questionable to turn AI on and off** in
  Automovement General, or use `/bmrai questionable on`. The permission defaults
  off. Questionable's preset payloads and setup changes never become xBMR policy;
  movement, rotation-link and other feature settings remain authoritative.
  Stop, lost provider state or revoked permission releases only activation owned
  by this integration. Manual activation and manual movement retain control.
  No changes to Questionable are required.

Validation covers release metadata and ordering, the Release build, native
movement regressions and the installed Questionable provider's combat-start,
solo-duty, movement-transition and cleanup protocols with inert subscribers.
The plugin build retains six existing obsolete gauge API warnings in unchanged
source. Development reload and offline checks do not establish live quest,
combat, encounter or performance acceptance.
