/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Returns a route between two or more locations using Open Route Service Directions for a selected profile and its settings as JSON.<br/>
    /// https://openrouteservice.org/dev/#/api-docs/v2/directions/{profile}/post
    /// </summary>
    public class ORSDirectionsRequest : TextWebService<ORSDirectionsResult>
    {
        private const string endpoint = "https://api.openrouteservice.org/v2/directions/";

        /// <summary>
        /// Open Route Service API key. If empty, the key from Key Manager will be used
        /// </summary>
        public string apiKey;

        /// <summary>
        /// Specifies the route profile
        /// </summary>
        public Profile profile = Profile.drivingCar;

        /// <summary>
        /// Specifies whether alternative routes are computed, and parameters for the algorithm determining suitable alternatives.
        /// </summary>
        public AlternativeRoutes alternativeRoutes;

        /// <summary>
        /// List of route attributes
        /// </summary>
        public string[] attributes;

        /// <summary>
        /// Specifies whether to return elevation values for points. Please note that elevation also gets encoded for json response encoded polyline.
        /// </summary>
        public bool elevation;

        /// <summary>
        /// The extra info items to include in the response
        /// </summary>
        public string[] extra_info;

        /// <summary>
        /// Specifies whether to simplify the geometry. Simplify geometry cannot be applied to routes with more than one segment and when extra_info is required.
        /// </summary>
        public bool geometry_simplify;

        /// <summary>
        /// Arbitrary identification string of the request reflected in the meta information.
        /// </summary>
        public string id;

        /// <summary>
        /// Specifies whether to return instructions.
        /// </summary>
        public bool instructions = true;

        /// <summary>
        /// Select html for more verbose instructions.
        /// </summary>
        public string instructions_format;

        /// <summary>
        /// Language for the route instructions.
        /// </summary>
        public string language;

        /// <summary>
        /// Specifies whether the maneuver object is included into the step object or not.
        /// </summary>
        public bool maneuvers;

        /// <summary>
        /// Advanced options.
        /// </summary>
        public Options options;

        /// <summary>
        /// Specifies the route preference.
        /// </summary>
        public string preference;

        /// <summary>
        /// A pipe list of maximum distances (measured in metres) that limit the search of nearby road segments to every given waypoint. The values must be greater than 0, the value of -1 specifies no limit in the search. The number of radiuses correspond to the number of waypoints.
        /// </summary>
        public int[] radiuses;

        /// <summary>
        /// Provides bearings of the entrance and all passed roundabout exits. 
        /// </summary>
        public bool roundabout_exits;

        /// <summary>
        /// Specifies the segments that should be skipped in the route calculation. A segment is the connection between two given coordinates and the counting starts with 1 for the connection between the first and second coordinate.
        /// </summary>
        public int[] skip_segments;

        /// <summary>
        /// Suppress warning messages in the response
        /// </summary>
        public bool suppress_warnings;

        /// <summary>
        /// Specifies the distance unit.
        /// </summary>
        public string units;

        /// <summary>
        /// Specifies whether to return geometry.
        /// </summary>
        public bool geometry = true;

        /// <summary>
        /// The maximum speed specified by user.
        /// </summary>
        public int? maximum_speed;

        public GeoPoint[] via;

        public readonly GeoPoint origin;
        public readonly GeoPoint destination;

        public ORSDirectionsRequest(GeoPoint origin, GeoPoint destination)
        {
            this.origin = origin;
            this.destination = destination;
        }

        private void AppendAttributes(StringBuilder builder)
        {
            if (attributes == null || attributes.Length == 0) return;
            builder.Append(",\"attributes\":[");
            for (int i = 0; i < attributes.Length; i++)
            {
                if (i > 0) builder.Append(",");
                builder.Append(attributes[i]);
            }

            builder.Append("]");
        }

        private void AppendExtraInfo(StringBuilder builder)
        {
            if (extra_info == null || extra_info.Length == 0) return;
            
            builder.Append(",\"extra_info\":[");
            for (int i = 0; i < extra_info.Length; i++)
            {
                if (i > 0) builder.Append(",");
                builder.Append(extra_info[i]);
            }

            builder.Append("]");
        }

        private void AppendLocations(StringBuilder postBuilder)
        {
            GeoPoint[] locations;
            if (via == null)
            {
                locations = new[] {origin, destination};
            }
            else
            {
                locations = new GeoPoint[via.Length + 2];
                locations[0] = origin;
                for (int i = 0; i < via.Length; i++) locations[i + 1] = via[i];
                locations[locations.Length - 1] = destination;
            }
            
            for (int i = 0; i < locations.Length; i++)
            {
                if (i > 0) postBuilder.Append(",");
                postBuilder.Append("[");
                AppendLocation(postBuilder, locations[i], true);
                postBuilder.Append("]");
            }

            postBuilder.Append("]");
        }

        public void AppendPost(StringBuilder builder)
        {
            if (alternativeRoutes != null) alternativeRoutes.AppendPost(builder);
            AppendAttributes(builder);
            if (elevation) builder.Append(",\"elevation\":\"true\"");
            AppendExtraInfo(builder);

            if (geometry_simplify) builder.Append(",\"geometry_simplify\":\"true\"");
            if (!string.IsNullOrEmpty(id)) builder.Append(",\"id\":\"").Append(id).Append("\"");
            if (!instructions) builder.Append(",\"instructions\":\"false\"");
            if (!string.IsNullOrEmpty(instructions_format)) builder.Append(",\"instructions_format\":\"").Append(instructions_format).Append("\"");
            if (!string.IsNullOrEmpty(language)) builder.Append(",\"language\":\"").Append(language).Append("\"");
            if (maneuvers) builder.Append(",\"maneuvers\":\"true\"");
            if (options != null) options.AppendPost(builder);
            if (!string.IsNullOrEmpty(preference)) builder.Append(",\"preference\":\"").Append(preference).Append("\"");

            AppendRadiuses(builder);

            if (roundabout_exits) builder.Append(",\"roundabout_exits\":\"true\"");

            AppendSkipSegments(builder);

            if (suppress_warnings) builder.Append(",\"suppress_warnings\":\"true\"");
            if (!string.IsNullOrEmpty(units)) builder.Append(",\"units\":\"").Append(units).Append("\"");
            if (!geometry) builder.Append(",\"geometry\":\"false\"");
            if (maximum_speed.HasValue) builder.Append(",\"maximum_speed\":").Append(maximum_speed.Value);
        }

        private void AppendSkipSegments(StringBuilder builder)
        {
            if (skip_segments != null && skip_segments.Length > 0)
            {
                builder.Append(",\"skip_segments\":[");
                for (int i = 0; i < skip_segments.Length; i++)
                {
                    if (i > 0) builder.Append(",");
                    builder.Append(skip_segments[i]);
                }

                builder.Append("]");
            }
        }

        private void AppendRadiuses(StringBuilder builder)
        {
            if (radiuses == null || radiuses.Length == 0) return;
            
            builder.Append(",\"radiuses\":[");
            for (int i = 0; i < radiuses.Length; i++)
            {
                if (i > 0) builder.Append(",");
                builder.Append(radiuses[i]);
            }

            builder.Append("]");
        }

        protected override void CreateRequest()
        {
            www = new WebRequest(url, GetPostData(), GetHeaders());
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            builder.Append(endpoint);
            builder.Append(ReflectionHelper.GetEnumDescription(profile));
        }

        private Dictionary<string, string> GetHeaders()
        {
            if (string.IsNullOrEmpty(apiKey)) apiKey = KeyManager.OpenRouteService();

            return new Dictionary<string, string>
            {
                {"Authorization", apiKey },
                {"Content-Type", "application/json"}
            };
        }

        private string GetPostData()
        {
            StringBuilder builder = StaticStringBuilder.Start();
            builder.Append("{\"coordinates\":[");
            AppendLocations(builder);
            AppendPost(builder);
            builder.Append("}");
            return builder.ToString();
        }

        public static ORSDirectionsResult Parse(string response) => ORSDirectionsResult.Parse(response);
        public override ORSDirectionsResult ParseResult(string response) => Parse(response);

        /// <summary>
        /// Specifies the route profile
        /// </summary>
        public enum Profile
        {
            [Description("driving-car")]
            drivingCar,
            [Description("driving-hgv")]
            drivingHgv,
            [Description("driving-regular")]
            cyclingRegular,
            [Description("cycling-road")]
            cyclingRoad,
            [Description("cycling-save")]
            cyclingSave,
            [Description("cycling-mountain")]
            cyclingMountain,
            [Description("cycling-tour")]
            cyclingTour,
            [Description("cycling-electric")]
            cyclingElectric,
            [Description("foot-walking")]
            footWalking,
            [Description("foot-hiking")]
            footHiking,
            [Description("wheelchair")]
            wheelchair
        }

        /// <summary>
        /// Specifies whether alternative routes are computed, and parameters for the algorithm determining suitable alternatives.
        /// </summary>
        public class AlternativeRoutes
        {
            /// <summary>
            /// Maximum fraction of the route that alternatives may share with the optimal route. The default value of 0.6 means alternatives can share up to 60% of path segments with the optimal route.
            /// </summary>
            public float share_factor = 0.6f;

            /// <summary>
            /// Target number of alternative routes to compute. Service returns up to this number of routes that fulfill the share-factor and weight-factor constraints.
            /// </summary>
            public int target_count = 2;

            /// <summary>
            /// Maximum factor by which route weight may diverge from the optimal route. The default value of 1.4 means alternatives can be up to 1.4 times longer (costly) than the optimal route.
            /// </summary>
            public float weight_factor = 1.4f;

            public void AppendPost(StringBuilder builder)
            {
                builder.Append(",\"alternative_routes\":{");
                builder.Append("\"share_factor\":").Append(share_factor.ToString(Culture.numberFormat));
                builder.Append(",\"target_count\":").Append(target_count);
                builder.Append(",\"weight_factor\":").Append(weight_factor.ToString(Culture.numberFormat));
                builder.Append("}");
            }
        }

        /// <summary>
        /// Advanced options.
        /// </summary>
        public class Options
        {
            /// <summary>
            /// all for no border crossing. controlled to cross open borders but avoid controlled ones. Only for driving-* profiles.
            /// </summary>
            public string avoid_borders;

            /// <summary>
            /// List of countries to exclude from routing with driving-* profiles. Can be used together with 'avoid_borders': 'controlled'. [ 11, 193 ] would exclude Austria and Switzerland.
            /// </summary>
            public int[] avoid_countries;

            /// <summary>
            /// List of features to avoid.
            /// </summary>
            public string[] avoid_features;

            /// <summary>
            /// Comprises areas to be avoided for the route. Formatted in GeoJSON as either a Polygon or Multipolygon object.
            /// </summary>
            public string avoid_polygons;

            /// <summary>
            /// Options to be applied on round trip routes.
            /// </summary>
            public RoundTrip round_trip;

            public void AppendPost(StringBuilder builder)
            {
                builder.Append(",\"options\":{");
                bool hasValue = false;
                if (!string.IsNullOrEmpty(avoid_borders))
                {
                    hasValue = true;
                    builder.Append("\"avoid_borders\":\"").Append(avoid_borders).Append("\"");
                }

                if (avoid_countries != null && avoid_countries.Length > 0)
                {
                    if (hasValue) builder.Append(",");
                    hasValue = true;
                    builder.Append("\"avoid_countries\":[");
                    for (int i = 0; i < avoid_countries.Length; i++)
                    {
                        if (i > 0) builder.Append(",");
                        builder.Append(avoid_countries[i]);
                    }

                    builder.Append("]");
                }

                if (avoid_features != null && avoid_features.Length > 0)
                {
                    if (hasValue) builder.Append(",");
                    hasValue = true;
                    builder.Append("\"avoid_features\":[");
                    for (int i = 0; i < avoid_features.Length; i++)
                    {
                        if (i > 0) builder.Append(",");
                        builder.Append("\"").Append(avoid_features[i]).Append("\"");
                    }

                    builder.Append("]");
                }

                if (!string.IsNullOrEmpty(avoid_polygons))
                {
                    if (hasValue) builder.Append(",");
                    hasValue = true;
                    builder.Append("\"avoid_polygons\":\"").Append(avoid_polygons).Append("\"");
                }

                if (round_trip != null) round_trip.AppendPost(builder);

                builder.Append("}");
            }
        }
        /// <summary>
        /// Options to be applied on round trip routes.
        /// </summary>
        public class RoundTrip
        {
            /// <summary>
            /// The target length of the route in m (note that this is a preferred value, but results may be different).
            /// </summary>
            public float length = 10000;

            /// <summary>
            /// The number of points to use on the route. Larger values create more circular routes.
            /// </summary>
            public int points = 5;

            /// <summary>
            /// A seed to use for adding randomisation to the overall direction of the generated route
            /// </summary>
            public int seed = 1;

            public void AppendPost(StringBuilder builder)
            {
                builder.Append(",\"round_trip\":{");
                builder.Append("\"length\":").Append(length.ToString(Culture.numberFormat));
                builder.Append(",\"points\":").Append(points);
                builder.Append(",\"seed\":").Append(seed);
                builder.Append("}");
            }
        }
    }
}