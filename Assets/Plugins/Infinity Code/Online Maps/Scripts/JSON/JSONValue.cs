/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The wrapper for JSON value.
    /// </summary>
    public class JSONValue : JSONItem
    {
        private JSONValueType _type;
        private object _value;

        public override int count => 0;

        /// <summary>
        /// Get the type of value
        /// </summary>
        public JSONValueType type => _type;

        /// <summary>
        /// Gets / sets the current value
        /// </summary>
        public object value
        {
            get => _value;
            set
            {
#if !UNITY_WP_8_1 || UNITY_EDITOR
                if (value == null || value is DBNull)
#else
                if (value == null)
#endif
                {
                    _type = JSONValueType.NULL;
                    _value = value;
                }
                else if (value is string)
                {
                    _type = JSONValueType.STRING;
                    _value = value;
                }
                else if (value is double)
                {
                    _type = JSONValueType.DOUBLE;
                    _value = (double)value;
                }
                else if (value is float)
                {
                    _type = JSONValueType.DOUBLE;
                    _value = (double)(float)value;
                }
                else if (value is bool)
                {
                    _type = JSONValueType.BOOLEAN;
                    _value = value;
                }
                else if (value is long)
                {
                    _type = JSONValueType.LONG;
                    _value = value;
                }
                else if (value is int || value is short || value is byte)
                {
                    _type = JSONValueType.LONG;
                    _value = Convert.ChangeType(value, typeof(long));
                }
                else throw new Exception("Unknown type of value.");
            }
        }

        public override JSONItem this[string key]
        {
            get => EMPTY;
            set { }
        }

        public override JSONItem this[int index] => EMPTY;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        public JSONValue(object value) => this.value = value;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="type">Type of value</param>
        public JSONValue(object value, JSONValueType type)
        {
            _value = value;
            _type = type;
        }

        public override object Deserialize(Type type) => Value(type);

        public override JSONItem GetAll(string key) => null;

        public override IEnumerator<JSONItem> GetEnumerator()
        {
            yield return this;
        }
        
        /// <summary>
        /// Get the type of value
        /// </summary>
        public JSONValueType GetValueType() => _type;

        public override void ToJSON(StringBuilder b)
        {
            if (_type == JSONValueType.STRING) WriteString(b);
            else if (_type == JSONValueType.NULL) b.Append("null");
            else if (_type == JSONValueType.BOOLEAN) b.Append((bool) _value ? "true" : "false");
            else if (_type == JSONValueType.DOUBLE) b.Append(((double) value).ToString(Culture.cultureInfo));
            else b.Append(value);
        }

        public override string ToString()
        {
            if (_type == JSONValueType.DOUBLE) return ((double) value).ToString(Culture.cultureInfo);
            return value.ToString();
        }

        public override object Value(Type t)
        {
            if (_type == JSONValueType.NULL || _value == null)
            {
                if (ReflectionHelper.IsValueType(t)) return Activator.CreateInstance(t);
                return null;
            }

            if (t == typeof(string)) return Convert.ChangeType(_value, t);

            if (_type == JSONValueType.BOOLEAN)
            {
                if (t == typeof(bool)) return Convert.ChangeType(_value, t);
            }
            else if (_type == JSONValueType.DOUBLE)
            {
                if (t == typeof(double)) return Convert.ChangeType(_value, t, Culture.numberFormat);
                if (t == typeof(float)) return Convert.ChangeType((double)_value, t, Culture.numberFormat);
            }
            else if (_type == JSONValueType.LONG)
            {
                if (t == typeof(long)) return Convert.ChangeType(_value, t);
#if UNITY_EDITOR
                if (t.IsSubclassOf(typeof(UnityEngine.Object)))
                {
                    return UnityEditor.EditorUtility.InstanceIDToObject((int)(long)_value);
                }
#endif

                try
                {
                    return Convert.ChangeType((long)_value, t);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message + "\n" + e.StackTrace);
                    return null;
                }
            }
            else if (_type == JSONValueType.STRING)
            {
                MethodInfo method = ReflectionHelper.GetMethod(t, "Parse", new[] { typeof(string), typeof(IFormatProvider) });
                if (method != null) return method.Invoke(null, new object[] { value, Culture.numberFormat });

                method = ReflectionHelper.GetMethod(t, "Parse", new[] { typeof(string) });
                return method.Invoke(null, new[] { value });
            }
            StringBuilder builder = new StringBuilder();
            ToJSON(builder);
            throw new InvalidCastException(t.FullName + "\n" + builder);
        }

        private void WriteString(StringBuilder b)
        {
            b.Append('\"');

            string s = value as string;

            int runIndex = -1;
            int l = s.Length;
            for (var index = 0; index < l; ++index)
            {
                var c = s[index];

                if (c >= ' ' && c < 128 && c != '\"' && c != '\\')
                {
                    if (runIndex == -1) runIndex = index;

                    continue;
                }

                if (runIndex != -1)
                {
                    b.Append(s, runIndex, index - runIndex);
                    runIndex = -1;
                }

                switch (c)
                {
                    case '\t': b.Append("\\t"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\n': b.Append("\\n"); break;
                    case '"':
                    case '\\': b.Append('\\'); b.Append(c); break;
                    default:
                        b.Append("\\u");
                        b.Append(((int)c).ToString("X4", NumberFormatInfo.InvariantInfo));
                        break;
                }
            }

            if (runIndex != -1) b.Append(s, runIndex, s.Length - runIndex);
            b.Append('\"');
        }

        public static implicit operator string(JSONValue val)
        {
            return val.ToString();
        }
    }
}