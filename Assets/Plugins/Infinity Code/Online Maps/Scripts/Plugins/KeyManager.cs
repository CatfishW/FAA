/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Stores keys to all supported services and automatically uses them in requests.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Key Manager")]
    [Plugin("Key Manager", true)]
    public class KeyManager: MonoBehaviour
    {
        private static KeyManager instance;

        /// <summary>
        /// AMap key
        /// </summary>
        public string amap;

        /// <summary>
        /// ArcGIS key
        /// </summary>
        public string arcgisKey;

        /// <summary>
        /// Bing Maps key
        /// </summary>
        public string bingMaps;

        /// <summary>
        /// Google Maps key
        /// </summary>
        public string googleMaps;

        /// <summary>
        /// HERE API key
        /// </summary>
        public string hereApiKey;

        /// <summary>
        /// Here App Code
        /// </summary>
        public string hereAppCode;

        /// <summary>
        /// Here App ID
        /// </summary>
        public string hereAppID;

        /// <summary>
        /// MapTiler key
        /// </summary>
        public string mapTiler;

        /// <summary>
        /// Mapbox Access Token
        /// </summary>
        public string mapboxAccessToken;

        /// <summary>
        /// Open Route Service key
        /// </summary>
        public string openRouteService;

        /// <summary>
        /// QQ key
        /// </summary>
        public string qq;
        
        /// <summary>
        /// TomTom key
        /// </summary>
        public string tomTom;

        /// <summary>
        /// What 3 Words key
        /// </summary>
        public string what3Words;

        /// <summary>
        /// Is there a key for AMap
        /// </summary>
        public static bool hasAMap => !string.IsNullOrEmpty(AMap());

        /// <summary>
        /// Is there a key for ArcGIS
        /// </summary>
        public static bool hasArcGISKey => !string.IsNullOrEmpty(ArcGIS());

        /// <summary>
        /// Is there a key for Bing Maps
        /// </summary>
        public static bool hasBingMaps => !string.IsNullOrEmpty(BingMaps());

        /// <summary>
        /// Is there a key for Google Maps
        /// </summary>
        public static bool hasGoogleMaps => !string.IsNullOrEmpty(GoogleMaps());

        /// <summary>
        /// Is there a app id and app code for Here
        /// </summary>
        public static bool hasHere => !string.IsNullOrEmpty(HereAppCode()) && !string.IsNullOrEmpty(HereAppID());

        /// <summary>
        /// Is there a key for Here
        /// </summary>
        public static bool hasHereKey => !string.IsNullOrEmpty(HereApiKey());

        /// <summary>
        /// Is there an access token for Mapbox
        /// </summary>
        public static bool hasMapbox => !string.IsNullOrEmpty(Mapbox());
        
        /// <summary>
        /// Is there a key for MapTiler
        /// </summary>
        public static bool hasMapTiler => !string.IsNullOrEmpty(MapTiler());

        /// <summary>
        /// Is there a key for Open Route Service
        /// </summary>
        public static bool hasOpenRouteService => !string.IsNullOrEmpty(OpenRouteService());

        /// <summary>
        /// Is there a key for QQ
        /// </summary>
        public static bool hasQQ => !string.IsNullOrEmpty(QQ());
        
        /// <summary>
        /// Is there a key for TomTom
        /// </summary>
        public static bool hasTomTom => !string.IsNullOrEmpty(TomTom());

        /// <summary>
        /// Is there a key for What 3 Words
        /// </summary>
        public static bool hasWhat3Words => !string.IsNullOrEmpty(What3Words());

        private void OnEnable()
        {
            instance = this;
        }

        /// <summary>
        /// Returns the key for AMap if present
        /// </summary>
        /// <returns>Key for AMap or null</returns>
        public static string AMap() => instance?.amap;

        /// <summary>
        /// Returns the key for ArcGIS if present
        /// </summary>
        /// <returns>Key for ArcGIS or null</returns>
        public static string ArcGIS() => instance?.arcgisKey;

        /// <summary>
        /// Returns the key for Bing Maps if present
        /// </summary>
        /// <returns>Key for Bing Maps or null</returns>
        public static string BingMaps() => instance?.bingMaps;

        /// <summary>
        /// Returns the key for Google Maps if present
        /// </summary>
        /// <returns>Key for Google Maps or null</returns>
        public static string GoogleMaps() => instance?.googleMaps;

        /// <summary>
        /// Returns Here Api Key if present
        /// </summary>
        /// <returns>Here Api Key or null</returns>
        public static string HereApiKey() => instance?.hereApiKey;

        /// <summary>
        /// Returns Here App Code if present
        /// </summary>
        /// <returns>Here App Code or null</returns>
        public static string HereAppCode() => instance?.hereAppCode;

        /// <summary>
        /// Returns Here App ID if present
        /// </summary>
        /// <returns>Here App ID or null</returns>
        public static string HereAppID() => instance?.hereAppID;

        /// <summary>
        /// Returns Mapbox Access Token if present
        /// </summary>
        /// <returns>Mapbox Access Token or null</returns>
        public static string Mapbox() => instance?.mapboxAccessToken;
        
        /// <summary>
        /// Returns the key for MapTiler if present
        /// </summary>
        /// <returns>Key for MapTiler or null</returns>
        public static string MapTiler() => instance?.mapTiler;

        /// <summary>
        /// Returns the key for Open Route Service if present
        /// </summary>
        /// <returns>Key for Open Route Service or null</returns>
        public static string OpenRouteService() => instance?.openRouteService;

        /// <summary>
        /// Returns the key for QQ if present
        /// </summary>
        /// <returns>Key for QQ or null</returns>
        public static string QQ() => instance?.qq;
        
        /// <summary>
        /// Returns the key for TomTom if present
        /// </summary>
        /// <returns>Key for TomTom or null</returns>
        public static string TomTom() => instance?.tomTom;

        /// <summary>
        /// Returns the key for What 3 Words if present
        /// </summary>
        /// <returns>Key for What 3 Words or null</returns>
        public static string What3Words() => instance?.what3Words;
    }
}