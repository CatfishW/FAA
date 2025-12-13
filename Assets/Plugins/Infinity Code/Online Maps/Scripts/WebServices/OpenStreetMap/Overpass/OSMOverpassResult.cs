/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Represents the result of an OSM Overpass API query.
    /// </summary>
    public class OSMOverpassResult
    {
        /// <summary>
        /// Dictionary of nodes with their IDs as keys.
        /// </summary>
        public readonly Dictionary<string, Node> nodes;

        /// <summary>
        /// Dictionary of ways with their IDs as keys.
        /// </summary>
        public readonly Dictionary<string, Way> ways;

        /// <summary>
        /// List of relations.
        /// </summary>
        public readonly List<Relation> relations;

        /// <summary>
        /// Initializes a new instance of the OSMOverpassResult class with the specified response.
        /// </summary>
        /// <param name="response">The XML response from the OSM Overpass API.</param>
        public OSMOverpassResult(string response)
        {
            int i = 0;
            OSMXMLNode rootNode = new OSMXMLNode(response, ref i);

            if (rootNode.children == null)
            {
                nodes = new Dictionary<string, Node>();
                ways = new Dictionary<string, Way>();
                relations = new List<Relation>();
                return;
            }

            int countNodes = 0;
            int countWays = 0;
            int countRelations = 0;

            for (int j = 0; j < rootNode.children.Count; j++)
            {
                OSMXMLNode node = rootNode.children[j];
                if (node.name == "node") countNodes++;
                else if (node.name == "way") countWays++;
                else if (node.name == "relation") countRelations++;
            }

            nodes = new Dictionary<string, Node>(countNodes);
            ways = new Dictionary<string, Way>(countWays);
            relations = new List<Relation>(countRelations);

            for (int j = 0; j < rootNode.children.Count; j++)
            {
                OSMXMLNode node = rootNode.children[j];
                if (node.name == "node") nodes.Add(node.GetAttribute("id"), new Node(node));
                else if (node.name == "way")
                {
                    Way way = new Way(node);
                    ways.TryAdd(way.id, way);
                }
                else if (node.name == "relation") relations.Add(new Relation(node));
            }
        }
        
        public static OSMOverpassResult Parse(string response)
        {
            return new OSMOverpassResult(response);
        }

        /// <summary>
        /// Fast XML parser optimized for OSM response.<br/>
        /// It has very limited support for XML and is not recommended for parsing any data except OSM response.
        /// </summary>
        public class OSMXMLNode
        {
            /// <summary>
            /// Noda name
            /// </summary>
            public string name;

            /// <summary>
            /// List of child nodes
            /// </summary>
            public List<OSMXMLNode> children;

            /// <summary>
            /// Array of attributes key
            /// </summary>
            public string[] attributeKeys;

            /// <summary>
            /// Array of attributes value
            /// </summary>
            public string[] attributeValues;

            /// <summary>
            /// Value of node
            /// </summary>
            public string value;

            private int l;
            private int attributeCapacity;
            private int attributeCount;

            /// <summary>
            /// Parse XML string.
            /// </summary>
            /// <param name="s">XML string</param>
            /// <param name="i">Index of current character</param>
            public OSMXMLNode(string s, ref int i)
            {
                l = s.Length;
                int it = 0;
                while (i < l)
                {
                    if (it++ > 1000)
                    {
                        Debug.Log("it > 1000");
                        return;
                    }

                    char c = s[i];
                    if (c == '<')
                    {
                        i++;
                        if (s[i] == '?')
                        {
                            i++;
                            int it2 = 0;
                            while (i < l)
                            {
                                if (it2++ > 100)
                                {
                                    Debug.Log("it2 > 100");
                                    return;
                                }

                                if (s[i] == '?' && s[i + 1] == '>') break;
                                i++;
                            }
                        }
                        else
                        {
                            char lastChar;
                            int sni, eni;
                            GetNameIndices(s, i, out sni, out eni, out lastChar);
                            name = s.Substring(sni, eni - sni);
                            i = eni + 1;
                            if (lastChar == ' ') ParseAttributes(s, ref i, out lastChar);
                            if (lastChar == ' ')
                            {
                                int it2 = 0;
                                while (i < l)
                                {
                                    c = s[i];
                                    if (it2++ > 100)
                                    {
                                        Debug.Log("it2 > 100");
                                        return;
                                    }

                                    if (c == '/')
                                    {
                                        lastChar = c;
                                        i += 2;
                                        break;
                                    }

                                    if (c == '>')
                                    {
                                        lastChar = c;
                                        i++;
                                        break;
                                    }
                                }
                            }

                            if (lastChar == '>')
                            {
                                if (s[i] == '>') i++;
                                ParseValue(s, ref i);
                            }

                            if (lastChar == '/') i += 2;
                            break;
                        }
                    }

                    i++;
                }
            }

            /// <summary>
            /// Gets the value of the specified attribute key.
            /// </summary>
            /// <param name="key">The attribute key.</param>
            /// <returns>The attribute value if found; otherwise, null.</returns>
            public string GetAttribute(string key)
            {
                for (int i = 0; i < attributeKeys.Length; i++)
                {
                    if (key == attributeKeys[i]) return attributeValues[i];
                }

                return null;
            }

            /// <summary>
            /// Gets the start and end indices of the attribute value in the XML string.
            /// </summary>
            /// <param name="s">The XML string.</param>
            /// <param name="i">The current index in the XML string.</param>
            /// <param name="svi">The start index of the attribute value.</param>
            /// <param name="evi">The end index of the attribute value.</param>
            private void GetAttributeValue(string s, int i, out int svi, out int evi)
            {
                svi = -1;
                evi = -1;
                int it = 0;
                while (i < l)
                {
                    if (it++ > 1000)
                    {
                        Debug.Log("it > 1000");
                        return;
                    }

                    if (s[i] == '"')
                    {
                        if (svi == -1) svi = i + 1;
                        else
                        {
                            evi = i;
                            return;
                        }
                    }

                    i++;
                }
            }

            private void GetNameIndices(string s, int i, out int startIndex, out int endIndex, out char lastChar)
            {
                int it = 0;
                startIndex = -1;
                endIndex = -1;
                while (i < l)
                {
                    if (it++ > 100)
                    {
                        Debug.Log("it > 100");
                        lastChar = (char)0;
                        return;
                    }

                    char c = s[i];
                    if (c == ' ')
                    {
                        if (startIndex != -1)
                        {
                            endIndex = i;
                            lastChar = c;
                            return;
                        }
                    }
                    else if (c == '/' || c == '>' || c == '=')
                    {
                        endIndex = i;
                        lastChar = c;
                        return;
                    }
                    else if (startIndex == -1) startIndex = i;

                    i++;
                }

                lastChar = (char)0;
            }

            private void ParseAttributes(string s, ref int i, out char lastChar)
            {
                int j = i;
                int countQuotes = 0;
                lastChar = (char)0;
                bool ignoreEnds = false;
                while (j < l)
                {
                    char c = s[j];
                    if (c == '"')
                    {
                        if (s[j - 1] != '\\')
                        {
                            ignoreEnds = !ignoreEnds;
                            countQuotes++;
                        }
                    }
                    else if (!ignoreEnds && c == '/' || c == '>')
                    {
                        lastChar = c;
                        break;
                    }

                    j++;
                }

                if (countQuotes == 0)
                {
                    i = j;
                    return;
                }

                if (countQuotes % 2 != 0)
                {
                    Debug.Log("Something wrong");
                    i = j;
                    return;
                }

                attributeCapacity = countQuotes / 2;
                attributeCount = 0;
                attributeKeys = new string[attributeCapacity];
                attributeValues = new string[attributeCapacity];

                int it = 0;
                while (true)
                {
                    if (it++ > 100)
                    {
                        Debug.Log("it > 100");
                        lastChar = (char)0;
                        return;
                    }

                    if (!ParseAttribute(s, ref i, out lastChar)) break;
                    if (lastChar == '/' || lastChar == '>') break;
                }
            }

            private bool ParseAttribute(string s, ref int i, out char lastChar)
            {
                int si, ei;
                GetNameIndices(s, i, out si, out ei, out lastChar);
                if (ei != -1)
                {
                    string key = s.Substring(si, ei - si);
                    i = ei + 1;
                    GetAttributeValue(s, i, out si, out ei);
                    string value = s.Substring(si, ei - si);
                    attributeKeys[attributeCount] = key;
                    attributeValues[attributeCount] = value;
                    attributeCount++;
                    i = ei + 1;
                    lastChar = s[i];
                    return true;
                }

                return false;
            }

            private void ParseChild(string s, ref int i)
            {
                OSMXMLNode child = new OSMXMLNode(s, ref i);
                if (children == null) children = new List<OSMXMLNode>();
                children.Add(child);
            }

            private void ParseValue(string s, ref int i)
            {
                int it = 0;
                while (i < l)
                {
                    if (it++ > 1000000)
                    {
                        Debug.Log("it > 1000000");
                        return;
                    }

                    char c = s[i];
                    if (c == '<')
                    {
                        if (s[i + 1] == '/')
                        {
                            int it2 = 0;
                            while (i < l)
                            {
                                if (it2++ > 1000)
                                {
                                    Debug.Log("it2 > 1000");
                                    return;
                                }

                                if (s[i] == '>')
                                {
                                    i++;
                                    return;
                                }

                                i++;
                            }
                        }
                        else ParseChild(s, ref i);
                    }
                    else if (c == ' ' || c == '\n' || c == '\t')
                    {
                        // Ignore
                    }
                    else
                    {
                        //Load string value
                        int si = i;
                        int ei = -1;
                        int it2 = 0;
                        while (i < l)
                        {
                            if (it2++ > 1000)
                            {
                                Debug.Log("it2 > 1000");
                                return;
                            }

                            if (s[i] == '<' && s[i + 1] == '/')
                            {
                                ei = i;
                                break;
                            }

                            i++;
                        }

                        value = s.Substring(si, ei - si);
                        it2 = 0;
                        while (i < l)
                        {
                            if (it2++ > 1000)
                            {
                                Debug.Log("it2 > 1000");
                                return;
                            }

                            if (s[i] == '>')
                            {
                                i++;
                                return;
                            }

                            i++;
                        }
                    }

                    i++;
                }
            }
        }

        /// <summary>
        /// The base class of Open Streen Map element.
        /// </summary>
        public abstract class OSMBase
        {
            /// <summary>
            /// Element ID
            /// </summary>
            public string id;

            /// <summary>
            /// Element tags
            /// </summary>
            public List<Tag> tags;

            /// <summary>
            /// Converts a string to a double value.
            /// </summary>
            /// <param name="s">The string to convert.</param>
            /// <returns>The double value represented by the string.</returns>
            protected static double CreateDouble(string s)
            {
                long n = 0;
                bool hasDecimalPoint = false;
                bool neg = false;
                long decimalV = 1;
                for (int x = 0; x < s.Length; x++)
                {
                    char c = s[x];
                    if (c == '.') hasDecimalPoint = true;
                    else if (c == '-') neg = true;
                    else
                    {
                        n *= 10;
                        n += c - '0';
                        if (hasDecimalPoint) decimalV *= 10;
                    }
                }

                if (neg) n = -n;

                return n / (double)decimalV;
            }

            /// <summary>
            /// Disposes the resources used by the object.
            /// </summary>
            public virtual void Dispose()
            {
                tags = null;
            }

            /// <summary>
            /// Determines whether the specified object is equal to the current object.
            /// </summary>
            /// <param name="other">The object to compare with the current object.</param>
            /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
            public bool Equals(OSMBase other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(this, other)) return true;
                return id == other.id;
            }

            /// <summary>
            /// Serves as the default hash function.
            /// </summary>
            /// <returns>A hash code for the current object.</returns>
            public override int GetHashCode()
            {
                return id.GetHashCode();
            }

            /// <summary>
            /// Get tag value for the key.
            /// </summary>
            /// <param name="key">Tag key</param>
            /// <returns>Tag value</returns>
            public string GetTagValue(string key)
            {
                if (tags == null) return null;
                for (int i = 0; i < tags.Count; i++)
                {
                    Tag tag = tags[i];
                    if (tag.key == key) return tag.value;
                }

                return null;
            }

            /// <summary>
            /// Checks for the tag with the specified key and value.
            /// </summary>
            /// <param name="key">Tag key</param>
            /// <param name="value">Tag value</param>
            /// <returns>True - if successful, False - otherwise.</returns>
            public bool HasTag(string key, string value)
            {
                return tags.Any(t => t.key == key && t.value == value);
            }

            /// <summary>
            /// Checks for the tag with the specified keys.
            /// </summary>
            /// <param name="keys">Tag keys.</param>
            /// <returns>True - if successful, False - otherwise.</returns>
            public bool HasTagKey(params string[] keys)
            {
                int kl = keys.Length;
                for (int i = 0; i < tags.Count; i++)
                {
                    Tag tag = tags[i];
                    for (int k = 0; k < kl; k++)
                    {
                        if (keys[k] == tag.key) return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Checks for the tag with the specified values.
            /// </summary>
            /// <param name="values">Tag values</param>
            /// <returns>True - if successful, False - otherwise.</returns>
            public bool HasTagValue(params string[] values)
            {
                return values.Any(val => tags.Any(t => t.value == val));
            }

            /// <summary>
            /// Checks for the tag with the specified key and values.
            /// </summary>
            /// <param name="key">Tag key</param>
            /// <param name="values">Tag values</param>
            /// <returns>True - if successful, False - otherwise.</returns>
            public bool HasTags(string key, params string[] values)
            {
                return tags.Any(tag => tag.key == key && values.Any(v => v == tag.value));
            }
        }

        /// <summary>
        /// Open Street Map node element class
        /// </summary>
        public class Node : OSMBase
        {
            /// <summary>
            /// Latitude
            /// </summary>
            public readonly double latitude;

            /// <summary>
            /// Longitude
            /// </summary>
            public readonly double longitude;

            /// <summary>
            /// Initializes a new instance of the Node class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public Node(OSMXMLNode node)
            {
                id = node.GetAttribute("id");
                latitude = CreateDouble(node.GetAttribute("lat"));
                longitude = CreateDouble(node.GetAttribute("lon"));
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public Node(XML node)
            {
                id = node.A("id");
                latitude = node.A<float>("lat");
                longitude = node.A<float>("lon");

                tags = new List<Tag>(node.count);

                foreach (XML subNode in node) tags.Add(new Tag(subNode));
            }

            /// <summary>
            /// Converts this node to a MercatorPoint using the specified projection.
            /// </summary>
            /// <param name="projection">The map projection to use for conversion.</param>
            /// <returns>A MercatorPoint representing the node's coordinates.</returns>
            public MercatorPoint ToMercator(Projection projection)
            {
                return projection.LocationToMercator(longitude, latitude);
            }

            /// <summary>
            /// Implicitly converts a Node to a GeoPoint.
            /// </summary>
            /// <param name="val">The Node to convert.</param>
            /// <returns>A GeoPoint representing the Node's coordinates.</returns>
            public static implicit operator GeoPoint(Node val)
            {
                return new GeoPoint(val.longitude, val.latitude);
            }
        }

        /// <summary>
        /// Open Street Map way element class
        /// </summary>
        public class Way : OSMBase
        {
            /// <summary>
            /// List of node id;
            /// </summary>
            public List<string> nodeRefs
            {
                get => _nodeRefs;
                set => _nodeRefs = value;
            }

            private List<string> _nodeRefs;

            /// <summary>
            /// Initializes a new instance of the Way class.
            /// </summary>
            public Way()
            {
            }

            /// <summary>
            /// Initializes a new instance of the Way class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public Way(OSMXMLNode node)
            {
                id = node.GetAttribute("id");

                int countNd = 0;
                int countTags = 0;

                for (int i = 0; i < node.children.Count; i++)
                {
                    OSMXMLNode subNode = node.children[i];
                    if (subNode.name == "nd") countNd++;
                    else if (subNode.name == "tag") countTags++;
                }

                _nodeRefs = new List<string>(countNd);
                tags = new List<Tag>(countTags);

                for (int i = 0; i < node.children.Count; i++)
                {
                    OSMXMLNode subNode = node.children[i];
                    if (subNode.name == "nd") _nodeRefs.Add(subNode.GetAttribute("ref"));
                    else if (subNode.name == "tag") tags.Add(new Tag(subNode));
                }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public Way(XML node)
            {
                id = node.A("id");
                _nodeRefs = new List<string>();
                tags = new List<Tag>();

                foreach (XML subNode in node)
                {
                    if (subNode.name == "nd") _nodeRefs.Add(subNode.A("ref"));
                    else if (subNode.name == "tag") tags.Add(new Tag(subNode));
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                _nodeRefs = null;
            }

            /// <summary>
            /// Returns a list of nodes related to that way.
            /// </summary>
            /// <param name="nodes">General list of nodes</param>
            /// <returns>List of nodes related to that way</returns>
            public List<Node> GetNodes(List<Node> nodes)
            {
                List<Node> _nodes = new List<Node>();
                foreach (string nRef in nodeRefs)
                {
                    Node node = nodes.FirstOrDefault(n => n.id == nRef);
                    if (node != null) _nodes.Add(node);
                }

                return _nodes;
            }

            /// <summary>
            /// Returns a list of nodes related to that way.
            /// </summary>
            /// <param name="nodes">General dictionary of nodes</param>
            /// <returns>List of nodes related to that way</returns>
            public List<Node> GetNodes(Dictionary<string, Node> nodes)
            {
                List<Node> _nodes = new List<Node>(10);
                foreach (string nRef in nodeRefs)
                {
                    if (nodes.TryGetValue(nRef, out Node node))
                    {
                        _nodes.Add(node);
                    }
                }

                return _nodes;
            }

            /// <summary>
            /// Gets a list of nodes related to that way.
            /// </summary>
            /// <param name="nodes">General dictionary of nodes</param>
            /// <param name="usedNodes">List of nodes related to that way</param>
            public void GetNodes(Dictionary<string, Node> nodes, List<Node> usedNodes)
            {
                usedNodes.Clear();
                for (int i = 0; i < _nodeRefs.Count; i++)
                {
                    string nRef = _nodeRefs[i];
                    Node node;
                    if (nodes.TryGetValue(nRef, out node)) usedNodes.Add(node);
                }
            }
        }

        /// <summary>
        /// Open Street Map relation element class
        /// </summary>
        public class Relation : OSMBase
        {
            /// <summary>
            /// List members of relation
            /// </summary>
            public List<RelationMember> members => _members;

            private List<RelationMember> _members;

            /// <summary>
            /// Initializes a new instance of the Relation class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public Relation(OSMXMLNode node)
            {
                id = node.GetAttribute("id");
                _members = new List<RelationMember>(16);
                tags = new List<Tag>(4);

                foreach (OSMXMLNode subNode in node.children)
                {
                    if (subNode.name == "member") _members.Add(new RelationMember(subNode));
                    else if (subNode.name == "tag") tags.Add(new Tag(subNode));
                }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public Relation(XML node)
            {
                id = node.A("id");
                _members = new List<RelationMember>(16);
                tags = new List<Tag>(4);

                foreach (XML subNode in node)
                {
                    if (subNode.name == "member") _members.Add(new RelationMember(subNode));
                    else if (subNode.name == "tag") tags.Add(new Tag(subNode));
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                _members = null;
            }
        }

        /// <summary>
        /// Open Street Map relation member class
        /// </summary>
        public class RelationMember
        {
            /// <summary>
            /// ID of reference element
            /// </summary>
            public readonly string reference;

            /// <summary>
            /// Member role
            /// </summary>
            public readonly string role;

            /// <summary>
            /// Member type
            /// </summary>
            public readonly string type;

            /// <summary>
            /// Initializes a new instance of the RelationMember class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public RelationMember(OSMXMLNode node)
            {
                type = node.GetAttribute("type");
                reference = node.GetAttribute("ref");
                role = node.GetAttribute("role");
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public RelationMember(XML node)
            {
                type = node.A("type");
                reference = node.A("ref");
                role = node.A("role");
            }
        }

        /// <summary>
        /// Open Street Map element tag class
        /// </summary>
        public class Tag
        {
            /// <summary>
            /// Tag key
            /// </summary>
            public readonly string key;

            /// <summary>
            /// Tag value
            /// </summary>
            public readonly string value;

            /// <summary>
            /// Initializes a new instance of the Tag class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public Tag(OSMXMLNode node)
            {
                key = node.GetAttribute("k");
                value = node.GetAttribute("v");
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public Tag(XML node)
            {
                key = node.A("k");
                value = node.A("v");
            }

            public override string ToString()
            {
                return key + ": " + value;
            }
        }

        /// <summary>
        /// Open Street Map area element class
        /// </summary>
        public class Area : OSMBase
        {
            /// <summary>
            /// Initializes a new instance of the Area class with the specified OSM XML node.
            /// </summary>
            /// <param name="node">The OSM XML node.</param>
            public Area(OSMXMLNode node)
            {
                id = node.GetAttribute("id");
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="node">Node</param>
            public Area(XML node)
            {
                id = node.A("id");

                tags = new List<Tag>(node.count);

                foreach (XML subNode in node) tags.Add(new Tag(subNode));
            }
        }
    }
}