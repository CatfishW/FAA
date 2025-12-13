/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

namespace OnlineMaps
{
    /// <summary>
    /// Controls map using GPS.<br/>
    /// User Location is a wrapper for Unity Location Service and Unity Compass.<br/>
    /// https://docs.unity3d.com/ScriptReference/LocationService.html
    /// </summary>
    [Serializable]
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/User Location")]
    public class UserLocation : UserLocationGenericBase<UserLocation>
    {
        /// <summary>
        /// Desired service accuracy in meters. 
        /// </summary>
        public float desiredAccuracy = 10;

        /// <summary>
        /// Request permission on Android at runtime when isEnabledByUser = false
        /// </summary>
        public bool requestPermissionRuntime = true;

        /// <summary>
        ///  The minimum distance (measured in meters) a device must move laterally before location is updated.
        /// </summary>
        public float updateDistance = 10;

        private List<LastLocationItem> lastLocations;
        private double lastLocationInfoTimestamp;
        private bool isPermissionRequested;
        private double _distance;

        /// <summary>
        /// Distance in meters from the last location.
        /// </summary>
        public double distance
        {
            get { return _distance; }
        }

        protected override GeoPoint GetLocationFromSensor()
        {
            LocationInfo data = Input.location.lastData;
            return new GeoPoint(data.longitude, data.latitude);
        }

        public override bool IsLocationServiceRunning()
        {
            return Input.location.status == LocationServiceStatus.Running;
        }

        protected override JSONItem SaveSettings()
        {
            return base.SaveSettings().AppendObject(new
            {
                desiredAccuracy,
                updateDistance
            });
        }

        /// <summary>
        /// Starts location service updates. Last location coordinates could be.
        /// </summary>
        /// <param name="desiredAccuracyInMeters">
        /// Desired service accuracy in meters. <br/>
        /// Using higher value like 500 usually does not require to turn GPS chip on and thus saves battery power.<br/>
        /// Values like 5-10 could be used for getting best accuracy. Default value is 10 meters.
        /// </param>
        /// <param name="updateDistanceInMeters">
        /// The minimum distance (measured in meters) a device must move laterally before Input.location property is updated. <br/>
        /// Higher values like 500 imply less overhead.
        /// </param>
        public void StartLocationService(float? desiredAccuracyInMeters = null, float? updateDistanceInMeters = null)
        {
            if (!desiredAccuracyInMeters.HasValue) desiredAccuracyInMeters = desiredAccuracy;
            if (!updateDistanceInMeters.HasValue) updateDistanceInMeters = updateDistance;

            Input.location.Start(desiredAccuracyInMeters.Value, updateDistanceInMeters.Value);
        }

        public override void StopLocationService()
        {
            Input.location.Stop();
        }

        public override bool TryStartLocationService()
        {
            if (!Input.location.isEnabledByUser)
            {
                if (requestPermissionRuntime && !isPermissionRequested)
                {
#if PLATFORM_ANDROID
                    if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                    {
                        isPermissionRequested = true;
                        Permission.RequestUserPermission(Permission.FineLocation);
                        return Permission.HasUserAuthorizedPermission(Permission.FineLocation);
                    }
#endif
                }
                return false;
            }
            else
            {
                StartLocationService();
                return true;
            }
        }

        public override void UpdateSpeed()
        {
            double longitude, latitude;
            double timestamp;
            if (!useGPSEmulator)
            {
                if (Input.location.status != LocationServiceStatus.Running) return;

                LocationInfo lastData = Input.location.lastData;
                timestamp = lastData.timestamp;
                if (Math.Abs(lastLocationInfoTimestamp - timestamp) < double.Epsilon) return;

                longitude = lastData.longitude;
                latitude = lastData.latitude;

                lastLocationInfoTimestamp = timestamp;
            }
            else
            {
                timestamp = Time.time;
                longitude = emulatedLocation.x;
                latitude = emulatedLocation.y;
            }

            if (OnGetLocation != null)
            {
                GeoPoint _location = OnGetLocation();
                longitude = _location.longitude;
                latitude = _location.latitude;
            }

            if (lastLocations == null) lastLocations = new List<LastLocationItem>();

            lastLocations.Add(new LastLocationItem(new GeoPoint(longitude, latitude), timestamp));
            while (lastLocations.Count > maxLocationCount) lastLocations.RemoveAt(0);

            if (lastLocations.Count < 2)
            {
                _speed = 0;
                return;
            }

            LastLocationItem p1 = lastLocations[0];
            LastLocationItem p2 = lastLocations[lastLocations.Count - 1];
            
            _distance = GeoMath.Distance(p1.longitude, p1.latitude, p2.longitude, p2.latitude);
            double time = (p2.timestamp - p1.timestamp) / 3600;
            _speed = Mathf.Abs((float) (_distance / time));
        }

        internal struct LastLocationItem
        {
            public GeoPoint location;
            public double timestamp;

            public double longitude => location.x;
            public double latitude => location.y;

            public LastLocationItem(GeoPoint location, double timestamp)
            {
                this.location = location;
                this.timestamp = timestamp;
            }
        }
    }
}