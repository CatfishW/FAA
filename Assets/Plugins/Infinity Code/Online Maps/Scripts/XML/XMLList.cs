/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if !NETFX_CORE
using System.Xml;
#else
using Windows.Data.Xml.Dom;
#endif

using System.Collections;

namespace OnlineMaps
{
    /// <summary>
    /// Wrapper for XmlNodeList.
    /// </summary>
    public class XMLList : IEnumerable
    {
        private readonly XmlNodeList _list;

        /// <summary>
        /// Count of the elements.
        /// </summary>
        public int count
        {
            get { return _list != null ? _list.Count : 0; }
        }

        /// <summary>
        /// Reference to XmlNodeList.
        /// </summary>
        public XmlNodeList list
        {
            get { return _list; }
        }

        /// <summary>
        /// Create empty list.
        /// </summary>
        public XMLList()
        {

        }

        /// <summary>
        /// Create wrapper for XmlNodeList.
        /// </summary>
        /// <param name="list">XmlNodeList.</param>
        public XMLList(XmlNodeList list)
        {
            _list = list;
        }

        /// <summary>
        /// Get the element by index.
        /// </summary>
        /// <param name="index">Index of element.</param>
        /// <returns>Element.</returns>
        public XML this[int index]
        {
            get
            {
                if (_list == null || index < 0 || index >= _list.Count) return new XML();
                return new XML(_list[index] as XmlElement);
            }
        }

        public IEnumerator GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }
    }
}