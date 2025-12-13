/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the use of elevation data from Bing Maps
    /// </summary>
    [Plugin("Bing Maps Elevations", typeof(ControlBaseDynamicMesh), "Elevations")]
    [AddComponentMenu("Infinity Code/Online Maps/Elevations/Bing Maps Elevation Manager")]
    public class BingMapsElevationManager : SinglePartElevationManager<BingMapsElevationManager>
    {
        private BingMapsBoundsElevationRequest elevationRequest;

        public override void CancelCurrentElevationRequest() 
        {
            waitSetElevationData = false;
        
            if (elevationRequest != null)
            {
                elevationRequest.Dispose();
                elevationRequest = null;
            }
        }

        public override void RequestNewElevationData()
        {
            base.RequestNewElevationData();

            if (elevationRequest != null || waitSetElevationData) return;

            elevationBufferPosition = bufferPosition;

            MapView view = map.view;
            int countX = view.countTilesX + 2;
            int countY = view.countTilesY + 2;

            GeoPoint tl = view.projection.TileToLocation(bufferPosition.x - 1, bufferPosition.y - 1, view.intZoom);
            GeoPoint br = view.projection.TileToLocation(bufferPosition.x + countX + 1, bufferPosition.y + countY + 1, view.intZoom);
            requestRect = new GeoRect(tl, br);

            if (OnGetElevation == null)
            {
                StartDownloadElevation(requestRect);
            }
            else
            {
                waitSetElevationData = true;
                OnGetElevation(requestRect);
            }

            if (OnElevationRequested != null) OnElevationRequested();
        }

        private void OnElevationRequestComplete(string response)
        {
            const int elevationDataResolution = 32;

            try
            {
                SavePrevValues();

                bool isFirstResponse = false;
                if (elevationData == null)
                {
                    elevationData = new short[elevationDataResolution, elevationDataResolution];
                    isFirstResponse = true;
                }
                Array ed = elevationData;

                if (BingMapsElevationRequestBase.ParseElevationArray(response, ref ed))
                {
                    dataRect = requestRect;
                    elevationDataWidth = elevationDataResolution;
                    elevationDataHeight = elevationDataResolution;

                    UpdateMinMax();
                    if (OnElevationUpdated != null) OnElevationUpdated();

                    control.UpdateControl();
                }
                else
                {
                    if (isFirstResponse)
                    {
                        dataRect = requestRect;
                        elevationDataWidth = elevationDataResolution;
                        elevationDataHeight = elevationDataResolution;
                    }
                    Debug.LogWarning(response);
                    if (OnElevationFails != null) OnElevationFails(response);
                }
                elevationRequest = null;
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message + "\n" + exception.StackTrace);
                if (OnElevationFails != null) OnElevationFails(exception.Message);
            }
            map.Redraw();
        }

        protected override void Start()
        {
            base.Start();

            if (!KeyManager.hasBingMaps)
            {
                Debug.LogWarning("Missing Map / Key Manager / Bing Maps API key.");
            }
        }

        /// <summary>
        /// Starts downloading elevation data for an area
        /// </summary>
        /// <param name="rect">Area</param>
        public void StartDownloadElevation(GeoRect rect)
        {
            elevationRequest = new BingMapsBoundsElevationRequest(rect, 32, 32)
            {
                key = KeyManager.BingMaps()
            };
            elevationRequest.HandleComplete(OnElevationRequestComplete).Send();
        }
    }
}