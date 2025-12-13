using UnityEngine;
using System;
using System.Text;

namespace WeatherRadar
{
    /// <summary>
    /// Weather data provider that integrates with the existing MQTT system.
    /// Receives radar images from the Python backend as Base64-encoded data.
    /// </summary>
    public class MQTTWeatherProvider : WeatherRadarProviderBase
    {
        [Header("MQTT Topics")]
        [SerializeField] private string radarImageTopic = "NEXRADImage";
        [SerializeField] private string coordinatesTopic = "NOAAWeatherCoordinates";
        [SerializeField] private string weatherDataTopic = "NOAAWeatherData";

        [Header("MQTT Connection")]
        [SerializeField] private string brokerAddress = "127.0.0.1";
        [SerializeField] private int brokerPort = 1883;
        [SerializeField] private bool autoConnect = true;

        [Header("Data Settings")]
        [SerializeField] private bool publishPositionUpdates = true;
        [SerializeField] private float positionUpdateInterval = 5f;

        public override string ProviderName => "MQTT Weather (NEXRAD)";

        private float lastPositionUpdate;
        private bool isConnected;
        private byte[] lastImageData;

        // Reference to MQTT client (uses existing project infrastructure)
        private object mqttClient; // Would be MqttClient from your MQTT package

        protected override void Start()
        {
            base.Start();

            if (autoConnect)
            {
                Connect();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Disconnect();
        }

        protected override void Update()
        {
            // Don't call base.Update() - we receive data via MQTT, not generate it

            if (!isConnected) return;

            // Publish position updates periodically
            if (publishPositionUpdates && Time.time - lastPositionUpdate >= positionUpdateInterval)
            {
                lastPositionUpdate = Time.time;
                PublishPositionUpdate();
            }
        }

        /// <summary>
        /// Connect to MQTT broker
        /// </summary>
        public void Connect()
        {
            try
            {
                SetStatus(ProviderStatus.Connecting);
                
                // NOTE: This is a template for MQTT connection
                // You would integrate with your existing MQTT infrastructure here
                // Example using M2Mqtt or similar library:
                
                /*
                mqttClient = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);
                mqttClient.MqttMsgPublishReceived += OnMqttMessage;
                
                string clientId = "UnityWeatherRadar_" + Guid.NewGuid().ToString().Substring(0, 8);
                mqttClient.Connect(clientId);
                
                // Subscribe to radar image topic
                mqttClient.Subscribe(new string[] { radarImageTopic }, new byte[] { 1 });
                */

                // For now, simulate connection
                isConnected = true;
                SetStatus(ProviderStatus.Active);
                
                Debug.Log($"[MQTTWeatherProvider] Connected to {brokerAddress}:{brokerPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MQTTWeatherProvider] Connection failed: {e.Message}");
                SetStatus(ProviderStatus.Error);
                isConnected = false;
            }
        }

        /// <summary>
        /// Disconnect from MQTT broker
        /// </summary>
        public void Disconnect()
        {
            try
            {
                /*
                if (mqttClient != null && mqttClient.IsConnected)
                {
                    mqttClient.Disconnect();
                }
                */
                
                isConnected = false;
                SetStatus(ProviderStatus.Inactive);
                
                Debug.Log("[MQTTWeatherProvider] Disconnected");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MQTTWeatherProvider] Disconnect error: {e.Message}");
            }
        }

        /// <summary>
        /// Handle incoming MQTT messages
        /// </summary>
        private void OnMqttMessage(string topic, byte[] payload)
        {
            if (topic == radarImageTopic)
            {
                ProcessRadarImage(payload);
            }
        }

        /// <summary>
        /// Process Base64-encoded radar image from MQTT
        /// </summary>
        private void ProcessRadarImage(byte[] payload)
        {
            try
            {
                // Decode Base64 string to bytes
                string base64String = Encoding.UTF8.GetString(payload);
                byte[] imageBytes = Convert.FromBase64String(base64String);
                lastImageData = imageBytes;

                // Load image into texture
                if (radarTexture == null)
                {
                    InitializeTexture();
                }

                // Create temporary texture to load PNG
                Texture2D tempTexture = new Texture2D(2, 2);
                if (tempTexture.LoadImage(imageBytes))
                {
                    // Copy to radar texture with proper sizing
                    CopyTextureData(tempTexture);
                    NotifyDataUpdated();
                    
                    Debug.Log($"[MQTTWeatherProvider] Received radar image: {tempTexture.width}x{tempTexture.height}");
                }
                else
                {
                    Debug.LogWarning("[MQTTWeatherProvider] Failed to decode radar image");
                }

                Destroy(tempTexture);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MQTTWeatherProvider] Error processing radar image: {e.Message}");
            }
        }

        private void CopyTextureData(Texture2D source)
        {
            int centerX = textureSize / 2;
            int centerY = textureSize / 2;
            float radius = textureSize / 2f;

            // Scale source to match radar texture size
            float scaleX = (float)source.width / textureSize;
            float scaleY = (float)source.height / textureSize;

            for (int x = 0; x < textureSize; x++)
            {
                for (int y = 0; y < textureSize; y++)
                {
                    // Check if within radar circle
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius)
                    {
                        radarTexture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Sample from source texture
                    int sourceX = Mathf.FloorToInt(x * scaleX);
                    int sourceY = Mathf.FloorToInt(y * scaleY);
                    
                    sourceX = Mathf.Clamp(sourceX, 0, source.width - 1);
                    sourceY = Mathf.Clamp(sourceY, 0, source.height - 1);

                    Color sourceColor = source.GetPixel(sourceX, sourceY);
                    
                    // Apply gain adjustment
                    float gainMultiplier = 1f + (gainDB / 8f);
                    sourceColor.r *= gainMultiplier;
                    sourceColor.g *= gainMultiplier;
                    sourceColor.b *= gainMultiplier;

                    radarTexture.SetPixel(x, y, sourceColor);
                }
            }

            radarTexture.Apply();
        }

        /// <summary>
        /// Publish position update to MQTT for backend to fetch new data
        /// </summary>
        private void PublishPositionUpdate()
        {
            if (!isConnected) return;

            try
            {
                // Format: lat,lon,tilt,gain,heading
                string message = $"{latitude},{longitude},{tiltDegrees},{gainDB},{heading}";
                
                /*
                byte[] payload = Encoding.UTF8.GetBytes(message);
                mqttClient.Publish(coordinatesTopic, payload, 1, false);
                */
                
                Debug.Log($"[MQTTWeatherProvider] Published position: {message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MQTTWeatherProvider] Error publishing position: {e.Message}");
            }
        }

        protected override void GenerateRadarData()
        {
            // This provider receives data via MQTT, not generates it
            // If no data received yet, show empty radar
            if (lastImageData == null)
            {
                ClearTexture();
            }
        }

        public override void SetPosition(float lat, float lon)
        {
            base.SetPosition(lat, lon);
            
            // Trigger immediate position update
            if (isConnected && publishPositionUpdates)
            {
                PublishPositionUpdate();
            }
        }

        public override void SetRange(float range)
        {
            base.SetRange(range);
            
            if (isConnected && publishPositionUpdates)
            {
                PublishPositionUpdate();
            }
        }

        public override void SetTilt(float tilt)
        {
            base.SetTilt(tilt);
            
            if (isConnected && publishPositionUpdates)
            {
                PublishPositionUpdate();
            }
        }

        public override void SetGain(float gain)
        {
            base.SetGain(gain);
            
            if (isConnected && publishPositionUpdates)
            {
                PublishPositionUpdate();
            }
        }

        public override void RefreshData()
        {
            base.RefreshData();
            
            if (isConnected)
            {
                PublishPositionUpdate();
            }
        }

        /// <summary>
        /// Check connection status
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// Set broker connection info
        /// </summary>
        public void SetBrokerInfo(string address, int port)
        {
            brokerAddress = address;
            brokerPort = port;
        }

        /// <summary>
        /// Simulate receiving MQTT data (for testing)
        /// </summary>
        public void SimulateDataReceived(Texture2D testTexture)
        {
            if (testTexture != null)
            {
                CopyTextureData(testTexture);
                NotifyDataUpdated();
            }
        }
    }
}
