"""Simulator-only S-76 automatic flight and calm rain profile.

Uses X-Plane's native pitch/roll autopilot and a rate-limited collective
speed assist. It does not override physics or teleport a healthy aircraft.
The host relay owns crash recovery; this module never controls real hardware.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
import time


def clamp(value, low, high):
    return max(low, min(high, value))


def collective_step(current, speed_error, dt):
    """Bound both the control range and slew; no integral wind-up."""
    return clamp(current + clamp(speed_error * .0007, -.018, .018) * clamp(dt, 0, .5), .15, .85)


def warm_rain_temperatures(altitude_levels_m):
    """Use X-Plane's reported layer heights, not a hard-coded 13-layer grid."""
    if not altitude_levels_m or not all(math.isfinite(h) for h in altitude_levels_m):
        raise ValueError("Weather altitude levels must be finite and nonempty")
    # Warm standard lapse-rate profile: above freezing throughout the demo's
    # 2,000–10,000 ft envelope, with a bounded upper-atmosphere temperature.
    return [max(-56.5, 28.0 - .0065 * max(0, h)) for h in altitude_levels_m]


def read_demo_turbulence(path=None):
    """Live-reload the explicit demo setting; never interpret it as percent."""
    path = Path(path) if path is not None else Path(__file__).with_name("rotorcraft_weather.json")
    if not path.exists():
        return 0.0
    value = json.loads(path.read_text())["turbulence_factor"]
    # The simulator permits 0..10. Keep unattended rotorcraft demos in a
    # deliberately restricted 0..1 subset; this is not a flight-manual limit.
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or not 0 <= value <= 1:
        raise ValueError("Demo turbulence_factor must be finite and between 0 and 1 (simulator scale 0..10)")
    return float(value)


def apply_calm_rain(set_value, get_value=None, turbulence_factor=0.0):
    # Configure regional static weather; local aircraft samples may differ.
    # No thunderstorm/shear preset: rain and turbulence are separate controls.
    for name, value in (
        ("sim/weather/region/update_immediately", 1),
        # X-Plane enum: 0 rapidly improves/clears the weather; 3 is static.
        ("sim/weather/region/change_mode", 3),
        ("sim/weather/region/variability_pct", 0),
        ("sim/weather/region/rain_percent", .8),
        ("sim/weather/region/sealevel_temperature_c", 28.0),
        ("sim/weather/region/visibility_reported_sm", 6.0),
        ("sim/weather/region/wind_speed_msc", [2.0] * 13),
        ("sim/weather/region/wind_direction_degt", [90.0] * 13),
        ("sim/weather/region/turbulence", [turbulence_factor] * 13),
        ("sim/weather/region/shear_speed_msc", [0.0] * 13),
        ("sim/weather/region/cloud_coverage_percent", [1.0, 1.0, 0.0]),
        ("sim/weather/region/cloud_type", [1.0, 1.0, 0.0]),
        ("sim/weather/region/cloud_base_msl_m", [3000.0, 4800.0, 8000.0]),
        ("sim/weather/region/cloud_tops_msl_m", [4500.0, 6000.0, 9000.0]),
    ):
        set_value(name, value)
    if get_value is not None:
        levels = get_value("sim/weather/region/atmosphere_alt_levels_m")
        set_value("sim/weather/region/temperatures_aloft_deg_c", warm_rain_temperatures(levels))


class S76HoldController:
    def __init__(self, set_value, command, altitude_ft=8000, heading_deg=90, speed_kt=100, clock=time.monotonic, get_value=None):
        if not 60 <= speed_kt <= 120 or not 2000 <= altitude_ft <= 10000:
            raise ValueError("S-76 demo targets outside tested setup envelope")
        self.set = set_value
        self.command = command
        self.get = get_value
        self.altitude = altitude_ft
        self.heading = heading_deg % 360
        self.speed = speed_kt
        self.clock = clock
        self.last = None
        self.last_arm = -100.0
        self.last_weather = -100.0
        self.collective = None

    def step(self, snapshot):
        now = self.clock()
        dt = .1 if self.last is None else clamp(now - self.last, 0, .5)
        self.last = now
        raw, own = snapshot.raw, snapshot.ownship
        if not all(math.isfinite(v) for v in (own.indicated_airspeed_kt, own.altitude_m, own.roll_deg, own.pitch_deg)):
            return "invalid-data"
        if own.altitude_agl_m < 30 or raw.get("sim/flightmodel2/misc/has_crashed", 0) > .5:
            self.collective = None
            return "recovery-required"
        if now - self.last_weather > 60:
            apply_calm_rain(self.set, self.get, read_demo_turbulence())
            self.last_weather = now
        servos = raw.get("sim/cockpit2/autopilot/servos_on", 0) > .5
        modes = int(raw.get("sim/cockpit/autopilot/autopilot_state", 0))
        needs_modes = not (modes & 2) or not (modes & 16384)
        if (self.collective is None or not servos or needs_modes) and now - self.last_arm > 4:
            self.set("sim/cockpit2/autopilot/autopilot_electric_master", 1)
            self.set("sim/cockpit2/switches/yaw_damper_on", 1)
            self.set("sim/cockpit2/engine/actuators/throttle_ratio_all", 1.0)
            self.set("sim/cockpit2/engine/actuators/governor_on", [1] * 16)
            self.command("sim/autopilot/servos_on")
            self.command("sim/autopilot/heading")
            self.command("sim/autopilot/altitude_hold")
            self.set("sim/cockpit2/autopilot/heading_dial_deg_mag_pilot", self.heading)
            # Altitude-hold command captures the current altitude first.
            self.set("sim/cockpit2/autopilot/altitude_dial_ft", self.altitude)
            self.last_arm = now
        actual = raw.get("sim/cockpit2/engine/actuators/prop_ratio_all", .5)
        if self.collective is None:
            self.collective = clamp(actual, .15, .85)
        self.collective = collective_step(self.collective, self.speed - max(0, own.indicated_airspeed_kt), dt)
        self.set("sim/cockpit2/engine/actuators/prop_ratio_all", self.collective)
        return "hold" if servos else "arming"
