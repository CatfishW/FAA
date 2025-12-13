/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the use of elevation data from Bing Maps base on tiles
    /// </summary>
    [Plugin("Bing Maps Tiled Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/Bing Maps Tiled Elevation Manager")]
    public class BingMapsTiledElevationManager : TiledElevationManager<BingMapsTiledElevationManager>
    {
        protected override string cachePrefix => "bing_elevation_";
        protected override int tileWidth => 32;
        protected override int tileHeight => 32;

        public override void CancelCurrentElevationRequest()
        {
        
        }

        private void OnTileDownloaded(Tile tile, BingMapsBoundsElevationRequest request)
        {
            if (request.status == RequestStatus.error)
            {
                Debug.Log("Download error");
                if (OnElevationFails != null) OnElevationFails(request.response);
                return;
            }

            if (OnDownloadSuccess != null) OnDownloadSuccess(tile, request.www);

            short[,] elevations = new short[32, 32];
            Array ed = elevations;
            if (BingMapsElevationRequestBase.ParseElevationArray(request.response, ref ed))
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        (elevations[x, y], elevations[x, 31 - y]) = (elevations[x, 31 - y], elevations[x, y]);
                    }
                }

                SetElevationToCache(tile, elevations);
                SetElevationData(tile, elevations);
            }

            if (OnElevationUpdated != null) OnElevationUpdated();
        }

        public override void StartDownloadElevationTile(Tile tile)
        {
            //double lx, ty, rx, by;
            GeoPoint tl = map.view.projection.TileToLocation(tile.x, tile.y, tile.zoom);
            GeoPoint br = map.view.projection.TileToLocation(tile.x + 1, tile.y + 1, tile.zoom);
            GeoRect r = new GeoRect(tl, br);

            var request = new BingMapsBoundsElevationRequest(r, tileWidth, tileHeight);
            request.OnFinish += _ => OnTileDownloaded(tile, request);
            request.Send();

            if (OnElevationRequested != null) OnElevationRequested();
        }
    }
}