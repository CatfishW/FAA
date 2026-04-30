#!/usr/bin/env python3
"""Remote X-Plane 11 telemetry relay for SSH-accessible GPU hosts.

This process runs beside X-Plane on the 4090 machine. In real mode it uses NASA
XPlaneConnect to keep the aircraft aloft and export continuous telemetry. In
mock mode it generates a synthetic endless-flight feed so Unity can be tested
without a live simulator.

The relay publishes newline-delimited JSON over plain TCP so the Windows Unity
machine can consume it through a simple SSH port forward.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import socket
import threading
import time
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from typing import Dict, List, Optional, Sequence


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def normalize_heading(heading: float) -> float:
    return heading % 360.0


def heading_delta(target: float, current: float) -> float:
    delta = (target - current + 180.0) % 360.0 - 180.0
    return delta


@dataclass
class OwnshipState:
    latitude: float
    longitude: float
    altitude_m: float
    altitude_agl_m: float
    pitch_deg: float
    roll_deg: float
    heading_deg: float
    track_deg: float
    flight_path_angle_deg: float
    slip_skid: float
    indicated_airspeed_kt: float
    true_airspeed_kt: float
    ground_speed_kt: float
    vertical_speed_fpm: float
    autopilot_engaged: bool
    autopilot_mode: int
    gear_down: bool
    on_ground: bool
    gps_valid: bool
    ils_valid: bool
    throttle_ratio: float
    elevator_input: float
    aileron_input: float
    rudder_input: float
    flaps_ratio: float
    speedbrake_ratio: float
    parking_brake_ratio: float


@dataclass
class WeatherState:
    wind_speed_kt: float
    wind_direction_deg: float
    barometer_inhg: float
    temperature_c: float
    visibility_m: float
    cloud_base_m: float


@dataclass
class TrafficTarget:
    icao24: str
    callsign: str
    latitude: float
    longitude: float
    altitude_m: float
    heading_deg: float
    velocity_mps: float
    vertical_rate_mps: float
    on_ground: bool


@dataclass
class AutomationState:
    controller: str
    mode: str
    recovery_active: bool
    target_altitude_m: float
    target_heading_deg: float
    target_speed_kt: float


@dataclass
class TelemetrySnapshot:
    timestamp_utc: str
    source_mode: str
    ownship: OwnshipState
    weather: WeatherState
    traffic: List[TrafficTarget] = field(default_factory=list)
    raw: Dict[str, float] = field(default_factory=dict)
    automation: Optional[AutomationState] = None


class BroadcastServer:
    def __init__(self, host: str, port: int) -> None:
        self._host = host
        self._port = port
        self._server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._server.bind((host, port))
        self._server.listen()
        self._clients: List[socket.socket] = []
        self._lock = threading.Lock()
        self._running = True
        self._accept_thread = threading.Thread(
            target=self._accept_loop, name="XPlaneRelayAccept", daemon=True
        )
        self._accept_thread.start()

    def _accept_loop(self) -> None:
        while self._running:
            try:
                client, _ = self._server.accept()
                client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                with self._lock:
                    self._clients.append(client)
            except OSError:
                return

    def broadcast(self, snapshot: TelemetrySnapshot) -> None:
        payload = json.dumps(asdict(snapshot), separators=(",", ":")) + "\n"
        encoded = payload.encode("utf-8")
        stale: List[socket.socket] = []
        with self._lock:
            for client in self._clients:
                try:
                    client.sendall(encoded)
                except OSError:
                    stale.append(client)
            for client in stale:
                self._clients.remove(client)
                try:
                    client.close()
                except OSError:
                    pass

    def close(self) -> None:
        self._running = False
        try:
            self._server.close()
        except OSError:
            pass
        with self._lock:
            for client in self._clients:
                try:
                    client.close()
                except OSError:
                    pass
            self._clients.clear()


class MockFlightSource:
    def __init__(
        self,
        target_altitude_ft: float,
        target_speed_kt: float,
        target_heading_deg: float,
        traffic_count: int,
    ) -> None:
        self._lat = 33.6407
        self._lon = -84.4277
        self._alt_m = target_altitude_ft * 0.3048
        self._heading = target_heading_deg
        self._speed = target_speed_kt
        self._vertical_speed_fpm = 0.0
        self._traffic_count = traffic_count
        self._start = time.time()

    def next_snapshot(self) -> TelemetrySnapshot:
        elapsed = time.time() - self._start
        heading_wobble = math.sin(elapsed / 20.0) * 8.0
        self._heading = normalize_heading(self._heading + 0.4)
        track = normalize_heading(self._heading + heading_wobble)
        self._vertical_speed_fpm = math.sin(elapsed / 12.0) * 120.0
        self._alt_m += (self._vertical_speed_fpm / 196.8504) * 0.2
        self._lat += math.cos(math.radians(track)) * 0.00012
        self._lon += math.sin(math.radians(track)) * 0.00012

        ownship = OwnshipState(
            latitude=self._lat,
            longitude=self._lon,
            altitude_m=self._alt_m,
            altitude_agl_m=max(250.0, self._alt_m - 120.0),
            pitch_deg=math.sin(elapsed / 8.0) * 2.0,
            roll_deg=math.sin(elapsed / 6.0) * 6.0,
            heading_deg=self._heading,
            track_deg=track,
            flight_path_angle_deg=self._vertical_speed_fpm / 600.0,
            slip_skid=math.sin(elapsed / 4.5) * 0.08,
            indicated_airspeed_kt=self._speed,
            true_airspeed_kt=self._speed + 6.0,
            ground_speed_kt=self._speed - 4.0,
            vertical_speed_fpm=self._vertical_speed_fpm,
            autopilot_engaged=True,
            autopilot_mode=2,
            gear_down=False,
            on_ground=False,
            gps_valid=True,
            ils_valid=False,
            throttle_ratio=0.62,
            elevator_input=clamp(self._vertical_speed_fpm / 700.0, -0.2, 0.2),
            aileron_input=clamp(heading_wobble / 20.0, -0.3, 0.3),
            rudder_input=0.0,
            flaps_ratio=0.0,
            speedbrake_ratio=0.0,
            parking_brake_ratio=0.0,
        )

        weather = WeatherState(
            wind_speed_kt=18.0,
            wind_direction_deg=235.0,
            barometer_inhg=29.92,
            temperature_c=11.0,
            visibility_m=12000.0,
            cloud_base_m=2200.0,
        )

        traffic: List[TrafficTarget] = []
        for idx in range(self._traffic_count):
            bearing = normalize_heading(
                self._heading + idx * (360.0 / max(1, self._traffic_count))
            )
            distance_nm = 4.0 + idx * 2.5
            offset_lat = math.cos(math.radians(bearing)) * distance_nm * 0.0166
            offset_lon = math.sin(math.radians(bearing)) * distance_nm * 0.0166
            traffic.append(
                TrafficTarget(
                    icao24=f"MOCK{idx:02d}",
                    callsign=f"M{idx:03d}",
                    latitude=self._lat + offset_lat,
                    longitude=self._lon + offset_lon,
                    altitude_m=self._alt_m + (idx - 1) * 180.0,
                    heading_deg=normalize_heading(bearing + 180.0),
                    velocity_mps=70.0 + idx * 8.0,
                    vertical_rate_mps=(-1.5 + idx) * 0.4,
                    on_ground=False,
                )
            )

        raw = {
            "sim/flightmodel/position/latitude": ownship.latitude,
            "sim/flightmodel/position/longitude": ownship.longitude,
            "sim/flightmodel/position/elevation": ownship.altitude_m,
            "sim/flightmodel/position/theta": ownship.pitch_deg,
            "sim/flightmodel/position/phi": ownship.roll_deg,
            "sim/flightmodel/position/psi": ownship.heading_deg,
            "sim/weather/aircraft/wind_speed_kt": weather.wind_speed_kt,
            "sim/weather/aircraft/wind_direction_deg": weather.wind_direction_deg,
        }

        automation = AutomationState(
            controller="mock-envelope-keeper",
            mode="hold",
            recovery_active=False,
            target_altitude_m=self._alt_m,
            target_heading_deg=self._heading,
            target_speed_kt=self._speed,
        )

        return TelemetrySnapshot(
            timestamp_utc=utc_now(),
            source_mode="mock",
            ownship=ownship,
            weather=weather,
            traffic=traffic,
            raw=raw,
            automation=automation,
        )


class XPlaneConnectSource:
    WEATHER_DREFS = [
        "sim/weather/wind_speed_kt[0]",
        "sim/weather/wind_direction_degt[0]",
        "sim/weather/barometer_sealevel_inhg",
        "sim/weather/temperature_ambient_c",
    ]

    XP11_WEATHER_DREFS = [
        "sim/weather/barometer_sealevel_inhg",
        "sim/weather/temperature_sealevel_c",
    ]

    OWN_DREFS = [
        "sim/flightmodel/position/indicated_airspeed",
        "sim/flightmodel/position/true_airspeed",
        "sim/flightmodel/position/groundspeed",
        "sim/flightmodel/position/vh_ind",
        "sim/cockpit/autopilot/autopilot_state",
        "sim/cockpit/switches/gear_handle_status",
    ]

    def __init__(
        self,
        xp_host: str,
        xp_port: int,
        target_altitude_ft: float,
        target_heading_deg: float,
        target_speed_kt: float,
        recovery_altitude_ft: float,
        traffic_slots: int,
    ) -> None:
        try:
            import xpc  # type: ignore
        except (
            ImportError
        ) as exc:  # pragma: no cover - runtime dependency on remote host
            raise RuntimeError(
                "Python xpc module not available. Install NASA XPlaneConnect client on the X-Plane host."
            ) from exc

        self._xpc = xpc.XPlaneConnect(xpHost=xp_host, xpPort=xp_port, timeout=1000)
        self._target_altitude_m = target_altitude_ft * 0.3048
        self._target_heading_deg = target_heading_deg
        self._target_speed_kt = target_speed_kt
        self._recovery_altitude_m = recovery_altitude_ft * 0.3048
        self._traffic_slots = traffic_slots

    def close(self) -> None:
        self._xpc.close()

    def next_snapshot(self) -> TelemetrySnapshot:
        posi = self._xpc.getPOSI()
        ctrl = self._xpc.getCTRL()
        own_values = self._xpc.getDREFs(self.OWN_DREFS)
        weather_values = self._safe_get_drefs(
            self.WEATHER_DREFS, self.XP11_WEATHER_DREFS
        )

        lat, lon, altitude_m, pitch_deg, roll_deg, heading_deg, gear = posi
        indicated_airspeed = own_values[0][0]
        true_airspeed = own_values[1][0]
        ground_speed_kt = own_values[2][0] * 1.94384
        vertical_speed_fpm = own_values[3][0] * 196.8504
        autopilot_mode = int(own_values[4][0])
        gear_down = bool(own_values[5][0] > 0.5)

        recovery_active = (
            altitude_m < self._recovery_altitude_m
            or abs(roll_deg) > 70.0
            or ground_speed_kt < 55.0
        )
        mode = "recover" if recovery_active else "hold"
        if recovery_active:
            self._xpc.pauseSim(True)
            self._xpc.sendPOSI(
                [
                    lat,
                    lon,
                    self._target_altitude_m,
                    0.0,
                    0.0,
                    self._target_heading_deg,
                    1.0,
                ]
            )
            self._xpc.pauseSim(False)
        else:
            pitch_cmd = clamp(
                ((self._target_altitude_m - altitude_m) * 3.28084) / 2500.0
                - vertical_speed_fpm / 3000.0,
                -0.35,
                0.35,
            )
            roll_cmd = clamp(
                heading_delta(self._target_heading_deg, heading_deg) / 45.0, -0.35, 0.35
            )
            throttle_cmd = clamp(
                0.55 + (self._target_speed_kt - ground_speed_kt) / 120.0, 0.15, 1.0
            )
            self._xpc.sendCTRL(
                [pitch_cmd, roll_cmd, 0.0, throttle_cmd, 1.0 if gear_down else 0.0, 0.0]
            )

        weather = WeatherState(
            wind_speed_kt=float(weather_values[0][0]) if weather_values else 0.0,
            wind_direction_deg=float(weather_values[1][0])
            if len(weather_values) > 1
            else 0.0,
            barometer_inhg=float(weather_values[2][0])
            if len(weather_values) > 2
            else 29.92,
            temperature_c=float(weather_values[3][0])
            if len(weather_values) > 3
            else 15.0,
            visibility_m=12000.0,
            cloud_base_m=max(altitude_m + 500.0, 1800.0),
        )

        ownship = OwnshipState(
            latitude=float(lat),
            longitude=float(lon),
            altitude_m=float(altitude_m),
            altitude_agl_m=max(altitude_m - 150.0, 100.0),
            pitch_deg=float(pitch_deg),
            roll_deg=float(roll_deg),
            heading_deg=normalize_heading(float(heading_deg)),
            track_deg=normalize_heading(float(heading_deg)),
            flight_path_angle_deg=clamp(vertical_speed_fpm / 600.0, -10.0, 10.0),
            slip_skid=0.0,
            indicated_airspeed_kt=float(indicated_airspeed),
            true_airspeed_kt=float(true_airspeed),
            ground_speed_kt=float(ground_speed_kt),
            vertical_speed_fpm=float(vertical_speed_fpm),
            autopilot_engaged=autopilot_mode >= 2,
            autopilot_mode=autopilot_mode,
            gear_down=gear_down,
            on_ground=altitude_m < 3.0,
            gps_valid=True,
            ils_valid=False,
            throttle_ratio=float(ctrl[3]),
            elevator_input=float(ctrl[0]),
            aileron_input=float(ctrl[1]),
            rudder_input=float(ctrl[2]),
            flaps_ratio=float(ctrl[5]),
            speedbrake_ratio=float(ctrl[6]) if len(ctrl) > 6 else 0.0,
            parking_brake_ratio=0.0,
        )

        traffic = self._read_traffic(float(lat), float(lon), float(altitude_m))
        raw = {
            "sim/flightmodel/position/latitude": float(lat),
            "sim/flightmodel/position/longitude": float(lon),
            "sim/flightmodel/position/elevation": float(altitude_m),
            "sim/flightmodel/position/theta": float(pitch_deg),
            "sim/flightmodel/position/phi": float(roll_deg),
            "sim/flightmodel/position/psi": float(heading_deg),
            "sim/cockpit/autopilot/autopilot_state": float(autopilot_mode),
            "sim/weather/barometer_sealevel_inhg": weather.barometer_inhg,
        }

        automation = AutomationState(
            controller="xpc-envelope-keeper",
            mode=mode,
            recovery_active=recovery_active,
            target_altitude_m=self._target_altitude_m,
            target_heading_deg=self._target_heading_deg,
            target_speed_kt=self._target_speed_kt,
        )

        return TelemetrySnapshot(
            timestamp_utc=utc_now(),
            source_mode="xpc",
            ownship=ownship,
            weather=weather,
            traffic=traffic,
            raw=raw,
            automation=automation,
        )

    def _safe_get_drefs(
        self, primary: Sequence[str], fallback: Sequence[str]
    ) -> List[Sequence[float]]:
        try:
            return self._xpc.getDREFs(list(primary))
        except Exception:
            return self._xpc.getDREFs(list(fallback))

    def _read_traffic(
        self, own_lat: float, own_lon: float, own_alt: float
    ) -> List[TrafficTarget]:
        targets: List[TrafficTarget] = []
        for slot in range(1, self._traffic_slots + 1):
            drefs = [
                f"sim/multiplayer/position/plane{slot}_lat",
                f"sim/multiplayer/position/plane{slot}_lon",
                f"sim/multiplayer/position/plane{slot}_el",
                f"sim/multiplayer/position/plane{slot}_psi",
                f"sim/multiplayer/position/plane{slot}_v_x",
                f"sim/multiplayer/position/plane{slot}_v_y",
                f"sim/multiplayer/position/plane{slot}_v_z",
                f"sim/multiplayer/position/plane{slot}_gear_deploy",
            ]
            try:
                values = self._xpc.getDREFs(drefs)
            except Exception:
                continue

            lat = float(values[0][0])
            lon = float(values[1][0])
            if abs(lat) < 0.001 and abs(lon) < 0.001:
                continue

            vx = float(values[4][0])
            vy = float(values[5][0])
            vz = float(values[6][0])
            velocity_mps = math.sqrt(vx * vx + vy * vy + vz * vz)
            targets.append(
                TrafficTarget(
                    icao24=f"XPL{slot:04d}",
                    callsign=f"XP{slot:02d}",
                    latitude=lat,
                    longitude=lon,
                    altitude_m=float(values[2][0]),
                    heading_deg=normalize_heading(float(values[3][0])),
                    velocity_mps=velocity_mps,
                    vertical_rate_mps=vy,
                    on_ground=bool(values[7][0] > 0.5),
                )
            )

        if not targets:
            for idx in range(3):
                targets.append(
                    TrafficTarget(
                        icao24=f"SYN{idx:04d}",
                        callsign=f"SYN{idx:02d}",
                        latitude=own_lat + 0.04 * (idx + 1),
                        longitude=own_lon - 0.03 * (idx + 1),
                        altitude_m=own_alt + idx * 250.0,
                        heading_deg=normalize_heading(
                            self._target_heading_deg + idx * 40.0
                        ),
                        velocity_mps=80.0 + idx * 15.0,
                        vertical_rate_mps=0.0,
                        on_ground=False,
                    )
                )

        return targets


def run(args: argparse.Namespace) -> None:
    server = BroadcastServer(args.listen_host, args.listen_port)
    if args.mode == "mock":
        source = MockFlightSource(
            args.target_altitude_ft,
            args.target_speed_kt,
            args.target_heading_deg,
            args.mock_traffic_count,
        )
        closer = None
    else:
        source = XPlaneConnectSource(
            args.xplane_host,
            args.xplane_port,
            args.target_altitude_ft,
            args.target_heading_deg,
            args.target_speed_kt,
            args.recovery_altitude_ft,
            args.traffic_slots,
        )
        closer = source.close

    period = 1.0 / args.broadcast_hz
    deadline = (
        time.time() + args.duration_seconds if args.duration_seconds > 0 else None
    )

    print(
        f"[xplane_remote_relay] mode={args.mode} listen={args.listen_host}:{args.listen_port} hz={args.broadcast_hz}"
    )
    try:
        while deadline is None or time.time() < deadline:
            started = time.time()
            snapshot = source.next_snapshot()
            server.broadcast(snapshot)
            time.sleep(max(0.0, period - (time.time() - started)))
    finally:
        server.close()
        if closer is not None:
            closer()


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Endless X-Plane 11 telemetry relay for remote Unity ingestion"
    )
    parser.add_argument("--mode", choices=("mock", "xpc"), default="mock")
    parser.add_argument("--listen-host", default="127.0.0.1")
    parser.add_argument("--listen-port", type=int, default=37211)
    parser.add_argument("--broadcast-hz", type=float, default=5.0)
    parser.add_argument("--duration-seconds", type=float, default=0.0)
    parser.add_argument("--target-altitude-ft", type=float, default=8500.0)
    parser.add_argument("--target-heading-deg", type=float, default=90.0)
    parser.add_argument("--target-speed-kt", type=float, default=160.0)
    parser.add_argument("--recovery-altitude-ft", type=float, default=4500.0)
    parser.add_argument("--mock-traffic-count", type=int, default=5)
    parser.add_argument("--xplane-host", default="127.0.0.1")
    parser.add_argument("--xplane-port", type=int, default=49009)
    parser.add_argument("--traffic-slots", type=int, default=5)
    return parser


if __name__ == "__main__":
    run(build_arg_parser().parse_args())
