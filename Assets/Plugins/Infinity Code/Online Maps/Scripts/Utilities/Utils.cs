/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    /// <summary>
    /// Helper class, which contains all the basic methods.
    /// </summary>
    public static class Utils
    {
        public const string ExampleMenuPath = "Infinity Code/Online Maps/Examples (API Usage)/";
        
        /// <summary>
        /// Prohibits reinitialization of a map or object
        /// </summary>
        public static bool doNotReinitFlag;

        private static bool _isPlaying;
        
        /// <summary>
        /// Whether the map is started in playmode.
        /// </summary>
        public static bool isPlaying
        {
            get => Application.isPlaying && _isPlaying;
            set
            {
                if (!Application.isPlaying)
                {
                    _isPlaying = false;
                    return;
                }

                _isPlaying = value;
            }
        }

        /// <summary>
        /// Deep copy object
        /// </summary>
        /// <param name="obj">Object to copy</param>
        /// <typeparam name="T">Type of target object</typeparam>
        /// <returns>Copy of object</returns>
        public static T DeepCopy<T>(object obj)
        {
            return (T)DeepCopy(obj, typeof(T));
        }

        /// <summary>
        /// Deep copy object
        /// </summary>
        /// <param name="obj">Object to copy</param>
        /// <param name="targetType">Type of target object</param>
        /// <returns>Copy of object</returns>
        /// <exception cref="ArgumentException">If the object is not serializable.</exception>
        public static object DeepCopy(object obj, Type targetType)
        {
            if (obj == null) return null;
            Type type = obj.GetType();

            if (ReflectionHelper.IsValueType(type) || type == typeof(string)) return obj;
            if (type.IsArray)
            {
                Type elementType = Type.GetType(targetType.FullName.Replace("[]", string.Empty));
                Array array = obj as Array;
                Array copied = Array.CreateInstance(elementType, array.Length);
                for (int i = 0; i < array.Length; i++) copied.SetValue(DeepCopy(array.GetValue(i), elementType), i);
                return copied;
            }

            if (ReflectionHelper.IsClass(type))
            {
                object target = Activator.CreateInstance(targetType);
                IEnumerable<FieldInfo> fields = ReflectionHelper.GetFields(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    object fieldValue = field.GetValue(obj);
                    if (fieldValue == null) continue;
                    field.SetValue(target, DeepCopy(fieldValue, field.FieldType));
                }

                return target;
            }

            throw new ArgumentException("Unknown type");
        }

        /// <summary>
        /// Converts Polyline to point list.
        /// </summary>
        /// <param name="encodedPoints">
        /// The encoded polyline.
        /// </param>
        /// <returns>
        /// A List of Vector2 points;
        /// </returns>
        public static List<GeoPoint> DecodePolylinePoints(string encodedPoints)
        {
            if (string.IsNullOrEmpty(encodedPoints)) return null;

            List<GeoPoint> poly = new List<GeoPoint>();
            char[] polylinechars = encodedPoints.ToCharArray();
            int index = 0;

            int lat = 0;
            int lng = 0;
            int next5bits;

            try
            {
                while (index < polylinechars.Length)
                {
                    int sum = 0;
                    int shifter = 0;
                    do
                    {
                        next5bits = polylinechars[index++] - 63;
                        sum |= (next5bits & 31) << shifter;
                        shifter += 5;
                    } while (next5bits >= 32 && index < polylinechars.Length);

                    if (index >= polylinechars.Length)
                        break;

                    lat += (sum & 1) == 1 ? ~(sum >> 1) : sum >> 1;

                    sum = 0;
                    shifter = 0;
                    do
                    {
                        next5bits = polylinechars[index++] - 63;
                        sum |= (next5bits & 31) << shifter;
                        shifter += 5;
                    } while (next5bits >= 32 && index < polylinechars.Length);

                    if (index >= polylinechars.Length && next5bits >= 32) break;

                    lng += (sum & 1) == 1 ? ~(sum >> 1) : sum >> 1;
                    GeoPoint p = new GeoPoint(Convert.ToDouble(lng) / 100000.0f, Convert.ToDouble(lat) / 100000.0f);
                    poly.Add(p);
                }
            }
            catch
            {
            }

            return poly;
        }

        /// <summary>
        /// Removes a gameobject, component or asset.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        public static void Destroy(Object obj)
        {
            if (!obj) return;

#if UNITY_EDITOR
            if (isPlaying)
            {
                if (obj.GetInstanceID() < 0) Object.Destroy(obj);
            }
            else Object.DestroyImmediate(obj);
#else
            Object.Destroy(obj);
#endif
        }

        /// <summary>
        /// Gets the object with the specified instance ID.
        /// </summary>
        /// <param name="tid">The instance ID of the object.</param>
        /// <returns>The object with the specified instance ID.</returns>
        public static Object GetObject(int tid)
        {
#if UNITY_EDITOR
            if (tid == 0) return null;
            return EditorUtility.InstanceIDToObject(tid);
#else
            return null;
#endif
        }

        /// <summary>
        /// Appends to a StringBuilder the names of the enumeration flags that are set in the given integer value.
        /// </summary>
        /// <param name="builder">The StringBuilder to append to.</param>
        /// <param name="key">The key to append to the StringBuilder before the enumeration value(s).</param>
        /// <param name="type">The Type of the enumeration.</param>
        /// <param name="value">The integer value representing the enumeration flags to check.</param>
        public static void GetValuesFromEnum(StringBuilder builder, string key, Type type, int value)
        {
            builder.Append("&").Append(key).Append("=");
            Array values = Enum.GetValues(type);

            bool addSeparator = false;
            for (int i = 0; i < values.Length; i++)
            {
                int v = (int)values.GetValue(i);
                if ((value & v) == v)
                {
                    if (addSeparator) builder.Append(",");
                    builder.Append(Enum.GetName(type, v));
                    addSeparator = true;
                }
            }
        }

        /// <summary>
        /// Converts HEX string to color.
        /// </summary>
        /// <param name="hex">HEX string</param>
        /// <returns>Color</returns>
        public static Color HexToColor(string hex)
        {
            byte r = Byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = Byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = Byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }
        
        /// <summary>
        /// Parses a string to an integer.
        /// </summary>
        /// <param name="str">A string containing the number to convert.</param>
        /// <returns>A 32-bit signed integer that is equivalent to the number contained in str.</returns>
        public static int ParseInt(string str)
        {
            int n = 1;
            int v = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (i == 0 && str[i] == '-')
                {
                    n = -1;
                    continue;
                }
            
                if (str[i] < '0' || str[i] > '9') break;
                v = v * 10 + str[i] - '0';
            }
            return v * n;
        }

        /// <summary>
        /// Replaces multiple values in a string
        /// </summary>
        /// <param name="str">Input string</param>
        /// <param name="origin">Values to be replaced</param>
        /// <param name="replace">Values to replace</param>
        /// <returns>String with replaced values</returns>
        public static string StrReplace(string str, string[] origin, string[] replace)
        {
            if (origin == null || replace == null) return str;

            for (int i = 0; i < Mathf.Min(origin.Length, replace.Length); i++) str = str.Replace(origin[i], replace[i]);
            return str;
        }

        /// <summary>
        /// Converts tile index to quadkey.
        /// What is the tiles and quadkey, and how it works, you can read here:
        /// https://msdn.microsoft.com/en-us/library/bb259689.aspx
        /// </summary>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <param name="zoom">Zoom</param>
        /// <returns>Quadkey</returns>
        public static string TileToQuadKey(int x, int y, int zoom)
        {
            StringBuilder quadKey = new StringBuilder();
            for (int i = zoom; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((x & mask) != 0) digit++;
                if ((y & mask) != 0)
                {
                    digit++;
                    digit++;
                }

                quadKey.Append(digit);
            }

            return quadKey.ToString();
        }

        /// <summary>
        /// Converts tile index to quadkey.
        /// What is the tiles and quadkey, and how it works, you can read here:
        /// https://msdn.microsoft.com/en-us/library/bb259689.aspx
        /// </summary>
        /// <param name="x">Tile X</param>
        /// <param name="y">Tile Y</param>
        /// <param name="zoom">Tile Zoom</param>
        /// <param name="quadKey">StringBuilder where to write quadKey</param>
        /// <returns>quadKey StringBuilder</returns>
        public static StringBuilder TileToQuadKey(int x, int y, int zoom, StringBuilder quadKey)
        {
            for (int i = zoom; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((x & mask) != 0) digit++;
                if ((y & mask) != 0)
                {
                    digit++;
                    digit++;
                }

                quadKey.Append(digit);
            }

            return quadKey;
        }
    }
}