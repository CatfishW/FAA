using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    public class GoogleRoutingRequest : TextWebService<GoogleRoutingResult>
    {
        /// <summary>
        /// Origin waypoint.
        /// </summary>
        public Waypoint origin;

        /// <summary>
        /// Destination waypoint.
        /// </summary>
        public Waypoint destination;

        /// <summary>
        /// A set of waypoints along the route (excluding terminal points), for either stopping at or passing by.
        /// Up to 25 intermediate waypoints are supported.
        /// </summary>
        public Waypoint[] intermediates;

        /// <summary>
        /// Specifies the mode of transportation.
        /// </summary>
        public RouteTravelMode travelMode = RouteTravelMode.DRIVE;

        /// <summary>
        /// Specifies how to compute the route.
        /// The server attempts to use the selected routing preference to compute the route.
        /// If the routing preference results in an error or an extra long latency, then an error is returned.
        /// You can specify this option only when the travelMode is DRIVE or TWO_WHEELER, otherwise the request fails.
        /// </summary>
        public RoutingPreference routingPreference = RoutingPreference.TRAFFIC_AWARE;

        /// <summary>
        /// Specifies your preference for the quality of the polyline.
        /// </summary>
        public PolylineQuality polylineQuality = PolylineQuality.OVERVIEW;

        /// <summary>
        /// Specifies the preferred encoding for the polyline.
        /// </summary>
        public PolylineEncoding polylineEncoding = PolylineEncoding.ENCODED_POLYLINE;

        /// <summary>
        /// The departure time.
        /// If you don't set this value, then this value defaults to the time that you made the request.
        /// NOTE: You can only specify a departureTime in the past when RouteTravelMode is set to TRANSIT.
        /// Transit trips are available for up to 7 days in the past or 100 days in the future.
        /// A timestamp in RFC3339 UTC "Zulu" format, with nanosecond resolution and up to nine fractional digits.
        /// Examples: "2014-10-02T15:01:23Z" and "2014-10-02T15:01:23.045123456Z". 
        /// </summary>
        public string departureTime;

        /// <summary>
        /// The arrival time.
        /// NOTE: Can only be set when RouteTravelMode is set to TRANSIT.
        /// You can specify either departureTime or arrivalTime, but not both.
        /// Transit trips are available for up to 7 days in the past or 100 days in the future.
        /// A timestamp in RFC3339 UTC "Zulu" format, with nanosecond resolution and up to nine fractional digits.
        /// Examples: "2014-10-02T15:01:23Z" and "2014-10-02T15:01:23.045123456Z".
        /// </summary>
        public string arrivalTime;

        /// <summary>
        /// Specifies whether to calculate alternate routes in addition to the route.
        /// No alternative routes are returned for requests that have intermediate waypoints.
        /// </summary>
        public bool computeAlternativeRoutes = false;

        /// <summary>
        /// A set of conditions to satisfy that affect the way routes are calculated.
        /// </summary>
        public RouteModifiers routeModifiers;

        /// <summary>
        /// The BCP-47 language code, such as "en-US" or "sr-Latn".
        /// For more information, see Unicode Locale Identifier.
        /// See Language Support for the list of supported languages.
        /// When you don't provide this value, the display language is inferred from the location of the route request.
        /// https://developers.google.com/maps/faq#languagesupport
        /// </summary>
        public string languageCode;

        /// <summary>
        /// Specifies the units of measure for the display fields.
        /// These fields include the instruction field in NavigationInstruction.
        /// The units of measure used for the route, leg, step distance, and duration are not affected by this value.
        /// If you don't provide this value, then the display units are inferred from the location of the first origin.
        /// </summary>
        public string regionCode;

        /// <summary>
        /// Specifies the units of measure for the display fields.
        /// These fields include the instruction field in NavigationInstruction.
        /// The units of measure used for the route, leg, step distance, and duration are not affected by this value.
        /// If you don't provide this value, then the display units are inferred from the location of the first origin.
        /// </summary>
        public Units units = Units.UNSPECIFIED;

        /// <summary>
        /// If set to true, the service attempts to minimize the overall cost of the route by re-ordering the specified intermediate waypoints.
        /// The request fails if any of the intermediate waypoints is a via waypoint.
        /// Use ComputeRoutesResponse.Routes.optimized_intermediate_waypoint_index to find the new ordering. If
        /// ComputeRoutesResponseroutes.optimized_intermediate_waypoint_index is not requested in the X-Goog-FieldMask header, the request fails.
        /// If optimizeWaypointOrder is set to false, ComputeRoutesResponse.optimized_intermediate_waypoint_index will be empty.
        /// </summary>
        public bool optimizeWaypointOrder;

        /// <summary>
        /// Specifies what reference routes to calculate as part of the request in addition to the default route.
        /// A reference route is a route with a different route calculation objective than the default route.
        /// For example a FUEL_EFFICIENT reference route calculation takes into account various parameters that would generate an optimal fuel efficient route.
        /// When using this feature, look for routeLabels on the resulting routes.
        /// </summary>
        public ReferenceRoute[] requestedReferenceRoutes;

        /// <summary>
        /// A list of extra computations which may be used to complete the request.
        /// Note: These extra computations may return extra fields on the response.
        /// These extra fields must also be specified in the field mask to be returned in the response.
        /// </summary>
        public ExtraComputation[] extraComputations;

        /// <summary>
        /// Specifies the assumptions to use when calculating time in traffic.
        /// This setting affects the value returned in the duration field in the Route and RouteLeg which contains the predicted time in traffic based on historical averages.
        /// TrafficModel is only available for requests that have set RoutingPreference to TRAFFIC_AWARE_OPTIMAL and RouteTravelMode to DRIVE.
        /// Defaults to BEST_GUESS if traffic is requested and TrafficModel is not specified.
        /// </summary>
        public TrafficModel trafficModel = TrafficModel.BEST_GUESS;

        /// <summary>
        /// Specifies preferences that influence the route returned for TRANSIT routes.
        /// NOTE: You can only specify a transitPreferences when RouteTravelMode is set to TRANSIT.
        /// </summary>
        public TransitPreferences transitPreferences;

        /// <summary>
        /// FieldMask used for response filtering.
        /// If empty, all fields should be returned unless documented otherwise.
        /// </summary>
        public string fieldMask = "*";

        /// <summary>
        /// API key. If not specified, the key will be taken from Key Manager.
        /// </summary>
        public string key;

        public GoogleRoutingRequest(object origin, object destination)
        {
            this.origin = ObjectToWaypoint(origin);
            this.destination = ObjectToWaypoint(destination);
        }

        private static Waypoint ObjectToWaypoint(object obj)
        {
            if (obj is Waypoint) return obj as Waypoint;
            if (obj is Vector2 || obj is Vector3 || obj is Vector2d || obj is GeoPoint || obj is GeoPoint3)
            {
                return new Waypoint
                {
                    location = new Location(obj),
                    type = WaypointType.LOCATION
                };
            }

            if (obj is string)
            {
                return new Waypoint
                {
                    address = obj as string,
                    type = WaypointType.ADDRESS
                };
            }

            return null;
        }

        /// <summary>
        /// Converts the response string to a result object.
        /// </summary>
        /// <param name="response">Response string</param>
        /// <returns>Result object</returns>
        public static GoogleRoutingResult GetResult(string response)
        {
            try
            {
                return JSON.Deserialize<GoogleRoutingResult>(response);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return null;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append("https://routes.googleapis.com/directions/v2:computeRoutes");
        }

        protected override void CreateRequest()
        {
            if (string.IsNullOrEmpty(key)) key = KeyManager.GoogleMaps();

            JSONObject json = ToJson();
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "X-Goog-FieldMask", fieldMask },
                { "X-Goog-Api-Key", key }
            };
            
            www = new WebRequest(url, json.ToString(), headers);
        }
        
        public static GoogleRoutingResult Parse(string response) => GoogleRoutingResult.Parse(response);
        public override GoogleRoutingResult ParseResult(string response) => Parse(response);

        private JSONObject ToJson()
        {
            JSONObject obj = new JSONObject();
            obj["origin"] = origin.ToJson();
            obj["destination"] = destination.ToJson();

            if (intermediates != null && intermediates.Length > 0)
            {
                JSONArray intermediatesArray = new JSONArray();
                foreach (Waypoint waypoint in intermediates) intermediatesArray.Add(waypoint.ToJson());
                obj.Add("intermediates", intermediatesArray);
            }

            if (travelMode != RouteTravelMode.DRIVE) obj.Add("travelMode", travelMode.ToString());
            if (routingPreference != RoutingPreference.TRAFFIC_AWARE) obj.Add("routingPreference", routingPreference.ToString());
            if (polylineQuality != PolylineQuality.OVERVIEW) obj.Add("polylineQuality", polylineQuality.ToString());
            if (polylineEncoding != PolylineEncoding.ENCODED_POLYLINE) obj.Add("polylineEncoding", polylineEncoding.ToString());
            if (!string.IsNullOrEmpty(departureTime)) obj.Add("departureTime", departureTime);
            if (!string.IsNullOrEmpty(arrivalTime)) obj.Add("arrivalTime", arrivalTime);
            if (computeAlternativeRoutes) obj.Add("computeAlternativeRoutes", true);
            if (routeModifiers != null) obj.Add("routeModifiers", routeModifiers.ToJson());
            if (!string.IsNullOrEmpty(languageCode)) obj.Add("languageCode", languageCode);
            if (!string.IsNullOrEmpty(regionCode)) obj.Add("regionCode", regionCode);
            if (units != Units.UNSPECIFIED) obj.Add("units", units.ToString());
            if (optimizeWaypointOrder) obj.Add("optimizeWaypointOrder", true);

            if (requestedReferenceRoutes != null && requestedReferenceRoutes.Length > 0)
            {
                JSONArray requestedReferenceRoutesArray = new JSONArray();
                foreach (ReferenceRoute referenceRoute in requestedReferenceRoutes)
                {
                    requestedReferenceRoutesArray.Add(new JSONValue(referenceRoute.ToString()));
                }

                obj.Add("requestedReferenceRoutes", requestedReferenceRoutesArray);
            }

            if (extraComputations != null && extraComputations.Length > 0)
            {
                JSONArray extraComputationsArray = new JSONArray();
                foreach (ExtraComputation extraComputation in extraComputations)
                {
                    extraComputationsArray.Add(new JSONValue(extraComputation.ToString()));
                }

                obj.Add("extraComputations", extraComputationsArray);
            }

            if (trafficModel != TrafficModel.BEST_GUESS) obj.Add("trafficModel", trafficModel.ToString());
            if (transitPreferences != null) obj.Add("transitPreferences", transitPreferences.ToJson());

            return obj;
        }

        /// <summary>
        /// An object that represents a latitude/longitude pair.
        /// This is expressed as a pair of doubles to represent degrees latitude and degrees longitude.
        /// Unless specified otherwise, this object must conform to the WGS84 standard.
        /// Values must be within normalized ranges.
        /// </summary>
        public struct LatLng
        {
            /// <summary>
            /// The latitude in degrees. It must be in the range [-90.0, +90.0].
            /// </summary>
            public double latitude;

            /// <summary>
            /// The longitude in degrees. It must be in the range [-180.0, +180.0].
            /// </summary>
            public double longitude;

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                obj.Add("latitude", latitude);
                obj.Add("longitude", longitude);
                return obj;
            }

            public static implicit operator Vector2(LatLng latLng)
            {
                return new Vector2((float)latLng.longitude, (float)latLng.latitude);
            }

            public static implicit operator GeoPoint(LatLng latLng)
            {
                return new GeoPoint(latLng.longitude, latLng.latitude);
            }
        }

        /// <summary>
        /// Encapsulates a location (a geographic point, and an optional heading).
        /// </summary>
        public class Location
        {
            /// <summary>
            /// The waypoint's geographic coordinates.
            /// </summary>
            public LatLng latLng;

            /// <summary>
            /// The compass heading associated with the direction of the flow of traffic.
            /// This value specifies the side of the road for pickup and drop-off.
            /// Heading values can be from 0 to 360, where 0 specifies a heading of due North, 90 specifies a heading of due East, and so on.
            /// You can use this field only for DRIVE and TWO_WHEELER RouteTravelMode.
            /// </summary>
            public int? heading;

            public Location()
            {
            }

            public Location(object obj)
            {
                if (obj is LatLng) latLng = (LatLng)obj;
                else if (obj is Vector2)
                {
                    Vector2 v = (Vector2)obj;
                    latLng = new LatLng { latitude = v.y, longitude = v.x };
                }
                else if (obj is Vector3)
                {
                    Vector3 v = (Vector3)obj;
                    latLng = new LatLng { latitude = v.y, longitude = v.x };
                }
                else if (obj is Vector2d)
                {
                    Vector2d v = (Vector2d)obj;
                    latLng = new LatLng { latitude = v.y, longitude = v.x };
                }
                else if (obj is GeoPoint)
                {
                    GeoPoint v = (GeoPoint)obj;
                    latLng = new LatLng { latitude = v.y, longitude = v.x };
                }
                else if (obj is GeoPoint3)
                {
                    GeoPoint3 v = (GeoPoint3)obj;
                    latLng = new LatLng { latitude = v.y, longitude = v.x };
                }
            }

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                obj.Add("latLng", latLng.ToJson());
                if (heading.HasValue) obj.Add("heading", heading.Value);
                return obj;
            }

            public static implicit operator Vector2(Location location)
            {
                return location.latLng;
            }
        }

        /// <summary>
        /// Preferences for TRANSIT based routes that influence the route that is returned.
        /// </summary>
        public class TransitPreferences
        {
            /// <summary>
            /// A set of travel modes to use when getting a TRANSIT route. Defaults to all supported modes of travel.
            /// </summary>
            public TransitTravelMode[] allowedTravelModes;

            /// <summary>
            /// A routing preference that, when specified, influences the TRANSIT route returned.
            /// </summary>
            public TransitRoutingPreference routingPreference;

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                if (allowedTravelModes != null && allowedTravelModes.Length > 0)
                {
                    JSONArray allowedTravelModesArray = new JSONArray();
                    foreach (TransitTravelMode travelMode in allowedTravelModes)
                    {
                        allowedTravelModesArray.Add(new JSONValue(travelMode.ToString()));
                    }

                    obj.Add("allowedTravelModes", allowedTravelModesArray);
                }

                if (routingPreference != TransitRoutingPreference.NONE) obj.Add("routingPreference", routingPreference.ToString());
                return obj;
            }
        }

        /// <summary>
        /// Encapsulates a waypoint. Waypoints mark both the beginning and end of a route, and include intermediate stops along the route.
        /// </summary>
        public class Waypoint
        {
            /// <summary>
            /// Marks this waypoint as a milestone rather a stopping point.
            /// For each non-via waypoint in the request, the response appends an entry to the legs array to provide the details for stopovers on that leg of the trip.
            /// Set this value to true when you want the route to pass through this waypoint without stopping over.
            /// Via waypoints don't cause an entry to be added to the legs array, but they do route the journey through the waypoint.
            /// You can only set this value on waypoints that are intermediates. The request fails if you set this field on terminal waypoints.
            /// If ComputeRoutesRequest.optimize_waypoint_order is set to true then this field cannot be set to true; otherwise, the request fails.
            /// </summary>
            public bool via;

            /// <summary>
            /// Indicates that the waypoint is meant for vehicles to stop at, where the intention is to either pickup or drop-off.
            /// When you set this value, the calculated route won't include non-via waypoints on roads that are unsuitable for pickup and drop-off.
            /// This option works only for DRIVE and TWO_WHEELER travel modes, and when the locationType is Location.
            /// </summary>
            public bool vehicleStopover;

            /// <summary>
            /// Indicates that the location of this waypoint is meant to have a preference for the vehicle to stop at a particular side of road.
            /// When you set this value, the route will pass through the location so that the vehicle can stop at the side of road that the location is biased towards from the center of the road.
            /// This option works only for DRIVE and TWO_WHEELER RouteTravelMode.
            /// </summary>
            public bool sideOfRoad;

            /// <summary>
            /// Type of the waypoint.
            /// </summary>
            public WaypointType type = WaypointType.LOCATION;

            /// <summary>
            /// A point specified using geographic coordinates, including an optional heading.
            /// </summary>
            public Location location;

            /// <summary>
            /// The POI Place ID associated with the waypoint.
            /// </summary>
            public string placeId;

            /// <summary>
            /// Human readable address or a plus code.
            /// See https://plus.codes for details.
            /// </summary>
            public string address;

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                if (via) obj.Add("via", true);
                if (vehicleStopover) obj.Add("vehicleStopover", true);
                if (sideOfRoad) obj.Add("sideOfRoad", true);

                if (type == WaypointType.LOCATION)
                {
                    obj.Add("location", location.ToJson());
                }
                else if (type == WaypointType.PLACE_ID)
                {
                    obj.Add("placeId", placeId);
                }
                else if (type == WaypointType.ADDRESS)
                {
                    obj.Add("address", address);
                }

                return obj;
            }
        }

        /// <summary>
        /// Encapsulates a set of optional conditions to satisfy when calculating the routes.
        /// </summary>
        public class RouteModifiers
        {
            /// <summary>
            /// When set to true, avoids toll roads where reasonable, giving preference to routes not containing toll roads.
            /// Applies only to the DRIVE and TWO_WHEELER RouteTravelMode.
            /// </summary>
            public bool avoidTolls = false;

            /// <summary>
            /// When set to true, avoids highways where reasonable, giving preference to routes not containing highways.
            /// Applies only to the DRIVE and TWO_WHEELER RouteTravelMode.
            /// </summary>
            public bool avoidHighways = false;

            /// <summary>
            /// When set to true, avoids ferries where reasonable, giving preference to routes not containing ferries.
            /// Applies only to the DRIVE andTWO_WHEELER RouteTravelMode.
            /// </summary>
            public bool avoidFerries = false;

            /// <summary>
            /// When set to true, avoids navigating indoors where reasonable, giving preference to routes not containing indoor navigation.
            /// Applies only to the WALK RouteTravelMode.
            /// </summary>
            public bool avoidIndoor = false;

            /// <summary>
            /// Specifies the vehicle information.
            /// </summary>
            public VehicleInfo vehicleInfo;

            /// <summary>
            /// Encapsulates information about toll passes.
            /// If toll passes are provided, the API tries to return the pass price.
            /// If toll passes are not provided, the API treats the toll pass as unknown and tries to return the cash price.
            /// Applies only to the DRIVE and TWO_WHEELER RouteTravelMode.
            /// </summary>
            public string[] tollPasses;

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                if (avoidTolls) obj.Add("avoidTolls", true);
                if (avoidHighways) obj.Add("avoidHighways", true);
                if (avoidFerries) obj.Add("avoidFerries", true);
                if (avoidIndoor) obj.Add("avoidIndoor", true);
                if (vehicleInfo != null) obj.Add("vehicleInfo", vehicleInfo.ToJson());
                if (tollPasses != null && tollPasses.Length > 0)
                {
                    JSONArray tollPassesArray = new JSONArray();
                    foreach (string tollPass in tollPasses) tollPassesArray.Add(new JSONValue(tollPass));
                    obj.Add("tollPasses", tollPassesArray);
                }

                return obj;
            }
        }

        /// <summary>
        /// Contains the vehicle information, such as the vehicle emission type.
        /// </summary>
        public class VehicleInfo
        {
            /// <summary>
            /// Describes the vehicle's emission type. Applies only to the DRIVE RouteTravelMode.
            /// </summary>
            public VehicleEmissionType emissionType = VehicleEmissionType.GASOLINE;

            public JSONObject ToJson()
            {
                JSONObject obj = new JSONObject();
                if (emissionType != VehicleEmissionType.GASOLINE) obj.Add("emissionType", emissionType.ToString());
                return obj;
            }
        }

        /// <summary>
        /// Extra computations to perform while completing the request.
        /// </summary>
        public enum ExtraComputation
        {
            /// <summary>
            /// Toll information for the route(s).
            /// </summary>
            TOLLS,

            /// <summary>
            /// Estimated fuel consumption for the route(s).
            /// </summary>
            FUEL_CONSUMPTION,

            /// <summary>
            /// Traffic aware polylines for the route(s).
            /// </summary>
            TRAFFIC_ON_POLYLINE,

            /// <summary>
            /// NavigationInstructions presented as a formatted HTML text string.
            /// This content is meant to be read as-is.
            /// This content is for display only. Do not programmatically parse it.
            /// </summary>
            HTML_FORMATTED_NAVIGATION_INSTRUCTIONS,

            /// <summary>
            /// Flyover information for the route(s).
            /// The routes.polyline_details.flyover_info fieldmask must be specified to return this information.
            /// This data will only currently be populated for certain metros in India.
            /// This feature is experimental, and the SKU/charge is subject to change.
            /// </summary>
            FLYOVER_INFO_ON_POLYLINE,

            /// <summary>
            /// Narrow road information for the route(s).
            /// The routes.polyline_details.narrow_road_info fieldmask must be specified to return this information.
            /// This data will only currently be populated for certain metros in India.
            /// This feature is experimental, and the SKU/charge is subject to change.
            /// </summary>
            NARROW_ROAD_INFO_ON_POLYLINE
        }

        /// <summary>
        /// Specifies the preferred type of polyline to be returned.
        /// </summary>
        public enum PolylineEncoding
        {
            /// <summary>
            /// Specifies a polyline encoded using the polyline encoding algorithm.
            /// https://developers.google.com/maps/documentation/utilities/polylinealgorithm
            /// </summary>
            ENCODED_POLYLINE,

            /// <summary>
            /// Specifies a polyline using the GeoJSON LineString format.
            /// https://tools.ietf.org/html/rfc7946#section-3.1.4
            /// </summary>
            GEO_JSON_LINESTRING
        }

        /// <summary>
        /// A set of values that specify the quality of the polyline.
        /// </summary>
        public enum PolylineQuality
        {
            /// <summary>
            /// Specifies a high-quality polyline - which is composed using more points than OVERVIEW, at the cost of increased response size. Use this value when you need more precision.
            /// </summary>
            HIGH_QUALITY,

            /// <summary>
            /// Specifies an overview polyline - which is composed using a small number of points.
            /// Use this value when displaying an overview of the route.
            /// Using this option has a lower request latency compared to using the HIGH_QUALITY option.
            /// </summary>
            OVERVIEW
        }

        /// <summary>
        /// A supported reference route on the ComputeRoutesRequest.
        /// </summary>
        public enum ReferenceRoute
        {
            /// <summary>
            /// Fuel efficient route.
            /// </summary>
            FUEL_EFFICIENT,

            /// <summary>
            /// Route with shorter travel distance. This is an experimental feature.
            /// For DRIVE requests, this feature prioritizes shorter distance over driving comfort.
            /// For example, it may prefer local roads instead of highways, take dirt roads, cut through parking lots, etc.
            /// This feature does not return any maneuvers that Google Maps knows to be illegal.
            /// For BICYCLE and TWO_WHEELER requests, this feature returns routes similar to those returned when you don't specify requestedReferenceRoutes.
            /// This feature is not compatible with any other travel modes, via intermediate waypoints, or optimizeWaypointOrder; such requests will fail.
            /// However, you can use it with any routingPreference.
            /// </summary>
            SHORTER_DISTANCE
        }

        /// <summary>
        /// A set of values used to specify the mode of travel.
        /// NOTE: WALK, BICYCLE, and TWO_WHEELER routes are in beta and might sometimes be missing clear sidewalks, pedestrian paths, or bicycling paths.
        /// You must display this warning to the user for all walking, bicycling, and two-wheel routes that you display in your app.
        /// </summary>
        public enum RouteTravelMode
        {
            /// <summary>
            /// Travel by passenger car.
            /// </summary>
            DRIVE,

            /// <summary>
            /// Travel by bicycle.
            /// </summary>
            BICYCLE,

            /// <summary>
            /// Travel by walking.
            /// </summary>
            WALK,

            /// <summary>
            /// Two-wheeled, motorized vehicle. For example, motorcycle.
            /// Note that this differs from the BICYCLE travel mode which covers human-powered mode.
            /// </summary>
            TWO_WHEELER,

            /// <summary>
            /// Travel by public transit routes, where available.
            /// </summary>
            TRANSIT
        }

        /// <summary>
        /// A set of values that specify factors to take into consideration when calculating the route.
        /// </summary>
        public enum RoutingPreference
        {
            /// <summary>
            /// Computes routes without taking live traffic conditions into consideration.
            /// Suitable when traffic conditions don't matter or are not applicable.
            /// Using this value produces the lowest latency.
            /// Note: For RouteTravelMode DRIVE and TWO_WHEELER, the route and duration chosen are based on road network and average time-independent traffic conditions, not current road conditions.
            /// Consequently, routes may include roads that are temporarily closed.
            /// Results for a given request may vary over time due to changes in the road network, updated average traffic conditions, and the distributed nature of the service.
            /// Results may also vary between nearly-equivalent routes at any time or frequency.
            /// </summary>
            TRAFFIC_UNAWARE,

            /// <summary>
            /// Calculates routes taking live traffic conditions into consideration.
            /// In contrast to TRAFFIC_AWARE_OPTIMAL, some optimizations are applied to significantly reduce latency.
            /// </summary>
            TRAFFIC_AWARE,

            /// <summary>
            /// Calculates the routes taking live traffic conditions into consideration, without applying most performance optimizations.
            /// Using this value produces the highest latency.
            /// </summary>
            TRAFFIC_AWARE_OPTIMAL
        }

        /// <summary>
        /// Specifies the assumptions to use when calculating time in traffic. This setting affects the value returned in the duration field in the response, which contains the predicted time in traffic based on historical averages.
        /// </summary>
        public enum TrafficModel
        {
            /// <summary>
            /// Indicates that the returned duration should be the best estimate of travel time given what is known about both historical traffic conditions and live traffic.
            /// Live traffic becomes more important the closer the departureTime is to now.
            /// </summary>
            BEST_GUESS,

            /// <summary>
            /// Indicates that the returned duration should be longer than the actual travel time on most days, though occasional days with particularly bad traffic conditions may exceed this value.
            /// </summary>
            PESSIMISTIC,

            /// <summary>
            /// Indicates that the returned duration should be shorter than the actual travel time on most days, though occasional days with particularly good traffic conditions may be faster than this value.
            /// </summary>
            OPTIMISTIC
        }

        /// <summary>
        /// Specifies routing preferences for transit routes.
        /// </summary>
        public enum TransitRoutingPreference
        {
            /// <summary>
            /// No preference specified.
            /// </summary>
            NONE,

            /// <summary>
            /// Indicates that the calculated route should prefer limited amounts of walking.
            /// </summary>
            LESS_WALKING,

            /// <summary>
            /// Indicates that the calculated route should prefer a limited number of transfers.
            /// </summary>
            FEWER_TRANSFERS
        }

        /// <summary>
        /// A set of values used to specify the mode of transit.
        /// </summary>
        public enum TransitTravelMode
        {
            /// <summary>
            /// Travel by bus.
            /// </summary>
            BUS,

            /// <summary>
            /// Travel by subway.
            /// </summary>
            SUBWAY,

            /// <summary>
            /// Travel by train.
            /// </summary>
            TRAIN,

            /// <summary>
            /// Travel by light rail or tram.
            /// </summary>
            LIGHT_RAIL,

            /// <summary>
            /// Travel by rail. This is equivalent to a combination of SUBWAY, TRAIN, and LIGHT_RAIL.
            /// </summary>
            RAIL
        }

        /// <summary>
        /// A set of values that specify the unit of measure used in the display.
        /// </summary>
        public enum Units
        {
            /// <summary>
            /// Units of measure not specified. Defaults to the unit of measure inferred from the request.
            /// </summary>
            UNSPECIFIED,

            /// <summary>
            /// Specifies that distances are expressed in meters and durations in seconds.
            /// </summary>
            METRIC,

            /// <summary>
            /// Specifies that distances are expressed in feet and miles and durations in seconds.
            /// </summary>
            IMPERIAL
        }

        /// <summary>
        /// A set of values describing the vehicle's emission type. Applies only to the DRIVE RouteTravelMode.
        /// </summary>
        public enum VehicleEmissionType
        {
            /// <summary>
            /// Gasoline/petrol fueled vehicle.
            /// </summary>
            GASOLINE,

            /// <summary>
            /// Electricity powered vehicle.
            /// </summary>
            ELECTRIC,

            /// <summary>
            /// Hybrid fuel (such as gasoline + electric) vehicle.
            /// </summary>
            HYBRID,

            /// <summary>
            /// Diesel fueled vehicle.
            /// </summary>
            DIESEL
        }

        /// <summary>
        /// A set of values that specify the type of the waypoint.
        /// </summary>
        public enum WaypointType
        {
            /// <summary>
            /// A point specified using geographic coordinates, including an optional heading.
            /// </summary>
            LOCATION,

            /// <summary>
            /// The POI Place ID associated with the waypoint.
            /// </summary>
            PLACE_ID,

            /// <summary>
            /// Human readable address or a plus code.
            /// </summary>
            ADDRESS
        }
    }
}