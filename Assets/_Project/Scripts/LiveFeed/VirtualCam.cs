using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VirtualCam : MonoBehaviour
{
    public WebCamTexture webcamTexture;
    public RawImage display;
    public int camIndex;
    public WebCamDevice[] WebCamDevices;
    // Start is called before the first frame update
    void Start()
    {
        camIndex = 0;
        display = this.GetComponent<RawImage>();

        WebCamDevices = WebCamTexture.devices;
        for (var i = 0; i < WebCamDevices.Length; i++)
            Debug.Log(WebCamDevices[i].name);

        if(webcamTexture != null)
        {
            display.texture = null;
            webcamTexture.Stop();
            webcamTexture = null;
        }    
            
        webcamTexture = new WebCamTexture(WebCamDevices[camIndex].name);
        display.texture = webcamTexture;
        webcamTexture.Play();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            camIndex += 1;
            webcamTexture.Stop();
            if (camIndex > 2)
            {
                camIndex = 0;
            }
            webcamTexture = new WebCamTexture(WebCamDevices[camIndex].name);
            display.texture = webcamTexture;
            webcamTexture.Play();
        }
    }


}
