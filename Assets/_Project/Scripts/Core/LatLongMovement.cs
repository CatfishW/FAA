using UnityEngine;
using System.Collections.Generic;

public class LatLongMovement : MonoBehaviour
{
    // WGS84 ellipsoid constants (as doubles)
    private const double a = 6378137.0; // semi-major axis in meters
    private const double e2 = 6.69437999014e-3; // first eccentricity squared
    public WeatherDataReceiver weatherDataReceiver;

    // Queue to store recent positions for averaging
    private Queue<Vector3d> positionQueue = new Queue<Vector3d>();
    private const int queueSize = 10; // Number of positions to average

    // Start latitude and longitude
    public float startLatitude;
    public float startLongitude;

    // Scaling factor to reduce the impact of Unity position on latitude/longitude
    public float scaleFactor = 1000f; // Higher values slow down latitude/longitude changes

    // ECEF start position
    private Vector3d startEcefPosition;

    void Start()
    {
        // Convert start latitude and longitude to ECEF coordinates
        startEcefPosition = LatLongToECEF(startLatitude, startLongitude);

        // Initialize the queue with the start position
        for (int i = 0; i < queueSize; i++)
        {
            positionQueue.Enqueue(startEcefPosition);
        }

        // Place the Unity object at this position (assuming the ECEF and Unity world positions are aligned)
        Vector3 startPosition = ECEFToUnity(startEcefPosition);
        transform.position = startPosition;
    }

    void Update()
    {
        // Calculate current position in Unity world space
        Vector3 currentUnityPosition = transform.position / scaleFactor; // **Scaled down movement**

        // Calculate ECEF position relative to the start
        Vector3d currentEcefPosition = UnityToECEF(currentUnityPosition);
        Vector3d displacement = currentEcefPosition - startEcefPosition;

        // Add the current position to the queue
        positionQueue.Enqueue(currentEcefPosition);

        // Remove the oldest position if the queue exceeds the desired size
        if (positionQueue.Count > queueSize)
        {
            positionQueue.Dequeue();
        }

        // Calculate the average position
        Vector3d averagePosition = new Vector3d(0, 0, 0);
        foreach (Vector3d pos in positionQueue)
        {
            averagePosition += pos;
        }
        averagePosition /= positionQueue.Count;

        // Convert ECEF coordinates to latitude and longitude
        (double latitude, double longitude) = ECEFToLatLong(averagePosition);
        
        // Update the WeatherDataReceiver
        weatherDataReceiver.latitude = (float)latitude;
        weatherDataReceiver.longitude = (float)longitude;

        // Output the latitude and longitude
        Debug.Log($"Aircraft Latitude: {latitude}, Longitude: {longitude}");
    }

    private Vector3d LatLongToECEF(double latitude, double longitude)
    {
        double latRad = latitude * Mathf.Deg2Rad;
        double lonRad = longitude * Mathf.Deg2Rad;

        double N = a / System.Math.Sqrt(1 - e2 * System.Math.Pow(System.Math.Sin(latRad), 2));

        double x = N * System.Math.Cos(latRad) * System.Math.Cos(lonRad);
        double y = N * System.Math.Cos(latRad) * System.Math.Sin(lonRad);
        double z = (N * (1 - e2)) * System.Math.Sin(latRad);

        return new Vector3d(x, y, z)/scaleFactor; // **Scaled down movement**
    }

    private (double latitude, double longitude) ECEFToLatLong(Vector3d ecefPosition)
    {
        double x = ecefPosition.x;
        double y = ecefPosition.y;
        double z = ecefPosition.z;

        double longitude = System.Math.Atan2(y, x);
        double p = System.Math.Sqrt(x * x + y * y);
        double theta = System.Math.Atan2(z * a, p * (1 - e2));
        double latitude = System.Math.Atan2(z + e2 * (1 - e2) * a * System.Math.Pow(System.Math.Sin(theta), 3),
                                             p - e2 * a * System.Math.Pow(System.Math.Cos(theta), 3));

        // Convert radians to degrees
        latitude = latitude * Mathf.Rad2Deg;
        longitude = longitude * Mathf.Rad2Deg;

        return (latitude, longitude);
    }

    private Vector3 ECEFToUnity(Vector3d ecefPosition)
    {
        // Assuming Unity's origin is at (0, 0, 0) in ECEF coordinates
        // and 1 Unity unit equals 1 meter
        return new Vector3((float)ecefPosition.x, (float)ecefPosition.y, (float)ecefPosition.z)/scaleFactor; // **Scaled down movement**
    }

    private Vector3d UnityToECEF(Vector3 unityPosition)
    {
        // Assuming Unity's origin is at (0, 0, 0) in ECEF coordinates
        // and 1 Unity unit equals 1 meter
        return new Vector3d(unityPosition.x, unityPosition.y, unityPosition.z);
    }

    private struct Vector3d
    {
        public double x, y, z;

        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        // Operator overloads for easier math
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3d operator *(Vector3d a, double b) => new Vector3d(a.x * b, a.y * b, a.z * b);
        public static Vector3d operator /(Vector3d a, double b) => new Vector3d(a.x / b, a.y / b, a.z / b);
    }
}
