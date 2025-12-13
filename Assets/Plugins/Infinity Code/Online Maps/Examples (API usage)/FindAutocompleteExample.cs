/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of get place predictions from Google Autocomplete API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "FindAutocompleteExample")]
    public class FindAutocompleteExample : MonoBehaviour
    {
        private void Start()
        {
            // Makes a request to Google Places Autocomplete API.
            new GooglePlacesAutocompleteRequest("Los ang")
                .HandleResult(OnResult) // Subscribe to the event of receiving results.
                .Send(); // Send the request.
        }

        /// <summary>
        /// This method is called when a response is received.
        /// </summary>
        /// <param name="results">Array of results</param>
        private void OnResult(GooglePlacesAutocompleteResult[] results)
        {
            // If there is no result
            if (results == null || results.Length == 0)
            {
                Debug.Log("No results");
                return;
            }

            // Log description of each result.
            foreach (GooglePlacesAutocompleteResult result in results)
            {
                Debug.Log(result.description);
            }
        }
    }
}