/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Result of HERE Routing API query.
    /// https://developer.here.com/documentation/routing-api/api-reference-swagger.html
    /// </summary>
    public class HereRoutingResult
    {
        /// <summary>
        /// Array of results
        /// </summary>
        public Route[] routes;

        /// <summary>
        /// Reference to JSON response object
        /// </summary>
        public JSONItem json;
        
        public static HereRoutingResult Parse(string response)
        {
            try
            {
                JSONItem json = JSON.Parse(response);
                HereRoutingResult result = json.Deserialize<HereRoutingResult>();
                result.json = json;
                return result;
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message + "\n" + exception.StackTrace);
            }
            return null;
        }

        /// <summary>
        /// Represents a route in the HERE Routing API result.
        /// </summary>
        public class Route
        {
            /// <summary>
            /// The unique identifier of the route.
            /// </summary>
            public string id;

            /// <summary>
            /// The sections that make up the route.
            /// </summary>
            public Section[] sections;
        }

        /// <summary>
        /// Represents a section of the route.
        /// </summary>
        public class Section
        {
            /// <summary>
            /// The unique identifier of the section.
            /// </summary>
            public string id;

            /// <summary>
            /// The type of the section.
            /// </summary>
            public string type;

            /// <summary>
            /// The actions to be taken in this section.
            /// </summary>
            public Action[] actions;

            /// <summary>
            /// The departure time and place of the section.
            /// </summary>
            public TimeWithAny departure;

            /// <summary>
            /// The arrival time and place of the section.
            /// </summary>
            public TimeWithAny arrival;

            /// <summary>
            /// The summary of the section.
            /// </summary>
            public Summary summary;

            /// <summary>
            /// The polyline representing the section.
            /// </summary>
            public string polyline;

            /// <summary>
            /// The spans of the section.
            /// </summary>
            public Span[] spans;

            /// <summary>
            /// The transport mode of the section.
            /// </summary>
            public Transport transport;

            private List<GeoPoint3> _polylinePoints;
            private List<Vector2d> _polylinePoints2d;

            /// <summary>
            /// The decoded polyline points of the section.
            /// </summary>
            public List<GeoPoint3> polylinePoints
            {
                get
                {
                    if (_polylinePoints == null)
                    {
                        if (!string.IsNullOrEmpty(polyline)) _polylinePoints = PolylineEncoderDecoder.Decode(polyline);
                    }

                    return _polylinePoints;
                }
            }
        }

        /// <summary>
        /// Represents an action to be taken in a section of the route.
        /// </summary>
        public class Action
        {
            /// <summary>
            /// The type of action.
            /// </summary>
            public string action;

            /// <summary>
            /// The duration of the action in seconds.
            /// </summary>
            public int duration;

            /// <summary>
            /// The instruction text for the action.
            /// </summary>
            public string instruction;

            /// <summary>
            /// The offset of the action in the section.
            /// </summary>
            public int offset;
        }

        /// <summary>
        /// Represents a time and place.
        /// </summary>
        public class TimeWithAny
        {
            /// <summary>
            /// The time.
            /// </summary>
            public string time;

            /// <summary>
            /// The place.
            /// </summary>
            public Place place;
        }

        /// <summary>
        /// Represents a place with a type and location.
        /// </summary>
        public class Place
        {
            /// <summary>
            /// The type of the place.
            /// </summary>
            public string type;

            /// <summary>
            /// The location of the place.
            /// </summary>
            public LatLng location;
        }

        /// <summary>
        /// Represents a geographical location with latitude and longitude.
        /// </summary>
        public class LatLng
        {
            /// <summary>
            /// The latitude of the location.
            /// </summary>
            public double lat;

            /// <summary>
            /// The longitude of the location.
            /// </summary>
            public double lng;
        }

        /// <summary>
        /// Represents a summary of a section.
        /// </summary>
        public class Summary
        {
            /// <summary>
            /// The duration of the section in seconds.
            /// </summary>
            public int duration;

            /// <summary>
            /// The length of the section in meters.
            /// </summary>
            public int length;
        }

        /// <summary>
        /// Represents a span of a section.
        /// </summary>
        public class Span
        {
            /// <summary>
            /// The offset of the span in the section.
            /// </summary>
            public int offset;

            /// <summary>
            /// The names associated with the span.
            /// </summary>
            public Name[] names;

            /// <summary>
            /// The length of the span in meters.
            /// </summary>
            public int length;
        }

        /// <summary>
        /// Represents a name with a value and language.
        /// </summary>
        public class Name
        {
            /// <summary>
            /// The value of the name.
            /// </summary>
            public string value;

            /// <summary>
            /// The language of the name.
            /// </summary>
            public string language;
        }

        /// <summary>
        /// Represents the transport mode of a section.
        /// </summary>
        public class Transport
        {
            /// <summary>
            /// The mode of transport.
            /// </summary>
            public string mode;
        }

        #region Flexible Polyline encoder / decoder

        /*https://github.com/heremaps/flexible-polyline*/

        /// <summary>
        /// Represents the third dimension type in the flexible polyline encoding.
        /// </summary>
        public enum ThirdDimension
        {
            /// <summary>
            /// No third dimension.
            /// </summary>
            Absent = 0,

            /// <summary>
            /// Level dimension.
            /// </summary>
            Level = 1,

            /// <summary>
            /// Altitude dimension.
            /// </summary>
            Altitude = 2,

            /// <summary>
            /// Elevation dimension.
            /// </summary>
            Elevation = 3,

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            Reserved1 = 4,

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            Reserved2 = 5,

            /// <summary>
            /// Custom dimension 1.
            /// </summary>
            Custom1 = 6,

            /// <summary>
            /// Custom dimension 2.
            /// </summary>
            Custom2 = 7
        }

        /// <summary>
        /// Provides methods to encode and decode polylines.
        /// </summary>
        public class PolylineEncoderDecoder
        {
            /// <summary>Header version
            /// A change in the version may affect the logic to encode and decode the rest of the header and data
            /// </summary>
            private const byte FORMAT_VERSION = 1;

            // Base64 URL-safe characters
            private static readonly char[] ENCODING_TABLE =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

            private static readonly int[] DECODING_TABLE =
            {
                62, -1, -1, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1, -1,
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21,
                22, 23, 24, 25, -1, -1, -1, -1, 63, -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35,
                36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51
            };

            /// <summary>
            /// Encode the list of coordinate triples.
            /// The third dimension value will be eligible for encoding only when ThirdDimension is other than ABSENT.
            /// This is lossy compression based on precision accuracy.
            /// </summary>
            /// <param name="locations">coordinates {@link List} of coordinate triples that to be encoded.</param>
            /// <param name="precision">Floating point precision of the coordinate to be encoded.</param>
            /// <param name="thirdDimension">{@link ThirdDimension} which may be a level, altitude, elevation or some other custom value</param>
            /// <param name="thirdDimPrecision">Floating point precision for thirdDimension value</param>
            /// <returns>URL-safe encoded {@link String} for the given coordinates.</returns>
            public static string Encode(List<GeoPoint3> locations, int precision, ThirdDimension thirdDimension, int thirdDimPrecision)
            {
                if (locations == null || locations.Count == 0)
                {
                    throw new ArgumentException("Invalid coordinates!");
                }

                if (!Enum.IsDefined(typeof(ThirdDimension), thirdDimension))
                {
                    throw new ArgumentException("Invalid thirdDimension");
                }

                Encoder enc = new Encoder(precision, thirdDimension, thirdDimPrecision);
                foreach (GeoPoint3 location in locations)
                {
                    enc.Add(location);
                }

                return enc.GetEncoded();
            }

            /// <summary>
            /// Decode the encoded input {@link String} to {@link List} of coordinate triples.
            /// @see PolylineDecoder#getThirdDimension(String) getThirdDimension
            /// @see LatLngZ
            /// </summary>
            /// <param name="encoded">encoded URL-safe encoded {@link String}</param>
            /// <returns>{@link List} of coordinate triples that are decoded from input</returns>
            public static List<GeoPoint3> Decode(string encoded)
            {
                if (encoded == null || string.IsNullOrEmpty(encoded.Trim()))
                {
                    throw new ArgumentException("Invalid argument!", "encoded");
                }

                List<GeoPoint3> result = new List<GeoPoint3>();
                Decoder dec = new Decoder(encoded);

                double lat = 0;
                double lng = 0;
                double z = 0;

                while (dec.DecodeOne(ref lat, ref lng, ref z))
                {
                    result.Add(new GeoPoint3(lng, lat, z));
                    lat = 0;
                    lng = 0;
                    z = 0;
                }

                return result;
            }

            /// <summary>
            /// ThirdDimension type from the encoded input {@link String}
            /// </summary>
            /// <param name="encoded">URL-safe encoded coordinate triples {@link String}</param>
            public static ThirdDimension GetThirdDimension(string encoded)
            {
                int index = 0;
                long header = 0;
                Decoder.DecodeHeaderFromString(encoded.ToCharArray(), ref index, ref header);
                return (ThirdDimension)((header >> 4) & 7);
            }

            /// <summary>
            /// Gets the format version of the polyline encoder.
            /// </summary>
            /// <returns>The format version as a byte.</returns>
            public byte GetVersion()
            {
                return FORMAT_VERSION;
            }

            /// <summary>
            /// Internal class for configuration, validation and encoding for an input request.
            /// </summary>
            private class Encoder
            {
                private readonly StringBuilder _result;
                private readonly Converter _latConverter;
                private readonly Converter _lngConverter;
                private readonly Converter _zConverter;
                private readonly ThirdDimension _thirdDimension;

                /// <summary>
                /// Initializes a new instance of the Encoder class with the specified precision, third dimension, and third dimension precision.
                /// </summary>
                /// <param name="precision">The precision for the latitude and longitude values.</param>
                /// <param name="thirdDimension">The third dimension type (e.g., altitude, elevation).</param>
                /// <param name="thirdDimPrecision">The precision for the third dimension values.</param>
                public Encoder(int precision, ThirdDimension thirdDimension, int thirdDimPrecision)
                {
                    _latConverter = new Converter(precision);
                    _lngConverter = new Converter(precision);
                    _zConverter = new Converter(thirdDimPrecision);
                    _thirdDimension = thirdDimension;
                    _result = new StringBuilder();
                    EncodeHeader(precision, (int)_thirdDimension, thirdDimPrecision);
                }

                private void EncodeHeader(int precision, int thirdDimensionValue, int thirdDimPrecision)
                {
                    // Encode the `precision`, `third_dim` and `third_dim_precision` into one encoded char
                    if (precision < 0 || precision > 15)
                    {
                        throw new ArgumentException("precision out of range");
                    }

                    if (thirdDimPrecision < 0 || thirdDimPrecision > 15)
                    {
                        throw new ArgumentException("thirdDimPrecision out of range");
                    }

                    if (thirdDimensionValue < 0 || thirdDimensionValue > 7)
                    {
                        throw new ArgumentException("thirdDimensionValue out of range");
                    }

                    long res = (thirdDimPrecision << 7) | (thirdDimensionValue << 4) | precision;
                    Converter.EncodeUnsignedVarint(FORMAT_VERSION, _result);
                    Converter.EncodeUnsignedVarint(res, _result);
                }

                private void Add(double lat, double lng)
                {
                    _latConverter.EncodeValue(lat, _result);
                    _lngConverter.EncodeValue(lng, _result);
                }

                private void Add(double lat, double lng, double z)
                {
                    Add(lat, lng);
                    if (_thirdDimension != ThirdDimension.Absent)
                    {
                        _zConverter.EncodeValue(z, _result);
                    }
                }

                /// <summary>
                /// Adds a GeoPoint3 tuple to the encoder.
                /// </summary>
                /// <param name="tuple">The GeoPoint3 tuple to add.</param>
                public void Add(GeoPoint3 tuple)
                {
                    Add(tuple.latitude, tuple.longitude, tuple.altitude);
                }

                /// <summary>
                /// Gets the encoded polyline string.
                /// </summary>
                /// <returns>The encoded polyline string.</returns>
                public string GetEncoded()
                {
                    return _result.ToString();
                }
            }

            /// <summary>
            /// Single instance for decoding an input request.
            /// </summary>
            private class Decoder
            {
                private readonly char[] _encoded;
                private int _index;
                private readonly Converter _latConverter;
                private readonly Converter _lngConverter;
                private readonly Converter _zConverter;

                private int _precision;
                private int _thirdDimPrecision;
                private ThirdDimension _thirdDimension;


                /// <summary>
                /// Initializes a new instance of the Decoder class with the specified encoded string.
                /// </summary>
                /// <param name="encoded">The encoded string to decode.</param>
                public Decoder(string encoded)
                {
                    _encoded = encoded.ToCharArray();
                    _index = 0;
                    DecodeHeader();
                    _latConverter = new Converter(_precision);
                    _lngConverter = new Converter(_precision);
                    _zConverter = new Converter(_thirdDimPrecision);
                }

                private bool HasThirdDimension()
                {
                    return _thirdDimension != ThirdDimension.Absent;
                }

                private void DecodeHeader()
                {
                    long header = 0;
                    DecodeHeaderFromString(_encoded, ref _index, ref header);
                    _precision = (int)(header & 15); // we pick the first 4 bits only
                    header >>= 4;
                    _thirdDimension = (ThirdDimension)(header & 7); // we pick the first 3 bits only
                    _thirdDimPrecision = (int)((header >> 3) & 15);
                }

                /// <summary>
                /// Decodes the header from the encoded string.
                /// </summary>
                /// <param name="encoded">The encoded string as a character array.</param>
                /// <param name="index">The current index in the encoded string.</param>
                /// <param name="header">The decoded header value.</param>
                public static void DecodeHeaderFromString(char[] encoded, ref int index, ref long header)
                {
                    long value = 0;

                    // Decode the header version
                    if (!Converter.DecodeUnsignedVarint(encoded, ref index, ref value))
                    {
                        throw new ArgumentException("Invalid encoding");
                    }

                    if (value != FORMAT_VERSION)
                    {
                        throw new ArgumentException("Invalid format version");
                    }

                    // Decode the polyline header
                    if (!Converter.DecodeUnsignedVarint(encoded, ref index, ref value))
                    {
                        throw new ArgumentException("Invalid encoding");
                    }

                    header = value;
                }


                /// <summary>
                /// Decodes a single set of latitude, longitude, and third dimension values from the encoded string.
                /// </summary>
                /// <param name="lat">The decoded latitude value.</param>
                /// <param name="lng">The decoded longitude value.</param>
                /// <param name="z">The decoded third dimension value (e.g., altitude).</param>
                /// <returns>True if a value was successfully decoded; otherwise, false.</returns>
                public bool DecodeOne(
                    ref double lat,
                    ref double lng,
                    ref double z)
                {
                    if (_index == _encoded.Length)
                    {
                        return false;
                    }

                    if (!_latConverter.DecodeValue(_encoded, ref _index, ref lat))
                    {
                        throw new ArgumentException("Invalid encoding");
                    }

                    if (!_lngConverter.DecodeValue(_encoded, ref _index, ref lng))
                    {
                        throw new ArgumentException("Invalid encoding");
                    }

                    if (HasThirdDimension())
                    {
                        if (!_zConverter.DecodeValue(_encoded, ref _index, ref z))
                        {
                            throw new ArgumentException("Invalid encoding");
                        }
                    }

                    return true;
                }
            }

            //Decode a single char to the corresponding value
            private static int DecodeChar(char charValue)
            {
                int pos = charValue - 45;
                if (pos < 0 || pos > 77)
                {
                    return -1;
                }

                return DECODING_TABLE[pos];
            }

            /// <summary>
            /// Stateful instance for encoding and decoding on a sequence of Coordinates part of an request.
            /// Instance should be specific to type of coordinates (e.g. Lat, Lng)
            /// so that specific type delta is computed for encoding.
            /// Lat0 Lng0 3rd0 (Lat1-Lat0) (Lng1-Lng0) (3rdDim1-3rdDim0)
            /// </summary>
            public class Converter
            {
                private long _multiplier;
                private long _lastValue;

                /// <summary>
                /// Initializes a new instance of the Converter class with the specified precision.
                /// </summary>
                /// <param name="precision">The precision for the latitude and longitude values.</param>
                public Converter(int precision)
                {
                    SetPrecision(precision);
                }

                private void SetPrecision(int precision)
                {
                    _multiplier = (long)Math.Pow(10, precision);
                }

                /// <summary>
                /// Encodes an unsigned variable-length integer.
                /// </summary>
                /// <param name="value">The value to encode.</param>
                /// <param name="result">The `StringBuilder` to append the encoded value to.</param>
                public static void EncodeUnsignedVarint(long value, StringBuilder result)
                {
                    while (value > 0x1F)
                    {
                        byte pos = (byte)((value & 0x1F) | 0x20);
                        result.Append(ENCODING_TABLE[pos]);
                        value >>= 5;
                    }

                    result.Append(ENCODING_TABLE[(byte)value]);
                }

                /// <summary>
                /// Encodes a value and appends it to the result.
                /// </summary>
                /// <param name="value">The value to encode.</param>
                /// <param name="result">The `StringBuilder` to append the encoded value to.</param>
                public void EncodeValue(double value, StringBuilder result)
                {
                    /*
                     * Round-half-up
                     * round(-1.4) --> -1
                     * round(-1.5) --> -2
                     * round(-2.5) --> -3
                     */
                    long scaledValue = (long)Math.Round(Math.Abs(value * _multiplier), MidpointRounding.AwayFromZero) * Math.Sign(value);
                    long delta = scaledValue - _lastValue;
                    bool negative = delta < 0;

                    _lastValue = scaledValue;

                    // make room on lowest bit
                    delta <<= 1;

                    // invert bits if the value is negative
                    if (negative)
                    {
                        delta = ~delta;
                    }

                    EncodeUnsignedVarint(delta, result);
                }

                /// <summary>
                /// Decodes an unsigned variable-length integer from the encoded character array.
                /// </summary>
                /// <param name="encoded">The encoded character array.</param>
                /// <param name="index">The current index in the encoded character array.</param>
                /// <param name="result">The decoded result as a long integer.</param>
                /// <returns>True if the decoding was successful; otherwise, false.</returns>
                public static bool DecodeUnsignedVarint(char[] encoded,
                    ref int index,
                    ref long result)
                {
                    short shift = 0;
                    long delta = 0;

                    while (index < encoded.Length)
                    {
                        long value = DecodeChar(encoded[index]);
                        if (value < 0)
                        {
                            return false;
                        }

                        index++;
                        delta |= (value & 0x1F) << shift;
                        if ((value & 0x20) == 0)
                        {
                            result = delta;
                            return true;
                        }
                        else
                        {
                            shift += 5;
                        }
                    }

                    if (shift > 0)
                    {
                        return false;
                    }

                    return true;
                }

                //Decode single coordinate (say lat|lng|z) starting at index
                /// <summary>
                /// Decodes a single coordinate value (latitude, longitude, or third dimension) from the encoded character array.
                /// </summary>
                /// <param name="encoded">The encoded character array.</param>
                /// <param name="index">The current index in the encoded character array.</param>
                /// <param name="coordinate">The decoded coordinate value.</param>
                /// <returns>True if the decoding was successful; otherwise, false.</returns>
                public bool DecodeValue(char[] encoded,
                    ref int index,
                    ref double coordinate)
                {
                    long delta = 0;
                    if (!DecodeUnsignedVarint(encoded, ref index, ref delta))
                    {
                        return false;
                    }

                    if ((delta & 1) != 0)
                    {
                        delta = ~delta;
                    }

                    delta >>= 1;
                    _lastValue += delta;
                    coordinate = (double)_lastValue / _multiplier;
                    return true;
                }
            }
        }

        #endregion
    }
}