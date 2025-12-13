/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections;
using System.Text;
using OnlineMaps;
using UnityEngine;
using UnityEngine.Networking;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of using Google Tiles API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "GoogleTilesAPI")]
    public class GoogleTilesAPI : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Map type
        /// </summary>
        public string mapType = "roadmap";
        
        /// <summary>
        /// Language
        /// </summary>
        public string language = "ja-JP";
        
        /// <summary>
        /// Region
        /// </summary>
        public string region = "region";

        /// <summary>
        /// URL of the request
        /// </summary>
        private string url = "https://www.googleapis.com/tile/v1/tiles/{zoom}/{x}/{y}?session={sessiontoken}&key={apikey}";
        
        /// <summary>
        /// Session token
        /// </summary>
        private string sessiontoken;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create a new map type
            TileProvider.CreateMapType("google.tilesapi", url);
            
            // Handle events to replace tokens and start downloading tiles
            Tile.OnReplaceURLToken += OnReplaceUrlToken;
            TileManager.OnStartDownloadTile += OnStartDownloadTile;

            // Select map type
            Map.instance.mapType = "google.tilesapi";

            // Start getting session token
            StartCoroutine(GetSessionToken());
        }

        /// <summary>
        /// Gets session token
        /// </summary>
        private IEnumerator GetSessionToken()
        {
            // Create json parameters for request
            JSONObject jReq = new JSONObject();
            jReq.Add("mapType", mapType);
            jReq.Add("language", language);
            jReq.Add("region", region);
            
            // Convert json to bytes
            byte[] requestBytes = Encoding.UTF8.GetBytes(jReq.ToString());

            // Create request
            UnityWebRequest www = new UnityWebRequest("https://tile.googleapis.com/tile/v1/createSession?key=" + KeyManager.GoogleMaps(), "POST");
            www.SetRequestHeader("Content-Type", "application/json");
            www.uploadHandler = new UploadHandlerRaw(requestBytes);
            www.downloadHandler = new DownloadHandlerBuffer();

            // Send request
            yield return www.SendWebRequest();

            // If there was an error, then exit the method.
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error + "\n" + Encoding.UTF8.GetString(www.downloadHandler.data));
                yield break;
            }

            // Get session token from response
            string response = Encoding.UTF8.GetString(www.downloadHandler.data);
            JSONItem json = JSON.Parse(response);
            sessiontoken = json.V<string>("session");

            // Iterate through all tiles to restart downloading
            map.tileManager.tiles.ForEach(t =>
            {
                if (t.status == TileStatus.error) t.status = TileStatus.idle;
            });

            // Redraw map
            map.Redraw();
        }

        /// <summary>
        /// Replaces tokens in the URL
        /// </summary>
        /// <param name="tile">Tile</param>
        /// <param name="token">Token</param>
        /// <returns>Vale of token</returns>
        private string OnReplaceUrlToken(Tile tile, string token)
        {
            if (token == "sessiontoken") return sessiontoken;
            if (token == "apikey") return KeyManager.GoogleMaps();
            return null;
        }

        /// <summary>
        /// Occurs when the tile starts downloading.
        /// </summary>
        /// <param name="tile">Tile</param>
        private void OnStartDownloadTile(Tile tile)
        {
            // If there is no session token, mark the tile as an error and exit the method.
            if (string.IsNullOrEmpty(sessiontoken))
            {
                tile.status = TileStatus.error;
                return;
            }

            // Pass the tile to built-in tile downloader
            TileManager.StartDownloadTile(tile);
        }
    }
}