"""
MQTT Handler for MqttWeather
Handles communication with MQTT broker
"""
import time
import paho.mqtt.client as mqtt
import json
import base64
import threading
import logging
from .WeatherService import WeatherService
from .RadarDataService import RadarDataService
from .RadarVisualizationService import RadarVisualizationService

# Set up logging
logging.basicConfig(level=logging.INFO,
                   format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class MqttHandler:
    """
    Handles MQTT communication for MqttWeather
    """
    def __init__(self, broker="localhost", port=1883, username=None, password=None,
                weather_topic="NOAAWeatherData", coordinates_topic="NOAAWeatherCoordinates",
                radar_image_topic="NEXRADImage"):
        """
        Initialize MQTT handler
        
        Parameters
        ----------
        broker : str
            MQTT broker address
        port : int
            MQTT broker port
        username : str, optional
            MQTT username
        password : str, optional
            MQTT password
        weather_topic : str
            MQTT topic for weather data
        coordinates_topic : str
            MQTT topic for coordinates
        radar_image_topic : str
            MQTT topic for radar images
        """
        # MQTT settings
        self.broker = broker
        self.port = port
        self.username = username
        self.password = password
        self.weather_topic = weather_topic
        self.coordinates_topic = coordinates_topic
        self.radar_image_topic = radar_image_topic
        
        # Initialize MQTT client
        self.client = mqtt.Client()
        self.client.on_connect = self.on_connect
        self.client.on_message = self.on_message
        self.client.on_disconnect = self.on_disconnect
        
        # Set credentials if provided
        if username is not None and password is not None:
            self.client.username_pw_set(username, password)
            
        # Initialize services
        self.weather_service = WeatherService()
        self.radar_service = RadarDataService()
        self.visualization_service = RadarVisualizationService()
        
        # Default position
        self.user_lat = 39.7083
        self.user_lon = -75.1179
        self.heading = 0
        self.tilt = 0
        self.gain = 0
        
        # Flag to check if position has been updated
        self.position_received = False
        self.last_processed_position = None
        
        # Lock for thread safety
        self.lock = threading.Lock()
        
        # Initialize running state
        self.running = False

    def on_connect(self, client, userdata, flags, rc):
        """
        Callback for when the client connects to the broker
        """
        if rc == 0:
            logger.info(f"Connected to MQTT broker at {self.broker}:{self.port}")
            # Subscribe to coordinates topic
            client.subscribe(self.coordinates_topic)
            logger.info(f"Subscribed to {self.coordinates_topic}")
        else:
            logger.error(f"Failed to connect to MQTT broker, return code: {rc}")
    
    def on_message(self, client, userdata, msg):
        """
        Callback for when a message is received from the broker
        """
        if msg.topic == self.coordinates_topic:
            # Parse coordinates message
            try:
                message = msg.payload.decode("utf-8")
                parts = message.split(',')
                
                if len(parts) >= 2:
                    new_lat = float(parts[0])
                    new_lon = float(parts[1])
                    
                    # Get tilt and gain if available
                    new_tilt = float(parts[2]) if len(parts) > 2 else 0
                    new_gain = float(parts[3]) if len(parts) > 3 else 0
                    new_heading = float(parts[4]) if len(parts) > 4 else 0
                    
                    # Check if position has changed enough to warrant an update
                    position_changed = False
                    
                    with self.lock:
                        # Calculate change in position
                        if self.last_processed_position is None:
                            position_changed = True
                        else:
                            last_lat, last_lon, last_tilt, last_gain, last_heading = self.last_processed_position
                            # Check if position, tilt, gain or heading has changed
                            if (abs(new_lat - last_lat) > 0.0001 or 
                                abs(new_lon - last_lon) > 0.0001 or
                                abs(new_tilt - last_tilt) > 0.1 or
                                abs(new_gain - last_gain) > 0.1 or
                                abs(new_heading - last_heading) > 1.0):
                                position_changed = True
                                
                        # Update position
                        self.user_lat = new_lat
                        self.user_lon = new_lon
                        self.tilt = new_tilt
                        self.gain = new_gain
                        self.heading = new_heading
                        
                        # Mark that position has been received
                        self.position_received = True
                        
                        if position_changed:
                            # Store the position for future comparisons
                            self.last_processed_position = (new_lat, new_lon, new_tilt, new_gain, new_heading)
                            logger.info(f"Position updated: lat={new_lat}, lon={new_lon}, " +
                                      f"tilt={new_tilt}, gain={new_gain}, heading={new_heading}")
                            
                            # Publish weather data and radar image immediately when position changes
                            threading.Thread(target=self.publish_data).start()
                        else:
                            logger.info("Position update received but no significant change detected")
                    
            except Exception as e:
                logger.error(f"Error parsing coordinates message: {e}")
    
    def on_disconnect(self, client, userdata, rc):
        """
        Callback for when the client disconnects from the broker
        """
        if rc != 0:
            logger.warning(f"Unexpected disconnection from MQTT broker, return code: {rc}")
        else:
            logger.info("Disconnected from MQTT broker")
            
    def publish_data(self):
        """
        Publish weather data and radar image to MQTT topics
        """
        with self.lock:
            if not self.position_received:
                logger.info("No position data received yet, skipping data publishing")
                return
                
            # Get current position
            lat = self.user_lat
            lon = self.user_lon
            tilt = self.tilt
            gain = self.gain
            heading = self.heading
        
        try:
            # Get weather data
            weather_data = self.weather_service.fetch_real_weather_data(
                lat, lon)
                
            # Generate radar visualization
            image_path = self.visualization_service.generate_visualization(
                user_lat=lat, 
                user_lon=lon, 
                heading=heading,
                tilt=tilt,
                gain=gain
            )
            
            # Publish weather data to MQTT
            self.client.publish(self.weather_topic, weather_data, qos=1, retain=True)
            logger.info(f"Published weather data to {self.weather_topic}")
            
            # Publish radar image to MQTT
            with open(image_path, "rb") as img_file:
                img_data = img_file.read()
                # Encode image data as base64 string
                b64_data = base64.b64encode(img_data).decode("utf-8")
                self.client.publish(self.radar_image_topic, b64_data, qos=1, retain=True)
                logger.info(f"Published radar image to {self.radar_image_topic}")
                
        except Exception as e:
            logger.error(f"Error publishing data: {e}")
            
    def start(self):
        """
        Connect to MQTT broker and start publishing data
        """
        try:
            # Connect to MQTT broker
            self.client.connect(self.broker, self.port, 60)
            
            # Start MQTT loop in a background thread
            self.client.loop_start()
            
            # Mark as running
            self.running = True
            
            # Keep running until interrupted
            while self.running:
                # We no longer publish data at regular intervals
                # Data will only be published when a position update is received
                time.sleep(1)
                
        except KeyboardInterrupt:
            logger.info("Interrupted by user")
        except Exception as e:
            logger.error(f"Error in MQTT handler: {e}")
        finally:
            # Clean up
            self.stop()
    
    def stop(self):
        """
        Stop the MQTT handler
        """
        self.running = False
        self.client.loop_stop()
        self.client.disconnect()
        logger.info("MQTT handler stopped")