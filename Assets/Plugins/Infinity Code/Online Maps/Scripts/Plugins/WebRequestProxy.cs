/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// A proxy for web requests, allowing customization of the request URL.
    /// </summary>
    [Plugin("WebRequest Proxy", false)]
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/WebRequest Proxy")]
    public class WebRequestProxy : MonoBehaviour, ISavable
    {
        /// <summary>
        /// The base URL for the web requests.
        /// </summary>
        public string url;

        /// <summary>
        /// Indicates if the proxy should be used only in WebGL builds.
        /// </summary>
#pragma warning disable 414
        public bool webGLOnly = true;
#pragma warning restore 414

        private void OnDisable()
        {
            WebRequest.OnInit -= OnInitRequest;
        }

        private void OnEnable()
        {
            WebRequest.OnInit -= OnInitRequest;
            WebRequest.OnInit += OnInitRequest;
        }

        private string OnInitRequest(string url)
        {
#if !UNITY_WEBGL
            if (!webGLOnly) return url;
#endif

            return this.url + url;
        }
    }
}