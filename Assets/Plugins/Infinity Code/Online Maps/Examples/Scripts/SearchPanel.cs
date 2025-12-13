/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OnlineMapsDemos
{
    [AddComponentMenu("Infinity Code/Online Maps/Demos/Search Panel")]
    public class SearchPanel : MonoBehaviour
    {
        /// <summary>
        /// (Optional) Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// Reference to the input field.
        /// </summary>
        public InputField inputField;

        /// <summary>
        /// Indicates whether to use autocomplete.
        /// </summary>
        public bool useAutocomplete = false;

        /// <summary>
        /// Reference to the autocomplete container.
        /// </summary>
        public RectTransform autocompleteContainer;

        /// <summary>
        /// Reference to the autocomplete item prefab.
        /// </summary>
        public GameObject autocompleteItemPrefab;

        /// <summary>
        /// Marker that shows the search result on the map.
        /// </summary>
        private Marker2D marker;

        /// <summary>
        /// Hides the autocomplete container if the mouse is not over it.
        /// </summary>
        private void HideAutocomplete()
        {
            if (!useAutocomplete) return;
            if (!autocompleteContainer) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(autocompleteContainer, InputManager.mousePosition)) return;

            if (autocompleteContainer) autocompleteContainer.gameObject.SetActive(false);
        }

        /// <summary>
        /// This method is called when the autocomplete request is completed.
        /// </summary>
        /// <param name="results">Array of results</param>
        private void OnAutocompleteResult(GooglePlacesAutocompleteResult[] results)
        {
            if (!autocompleteContainer || !autocompleteItemPrefab) return;

            if (results == null || results.Length == 0)
            {
                autocompleteContainer.gameObject.SetActive(false);
                return;
            }

            autocompleteContainer.gameObject.SetActive(true);
            foreach (Transform t in autocompleteContainer) Destroy(t.gameObject);

            float y = 0;

            foreach (GooglePlacesAutocompleteResult result in results)
            {
                GameObject item = Instantiate(autocompleteItemPrefab, autocompleteContainer, false);
                item.GetComponentInChildren<Text>().text = result.description;
                item.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    inputField.text = result.description;
                    Search();
                    inputField.ActivateInputField();
                });


                RectTransform rectTransform = item.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(0, -y);
                y += rectTransform.rect.height;
            }

            RectTransform containerRectTransform = autocompleteContainer.GetComponent<RectTransform>();
            containerRectTransform.sizeDelta = new Vector2(containerRectTransform.sizeDelta.x, y);
        }

        /// <summary>
        /// This method is called when the geocoding request is completed.
        /// </summary>
        /// <param name="results">Array of results</param>
        private void OnGeocodingResult(GoogleGeocodingResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                Debug.Log("Location not found.");
                return;
            }

            GoogleGeocodingResult r = results[0];
            map.view.center = r.geometry_location;

            GeoPoint[] points = { r.geometry_bounds_northeast, r.geometry_bounds_southwest };
            (GeoPoint _, int zoom) = GeoMath.CenterPointAndZoom(points);
            map.view.intZoom = zoom;

            if (marker == null) marker = map.marker2DManager.Create(r.geometry_location, r.formatted_address);
            else
            {
                marker.location = r.geometry_location;
                marker.label = r.formatted_address;
            }
        }

        /// <summary>
        /// This method is called when the input field text is changed.
        /// </summary>
        public void OnInputChanged()
        {
            if (!useAutocomplete) return;
            if (!KeyManager.hasGoogleMaps) return;

            if (inputField.text.Length < 3)
            {
                if (autocompleteContainer) autocompleteContainer.gameObject.SetActive(false);
                return;
            }

            new GooglePlacesAutocompleteRequest(inputField.text)
                .HandleResult(OnAutocompleteResult)
                .Send();
        }

        /// <summary>
        /// This method is called when the search button is pressed.
        /// </summary>
        public void Search()
        {
            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please enter Map / Key Manager / Google Maps");
                return;
            }

            if (!inputField) return;
            if (inputField.text.Length < 3) return;

            string locationName = inputField.text;

            new GoogleGeocodingRequest(locationName)
                .HandleResult(OnGeocodingResult)
                .Send();
        }

        /// <summary>
        /// Shows the autocomplete container.
        /// </summary>
        private void ShowAutocomplete()
        {
            if (!useAutocomplete) return;
            if (!autocompleteContainer) return;
            if (autocompleteContainer.transform.childCount == 0) return;

            autocompleteContainer.gameObject.SetActive(true);
        }

        private void Start()
        {
            if (!map) map = Map.instance;

            EventTrigger trigger = inputField.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry lostFocusEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            lostFocusEntry.callback.AddListener((data) => { HideAutocomplete(); });
            trigger.triggers.Add(lostFocusEntry);

            EventTrigger.Entry gainFocusEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            gainFocusEntry.callback.AddListener((data) => { ShowAutocomplete(); });
            trigger.triggers.Add(gainFocusEntry);
        }

        private void Update()
        {
            EventSystem eventSystem = EventSystem.current;
            if ((InputManager.GetKeyUp(KeyCode.KeypadEnter) || InputManager.GetKeyUp(KeyCode.Return))
                && eventSystem.currentSelectedGameObject == inputField.gameObject)
            {
                Search();
            }
        }
    }
}