"""Run on 4090 with PYTHONPATH set to the installed xplane12api repository."""
import base64
import unittest

try:
    from xplane12.bridge.webapi_client import XPlaneState, XPlaneWebApiClient
except ImportError:
    XPlaneWebApiClient = None

REF = "sim/cockpit2/tcas/targets/icao_type"


@unittest.skipIf(XPlaneWebApiClient is None, "Requires the host's xplane12api package")
class TrafficMetadataTests(unittest.TestCase):
    def setUp(self):
        self.client = XPlaneWebApiClient(XPlaneState(), [], websocket_enabled=False)

    def tearDown(self):
        self.client.executor.shutdown(wait=True)

    def payload(self):
        data = bytearray(512)
        for slot, code in enumerate(("S76", "C172", "R22", "F4", "B738")):
            data[slot * 8:slot * 8 + len(code)] = code.encode("ascii")
        return base64.b64encode(data).decode("ascii")

    def test_native_slots_preserve_ownship_and_ai_indices(self):
        result = self.client._coerce_subscription_value(REF, self.payload())
        self.assertEqual(len(result), 160)
        for slot, expected in enumerate(("S76", "C172", "R22", "F4", "B738")):
            value = bytes(int(result[f"{REF}[{slot * 8 + i}]"]) for i in range(8))
            self.assertEqual(value.split(b"\0")[0].decode(), expected)

    def test_websocket_and_rest_use_identical_numeric_metadata(self):
        expected = self.client._coerce_subscription_value(REF, self.payload())
        actual = self.client._updates_from_websocket_payload(
            {"type": "dataref_update_values", "data": {"42": self.payload()}}, {42: [REF]})
        self.assertEqual(actual, expected)

    def test_invalid_or_short_metadata_clears_old_types(self):
        for invalid in (None, "!!!", base64.b64encode(b"C172").decode(), []):
            result = self.client._coerce_subscription_value(REF, invalid)
            self.assertEqual(len(result), 160)
            self.assertTrue(all(value == 0 for value in result.values()))

    def test_ordinary_numeric_arrays_are_unchanged(self):
        self.assertEqual(self.client._coerce_subscription_value("normal[1]", [2, 3]), 3)
        self.assertIsNone(self.client._coerce_subscription_value("normal", "not numeric"))

    def test_all_region_and_local_turbulence_layers_are_subscribed(self):
        from xplane12.compat.legacy_categories import build_subscriptions
        weather = [s.dataref for s in build_subscriptions() if s.category == "weather"]
        for scope in ("region", "aircraft"):
            for layer in range(13):
                self.assertEqual(weather.count(f"sim/weather/{scope}/turbulence[{layer}]"), 1)

    def test_turbulence_factor_is_not_clamped_to_a_ratio(self):
        for scope in ("region", "aircraft"):
            for value in (0., .2, 5., 10.):
                self.assertEqual(self.client._coerce_subscription_value(
                    f"sim/weather/{scope}/turbulence[12]", [value] * 13), value)


if __name__ == "__main__":
    unittest.main()
