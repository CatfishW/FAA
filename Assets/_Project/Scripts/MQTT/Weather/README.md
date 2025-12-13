# MqttWeather

A Python package for NEXRAD weather data acquisition, visualization, and MQTT communication.

## Overview

MqttWeather is a comprehensive tool that provides:
- NEXRAD radar data acquisition from AWS
- Weather data fetching and simulation
- Radar data visualization
- MQTT-based communication for sending and receiving weather data and radar images

This package is designed for aviation, meteorology, and IoT applications that require real-time weather data and visualization.

## Features

- **Weather Data Service**: Fetch real weather data from NOAA or generate simulated weather data
- **Radar Data Service**: Download NEXRAD radar data from AWS S3
- **Radar Visualization**: Generate high-quality radar visualizations with customizable parameters:
  - Control tilt, gain, and heading
  - Adjust map dimensions
  - Dynamic range rings and heading indicators
- **MQTT Communication**: 
  - Subscribe to coordinate updates
  - Publish weather data and radar images
  - Base64-encoded image transfer

## Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/MqttWeather.git

# Install required dependencies
pip install -r requirements.txt
```

## Usage

### Command Line Interface

MqttWeather provides a command-line interface with three main commands:

#### 1. MQTT Service

Start the MQTT service to listen for coordinate updates and publish weather data and radar images:

```bash
python -m MqttWeather.main mqtt --broker 127.0.0.1 --port 1883
```
or
```bash
python -m MqttWeather.main mqtt
```

Optional arguments:
- `--broker`: MQTT broker address (default: 127.0.0.1)
- `--port`: MQTT broker port (default: 1883)
- `--weather-topic`: MQTT topic for weather data (default: NOAAWeatherData)
- `--coordinates-topic`: MQTT topic for coordinates (default: NOAAWeatherCoordinates)
- `--radar-topic`: MQTT topic for radar images (default: NEXRADImage)

#### 2. Weather Data

Get weather data for a specific location:

```bash
python -m MqttWeather.main weather --lat 39.7083 --lon -75.1179 --simulated
```

Optional arguments:
- `--lat`: Latitude (default: 39.7083)
- `--lon`: Longitude (default: -75.1179)
- `--simulated`: Use simulated data instead of real data

#### 3. Radar Visualization

Generate radar visualization for a specific location:

```bash
python -m MqttWeather.main radar --lat 39.7083 --lon -75.1179 --heading 45 --tilt 2 --gain 5
```

Optional arguments:
- `--lat`: Latitude (default: 39.7083)
- `--lon`: Longitude (default: -75.1179)
- `--heading`: Heading angle in degrees (default: 0)
- `--tilt`: Tilt angle in degrees (default: 0)
- `--gain`: Gain adjustment for reflectivity (default: 0)
- `--width`: Width of the map in kilometers (default: 50)
- `--height`: Height of the map in kilometers (default: 50)
- `--output`: Output file path (default: radar.png)

### Using as a Library

```python
from MqttWeather import WeatherService, RadarDataService, RadarVisualizationService, MqttHandler

# Weather data
weather_service = WeatherService()
weather_data = weather_service.simulate_weather_data()

# Radar data
radar_service = RadarDataService()
radar_files = radar_service.fetch_nexrad_data("KDOX", 1)

# Radar visualization
visualization_service = RadarVisualizationService()
image_path = visualization_service.generate_visualization(39.7083, -75.1179)

# MQTT handler
mqtt_handler = MqttHandler()
mqtt_handler.start()  # This starts a blocking loop
```

## MQTT Protocol

### Coordinate Updates

Subscribe to the coordinates topic (`NOAAWeatherCoordinates` by default) to receive location updates:

Format: `"latitude,longitude,tilt,gain,heading"`

Example: `"39.7083,-75.1179,0,0,0"`

### Weather Data

Weather data is published to the weather topic (`NOAAWeatherData` by default) as a JSON string:

```json
{
  "WindSpeed": 10.25,
  "WindDirection": 220.5,
  "Ceiling": 5000,
  "SkyCondition": "BKN",
  "Visibility": 10000,
  "OutsideAirTemperature": 22.5,
  "DewPoint": 15.3,
  "Latitude": 39.7083,
  "Longitude": -75.1179,
  "Elevation": 5.2,
  "Time": "2023-04-01T12:34:56"
}
```

### Radar Images

Radar images are published to the radar image topic (`NEXRADImage` by default) as base64-encoded strings.

## Dependencies

- paho-mqtt
- pyart
- matplotlib
- cartopy
- numpy
- pandas
- boto3
- geopy
- scipy
- netCDF4
- metpy (optional)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

- NOAA for providing weather data
- AWS for hosting NEXRAD radar data
- The Py-ART project for radar tools