/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace OnlineMaps
{
    /// <summary>
    /// Controls map using User Location (GPS and compass).
    /// </summary>
    [Plugin("User Location")]
    public abstract class UserLocationBase : MonoBehaviour, ISavableAdvanced
    {
        private static UserLocationBase _baseInstance;

        /// <summary>
        /// This event is called when the user rotates the device
        /// </summary>
        public Action<float> OnCompassChanged;

        /// <summary>
        /// This event allows you to intercept receiving a GPS location
        /// </summary>
        public Func<GeoPoint> OnGetLocation;

        /// <summary>
        /// This event is called when the IP location are found
        /// </summary>
        public Action OnFindLocationByIPComplete;

        /// <summary>
        /// This event is called when changed your GPS location
        /// </summary>
        public Action<GeoPoint> OnLocationChanged;

        /// <summary>
        /// This event is called when the GPS is initialized (the first value is received) or location by IP is found
        /// </summary>
        public Action OnLocationInited;

        /// <summary>
        /// This event called after map location restored when timeout "Restore After" expires.
        /// </summary>
        public Action OnLocationRestored;

        /// <summary>
        /// Stops location updates when the user interacts with the map.
        /// </summary>
        public bool autoStopUpdateOnInput = true;

        /// <summary>
        /// Threshold of compass
        /// </summary>
        public float compassThreshold = 8;

        /// <summary>
        /// Specifies the need to create a marker that indicates the current GPS coordinates
        /// </summary>
        public bool createMarkerInUserLocation;

        /// <summary>
        /// Indicates whether to disable the emulator when used on the device
        /// </summary>
        public bool disableEmulatorInPublish = true;

        /// <summary>
        /// Emulated compass trueHeading. Do not use this field. Use UserLocation.trueHeading instead.
        /// </summary>
        public float emulatedCompass;

        /// <summary>
        /// Emulated GPS location. Do not use this field. Use UserLocation.location instead.
        /// </summary>
        public GeoPoint emulatedLocation;

        /// <summary>
        /// Specifies whether to search for a location by IP
        /// </summary>
        public bool findLocationByIP;

        /// <summary>
        /// Smooth rotation by compass. This helps to bypass the jitter.
        /// </summary>
        public bool lerpCompassValue = true;

        /// <summary>
        /// Scale of the marker
        /// </summary>
        public float markerScale = 1;

        /// <summary>
        /// Tooltip of the marker
        /// </summary>
        public string markerTooltip;

        /// <summary>
        /// Type of the marker.
        /// </summary>
        public UserLocationMarkerType markerType = UserLocationMarkerType.twoD;

        /// <summary>
        /// Align of the 2D marker
        /// </summary>
        public Align marker2DAlign = Align.Center;

        /// <summary>
        /// Texture of 2D marker
        /// </summary>
        public Texture2D marker2DTexture;

        /// <summary>
        /// Prefab of 3D marker.
        /// </summary>
        public GameObject marker3DPrefab;

        /// <summary>
        /// Marker size type.
        /// </summary>
        public Marker3D.SizeType marker3DSizeType = Marker3D.SizeType.scene;

        /// <summary>
        /// The maximum number of stored locations. It is used to calculate the speed.
        /// </summary>
        public int maxLocationCount = 3;

        /// <summary>
        /// Current GPS coordinates.<br/>
        /// Important: location not available Start, because GPS is not already initialized.<br/>
        /// Use OnLocationInited event, to detect the initialization of GPS.
        /// </summary>
        public GeoPoint location = GeoPoint.zero;

        /// <summary>
        /// Use the GPS coordinates after seconds of inactivity.
        /// </summary>
        public int restoreAfter = 10;

        /// <summary>
        /// Rotates the camera through a compass. Requires CameraOrbit component.
        /// </summary>
        public bool rotateCameraByCompass;

        /// <summary>
        /// The heading in degrees relative to the geographic North Pole.<br/>
        /// Important: location not available Start, because compass is not already initialized.<br/>
        /// Use OnCompassChanged event, to determine the initialization of compass.
        /// </summary>
        public float trueHeading;

        /// <summary>
        /// Specifies the need to update the location of the emulator by marker location.
        /// </summary>
        public bool updateEmulatedLocationByMarker;

        /// <summary>
        /// Specifies whether the script will automatically update the location
        /// </summary>
        public bool updateLocation = true;

        /// <summary>
        /// Specifies the need for marker rotation
        /// </summary>
        public bool useCompassForMarker;

        /// <summary>
        /// Specifies GPS emulator usage. Works only in Unity Editor.
        /// </summary>
        public bool useGPSEmulator;

        private Map map;

        private bool _allowUpdateLocation = true;
        private float lastLocationChangedTime;
        private bool lockDisable;
        private bool isLocationInited;

        private Marker _marker;
        protected float _speed = 0;
        private bool started;
        private SavableItem[] savableItems;

        /// <summary>
        /// Instance of UserLocation base.
        /// </summary>
        public static UserLocationBase baseInstance => _baseInstance;

        /// <summary>
        /// Instance of marker.
        /// </summary>
        public static Marker marker
        {
            get => _baseInstance._marker;
            set => _baseInstance._marker = value;
        }

        /// <summary>
        /// Is it allowed to update the location.
        /// </summary>
        public bool allowUpdateLocation
        {
            get => _allowUpdateLocation;
            set
            {
                if (value == _allowUpdateLocation) return;
                _allowUpdateLocation = value;
                if (value) UpdateLocation();
            }
        }

        /// <summary>
        /// Speed km/h.
        /// Note: in Unity Editor will always be zero.
        /// </summary>
        public float speed => _speed;

        private void CreateMarker()
        {
            if (markerType == UserLocationMarkerType.twoD)
            {
                Marker2D m2d = map.marker2DManager.Create(location, marker2DTexture, markerTooltip);
                _marker = m2d;
                m2d.align = marker2DAlign;
            }
            else
            {
                ControlBase3D control = map.control as ControlBase3D;
                if (!control)
                {
                    Debug.LogError("You must use the 3D control (Tileset or Plane).");
                    createMarkerInUserLocation = false;
                    return;
                }

                Marker3D m3d = control.marker3DManager.Create(location, marker3DPrefab, markerTooltip);
                _marker = m3d;
                m3d.sizeType = marker3DSizeType;
            }
            
            _marker.scale = markerScale;
            if (useCompassForMarker) _marker.rotation = trueHeading;
        }

        /// <summary>
        /// Returns the current GPS location or emulator location.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        public void GetLocation(out float longitude, out float latitude)
        {
            longitude = (float)location.x;
            latitude = (float)location.y;
        }

        /// <summary>
        /// Returns the current GPS location or emulator location.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        public void GetLocation(out double longitude, out double latitude)
        {
            longitude = location.x;
            latitude = location.y;
        }

        /// <summary>
        /// Returns the current GPS location from sensor.
        /// </summary>
        /// <returns>GPS location</returns>
        protected abstract GeoPoint GetLocationFromSensor();

        public SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("UserLocation", "User Location", SaveSettings)
                {
                    loadCallback = LoadSettings
                }
            };

            return savableItems;
        }

        /// <summary>
        /// Checks that the Location Service is running.
        /// </summary>
        /// <returns>True - location service is running, false - otherwise.</returns>
        public abstract bool IsLocationServiceRunning();

        public void LoadSettings(JSONItem json)
        {
            (json as JSONObject).DeserializeObject(this);
        }

        private void OnCameraOrbitChangedByInput()
        {
            if (lockDisable) return;

            lastLocationChangedTime = Time.realtimeSinceStartup;
            if (autoStopUpdateOnInput) _allowUpdateLocation = false;
        }

        private void OnChangeLocation()
        {
            if (lockDisable) return;

            lastLocationChangedTime = Time.realtimeSinceStartup;
            if (autoStopUpdateOnInput) _allowUpdateLocation = false;
        }

        protected virtual void OnEnable()
        {
            _baseInstance = this;
            map = GetComponent<Map>();
            if (map != null) map.OnLocationChanged += OnChangeLocation;
        }

        private void OnFindLocationComplete(WebRequest www)
        {
            if (www.hasError) return;

            string response = www.text;
            if (string.IsNullOrEmpty(response)) return;

            int index = 0;
            const string s = "\"loc\": \"";
            float lat = 0, lng = 0;
            bool success = false;
            for (int i = 0; i < response.Length; i++)
            {
                if (response[i] != s[index])
                {
                    index = 0;
                    continue;
                }

                index++;
                if (index < s.Length) continue;

                i++;
                int startIndex = i;
                while (true)
                {
                    char c = response[i];
                    if (c == ',')
                    {
                        lat = float.Parse(response.Substring(startIndex, i - startIndex), Culture.numberFormat);
                        i++;
                        startIndex = i;
                    }
                    else if (c == '"')
                    {
                        lng = float.Parse(response.Substring(startIndex, i - startIndex), Culture.numberFormat);
                        success = true;
                        break;
                    }

                    i++;
                }

                break;
            }

            if (!success) return;
            
            if (useGPSEmulator) emulatedLocation = new GeoPoint(lng, lat);
            else if (location == GeoPoint.zero)
            {
                location = new GeoPoint(lng, lat);
                if (!isLocationInited && OnLocationInited != null)
                {
                    isLocationInited = true;
                    OnLocationInited();
                }
                if (OnLocationChanged != null) OnLocationChanged(location);
            }
            if (OnFindLocationByIPComplete != null) OnFindLocationByIPComplete();
        }

        private void OnMarkerDrag(Marker m)
        {
            if (!useGPSEmulator) return;
        
            emulatedLocation = m.location;
            OnChangeLocation();
        }

        protected virtual JSONItem SaveSettings()
        {
            JSONObject json = JSON.Serialize(new
            {
                autoStopUpdateOnInput,
                updateLocation,
                restoreAfter,
                createMarkerInUserLocation,
                useGPSEmulator
            }) as JSONObject;

            if (createMarkerInUserLocation)
            {
                json.AppendObject(new
                {
                    markerType,
                    markerScale,
                    markerTooltip,
                    useCompassForMarker
                });

                if (markerType == UserLocationMarkerType.twoD)
                {
                    json.AppendObject(new
                    {
                        marker2DAlign,
                        marker2DTexture
                    });
                }
                else
                {
                    json.Add("marker3DPrefab", marker3DPrefab.GetInstanceID());
                    json.Add("marker3DSizeType", marker3DSizeType);
                }
            }

            if (useGPSEmulator)
            {
                json.AppendObject(new
                {
                    emulatedLocation,
                    emulatedCompass
                });
            }

            return json;
        }

        public void SetStarted(bool value)
        {
            started = value;
        }

        private void Start()
        {
            map.OnLocationChanged += OnChangeLocation;

            if (CameraOrbit.instance != null) CameraOrbit.instance.OnChangedByInput += OnCameraOrbitChangedByInput;

            if (findLocationByIP)
            {
#if UNITY_EDITOR || !UNITY_WEBGL
                WebRequest findByIPRequest = new WebRequest("https://ipinfo.io/json");
#else
                WebRequest findByIPRequest = new WebRequest("https://service.infinity-code.com/getlocation.php");
#endif
                findByIPRequest.OnComplete += OnFindLocationComplete;
            }
        }

        /// <summary>
        /// Stops Location Service
        /// </summary>
        public abstract void StopLocationService();

        /// <summary>
        /// Try to start Location Service.
        /// </summary>
        /// <returns>True - success, false - otherwise.</returns>
        public abstract bool TryStartLocationService();
        
        private void TryUpdateMapLocation()
        {
            if (!updateLocation) return;
        
            if (_allowUpdateLocation)
            {
                UpdateLocation();
            }
            else if (restoreAfter > 0 && Time.realtimeSinceStartup > lastLocationChangedTime + restoreAfter)
            {
                _allowUpdateLocation = true;
                UpdateLocation();
                if (OnLocationRestored != null) OnLocationRestored();
            }
        }

        private void Update()
        {
            if (map == null)
            {
                map = Map.instance;
                if (map == null) return;
            }

            try
            {
                if (!started)
                {
#if !UNITY_EDITOR
                    Input.compass.enabled = true;
                    if(!TryStartLocationService()) return;
#endif
                    started = true;
                }

#if !UNITY_EDITOR
                if (disableEmulatorInPublish) useGPSEmulator = false;
#endif
                bool locationChanged = false;

                if (createMarkerInUserLocation && _marker == null && (useGPSEmulator || location != GeoPoint.zero)) UpdateMarker();

                if (!useGPSEmulator && !IsLocationServiceRunning()) return;

                bool compassChanged = UpdateCompass(ref locationChanged);
                UpdateSpeed();

                if (useGPSEmulator) UpdateLocationFromEmulator(ref locationChanged);
                else UpdateLocationFromInput(ref locationChanged);

                if (createMarkerInUserLocation)
                {
                    if (locationChanged || compassChanged) UpdateMarker();
                    UpdateMarkerRotation();
                }

                if (rotateCameraByCompass)
                {
                    UpdateCameraRotation();
                }

                if (locationChanged)
                {
                    if (!isLocationInited)
                    {
                        isLocationInited = true;
                        if (OnLocationInited != null) OnLocationInited();
                    }
                    if (OnLocationChanged != null) OnLocationChanged(location);
                }

                TryUpdateMapLocation();

                if (locationChanged) map.Redraw();
            }
            catch 
            {
                
            }
        }
        
        private bool UpdateCompass(ref bool locationChanged)
        {
            bool compassChanged;
        
            if (useGPSEmulator)
            {
                compassChanged = UpdateCompassFromEmulator();
                if (!isLocationInited) locationChanged = true;
            }
            else compassChanged = UpdateCompassFromInput();

            return compassChanged;
        }

        private void UpdateCameraRotation()
        {
            CameraOrbit co = CameraOrbit.instance;
            if (co == null) return;

            float value = Mathf.Repeat(co.rotation.y, 360);
            float off = value - Mathf.Repeat(trueHeading, 360);

            if (off > 180) value -= 360;
            else if (off < -180) value += 360;

            if (!(Math.Abs(trueHeading - value) >= float.Epsilon)) return;

            if (!lerpCompassValue || Mathf.Abs(trueHeading - value) < 0.003f) value = trueHeading;
            else value = Mathf.Lerp(value, trueHeading, 0.02f);

            co.rotation = new Vector2(co.rotation.x, value);
        }

        private bool UpdateCompassFromEmulator()
        {
            if (Math.Abs(trueHeading - emulatedCompass) <= float.Epsilon) return false;
            
            trueHeading = Mathf.Repeat(emulatedCompass, 360);
            if (OnCompassChanged != null) OnCompassChanged(trueHeading);

            return true;
        }

        private bool UpdateCompassFromInput()
        {
            float heading = Input.compass.trueHeading;
            float offset = trueHeading - heading;

            if (offset > 180) offset -= 360;
            else if (offset < -180) offset += 360;

            if (Mathf.Abs(offset) < compassThreshold) return false;
            
            trueHeading = heading;
            if (OnCompassChanged != null) OnCompassChanged(trueHeading);
            return true;
        }

        private void UpdateMarker()
        {
            if (_marker != null)
            {
                _marker.location = location;
                return;
            }

            CreateMarker();

            if (updateEmulatedLocationByMarker && _marker != null)
            {
                _marker.SetDraggable();
                _marker.OnDrag += OnMarkerDrag;
            }
        }

        private void UpdateMarkerRotation()
        {
            if (!useCompassForMarker || marker == null) return;

            float value = marker.rotation;

            if (trueHeading - value > 180) value += 360;
            else if (trueHeading - value < -180) value -= 360;

            if (!(Math.Abs(trueHeading - value) >= float.Epsilon)) return;
            
            if (!lerpCompassValue || Mathf.Abs(trueHeading - value) < 0.003f) value = trueHeading;
            else value = Mathf.Lerp(value, trueHeading, 0.02f);

            marker.rotation = value;
            map.Redraw();
        }

        /// <summary>
        /// Sets map location using GPS coordinates.
        /// </summary>
        public void UpdateLocation()
        {
            if (!useGPSEmulator && location == GeoPoint.zero) return;
            if (!map) return;

            lockDisable = true;

            GeoPoint p = map.view.center;
            bool changed = false;

            if (Math.Abs(p.x - location.x) > float.Epsilon)
            {
                p.x = location.x;
                changed = true;
            }
            if (Math.Abs(p.y - location.y) > float.Epsilon)
            {
                p.y = location.y;
                changed = true;
            }

            if (changed)
            {
                map.view.center = p;
                map.Redraw();
            }

            lockDisable = false;
        }

        private void UpdateLocationFromEmulator(ref bool locationChanged)
        {
            if (Math.Abs(location.x - emulatedLocation.x) > float.Epsilon)
            {
                location.x = emulatedLocation.x;
                locationChanged = true;
            }
            if (Math.Abs(location.y - emulatedLocation.y) > float.Epsilon)
            {
                location.y = emulatedLocation.y;
                locationChanged = true;
            }
        }

        private void UpdateLocationFromInput(ref bool locationChanged)
        {
            GeoPoint _location = OnGetLocation != null ? OnGetLocation() : GetLocationFromSensor();
            if (location == _location) return;
            
            location = _location;
            locationChanged = true;
        }

        /// <summary>
        /// Updates the speed data.
        /// </summary>
        public abstract void UpdateSpeed();
    }
}