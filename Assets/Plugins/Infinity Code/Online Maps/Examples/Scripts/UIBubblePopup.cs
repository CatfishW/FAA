/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMapsDemos
{
    /// <summary>
    /// Example is how to use a combination of data from Google Places API on bubble popup.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Demos/UIBubblePopup")]
    public class UIBubblePopup : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control
        /// </summary>
        public ControlBaseDynamicMesh control;

        /// <summary>
        /// Reference to the touch forwarder
        /// </summary>
        public RawImageTouchForwarder forwarder;
        
        /// <summary>
        /// Root canvas
        /// </summary>
        public Canvas canvas;

        /// <summary>
        /// Bubble popup
        /// </summary>
        public GameObject bubble;

        /// <summary>
        /// Title text
        /// </summary>
        public Text title;

        /// <summary>
        /// Address text
        /// </summary>
        public Text address;

        /// <summary>
        /// Photo RawImage
        /// </summary>
        public RawImage photo;

        /// <summary>
        /// Array of data for markers
        /// </summary>
        public CData[] datas;

        /// <summary>
        /// Reference to active marker
        /// </summary>
        private Marker2D targetMarker;

        /// <summary>
        /// This method is called when downloading photo texture.
        /// </summary>
        /// <param name="request">Web request</param>
        private void OnDownloadPhotoComplete(WebRequest request)
        {
            Texture2D texture = new Texture2D(1, 1);
            request.LoadImageIntoTexture(texture);

            // Set place texture to bubble popup
            photo.texture = texture;
        }

        /// <summary>
        /// This method is called by clicking on the map
        /// </summary>
        private void OnMapClick()
        {
            // Remove active marker reference
            targetMarker = null;

            // Hide the popup
            bubble.SetActive(false);
        }

        /// <summary>
        /// This method is called by clicking on the marker
        /// </summary>
        /// <param name="marker">The marker on which clicked</param>
        private void OnMarkerClick(Marker marker)
        {
            // Set active marker reference
            targetMarker = marker as Marker2D;

            // Get a result item from instance of the marker
            CData data = marker.GetData<CData>("data");
            if (data == null) return;

            // Show the popup
            bubble.SetActive(true);

            // Set title and address
            title.text = data.title;
            address.text = data.address;

            // Destroy the previous photo
            if (photo.texture)
            {
                Utils.Destroy(photo.texture);
                photo.texture = null;
            }

            WebRequest request = new WebRequest(data.photo_url);
            request.OnComplete += OnDownloadPhotoComplete;

            // Initial update position
            UpdateBubblePosition();
        }

        /// <summary>
        /// Start is called on the frame when a script is enabled just before any of the Update methods are called the first time.
        /// </summary>
        private void Start()
        {
            if (!control && !(control = ControlBaseDynamicMesh.instance))
            {
                Debug.LogError("ControlBaseDynamicMesh instance not found");
                return;
            }
            
            // Subscribe to events of the map 
            control.map.OnLocationChanged += UpdateBubblePosition;
            control.map.OnZoomChanged += UpdateBubblePosition;
            control.OnClick += OnMapClick;
            control.OnMeshUpdated += UpdateBubblePosition;

            if (CameraOrbit.instance != null)
            {
                CameraOrbit.instance.OnCameraControl += UpdateBubblePosition;
            }

            if (datas != null)
            {
                foreach (CData data in datas)
                {
                    Marker2D marker = Marker2DManager.CreateItem(data.longitude, data.latitude);
                    marker["data"] = data;
                    marker.OnClick += OnMarkerClick;
                }
            }


            // Initial hide popup
            bubble.SetActive(false);
        }

        /// <summary>
        /// Updates the popup position
        /// </summary>
        private void UpdateBubblePosition()
        {
            // If no marker is selected then exit.
            if (targetMarker == null) return;

            // Hide the popup if the marker is outside the map view
            if (!targetMarker.inMapView)
            {
                if (bubble.activeSelf) bubble.SetActive(false);
            }
            else if (!bubble.activeSelf) bubble.SetActive(true);

            // Convert the coordinates of the marker to the screen position.
            Vector2 screenPosition = targetMarker.location.ToScreen(control);

            if (forwarder)
            {
                screenPosition = forwarder.MapToForwarderSpace(screenPosition);
            }

            // Add marker height
            screenPosition.y += targetMarker.height;

            // Get a local position inside the canvas.
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPosition, null, out point);

            // Set local position of the popup
            (bubble.transform as RectTransform).localPosition = point;
        }

        [Serializable]
        public class CData
        {
            public string title;
            public string address;
            public string photo_url;
            public double longitude;
            public double latitude;
        }
    }
}