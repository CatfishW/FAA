import requests
import paho.mqtt.client as mqtt
import json
import time
import logging
import threading
import ssl
import csv
import os
import random
from urllib3.exceptions import InsecureRequestWarning

# Suppress only the specific InsecureRequestWarning
requests.packages.urllib3.disable_warnings(InsecureRequestWarning)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class AircraftDataBridge:
    def __init__(self):
        # MQTT Configuration
        self.broker_address = "agist.org"
        self.broker_port = 1883
        self.client_id = f"PythonAircraftBridge_{int(time.time())}"
        self.mqtt_username = "TangClinic"
        self.mqtt_password = "Tang123"
        
        # MQTT Topics
        self.aircraft_data_topic = "aircraft/traffic"
        self.request_topic = "aircraft/request"
        
        # MQTT Settings
        self.auto_reconnect = True
        self.reconnect_interval = 5
        self.clean_session = True
        
        # API Settings
        self.api_url_airplanes_live = "https://api.airplanes.live/v2/point/"  # expects lat,lon and optional dist (meters)
        self.api_url_opensky = "https://opensky-network.org/api/states/all"
        
        # Publisher settings
        self.publish_interval_sec = 5  # periodic publish interval
        self.enable_periodic_publishing = False  # on-demand by default

        # Connection state
        self.is_connected = False
        self.mqtt_client = None
        self.stop_event = threading.Event()
        self.publisher_thread = None
        
        # Proxy configuration
        self.use_proxy = True
        self.proxies = {}
        self.proxy_list = []
        self.current_proxy = None
        self.proxy_failures = {}
        
        # Load proxies from CSV file
        script_dir = os.path.dirname(os.path.abspath(__file__))
        csv_path = os.path.join(script_dir, "data.csv")
        self.load_proxies_from_csv(csv_path)
        
        # Get a working proxy
        if self.use_proxy and self.proxy_list:
            self.current_proxy = self.find_working_proxy()
            if self.current_proxy:
                self.proxies = {
                    "http": self.current_proxy,
                    "https": self.current_proxy,
                }
                logger.info(f"Using proxy: {self.current_proxy}")
            else:
                logger.warning("No working proxy found, will try direct connection")
                self.use_proxy = False
        
        # Initialize MQTT client
        self.initialize_mqtt_client()
    
    def load_proxies_from_csv(self, csv_file_path):
        """Load proxies from CSV file"""
        try:
            if os.path.exists(csv_file_path):
                with open(csv_file_path, 'r') as file:
                    csv_reader = csv.reader(file)
                    for row in csv_reader:
                        if row and len(row) > 0 and row[0].startswith('socks4://'):
                            self.proxy_list.append(row[0])
                logger.info(f"Loaded {len(self.proxy_list)} proxies from CSV file")
            else:
                logger.error(f"Proxy CSV file not found: {csv_file_path}")
        except Exception as e:
            logger.error(f"Error loading proxies from CSV: {e}")
    
    def test_proxy(self, proxy):
        """Test if a proxy is working"""
        if proxy in self.proxy_failures and self.proxy_failures[proxy] >= 3:
            return False
            
        try:
            test_proxies = {
                "http": proxy,
                "https": proxy,
            }
            response = requests.get("https://opensky-network.org/api/states/all", 
                                  proxies=test_proxies, 
                                  timeout=5, 
                                  verify=False)
            return response.status_code == 200
        except Exception as e:
            if proxy not in self.proxy_failures:
                self.proxy_failures[proxy] = 1
            else:
                self.proxy_failures[proxy] += 1
            logger.debug(f"Proxy {proxy} test failed: {e}")
            return False
    
    def find_working_proxy(self):
        """Find a working proxy from the list"""
        if not self.proxy_list:
            return None
            
        # Make a copy and shuffle to try proxies in random order
        test_proxies = self.proxy_list.copy()
        random.shuffle(test_proxies)
        
        for proxy in test_proxies:
            logger.debug(f"Testing proxy: {proxy}")
            if self.test_proxy(proxy):
                logger.info(f"Found working proxy: {proxy}")
                return proxy
        
        return None
    
    def initialize_mqtt_client(self):
        """Initialize the MQTT client with appropriate callbacks"""
        self.mqtt_client = mqtt.Client(client_id=self.client_id, clean_session=self.clean_session)
        
        # Set up authentication if provided
        if self.mqtt_username and self.mqtt_password:
            self.mqtt_client.username_pw_set(self.mqtt_username, self.mqtt_password)
        
        # Set up callbacks
        self.mqtt_client.on_connect = self.on_connect
        self.mqtt_client.on_disconnect = self.on_disconnect
        self.mqtt_client.on_message = self.on_message
        
        # Auto-reconnect configuration
        self.mqtt_client.reconnect_delay_set(min_delay=1, max_delay=self.reconnect_interval)
    
    def connect(self):
        """Connect to the MQTT broker"""
        try:
            logger.info(f"Connecting to MQTT broker at {self.broker_address}:{self.broker_port}")
            self.mqtt_client.connect(self.broker_address, self.broker_port)
            self.mqtt_client.loop_start()
            return True
        except Exception as e:
            logger.error(f"Failed to connect to MQTT broker: {e}")
            return False
    
    def disconnect(self):
        """Disconnect from the MQTT broker"""
        self.stop_event.set()
        if self.mqtt_client:
            self.mqtt_client.loop_stop()
            self.mqtt_client.disconnect()
            logger.info("Disconnected from MQTT broker")
    
    def on_connect(self, client, userdata, flags, rc):
        """Callback when connected to the MQTT broker"""
        if rc == 0:
            self.is_connected = True
            logger.info("Connected to MQTT broker")
            # Subscribe to the request topic
            client.subscribe(self.request_topic)
            logger.info(f"Subscribed to topic: {self.request_topic}")
            # Publish initial data immediately
            self.fetch_and_publish_data()
            # Start periodic publisher if enabled (off by default for on-demand fetching)
            if self.enable_periodic_publishing and (self.publisher_thread is None or not self.publisher_thread.is_alive()):
                self.publisher_thread = threading.Thread(target=self._periodic_publisher_loop, daemon=True)
                self.publisher_thread.start()
        else:
            logger.error(f"Failed to connect to MQTT broker with result code {rc}")
    
    def on_disconnect(self, client, userdata, rc):
        """Callback when disconnected from the MQTT broker"""
        self.is_connected = False
        if rc != 0:
            logger.warning(f"Unexpected disconnection from MQTT broker: {rc}")
            if self.auto_reconnect:
                logger.info(f"Attempting to reconnect in {self.reconnect_interval} seconds...")
    
    def on_message(self, client, userdata, msg):
        """Callback when message is received - updates are only triggered by messages"""
        try:
            payload = msg.payload.decode()
            topic = msg.topic
            
            logger.info(f"Message received on topic {topic}")
            
            if topic == self.request_topic:
                # Process request for data update
                try:
                    request_data = json.loads(payload)
                    # Store last request parameters to refine subsequent fetches
                    self.last_request_params = request_data
                    if request_data.get("command") == "update":
                        logger.info("Data update requested, fetching fresh data...")
                        self.fetch_and_publish_data()
                    else:
                        logger.info(f"Received command: {request_data.get('command', 'unknown')}")
                except json.JSONDecodeError:
                    # Handle plain text messages
                    if payload.strip().lower() == "update":
                        logger.info("Data update requested via plain text, fetching fresh data...")
                        self.fetch_and_publish_data()
                    else:
                        logger.info(f"Received message: {payload}")
        except Exception as e:
            logger.error(f"Error processing message: {e}")
    
    def fetch_and_publish_data(self):
        """Fetch data from Airplanes.live (preferred) or OpenSky API and publish to MQTT"""
        max_retries = 3
        retry_count = 0
        
        while retry_count < max_retries:
            try:
                request_params = getattr(self, 'last_request_params', None)

                # Try Airplanes.live point endpoint first if we have a center
                response = None
                if request_params and 'center_lat' in request_params and 'center_lon' in request_params:
                    lat = request_params.get('center_lat')
                    lon = request_params.get('center_lon')
                    radius_km = float(request_params.get('radius_km', 250))
                    radius_m = int(max(10000, min(400000, radius_km * 1000)))
                    url = f"{self.api_url_airplanes_live}?lat={lat}&lon={lon}&dist={radius_m}"
                    logger.info(f"Fetching Airplanes.live data near ({lat},{lon}) r={radius_m}m")
                    try:
                        response = requests.get(url, timeout=10)
                        if response.status_code != 200:
                            response = None
                    except Exception as e:
                        logger.debug(f"Airplanes.live request failed: {e}")

                # Fallback to OpenSky global states
                if response is None:
                    url = self.api_url_opensky
                    logger.info("Fetching OpenSky global data")
                    if self.use_proxy and self.current_proxy:
                        response = requests.get(url, proxies=self.proxies, timeout=10, verify=False)
                    else:
                        response = requests.get(url, timeout=10)
                
                if response.status_code == 200:
                    data = response.json()
                    # Normalize Airplanes.live response to OpenSky-like schema if needed
                    if 'ac' in data and isinstance(data['ac'], list):
                        states = []
                        for ac in data['ac']:
                            icao24 = ac.get('icao') or ac.get('hex') or ''
                            callsign = ac.get('call') or ''
                            origin_country = ''
                            lon = ac.get('lon') or 0
                            lat = ac.get('lat') or 0
                            alt_baro = ac.get('alt_baro') or ac.get('alt_baro_ft')
                            if alt_baro is None:
                                alt_baro = 0
                            vel = ac.get('gs') or 0
                            track = ac.get('track') or 0
                            vr = ac.get('roc') or 0
                            on_ground = bool(ac.get('gnd'))
                            state = [
                                icao24,
                                callsign,
                                origin_country,
                                None, None,
                                lon,
                                lat,
                                alt_baro,
                                on_ground,
                                vel,
                                track,
                                vr,
                            ] + [None] * 5
                            states.append(state)
                        data = { 'time': int(time.time()), 'states': states }
                    logger.info(f"Successfully fetched data with {len(data.get('states', []))} aircraft")
                    
                    # Publish the data to the MQTT topic
                    self.mqtt_client.publish(self.aircraft_data_topic, json.dumps(data))
                    logger.info(f"Published aircraft data to {self.aircraft_data_topic}")
                    return
                else:
                    logger.error(f"API request failed with status code: {response.status_code}")
                    retry_count += 1
                    # Try another proxy
                    self.switch_proxy()
            
            except Exception as e:
                logger.error(f"Error fetching or publishing data: {e}")
                retry_count += 1
                
                # Mark current proxy as failed and try another one
                if self.use_proxy and self.current_proxy:
                    if self.current_proxy not in self.proxy_failures:
                        self.proxy_failures[self.current_proxy] = 1
                    else:
                        self.proxy_failures[self.current_proxy] += 1
                    
                    self.switch_proxy()
            
            # Wait before retrying
            if retry_count < max_retries:
                time.sleep(2)
    
    def switch_proxy(self):
        """Switch to another working proxy"""
        logger.info("Switching proxy...")
        new_proxy = self.find_working_proxy()
        
        if new_proxy:
            self.current_proxy = new_proxy
            self.proxies = {
                "http": self.current_proxy,
                "https": self.current_proxy,
            }
            logger.info(f"Switched to proxy: {self.current_proxy}")
        else:
            logger.warning("No working proxy found, trying direct connection")
            self.use_proxy = False
            self.current_proxy = None
            self.proxies = {}
    
    def run(self):
        """Run the main application loop - only updates when messages are received"""
        try:
            if self.connect():
                # Keep the main thread running
                logger.info("MQTT bridge running. Periodic publishing is %s. Press Ctrl+C to stop.", "enabled" if self.enable_periodic_publishing else "disabled")
                while not self.stop_event.is_set():
                    time.sleep(1)
        
        except KeyboardInterrupt:
            logger.info("Stopping MQTT bridge...")
        finally:
            self.disconnect()

    def _periodic_publisher_loop(self):
        """Background thread loop to periodically fetch and publish data"""
        logger.info("Starting periodic publisher thread with interval %ss", self.publish_interval_sec)
        while not self.stop_event.is_set():
            start = time.time()
            try:
                self.fetch_and_publish_data()
            except Exception as e:
                logger.error("Periodic publish error: %s", e)
            # sleep remaining interval (with guard)
            elapsed = time.time() - start
            delay = max(1.0, self.publish_interval_sec - elapsed)
            # Jitter to avoid synchronized calls if multiple instances
            delay += random.random() * 0.5
            self.stop_event.wait(delay)


if __name__ == "__main__":
    bridge = AircraftDataBridge()
    bridge.run()