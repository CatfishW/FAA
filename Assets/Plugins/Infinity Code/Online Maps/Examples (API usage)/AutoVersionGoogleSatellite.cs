/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of automatic versioning of Google Satellite.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Auto Version Google Satellite")]
    public class AutoVersionGoogleSatellite : MonoBehaviour
    {
        /// <summary>
        /// Key for storing the last version of Google Satellite.
        /// </summary>
        private const string VERSION_KEY = "LastGoogleSatelliteVersion";
        
        /// <summary>
        /// Key for storing the last date of checking the version of Google Satellite.
        /// </summary>
        private const string LAST_CHECK_KEY = "LastGoogleSatelliteVersionCheckDate";
        
        /// <summary>
        /// Reference to the map.
        /// </summary>
        public Map map;

        private MapType mapType;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            // Find the Google Satellite map type.
            mapType = TileProvider.FindMapType("google.satellite");
            
            // Load the last known version number.
            string version = PlayerPrefs.GetString(VERSION_KEY, "995");
            
            // Set the version number to the map.
            map.activeType["version"] = version;
            
            // Get the date of the last check.
            string lastCheckDate = PlayerPrefs.GetString(LAST_CHECK_KEY, "2023-07-26");
            DateTime lastCheckDateTime = DateTime.ParseExact(lastCheckDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime currentDate = DateTime.Now;
            TimeSpan difference = currentDate - lastCheckDateTime;
            
            // If the last check was more than 3 days ago, then check the version number.
            if (difference.TotalDays > 3)
            {
                // Send a request to the Google Maps API.
                WebRequest www = new WebRequest("https://maps.googleapis.com/maps/api/js");
                www.OnComplete += OnRequestComplete;
            }
        }

        /// <summary>
        /// Event that occurs when the request is completed.
        /// </summary>
        /// <param name="www">Reference to the request.</param>
        private void OnRequestComplete(WebRequest www)
        {
            // If there was an error, then exit the method.
            if (www.hasError) return;
            
            // Get the response text.
            string response = www.text;
            
            // Find the version number in the response text.
            Match match = Regex.Match(response, @"kh\?v=(\d+)");
            
            // If the version number was not found, then exit the method.
            if (!match.Success) return;
            
            // Get the version number.
            string version = match.Groups[1].Value;
            
            // Save the version number and the date of the last check.
            PlayerPrefs.SetString(VERSION_KEY, version);
            PlayerPrefs.SetString(LAST_CHECK_KEY, DateTime.Now.ToString("yyyy-MM-dd"));
            
            // Set the version number to the map.
            mapType["version"] = version;
        }
    }
}