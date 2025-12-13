# FAA Symbology + Real‑Time Traffic + FAA Charts

- Presenter: <Your Name>
- Date: <Date>
- Version: <Commit / Build>

---

## 1) Title
- FAA Symbology + Real‑Time Traffic + FAA Charts
- Team, date, version

Speaker notes:
- Briefly introduce the three pillars: symbology, data, charts.

---

## 2) Goals and context
- Improve pilot situational awareness
- Add reliable traffic data and FAA chart context
- Provide operations controls + observability

Speaker notes:
- Focus on clarity and trust: icons, trails, and charts must be accurate and readable.

---

## 3) What’s new (at a glance)
- Distance‑progressive symbology (diamond → type → 360° directional)
- Trails on Online Maps (zoom-scaled, de‑noised)
- FAA VFR Sectional overlays (ArcGIS tiles)
- On‑demand live traffic; debug UI; teleport
- Logging + Python video renderer
- Stability + correctness fixes (provider API, lon/lat, XYZ tiles)

Speaker notes:
- Give a 30‑second overview, then deep‑dive.

---

## 4) Aircraft icon progression (ACN)
- Far: diamond; Medium: aircraft type; Close: 360° directional cue
- Direction from heading vs pilot vector + vertical rate
- Real‑time switching with robust sprite loading
- Configurable thresholds and stage scales

Speaker notes:
- Show a quick animation/gif of far→medium→close.

---

## 5) Directional icon details
- Cues mapped to provided directional sprites (UpwardLeft, DownwardTowards, etc.)
- Heuristics for climbing/descending and lateral bias
- Drone heuristic (low altitude + low speed)

Speaker notes:
- Explain the decision tree and graceful fallbacks.

---

## 6) Icon readability and scaling
- Dynamic scale curve: near ~1.2× → far ~0.8×
- Stage base scales × distance multiplier
- Prevents clutter while staying legible

Speaker notes:
- Mention tunables surfaced in the debug UI.

---

## 7) Moving‑map trails
- Online Maps `Line` elements
- Fixes: lon/lat order, seed first point
- Min spacing (e.g., 100 m) to remove jitter
- Width scales by zoom; limited history (e.g., 64 points)

Speaker notes:
- Show a sequence of screenshots with trail growth.

---

## 8) FAA VFR Sectionals overlay (ArcGIS tiles)
- Switch to FAA VFR Sectional layer via the helper/UI
- XYZ tile template (Web Mercator 102100)
- Recommended zoom LOD: 8–12 for crisp tiles

Links:
- VFR Sectional service (info): https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer?f=html&cacheKey=9617a9dc8b350b3f
- Explore (FAA portal): https://adds-faa.opendata.arcgis.com/maps/6ab79dc5de5743adb3e3b6e3c803aa59/explore?location=36.203116%2C-82.092818%2C10

Speaker notes:
- Show both OSM and FAA views; call out terrain/airspace context.

---

## 9) Data ingestion and freshness
- Python bridge: on‑demand publishing by default
- Unity can request updates periodically or manually
- Request payload includes center/radius to localize fetch
- Airplanes.live and OpenSky fallbacks; normalized to OpenSky‑like schema

Speaker notes:
- Emphasize reduced bandwidth & more relevant local traffic.

---

## 10) Debug/ops UI and teleport
- UI: icon thresholds/scales, MQTT interval, chart switching
- Aircraft search; teleport near selected target; face target
- One‑click demo controls

Speaker notes:
- Short live demo sequence.

---

## 11) Observability: logging + Python video
- `TrafficDataLogger` writes JSONL snapshots
- `visualize_traffic_log.py` renders MP4 with markers/labels
- Supports demo playback, regression checks, and A/B of parameters

Speaker notes:
- Show a still frame of the video output.

---

## 12) Fixes and quality improvements
- Online Maps provider API: use `map.mapType` & `customProviderURL`
- Removed invalid provider calls; fixed compile errors
- GeoPoint order corrected (lon,lat)
- Forced sprite updates every frame to avoid stale images
- Diamond threshold for far‑away aircraft on map

Speaker notes:
- Highlight reliability and correctness.

---

## 13) Risks, constraints, compliance
- External service availability / rate limits; caching/backoff
- Chart licensing/attribution (FAA AIS on chart slides/HUD)
- XYZ vs TMS tile orientation; standardize on XYZ
- Performance: pooling, caching, GPU text; test under load
- Testing: deterministic playback from logs; regional regressions

Speaker notes:
- Show mitigation plan.

---

## 14) Roadmap (next)
- 3D model silhouettes; altitude/lighting cues; night mode
- Vector overlays (airspace, obstacles); TAC support
- Offline tile cache; stronger retry/backoff & telemetry
- UX presets; alerting & audio cues

Speaker notes:
- Close with 2–3 initiative priorities.

---

## Appendix A — Implementation quick refs
- FAA VFR Sectional XYZ tiles:
  - `https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer/tile/{z}/{y}/{x}`
- Overlay helper (C#):
  - `ConfigureFaaCustomTiles(string urlTemplate)`
  - `ConfigureFaaArcGisSectional()` / `ConfigureFaaArcGisTac()`
- Debug UI: chart buttons (VFR/TAC/OSM), icon/timing toggles, teleport
- Logs: `persistentDataPath/TrafficLogs/traffic_*.jsonl`; renderer: `Scripts/Traffic Data/visualize_traffic_log.py`

---

## Appendix B — Live demo script (2–3 min)
1) Start on OSM; show far→medium→close icon progression.
2) Teleport near target; demonstrate directional icons (climb/descend).
3) Switch to FAA Sectionals; zoom to LOD 8–12; observe trails.
4) Trigger manual update; adjust interval; highlight on‑demand flow.
5) Show a 10–15s MP4 generated from logs.

---

## Appendix C — Image placeholders
- HUD close‑range directional
- Map trails sequence (3 frames)
- FAA vs OSM side‑by‑side

---

## Attribution
- FAA AIS — VFR Sectionals (via ArcGIS tiles) — see links above.

---

## Deep‑dive: Icon progression parameters
| Parameter | Default | Purpose |
|---|---:|---|
| mediumRangeNm | 10 | Diamond → aircraft type switch |
| closeRangeNm | 3 | Aircraft type → directional switch |
| farIconScale | 0.9 | Base scale for far (diamond) stage |
| mediumIconScale | 1.0 | Base scale for medium (type) stage |
| closeIconScale | 1.15 | Base scale for close (directional) stage |
| distance curve | 0.1–10 nm | 1.2× near → 0.8× far |

Speaker notes:
- Explain that stage base × distance curve keeps icons readable.

---

## Deep‑dive: Directional selection logic (pseudocode)
```
headingDir = Yaw(latest.heading)
toPilot = Normalize(pilotPos - aircraftPos)
angle = Angle(headingDir, toPilot)
isTowards = angle < 45°
isAway    = angle > 135°
signedSide = SignedAngle(headingDir, toPilot)   // +left, −right
isRight = signedSide > 0   // corrected mapping

if isTowards:
  if climbing:   pick UpwardTowards / UpwardClimb / (UpwardRight|UpwardLeft)
  elif descending: pick DownwardTowards / (DownturnRight|DownwardLeftFacingTowards)
  else:          pick (UpwardRight|UpwardLeft)
elif isAway:
  ... (similar pattern with UpwardRight2 / etc.)
else:
  ... (lateral choices)
```

Speaker notes:
- Highlight reverted LR mapping to match in‑app sprite orientation.

---

## Deep‑dive: Trails algorithm
- Data source: live lat/lon → `GeoPoint(lon, lat)` (note order!)
- Seed first point on creation to ensure first segment renders
- Add point only if Haversine distance ≥ `minTrailPointSpacingM` (e.g., 100 m)
- Width scales with zoom: `Lerp(trailWidthMin, trailWidthMax, Zoom01)`
- Keep last `maxTrailPoints` samples (e.g., 64)

Speaker notes:
- Show a 3‑frame growth sequence.

---

## FAA VFR Sectionals — step‑by‑step tutorial
1. Open the FAA item: https://adds-faa.opendata.arcgis.com/maps/6ab79dc5de5743adb3e3b6e3c803aa59/explore?location=36.203116%2C-82.092818%2C10
2. Copy XYZ tiles template: `https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer/tile/{z}/{y}/{x}`
3. Inspector way: Map → Provider: Custom; paste URL into Custom Provider URL
4. Code way:
```
FindObjectOfType<OnlineMapTrafficOverlay>()
  .ConfigureFaaCustomTiles("https://tiles.arcgis.com/.../tile/{z}/{y}/{x}");
```
5. Zoom between LOD 8–12 for best clarity

Speaker notes:
- Mention token `?token=...` if secured.

---

## Live traffic ingestion — architecture
- Unity requests updates (manual/periodic) → MQTT topic `aircraft/request`
- Python bridge fetches localized data (center/radius) from Airplanes.live → OpenSky fallback
- Bridge publishes raw payload → `aircraft/traffic`
- Unity normalizes → list of `AircraftData` and drives UI/POIs/trails

Speaker notes:
- Explain on‑demand mode (less bandwidth; more relevant). 

---

## Debug UI — operator cheat sheet
- Icon progression: medium/close ranges; far/med/close scales
- MQTT: periodic toggle; interval; Request Update Now
- Charts: buttons for VFR Sectional / TAC (vfrmap) / OSM
- Teleport: ICAO/callsign search; offset (m); bearing (deg)

Speaker notes:
- Recommend demo flow: OSM → Sectional; search target → teleport.

---

## Logging & video rendering — details
- JSONL snapshot schema:
```
{
  "time": 1690000000,
  "count": N,
  "aircraft": [
    { "icao24": "abcd12", "callsign": "N12345", "latitude": 40.0, "longitude": -75.0,
      "altitude": 1200, "velocity": 68, "heading": 220, "verticalRate": -1.2, "type": "General" }
  ]
}
```
- Output path: `persistentDataPath/TrafficLogs/traffic_*.jsonl`
- Render command:
```
cd Assets/Scripts/Traffic Data
python visualize_traffic_log.py \
  "<path-to-jsonl>" --fps 5
```

Speaker notes:
- Suggest adding video clips to the deck and demos.

---

## KPIs (fill‑in) & observations
- UI legibility: icon coverage at 3 distances (near/mid/far) — pass/fail
- Symbol correctness: directional cues vs actual motion — pass/fail
- Data freshness: median fetch‐to‑render latency (ms)
- Stability: error rate in logs (%), MQTT reconnects (#/hr)
- Performance: CPU/GPU frametime (ms), allocation spikes (B)

Speaker notes:
- Provide numbers after a test run; keep this slide updated.

---

## Known issues & mitigations
- External tiles/API availability → implement offline cache & retries
- LOD gaps outside 8–12 on FAA tiles → clamp zoom or resample
- Sprite naming variability → Inspector overrides for all icon types
- Heavy traffic density → pooling & throttled updates

Speaker notes:
- Invite feedback for priority of mitigations.

---

## Release checklist
- [ ] FAA tiles verified at LOD 8–12
- [ ] Icon overrides set for final art (Inspector)
- [ ] Debug UI hidden/locked for production
- [ ] Logging storage policy approved
- [ ] Attribution in HUD/footer

Speaker notes:
- Use this slide for sign‑off.

