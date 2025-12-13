"""
Main entry point for the MqttWeather application
"""
import argparse
import sys
import os
from MqttWeather import MqttHandler
from MqttWeather import (
    WeatherService,
    RadarDataService,
    RadarVisualizationService
)


def main():
    #python -m MqttWeather.main mqtt
    """
    Main entry point for the MqttWeather application
    
    Parses command line arguments and starts the appropriate service
    """
    parser = argparse.ArgumentParser(description="MqttWeather - NEXRAD Weather Data and Visualization with MQTT")
    
    # Add subparsers for different commands
    subparsers = parser.add_subparsers(dest="command", help="Command to run")
    
    # MQTT service command
    mqtt_parser = subparsers.add_parser("mqtt", help="Start MQTT service")
    mqtt_parser.add_argument("--broker", type=str, default="agist.org", help="MQTT broker address")
    mqtt_parser.add_argument("--port", type=int, default=1883, help="MQTT broker port")
    mqtt_parser.add_argument("--username", type=str, default="TangClinic", help="MQTT username")
    mqtt_parser.add_argument("--password", type=str, default="Tang123", help="MQTT password")
    mqtt_parser.add_argument("--weather-topic", type=str, default="NOAAWeatherData", help="MQTT topic for weather data")
    mqtt_parser.add_argument("--coordinates-topic", type=str, default="NOAAWeatherCoordinates", help="MQTT topic for coordinates")
    mqtt_parser.add_argument("--radar-topic", type=str, default="NEXRADImage", help="MQTT topic for radar images")
    
    # Weather service command
    weather_parser = subparsers.add_parser("weather", help="Get weather data")
    weather_parser.add_argument("--lat", type=float, default=39.7083, help="Latitude")
    weather_parser.add_argument("--lon", type=float, default=-75.1179, help="Longitude")
    weather_parser.add_argument("--simulated", action="store_true", help="Use simulated data instead of real data")
    
    # Radar visualization command
    radar_parser = subparsers.add_parser("radar", help="Generate radar visualization")
    radar_parser.add_argument("--lat", type=float, default=39.7083, help="Latitude")
    radar_parser.add_argument("--lon", type=float, default=-75.1179, help="Longitude")
    radar_parser.add_argument("--heading", type=float, default=0, help="Heading angle in degrees")
    radar_parser.add_argument("--tilt", type=float, default=0, help="Tilt angle in degrees")
    radar_parser.add_argument("--gain", type=float, default=0, help="Gain adjustment for reflectivity")
    radar_parser.add_argument("--width", type=float, default=50, help="Width of the map in kilometers")
    radar_parser.add_argument("--height", type=float, default=50, help="Height of the map in kilometers")
    radar_parser.add_argument("--output", type=str, default="radar.png", help="Output file path")
    
    # Parse arguments
    args = parser.parse_args()
    
    # Execute command
    if args.command == "mqtt":
        # Start MQTT service
        handler = MqttHandler(
            broker=args.broker,
            port=args.port,
            username=args.username,
            password=args.password,
            weather_topic=args.weather_topic,
            coordinates_topic=args.coordinates_topic,
            radar_image_topic=args.radar_topic
        )
        print(f"Starting MQTT handler on {args.broker}:{args.port} with username '{args.username}'")
        try:
            handler.start()
        except KeyboardInterrupt:
            print("Ctrl+C pressed. Terminating...")
            
    elif args.command == "weather":
        # Get weather data
        service = WeatherService(latitude=args.lat, longitude=args.lon)
        if args.simulated:
            data = service.simulate_weather_data()
            print("Simulated Weather Data:")
        else:
            data = service.fetch_real_weather_data()
            print("Real Weather Data:")
        print(data)
        
    elif args.command == "radar":
        # Generate radar visualization
        service = RadarVisualizationService(image_path=args.output)
        image_path = service.generate_visualization(
            user_lat=args.lat,
            user_lon=args.lon,
            width_km=args.width,
            height_km=args.height,
            heading=args.heading,
            tilt=args.tilt,
            gain=args.gain
        )
        print(f"Radar visualization generated at: {image_path}")
        
    else:
        # No command specified, show help
        parser.print_help()


if __name__ == "__main__":
    main()