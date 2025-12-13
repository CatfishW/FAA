using System;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections;

public class SendToPython : MonoBehaviour
{
    public int width, height;

    //================
    String IP;
    MqttFactory factory;
    IMqttClient mqttClient;
    IMqttClientOptions options;
    AutoResetEvent semaphore;
    TimeSpan receiveTimeout;
    byte[] data;
    byte[] imageToSend;
    //================

    public bool statusRunning;

    public bool connected;
    public bool startProgram;
    public bool sendNewData; //This will be set to true by RecieveFromPython when we are ready to send more data to the IDE
                             //This is so we do not constantly publish new data every frame

    public Camera imageCaptureCam;

    void Start()
    {
        statusRunning = false;
        connected = false;
        sendNewData = true;
        startProgram = false;
        //----------
        String ValidIP = "127.0.0.1"; //########### This needs to be updated to current ip when testing!
        String PersistentPath = Application.persistentDataPath;
        DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
        FileInfo[] info = dir.GetFiles("*.*");

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

        mqttClient.UseConnectedHandler(async e =>
        {
            Debug.Log("### CONNECTED TO MQTT CLIENT ###");
            connected = true;

            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic("ImageData").Build());

            Debug.Log("###SUBSCRIBED TO ImageData Topic ###");
        });
    }

    // Update is called once per frame
    void Update()
    {
        //semaphore.WaitOne((int)receiveTimeout.TotalMilliseconds, true);

  

        if (connected == true && sendNewData == true)
        {
            sendNewData = false;
            StartCoroutine(CaptureImage());
            

        }

    }

    IEnumerator SendData() //Send the data
    {
        if (mqttClient.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("ImageData") //Publish message to Imagedata topic
                .WithPayload(imageToSend)
                .Build();
            mqttClient.PublishAsync(message, CancellationToken.None);
            Debug.Log("Sending image to python");
        }

        yield return new WaitForSeconds(0.0f);
    }

    IEnumerator CaptureImage() //Capture image from camera and set bytes to imageToSend
    {
        //var imagePath = @"C:\Users\grant\Documents\College\Grad\MLImages";

        RenderTexture rt = new RenderTexture(width, height, 24);
        imageCaptureCam.targetTexture = rt;
        Texture2D tex = new Texture2D(width, width, TextureFormat.RGB24, false);

        yield return new WaitForEndOfFrame();//Make sure the frame has ended before capturing the texture;

        imageCaptureCam.Render();
        RenderTexture.active = rt;

        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        byte[] bytes; //these need to be sent
        bytes = tex.EncodeToPNG();

        imageToSend = bytes;
        //send these bytes
        //System.IO.File.WriteAllBytes(tempPath, bytes);

        StartCoroutine(SendData());
    }











}
