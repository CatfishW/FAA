using System;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using UnityEngine;
using System.IO;
using System.Text;
using TMPro;
public class PythonDataReceiver : MonoBehaviour
{

    String IP;
    MqttFactory factory;
    IMqttClient mqttClient;
    IMqttClientOptions options;
    AutoResetEvent semaphore;
    TimeSpan receiveTimeout;
    byte[] data;
    //================
    public bool newData;
    public bool connected;
    public TextMeshPro LongitudeText {set; private get; }
    public TextMeshPro LatitudeText {set; private get; }

    SendToPython sendToPythonReference;
    bool sendNewData;
    public Camera imageCaptureCam;
    void Start()
    {
        newData = false; //Informs internal code when new message comes in that needs to be procesed

        connected = false;
        sendToPythonReference = GetComponent<SendToPython>();
        //This will allow us to send another image to the python IDE for processing

        //----------
        String ValidIP = "127.0.0.1"; //########### This needs to be updated to current ip when testing!

        factory = new MqttFactory();
        mqttClient = factory.CreateMqttClient();
        options = new MqttClientOptionsBuilder()
                .WithClientId("")
                .WithTcpServer(ValidIP, 1883)
                .WithCleanSession()
                .Build();

        mqttClient.ConnectAsync(options, CancellationToken.None);

        semaphore = new AutoResetEvent(false);
        receiveTimeout = TimeSpan.FromSeconds(0.02f);

        mqttClient.UseConnectedHandler(async e =>
        {
            Debug.Log("### CONNECTED TO MQTT CLIENT ###");
            connected = true;
            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic("WeatherPrediction").Build());

            Debug.Log("###SUBSCRIBED TO WeatherPrediction Topic ###");
        });

        mqttClient.UseDisconnectedHandler(async e =>
        {
            Console.WriteLine("### DISCONNECTED FROM SERVER ###");
            try
            {
                await mqttClient.ConnectAsync(options, CancellationToken.None); // Since 3.0.5 with CancellationToken
            }
            catch
            {
                Console.WriteLine("### RECONNECTING FAILED ###");
            }
        });

        //---------------------------------------------------------------------------------

        mqttClient.UseApplicationMessageReceivedHandler(e => //Recieves data packet
        {
            //semaphore.Set(); //Unsure if necessary
           
            data = e.ApplicationMessage.Payload;
            newData = true;
            semaphore.Set();

        }); //end of mqttClient.UseApplicationMessageReceivedHandler

        //mqttClient.UseConnectedHandler
        //await mqttClient.PublishAsync(new TopicFilterBuilder().WithTopic("HelipadCoordinate").Build())
    }

    // Update is called once per frame
    void Update()
    {

        //semaphore.WaitOne((int)receiveTimeout.TotalMilliseconds, true);

        if (data != null && newData == true)
        {
            //string encodedText = Convert.ToBase64String(data);
            string decodedText = System.Text.Encoding.UTF8.GetString(data);
            //Debug.Log("Recieved: " + decodedText);

            string[] stringValues = decodedText.Split(',');
            Debug.Log("Val recieved from python IDE: " + decodedText); //This will be numerical value
            LongitudeText.GetComponent<TextMeshPro>().text = "Longitude: " + stringValues[0];
        }
    }
}












