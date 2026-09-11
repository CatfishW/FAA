import unittest
import json
from unittest.mock import patch
from types import SimpleNamespace
from rotorcraft_hold import collective_step, S76HoldController, apply_calm_rain, warm_rain_temperatures, read_demo_turbulence


class RotorcraftTests(unittest.TestCase):
    def test_collective_slew_and_bounds(self):
        self.assertAlmostEqual(collective_step(.5, 100, .1), .5018)
        self.assertAlmostEqual(collective_step(.5, -100, .1), .4982)
        self.assertEqual(collective_step(.85, 100, 1), .85)
        self.assertEqual(collective_step(.15, -100, 1), .15)
        self.assertLessEqual(collective_step(.5, 100, 999), .509)

    def test_jet_targets_rejected(self):
        with self.assertRaises(ValueError):
            S76HoldController(lambda *x: None, lambda *x: None, speed_kt=240)

    def test_rain_does_not_require_turbulence_or_extreme_wind(self):
        writes = {}
        apply_calm_rain(lambda key, value: writes.update({key: value}))
        self.assertEqual(writes["sim/weather/region/rain_percent"], .8)
        self.assertEqual(writes["sim/weather/region/turbulence"], [0.] * 13)
        self.assertLess(max(writes["sim/weather/region/wind_speed_msc"]), 3)

    def test_rain_is_static_not_rapidly_improving(self):
        writes = {}
        apply_calm_rain(lambda key, value: writes.update({key: value}))
        self.assertEqual(writes["sim/weather/region/change_mode"], 3)
        self.assertEqual(writes["sim/weather/region/variability_pct"], 0)
        self.assertEqual(writes["sim/weather/region/cloud_coverage_percent"][:2], [1., 1.])

    def test_turbulence_setting_survives_periodic_rain_refresh(self):
        writes = {}
        with patch("rotorcraft_hold.Path.exists", return_value=True), patch("rotorcraft_hold.Path.read_text", return_value='{"turbulence_factor": 0.5}'):
            apply_calm_rain(lambda key, value: writes.update({key: value}), turbulence_factor=read_demo_turbulence())
        self.assertEqual(writes["sim/weather/region/turbulence"], [.5] * 13)

    def test_turbulence_config_rejects_invalid_or_excessive_demo_values(self):
        for value in (True, -1, 1.1, "0.5", float("nan"), float("inf")):
            with patch("rotorcraft_hold.Path.exists", return_value=True), patch("rotorcraft_hold.Path.read_text", return_value=json.dumps({"turbulence_factor": value})):
                with self.assertRaises(ValueError):
                    read_demo_turbulence()

    def test_missing_turbulence_config_defaults_to_calm(self):
        with patch("rotorcraft_hold.Path.exists", return_value=False):
            self.assertEqual(read_demo_turbulence(), 0)

    def test_warm_profile_uses_reported_altitude_levels(self):
        writes, reads = {}, []
        levels = [0., 777., 2500., 3048., 8000., 17000.]
        def read(key):
            reads.append(key)
            return levels
        apply_calm_rain(lambda key, value: writes.update({key: value}), read)
        self.assertEqual(reads, ["sim/weather/region/atmosphere_alt_levels_m"])
        profile = writes["sim/weather/region/temperatures_aloft_deg_c"]
        self.assertEqual(len(profile), len(levels))
        self.assertAlmostEqual(profile[1], 22.9495)
        self.assertGreater(profile[3], 5)
        self.assertEqual(profile[-1], -56.5)

    def test_warm_profile_rejects_invalid_levels(self):
        for levels in ([], [float("nan")], [float("inf")]):
            with self.assertRaises(ValueError):
                warm_rain_temperatures(levels)

    def test_servos_alone_do_not_mean_modes_are_engaged(self):
        calls = []
        controller = S76HoldController(lambda *x: calls.append(x), lambda x: calls.append(x), clock=lambda: 100.)
        snapshot = SimpleNamespace(raw={"sim/cockpit2/autopilot/servos_on": 1}, ownship=SimpleNamespace(
            indicated_airspeed_kt=100., altitude_m=2500., altitude_agl_m=2000., roll_deg=0., pitch_deg=2.))
        controller.step(snapshot)
        self.assertIn("sim/autopilot/altitude_hold", calls)
        self.assertIn("sim/autopilot/heading", calls)

    def test_crash_waits_for_recovery_instead_of_controlling_wreck(self):
        calls = []
        controller = S76HoldController(lambda *x: calls.append(x), lambda x: calls.append(x))
        snapshot = SimpleNamespace(raw={"sim/flightmodel2/misc/has_crashed": 1}, ownship=SimpleNamespace(
            indicated_airspeed_kt=0., altitude_m=300., altitude_agl_m=0., roll_deg=2., pitch_deg=0.))
        self.assertEqual(controller.step(snapshot), "recovery-required")
        self.assertEqual(calls, [])


if __name__ == "__main__":
    unittest.main()
