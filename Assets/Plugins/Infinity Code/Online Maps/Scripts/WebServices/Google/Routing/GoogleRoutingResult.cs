namespace OnlineMaps.Webservices
{
    public class GoogleRoutingResult
    {
        /// <summary>
        /// Contains an array of computed routes (up to three) when you specify compute_alternatives_routes, and contains just one route when you don't.
        /// When this array contains multiple entries, the first one is the most recommended route.
        /// If the array is empty, then it means no route could be found.
        /// </summary>
        public Route[] routes;

        /// <summary>
        /// In some cases when the server is not able to compute the route results with all of the input preferences, it may fallback to using a different way of computation.
        /// When fallback mode is used, this field contains detailed info about the fallback response.
        /// Otherwise this field is unset.
        /// </summary>
        public FallbackInfo fallbackInfo;

        /// <summary>
        /// Contains geocoding response info for waypoints specified as addresses.
        /// </summary>
        public GeocodingResults geocodingResults;
        
        public static GoogleRoutingResult Parse(string response)
        {
            return JSON.Deserialize<GoogleRoutingResult>(response);
        }

        /// <summary>
        /// Information related to how and why a fallback result was used.
        /// If this field is set, then it means the server used a different routing mode from your preferred mode as fallback.
        /// </summary>
        public class FallbackInfo
        {
            /// <summary>
            /// Routing mode used for the response.
            /// If fallback was triggered, the mode may be different from routing preference set in the original client request.
            /// </summary>
            public string routingMode;

            /// <summary>
            /// The reason why fallback response was used instead of the original response.
            /// This field is only populated when the fallback mode is triggered and the fallback response is returned.
            /// </summary>
            public string reason;
        }

        /// <summary>
        /// Encapsulates information about flyovers along the polyline.
        /// </summary>
        public class FlyoverInfo
        {
            /// <summary>
            /// Denotes whether a flyover exists for a given stretch of the polyline.
            /// </summary>
            public string flyoverPresence;

            /// <summary>
            /// The location of flyover related information along the polyline.
            /// </summary>
            public PolylinePointIndex polylinePointIndex;
        }

        /// <summary>
        /// Details about the locations used as waypoints.
        /// Only populated for address waypoints.
        /// Includes details about the geocoding results for the purposes of determining what the address was geocoded to.
        /// </summary>
        public class GeocodedWaypoint
        {
            /// <summary>
            /// Indicates the status code resulting from the geocoding operation.
            /// </summary>
            public Status geocoderStatus;

            /// <summary>
            /// The type(s) of the result, in the form of zero or more type tags.
            /// Supported types: Address types and address component types.
            /// https://developers.google.com/maps/documentation/geocoding/requests-geocoding#Types
            /// </summary>
            public string[] type;

            /// <summary>
            /// Indicates that the geocoder did not return an exact match for the original request, though it was able to match part of the requested address.
            /// You may wish to examine the original request for misspellings and/or an incomplete address.
            /// </summary>
            public bool partialMatch;

            /// <summary>
            /// The place ID for this result.
            /// </summary>
            public string placeId;

            /// <summary>
            /// The index of the corresponding intermediate waypoint in the request.
            /// Only populated if the corresponding waypoint is an intermediate waypoint.
            /// </summary>
            public int intermediateWaypointRequestIndex;
        }

        /// <summary>
        /// Contains GeocodedWaypoints for origin, destination and intermediate waypoints.
        /// Only populated for address waypoints.
        /// </summary>
        public class GeocodingResults
        {
            /// <summary>
            /// Contains information about the origin waypoint.
            /// </summary>
            public GeocodedWaypoint origin;

            /// <summary>
            /// Contains information about the destination waypoint.
            /// </summary>
            public GeocodedWaypoint destination;

            /// <summary>
            /// Contains information about the intermediate waypoints.
            /// </summary>
            public GeocodedWaypoint[] intermediates;
        }

        /// <summary>
        /// Localized variant of a text in a particular language.
        /// </summary>
        public class LocalizedText
        {
            /// <summary>
            /// Localized string in the language corresponding to languageCode below.
            /// </summary>
            public string text;

            /// <summary>
            /// The text's BCP-47 language code, such as "en-US" or "sr-Latn".
            /// For more information, see http://www.unicode.org/reports/tr35/#Unicode_locale_identifier.
            /// </summary>
            public string languageCode;
        }

        /// <summary>
        /// Localized description of time.
        /// </summary>
        public class LocalizedTime
        {
            /// <summary>
            /// The time specified as a string in a given time zone.
            /// </summary>
            public LocalizedText time;

            /// <summary>
            /// Contains the time zone. The value is the name of the time zone as defined in the IANA Time Zone Database, e.g. "America/New_York".
            /// </summary>
            public string timeZone;
        }

        /// <summary>
        /// Represents an amount of money with its currency type.
        /// </summary>
        public class Money
        {
            /// <summary>
            /// The three-letter currency code defined in ISO 4217.
            /// </summary>
            public string currencyCode;

            /// <summary>
            /// The whole units of the amount. For example if currencyCode is "USD", then 1 unit is one US dollar.
            /// </summary>
            public string units;

            /// <summary>
            /// Number of nano (10^-9) units of the amount.
            /// The value must be between -999,999,999 and +999,999,999 inclusive.
            /// If units is positive, nanos must be positive or zero.
            /// If units is zero, nanos can be positive, zero, or negative.
            /// If units is negative, nanos must be negative or zero.
            /// For example $-1.75 is represented as units=-1 and nanos=-750,000,000.
            /// </summary>
            public int nanos;
        }

        /// <summary>
        /// Provides summarized information about different multi-modal segments of the RouteLeg.steps.
        /// A multi-modal segment is defined as one or more contiguous RouteLegStep that have the same RouteTravelMode.
        /// This field is not populated if the RouteLeg does not contain any multi-modal segments in the steps.
        /// </summary>
        public class MultiModalSegment
        {
            /// <summary>
            /// NavigationInstruction for the multi-modal segment.
            /// </summary>
            public NavigationInstruction navigationInstruction;

            /// <summary>
            /// The travel mode of the multi-modal segment.
            /// </summary>
            public string travelMode;

            /// <summary>
            /// The corresponding RouteLegStep index that is the start of a multi-modal segment.
            /// </summary>
            public int stepStartIndex;

            /// <summary>
            /// The corresponding RouteLegStep index that is the end of a multi-modal segment.
            /// </summary>
            public int stepEndIndex;
        }

        /// <summary>
        /// Encapsulates information about narrow roads along the polyline.
        /// </summary>
        public class NarrowRoadInfo
        {
            /// <summary>
            /// Denotes whether a narrow road exists for a given stretch of the polyline.
            /// </summary>
            public string narrowRoadPresence;

            /// <summary>
            /// The location of narrow road related information along the polyline.
            /// </summary>
            public PolylinePointIndex polylinePointIndex;
        }

        /// <summary>
        /// Encapsulates navigation instructions for a RouteLegStep.
        /// </summary>
        public class NavigationInstruction
        {
            /// <summary>
            /// Encapsulates the navigation instructions for the current step (for example, turn left, merge, or straight).
            /// This field determines which icon to display.
            /// </summary>
            public string maneuver;

            /// <summary>
            /// Instructions for navigating this step.
            /// </summary>
            public string instructions;
        }

        /// <summary>
        /// Encapsulates an encoded polyline.
        /// </summary>
        public class Polyline
        {
            /// <summary>
            /// The string encoding of the polyline using the polyline encoding algorithm
            /// </summary>
            public string encodedPolyline;

            /// <summary>
            /// Specifies a polyline using the GeoJSON LineString format.
            /// </summary>
            public object geoJsonLinestring;

            private GeoPoint[] _points;

            /// <summary>
            /// The points of the polyline.
            /// </summary>
            public GeoPoint[] points
            {
                get
                {
                    if (_points == null)
                    {
                        _points = Utils.DecodePolylinePoints(encodedPolyline).ToArray();
                    }

                    return _points;
                }
            }
        }

        /// <summary>
        /// Details corresponding to a given index or contiguous segment of a polyline.
        /// Given a polyline with points P_0, P_1, ... , P_N (zero-based index), the PolylineDetails defines an interval and associated metadata.
        /// </summary>
        public class PolylineDetails
        {
            /// <summary>
            /// Flyover details along the polyline.
            /// </summary>
            public FlyoverInfo[] flyoverInfo;

            /// <summary>
            /// Narrow road details along the polyline.
            /// </summary>
            public NarrowRoadInfo[] narrowRoadInfo;
        }

        /// <summary>
        /// Encapsulates the start and end indexes for a polyline detail.
        /// For instances where the data corresponds to a single point, startIndex and endIndex will be equal.
        /// </summary>
        public class PolylinePointIndex
        {
            /// <summary>
            /// The start index of this detail in the polyline.
            /// </summary>
            public int startIndex;

            /// <summary>
            /// The end index of this detail in the polyline.
            /// </summary>
            public int endIndex;
        }

        /// <summary>
        /// Contains a route, which consists of a series of connected road segments that join beginning, ending, and intermediate waypoints.
        /// </summary>
        public class Route
        {
            /// <summary>
            /// Labels for the Route that are useful to identify specific properties of the route to compare against others.
            /// </summary>
            public string[] routeLabels;

            /// <summary>
            /// A collection of legs (path segments between waypoints) that make up the route.
            /// Each leg corresponds to the trip between two non-via Waypoints.
            /// For example, a route with no intermediate waypoints has only one leg.
            /// A route that includes one non-via intermediate waypoint has two legs.
            /// A route that includes one via intermediate waypoint has one leg.
            /// The order of the legs matches the order of waypoints from origin to intermediates to destination.
            /// </summary>
            public RouteLeg[] legs;

            /// <summary>
            /// The travel distance of the route, in meters.
            /// </summary>
            public int distanceMeters;

            /// <summary>
            /// The length of time needed to navigate the route.
            /// If you set the routingPreference to TRAFFIC_UNAWARE, then this value is the same as staticDuration.
            /// If you set the routingPreference to either TRAFFIC_AWARE or TRAFFIC_AWARE_OPTIMAL, then this value is calculated taking traffic conditions into account.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string duration;

            /// <summary>
            /// The duration of travel through the route without taking traffic conditions into consideration.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string staticDuration;

            /// <summary>
            /// The overall route polyline. This polyline is the combined polyline of all legs.
            /// </summary>
            public Polyline polyline;

            /// <summary>
            /// A description of the route.
            /// </summary>
            public string description;

            /// <summary>
            /// An array of warnings to show when displaying the route.
            /// </summary>
            public string[] warnings;

            /// <summary>
            /// The viewport bounding box of the polyline.
            /// </summary>
            public Viewport viewport;

            /// <summary>
            /// Additional information about the route.
            /// </summary>
            public RouteTravelAdvisory travelAdvisory;

            /// <summary>
            /// If you set optimizeWaypointOrder to true, this field contains the optimized ordering of intermediate waypoints.
            /// Otherwise, this field is empty.
            /// For example, if you give an input of Origin: LA;
            /// Intermediate waypoints: Dallas, Bangor, Phoenix;
            /// Destination: New York;
            /// and the optimized intermediate waypoint order is Phoenix, Dallas, Bangor, then this field contains the values [2, 0, 1].
            /// The index starts with 0 for the first intermediate waypoint provided in the input.
            /// </summary>
            public int[] optimizedIntermediateWaypointIndex;

            /// <summary>
            /// Text representations of properties of the Route.
            /// </summary>
            public RouteLocalizedValues localizedValues;

            /// <summary>
            /// An opaque token that can be passed to Navigation SDK to reconstruct the route during navigation, and, in the event of rerouting, honor the original intention when the route was created.
            /// Treat this token as an opaque blob.
            /// Don't compare its value across requests as its value may change even if the service returns the exact same route.
            /// NOTE: Route.route_token is only available for requests that have set ComputeRoutesRequest.routing_preference to TRAFFIC_AWARE or TRAFFIC_AWARE_OPTIMAL.
            /// Route.route_token is not supported for requests that have Via waypoints.
            /// </summary>
            public string routeToken;

            /// <summary>
            /// Contains information about details along the polyline.
            /// </summary>
            public PolylineDetails polylineDetails;

            private int? _durationSec;

            public int durationSec
            {
                get
                {
                    if (!_durationSec.HasValue)
                    {
                        _durationSec = Utils.ParseInt(duration);
                    }

                    return _durationSec.Value;
                }
            }
        }

        /// <summary>
        /// Contains a segment between non-via waypoints.
        /// </summary>
        public class RouteLeg
        {
            /// <summary>
            /// The travel distance of the route leg, in meters.
            /// </summary>
            public int distanceMeters;

            /// <summary>
            /// The length of time needed to navigate the leg.
            /// If the route_preference is set to TRAFFIC_UNAWARE, then this value is the same as staticDuration.
            /// If the route_preference is either TRAFFIC_AWARE or TRAFFIC_AWARE_OPTIMAL, then this value is calculated taking traffic conditions into account.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string duration;

            /// <summary>
            /// The duration of travel through the leg, calculated without taking traffic conditions into consideration.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string staticDuration;

            /// <summary>
            /// The overall polyline for this leg that includes each step's polyline.
            /// </summary>
            public Polyline polyline;

            /// <summary>
            /// The start location of this leg. This location might be different from the provided origin.
            /// For example, when the provided origin is not near a road, this is a point on the road.
            /// </summary>
            public GoogleRoutingRequest.Location startLocation;

            /// <summary>
            /// The end location of this leg. This location might be different from the provided destination.
            /// For example, when the provided destination is not near a road, this is a point on the road.
            /// </summary>
            public GoogleRoutingRequest.Location endLocation;

            /// <summary>
            /// An array of steps denoting segments within this leg. Each step represents one navigation instruction.
            /// </summary>
            public RouteLegStep[] steps;

            /// <summary>
            /// Contains the additional information that the user should be informed about, such as possible traffic zone restrictions, on a route leg.
            /// </summary>
            public RouteLegTravelAdvisory travelAdvisory;

            /// <summary>
            /// Text representations of properties of the RouteLeg.
            /// </summary>
            public RouteLegLocalizedValues localizedValues;

            /// <summary>
            /// Overview information about the steps in this RouteLeg. This field is only populated for TRANSIT routes.
            /// </summary>
            public StepsOverview stepsOverview;
        }

        /// <summary>
        /// Text representations of certain properties.
        /// </summary>
        public class RouteLegLocalizedValues
        {
            /// <summary>
            /// Travel distance represented in text form.
            /// </summary>
            public LocalizedText distance;

            /// <summary>
            /// Duration without taking traffic conditions into consideration, represented in text form.
            /// </summary>
            public LocalizedText staticDuration;
        }

        /// <summary>
        /// Contains a segment of a RouteLeg.
        /// A step corresponds to a single navigation instruction.
        /// Route legs are made up of steps.
        /// </summary>
        public class RouteLegStep
        {
            /// <summary>
            /// The travel distance of this step, in meters.
            /// In some circumstances, this field might not have a value.
            /// </summary>
            public int distanceMeters;

            /// <summary>
            /// The duration of travel through this step without taking traffic conditions into consideration.
            /// In some circumstances, this field might not have a value.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string staticDuration;

            /// <summary>
            /// The polyline associated with this step.
            /// </summary>
            public Polyline polyline;

            /// <summary>
            /// The start location of this step.
            /// </summary>
            public GoogleRoutingRequest.Location startLocation;

            /// <summary>
            /// The end location of this step.
            /// </summary>
            public GoogleRoutingRequest.Location endLocation;

            /// <summary>
            /// Navigation instructions.
            /// </summary>
            public NavigationInstruction navigationInstruction;

            /// <summary>
            /// Contains the additional information that the user should be informed about, such as possible traffic zone restrictions, on a leg step.
            /// </summary>
            public RouteLegTravelAdvisory travelAdvisory;

            /// <summary>
            /// Text representations of properties of the RouteLegStep.
            /// </summary>
            public RouteLegLocalizedValues localizedValues;

            /// <summary>
            /// Details pertaining to this step if the travel mode is TRANSIT.
            /// </summary>
            public RouteLegStepTransitDetails transitDetails;

            /// <summary>
            /// The travel mode used for this step.
            /// </summary>
            public string travelMode;

            private int? _durationSec;

            /// <summary>
            /// The duration of travel through this step without taking traffic conditions into consideration.
            /// </summary>
            public int durationSec
            {
                get
                {
                    if (!_durationSec.HasValue)
                    {
                        _durationSec = Utils.ParseInt(staticDuration);
                    }

                    return _durationSec.Value;
                }
            }
        }

        /// <summary>
        /// Additional information for the RouteLegStep related to TRANSIT routes.
        /// </summary>
        public class RouteLegStepTransitDetails
        {
            /// <summary>
            /// Information about the arrival and departure stops for the step.
            /// </summary>
            public TransitStopDetails stopDetails;

            /// <summary>
            /// Text representations of properties of the RouteLegStepTransitDetails.
            /// </summary>
            public TransitDetailsLocalizedValues localizedValues;

            /// <summary>
            /// Specifies the direction in which to travel on this line as marked on the vehicle or at the departure stop.
            /// The direction is often the terminus station.
            /// </summary>
            public string headsign;

            /// <summary>
            /// Specifies the expected time as a duration between departures from the same stop at this time.
            /// For example, with a headway seconds value of 600, you would expect a ten minute wait if you should miss your bus.
            /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
            /// </summary>
            public string headway;

            /// <summary>
            /// Information about the transit line used in this step.
            /// </summary>
            public TransitLine transitLine;

            /// <summary>
            /// The number of stops from the departure to the arrival stop.
            /// This count includes the arrival stop, but excludes the departure stop.
            /// For example, if your route leaves from Stop A, passes through stops B and C, and arrives at stop D, stopCount returns 3.
            /// </summary>
            public int stopCount;

            /// <summary>
            /// The text that appears in schedules and sign boards to identify a transit trip to passengers.
            /// The text should uniquely identify a trip within a service day.
            /// For example, "538" is the tripShortText of the Amtrak train that leaves San Jose, CA at 15:10 on weekdays to Sacramento, CA.
            /// </summary>
            public string tripShortText;
        }

        /// <summary>
        /// Contains the additional information that the user should be informed about, such as possible traffic zone restrictions on a leg step.
        /// </summary>
        public class RouteLegTravelAdvisory
        {
            /// <summary>
            /// NOTE: This field is not currently populated.
            /// </summary>
            public SpeedReadingInterval[] speedReadingIntervals;
        }

        /// <summary>
        /// Text representations of certain properties.
        /// </summary>
        public class RouteLocalizedValues
        {
            /// <summary>
            /// Travel distance represented in text form.
            /// </summary>
            public LocalizedText distance;

            /// <summary>
            /// Duration, represented in text form and localized to the region of the query.
            /// Takes traffic conditions into consideration.
            /// Note: If you did not request traffic information, this value is the same value as staticDuration.
            /// </summary>
            public LocalizedText duration;

            /// <summary>
            /// Duration without taking traffic conditions into consideration, represented in text form.
            /// </summary>
            public LocalizedText staticDuration;

            /// <summary>
            /// Transit fare represented in text form.
            /// </summary>
            public LocalizedText transitFare;
        }

        /// <summary>
        /// Contains the additional information that the user should be informed about, such as possible traffic zone restrictions.
        /// </summary>
        public class RouteTravelAdvisory
        {
            /// <summary>
            /// Contains information about tolls on the route.
            /// This field is only populated if tolls are expected on the route.
            /// If this field is set, but the estimatedPrice subfield is not populated, then the route contains tolls, but the estimated price is unknown.
            /// If this field is not set, then there are no tolls expected on the route.
            /// </summary>
            public TollInfo tollInfo;

            /// <summary>
            /// Speed reading intervals detailing traffic density.
            /// Applicable in case of TRAFFIC_AWARE and TRAFFIC_AWARE_OPTIMAL routing preferences.
            /// The intervals cover the entire polyline of the route without overlap.
            /// The start point of a specified interval is the same as the end point of the preceding interval.
            /// </summary>
            public SpeedReadingInterval[] speedReadingIntervals;

            /// <summary>
            /// The predicted fuel consumption in microliters.
            /// </summary>
            public string fuelConsumptionMicroliters;

            /// <summary>
            /// Returned route may have restrictions that are not suitable for requested travel mode or route modifiers.
            /// </summary>
            public bool routeRestrictionsPartiallyIgnored;

            /// <summary>
            /// If present, contains the total fare or ticket costs on this route This property is only returned for TRANSIT requests and only for routes where fare information is available for all transit steps.
            /// </summary>
            public Money transitFare;
        }

        /// <summary>
        /// Traffic density indicator on a contiguous segment of a polyline or path.
        /// Given a path with points P_0, P_1, ... , P_N (zero-based index), the SpeedReadingInterval defines an interval and describes its traffic using the following categories.
        /// </summary>
        public class SpeedReadingInterval
        {
            /// <summary>
            /// The starting index of this interval in the polyline.
            /// </summary>
            public int startPolylinePointIndex;

            /// <summary>
            /// The ending index of this interval in the polyline.
            /// </summary>
            public int endPolylinePointIndex;

            /// <summary>
            /// Traffic speed in this interval.
            /// </summary>
            public string speed;
        }

        /// <summary>
        /// The Status type defines a logical error model that is suitable for different programming environments, including REST APIs and RPC APIs.
        /// It is used by gRPC. Each Status message contains three pieces of data: error code, error message, and error details.
        /// </summary>
        public class Status
        {
            /// <summary>
            /// The status code, which should be an enum value of google.rpc.Code.
            /// </summary>
            public int code;

            /// <summary>
            /// A developer-facing error message, which should be in English.
            /// Any user-facing error message should be localized and sent in the google.rpc.Status.details field, or localized by the client.
            /// </summary>
            public string message;

            /// <summary>
            /// A list of messages that carry the error details.
            /// There is a common set of message types for APIs to use.
            /// An object containing fields of an arbitrary type.
            /// An additional field "@type" contains a URI identifying the type. 
            /// </summary>
            public object[] details;
        }

        /// <summary>
        /// Provides overview information about a list of RouteLegSteps.
        /// </summary>
        public class StepsOverview
        {
            /// <summary>
            /// Summarized information about different multi-modal segments of the RouteLeg.steps.
            /// This field is not populated if the RouteLeg does not contain any multi-modal segments in the steps.
            /// </summary>
            public MultiModalSegment[] multiModalSegments;
        }

        /// <summary>
        /// Encapsulates toll information on a Route or on a RouteLeg.
        /// </summary>
        public class TollInfo
        {
            /// <summary>
            /// The monetary amount of tolls for the corresponding Route or RouteLeg.
            /// This list contains a money amount for each currency that is expected to be charged by the toll stations.
            /// Typically this list will contain only one item for routes with tolls in one currency.
            /// For international trips, this list may contain multiple items to reflect tolls in different currencies.
            /// </summary>
            public Money[] estimatedPrice;
        }

        /// <summary>
        /// Localized descriptions of values for RouteTransitDetails.
        /// </summary>
        public class TransitDetailsLocalizedValues
        {
            /// <summary>
            /// Time in its formatted text representation with a corresponding time zone.
            /// </summary>
            public LocalizedTime arrivalTime;

            /// <summary>
            /// Time in its formatted text representation with a corresponding time zone.
            /// </summary>
            public LocalizedTime departureTime;
        }

        /// <summary>
        /// Contains information about the transit line used in this step.
        /// </summary>
        public class TransitLine
        {
            /// <summary>
            /// The time specified as a string in a given time zone.
            /// </summary>
            public LocalizedText time;

            /// <summary>
            /// Contains the time zone. The value is the name of the time zone as defined in the IANA Time Zone Database, e.g. "America/New_York".
            /// </summary>
            public string timeZone;
        }

        /// <summary>
        /// Information about a transit stop.
        /// </summary>
        public class TransitStop
        {
            /// <summary>
            /// The name of the transit stop.
            /// </summary>
            public string name;

            /// <summary>
            /// The location of the stop expressed in latitude/longitude coordinates.
            /// </summary>
            public GoogleRoutingRequest.Location location;
        }

        /// <summary>
        /// Details about the transit stops for the RouteLegStep.
        /// </summary>
        public class TransitStopDetails
        {
            /// <summary>
            /// Information about the arrival stop for the step.
            /// </summary>
            public TransitStop arrivalStop;

            /// <summary>
            /// The estimated time of arrival for the step.
            /// A timestamp in RFC3339 UTC "Zulu" format, with nanosecond resolution and up to nine fractional digits.
            /// Examples: "2014-10-02T15:01:23Z" and "2014-10-02T15:01:23.045123456Z".
            /// </summary>
            public string arrivalTime;

            /// <summary>
            /// Information about the departure stop for the step.
            /// </summary>
            public TransitStop departureStop;

            /// <summary>
            /// The estimated time of departure for the step.
            /// A timestamp in RFC3339 UTC "Zulu" format, with nanosecond resolution and up to nine fractional digits.
            /// Examples: "2014-10-02T15:01:23Z" and "2014-10-02T15:01:23.045123456Z".
            /// </summary>
            public string departureTime;
        }

        /// <summary>
        /// A latitude-longitude viewport, represented as two diagonally opposite low and high points.
        /// A viewport is considered a closed region, i.e. it includes its boundary.
        /// The latitude bounds must range between -90 to 90 degrees inclusive, and the longitude bounds must range between -180 to 180 degrees inclusive.
        /// </summary>
        public class Viewport
        {
            /// <summary>
            /// The low point of the viewport.
            /// </summary>
            public GoogleRoutingRequest.LatLng low;

            /// <summary>
            /// The high point of the viewport.
            /// </summary>
            public GoogleRoutingRequest.LatLng high;
        }
    }
}