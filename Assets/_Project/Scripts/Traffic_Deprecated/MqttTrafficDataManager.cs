using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

public class MqttTrafficDataManager : TrafficDataManager
{
    public enum DataSourceMode
    {
        MQTT,
        API
    }

    [Header("Data Source Configuration")]
    [SerializeField] private DataSourceMode dataSource = DataSourceMode.MQTT;
    
    [Header("MQTT Connection Settings")]
    [SerializeField] private string brokerAddress = "localhost";
    [SerializeField] private int brokerPort = 1883;
    [SerializeField] private string clientId = "UnityAircraftClient";
    [SerializeField] private string mqttUsername = "";
    [SerializeField] private string mqttPassword = "";
    
    [Header("MQTT Topics")]
    [SerializeField] private string aircraftDataTopic = "aircraft/traffic";
    [SerializeField] private string requestTopic = "aircraft/request";
    
    [Header("MQTT Settings")]
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private float updateInterval = 0.2f;
    [Tooltip("If true, Unity will periodically request updates from the Python bridge.")]
    [SerializeField] private bool requestUpdatesPeriodically = true;
    [Tooltip("Seconds between periodic update requests to the MQTT bridge")]
    [SerializeField] private float requestIntervalSeconds = 5f;

    // Core MQTT components
    private MqttFactory mqttFactory;
    private IMqttClient mqttClient;
    private IMqttClientOptions mqttOptions;
    private bool isConnected = false;
    private float lastUpdateTime;
    private float lastRequestTime;
    
    // Dictionary to store and update aircraft data
    private Dictionary<string, AircraftData> aircraftDict = new Dictionary<string, AircraftData>();

    #region Unity Lifecycle Methods
    protected override void OnEnable()
    {
        if (dataSource == DataSourceMode.MQTT)
        {
            bool originalAutoStart = autoStartFetching;
            autoStartFetching = false;
            base.OnEnable();
            autoStartFetching = originalAutoStart;
            
            InitializeMqttClient();
        }
        else
        {
            base.OnEnable();
        }
    }

    protected override void OnDisable()
    {
        if (dataSource == DataSourceMode.MQTT)
        {
            DisconnectMqttClient();
        }
        
        base.OnDisable();
    }
    
    private void Update()
    {
        if (dataSource == DataSourceMode.MQTT && 
            Time.time - lastUpdateTime >= updateInterval && 
            aircraftList != null)
        {
            lastUpdateTime = Time.time;
            onDataUpdated?.Invoke(aircraftList);
        }

        // Periodically request latest data from the bridge for fresher updates
        if (dataSource == DataSourceMode.MQTT && requestUpdatesPeriodically && Time.time - lastRequestTime >= requestIntervalSeconds)
        {
            lastRequestTime = Time.time;
            _ = RequestDataUpdate();
        }
    }
    
    // Override to control how fetching starts based on data source
    public override void StartFetching()
    {
        if (dataSource == DataSourceMode.API)
        {
            base.StartFetching();
        }
        else if (dataSource == DataSourceMode.MQTT && !isConnected)
        {
            InitializeMqttClient();
        }
    }
    
    public override void StopFetching()
    {
        if (dataSource == DataSourceMode.API)
        {
            base.StopFetching();
        }
        else if (dataSource == DataSourceMode.MQTT)
        {
            DisconnectMqttClient();
        }
    }
    
    public override void FetchDataNow()
    {
        if (dataSource == DataSourceMode.API)
        {
            base.FetchDataNow();
        }
        else if (dataSource == DataSourceMode.MQTT)
        {
            _ = RequestDataUpdate();
        }
    }
    
    private void OnApplicationQuit()
    {
        DisconnectMqttClient();
    }
    #endregion

    #region MQTT Connection Management
    private void InitializeMqttClient()
    {
        try
        {
            mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            
            mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId(clientId + "_" + UnityEngine.Random.Range(1000, 9999))
                .WithTcpServer(brokerAddress, brokerPort)
                .WithCleanSession()
                .Build();

            if (!string.IsNullOrEmpty(mqttUsername))
            {
                mqttOptions = new MqttClientOptionsBuilder()
                    .WithClientId(clientId + "_" + UnityEngine.Random.Range(1000, 9999))
                    .WithTcpServer(brokerAddress, brokerPort)
                    .WithCredentials(mqttUsername, mqttPassword)
                    .WithCleanSession()
                    .Build();
            }

            // Set up handlers
            mqttClient.UseConnectedHandler(async e =>
            {
                isConnected = true;
                Debug.Log($"[MQTT] Connected to broker at {brokerAddress}:{brokerPort}");
                
                // Subscribe to aircraft data topic
                await mqttClient.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic(aircraftDataTopic)
                    .Build());
                
                Debug.Log($"[MQTT] Subscribed to topic: {aircraftDataTopic}");
            });

            mqttClient.UseDisconnectedHandler(async e =>
            {
                isConnected = false;
                Debug.LogWarning($"[MQTT] Disconnected from broker: {e.Exception?.Message}");
                
                if (autoReconnect)
                {
                    await mqttClient.ReconnectAsync();
                }
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                if (e.ApplicationMessage.Topic == aircraftDataTopic)
                {
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    ProcessMessage(payload);
                }
            });

            // Connect to broker
            mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Error initializing client: {ex.Message}");
        }
    }

    private void DisconnectMqttClient()
    {
        if (mqttClient != null && mqttClient.IsConnected)
        {
            try
            {
                mqttClient.DisconnectAsync();
                isConnected = false;
                Debug.Log("[MQTT] Disconnected from broker");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MQTT] Error disconnecting: {ex.Message}");
            }
        }
    }
    #endregion

    #region Message Processing
   private void ProcessMessage(string payload)
    {
        try
        {
            JObject responseObj = JObject.Parse(payload);
            JArray states = (JArray)responseObj["states"];

            if (states != null && states.Count > 0)
            {
                List<AircraftData> updatedAircraft = new List<AircraftData>();

                foreach (JArray state in states)
                {
                    if (state.Count < 17) continue;

                    string icao24 = state[0].ToString().Trim().ToLower();

                    // Get or create aircraft data
                    if (!aircraftDict.TryGetValue(icao24, out AircraftData aircraft))
                    {
                        aircraft = new AircraftData();
                        aircraftDict[icao24] = aircraft;
                    }

                    // Update aircraft data
                    aircraft.icao24 = icao24;
                    aircraft.callsign = state[1].ToString().Trim();
                    aircraft.originCountry = state[2].ToString();
                    aircraft.longitude = state[5].Type != JTokenType.Null ? state[5].Value<float>() : 0;
                    aircraft.latitude = state[6].Type != JTokenType.Null ? state[6].Value<float>() : 0;
                    aircraft.altitude = state[7].Type != JTokenType.Null ? state[7].Value<float>() : 0;
                    aircraft.onGround = state[8].Type != JTokenType.Null ? state[8].Value<bool>() : false;
                    aircraft.velocity = state[9].Type != JTokenType.Null ? state[9].Value<float>() : 0;
                    aircraft.heading = state[10].Type != JTokenType.Null ? state[10].Value<float>() : 0;
                    aircraft.verticalRate = state[11].Type != JTokenType.Null ? state[11].Value<float>() : 0;
                    aircraft.lastUpdateTime = DateTime.Now;
                    aircraft.type = DetermineAircraftType(aircraft);

                    // Apply radius filter if enabled
                    if (radiusFilterKm > 0)
                    {
                        float distance = CalculateDistanceKm(referenceLatitude, referenceLongitude, aircraft.latitude, aircraft.longitude);
                        if (distance > radiusFilterKm)
                        {
                            continue; // Skip aircraft outside the radius
                        }
                    }

                    // Only include aircraft with valid positions
                    if (aircraft.latitude != 0 && aircraft.longitude != 0)
                    {
                        updatedAircraft.Add(aircraft);
                    }
                }

                if (updatedAircraft.Count > 0)
                {
                    aircraftList = updatedAircraft;
                    aircraftMap = new Dictionary<string, AircraftData>();
                    foreach (var aircraft in updatedAircraft)
                    {
                        aircraftMap[aircraft.icao24] = aircraft;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Failed to parse message: {ex.Message}");
        }
    }
    
    private AircraftType DetermineAircraftType(AircraftData aircraft)
    {
        if (aircraft.callsign.Contains("MIL") || 
            aircraft.callsign.Contains("RCH") ||
            aircraft.callsign.Contains("NAVY"))
            return AircraftType.Military;
        
        if (aircraft.altitude > 7000 && !string.IsNullOrEmpty(aircraft.callsign))
            return AircraftType.Commercial;
        
        if (aircraft.altitude < 1500 && aircraft.velocity < 80)
            return AircraftType.Helicopter;
        
        if (aircraft.altitude < 5000)
            return AircraftType.General;
        
        return AircraftType.Unknown;
    }
    #endregion

    #region Public Methods
    public async Task RequestDataUpdate()
    {
        if (!isConnected || mqttClient == null)
        {
            Debug.LogWarning("[MQTT] Cannot request data: not connected to broker");
            return;
        }

        try
        {
            // Include current center and radius filter to help the bridge fetch focused data
            var requestPayload = new Dictionary<string, object>();
            requestPayload["command"] = "update";
            requestPayload["timestamp"] = DateTimeOffset.Now.ToUnixTimeSeconds();
            requestPayload["radius_km"] = radiusFilterKm > 0 ? radiusFilterKm : 250f;
            requestPayload["center_lat"] = referenceLatitude;
            requestPayload["center_lon"] = referenceLongitude;
            string requestMessage = JsonConvert.SerializeObject(requestPayload);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(requestTopic)
                .WithPayload(requestMessage)
                .Build();

            await mqttClient.PublishAsync(message);
            Debug.Log("[MQTT] Data update requested");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Failed to request data: {ex.Message}");
        }
    }

    public bool IsConnected()
    {
        return isConnected && mqttClient != null && mqttClient.IsConnected;
    }

    public async Task ForceReconnect()
    {
        DisconnectMqttClient();
        await Task.Delay(1000); 
        InitializeMqttClient();
    }
    #endregion
}