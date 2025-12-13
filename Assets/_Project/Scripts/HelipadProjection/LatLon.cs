using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Math;
/* 
 * Helper class for coordinate manipulation
 * 
 * Grant Morfitt
 * 
 */
public class LatLong
{


    double latitude, longitude, altitude;
    public LatLong(double _latitude, double _longitude, double _altitude)
    {
        latitude = _latitude;
        longitude = _longitude;
        altitude = _altitude; //MSL not AGL
    }

    public static double CalculateDistance(LatLong coordinate1, LatLong coordinate2)
    {
        double lat1 = coordinate1.latitude;
        double lon1 = coordinate1.longitude;

        double lat2 = coordinate2.latitude;
        double lon2 = coordinate2.longitude;

        double R = 6371e3f; // metres

        //const φ1
        double lat1Radians = lat1 * Mathf.PI / 180; // φ, λ in radians
        //const φ2
        double lat2Radians = lat2 * Mathf.PI / 180;

        //const Δφ
        double latDistanceRadians = (lat2 - lat1) * Mathf.PI / 180;
        //const Δλ
        double lonDistanceRadians = (lon2 - lon1) * Mathf.PI / 180;

        double a = System.Math.Sin(latDistanceRadians / 2.0) * System.Math.Sin(latDistanceRadians / 2.0) +
                  System.Math.Cos(lat1Radians) * System.Math.Cos(lat2Radians) *
                  System.Math.Sin(lonDistanceRadians / 2.0) * System.Math.Sin(lonDistanceRadians / 2.0);
        double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1.0 - a));

        double d = R * c; // in metres

        return d;
    }

    public static double CalculateBearing(LatLong coordinate1, LatLong coordinate2)
    {
        /*  This uses the round earth model, so not accurate for applications other than X-Plane which assumes a spherical earth
         *  The earth is flat anyhow, so that should worry you more than precision
         * 
         *  θ = atan2 [(sin Δλ ⋅ cos φ₂), (cos φ₁ ⋅ sin φ₂ − sin φ₁ ⋅ cos φ₂ ⋅ cos Δλ)]
         
            d = R ⋅ c
            φ₁ is the latitude of initial point (positive for N and negative for S);
            φ₂ is the latitude of the final point (positive for N and negative for S);
            λ₁ is the longitude of the initial point (positive for E and negative for W);
            λ₂ is the longitude of the final point (positive for E and negative for W);
            Δφ = φ₂ - φ₁;
            Δλ = λ₂ - λ₁
         * 
         */

        double lat1 = coordinate1.latitude * (System.Math.PI/180);
        double lon1 = coordinate1.longitude * (System.Math.PI / 180);

        double lat2 = coordinate2.latitude * (System.Math.PI/180);
        double lon2 = coordinate2.longitude * (System.Math.PI / 180);


        double deltaPsi = lat2 - lat1;
        double deltaLambda = lon2 - lon1;

        double Y = System.Math.Sin(deltaLambda) * System.Math.Cos(lat2);

        double X = (System.Math.Cos(lat1) * System.Math.Sin(lat2)) - (System.Math.Sin(lat1) * System.Math.Cos(lat2) * System.Math.Cos(deltaLambda));
       
        double theta = System.Math.Atan2(Y, X);

        double azimuth = ((theta * Mathf.Rad2Deg) + 360) % 360;


        return azimuth;
    }
}
