using System;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using UnityEngine;
using System.IO;
using System.Text;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Handles receiving weather data from MQTT and displaying radar imagery in Unity
/// </summary>
public class WeatherDataReceiver : MonoBehaviour
{
    #region MQTT Configuration
    [Header("MQTT Settings")]
    [Tooltip("MQTT server IP address")]
    public string mqttServerIp = "agist.org";
    
    [Tooltip("MQTT server port")]
    public int mqttServerPort = 1883;
    
    [Tooltip("MQTT username (optional)")]
    public string mqttUsername = "TangClinic";
    
    [Tooltip("MQTT password (optional)")]
    public string mqttPassword = "Tang123";
    
    [Tooltip("MQTT topic for weather data")]
    public string MQTT_WEATHER_TOPIC = "NOAAWeatherData";
    
    [Tooltip("MQTT topic for radar image data")]
    public string RADAR_IMAGE_DATA_TOPIC = "NEXRADImage";
    
    [Tooltip("MQTT topic for sending coordinates")]
    public string MQTT_COORDINATES_TOPIC = "NOAAWeatherCoordinates";
    
    [Tooltip("How often to send position data (seconds)")]
    [Range(1.0f, 60.0f)]
    public float positionDataSendInterval = 10.0f;
    #endregion

    #region Position and Navigation
    [Header("Position and Navigation")]
    [Tooltip("Aircraft latitude")]
    public float latitude;
    
    [Tooltip("Aircraft longitude")]
    public float longitude;
    
    [Tooltip("Aircraft heading in degrees")]
    public float heading = 0f;
    
    [Tooltip("Reference to Mqtt Traffic Data Manager for coordinates")]
    public MqttTrafficDataManager trafficDataManager;
    
    [Tooltip("Use coordinates from Traffic Data Manager")]
    public bool useTrafficManagerCoordinates = true;
    #endregion

    #region Radar Settings
    [Header("Radar Settings")]
    [Tooltip("Radar tilt angle in degrees")]
    [Range(-10.0f, 10.0f)]
    public float radarTiltAngle = 0f;
    
    [Tooltip("Radar gain value")]
    [Range(0.0f, 10.0f)]
    public float radarGainValue = 0f;
    #endregion

    #region UI References
    [Header("UI References")]
    [Tooltip("Text component to display weather information")]
    public Text weatherInfoText;
    
    [Tooltip("Image component to display radar imagery")]
    public Image radarImageDisplay;
    
    [Tooltip("UI Element for scan line animation")]
    public RectTransform scanLine;
    
    [Tooltip("Slider for controlling radar tilt")]
    public Slider tiltSlider;
    
    [Tooltip("Slider for controlling radar gain")]
    public Slider gainSlider;
    public TMP_Text tiltValueText;
    public TMP_Text gainValueText;
    #endregion

    #region Scan Animation Settings
    [Header("Scan Animation")]
    [Tooltip("Speed of scan animation in degrees per second")]
    public float scanSpeed = 180f;
    
    [Tooltip("Color of the scan line")]
    public Color scanColor = new Color(1, 1, 1, 0.5f);
    #endregion

    #region Private Fields
    // MQTT Client 
    private MqttFactory mqttFactory;
    private IMqttClient mqttClient;
    private IMqttClientOptions mqttOptions;
    
    // Data handling
    private byte[] weatherData;
    private byte[] imagePayload;
    private bool newWeatherDataReceived = false;
    private bool newImageReceived = false;
    private bool mqttConnected = false;
    
    // Scan animation
    private bool isScanning = false;
    private float scanAngle;
    
    // References
    private HeadingHUD headingHUD;
    #endregion

    #region Unity Lifecycle Methods
    /// <summary>
    /// Initialize MQTT connection and UI elements
    /// </summary>
    void Start()
    {
        // Initialize UI components
        InitializeUIComponents();
        
        // Setup MQTT client and connection
        SetupMqttClient();
        
        // Start coroutines for data handling
        StartCoroutine(SendPositionDataPeriodically());
        StartCoroutine(ProcessWeatherDataPeriodically());
        StartCoroutine(ProcessRadarImagePeriodically());
    }

    /// <summary>
    /// Update heading from HeadingHUD and handle scan animation
    /// </summary>
    void Update()
    {
        if (headingHUD != null)
        {
            heading = headingHUD.currentHeading;
        }
        
        UpdateScanAnimation();
    }

    /// <summary>
    /// Clean up MQTT client on destroy
    /// </summary>
    void OnDestroy()
    {
        if (mqttClient != null && mqttClient.IsConnected)
        {
            try 
            {
                mqttClient.DisconnectAsync().Wait();
                mqttClient.Dispose();
                Debug.Log("MQTT client disconnected and disposed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error disconnecting MQTT client: {ex.Message}");
            }
        }
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Initialize UI components and references
    /// </summary>
    private void InitializeUIComponents()
    {
        // Initialize scan line
        if (scanLine != null)
        {
            scanLine.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Scan line not assigned. Radar scan animation will not work.");
        }
        
        // Find and initialize sliders if not assigned in inspector
        if (tiltSlider == null)
        {
            tiltSlider = GameObject.Find("TiltSlider")?.GetComponent<Slider>();
        }
        
        if (gainSlider == null)
        {
            gainSlider = GameObject.Find("GainSlider")?.GetComponent<Slider>();
        }
        
        // Add listeners to sliders
        if (tiltSlider != null)
        {
            tiltSlider.onValueChanged.AddListener(UpdateTiltAngle);
        }
        else
        {
            Debug.LogWarning("Tilt slider not found. Manual tilt control disabled.");
        }
        
        if (gainSlider != null)
        {
            gainSlider.onValueChanged.AddListener(UpdateGainValue);
        }
        else
        {
            Debug.LogWarning("Gain slider not found. Manual gain control disabled.");
        }
        
        // Find HeadingHUD component
        headingHUD = FindObjectOfType<HeadingHUD>();
        if (headingHUD == null)
        {
            Debug.LogWarning("HeadingHUD component not found. Using default heading value.");
        }
    }

    /// <summary>
    /// Setup MQTT client and connection
    /// </summary>
    private void SetupMqttClient()
    {
        try
        {
            mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            
            // Configure MQTT client options
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"UnityWeatherClient-{Guid.NewGuid()}")
                .WithTcpServer(mqttServerIp, mqttServerPort)
                .WithCleanSession();
                
            // Add credentials if provided
            if (!string.IsNullOrEmpty(mqttUsername) && !string.IsNullOrEmpty(mqttPassword))
            {
                optionsBuilder.WithCredentials(mqttUsername, mqttPassword);
            }
            
            mqttOptions = optionsBuilder.Build();
            
            // Setup event handlers
            mqttClient.UseConnectedHandler(async e =>
            {
                Debug.Log("Connected to MQTT broker");
                mqttConnected = true;
                
                // Subscribe to weather data topic
                await mqttClient.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic(MQTT_WEATHER_TOPIC)
                    .Build());
                Debug.Log($"Subscribed to {MQTT_WEATHER_TOPIC}");
                
                // Subscribe to radar image topic
                await mqttClient.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic(RADAR_IMAGE_DATA_TOPIC)
                    .Build());
                Debug.Log($"Subscribed to {RADAR_IMAGE_DATA_TOPIC}");
            });
            
            mqttClient.UseDisconnectedHandler(async e =>
            {
                Debug.LogWarning("Disconnected from MQTT broker");
                mqttConnected = false;
                
                // Try to reconnect
                await TryReconnect();
            });
            
            mqttClient.UseApplicationMessageReceivedHandler(HandleMqttMessage);
            
            // Connect to MQTT broker
            ConnectToMqttBroker();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting up MQTT client: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Connect to the MQTT broker
    /// </summary>
    private async void ConnectToMqttBroker()
    {
        try
        {
            Debug.Log($"Connecting to MQTT broker at {mqttServerIp}:{mqttServerPort}");
            await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to MQTT broker: {ex.Message}");
            // Try to reconnect after delay
            await TryReconnect();
        }
    }
    
    /// <summary>
    /// Try to reconnect to the MQTT broker
    /// </summary>
    private async System.Threading.Tasks.Task TryReconnect()
    {
        try
        {
            if (!mqttClient.IsConnected)
            {
                // Wait a bit before reconnecting
                await System.Threading.Tasks.Task.Delay(5000);
                Debug.Log("Attempting to reconnect to MQTT broker...");
                await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Reconnection attempt failed: {ex.Message}");
        }
    }
    #endregion

    #region MQTT Message Handling
    /// <summary>
    /// Handle incoming MQTT messages
    /// </summary>
    private void HandleMqttMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            // Check which topic the message came from
            if (e.ApplicationMessage.Topic == RADAR_IMAGE_DATA_TOPIC)
            {
                // Store the image payload for processing on the main thread
                imagePayload = e.ApplicationMessage.Payload;
                newImageReceived = true;
                Debug.Log("Received new radar image data");
            }
            else if (e.ApplicationMessage.Topic == MQTT_WEATHER_TOPIC)
            {
                // Store the weather data for processing on the main thread
                weatherData = e.ApplicationMessage.Payload;
                newWeatherDataReceived = true;
                Debug.Log("Received new weather data");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling MQTT message: {ex.Message}");
        }
    }
    #endregion

    #region Coroutines
    /// <summary>
    /// Periodically send position data to MQTT broker
    /// </summary>
    private IEnumerator SendPositionDataPeriodically()
    {
        while (true)
        {
            if (mqttConnected)
            {
                SendPositionData();
            }
            yield return new WaitForSeconds(positionDataSendInterval);
        }
    }
    
    /// <summary>
    /// Periodically process weather data
    /// </summary>
    private IEnumerator ProcessWeatherDataPeriodically()
    {
        while (true)
        {
            if (weatherData != null && newWeatherDataReceived)
            {
                ProcessWeatherData();
                newWeatherDataReceived = false;
                weatherData = null;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// Periodically process radar image data
    /// </summary>
    private IEnumerator ProcessRadarImagePeriodically()
    {
        while (true)
        {
            if (imagePayload != null && newImageReceived)
            {
                ProcessRadarImage();
                newImageReceived = false;
                imagePayload = null;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
    #endregion

    #region Data Processing
    /// <summary>
    /// Send position and radar settings data to MQTT broker
    /// </summary>
    private void SendPositionData()
    {
        try
        {
            // Get coordinates from Traffic Data Manager if enabled
            if (useTrafficManagerCoordinates && trafficDataManager != null)
            {
                // Directly access the properties from the MonoBehaviour
                latitude = trafficDataManager.referenceLatitude;
                longitude = trafficDataManager.referenceLongitude;
                Debug.Log($"Using coordinates from Traffic Data Manager: Lat: {latitude}, Lon: {longitude}");
                
            }
            
            // Format the message with current position and radar settings
            string message = $"{latitude},{longitude},{radarTiltAngle},{radarGainValue},{heading}";
            
            // Build the MQTT message
            var messagePayload = new MqttApplicationMessageBuilder()
                .WithTopic(MQTT_COORDINATES_TOPIC)
                .WithPayload(message)
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();
            
            // Send the message
            mqttClient.PublishAsync(messagePayload, CancellationToken.None);
            Debug.Log($"Sent position data: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending position data: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Process weather data from MQTT
    /// </summary>
    private void ProcessWeatherData()
    {
        try
        {
            // Decode the weather data from UTF-8
            string decodedText = Encoding.UTF8.GetString(weatherData);
            Debug.Log($"Weather data received: {decodedText}");
            
            // Update the UI with the weather information
            if (weatherInfoText != null)
            {
                weatherInfoText.text = decodedText;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing weather data: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Process radar image data from MQTT
    /// </summary>
    private void ProcessRadarImage()
    {
        Debug.Log("Processing radar image data...");
        StartNewScan();
        
        try
        {
            // Convert payload (Base64) to a string
            string base64String = Encoding.UTF8.GetString(imagePayload);
            
            // Decode Base64 string to get image bytes
            byte[] imageBytes = Convert.FromBase64String(base64String);
            
            // Create a temporary texture and load the image bytes
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageBytes))
            {
                // Create a sprite from the texture
                Sprite sprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f)
                );
                
                // Display the sprite on the UI
                if (radarImageDisplay != null)
                {
                    radarImageDisplay.sprite = sprite;
                    Debug.Log("Radar image updated successfully");
                }
                else
                {
                    Debug.LogError("Radar image display component not assigned");
                }
            }
            else
            {
                Debug.LogError("Failed to load image into texture");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to process radar image: {ex.Message}");
        }
    }
    #endregion

    #region UI Interactions
    /// <summary>
    /// Update the radar tilt angle from slider value
    /// </summary>
    public void UpdateTiltAngle(float tiltAngle)
    {
        radarTiltAngle = tiltAngle;
        
        // Update the tilt value text display
        if (tiltValueText != null)
        {
            tiltValueText.text = $"{radarTiltAngle:F1}°";
        }
        
        Debug.Log($"Tilt angle updated: {radarTiltAngle}");
    }
    
    /// <summary>
    /// Update the radar gain value from slider value
    /// </summary>
    public void UpdateGainValue(float gainValue)
    {
        radarGainValue = gainValue;
        
        // Update the gain value text display
        if (gainValueText != null)
        {
            gainValueText.text = $"{radarGainValue:F1}";
        }
        
        Debug.Log($"Gain value updated: {radarGainValue}");
    }
    #endregion

    #region Scan Animation
    /// <summary>
    /// Update the radar scan animation
    /// </summary>
    private void UpdateScanAnimation()
    {
        if (!isScanning || scanLine == null) 
            return;

        // Increment the scan angle
        scanAngle -= scanSpeed * Time.deltaTime;

        // Apply rotation
        scanLine.rotation = Quaternion.Euler(0, 0, scanAngle);

        // Check if scan is complete
        if (scanAngle <= -60f)
        {
            // End the scan and hide the scan line
            isScanning = false;
            scanLine.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Start a new radar scan animation
    /// </summary>
    private void StartNewScan()
    {
        if (scanLine == null)
        {
            Debug.LogWarning("ScanLine RectTransform not assigned");
            return;
        }

        isScanning = true;
        scanAngle = 90f;
        scanLine.rotation = Quaternion.Euler(0f, 0f, scanAngle);
        scanLine.gameObject.SetActive(true);
    }
    #endregion
}












