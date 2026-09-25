import hashlib
import json
from pathlib import Path
import struct
import tempfile
import threading
import unittest
from urllib.error import HTTPError
from urllib.request import urlopen

from terrain_server import HTTPServer, Raster, Scenery, TerrainError, atoms, handler_for, read_dsf


def atom(name, data):
    return name[::-1].encode() + struct.pack("<I", 8 + len(data)) + data


def dsf(south=33, west=-83, base=100, overlay=False, dem=True):
    props = {"sim/south": str(south), "sim/north": str(south + 1),
             "sim/west": str(west), "sim/east": str(west + 1), "sim/overlay": "1" if overlay else "0"}
    head = atom("HEAD", atom("PROP", "\0".join(v for pair in props.items() for v in pair).encode() + b"\0"))
    defs = atom("DEFN", atom("DEMN", b"elevation\0" if dem else b""))
    raster = b""
    if dem:
        info = struct.pack("<BBHIIff", 1, 2, 5, 3, 3, 1, 0)
        values = [base + y * 100 + x * 10 for y in range(3) for x in range(3)]
        raster = atom("DEMS", atom("DEMI", info) + atom("DEMD", struct.pack("<9h", *values)))
    payload = b"XPLNEDSF\x01\0\0\0" + head + defs + raster
    return payload + hashlib.md5(payload).digest()


class TerrainTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "Custom Scenery").mkdir()
        self.put("Global Scenery/X-Plane 12 Global Scenery", dsf())

    def put(self, pack, contents, south=33, west=-83):
        target = self.root / pack / Scenery.dsf_relative(south, west)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(contents)
        return target

    def packs(self, text):
        (self.root / "Custom Scenery/scenery_packs.ini").write_text(text)

    def test_raster_southwest_and_northeast_order(self):
        scenery = Scenery(self.root)
        self.assertEqual(scenery.sample(33, -83)[0], 100)
        self.assertAlmostEqual(scenery.sample(33.5, -82.5)[0], 210)
        raster, _ = scenery.get(33, -83)
        self.assertEqual(raster.sample(1, 1), 320)

    def test_signed_unsigned_float_scale_offset(self):
        for flag, fmt, bpp in [(0, "f", 4), (1, "h", 2), (2, "B", 1)]:
            raster = Raster.decode(struct.pack("<BBHIIff", 1, bpp, flag | 4, 2, 2, 2, -30), struct.pack("<4" + fmt, 10, 20, 30, 40))
            self.assertEqual(raster.sample(.5, .5), 20)

    def test_area_centric_has_half_pixel_offset(self):
        raster = Raster.decode(struct.pack("<BBHIIff", 1, 2, 1, 2, 2, 1, 0), struct.pack("<4h", 0, 10, 100, 110))
        self.assertEqual(raster.sample(.25, .25), 0)
        self.assertEqual(raster.sample(.75, .75), 110)
        self.assertEqual(raster.sample(0, 0), 0)

    def test_no_data_is_not_a_sea_level_fallback(self):
        raster = Raster.decode(struct.pack("<BBHIIff", 1, 2, 5, 2, 2, 1, 0), struct.pack("<4h", 0, 0, 0, -32768))
        with self.assertRaises(TerrainError):
            raster.sample(.5, .5)

    def test_shared_tile_edges_are_identical(self):
        scenery = Scenery(self.root)
        left, right = scenery.tile(332, -828, 33), scenery.tile(332, -827, 33)
        self.assertEqual(left["heights_m"][32::33], right["heights_m"][::33])
        north = scenery.tile(333, -828, 33)
        self.assertEqual(left["heights_m"][-33:], north["heights_m"][:33])

    def test_scenery_priority_and_disabled_pack(self):
        self.put("Custom Scenery/high", dsf(base=500))
        self.put("Custom Scenery/disabled", dsf(base=900))
        self.packs("SCENERY_PACK_DISABLED Custom Scenery/disabled/\nSCENERY_PACK Custom Scenery/high/\n")
        self.assertEqual(Scenery(self.root).sample(33, -83)[0], 500)

    def test_distant_span_and_shared_lod_sample_positions(self):
        scenery = Scenery(self.root)
        coarse = scenery.tile(330, -830, 33, span=4)
        fine = scenery.tile(334, -830, 129)
        self.assertEqual(coarse["schema_version"], 2)
        self.assertEqual(coarse["tile_degrees"], .4)
        self.assertEqual(coarse["heights_m"][-33:-24], fine["heights_m"][:129:16])
        wide = scenery.tile(330, -830, 33, span=8)
        self.assertEqual(wide["span"], 8)
        self.assertEqual(len(wide["heights_m"]), 1089)

    def test_span_limits_and_polar_overrun(self):
        for span in (0, -1, 3, 16):
            with self.assertRaises(TerrainError):
                Scenery(self.root).tile(330, -830, 33, span)
        with self.assertRaises(TerrainError):
            Scenery(self.root).tile(848, 0, 33, 4)

    def test_overlay_without_raster_is_skipped(self):
        self.put("Custom Scenery/airport", dsf(overlay=True, dem=False))
        self.packs("SCENERY_PACK Custom Scenery/airport/\n")
        self.assertEqual(Scenery(self.root).sample(33, -83)[0], 100)

    def test_unsupported_custom_mesh_does_not_use_wrong_global_dem(self):
        self.put("Custom Scenery/mesh", dsf(dem=False))
        self.packs("SCENERY_PACK Custom Scenery/mesh/\n")
        with self.assertRaisesRegex(TerrainError, "no supported elevation"):
            Scenery(self.root).sample(33, -83)

    def test_pack_order_change_invalidates_cached_source(self):
        scenery = Scenery(self.root)
        scenery.sample(33, -83)
        self.put("Custom Scenery/high", dsf(base=500))
        self.packs("SCENERY_PACK Custom Scenery/high/\n")
        self.assertEqual(scenery.tile(330, -830, 33)["heights_m"][0], 500)

    def test_checksum_corruption_fails(self):
        path = self.put("Custom Scenery/bad", dsf()[:-1] + b"x")
        with self.assertRaisesRegex(TerrainError, "checksum"):
            read_dsf(path)

    def test_malformed_atom_and_raster_dimensions(self):
        with self.assertRaises(TerrainError):
            list(atoms(b"DAEH\x01\0\0\0"))
        with self.assertRaises(TerrainError):
            Raster.decode(struct.pack("<BBHIIff", 1, 2, 5, 9000, 9000, 1, 0), b"\0\0")

    def test_negative_coordinate_tile_folder(self):
        self.assertEqual(str(Scenery.dsf_relative(-1, -1)), "Earth nav data/-10-010/-01-001.dsf")

    def test_request_limits_and_missing_coverage(self):
        scenery = Scenery(self.root)
        for args in [(900, 0, 33), (337, -829, 2049), (0, 1900, 33)]:
            with self.assertRaises(TerrainError):
                scenery.tile(*args)
        with self.assertRaisesRegex(TerrainError, "No installed"):
            scenery.tile(400, 100, 33)

    def test_http_contract_and_read_only(self):
        server = HTTPServer(("127.0.0.1", 0), handler_for(Scenery(self.root)))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f"http://127.0.0.1:{server.server_port}"
        try:
            with urlopen(base + "/health") as response:
                self.assertTrue(json.load(response)["read_only"])
            with urlopen(base + "/v1/terrain/tile?lat_index=332&lon_index=-828&resolution=33") as response:
                self.assertEqual(len(json.load(response)["heights_m"]), 1089)
            with urlopen(base + "/v1/terrain/tile?lat_index=330&lon_index=-830&resolution=33&span=8") as response:
                self.assertEqual(json.load(response)["tile_degrees"], .8)
            with self.assertRaises(HTTPError) as failure:
                urlopen(base + "/v1/terrain/tile?path=/etc/passwd")
            self.assertEqual(failure.exception.code, 400)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()


if __name__ == "__main__":
    unittest.main()
