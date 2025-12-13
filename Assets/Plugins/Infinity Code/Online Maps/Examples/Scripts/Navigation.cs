/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;
using UnityEngine.Serialization;

namespace OnlineMapsDemos
{
    public class Navigation : MonoBehaviour
    {
        #region FIELDS

        /// <summary>
        /// Reference to Control. If missed, the singleton value will be used.
        /// </summary>
        public TileSetControl control;

        /// <summary>
        /// The distance (km) from the user's location to the nearest point on the route to consider that he left the route.
        /// </summary>
        public float updateRouteAfterKm = 0.05f;

        /// <summary>
        /// Delay to find a new route.
        /// </summary>
        public float updateRouteDelay = 10;

        /// <summary>
        /// If TRUE the user's GPS location will be used, if FALSE the marker location will be used, which you can drag.
        /// </summary>
        public bool useUserLocation;

        /// <summary>
        /// Should the compass value be smoothed.
        /// </summary>
        public bool lerpCompassValue = true;

        /// <summary>
        /// Prefab of 3D marker.
        /// </summary>
        public GameObject markerPrefab;

        private Map map;
        private Marker3D marker;
        private GeoPoint[] routePoints;
        private GeoPoint lastLocation;
        private bool followRoute;
        private float speed = 0;
        private LastLocation lastKnownLocation;
        private GeoPoint currentLocation;
        private Vector2d correction;
        private float correctionProgress;
        private int maxLocationCount = 3;
        private List<LastLocation> lastLocations;
        private float timeToUpdateRoute = float.MinValue;
        private GeoPoint destinationPoint;
        private float heading;
        private float smoothedCompass;
        private float correctionTime = 2f;

        #endregion

        #region PROPERTIES

        public int currentStepIndex { get; private set; }
        public int coveredDistance { get; private set; }
        public int coveredDuration { get; private set; }
        public GeoPoint lastPointOnRoute { get; private set; }
        public NavigationUI navigationUI { get; private set; }
        public int pointIndex { get; private set; }
        public GoogleRoutingResult.RouteLegStep[] steps { get; private set; }

        public int remainDistance => totalDistance - coveredDistance;

        public int remainDuration => totalDuration - coveredDuration;

        public NavigationRouteDrawer routeDrawer { get; private set; }

        public int totalDistance { get; private set; }
        public int totalDuration { get; private set; }

        #endregion

        public void CancelNavigation()
        {
            routeDrawer.RemoveLines();

            routePoints = null;
            steps = null;
            followRoute = false;
        }

        /// <summary>
        /// Checks if the user has reached the destination.
        /// </summary>
        /// <param name="location">User's location</param>
        /// <returns>Whether the user has reached the destination</returns>
        private bool CheckFinished(GeoPoint location)
        {
            if (currentStepIndex != steps.Length - 1) return false;

            // Get distance between user and destination
            double d = location.Distance(destinationPoint);

            // If the distance is less than the threshold, the user has reached the destination
            if (d < 0.02) return false;

            // Stop navigation and show finish UI
            followRoute = false;
            navigationUI.Finish();

            Debug.Log("Finished");

            return true;
        }

        public void FindRoute()
        {
            // Check for Google Maps API key
            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please enter Map / Key Manager / Google Maps");
                return;
            }

            // Send request to Google Directions API
            new GoogleRoutingRequest(
                    GetUserLocation(),
                    navigationUI.destinationInput.text)
                .HandleResult(OnResult)
                .Send();
        }

        private GeoPoint GetExpectedLocation()
        {
            if (!useUserLocation) return marker.location;
            if (Math.Abs(speed) < float.Epsilon) return lastKnownLocation.location;

            double coveredDistance = (Time.time - lastKnownLocation.timestamp) * speed / 3600f;

            GeoPoint location = lastKnownLocation.location.Distant(coveredDistance, heading);

            if (correctionProgress < 1 && correction.sqrMagnitude > 0)
            {
                float nextCorrectionProgress = correctionProgress + Time.deltaTime / correctionTime;
                if (nextCorrectionProgress > 1) nextCorrectionProgress = 1;

                float correctionDelta = nextCorrectionProgress - correctionProgress;
                currentLocation.x += correction.x * correctionDelta;
                currentLocation.y += correction.y * correctionDelta;

                correctionProgress = nextCorrectionProgress;
            }

            return location;
        }

        /// <summary>
        /// Finds the nearest point on the route and checks if the user has left the route.
        /// </summary>
        /// <param name="location">User location.</param>
        /// <param name="pointChanged">Returns whether the number of the route point in use has changed.</param>
        /// <returns>Returns whether the user is following the route.</returns>
        private bool GetPointOnRoute(GeoPoint location, out bool pointChanged)
        {
            pointChanged = false;
            var step = steps[currentStepIndex];
            GeoPoint p1 = step.polyline.points[pointIndex];
            GeoPoint p2 = step.polyline.points[pointIndex + 1];
            GeoPoint p;
            double dist;

            if (p1 != p2)
            {
                // Check if the user is on the same route point.
                Geometry.NearestPointStrict(location.x, location.y, p1.x, p1.y, p2.x, p2.y, out p.x, out p.y);
                if (p != p2)
                {
                    dist = p.Distance(location);

                    if (dist < updateRouteAfterKm)
                    {
                        timeToUpdateRoute = float.MinValue;
                        lastPointOnRoute = p;
                        return true;
                    }
                }
            }

            // Checking what step and point the user is on
            for (int i = currentStepIndex; i < steps.Length; i++)
            {
                step = steps[i];
                GeoPoint[] polyline = step.polyline.points;

                for (int j = pointIndex; j < polyline.Length - 1; j++)
                {
                    p1 = polyline[j];
                    p2 = polyline[j + 1];
                    Geometry.NearestPointStrict(location.x, location.y, p1.x, p1.y, p2.x, p2.y, out p.x, out p.y);
                    if (p == p2) continue;

                    dist = p.Distance(location);
                    if (dist < updateRouteAfterKm)
                    {
                        // Update the step instruction and save the index of step and point.
                        navigationUI.SetInstruction(step.navigationInstruction.instructions);
                        currentStepIndex = i;
                        pointChanged = true;
                        pointIndex = j;
                        timeToUpdateRoute = float.MinValue;
                        lastPointOnRoute = p;
                        if (!useUserLocation)
                        {
                            LookToLocation(p, p2);
                        }
                        return true;
                    }
                }

                pointIndex = 0;
            }

            // The user has left the route. If the countdown to the search for a new route has not started, we start it.
            if (timeToUpdateRoute < -999) timeToUpdateRoute = updateRouteDelay;

            return false;
        }

        /// <summary>
        /// Gets the user's location.
        /// </summary>
        /// <returns>User's location</returns>
        private GeoPoint GetUserLocation()
        {
            return useUserLocation ? UserLocation.instance.location : marker.location;
        }

        /// <summary>
        /// Rotates the marker towards the second point.
        /// </summary>
        /// <param name="fromLocation">Looking point</param>
        /// <param name="toLocation">Target point</param>
        private void LookToLocation(GeoPoint fromLocation, GeoPoint toLocation)
        {
            marker.rotation = (float)fromLocation.AngleInMercator(map, toLocation) + 90;
        }

        /// <summary>
        /// Called when the compass value has been changed.
        /// </summary>
        /// <param name="newHeading">Compass true heading (0-360)</param>
        private void OnCompassChanged(float newHeading)
        {
            // Set the rotation of the marker.
            // Update compass value
            heading = newHeading;

            // If the marker rotation should not smooth, update the rotation
            if (!lerpCompassValue && marker != null) marker.rotation = newHeading;
        }

        private void OnEnable()
        {
            navigationUI = GetComponent<NavigationUI>();
            routeDrawer = GetComponent<NavigationRouteDrawer>();
        }

        /// <summary>
        /// Called when the user's GPS location has changed.
        /// </summary>
        /// <param name="location">User's GPS location</param>
        private void OnLocationChanged(GeoPoint location)
        {
            // Save a new location
            lastKnownLocation = new LastLocation(location, Time.time);

            // Calculating the correction vector
            correction = location - currentLocation;

            // Update current speed
            UpdateSpeed();

            // Calculate a distance between new and old locations
            double dx, dy;
            GeoMath.Distances(location.x, location.y, currentLocation.x, currentLocation.y, out dx, out dy);
            double d = Math.Sqrt(dx * dx + dy * dy);

            // If the distance is too long or the speed is too low, update the location
            if (d > 0.01 || speed < 1)
            {
                currentLocation = location;
                correction = Vector2d.zero;
            }

            // Reset correction progress
            correctionProgress = 0;
        }

        /// <summary>
        /// This method is called when the Google Directions API returned a response.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnResult(GoogleRoutingResult result)
        {
            // If there are no routes, return
            if (result.routes.Length == 0)
            {
                Debug.Log("Can't find route");
                return;
            }

            GoogleRoutingResult.Route route = result.routes[0];
            if (route == null)
            {
                Debug.Log("Can't find route");
                return;
            }

            // Reset step and point indices
            currentStepIndex = 0;
            pointIndex = 0;

            // Get steps from the route
            steps = route.legs.SelectMany(l => l.steps).ToArray();

            // Get route points
            routePoints = route.polyline.points;

            // The remaining points are the entire route
            routeDrawer.SetRemainPoints(routePoints.ToList());

            // The destination is the last point
            destinationPoint = routePoints.Last();

            // Calculate total distance and duration
            totalDistance = route.distanceMeters;
            totalDuration = route.durationSec;

            // Set distance, duration and first instruction on UI
            navigationUI.SetDistance(totalDistance);
            navigationUI.SetDuration(totalDuration);
            navigationUI.SetRemainDistance(totalDistance);
            navigationUI.SetRemainDuration(totalDuration);
            navigationUI.SetInstruction(steps[0].navigationInstruction.instructions);

            // Show the whole route
            GoogleRoutingResult.Viewport v = route.viewport;

            GeoPoint[] bounds =
            {
                v.low,
                v.high
            };

            (GeoPoint center, int zoom) = GeoMath.CenterPointAndZoom(bounds);

            map.view.SetCenter(center, zoom);
            lastLocation = marker.location;

            // If a marker location is used, turn it towards the second point
            if (!useUserLocation) LookToLocation(routePoints[0], routePoints[1]);

            // Show confirmation UI
            navigationUI.ShowConfirmation();
        }

        /// <summary>
        /// This method is called when Google Directions API returned updated route.
        /// </summary>
        /// <param name="result">Result object</param>
        private void OnUpdateResult(GoogleRoutingResult result)
        {
            // If there are no routes, return
            if (result.routes.Length == 0)
            {
                Debug.Log("Can't find route");
                return;
            }

            GoogleRoutingResult.Route route = result.routes[0];
            if (route == null)
            {
                Debug.Log("Can't find route");
                return;
            }

            // Get steps from route
            steps = route.legs.SelectMany(l => l.steps).ToArray();

            // Get route points
            routePoints = route.polyline.points;
            destinationPoint = routePoints.Last();

            // Calculate total distance and duration
            totalDistance = route.distanceMeters;
            totalDuration = route.durationSec;

            // Set distance, duration and first instruction on UI
            navigationUI.SetDistance(totalDistance);
            navigationUI.SetDuration(totalDuration);
            navigationUI.SetRemainDistance(totalDistance);
            navigationUI.SetRemainDuration(totalDuration);
            navigationUI.SetInstruction(steps[0].navigationInstruction.instructions);

            // Reset step and point indices
            currentStepIndex = 0;
            pointIndex = 0;

            // Update covered and remain lines
            routeDrawer.UpdateLines();
        }

        /// <summary>
        /// Requests an updated route
        /// </summary>
        private void RequestUpdateRoute()
        {
            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogWarning("Please enter Map / Key Manager / Google Maps");
            }

            // Send request to Google Directions API
            new GoogleRoutingRequest(
                GetUserLocation(),
                navigationUI.destinationInput.text).HandleResult(OnUpdateResult).Send();
        }

        private void SmoothCompass()
        {
            if (!lerpCompassValue) return;
            if (heading - smoothedCompass > 180) smoothedCompass += 360;
            else if (heading - smoothedCompass < -180) smoothedCompass -= 360;

            if (Math.Abs(heading - smoothedCompass) < float.Epsilon) return;
            if (Mathf.Abs(heading - smoothedCompass) < 0.003f) smoothedCompass = heading;
            else smoothedCompass = Mathf.Lerp(smoothedCompass, heading, 0.02f);

            marker.rotation = smoothedCompass;
        }

        private void Start()
        {
            // Get map and control instances
            if (!control) control = TileSetControl.instance;
            map = control.map;

            // Create a new marker in the center of the map
            GeoPoint center = map.view.center;
            marker = control.marker3DManager.Create(center, markerPrefab);

            currentStepIndex = pointIndex = -1;

            // If you use user location, subscribe to events
            // Else make a marker draggable
            if (useUserLocation)
            {
                UserLocation.instance.OnLocationChanged += OnLocationChanged;
                UserLocation.instance.OnCompassChanged += OnCompassChanged;
            }
            else marker.isDraggable = true;
        }

        public void StartNavigation()
        {
            // Zoom in on the map at the first route point
            map.view.SetCenter(routePoints[0], 19);

            // Create covered line
            routeDrawer.InitCoveredPoints();

            // Start navigation and reset indices
            followRoute = true;
            currentStepIndex = 0;
            pointIndex = 0;
        }

        /// <summary>
        /// Called every frame
        /// </summary>
        private void Update()
        {
            SmoothCompass();

            // If navigation is not started, return
            if (!followRoute)
            {
                if (useUserLocation) marker.location = GetUserLocation();
                return;
            }

            // If the user has left the route, wait for a delay and request a new route
            if (timeToUpdateRoute > 0)
            {
                timeToUpdateRoute -= Time.deltaTime;
                if (timeToUpdateRoute <= 0)
                {
                    timeToUpdateRoute = float.MinValue;
                    RequestUpdateRoute();
                }
            }

            // Get the location of the marker, and if it hasn't changed, return
            GeoPoint expectedLocation = GetExpectedLocation();
            if (expectedLocation == lastLocation) return;

            lastLocation = expectedLocation;
            bool pointChanged;

            // Check if the user has reached the destination
            if (CheckFinished(expectedLocation))
            {
                marker.location = expectedLocation;
            }
            // Get the nearest point on a route
            else if (GetPointOnRoute(expectedLocation, out pointChanged))
            {
                if (useUserLocation) marker.location = lastPointOnRoute;
                else marker.location = expectedLocation;

                UpdateCoveredValues();

                // Update covered and remain lines
                routeDrawer.UpdateLines();

                // If the point index has changed, update the distance and duration on UI
                if (pointChanged) navigationUI.UpdateRemainDistanceAndDuration();

                // Redraw the map
                map.Redraw();
            }
            else
            {
                marker.location = expectedLocation;

                // The user has left the route
                Debug.Log("The user has left the route");
            }
        }

        private void UpdateCoveredValues()
        {
            coveredDistance = 0;
            coveredDuration = 0;

            GoogleRoutingResult.RouteLegStep s;

            // Sum the distances and the duration of covered steps
            for (int i = 0; i < currentStepIndex; i++)
            {
                s = steps[i];
                coveredDistance += s.distanceMeters;
                coveredDuration += s.durationSec;
            }

            s = steps[currentStepIndex];
            GeoPoint[] polyline = s.polyline.points;
            double stepDistance = 0;

            // Sum the distance between covered points on current step
            for (int i = 0; i < pointIndex - 1; i++)
            {
                GeoPoint p1 = polyline[i];
                GeoPoint p2 = polyline[i + 1];
                stepDistance += GeoMath.Distance(p1, p2) * 1000;
            }

            // Add the progress of the current step to the covered distance and duration.
            if (stepDistance > s.distanceMeters) stepDistance = s.distanceMeters;
            coveredDistance += (int)stepDistance;
            coveredDuration += (int)(stepDistance / s.distanceMeters * s.durationSec);
        }

        /// <summary>
        /// Update speed
        /// </summary>
        public void UpdateSpeed()
        {
            if (lastLocations == null) lastLocations = new List<LastLocation>();

            lastLocations.Add(lastKnownLocation);
            while (lastLocations.Count > maxLocationCount) lastLocations.RemoveAt(0);

            if (lastLocations.Count < 2)
            {
                speed = 0;
                return;
            }

            LastLocation p1 = lastLocations[0];
            LastLocation p2 = lastLocations[lastLocations.Count - 1];

            double distances = GeoMath.Distance(p1.location, p2.location);
            double time = (p2.timestamp - p1.timestamp) / 3600;
            speed = Mathf.Abs((float)(distances / time));
        }

        internal struct LastLocation
        {
            public GeoPoint location;
            public double timestamp;

            public LastLocation(GeoPoint location, double timestamp)
            {
                this.location = location;
                this.timestamp = timestamp;
            }
        }
    }
}