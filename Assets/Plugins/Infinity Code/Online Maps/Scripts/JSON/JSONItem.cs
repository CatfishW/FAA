/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace OnlineMaps
{
    /// <summary>
    /// The base class of JSON elements.
    /// </summary>
    public abstract class JSONItem : IEnumerable<JSONItem>
    {
        /// <summary>
        /// Represents an empty JSON item.
        /// </summary>
        protected static readonly JSONItem EMPTY = new JSONValue(null, JSONValueType.NULL);

        /// <summary>
        /// Gets the count of JSON items.
        /// </summary>
        public abstract int count { get; }

        /// <summary>
        /// Get the element by index
        /// </summary>
        /// <param name="index">Index of element</param>
        /// <returns>Element</returns>
        public abstract JSONItem this[int index] { get; }

        /// <summary>
        /// Get the element by key.<br/>
        /// Supports XPath like selectors:<br/>
        /// ["key"] - get element by key.<br/>
        /// ["key1/key2"] - get element key2, which is a child of the element key1.<br/>
        /// ["key/N"] - where N is number. Get array element by index N, which is a child of the element key1.<br/>
        /// ["key/*"] - get all array elements, which is a child of the element key1.<br/>
        /// ["//key"] - get all elements with the key on the first or the deeper levels of the current element.<br/>
        /// </summary>
        /// <param name="key">Element key</param>
        /// <returns>Element</returns>
        public abstract JSONItem this[string key] { get; set; }

        /// <summary>
        /// Serializes the object and adds to the current json node.
        /// </summary>
        /// <param name="obj">Object</param>
        /// <returns>Current json node</returns>
        public virtual JSONItem AppendObject(object obj)
        {
            throw new Exception("AppendObject is only allowed for JSONObject.");
        }

        /// <summary>
        /// Returns the value of the child element, converted to the specified type.
        /// </summary>
        /// <typeparam name="T">Type of variable</typeparam>
        /// <param name="childName">Child element key</param>
        /// <returns>Value</returns>
        public T ChildValue<T>(string childName)
        {
            JSONItem el = this[childName];
            if (el == null) return default(T);
            return el.Value<T>();
        }

        /// <summary>
        /// Deserializes current element
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <returns>Object</returns>
        public T Deserialize<T>()
        {
            return (T)Deserialize(typeof(T));
        }

        /// <summary>
        /// Deserializes current element
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Object</returns>
        public abstract object Deserialize(Type type);

        public override bool Equals(object obj)
        {
            if (this == EMPTY && obj == null) return true;
            return ReferenceEquals(this, obj);
        }

        /// <summary>
        /// Get all elements with the key on the first or the deeper levels of the current element.
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Elements</returns>
        public abstract JSONItem GetAll(string key);

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public virtual IEnumerator<JSONItem> GetEnumerator()
        {
            return null;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Converts the current and the child elements to JSON string.
        /// </summary>
        /// <param name="b">StringBuilder instance</param>
        public abstract void ToJSON(StringBuilder b);

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            ToJSON(b);
            return b.ToString();
        }

        /// <summary>
        /// Returns the value of the element, converted to the specified type.
        /// </summary>
        /// <param name="type">Type of variable</param>
        /// <returns>Value</returns>
        public abstract object Value(Type type);

        /// <summary>
        /// Returns the value of the element, converted to the specified type.
        /// </summary>
        /// <typeparam name="T">Type of variable</typeparam>
        /// <returns>Value</returns>
        public virtual T Value<T>()
        {
            return (T)Value(typeof(T));
        }

        /// <summary>
        /// Returns the value of the element, converted to the specified type.
        /// </summary>
        /// <typeparam name="T">Type of variable</typeparam>
        /// <returns>Value</returns>
        public T V<T>()
        {
            return Value<T>();
        }

        /// <summary>
        /// Returns the value of the child element, converted to the specified type.
        /// </summary>
        /// <typeparam name="T">Type of variable</typeparam>
        /// <param name="childName">Child element key</param>
        /// <returns>Value</returns>
        public T V<T>(string childName)
        {
            return ChildValue<T>(childName);
        }
    }
}