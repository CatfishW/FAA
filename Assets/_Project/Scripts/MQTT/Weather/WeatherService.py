"""
Weather Data Service Module
Provides functionality to fetch or simulate weather data
"""
import json
import random
import time
import logging

# Try to import NOAA SDK, if available
try:
    from noaa_sdk import NOAA
    NOAA_AVAILABLE = True
except ImportError:
    NOAA_AVAILABLE = False
    logging.warning("NOAA SDK not available. Will use simulated data instead.")

class WeatherService:
    """
    Service for fetching or simulating weather data
    """
    def __init__(self, latitude=39.7083, longitude=-75.1179):
        """
        Initialize the weather service
        
        Parameters
        ----------
        latitude : float
            Default latitude for data fetching
        longitude : float
            Default longitude for data fetching
        """
        self.latitude = latitude
        self.longitude = longitude

    def simulate_weather_data(self, custom_lat=None, custom_lon=None):
        """
        Generate simulated weather data
        
        Parameters
        ----------
        custom_lat : float, optional
            Override the default latitude
        custom_lon : float, optional
            Override the default longitude
            
        Returns
        -------
        str
            JSON string containing simulated weather data
        """
        lat = custom_lat if custom_lat is not None else self.latitude
        lon = custom_lon if custom_lon is not None else self.longitude
        
        data = {
            "WindSpeed": round(random.uniform(0, 140), 2),
            "WindDirection": round(random.uniform(0, 360), 2),
            "Ceiling": round(random.uniform(0, 10000), 2),
            "SkyCondition": random.choice(["SKC", "CLR", "FEW", "SCT", "BKN", "OVC"]),
            "Visibility": round(random.uniform(1, 89999), 2),
            "OutsideAirTemperature": round(random.uniform(-20, 40), 2),
            "DewPoint": round(random.uniform(-30, 30), 2),
            "Latitude": lat,
            "Longitude": lon,
            "Elevation": round(random.uniform(0, 10), 2),
            "Time": time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime())
        }
        return json.dumps(data)

    def fetch_real_weather_data(self, custom_lat=None, custom_lon=None):
        """
        Fetch real weather data from NOAA API
        
        Parameters
        ----------
        custom_lat : float, optional
            Override the default latitude
        custom_lon : float, optional
            Override the default longitude
            
        Returns
        -------
        str
            JSON string containing real weather data, or simulated data if fetch fails
        """
        lat = custom_lat if custom_lat is not None else self.latitude
        lon = custom_lon if custom_lon is not None else self.longitude
        
        # If NOAA SDK is not available, use simulated data
        if not NOAA_AVAILABLE:
            logging.info("Using simulated weather data because NOAA SDK is not available")
            return self.simulate_weather_data(custom_lat=lat, custom_lon=lon)
        
        try:
            n = NOAA()
            obs = n.get_observations_by_lat_lon(lat, lon)
            if not obs:
                logging.warning("No observations found. Using simulated data.")
                return self.simulate_weather_data(custom_lat=lat, custom_lon=lon)
                
            # Sort observations by timestamp in descending order
            obs_sorted = sorted(obs, key=lambda x: x.get('timestamp', ''), reverse=True)
            
            # Take the most recent useful observation
            newest_ob = obs_sorted[0]  # Default to first
            for ob in obs_sorted[:5]:  # Check first 5 for complete data
                if all(key in ob for key in ['windSpeed', 'temperature', 'visibility']):
                    newest_ob = ob
                    break
                    
            data = {
                "WindSpeed": newest_ob.get('windSpeed', {}).get('value', 'N/A'),
                "WindDirection": newest_ob.get('windDirection', {}).get('value', 'N/A'),
                "Ceiling": newest_ob.get('cloudLayers', [{}])[0].get('base', {}).get('value', 'N/A') 
                           if newest_ob.get('cloudLayers') else 'N/A',
                "SkyCondition": newest_ob.get('cloudLayers', [{}])[0].get('amount', 'N/A')
                               if newest_ob.get('cloudLayers') else 'N/A',
                "Visibility": newest_ob.get('visibility', {}).get('value', 'N/A'),
                "OutsideAirTemperature": newest_ob.get('temperature', {}).get('value', 'N/A'),
                "DewPoint": newest_ob.get('dewpoint', {}).get('value', 'N/A'),
                "Latitude": lat,
                "Longitude": lon,
                "Elevation": newest_ob.get('elevation', {}).get('value', 'N/A'),
                "Time": newest_ob.get('timestamp', 'N/A')
            }
            return json.dumps(data)
            
        except Exception as e:
            logging.error(f"Error fetching observations: {e}")
            logging.info("Falling back to simulated data")
            return self.simulate_weather_data(custom_lat=lat, custom_lon=lon)


if __name__ == "__main__":
    # Test the weather service
    service = WeatherService()
    print("Simulated Weather Data:")
    print(service.simulate_weather_data())
    
    print("\nAttempting to fetch real weather data:")
    real_data = service.fetch_real_weather_data()
    if real_data:
        print(real_data)