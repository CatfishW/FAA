using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using FAA.Customization;
using FAA.XPlaneIntegration.Runtime;
using IndicatorSystem.Controller;
using IndicatorSystem.Core;
using Newtonsoft.Json.Linq;
using TrafficRadar;
using TrafficRadar.Core;
using UnityEngine;
using VisualUnderstanding.Core;
using WeatherRadar;

namespace FAA.Explanations
{
    /// <summary>Copies only allowlisted data on the main thread. No polling, simulator writes or disk screenshots.</summary>
    public static class ExplanationEvidenceCollector
    {
        private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
        private static JToken N(double value) => ExplanationEvidence.Finite(value);
        private static string Short(string value, int limit = 600) => string.IsNullOrEmpty(value) ? "" : value.Substring(0, Math.Min(value.Length, limit));

        public static ExplanationSnapshot Capture(bool includeImages)
        {
            var snapshot = new ExplanationSnapshot { ImagesAllowed = includeImages };
            var bridge = Find<XPlane12ApiHudBridge>();
            var radar = Find<TrafficRadarDisplay>();
            // Use the controller wired to the displayed radar, never a dormant duplicate feed.
            var traffic = radar != null ? radar.TrafficController : null;
            var weather = Find<XPlaneOriginalWeatherRadarDisplay>();
            var weatherSettings = Find<WeatherRadarDataProvider>()?.RadarData;
            var chart = radar != null ? radar.ChartProvider : null;
            bool live = Application.isPlaying && bridge != null && bridge.IsFeedHealthy;
            double age = bridge != null ? bridge.LastPacketAgeSeconds : double.PositiveInfinity;
            string feedState = !Application.isPlaying ? "EDITOR PREVIEW" : live ? "LIVE AT CAPTURE" : "STALE / UNAVAILABLE";
            var raw = bridge != null ? bridge.LatestSnapshot : null;

            var flight = new JObject { ["feed_state"] = feedState, ["feed_healthy"] = live, ["values"] = new JObject() };
            var values = (JObject)flight["values"];
            AddValue(values, raw?.Aircraft, "ias_kt", "sim/flightmodel/position/indicated_airspeed");
            AddValue(values, raw?.Aircraft, "altitude_msl_ft", "sim/flightmodel/position/elevation", 3.28084);
            AddValue(values, raw?.Aircraft, "altitude_agl_ft", "sim/flightmodel/position/y_agl", 3.28084);
            AddValue(values, raw?.Aircraft, "vertical_speed_fpm", "sim/flightmodel/position/vh_ind", 196.8504);
            AddValue(values, raw?.Aircraft, "ground_speed_kt", "sim/flightmodel/position/groundspeed", 1.94384);
            AddValue(values, raw?.Aircraft, "pitch_deg", "sim/flightmodel/position/theta");
            AddValue(values, raw?.Aircraft, "roll_deg", "sim/flightmodel/position/phi");
            AddValue(values, raw?.Aircraft, "heading_true_deg", "sim/flightmodel/position/psi");
            AddValue(values, raw?.Aircraft, "latitude_deg", "sim/flightmodel/position/latitude");
            AddValue(values, raw?.Aircraft, "longitude_deg", "sim/flightmodel/position/longitude");
            AddValue(values, raw?.Weather, "precipitation_ratio_at_aircraft", "sim/weather/aircraft/precipitation_on_aircraft_ratio");
            AddValue(values, raw?.Weather, "visibility_m", "sim/weather/visibility_reported_m");
            AddValue(values, raw?.Weather, "wind_speed_mps", "sim/weather/aircraft/wind_now_speed_msc");
            AddValue(values, raw?.Weather, "wind_true_deg", "sim/weather/aircraft/wind_now_direction_degt");
            AddValue(values, raw?.Weather, "wind_speed_kt", "sim/weather/aircraft/wind_now_speed_msc", 1.94384);
            AddValue(values, raw?.Weather, "visibility_sm", "sim/weather/visibility_reported_m", 1.0 / 1609.344);
            var engine = bridge != null ? bridge.LatestRawFlightData : null;
            flight["derived_engine_values"] = new JObject
            {
                ["engine_count"] = engine != null && engine.engineCount > 0 ? (JToken)engine.engineCount : JValue.CreateNull(),
                ["torque_1_percent"] = engine != null && engine.engine1TorqueValid ? N(engine.engine1Torque) : JValue.CreateNull(),
                ["n2_1_percent"] = engine != null && engine.engine1NRValid ? N(engine.engine1NR) : JValue.CreateNull(),
                ["rotor_nr_percent"] = engine != null && engine.rotorNRValid ? N(engine.rotorNR) : JValue.CreateNull(),
                ["provenance"] = "XPlane12ApiHudBridge validated conversions. Missing fields remain null."
            };
            flight["limitations"] = "Simulation snapshot, not certified instruments. Approved aircraft limits and operating conditions are not supplied; do not invent safe speed or power margins. Missing datarefs are null; packet age is feed-level, not independent per-field validation.";
            snapshot.Add("read_flight_telemetry", "Flight telemetry", "XPlane12ApiHudBridge · raw datarefs", feedState, flight, age);

            var contacts = new JArray();
            var targets = traffic != null ? traffic.CurrentTargets : null;
            if (targets != null)
                foreach (var t in targets.OrderBy(t => t.DistanceNM).Take(12))
                    contacts.Add(new JObject
                    {
                        ["callsign"] = Short(t.Callsign, 32), ["track_id"] = Short(t.Icao24, 32),
                        ["aircraft_type"] = t.AircraftType.ToString(), ["distance_nm"] = N(t.DistanceNM),
                        ["bearing_deg"] = N(t.BearingDegrees), ["relative_altitude_ft"] = N(t.RelativeAltitudeFeet),
                        ["bearing_reference"] = "true north",
                        ["bearing_relative_to_nose_deg"] = live && raw != null &&
                            raw.Aircraft.TryGetValue("sim/flightmodel/position/psi", out float ownHeading) &&
                            !float.IsNaN(ownHeading) && !float.IsInfinity(ownHeading) ?
                            N(Mathf.DeltaAngle(ownHeading, t.BearingDegrees)) : JValue.CreateNull(),
                        ["altitude_ft"] = N(t.AltitudeFeet), ["ground_speed_kt"] = N(t.GroundSpeedKnots),
                        ["vertical_rate_fpm"] = N(t.VerticalRateFpm), ["target_age_seconds"] = N(t.TimeSinceUpdate),
                        ["app_threat_class"] = t.ThreatLevel.ToString()
                    });
            snapshot.Add("read_traffic", "Traffic picture", "TrafficRadarController · processed simulator targets", traffic == null || targets == null ? "UNAVAILABLE" : feedState,
                new JObject
                {
                    ["feed_healthy"] = live, ["range_nm"] = traffic != null ? N(traffic.RangeNM) : JValue.CreateNull(),
                    ["processed_target_set_available"] = targets != null,
                    ["targets_in_display_set"] = targets != null ? (JToken)targets.Count : JValue.CreateNull(), ["returned_nearest_targets"] = contacts,
                    ["limitations"] = "Only nearest 12 of range/cap-filtered display set. Unknown category is not inferred from speed or callsign. App threat classes are not certified TCAS. No contacts does not establish clear airspace."
                }, age);

            var chartData = new JObject { ["available"] = chart != null };
            if (chart != null)
            {
                chartData["source"] = chart.MapSourceName;
                chartData["attribution"] = chart.MapSourceAttribution;
                chartData["load_state"] = chart.Status.ToString();
                chartData["procedural_fallback"] = chart.IsUsingProceduralFallback;
                chartData["has_last_good_texture"] = chart.HasLastGoodTexture;
                chartData["tiles_in_last_success"] = chart.LastSuccessfulTileCount;
                chartData["last_fetch_utc"] = chart.LastSuccessfulFetchUtc == DateTime.MinValue ? JValue.CreateNull() : (JToken)chart.LastSuccessfulFetchUtc.ToString("O");
                chartData["tile_zoom_level"] = chart.LastSuccessfulZoomLevel;
                chartData["display_range_nm"] = N(radar.RangeNM);
                chartData["chart_visible"] = radar.ChartBackgroundVisible && radar.InstrumentDisplayEnabled;
                chartData["opacity"] = N(radar.ChartOpacity);
                chartData["track_up"] = radar.TrackUpModeEnabled;
                chartData["full_map"] = radar.IsFullscreen;
                chartData["requested_center_latitude"] = N(chart.LastRequestedLatitude);
                chartData["requested_center_longitude"] = N(chart.LastRequestedLongitude);
                chartData["loaded_center_latitude"] = N(chart.LastSuccessfulLatitude);
                chartData["loaded_center_longitude"] = N(chart.LastSuccessfulLongitude);
                chartData["map_pan_canvas_pixels"] = new JArray(N(radar.MapPan.x), N(radar.MapPan.y));
                chartData["edition_effective_date"] = JValue.CreateNull();
            }
            chartData["limitations"] = "Raster context only. Download time does not establish chart currency. Partial tile coverage or retained last-good texture may not cover requested area. Visual contents require the image tool; do not infer labels from location metadata.";
            snapshot.Add("read_chart_status", "Chart & coverage", "FAASectionalChartProvider + TrafficRadarDisplay", chart == null ? "UNAVAILABLE" : chart.IsUsingProceduralFallback ? "PROCEDURAL FALLBACK" : chart.Status.ToString().ToUpperInvariant(), chartData, chart != null ? chart.SecondsSinceLastSuccess : (double?)null);

            var displays = new JArray(UnityEngine.Object.FindObjectsByType<FaaRadarPresentation>().Where(p => p.isActiveAndEnabled).Select(p => new JObject
            {
                ["instrument"] = p.RadarKind.ToString(), ["display_on"] = p.IsDisplayOn,
                ["state"] = p.CurrentState.ToString(), ["status_text"] = Short(p.StatusText, 120)
            }));
            var cues = Find<IndicatorSystemController>();
            bool hasRegionalTurbulence = TurbulenceEvidence.TryRegionalMaximum(raw?.Weather, out float turbulenceValue, out int turbulenceSamples);
            bool hasAircraftTurbulence = TurbulenceEvidence.TryAircraftMaximum(raw?.Weather, out float aircraftTurbulence, out int aircraftTurbulenceSamples);
            var status = new JObject
            {
                ["radar_displays"] = displays, ["feed_state"] = feedState,
                ["screen_cues"] = cues == null ? (JToken)JValue.CreateNull() : new JObject
                {
                    ["traffic_markers_enabled"] = cues.IsTypeVisible(IndicatorType.Traffic),
                    ["weather_markers_enabled"] = cues.IsTypeVisible(IndicatorType.Weather),
                    ["in_view"] = cues.OnScreenCount, ["off_screen"] = cues.OffScreenCount, ["suppressed_by_declutter"] = cues.SuppressedCount
                },
                ["weather_settings"] = weatherSettings == null ? (JToken)JValue.CreateNull() : new JObject
                {
                    ["range_nm"] = N(weatherSettings.currentRange), ["mode"] = weatherSettings.currentMode.ToString(),
                    ["gain_db"] = N(weatherSettings.gainOffset), ["tilt_deg"] = N(weatherSettings.tiltAngle)
                },
                ["weather_power"] = weather != null && weather.HasRadarPowerState && live ? (JToken)weather.IsRadarPowered : JValue.CreateNull(),
                ["weather_texture_fresh"] = weather != null && weather.HasFreshTexture,
                ["turbulence"] = new JObject
                {
                    ["spatial_scan_available"] = false,
                    ["regional_samples_available"] = turbulenceSamples,
                    ["scale"] = "X-Plane turbulence factor 0..10; not percent or an operational severity category",
                    ["regional_setting_max_factor"] = live && hasRegionalTurbulence ? N(turbulenceValue) : JValue.CreateNull(),
                    ["aircraft_column_samples_available"] = aircraftTurbulenceSamples,
                    ["aircraft_column_max_factor"] = live && hasAircraftTurbulence ? N(aircraftTurbulence) : JValue.CreateNull(),
                    ["explanation"] = TurbulenceEvidence.Explanation(raw?.Weather, live),
                    ["limitations"] = "This integration supplies no measured spatial turbulence scan. Regional simulator settings do not locate turbulence; precipitation is not turbulence. Missing samples are not zero or clear air."
                },
                ["weather_spatial_source"] = weather == null ? "unavailable" : weather.IsProceduralTexture ? "SIM WX · illustrative synthesized spatial returns" : "provider image · measurement provenance not independently verified",
                ["limitations"] = "Display and marker switches control local visibility only; they do not command simulator power. Unknown power stays null. SIM WX is not measured storm geometry."
            };
            snapshot.Add("read_display_status", "Displays & screen cues", "FaaRadarPresentation · indicator controller · weather display", "LOCAL STATE AT CAPTURE", status, 0);

            var vision = Find<VisualAnalysisManager>();
            var findings = new JArray();
            if (vision != null)
                foreach (var type in new[] { VisualAnalysisType.SectionalChart, VisualAnalysisType.WeatherRadar })
                {
                    var result = vision.GetLastResult(type);
                    if (result == null) continue;
                    findings.Add(new JObject
                    {
                        ["analysis_type"] = type.ToString(), ["success"] = result.IsSuccess,
                        ["analysis_utc"] = result.timestamp.ToUniversalTime().ToString("O"),
                        ["analysis_age_seconds"] = N(Math.Max(0, (snapshot.CapturedUtc - result.timestamp.ToUniversalTime()).TotalSeconds)),
                        ["summary_unverified"] = Short(result.summary, 1400),
                        ["findings_unverified"] = new JArray((result.findings ?? new List<AnalysisFinding>()).Take(5).Select(f => new JObject
                        { ["title"] = Short(f.title, 120), ["description"] = Short(f.description, 500) })),
                        ["matches_current_image"] = JValue.CreateNull()
                    });
                }
            snapshot.Add("read_visual_analysis", "Existing visual analysis", "VisualAnalysisManager · cached interpretations", findings.Count == 0 ? "NO CACHED FINDINGS" : "UNVERIFIED / ALIGNMENT UNKNOWN",
                new JObject { ["manager_available"] = vision != null, ["cached_results"] = findings,
                    ["limitations"] = "Untrusted cached AI interpretation, not evidence of current image contents. Image match is unknown. Legacy hard-coded confidence values are deliberately excluded." });

            CaptureImage(snapshot, "inspect_chart_image", "Chart image", radar != null ? radar.ChartImage?.texture : null,
                radar != null && radar.ChartImage != null ? radar.ChartImage.uvRect : new Rect(0, 0, 1, 1),
                chart != null && chart.IsUsingProceduralFallback ? "PROCEDURAL FALLBACK" : "RASTER / UNVERIFIED CONTENT",
                "North-up source crop at captured UV extent. Does not include traffic symbols, rotation, circular mask or HUD. Effective chart date unknown. Image interpretation/OCR is not authoritative.", chart != null ? chart.SecondsSinceLastSuccess : (double?)null);
            CaptureImage(snapshot, "inspect_weather_image", "Weather image", weather != null ? weather.CurrentTexture : null, new Rect(0, 0, 1, 1),
                weather != null && weather.IsProceduralTexture ? "ILLUSTRATIVE SIM WX" : "PROVIDER IMAGE / PROVENANCE UNVERIFIED",
                "Source texture, not a whole-screen screenshot. SIM WX spatial returns are synthesized, not detected storm locations; do not infer real hazards or clear sectors.", weather != null && weather.HasUsableTexture ? Math.Max(0, Time.realtimeSinceStartup - weather.LastTextureRealtime) : (double?)null);
            return snapshot;
        }

        private static void AddValue(JObject data, IDictionary<string, float> source, string label, string dataref, double multiplier = 1)
        {
            data[label] = new JObject { ["value"] = source != null && source.TryGetValue(dataref, out float value) ? N(value * multiplier) : JValue.CreateNull(), ["dataref"] = dataref };
        }

        private static void CaptureImage(ExplanationSnapshot snapshot, string tool, string title, Texture source, Rect uv, string state, string limitations, double? age)
        {
            var data = new JObject { ["image_attached"] = false, ["limitations"] = limitations };
            string dataUrl = null;
            if (snapshot.ImagesAllowed && source != null)
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture target = null;
                Texture2D pixels = null;
                try
                {
                    int size = Math.Min(1024, Math.Max(source.width, source.height));
                    target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(source, target, uv.size, uv.position);
                    RenderTexture.active = target;
                    pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0); pixels.Apply();
                    byte[] jpeg = pixels.EncodeToJPG(85);
                    if (jpeg.Length > 1500000) throw new InvalidOperationException("Image limit");
                    using (var hash = SHA256.Create()) data["sha256"] = BitConverter.ToString(hash.ComputeHash(jpeg)).Replace("-", "").ToLowerInvariant();
                    data["image_attached"] = true; data["image_pixels"] = new JArray(size, size);
                    data["source_pixels"] = new JArray(source.width, source.height);
                    data["crop_uv"] = new JArray(uv.x, uv.y, uv.width, uv.height);
                    dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
                }
                catch { state = "CAPTURE UNAVAILABLE"; data["image_attached"] = false; }
                finally
                {
                    RenderTexture.active = previous;
                    if (target != null) RenderTexture.ReleaseTemporary(target);
                    if (pixels != null) UnityEngine.Object.Destroy(pixels);
                }
            }
            else state = !snapshot.ImagesAllowed ? "IMAGE SHARING OFF" : "NO SOURCE IMAGE";
            snapshot.Add(tool, title, "Scoped source texture capture · SHA-256 identity", state, data, age);
            if (dataUrl != null) snapshot.ImageDataUrls[tool] = dataUrl;
        }
    }
}
