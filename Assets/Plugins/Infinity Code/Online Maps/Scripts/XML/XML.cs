/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if NETFX_CORE
using Windows.Data.Xml.Dom;
#else
using System.Xml;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    /// <summary>
    /// Wrapper for XML.
    /// </summary>
    public class XML : IEnumerable
    {
        private XmlDocument _document;
        private XmlElement _element;

        /// <summary>
        /// Name of the node.
        /// </summary>
        public string name
        {
#if !NETFX_CORE
            get { return _element != null ? _element.Name : null; }
#else
            get { return _element != null ? _element.TagName: null; }
#endif
        }

        /// <summary>
        /// The number of child nodes.
        /// </summary>
        public int count
        {
            get { return hasChildNodes ? _element.ChildNodes.Count : 0; }
        }

        /// <summary>
        /// Checks whether the contents of the node.
        /// </summary>
        public bool isNull
        {
            get { return _document == null || _element == null; }
        }

        /// <summary>
        /// Reference to XmlDocument.
        /// </summary>
        public XmlDocument document
        {
            get { return _document; }
        }

        /// <summary>
        /// Reference to XmlElement.
        /// </summary>
        public XmlElement element
        {
            get { return _element; }
        }

        /// <summary>
        /// Gets a value indicating whether the current node has any attributes. 
        /// </summary>
        public bool hasAttributes
        {
            get
            {
                return _element != null && _element.Attributes.Count > 0;
            }
        }

        /// <summary>
        /// Gets a value indicating the presence of the child nodes from the current node.
        /// </summary>
        public bool hasChildNodes
        {
            get
            {
#if !NETFX_CORE
                return _element != null && _element.HasChildNodes;
#else
                return _element != null && _element.HasChildNodes();
#endif
            }
        }

        /// <summary>
        /// Content of node as string.
        /// </summary>
        public string outerXml
        {
            get
            {
#if !NETFX_CORE
                return _element != null ? _element.OuterXml : null;
#else
                return _element != null? _element.GetXml(): null;
#endif
            }
        }

        /// <summary>
        /// Get the child element by index.
        /// </summary>
        /// <param name="index">Index of child element.</param>
        /// <returns>Child element.</returns>
        public XML this[int index]
        {
            get
            {
                if (!hasChildNodes) return new XML();
                if (index < 0 || index >= _element.ChildNodes.Count) return new XML();
                return new XML(_element.ChildNodes[index] as XmlElement);
            }
        }
    
        /// <summary>
        /// Get the child element by name.
        /// </summary>
        /// <param name="childName">Name of child element.</param>
        /// <returns>Child element.</returns>
        public XML this[string childName]
        {
            get
            {
                if (!hasChildNodes) return new XML();
#if !NETFX_CORE
                return new XML(_element[childName]);
#else
                return new XML(GetFirstChild(_element, childName));
#endif
            }
        }

        /// <summary>
        /// Creates an empty element.
        /// </summary>
        public XML()
        {
        
        }

        /// <summary>
        /// Creates a new element with the specified name.
        /// </summary>
        /// <param name="nodeName">Name of element.</param>
        public XML(string nodeName)
        {
            try
            {
                _document = new XmlDocument();
                _element = _document.CreateElement(nodeName);
                _document.AppendChild(_element);
            }
            catch (Exception)
            {
                _document = null;
                _element = null;
            }
        }

        /// <summary>
        /// Creates a new element based on the XmlElement.
        /// </summary>
        /// <param name="xmlElement">XmlElement for which will create the wrapper.</param>
        public XML(XmlElement xmlElement)
        {
            if (xmlElement == null) return;

            _element = xmlElement;
            _document = _element.OwnerDocument;
        }

        /// <summary>
        /// Get an attribute by name.
        /// </summary>
        /// <param name="attributeName">Name of attribute.</param>
        /// <returns>Value of attribute as string.</returns>
        public string A(string attributeName)
        {
            return A<string>(attributeName);
        }

        /// <summary>
        /// Get an attribute by name, and return as the specified type.
        /// </summary>
        /// <typeparam name="T">Type of attribute.</typeparam>
        /// <param name="attributeName">Name of attribute.</param>
        /// <returns>Value of attribute as specified type.</returns>
        public T A<T>(string attributeName)
        {
            if (!hasAttributes) return default(T);
#if !NETFX_CORE
            XmlAttribute el = _element.Attributes[attributeName];
#else
            XmlAttribute el = _element.Attributes.GetNamedItem(attributeName) as XmlAttribute;
#endif

            if (el == null) return default(T);

            string value = el.Value;
            if (string.IsNullOrEmpty(value)) return default(T);

            Type type = typeof(T);
            if (type == typeof(string)) return (T)Convert.ChangeType(value, type);

            T obj = default;
            PropertyInfo[] properties = ReflectionHelper.GetProperties(type);
            Type underlyingType = type;

#if !UNITY_WSA
            if (properties.Length == 2 && string.Equals(properties[0].Name, "HasValue", StringComparison.InvariantCultureIgnoreCase)) underlyingType = properties[1].PropertyType;
#else
            if (properties.Length == 2 && string.Equals(properties[0].Name, "HasValue", StringComparison.OrdinalIgnoreCase)) underlyingType = properties[1].PropertyType;
#endif

            MethodInfo method = ReflectionHelper.GetMethod(underlyingType, "Parse", new[] { typeof(string), typeof(IFormatProvider) });
            if (method != null) return (T)method.Invoke(null, new object[] { value, Culture.numberFormat });

            method = ReflectionHelper.GetMethod(underlyingType, "Parse", new[] { typeof(string) });
            if (method != null) return (T)method.Invoke(null, new object[] { value });

            return obj;
        }

        /// <summary>
        /// Set an named attribute.
        /// </summary>
        /// <param name="attributeName">Name of attribute.</param>
        /// <param name="value">Value of attribute.</param>
        public void A(string attributeName, object value)
        {
            if (_element == null) return;

            string val;
            Type type = value.GetType();
            MethodInfo method = ReflectionHelper.GetMethod(type, "ToString", new[] { typeof(IFormatProvider) });
            if (method != null) val = (string)method.Invoke(value, new object[] { Culture.numberFormat });
            else val = value.ToString();

            _element.SetAttribute(attributeName, val);
        }

        /// <summary>
        /// Sets the color attribute as hex value.
        /// </summary>
        /// <param name="attributeName">Name of attribute.</param>
        /// <param name="value">Color</param>
        public void A(string attributeName, Color32 value)
        {
            A(attributeName, value.r.ToString("X2") + value.g.ToString("X2") + value.b.ToString("X2"));
        }

        /// <summary>
        /// Append a child element.
        /// </summary>
        /// <param name="newChild">Element.</param>
        public void AppendChild(XmlElement newChild)
        {
            if (_element == null || newChild == null) return;
            if (_element.OwnerDocument != newChild.OwnerDocument) newChild = _element.OwnerDocument.ImportNode(newChild, true) as XmlElement;
            _element.AppendChild(newChild);
        }

        /// <summary>
        /// Append a child element.
        /// </summary>
        /// <param name="newChild">Element.</param>
        public void AppendChild(XML newChild)
        {
            if (newChild == null) return;
            AppendChild(newChild._element);
        }

        /// <summary>
        /// Append a child elements.
        /// </summary>
        /// <param name="list">List of elements.</param>
#if !NETFX_CORE
        public void AppendChildren(IEnumerable<XmlNode> list)
#else
        public void AppendChildren(IEnumerable<IXmlNode> list)
#endif
        {
            if (_element == null) return;

            foreach (var node in list) _element.AppendChild(node);
        }

        /// <summary>
        /// Append a child elements.
        /// </summary>
        /// <param name="list">List of elements.</param>
        public void AppendChildren(IEnumerable<XML> list)
        {
            if (_element == null) return;

            foreach (XML node in list)
            {
                if (node._element != null) _element.AppendChild(node._element);
            }
        }

        /// <summary>
        /// Append a child elements.
        /// </summary>
        /// <param name="list">List of elements.</param>
        public void AppendChildren(XmlNodeList list)
        {
            if (_element == null) return;

#if !NETFX_CORE
            foreach (XmlNode node in list) _element.AppendChild(node);
#else
            foreach (IXmlNode node in list) _element.AppendChild(node);
#endif
        }

        /// <summary>
        /// Append a child elements.
        /// </summary>
        /// <param name="list">List of elements.</param>
        public void AppendChildren(XMLList list)
        {
            if (_element == null) return;

            foreach (XML node in list)
            {
                if (node._element != null) _element.AppendChild(node._element);
            }
        }

        /// <summary>
        /// Creates a child element with the specified name.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName)
        {
            if (_document == null || _element == null) return new XML();

            XmlElement xmlElement = _document.CreateElement(nodeName);
            _element.AppendChild(xmlElement);
            return new XML(xmlElement);
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, bool value)
        {
            return Create(nodeName, value ? "True" : "False");
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, Color32 value)
        {
            return Create(nodeName, value.r.ToString("X2") + value.g.ToString("X2") + value.b.ToString("X2"));
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, float value)
        {
            return Create(nodeName, value.ToString(Culture.numberFormat));
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, double value)
        {
            return Create(nodeName, value.ToString(Culture.numberFormat));
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, int value)
        {
            return Create(nodeName, value.ToString());
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, LimitedRange value)
        {
            XML node = Create(nodeName);
            node.Create("Min", value.min);
            node.Create("Max", value.max);
            return node;
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, Object value)
        {
            return Create(nodeName, value != null ? value.GetInstanceID() : 0);
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, string value)
        {
            XML node = Create(nodeName);
            node.SetChild(value);
            return node;
        }
        
        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, GeoPoint value)
        {
            XML node = Create(nodeName);
            node.Create("X", value.x);
            node.Create("Y", value.y);
            return node;
        }
        
        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, Vector2 value)
        {
            XML node = Create(nodeName);
            node.Create("X", value.x);
            node.Create("Y", value.y);
            return node;
        }

        /// <summary>
        /// Creates a child element with the specified name and value.
        /// </summary>
        /// <param name="nodeName">Name of child element.</param>
        /// <param name="value">Value of child element.</param>
        /// <returns>Child element.</returns>
        public XML Create(string nodeName, Vector3 value)
        {
            XML node = Create(nodeName);
            node.Create("X", value.x);
            node.Create("Y", value.y);
            node.Create("Z", value.z);
            return node;
        }

        /// <summary>
        /// Find a child at the specified XPath.
        /// </summary>
        /// <param name="xpath">XPath string.</param>
        /// <param name="nsmgr">An XmlNamespaceManager to use for resolving namespaces for prefixes in the XPath expression. </param>
        /// <returns>Child element.</returns>
        public XML Find(string xpath, XmlNamespaceManager nsmgr = null)
        {
            if (!hasChildNodes) return new XML();
#if !NETFX_CORE

            XmlElement xmlElement = _element.SelectSingleNode(xpath, nsmgr) as XmlElement;
#else
            string ns = null;
            if (nsmgr != null)
            {
                var nss = nsmgr.GetNamespacesInScope(System.Xml.XmlNamespaceScope.ExcludeXml);
                if (nss.Keys.Count > 0)
                {
                    var key = nss.Keys.First();
                    ns = String.Format("xmlns:{0}='{1}'", key, nsmgr.LookupNamespace(key));                
                }
            }
            
            XmlElement xmlElement = (ns == null ? _element.SelectSingleNode(xpath) : _element.SelectSingleNodeNS(xpath, ns)) as XmlElement;
#endif

            if (xmlElement != null) return new XML(xmlElement);
            return new XML();
        }

        /// <summary>
        /// Find a child at the specified XPath, and return value as the specified type.
        /// </summary>
        /// <typeparam name="T">Type of child element.</typeparam>
        /// <param name="xpath">XPath string.</param>
        /// <param name="nsmgr">An XmlNamespaceManager to use for resolving namespaces for prefixes in the XPath expression. </param>
        /// <returns>Value of child element as the specified type.</returns>
        public T Find<T>(string xpath, XmlNamespaceManager nsmgr = null)
        {
            if (!hasChildNodes) return default;
#if !NETFX_CORE
            return Get<T>(_element.SelectSingleNode(xpath, nsmgr) as XmlElement);
#else
            string ns = null;
            if (nsmgr != null)
            {
                var nss = nsmgr.GetNamespacesInScope(System.Xml.XmlNamespaceScope.ExcludeXml);
                if (nss.Keys.Count > 0)
                {
                    var key = nss.Keys.First();
                    ns = String.Format("xmlns:{0}='{1}'", key, nsmgr.LookupNamespace(key));                      
                }
            }
                    
            return Get<T>((ns == null ? _element.SelectSingleNode(xpath) : _element.SelectSingleNodeNS(xpath, ns)) as XmlElement);
#endif
        }

        /// <summary>
        /// Finds all children at the specified XPath.
        /// </summary>
        /// <param name="xpath">XPath string.</param>
        /// <param name="nsmgr">An XmlNamespaceManager to use for resolving namespaces for prefixes in the XPath expression. </param>
        /// <returns>List of the elements.</returns>
        public XMLList FindAll(string xpath, XmlNamespaceManager nsmgr = null)
        {
            if (!hasChildNodes) return new XMLList();
#if !NETFX_CORE
            return new XMLList(_element.SelectNodes(xpath, nsmgr));
#else
            string ns = null;
            if (nsmgr != null)
            {
                var nss = nsmgr.GetNamespacesInScope(System.Xml.XmlNamespaceScope.ExcludeXml);
                if (nss.Keys.Count > 0)
                {
                    var key = nss.Keys.First();
                    ns = String.Format("xmlns:{0}='{1}'", key, nsmgr.LookupNamespace(key));                       
                }
            }
            
            return new XMLList(ns == null ? _element.SelectNodes(xpath) : _element.SelectNodesNS(xpath, ns));
#endif
        }

        /// <summary>
        /// Get the value of element as string.
        /// </summary>
        /// <param name="childName">Name of child.</param>
        /// <returns>Value of element as string.</returns>
        public string Get(string childName)
        {
            return Get<string>(childName);
        }

#if NETFX_CORE
        private static XmlElement GetFirstChild(XmlElement element, string childName)
        {
            if (element == null) return null;
            var nodeList = element.GetElementsByTagName(childName);
            if (nodeList.Count == 0) return null;
            return nodeList[0] as XmlElement;
        }
#endif

        /// <summary>
        /// Get the value of element as the specified type.
        /// </summary>
        /// <typeparam name="T">Type of element</typeparam>
        /// <param name="el">Element</param>
        /// <returns>Value of element as the specified type.</returns>
        public T Get<T>(XmlElement el)
        {
            return Get(el, default(T));
        }

        /// <summary>
        /// Get the value of element as the specified type or default value if the child is not found.
        /// </summary>
        /// <typeparam name="T">Type of element</typeparam>
        /// <param name="el">Element</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Value of element as the specified type or default value.</returns>
        public T Get<T>(XmlElement el, T defaultValue)
        {
            if (el == null) return defaultValue;

#if !NETFX_CORE
            string value = el.InnerXml;
#else
            string value = el.InnerText;
#endif
            if (string.IsNullOrEmpty(value)) return defaultValue;

            Type type = typeof(T);

            if (type == typeof(string)) return (T)Convert.ChangeType(value, type);
            if (type == typeof(Color) || type == typeof(Color32)) return (T)Convert.ChangeType(Utils.HexToColor(value), type);

#if !NETFX_CORE
            if (type == typeof(Vector2)) return (T)Convert.ChangeType(new Vector2(Get<float>(el["X"]), Get<float>(el["Y"])), type);
            if (type == typeof(Vector3)) return (T)Convert.ChangeType(new Vector3(Get<float>(el["X"]), Get<float>(el["Y"]), Get<float>(el["Z"])), type);
            if (type == typeof(LimitedRange)) return (T)Convert.ChangeType(new LimitedRange(Get<int>(el["Min"]), Get<int>(el["Max"])), type);
#else
            if (type == typeof(Vector2)) return (T)Convert.ChangeType(new Vector2(Get<float>(GetFirstChild(el, "X")), Get<float>(GetFirstChild(el, "Y"))), type);
            if (type == typeof(Vector3)) return (T)Convert.ChangeType(new Vector3(Get<float>(GetFirstChild(el, "X")), Get<float>(GetFirstChild(el, "Y")), Get<float>(GetFirstChild(el, "Z"))), type);
            if (type == typeof(Range)) return (T)Convert.ChangeType(new Range(Get<int>(GetFirstChild(el, "Min")), Get<int>(GetFirstChild(el, "Max"))), type);
#endif

            T obj = defaultValue;
            PropertyInfo[] properties = ReflectionHelper.GetProperties(type);
            Type underlyingType = type;

#if !UNITY_WSA
            if (properties.Length == 2 && string.Equals(properties[0].Name, "HasValue", StringComparison.InvariantCultureIgnoreCase)) underlyingType = properties[1].PropertyType;
#else
            if (properties.Length == 2 && string.Equals(properties[0].Name, "HasValue", StringComparison.OrdinalIgnoreCase)) underlyingType = properties[1].PropertyType;
#endif

            try
            {
                MethodInfo method = ReflectionHelper.GetMethod(underlyingType, "Parse", new[] { typeof(string), typeof(IFormatProvider) });
                if (method != null) obj = (T)method.Invoke(null, new object[] { value, Culture.numberFormat });
                else
                {
                    method = ReflectionHelper.GetMethod(underlyingType, "Parse", new[] { typeof(string) });
                    obj = (T)method.Invoke(null, new object[] { value });
                }
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message + "\n" + exception.StackTrace);
                throw;
            }

            return obj;
        }

        /// <summary>
        /// Get the value of child element as the specified type.
        /// </summary>
        /// <typeparam name="T">Type of child element.</typeparam>
        /// <param name="childName">Name of child.</param>
        /// <returns>Value of element as the specified type.</returns>
        public T Get<T>(string childName)
        {
            return Get(childName, default(T));
        }

        /// <summary>
        /// Get the value of child element as the specified type or default value if the child is not found.
        /// </summary>
        /// <typeparam name="T">Type of child element.</typeparam>
        /// <param name="childName">Name of child.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <returns>Value of element as the specified type or default value.</returns>
        public T Get<T>(string childName, T defaultValue)
        {
            if (!hasChildNodes) return defaultValue;
#if !NETFX_CORE
            return Get(_element[childName], defaultValue);
#else
            return Get(GetFirstChild(_element, childName), defaultValue);
#endif
        }

        public IEnumerator GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        public Vector2d GetLatLng(string subNodeName)
        {
            XML subNode = this[subNodeName];
            return new Vector2d(subNode.Get<double>("lng"), subNode.Get<double>("lat"));
        }

        /// <summary>
        /// Get NamespaceManager for current xml node.
        /// </summary>
        /// <param name="prefix">Namespace prefix.</param>
        /// <returns>NamespaceManager</returns>
        public NamespaceManager GetNamespaceManager(string prefix = null)
        {
#if !NETFX_CORE
            NamespaceManager nsmgr = new NamespaceManager(document.NameTable);
            if (prefix == null) prefix = element.GetPrefixOfNamespace(element.NamespaceURI);
            nsmgr.AddNamespace(prefix, element.NamespaceURI);
#else
            NamespaceManager nsmgr = new NamespaceManager(new System.Xml.NameTable());
            if (prefix == null) prefix = element.Prefix.ToString();
            nsmgr.AddNamespace(prefix, element.NamespaceUri.ToString());
#endif
            return nsmgr;
        }

        /// <summary>
        /// Checks whether the contain child element with the specified name.
        /// </summary>
        /// <param name="childName">Name of child element</param>
        /// <returns>True - this contains the child with the specified name, false - otherwise.</returns>
        public bool HasChild(string childName)
        {
            if (!hasChildNodes) return false;
#if !NETFX_CORE
            return _element[childName] != null;
#else
            return GetFirstChild(_element, childName) != null;
#endif
        }

        /// <summary>
        /// Converts XMLNode coordinates from Google Maps into Vector2.
        /// </summary>
        /// <param name="node">XMLNode coordinates from Google Maps.</param>
        /// <returns>Coordinates as Vector2.</returns>
        public static Vector2 GetVector2FromNode(XML node)
        {
            float lng = node.Get<float>("lng");
            float lat = node.Get<float>("lat");
            return new Vector2(lng, lat);
        }

        /// <summary>
        /// Converts XMLNode coordinates from Google Maps into Vector2d.
        /// </summary>
        /// <param name="node">XMLNode coordinates from Google Maps.</param>
        /// <returns>Coordinates as Vector2d.</returns>
        public static Vector2d GetVector2dFromNode(XML node)
        {
            double lng = node.Get<double>("lng");
            double lat = node.Get<double>("lat");
            return new Vector2d(lng, lat);
        }

        /// <summary>
        /// Loads the XML from a string.
        /// </summary>
        /// <param name="xmlString">XML string.</param>
        /// <returns>First element.</returns>
        public static XML Load(string xmlString)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xmlString);
                return new XML(document.DocumentElement);
            }
            catch
            {
                Debug.Log("Can not load XML from string:\n" + xmlString);
                return new XML();
            }
        }

        /// <summary>
        /// Removes this element from the XML.
        /// </summary>
        public void Remove()
        {
            if (_element?.ParentNode == null) return;
            _element.ParentNode.RemoveChild(_element);
        }

        /// <summary>
        /// Removes child element from the XML.
        /// </summary>
        /// <param name="childName">Name of child element.</param>
        public void Remove(string childName)
        {
            if (!hasChildNodes) return;
#if !NETFX_CORE
            _element.RemoveChild(_element[childName]);
#else
            _element.RemoveChild(GetFirstChild(_element, childName));
#endif
        }

        /// <summary>
        /// Removes child element from the XML.
        /// </summary>
        /// <param name="childIndex">Index of child element.</param>
        public void Remove(int childIndex)
        {
            if (!hasChildNodes) return;
            if (childIndex < 0 || childIndex >= _element.ChildNodes.Count) return;
            _element.RemoveChild(_element.ChildNodes[childIndex]);
        }

        /// <summary>
        /// Sets the value of the element.
        /// </summary>
        /// <param name="value">Value of element.</param>
        private void SetChild(string value)
        {
            if (_element == null || _document == null) return;
            _element.AppendChild(_document.CreateTextNode(value));
        }

        /// <summary>
        /// Gets the value of the element as string.
        /// </summary>
        /// <returns>Value of the element as string.</returns>
        public string Value()
        {
            return Value<string>();
        }

        /// <summary>
        /// Gets the value of the element as the specified type.
        /// </summary>
        /// <typeparam name="T">Type of element.</typeparam>
        /// <returns>Value as the specified type.</returns>
        public T Value<T>()
        {
            return Get<T>(_element);
        }
    }
}