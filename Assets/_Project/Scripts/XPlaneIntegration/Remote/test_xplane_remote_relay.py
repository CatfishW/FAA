import json
import socket
import sys
import threading
import time
import unittest
from pathlib import Path
import importlib.util


MODULE_PATH = Path(__file__).resolve().parent / "xplane_remote_relay.py"
SPEC = importlib.util.spec_from_file_location("xplane_remote_relay", MODULE_PATH)
assert SPEC is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class XPlaneRemoteRelayTests(unittest.TestCase):
    def test_mock_snapshot_contains_required_sections(self):
        source = MODULE.MockFlightSource(8500.0, 160.0, 90.0, 4)
        snapshot = source.next_snapshot()

        self.assertEqual("mock", snapshot.source_mode)
        self.assertIsNotNone(snapshot.ownship)
        self.assertIsNotNone(snapshot.weather)
        self.assertEqual(4, len(snapshot.traffic))
        self.assertIn("sim/flightmodel/position/latitude", snapshot.raw)
        self.assertTrue(snapshot.ownship.autopilot_engaged)

    def test_broadcast_server_streams_ndjson(self):
        server = MODULE.BroadcastServer("127.0.0.1", 37219)
        client = socket.create_connection(("127.0.0.1", 37219), timeout=2)
        try:
            source = MODULE.MockFlightSource(8500.0, 160.0, 90.0, 2)
            time.sleep(0.1)
            server.broadcast(source.next_snapshot())
            data = client.recv(8192).decode("utf-8")
            payload = json.loads(data.strip())
            self.assertIn("ownship", payload)
            self.assertIn("weather", payload)
            self.assertIn("traffic", payload)
        finally:
            client.close()
            server.close()


if __name__ == "__main__":
    unittest.main()
