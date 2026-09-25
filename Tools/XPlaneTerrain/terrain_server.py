#!/usr/bin/env python3
"""Read-only, loopback DSF elevation service. No simulator controls or asset export.

Spec: https://developer.x-plane.com/article/dsf-file-format-specification/
Only the elevation raster is reconstructed, not X-Plane's final physical mesh,
airport flattening, water polygons, art assets, or objects. Unsupported base
meshes fail explicitly rather than silently substituting different scenery.
"""
import argparse
from collections import OrderedDict
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import math
import os
from pathlib import Path
import selectors
import struct
import subprocess
import time
from urllib.parse import parse_qs, urlparse

MAX_DSF_BYTES = 256 * 1024 * 1024
TILE_DEGREES = 0.1


class TerrainError(ValueError):
    pass


def atoms(blob, start=0, end=None):
    end = len(blob) if end is None else end
    while start < end:
        if start + 8 > end:
            raise TerrainError("Truncated DSF atom")
        atom_id, size = struct.unpack_from("<4sI", blob, start)
        if size < 8 or start + size > end:
            raise TerrainError("Invalid DSF atom length")
        yield atom_id[::-1].decode("ascii"), memoryview(blob)[start + 8:start + size]
        start += size


def strings(blob):
    raw = bytes(blob)
    if not raw:
        return []
    if raw[-1] != 0:
        raise TerrainError("Invalid DSF string table")
    return raw[:-1].decode("utf-8").split("\0")


@dataclass
class Raster:
    width: int
    height: int
    flags: int
    scale: float
    offset: float
    format: str
    pixel_bytes: int
    data: bytes

    @classmethod
    def decode(cls, info, data):
        if len(info) != 20:
            raise TerrainError("Unsupported DEM metadata length")
        version, bpp, flags, width, height, scale, offset = struct.unpack("<BBHIIff", info)
        formats = {(0, 4): "f", (1, 1): "b", (1, 2): "h", (1, 4): "i",
                   (2, 1): "B", (2, 2): "H", (2, 4): "I"}
        fmt = formats.get((flags & 3, bpp))
        if version != 1 or fmt is None or flags & ~7 or not 2 <= width <= 16384 or not 2 <= height <= 16384:
            raise TerrainError("Unsupported DEM encoding")
        if len(data) != width * height * bpp or not math.isfinite(scale + offset):
            raise TerrainError("Invalid DEM payload")
        return cls(width, height, flags, scale, offset, "<" + fmt, bpp, bytes(data))

    def value(self, x, y):
        # DSF rows run west to east, starting at the south edge.
        raw = struct.unpack_from(self.format, self.data, (y * self.width + x) * self.pixel_bytes)[0]
        if raw == -32768 or not math.isfinite(raw):
            raise TerrainError("Elevation contains no-data samples")
        value = raw * self.scale + self.offset
        if not -12000 <= value <= 12000:
            raise TerrainError("Elevation out of range")
        return value

    def sample(self, u, v):
        post = bool(self.flags & 4)
        x = max(0, min(self.width - 1, u * (self.width - 1 if post else self.width) - (0 if post else .5)))
        y = max(0, min(self.height - 1, v * (self.height - 1 if post else self.height) - (0 if post else .5)))
        x0, y0 = int(x), int(y)
        x1, y1 = min(x0 + 1, self.width - 1), min(y0 + 1, self.height - 1)
        fx, fy = x - x0, y - y0
        return ((self.value(x0, y0) * (1 - fx) + self.value(x1, y0) * fx) * (1 - fy)
                + (self.value(x0, y1) * (1 - fx) + self.value(x1, y1) * fx) * fy)


def read_dsf(path):
    with path.open("rb") as source:
        magic = source.read(8)
    if magic.startswith(b"7z\xbc\xaf\x27\x1c"):
        # No shell and no archive extraction to disk. Limit decoded size/time.
        with subprocess.Popen(["7z", "x", "-so", str(path)], stdout=subprocess.PIPE,
                              stderr=subprocess.DEVNULL) as process:
            try:
                chunks, byte_count = [], 0
                deadline = time.monotonic() + 30
                with selectors.DefaultSelector() as selector:
                    selector.register(process.stdout, selectors.EVENT_READ)
                    while True:
                        remaining = deadline - time.monotonic()
                        if remaining <= 0 or not selector.select(remaining):
                            raise TerrainError("DSF decompression timed out")
                        chunk = os.read(process.stdout.fileno(), 65536)
                        if not chunk:
                            break
                        byte_count += len(chunk)
                        if byte_count > MAX_DSF_BYTES:
                            raise TerrainError("DSF exceeds size limit")
                        chunks.append(chunk)
                if process.wait(timeout=max(.1, deadline - time.monotonic())) != 0:
                    raise TerrainError("DSF decompression failed")
                blob = b"".join(chunks)
            finally:
                if process.poll() is None:
                    process.kill()
    else:
        with path.open("rb") as source:
            blob = source.read(MAX_DSF_BYTES + 1)
    if len(blob) > MAX_DSF_BYTES or len(blob) < 28 or blob[:12] != b"XPLNEDSF\x01\0\0\0":
        raise TerrainError("Unsupported DSF file")
    if hashlib.md5(blob[:-16]).digest() != blob[-16:]:
        raise TerrainError("DSF checksum mismatch")
    props, names, infos, layers = {}, [], [], []
    for kind, payload in atoms(blob, 12, len(blob) - 16):
        if kind == "HEAD":
            for child, value in atoms(payload):
                if child == "PROP":
                    table = strings(value)
                    if len(table) % 2:
                        raise TerrainError("Invalid DSF properties")
                    props.update(zip(table[::2], table[1::2]))
        elif kind == "DEFN":
            for child, value in atoms(payload):
                if child == "DEMN":
                    names = strings(value)
        elif kind == "DEMS":
            for child, value in atoms(payload):
                if child == "DEMI":
                    infos.append(bytes(value))
                elif child == "DEMD":
                    layers.append(bytes(value))
    if props.get("sim/overlay") == "1":
        return props, None, blob[-16:].hex()
    if len(names) != len(infos) or len(names) != len(layers) or "elevation" not in names:
        raise TerrainError("Selected base mesh has no supported elevation raster")
    index = names.index("elevation")
    return props, Raster.decode(infos[index], layers[index]), blob[-16:].hex()


class Scenery:
    def __init__(self, root):
        self.root = Path(root).resolve(strict=True)
        self.cache = OrderedDict()
        self.pack_signature = None
        self.packs = []
        self.refresh_packs()

    def refresh_packs(self):
        ini = self.root / "Custom Scenery/scenery_packs.ini"
        signature = ini.read_bytes() if ini.exists() else b""
        if self.pack_signature == signature:
            return False
        self.pack_signature = signature
        self.cache.clear()
        packs = []
        for line in signature.decode("utf-8-sig").splitlines():
            if not line.startswith("SCENERY_PACK "):
                continue  # Disabled packs are never selected.
            name = line[len("SCENERY_PACK "):].strip()
            if name == "*GLOBAL_AIRPORTS*":
                name = "Global Scenery/Global Airports"
            packs.append(self.root / name)
        packs.extend(self.root / name for name in (
            "Global Scenery/X-Plane 12 Demo Areas", "Global Scenery/X-Plane 12 Global Scenery"))
        self.packs = list(dict.fromkeys(packs))
        return True

    @staticmethod
    def dsf_relative(lat, lon):
        return Path("Earth nav data") / f"{lat // 10 * 10:+03d}{lon // 10 * 10:+04d}" / f"{lat:+03d}{lon:+04d}.dsf"

    def get(self, lat, lon):
        key = (lat, lon)
        if key in self.cache:
            record = self.cache.pop(key)
            if record[0].stat().st_mtime_ns == record[1]:
                self.cache[key] = record
                return record[2:]
        for pack in self.packs:
            path = pack / self.dsf_relative(lat, lon)
            if not path.is_file():
                continue
            props, raster, revision = read_dsf(path)
            if raster is None:
                continue
            bounds = [int(props.get(k, "999")) for k in ("sim/south", "sim/west", "sim/north", "sim/east")]
            if bounds != [lat, lon, lat + 1, lon + 1]:
                raise TerrainError("DSF geographic bounds mismatch")
            source = {"name": pack.name + "/" + path.name, "revision": revision,
                      "width": raster.width, "height": raster.height,
                      "post_centric": bool(raster.flags & 4)}
            self.cache[key] = (path, path.stat().st_mtime_ns, raster, source)
            while len(self.cache) > 4:
                self.cache.popitem(last=False)
            return raster, source
        raise TerrainError(f"No installed base scenery at {lat:+03d}{lon:+04d}")

    def sample(self, latitude, longitude):
        longitude = (longitude + 180) % 360 - 180
        south, west = math.floor(latitude), math.floor(longitude)
        raster, source = self.get(south, west)
        return raster.sample(longitude - west, latitude - south), source

    def tile(self, lat_index, lon_index, resolution, span=1):
        if span not in (1, 2, 4, 8):
            raise TerrainError("Span must be 1, 2, 4, or 8 tenths of a degree")
        if not -850 <= lat_index < 850 or lat_index + span > 850 or not -1800 <= lon_index < 1800:
            raise TerrainError("Tile index outside supported coverage (85S to 85N)")
        if resolution not in (33, 65, 129):
            raise TerrainError("Resolution must be 33, 65, or 129")
        self.refresh_packs()
        heights, sources = [], {}
        for y in range(resolution):
            # Integer global grid indices yield identical shared-edge samples.
            latitude = (lat_index * (resolution - 1) + y * span) / (10 * (resolution - 1))
            for x in range(resolution):
                longitude = (lon_index * (resolution - 1) + x * span) / (10 * (resolution - 1))
                value, source = self.sample(latitude, longitude)
                heights.append(round(value, 3))
                sources[source["name"]] = source
        return {"schema_version": 1 if span == 1 else 2, "source_kind": "xplane_dsf_elevation", "status": "ready",
                "altitude_datum": "MSL_m", "row_order": "south_to_north", "column_order": "west_to_east",
                "lat_index": lat_index, "lon_index": lon_index, "span": span, "tile_degrees": span / 10,
                "south": lat_index / 10, "west": lon_index / 10, "resolution": resolution,
                "generated_utc": datetime.now(timezone.utc).isoformat(), "sources": list(sources.values()),
                "minimum_m": min(heights), "maximum_m": max(heights), "heights_m": heights,
                "limitations": "DSF elevation only; not final mesh, airport flattening, objects, imagery, or water mask"}


def handler_for(scenery):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            parsed = urlparse(self.path)
            try:
                if parsed.path == "/health":
                    self.reply(200, {"status": "ready", "source_kind": "xplane_dsf_elevation",
                                     "schema_version": 2, "supported_spans": [1, 2, 4, 8],
                                     "read_only": True, "cached_dsf_count": len(scenery.cache)})
                elif parsed.path == "/v1/terrain/tile":
                    params = parse_qs(parsed.query, strict_parsing=True)
                    if set(params) not in ({"lat_index", "lon_index", "resolution"},
                                           {"lat_index", "lon_index", "resolution", "span"}) or any(len(v) != 1 for v in params.values()):
                        raise ValueError("Expected lat_index, lon_index, resolution and optional span")
                    result = scenery.tile(*(int(params[k][0]) for k in ("lat_index", "lon_index", "resolution")),
                                          span=int(params.get("span", ["1"])[0]))
                    self.reply(200, result)
                else:
                    self.reply(404, {"status": "unavailable", "error": "Unknown endpoint"})
            except TerrainError as error:
                self.reply(422, {"status": "unavailable", "error": str(error)})
            except (ValueError, OverflowError) as error:
                self.reply(400, {"status": "unavailable", "error": str(error)})
            except (OSError, subprocess.SubprocessError):
                self.reply(503, {"status": "unavailable", "error": "Scenery read failed; check server installation"})

        def reply(self, status, payload):
            body = json.dumps(payload, separators=(",", ":"), allow_nan=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            try:
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError):
                pass

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--xplane-root", required=True, type=Path)
    parser.add_argument("--port", type=int, default=8767)
    args = parser.parse_args()
    scenery = Scenery(args.xplane_root)
    # Deliberately loopback-only. Use SSH forwarding, never expose scenery publicly.
    server = HTTPServer(("127.0.0.1", args.port), handler_for(scenery))
    server.timeout = 10
    print(f"X-Plane DSF elevation service on 127.0.0.1:{args.port} (read-only)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
