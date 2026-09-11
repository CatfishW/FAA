"""Read-only bounded stability check; does not alter the simulator."""
import argparse
import json
import time
from urllib.request import urlopen


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://127.0.0.1:12678/v1/snapshot")
    parser.add_argument("--seconds", type=int, default=180)
    args = parser.parse_args()
    rows, failures = [], []
    until = time.monotonic() + args.seconds
    while time.monotonic() < until:
        try:
            with urlopen(args.url, timeout=3) as response:
                s = json.load(response)
            o = s["ownship"]
            row = dict(ias=o["indicated_airspeed_kt"], altitude_ft=o["altitude_m"] * 3.28084,
                       agl_ft=o["altitude_agl_m"] * 3.28084, roll=o["roll_deg"], pitch=o["pitch_deg"],
                       vs=o["vertical_speed_fpm"], age=s["health"]["last_packet_age_sec"],
                       crashed=s["raw"].get("sim/flightmodel2/misc/has_crashed", 1),
                       precip=s["weather"]["precipitation_on_aircraft_ratio"])
            rows.append(row)
            if not (60 <= row["ias"] <= 120 and row["agl_ft"] > 1000 and abs(row["roll"]) < 20
                    and abs(row["pitch"]) < 15 and abs(row["vs"]) < 500 and row["age"] < 3 and row["crashed"] == 0):
                failures.append(row)
        except Exception as error:
            failures.append({"error": str(error)})
        time.sleep(1)
    result = {"duration_seconds": args.seconds, "samples": len(rows), "violations": len(failures),
              "ranges": {key: [min(r[key] for r in rows), max(r[key] for r in rows)] for key in rows[0]} if rows else {},
              "first_failures": failures[:3]}
    print(json.dumps(result, indent=2))
    raise SystemExit(1 if failures or not rows else 0)


if __name__ == "__main__":
    main()
