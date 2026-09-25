#!/usr/bin/env python3
"""Compare Unity's sampled heightfield with one unchanged simulator snapshot.

This reports the raster/final-mesh discrepancy; it is not a clearance test.
The read-only check does not alter aircraft, weather or simulator scenery.
"""
import argparse
import json
import math
from urllib.request import urlopen


def get(url):
    with urlopen(url, timeout=20) as response:
        return json.load(response)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--telemetry", default="http://127.0.0.1:12678")
    parser.add_argument("--terrain", default="http://127.0.0.1:12679")
    args = parser.parse_args()
    snapshot = get(args.telemetry.rstrip("/") + "/v1/snapshot")
    health = snapshot.get("health", {})
    age = health.get("last_packet_age_sec")
    if health.get("status") != "ok" or not isinstance(age, (int, float)) or not math.isfinite(age) or age > 5:
        print(json.dumps({"status": "unavailable", "reason": "A healthy, fresh simulator snapshot is required",
                          "health_status": health.get("status")}, indent=2))
        return 2
    ownship = snapshot["ownship"]
    lat, lon = ownship["latitude"], ownship["longitude"]
    ground = ownship["altitude_m"] - ownship["altitude_agl_m"]
    if not all(math.isfinite(v) for v in (lat, lon, ground)):
        raise ValueError("Snapshot has no finite ground-height reference")
    lat_index, lon_index = math.floor(lat * 10), math.floor(lon * 10)
    tile = get(args.terrain.rstrip("/") + f"/v1/terrain/tile?lat_index={lat_index}&lon_index={lon_index}&resolution=129")
    n = tile["resolution"]
    x, y = (lon - tile["west"]) * 10 * (n - 1), (lat - tile["south"]) * 10 * (n - 1)
    x0, y0 = min(n - 2, int(x)), min(n - 2, int(y))
    fx, fy = x - x0, y - y0
    values = tile["heights_m"]
    sw, se = values[y0 * n + x0:y0 * n + x0 + 2]
    nw, ne = values[(y0 + 1) * n + x0:(y0 + 1) * n + x0 + 2]
    # Same diagonal and planar triangle interpolation as the rendered Unity mesh.
    height = sw + fx * (se - sw) + fy * (nw - sw) if fx + fy <= 1 else ne + (1 - fx) * (nw - ne) + (1 - fy) * (se - ne)
    print(json.dumps({"snapshot_utc": snapshot.get("timestamp_utc"), "source_mode": snapshot.get("source_mode"),
                      "tile": [lat_index, lon_index], "sources": tile["sources"],
                      "unity_surface_msl_m": round(height, 3), "simulator_ground_msl_m": round(ground, 3),
                      "difference_m": round(height - ground, 3),
                      "meaning": "Spot comparison only; source raster is not final X-Plane collision mesh"}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
