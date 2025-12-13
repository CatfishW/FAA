/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of work with What3Words
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "What3WordsExample")]
    public class What3WordsExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        private string words = "";

        private void OnGUI()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            words = GUILayout.TextField(words, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Forward"))
            {
                // Forward geocodes a 3 word address to a position, expressed as coordinates of latitude and longitude.
                new What3WordsForwardRequest(words)
                    .HandleResult(OnForwardResult) // Subscribes to OnResult event
                    .Send(); // Sends the request
            }
            if (GUILayout.Button("Suggestions"))
            {
                // Returns a list of 3 word addresses based on user input and other parameters.
                new What3WordsAutoSuggestRequest(words)
                {
                    focus = map.view.center
                }.HandleResult(OnSuggestResult) // Subscribes to OnResult event
                    .Send(); // Sends the request
            }
            if (GUILayout.Button("Blends"))
            {
                // Returns a blend of the three most relevant 3 word address candidates 
                // for a given location, based on a full or partial 3 word address.
                // The specified 3 word address may either be a full 3 word address or 
                // a partial 3 word address containing the first 2 words in full and 
                // at least 1 character of the 3rd word.StandardBlend provides the search logic 
                // that powers the search box on map.what3words.com and in the what3words mobile apps.
                new What3WordsStandardBlendRequest(words)
                {
                    focus = map.view.center
                }.HandleResult(OnSuggestResult) // Subscribes to OnResult event
                    .Send(); // Sends the request
            }
            if (GUILayout.Button("Grid"))
            {
                // Returns a section of the 3m x 3m what3words grid for a given area.
                new What3WordsGridRequest(map.view.rect)
                    .HandleResult(OnGridResult) // Subscribes to OnResult event
                    .Send(); // Sends the request
            }
            if (GUILayout.Button("Languages"))
            {
                // Retrieves a list of the currently loaded and available 3 word address languages.
                new What3WordsGetLanguagesRequest()
                    .HandleResult(OnLanguagesResult) // Subscribes to OnResult event
                    .Send(); // Sends the request
            }

        }

        private void Start()
        {
            ControlBase.instance.OnClick += OnMapClick;
        }

        private void OnMapClick()
        {
            // Reverse geocodes coordinates, expressed as latitude and longitude to a 3 word address.
            new What3WordsReverseRequest(map.view.center).OnResult += OnReverseComplete;
        }

        private void OnReverseComplete(What3WordsFRResult result)
        {
            words = result.words;

            Debug.Log(JSON.Serialize(result).ToString());
        }

        private void OnForwardResult(What3WordsFRResult result)
        {
            map.view.center = result.geometry;
            Debug.Log(JSON.Serialize(result).ToString());
        }

        private void OnSuggestResult(What3WordsSBResult result)
        {
            Debug.Log(JSON.Serialize(result).ToString());
        }

        private void OnGridResult(What3WordsGridResult result)
        {
            Debug.Log(JSON.Serialize(result).ToString());
        }

        private void OnLanguagesResult(What3WordsLanguagesResult result)
        {
            Debug.Log(JSON.Serialize(result).ToString());
        }
    }
}