"""Apply only AI traffic to the running simulator on 4090; leave ownship/weather alone."""
import argparse
import base64
import json
from pathlib import Path
import time
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", type=Path, default=Path(__file__).with_name("mixed_traffic.json"))
    parser.add_argument("--xplane-root", type=Path, required=True)
    parser.add_argument("--apply", action="store_true", help="Without this flag, validate only")
    args = parser.parse_args()
    base = "http://127.0.0.1:8086/api/v3"

    def request(path, payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        req = Request(base + path, data=data, method="GET" if data is None else "PATCH",
                      headers={"Content-Type": "application/json"})
        with urlopen(req, timeout=30) as response:
            body = response.read()
            return json.loads(body) if body else {"http_status": response.status}

    profile = json.loads(args.profile.read_text())
    if set(profile) != {"ai_aircraft"} or not 1 <= len(profile["ai_aircraft"]) <= 19:
        raise ValueError("Profile must contain only 1..19 AI aircraft")
    expected = []
    for plane in profile["ai_aircraft"]:
        if set(plane) != {"aircraft", "mission"} or plane["mission"] != "atc":
            raise ValueError("Only normal ATC traffic is allowed")
        path = (args.xplane_root / plane["aircraft"]["path"]).resolve()
        if not path.is_relative_to(args.xplane_root.resolve()) or path.suffix != ".acf" or not path.is_file():
            raise ValueError(f"Unavailable aircraft: {path}")
        code = next((line.split()[-1] for line in path.read_text(errors="replace").splitlines()
                     if line.startswith("P acf/_ICAO ")), "")
        expected.append(code)
    print(json.dumps({"validated_ai_count": len(expected), "expected_icao": expected, "apply": args.apply}))
    if not args.apply:
        return
    query = urlencode({"filter[name]": "sim/cockpit2/tcas/targets/icao_type"})
    identifier = request("/datarefs?" + query)["data"][0]["id"]

    def read_types():
        raw = base64.b64decode(request(f"/datarefs/{identifier}/value")["data"], validate=True)
        return [raw[i:i+8].split(b"\0")[0].decode("ascii") for i in range(0, 160, 8)]

    before = read_types()
    print(json.dumps({"before_icao": before[:len(expected)+1]}))
    print(json.dumps({"update_result": request("/flight", profile)}))
    for _ in range(15):
        after = read_types()
        if after[1:len(expected)+1] == expected:
            print(json.dumps({"verified": True, "ownship_unchanged": before[0] == after[0], "loaded_icao": after[:len(expected)+1]}))
            if before[0] != after[0]:
                raise RuntimeError("Ownship type unexpectedly changed")
            return
        time.sleep(1)
    print(json.dumps({"verified": False, "loaded_icao": after[:len(expected)+1]}))
    raise SystemExit("AI metadata did not match the requested loaded aircraft")


if __name__ == "__main__":
    main()
