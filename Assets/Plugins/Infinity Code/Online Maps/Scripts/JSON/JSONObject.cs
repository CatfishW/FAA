/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace OnlineMaps
{
    /// <summary>
    /// The wrapper for JSON dictionary.
    /// </summary>
    public class JSONObject : JSONItem
    {
        private Dictionary<string, JSONItem> _table;

        /// <summary>
        /// Gets the count of items in the JSON object.
        /// </summary>
        public override int count => _table.Count;

        /// <summary>
        /// Dictionary of items
        /// </summary>
        public Dictionary<string, JSONItem> table => _table;

        /// <summary>
        /// Gets the JSON item by key.
        /// </summary>
        /// <param name="key">The key of the JSON item.</param>
        /// <returns>The JSON item associated with the specified key.</returns>
        public override JSONItem this[string key]
        {
            get => Get(key);
            set => Add(key, value);
        }

        /// <summary>
        /// Gets the JSON item by index.
        /// </summary>
        /// <param name="index">The index of the JSON item.</param>
        /// <returns>The JSON item at the specified index.</returns>
        public override JSONItem this[int index]
        {
            get
            {
                if (index < 0) return null;

                int i = 0;
                foreach (KeyValuePair<string, JSONItem> pair in _table)
                {
                    if (i == index) return pair.Value;
                    i++;
                }

                return EMPTY;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public JSONObject()
        {
            _table = new Dictionary<string, JSONItem>();
        }

        /// <summary>
        /// Adds element to the dictionary
        /// </summary>
        /// <param name="name">Key</param>
        /// <param name="value">Value</param>
        public void Add(string name, JSONItem value)
        {
            _table[name] = value;
        }

        /// <summary>
        /// Adds an element to the dictionary with the specified key and value.
        /// </summary>
        /// <param name="name">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        public void Add(string name, object value)
        {
            if (value is string || value is bool || value is int || value is long || value is short || value is float || value is double) _table[name] = new JSONValue(value);
            else if (value is UnityEngine.Object)
            {
                _table[name] = new JSONValue((value as UnityEngine.Object).GetInstanceID());
            }
            else _table[name] = JSON.Serialize(value);
        }

        /// <summary>
        /// Adds an element to the dictionary with the specified key, value, and value type.
        /// </summary>
        /// <param name="name">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <param name="valueType">The type of the value.</param>
        public void Add(string name, object value, JSONValueType valueType)
        {
            _table[name] = new JSONValue(value, valueType);
        }

        /// <summary>
        /// Serializes the object and adds it to the current JSON node.
        /// </summary>
        /// <param name="obj">The object to serialize and append.</param>
        /// <returns>The current JSON node.</returns>
        public override JSONItem AppendObject(object obj)
        {
            Combine(JSON.Serialize(obj));
            return this;
        }

        /// <summary>
        /// Combines two JSON Object.
        /// </summary>
        /// <param name="other">Other JSON Object</param>
        /// <param name="overwriteExistingValues">Overwrite the existing values?</param>
        public void Combine(JSONItem other, bool overwriteExistingValues = true)
        {
            JSONObject otherObj = other as JSONObject;
            if (otherObj == null) throw new Exception("Only JSONObject is allowed to be combined.");
            Dictionary<string, JSONItem> otherDict = otherObj.table;
            foreach (KeyValuePair<string, JSONItem> pair in otherDict)
            {
                if (overwriteExistingValues || !_table.ContainsKey(pair.Key)) _table[pair.Key] = pair.Value;
            }
        }

        public override object Deserialize(Type type)
        {
            IEnumerable<MemberInfo> members = ReflectionHelper.GetMembers(type, BindingFlags.Instance | BindingFlags.Public);
            return Deserialize(type, members);
        }

        /// <summary>
        /// Deserializes current element
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="members">Members of variable</param>
        /// <returns>Object</returns>
        public object Deserialize(Type type, IEnumerable<MemberInfo> members)
        {
            object v = Activator.CreateInstance(type);
            DeserializeObject(v, members);
            return v;
        }

        /// <summary>
        /// Deserializes the specified object using the provided binding flags.
        /// </summary>
        /// <param name="obj">The object to deserialize.</param>
        /// <param name="bindingFlags">The binding flags to use for deserialization.</param>
        public void DeserializeObject(object obj, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            IEnumerable<MemberInfo> members = ReflectionHelper.GetMembers(obj.GetType(), bindingFlags);
            DeserializeObject(obj, members);
        }

        /// <summary>
        /// Deserializes the specified object using the provided members.
        /// </summary>
        /// <param name="obj">The object to deserialize.</param>
        /// <param name="members">The members to use for deserialization.</param>
        public void DeserializeObject(object obj, IEnumerable<MemberInfo> members)
        {
            foreach (MemberInfo member in members)
            {
#if !NETFX_CORE
                MemberTypes memberType = member.MemberType;
                if (memberType != MemberTypes.Field && memberType != MemberTypes.Property) continue;
#else
                MemberTypes memberType;
                if (member is PropertyInfo) memberType = MemberTypes.Property;
                else if (member is FieldInfo) memberType = MemberTypes.Field;
                else continue;
#endif

                if (memberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite) continue;
                JSONItem item;

#if !NETFX_CORE
                object[] attributes = member.GetCustomAttributes(typeof(AliasAttribute), true);
                AliasAttribute alias = attributes.Length > 0 ? attributes[0] as AliasAttribute : null;
#else
                IEnumerable<Attribute> attributes = member.GetCustomAttributes(typeof(AliasAttribute), true);
                AliasAttribute alias = null;
                foreach (Attribute a in attributes)
                {
                    alias = a as AliasAttribute;
                    break;
                }
#endif
                if (alias == null || !alias.ignoreFieldName)
                {
                    if (_table.TryGetValue(member.Name, out item))
                    {
                        Type t = memberType == MemberTypes.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
                        if (memberType == MemberTypes.Field) ((FieldInfo)member).SetValue(obj, item.Deserialize(t));
                        else ((PropertyInfo)member).SetValue(obj, item.Deserialize(t), null);
                        continue;
                    }
                }

                if (alias != null)
                {
                    for (int j = 0; j < alias.aliases.Length; j++)
                    {
                        if (_table.TryGetValue(alias.aliases[j], out item))
                        {
                            Type t = memberType == MemberTypes.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
                            if (memberType == MemberTypes.Field) ((FieldInfo)member).SetValue(obj, item.Deserialize(t));
                            else ((PropertyInfo)member).SetValue(obj, item.Deserialize(t), null);
                            break;
                        }
                    }
                }
            }
        }

        private JSONItem Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return EMPTY;

            if (key.Length > 2 && key[0] == '/' && key[1] == '/')
            {
                string k = key.Substring(2);
                if (string.IsNullOrEmpty(k) || k.StartsWith("//")) return EMPTY;
                return GetAll(k);
            }

            return GetThis(key);
        }
        
        /// <summary>
        /// Get all elements with the key on the first or the deeper levels of the current element.
        /// </summary>
        /// <param name="k">Key</param>
        /// <returns>Elements</returns>
        public override JSONItem GetAll(string k)
        {
            JSONItem item = GetThis(k);
            JSONArray arr = null;
            if (item != null)
            {
                arr = new JSONArray();
                arr.Add(item);
            }

            var enumerator = _table.GetEnumerator();
            while (enumerator.MoveNext())
            {
                item = enumerator.Current.Value;
                JSONArray subArr = item.GetAll(k) as JSONArray;
                if (subArr != null)
                {
                    if (arr == null) arr = new JSONArray();
                    arr.AddRange(subArr);
                }
            }

            return arr;
        }

        public override IEnumerator<JSONItem> GetEnumerator()
        {
            return _table.Values.GetEnumerator();
        }

        private JSONItem GetThis(string key)
        {
            JSONItem item;
            int index = -1;
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == '/')
                {
                    index = i;
                    break;
                }
            }

            if (index != -1)
            {
                string k = key.Substring(0, index);
                if (!string.IsNullOrEmpty(k))
                {
                    if (_table.TryGetValue(k, out item))
                    {
                        string nextPart = key.Substring(index + 1);
                        return item[nextPart];
                    }
                }

                return EMPTY;
            }

            return _table.TryGetValue(key, out item) ? item : EMPTY;
        }

        /// <summary>
        /// Parse a string that contains JSON dictonary
        /// </summary>
        /// <param name="json">String that contains JSON dictonary</param>
        /// <returns>Instance</returns>
        public static JSONObject ParseObject(string json)
        {
            return JSON.Parse(json) as JSONObject;
        }

        public override void ToJSON(StringBuilder b)
        {
            b.Append("{");
            bool hasChildren = false;
            foreach (KeyValuePair<string, JSONItem> pair in _table)
            {
                b.Append("\"").Append(pair.Key).Append("\"").Append(":");
                pair.Value.ToJSON(b);
                b.Append(",");
                hasChildren = true;
            }

            if (hasChildren) b.Remove(b.Length - 1, 1);
            b.Append("}");
        }

        public override object Value(Type type)
        {
            if (ReflectionHelper.IsValueType(type)) return Activator.CreateInstance(type);
            return Deserialize(type);
        }
    }
}