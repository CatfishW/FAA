import os
import csv
import random
import logging
import requests
import time
from urllib.parse import urlparse

class ProxyTrafficDataManager:
    """
    A class for managing traffic data requests through random proxies
    """
    
    def __init__(self, proxy_csv_path="C:/Users/Tang_/Documents/Unity/FAA_Symbology/Assets/Resources/proxy.csv", max_retries=3):
        """Initialize the proxy manager"""
        self.proxy_list = []
        self.selected_proxy = None
        self.proxy_host = None
        self.proxy_port = None
        self.max_retries = max_retries
        self.verified_proxies = []
        self.failed_proxies = []
        
        self.setup_logging()
        self.load_proxies(proxy_csv_path)
    
    def setup_logging(self):
        """Configure logging"""
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
        )
        self.logger = logging.getLogger("ProxyTrafficManager")
    
    def load_proxies(self, file_path):
        """Load proxy IPs from the proxy CSV file"""
        if os.path.exists(file_path):
            try:
                with open(file_path, 'r') as f:
                    reader = csv.reader(f)
                    for row in reader:
                        if row and row[0].strip():
                            self.proxy_list.append(row[0].strip())
                
                self.logger.info(f"Loaded {len(self.proxy_list)} proxies from {file_path}")
            except Exception as e:
                self.logger.error(f"Error loading proxies: {str(e)}")
        else:
            self.logger.error(f"Proxy file not found at: {file_path}")
    
    def select_random_proxy(self):
        """Select a random proxy from the loaded list, excluding failed ones"""
        available_proxies = [p for p in self.proxy_list if p not in self.failed_proxies]
        
        if not available_proxies:
            self.logger.warning("No available proxies left to try")
            return False
        
        self.selected_proxy = random.choice(available_proxies)
        self.logger.info(f"Selected Proxy: {self.selected_proxy}")
        self.parse_selected_proxy()
        return True
    
    def parse_selected_proxy(self):
        """Parse the selected proxy string into host and port components"""
        if not self.selected_proxy:
            return False
        
        try:
            # Split by comma to handle format like "http://IP:Port,Country"
            proxy_parts = self.selected_proxy.split(',')
            proxy_url = proxy_parts[0].strip()
            
            # Try to parse as a URL if it has a protocol prefix
            if '://' in proxy_url:
                parsed_url = urlparse(proxy_url)
                self.proxy_host = parsed_url.hostname
                self.proxy_port = parsed_url.port or 80
            else:
                # Parse as simple IP:Port format
                address_parts = proxy_url.split(':')
                if len(address_parts) == 2:
                    self.proxy_host = address_parts[0]
                    try:
                        self.proxy_port = int(address_parts[1])
                    except ValueError:
                        self.proxy_port = 80
                        self.logger.warning(f"Invalid proxy port, using default 80: {address_parts[1]}")
                else:
                    self.logger.error(f"Invalid proxy format: {proxy_url}. Expected format: [http://]IP:Port")
                    return False
            
            self.logger.info(f"Parsed proxy: {self.proxy_host}:{self.proxy_port}")
            return True
        
        except Exception as e:
            self.logger.error(f"Error parsing proxy: {str(e)}")
            return False
    
    def verify_proxy(self, test_url="http://172.67.88.34:80", timeout=5):
        """Verify if the current proxy is working"""
        if not self.proxy_host:
            return False
        
        proxy_url = f"http://{self.proxy_host}:{self.proxy_port}"
        proxy_url_https = f"https://{self.proxy_host}:{self.proxy_port}" if self.proxy_port else f"https://{self.proxy_host}"
        proxies = {
            "http": proxy_url,
            "https": proxy_url_https  # Use the same proxy for HTTPS if available
        }
        print(proxies)
        
        try:
            self.logger.info(f"Verifying proxy: {proxy_url}")
            response = requests.get(test_url, proxies=proxies, timeout=timeout)
            if response.status_code == 200:
                self.logger.info(f"Proxy verification successful: {proxy_url}")
                if self.selected_proxy not in self.verified_proxies:
                    self.verified_proxies.append(self.selected_proxy)
                return True
            else:
                self.logger.warning(f"Proxy verification failed with status code: {response.status_code} for {proxy_url}")
                if self.selected_proxy not in self.failed_proxies:
                    self.failed_proxies.append(self.selected_proxy)
                return False
        except Exception as e:
            self.logger.warning(f"Proxy verification failed: {proxy_url}, Error: {str(e)}")
            if self.selected_proxy not in self.failed_proxies:
                self.failed_proxies.append(self.selected_proxy)
        
        return False
    
    def create_api_request(self, url, method="GET", headers=None):
        """Create a request using the selected proxy"""
        if headers is None:
            headers = {}
        
        # Set default headers
        if 'Accept' not in headers:
            headers['Accept'] = 'application/json'
        
        # Apply proxy settings to the request
        proxies = None
        if self.proxy_host and self.proxy_port:
            proxy_url = f"http://{self.proxy_host}:{self.proxy_port}"
            proxies = {
                "http": proxy_url,
                "https": proxy_url
            }
            self.logger.info(f"Setting proxy for request: {proxy_url}")
        
        try:
            # Make the API request
            response = requests.request(
                method=method,
                url=url,
                headers=headers,
                proxies=proxies,
                timeout=30
            )
            
            return response
        except Exception as e:
            self.logger.error(f"Request error: {str(e)}")
            if self.selected_proxy not in self.failed_proxies:
                self.failed_proxies.append(self.selected_proxy)
            return None
    
    def fetch_data(self, url, retry_delay=1):
        """Fetch data from the specified URL using proxies with retry mechanism"""
        retries = 0
        
        while retries <= self.max_retries:
            # Select and verify a working proxy
            proxy_found = False
            for _ in range(min(10, len(self.proxy_list))):  # Try up to 10 proxies
                if self.select_random_proxy() and self.verify_proxy():
                    proxy_found = True
                    break
            
            if not proxy_found:
                self.logger.error("Could not find a working proxy after multiple attempts")
                return None
                
            # Try to fetch data with the working proxy
            response = self.create_api_request(url)
            
            if response and response.status_code == 200:
                self.logger.info(f"Successfully fetched data from {url}")
                return response.json()
            else:
                status = response.status_code if response else "No response"
                self.logger.warning(f"Attempt {retries+1}/{self.max_retries+1} failed. Status: {status}")
                retries += 1
                if retries <= self.max_retries:
                    time.sleep(retry_delay)
        
        self.logger.error(f"Failed to fetch data after {self.max_retries+1} attempts")
        return None

# Example usage
if __name__ == "__main__":
    manager = ProxyTrafficDataManager(max_retries=3)
    data = manager.fetch_data("https://opensky-network.org/api/states/all")
    if data:
        print(f"Received data with {len(data.get('states', []))} aircraft states")