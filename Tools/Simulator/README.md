# Rotorcraft demonstration support

These tools control **only the X-Plane simulator**, not an aircraft. They are
research/demo automation, not certified flight-control or navigation software.

## Active host configuration

The `4090` host now uses the installed **Sikorsky S-76C** for the unattended
rotorcraft demonstration. The former R22 branch only observed telemetry; it did
not engage flight controls. Its inherited targets were also inappropriate for a
helicopter. A low-attitude crash could evade the old attitude-only recovery test.

The host's existing `xplane12-autoflight.service` loads
`xplane12.host.rotorcraft_hold.S76HoldController` from its relay. It uses native
heading and altitude hold, governor/flight throttle, yaw damping, and a
rate-limited collective speed assist. Healthy flight is never teleported and
the physics model is not overridden. Native `has_crashed` now also triggers the
existing timed air-start recovery. Automatic recovery is visible as a new flight,
not counted as uninterrupted stable flight.

Selected demo settings in the host's existing environment file:

```ini
XPLANE_AIRCRAFT_PATH="Aircraft/Laminar Research/Sikorsky S-76/S-76C.acf"
TARGET_ALTITUDE_FT=8200
TARGET_HEADING_DEG=90
TARGET_SPEED_KT=100
RECOVERY_ALTITUDE_FT=4000
```

The altitude selector is a simulator autopilot reference; geometric MSL on the
HUD can differ from the autopilot's barometric hold altitude. The controller
captures the current hold altitude on engagement. This is an altitude/heading
demonstration, not automatic route following, terrain avoidance, or landing.

`rotorcraft_hold.py` is deployed next to the host relay. The relay creates it
only for the S-76, invokes `step(snapshot)` in the telemetry loop, and does not
apply the legacy severe-weather preset to rotorcraft. Host files changed for
integration have dated `.bak-rotorcraft-20260907` backups. The telemetry
subscription list has a `.bak-hud-limits-20260907` backup.

## Rain profile

`apply_calm_rain` uses regional, writable X-Plane 12 DataRefs: precipitation
ratio 0.8, overcast stratus layers, 6 SM reported visibility, 2 m/s wind, zero configured
shear, and a warm temperature profile. `change_mode=3` selects static
weather; `0` was rapidly improving weather and cleared the rain. The warm profile
uses X-Plane's reported atmospheric layer heights instead of assuming a fixed grid.
A ratio is not a rain rate
or probability of precipitation. The aircraft's point sample can differ from
the region and change as it moves; inspect the live API sample to verify it.
The profile is re-applied by the demo controller every 60 seconds. After deployment,
`sim/operation/regen_weather` was invoked once to regenerate the cloud system immediately;
`update_immediately` alone does not regenerate the clouds.

`rotorcraft_weather.json` now explicitly sets turbulence to **0.2 on X-Plane's
0–10 factor scale**, in all 13 regional layers. It is re-read at each 60-second
weather refresh, so the helper no longer silently resets it to zero. The demo
config accepts only 0–1, a deliberately restricted subset—not an aircraft limit.
Missing config defaults to zero; invalid config is rejected. The 0.5 initial
trial remained airborne but exceeded the existing demo stability checks, so it
was reduced. This is not a claim of guaranteed unattended stability.

The subsequent 90-second 0.2 trial collected 78 fresh samples with no crash,
but **failed stability acceptance** (48 violations): geometric altitude rose
from about 9,844 to 10,935 ft, and vertical speed reached +1,644 ft/min. The
existing altitude/collective controller still needs separate tuning under
turbulence. Telemetry delivery and absence of a crash are not proof of stable
altitude hold. Do not leave this scenario unattended on the strength of these tests.

The host exports all 13 `sim/weather/region/turbulence[i]` base settings and
13 read-only `sim/weather/aircraft/turbulence[i]` local-column samples. Unity
accepts the documented 0–10 scale and the briefing reports their maxima separately,
not as percentages. **Neither array is a spatial turbulence radar scan.** TURB
shows the values and an explicit no-scan message; it does not paint invented
magenta turbulence locations or relabel rain as turbulence.

The Unity bridge synthesizes spatial weather imagery from weather DataRefs.
Its `SIM WX` overlay is illustrative, not a measured weather-radar return, and
must not be used for real weather avoidance.

## Telemetry retained after the UI rollback

The experimental instrument cards, bank/mode strip, expanded weather footer,
and speed-margin UI were rolled back at the user's request. The earlier HUD
presentation is active. No speed-limit advisory is currently shown.

The additional telemetry below remains available for future work. `acf_Vne`
is a static simulator aircraft reference, not a computation of every altitude,
temperature, loading, configuration, turbulence, or flight-manual restriction.

The host snapshot subscriptions now include:

- `sim/aircraft/view/acf_Vne`, `acf_Vno`
- `sim/aircraft2/metadata/is_helicopter`
- `sim/aircraft/prop/acf_en_type[0]`
- `sim/aircraft/engine/acf_RSC_redline_eng`
- `sim/cockpit2/engine/indicators/engine_speed_rpm[0..1]`
- `sim/cockpit2/engine/indicators/MPR_in_hg[0..1]`
- `sim/flightmodel2/misc/has_crashed`
- `sim/cockpit2/autopilot/servos_on`, `speed_status`

The existing engine bars are numeric references, not aircraft-specific safe
operating bands. The underlying altitude telemetry is geometric MSL and must
not be treated as a certified barometric altimeter.

## Verification

```sh
python3 -m unittest discover -s Tools/Simulator -p 'test_*.py' -v
python3 Tools/Simulator/verify_flight.py --seconds 180
```

The second command is read-only. It samples live telemetry once per second and
reports ranges and violations for freshness, crash state, clearance, attitude,
airspeed and vertical speed. Its bounds describe this demo acceptance test, not
aircraft operating limitations. A passing finite test does not guarantee
indefinite unattended operation.

The restored Unity presentation was checked with `FaaInstrumentPresentationTests`
(41 passed). The rotorcraft controller has eight unit tests. After the corrected
rain profile, a 60-second live check collected 55 samples with no violations:
IAS 93.57–93.70 kt, geometric altitude 8,327.97–8,329.28 ft, and vertical speed
−3.54 to +7.26 ft/min. Aircraft precipitation stayed at 0.796 throughout the run,
with no crash or recovery event. These values are observations, not operating limits.
These are observations from that finite run, not a promise of indefinite stability.

## Primary references

- [X-Plane local Web API](https://developer.x-plane.com/article/x-plane-web-api/)
- [X-Plane 12 weather DataRefs](https://developer.x-plane.com/article/weather-datarefs-in-x-plane-12/)
- [Helicopter governors and correlators](https://developer.x-plane.com/article/helicopter-governor-and-correlator-configuration/)
- The installed simulator's `Resources/plugins/DataRefs.txt` is used to verify
  current names, units, writability, and enum values.

No API keys, SSH passwords, or machine environment files belong in this folder.

## Mixed AI traffic (4090)

`mixed_traffic.json` configures 12 native ATC AI aircraft: C172, S76, PA18, B738,
BE58, R22, BE9L, MD82, SR22, S76, C750, A333. This provides general aviation,
helicopter and airliner silhouettes. Military symbols remain supported but the
new F-4 metadata copy was skipped by the running simulator, so it is not included
in this verified profile. Stock aircraft were not modified.

`apply_mixed_traffic.py --xplane-root <installed-root>` validates without writes;
add `--apply` to update only AI aircraft via the local native flight API. It checks
the actual TCAS ICAO bytes after loading, including unchanged ownship type, and
fails on mismatches instead of claiming success. Remote air-start defaults now
use this same mix for future automatic recovery. Category codes come from native
metadata, not a fixed slot list or speed estimates.

Host telemetry changes are in `xplane12/bridge/webapi_client.py` (decode the
single base64 ICAO array into numeric snapshot fields) and
`xplane12/compat/legacy_categories.py` (subscribe type/override/turbulence).
The host has dated `.bak-mixed-types-20260910` and `.bak-turbulence-20260910`
backups. Only the data API and flight helper were restarted; the helper used
`--skip-air-start` during deployment, leaving X-Plane and ownship running.

Verification on 2026-09-10: 95 direct Unity assertions passed (including 17
type-metadata and 15 turbulence-mode cases); 21 host Python unit tests passed.
Live native ICAO readback matched all 12 requested AI slots, and Unity counted
6 General, 3 Helicopter and 3 Commercial targets. Both turbulence arrays had
13 samples at 0.2, including after the periodic weather refresh. Pilot Brief's
captured evidence contained the same values with `spatial_scan_available=false`.
